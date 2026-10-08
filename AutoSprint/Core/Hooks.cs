using System;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.CameraModes;
using UnityEngine;

namespace AutoSprint.Core
{
    internal class Hooks
    {
        public static Hooks Instance { get; private set; }
        public static void Init() => Instance ??= new Hooks();
        private Hooks()
        {
            On.RoR2.EntityStateCatalog.Init += StateManager.EntityStateCatalog_Init;

            SettingsChanged();
            AutoSprintPlugin.Instance.Config.SettingChanged += SettingsChanged;
        }

        private void SettingsChanged(object _ = null, EventArgs __ = null)
        {
            bool enableMod = PluginConfig.EnableMod.Value;
            if (enableMod)
            {
                MainHooksEnabled = true;
                DebugModeEnabled = PluginConfig.EnableDebugMode.Value;
                FovSliderEnabled = PluginConfig.FovSlider.Value > 0;
                ForceFovEnabled = PluginConfig.DisableSprintingFOV.Value || PluginConfig.ForceSprintingFOV.Value;
                DisableSprintingSpeedLinesEnabled = PluginConfig.DisableSprintingSpeedLines.Value;
                DisableSprintingCrosshairEnabled = PluginConfig.DisableSprintingCrosshair.Value;
            }
            else
            {
                MainHooksEnabled = false;
                DebugModeEnabled = false;
                FovSliderEnabled = false;
                ForceFovEnabled = false;
                DisableSprintingSpeedLinesEnabled = false;
                DisableSprintingCrosshairEnabled = false;
            }
        }

        private bool MainHooksEnabled
        {
            set
            {
                if (field == value)
                    return;
                field = value;
                if (field)
                {
                    IL.RoR2.PlayerCharacterMasterController.PollButtonInput += PlayerCharacterMasterController_PollButtonInput;
                }
                else
                {
                    IL.RoR2.PlayerCharacterMasterController.PollButtonInput -= PlayerCharacterMasterController_PollButtonInput;
                }
            }
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
                c.Emit(OpCodes.Stloc, isSprintingLoc);
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
                c.EmitDelegate(() => PluginConfig.EnableOmniSprint.Value);
                c.Emit(OpCodes.Brtrue, label);
            }
            else
                Log.Error("AutoSprint IL hook for PlayerCharacterMasterController_PollButtonInput - Enable Omni Sprint failed");
        }

        private bool DebugModeEnabled
        {
            set
            {
                if (field == value)
                    return;
                field = value;
                if (field)
                {
                    On.RoR2.EntityStateMachine.SetState += EntityStateMachine_SetState;
                }
                else
                {
                    On.RoR2.EntityStateMachine.SetState -= EntityStateMachine_SetState;
                }
            }
        }

        private static void EntityStateMachine_SetState(On.RoR2.EntityStateMachine.orig_SetState orig, EntityStateMachine self, EntityStates.EntityState newState)
        {
            if (self.networker && self.networker == AutoSprintManager.CachedStateMachine && newState != null)
            {
                var newType = newState.GetType();
                var stateIndex = EntityStateCatalog.GetStateIndex(newType);
                bool canSprint = StateManager.EntityStateDisabledSet.Contains(stateIndex) || StateManager.SprintStateDelayTable.ContainsKey(stateIndex);
                
                Log.Info($"Can Sprint? {canSprint}  |  {newType.FullName}");
            }

            orig(self, newState);
        }

        private bool FovSliderEnabled
        {
            set
            {
                if (field == value)
                    return;
                field = value;
                if (field)
                {
                    IL.RoR2.CameraModes.CameraModePlayerBasic.UpdateInternal += CameraModePlayerBasic_UpdateInternal2;
                }
                else
                {
                    IL.RoR2.CameraModes.CameraModePlayerBasic.UpdateInternal -= CameraModePlayerBasic_UpdateInternal2;
                }
            }
        }

        private static void CameraModePlayerBasic_UpdateInternal2(ILContext il)
        {
            ILCursor[] cList = null;
            if (!new ILCursor(il).TryFindNext(out cList,
                    x => x.MatchLdfld<CameraModeBase.CameraInfo>(nameof(CameraModeBase.CameraInfo.baseFov)),
                    x => x.MatchLdfld<CameraRigController>(nameof(CameraRigController.baseFov))
                ))
            {
                Log.Error("AutoSprint IL hook for CameraModePlayerBasic_UpdateInternal Custom FOV failed");
                return;
            }

            cList[0].Index++;
            cList[0].EmitDelegate<Func<float, float>>((fov) => fov + PluginConfig.FovSlider.Value);

            cList[1].Index++;
            cList[1].EmitDelegate<Func<float, float>>((fov) => fov + PluginConfig.FovSlider.Value);
        }

