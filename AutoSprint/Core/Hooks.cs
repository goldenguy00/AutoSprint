using System;
using System.Collections;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.CameraModes;

namespace AutoSprint.Core
{
    internal class Hooks
    {
        public static Hooks Instance { get; private set; }

        public static void Init() => Instance ??= new Hooks();

        private bool _hooksEnabled;

        private Hooks()
        {
            EnableDebugMode_SettingChanged(null, null);

            PluginConfig.EnableDebugMode.SettingChanged += EnableDebugMode_SettingChanged;

            On.RoR2.EntityStateCatalog.Init += EntityStateCatalog_Init;

            IL.RoR2.PlayerCharacterMasterController.PollButtonInput += PlayerCharacterMasterController_PollButtonInput;
            IL.RoR2.UI.CrosshairManager.UpdateCrosshair += CrosshairManager_UpdateCrosshair;
            IL.RoR2.CameraModes.CameraModePlayerBasic.UpdateInternal += CameraModePlayerBasic_UpdateInternal;
            IL.RoR2.CameraModes.CameraModePlayerBasic.UpdateInternal += CameraModePlayerBasic_UpdateInternal2;
        }

        private void EnableDebugMode_SettingChanged(object sender, EventArgs e)
        {
            if (_hooksEnabled != PluginConfig.EnableDebugMode.Value)
            {
                _hooksEnabled = PluginConfig.EnableDebugMode.Value;

                if (_hooksEnabled)
                    On.EntityStates.EntityState.OnEnter += EntityState_OnEnter;
                else
                    On.EntityStates.EntityState.OnEnter -= EntityState_OnEnter;
            }
        }

        private static void EntityState_OnEnter(On.EntityStates.EntityState.orig_OnEnter orig, EntityStates.EntityState self)
        {
            orig(self);

            if (self.characterBody && self.characterBody == AutoSprintManager.CachedBody)
                Log.Info(self.GetType().FullName);
        }

        private static IEnumerator EntityStateCatalog_Init(On.RoR2.EntityStateCatalog.orig_Init orig)
        {
            yield return orig();

            for (var k = 0; k < EntityStateCatalog.stateIndexToType.Length; k++)
            {
                StateManager.TypeFullNameToStateIndex[EntityStateCatalog.stateIndexToType[k].FullName] = (EntityStateIndex)k;
            }

            yield return null;

            StateManager.UpdateDisabledStates(null, null);
            StateManager.UpdateDelayStates(null, null);

            PluginConfig.DisableSprintingCustomList.SettingChanged += StateManager.UpdateDisabledStates;
            PluginConfig.DisableSprintingCustomList2.SettingChanged += StateManager.UpdateDelayStates;
        }

        private static void CameraModePlayerBasic_UpdateInternal2(ILContext il)
        {
            var c0 = new ILCursor(il);
            ILCursor[] cList = null;
            int camParamsLoc = 0;

            if (!c0.TryFindNext(out cList,
                    x => x.MatchLdfld<CameraModeBase.CameraInfo>(nameof(CameraModeBase.CameraInfo.baseFov)),
                    x => x.MatchLdfld<CameraRigController>(nameof(CameraRigController.baseFov))) ||
                !c0.TryGotoNext(MoveType.After,
                    x => x.MatchLdloc(out camParamsLoc),
                    x => x.MatchCall<UnityEngine.Object>("op_Implicit"),
                    x => x.MatchBrfalse(out _)
                ))
            {
                Log.Error("AutoSprint IL hook for CameraModePlayerBasic_UpdateInternal Custom FOV failed");
                return;
            }

            cList[0].Index++;
            cList[0].EmitDelegate<Func<float, float>>((fov) => fov + PluginConfig.FovSlider.Value);

            cList[1].Index++;
            cList[1].EmitDelegate<Func<float, float>>((fov) => fov + PluginConfig.FovSlider.Value);

            c0.Emit(OpCodes.Ldloc, camParamsLoc);
            c0.EmitDelegate<Action<CameraTargetParams>>((camParams) => camParams.currentCameraParamsData.fov.value = PluginConfig.FovSlider.Value);
        }

