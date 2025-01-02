using System;
using System.Collections;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.CameraModes;
using RoR2.Skills;

namespace AutoSprint
{
    internal class Hooks
    {
        public static Hooks Instance { get; private set; }

        public static void Init() => Instance ??= new Hooks();

        private static bool hooksEnabled;

        private Hooks()
        {
            EnableDebugMode_SettingChanged(null, null);
            PluginConfig.EnableDebugMode.SettingChanged += EnableDebugMode_SettingChanged;
            On.RoR2.EntityStateCatalog.Init += EntityStateCatalog_Init;
            On.RoR2.Skills.SkillCatalog.Init += SkillCatalog_Init;

            IL.RoR2.PlayerCharacterMasterController.PollButtonInput += PlayerCharacterMasterController_PollButtonInput;
            IL.RoR2.UI.CrosshairManager.UpdateCrosshair += CrosshairManager_UpdateCrosshair;
            IL.RoR2.CameraModes.CameraModePlayerBasic.UpdateInternal += CameraModePlayerBasic_UpdateInternal;
        }

        private static void CameraModePlayerBasic_UpdateInternal(ILContext il)
        {
            var c = new ILCursor(il);

            ILLabel noFovLabel = null;
            ILCursor[] c1 = null;
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
                Log.Error("ILHook failed for CameraModePlayerBasic_UpdateInternal");
        }

        private static void EnableDebugMode_SettingChanged(object sender, EventArgs e)
        {
            if (PluginConfig.EnableDebugMode.Value)
            {
                if (!hooksEnabled)
                {
                    On.EntityStates.EntityState.OnEnter += EntityState_OnEnter;
                    hooksEnabled = true;
                }
            }
            else
            {
                if (hooksEnabled)
                {
                    On.EntityStates.EntityState.OnEnter -= EntityState_OnEnter;
                    hooksEnabled = false;
                }
            }
        }

        private static void EntityState_OnEnter(On.EntityStates.EntityState.orig_OnEnter orig, EntityStates.EntityState self)
        {
            orig(self);
            if (self.characterBody && self.characterBody == AutoSprintManager.Instance?.cachedBody)
                Log.Info(self.GetType().FullName);
        }

        private static void PlayerCharacterMasterController_PollButtonInput(ILContext il)
        {
            var c = new ILCursor(il);

            int playerLoc = 0;
            int isSprintingLoc = 0;
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
                c.EmitDelegate(AutoSprintManager.Sprint);
            }
            else
            {
                Log.Error("Autosprint IL hook1 failed");
            }

            ILLabel label = null;
            if (c.TryGotoNext(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<PlayerCharacterMasterController>(nameof(PlayerCharacterMasterController.body)),
                    x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.bodyFlags)) &&
                c.TryFindNext(out _, x => x.MatchBrtrue(out label))))
            {
                c.EmitDelegate(() => PluginConfig.EnableOmniSprint.Value);
                c.Emit(OpCodes.Brtrue, label);
            }
            else
            {
                Log.Error("Autosprint IL hook failed");
            }
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
            {
                Log.Error("Crosshair IL hook failed");
            }
        }

        private static IEnumerator EntityStateCatalog_Init(On.RoR2.EntityStateCatalog.orig_Init orig)
        {
            yield return orig();

            for (int k = 0; k < EntityStateCatalog.stateIndexToType.Length; k++)
            {
                AutoSprintManager.typeFullNameToStateIndex[EntityStateCatalog.stateIndexToType[k].FullName] = (EntityStateIndex)k;
            }

            yield return null;

            AutoSprintManager.OnLoad();
        }

        private static void SkillCatalog_Init(On.RoR2.Skills.SkillCatalog.orig_Init orig)
        {
            orig();

            foreach (var skill in SkillCatalog.allSkillDefs)
            {
                if (!skill || skill.forceSprintDuringState || skill.activationState.stateType is null || skill.activationState.stateType == typeof(EntityStates.Idle))
                    continue;

                var type = skill.activationState.stateType;
                if (skill.canceledFromSprinting)
                {
                    AutoSprintManager.sprintDisabledList.Add(type.FullName);
                }
            }

            AutoSprintManager.sprintDisabledList.Add(typeof(EntityStates.Toolbot.FireNailgun).FullName);
            AutoSprintManager.sprintDisabledList.Add(typeof(EntityStates.Toolbot.ToolbotDualWieldBase).FullName);

            AutoSprintManager.sprintDisabledList.Add(typeof(EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam).FullName);

            AutoSprintManager.sprintDisabledList.Add(typeof(EntityStates.Railgunner.Scope.ActiveScopeHeavy).FullName);
            AutoSprintManager.sprintDisabledList.Add(typeof(EntityStates.Railgunner.Scope.ActiveScopeLight).FullName);
            AutoSprintManager.sprintDisabledList.Add(typeof(EntityStates.Railgunner.Scope.WindUpScopeHeavy).FullName);
            AutoSprintManager.sprintDisabledList.Add(typeof(EntityStates.Railgunner.Scope.WindUpScopeLight).FullName);

            AutoSprintManager.sprintDisabledList.Remove(typeof(EntityStates.FalseSon.LaserFather).FullName);

            // AHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH
            AutoSprintManager.sprintDisabledList.Remove(typeof(EntityStates.Croco.Slash).FullName);
            AutoSprintManager.sprintDisabledList.Remove(typeof(EntityStates.Croco.Bite).FullName);
        }
    }
}