        private bool ForceFovEnabled
        {
            set
            {
                if (field == value)
                    return;
                field = value;
                if (field)
                {
                    IL.RoR2.CameraModes.CameraModePlayerBasic.UpdateInternal += CameraModePlayerBasic_UpdateInternal;
                }
                else
                {
                    IL.RoR2.CameraModes.CameraModePlayerBasic.UpdateInternal -= CameraModePlayerBasic_UpdateInternal;
                }
            }
        }

        private static void CameraModePlayerBasic_UpdateInternal(ILContext il)
        {
            var c = new ILCursor(il);
            ILCursor[] c1 = null;
            ILLabel noFovLabel = null;

            if (c.TryGotoNext(
                    x => x.MatchLdarg(out _),
                    x => x.MatchLdflda<CameraModeBase.CameraModeContext>(nameof(CameraModePlayerBasic.CameraModeContext.targetInfo)),
                    x => x.MatchLdfld<CameraModeBase.TargetInfo>(nameof(CameraModeBase.TargetInfo.isSprinting))) &&
                c.TryFindNext(out c1,
                    x => x.MatchBrfalse(out noFovLabel)
                ))
            {
                c1[0].Index++;
                var setFovLabel = c1[0].MarkLabel();

                c.EmitDelegate(() => PluginConfig.ForceSprintingFOV.Value);
                c.Emit(OpCodes.Brtrue, setFovLabel);

                c.EmitDelegate(() => PluginConfig.DisableSprintingFOV.Value);
                c.Emit(OpCodes.Brtrue, noFovLabel);
            }
            else
                Log.Error("AutoSprint IL hook for CameraModePlayerBasic_UpdateInternal Sprinting FOV failed");

        }

        private bool DisableSprintingSpeedLinesEnabled
        {
            set
            {
                if (field == value)
                    return;
                field = value;
                if (field)
                {
                    On.RoR2.CameraRigController.SetParticleSystemActive += CameraRigController_SetParticleSystemActive;
                    On.RoR2.CameraRigController.SetSprintParticlesActive += CameraRigController_SetSprintParticlesActive;
                }
                else
                {
                    On.RoR2.CameraRigController.SetParticleSystemActive -= CameraRigController_SetParticleSystemActive;
                    On.RoR2.CameraRigController.SetSprintParticlesActive -= CameraRigController_SetSprintParticlesActive;
                }
            }
        }

        private static void CameraRigController_SetSprintParticlesActive(On.RoR2.CameraRigController.orig_SetSprintParticlesActive orig, CameraRigController self, bool newSprintParticlesActive)
        {
            orig(self, false);
        }

        private static void CameraRigController_SetParticleSystemActive(On.RoR2.CameraRigController.orig_SetParticleSystemActive orig, CameraRigController self, bool newParticlesActive, ParticleSystem particleSystem)
        {
            if (particleSystem == self.sprintingParticleSystem)
            {
                newParticlesActive = false;
            }

            orig(self, newParticlesActive, particleSystem);
        }

        private bool DisableSprintingCrosshairEnabled
        {
            set
            {
                if (field == value)
                    return;
                field = value;
                if (field)
                {
                    IL.RoR2.UI.CrosshairManager.UpdateCrosshair += CrosshairManager_UpdateCrosshair;
                }
                else
                {
                    IL.RoR2.UI.CrosshairManager.UpdateCrosshair -= CrosshairManager_UpdateCrosshair;
                }
            }
        }

        private static void CrosshairManager_UpdateCrosshair(ILContext il)
        {
            var c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchCallOrCallvirt(AccessTools.PropertyGetter(typeof(CharacterBody), nameof(CharacterBody.isSprinting)))
                ))
            {
                c.EmitDelegate<Func<bool, bool>>((val) => false);
            }
            else
                Log.Error("AutoSprint IL hook for CrosshairManager_UpdateCrosshair failed");
        }
    }
}