        private static void CameraModePlayerBasic_UpdateInternal(ILContext il)
        {
            var c = new ILCursor(il);
            ILCursor[] c1 = null;
            ILLabel noFovLabel = null;

            if (c.TryGotoNext(
                    x => x.MatchLdarg(out _),
                    x => x.MatchLdflda<CameraModeBase.CameraModeContext>(nameof(CameraModeBase.CameraModeContext.targetInfo)),
                    x => x.MatchLdfld<CameraModeBase.TargetInfo>(nameof(CameraModeBase.TargetInfo.isSprinting))) &&
                c.TryFindNext(out c1,
                    x => x.MatchBrfalse(out noFovLabel)
                ))
            {
                c1[0].Index++;
                var setFovLabel = c1[0].MarkLabel();

                c.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(PluginConfig), nameof(PluginConfig.ForceSprintingFOV)));
                c.Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<bool>), nameof(ConfigEntry<bool>.Value)));
                c.Emit(OpCodes.Brtrue, setFovLabel);

                c.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(PluginConfig), nameof(PluginConfig.DisableSprintingFOV)));
                c.Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<bool>), nameof(ConfigEntry<bool>.Value)));
                c.Emit(OpCodes.Brtrue, noFovLabel);
            }
            else
                Log.Error("AutoSprint IL hook for CameraModePlayerBasic_UpdateInternal Sprinting FOV failed");

        }

        private static void PlayerCharacterMasterController_PollButtonInput(ILContext il)
        {
            var c = new ILCursor(il);

            var playerLoc = 0;
            var isSprintingLoc = 0;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCallOrCallvirt(AccessTools.PropertyGetter(typeof(PlayerCharacterMasterController), nameof(PlayerCharacterMasterController.networkUser))),
                    x => x.MatchLdloca(out _),
                    x => x.MatchLdloca(out playerLoc)) &&
                c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<PlayerCharacterMasterController>(nameof(PlayerCharacterMasterController.body)),
                    x => x.MatchCallOrCallvirt(AccessTools.PropertyGetter(typeof(CharacterBody), nameof(CharacterBody.isSprinting)))) &&
                c.TryGotoNext(MoveType.After,
                    x => x.MatchStloc(out isSprintingLoc)
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, playerLoc);
                c.Emit(OpCodes.Ldloc, isSprintingLoc);
                c.EmitDelegate(AutoSprintManager.TryHandleSprint);
            }
            else
                Log.Error("AutoSprint IL hook for PlayerCharacterMasterController_PollButtonInput failed");

            ILLabel label = null;
            if (c.TryGotoNext(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<PlayerCharacterMasterController>(nameof(PlayerCharacterMasterController.body)),
                    x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.bodyFlags)) &&
                c.TryFindNext(out _,
                    x => x.MatchBrtrue(out label))
                ))
            {
                c.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(PluginConfig), nameof(PluginConfig.EnableOmniSprint)));
                c.Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<bool>), nameof(ConfigEntry<bool>.Value)));
                c.Emit(OpCodes.Brtrue, label);
            }
            else
                Log.Error("AutoSprint IL hook for PlayerCharacterMasterController_PollButtonInput - Enable Omni Sprint failed");
        }

        private static void CrosshairManager_UpdateCrosshair(ILContext il)
        {
            var c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchCallOrCallvirt(AccessTools.PropertyGetter(typeof(CharacterBody), nameof(CharacterBody.isSprinting)))
                ))
            {
                c.EmitDelegate<Func<bool, bool>>((val) => val && !PluginConfig.DisableSprintingCrosshair.Value);
            }
            else
                Log.Error("AutoSprint IL hook for CrosshairManager_UpdateCrosshair failed");
        }
    }
}
