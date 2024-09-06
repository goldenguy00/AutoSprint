using System.Collections.Generic;
using RoR2;
using UnityEngine;
using Rewired;
using HarmonyLib;
using RoR2.Skills;
using MonoMod.Cil;
using System;
using System.Reflection;

namespace AutoSprint
{
    public class AutoSprintManager
    {
        internal static readonly HashSet<string> sprintDisabledList = [];
        internal static readonly Dictionary<string, FieldInfo> animDelayList = [];

        public const float ANIM_CANCEL_DELAY = -0.15f;

        public CharacterBody cachedBody;
        public EntityStateMachine[] cachedStates;

        public bool waitForAnimation, enableWalk;
        public float timer;

        public static AutoSprintManager Instance { get; private set; }

        public static void Init() => Instance ??= new AutoSprintManager();

        private AutoSprintManager()
        {
            On.RoR2.Skills.SkillCatalog.Init += SkillCatalog_Init;
            On.RoR2.PlayerCharacterMasterController.FixedUpdate += PlayerCharacterMasterController_FixedUpdate;

            IL.RoR2.UI.CrosshairManager.UpdateCrosshair += CrosshairManager_UpdateCrosshair;
        }

        private static void CrosshairManager_UpdateCrosshair(ILContext il)
        {
            var c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchCallvirt<CharacterBody>("get_isSprinting")
                ))
            {
                c.EmitDelegate<Func<bool, bool>>((val) => val && !PluginConfig.DisableSprintingCrosshair.Value);
            }
        }

        private void SkillCatalog_Init(On.RoR2.Skills.SkillCatalog.orig_Init orig)
        {
            orig();

            foreach (var skill in SkillCatalog.allSkillDefs)
            {
                if (!skill || skill.forceSprintDuringState || skill.activationState.stateType is null)
                    continue;
                
                var type = skill.activationState.stateType;
                if (skill.canceledFromSprinting)
                {
                    sprintDisabledList.Add(type.FullName);
                }
                else if (skill.cancelSprintingOnActivation && !animDelayList.ContainsKey(type.FullName))
                {
                    var durationField = AccessTools.FindIncludingBaseTypes(type, t => t.GetField("duration", AccessTools.all));
                    if (durationField != null)
                        animDelayList.Add(type.FullName, durationField);
                }
            }

            // special cases bandaid fix.
            // they dont list cancelledBySprinting in the skilldef,
            // but are cancelled by sprinting in the entitystate.FixedUpdate
            // ill do it right some other time

            sprintDisabledList.Add(typeof(EntityStates.Toolbot.ToolbotDualWield).FullName);
            sprintDisabledList.Add(typeof(EntityStates.Toolbot.ToolbotDualWieldStart).FullName);
            sprintDisabledList.Add(typeof(EntityStates.Toolbot.ToolbotDualWieldEnd).FullName);
            animDelayList.Remove(typeof(EntityStates.Toolbot.ToolbotDualWield).FullName);
            animDelayList.Remove(typeof(EntityStates.Toolbot.ToolbotDualWieldStart).FullName);
            animDelayList.Remove(typeof(EntityStates.Toolbot.ToolbotDualWieldEnd).FullName);

            sprintDisabledList.Add(typeof(EntityStates.Toolbot.FireNailgun).FullName);
            animDelayList.Remove(typeof(EntityStates.Toolbot.FireNailgun).FullName);

            sprintDisabledList.Add(typeof(EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam).FullName);
            animDelayList.Remove(typeof(EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam).FullName);

            sprintDisabledList.Add(typeof(EntityStates.Railgunner.Scope.ActiveScopeHeavy).FullName);
            sprintDisabledList.Add(typeof(EntityStates.Railgunner.Scope.ActiveScopeLight).FullName);
            animDelayList.Remove(typeof(EntityStates.Railgunner.Scope.ActiveScopeHeavy).FullName);
            animDelayList.Remove(typeof(EntityStates.Railgunner.Scope.ActiveScopeLight).FullName);

            animDelayList.Remove(typeof(EntityStates.FalseSon.LaserFather).FullName);

            // AHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH
            sprintDisabledList.Remove(typeof(EntityStates.Croco.Slash).FullName);
            sprintDisabledList.Remove(typeof(EntityStates.Croco.Bite).FullName);
            animDelayList[typeof(EntityStates.Croco.Slash).FullName] = AccessTools.DeclaredField(typeof(EntityStates.Croco.Slash), nameof(EntityStates.Croco.Slash.durationBeforeInterruptable));
            animDelayList[typeof(EntityStates.Croco.Bite).FullName] = AccessTools.DeclaredField(typeof(EntityStates.Croco.Bite), nameof(EntityStates.Croco.Bite.durationBeforeInterruptable));

            // lazy paladin compat
            var customList = "PaladinMod.States.Spell.ChannelCruelSun, PaladinMod.States.Spell.ChannelHealZone, PaladinMod.States.Spell.ChannelSmallHeal, PaladinMod.States.Spell.ChannelTorpor, PaladinMod.States.Spell.ChannelWarcry, " +
                "PaladinMod.States.Spell.CastCruelSun, PaladinMod.States.Spell.CastChanneledWarcry, PaladinMod.States.Spell.CastChanneledTorpor, PaladinMod.States.Spell.CastChanneledHealZone, " + PluginConfig.DisableSprintingCustomList.Value;
            var states = customList.Replace(" ", string.Empty).Split(',');
            foreach (var state in states)
            {
                if (!string.IsNullOrWhiteSpace(state))
                {
                    sprintDisabledList.Add(state);
                    animDelayList.Remove(state);
                }
            }
        }

        private void PlayerCharacterMasterController_FixedUpdate(On.RoR2.PlayerCharacterMasterController.orig_FixedUpdate orig, PlayerCharacterMasterController self)
        {
            orig(self);

            if (self.body && self.bodyInputs && PlayerCharacterMasterController.CanSendBodyInput(self.networkUser, out _, out var inputPlayer, out _, out var onlyAllowMovement) && !onlyAllowMovement)
            {
                HandleSprint(self.body, self.bodyInputs, inputPlayer);
            }
        }

        // Checks if an EntityState blocks sprinting
        private bool CanSprintBeEnabled(CharacterBody targetBody, out float sprintDelayTime)
        {
            if (targetBody != cachedBody)
            {
                cachedBody = targetBody;
                cachedStates = targetBody.GetComponents<EntityStateMachine>();
            }

            bool canSprint = true;
            sprintDelayTime = 0f;
            for (int i = 0; i < cachedStates.Length; i++)
            {
                var currentState = cachedStates[i];
                if (currentState && !currentState.IsInMainState())
                {
                    var stateName = currentState.state.ToString();
                    if (sprintDisabledList.Contains(stateName))
                        canSprint = false;
                    else if (animDelayList.TryGetValue(stateName, out var field) && field != null && field.GetValue(currentState.state) is float duration)
                        sprintDelayTime = Mathf.Max(sprintDelayTime, duration);
                }
            }

            return canSprint;
        }

        private bool IsKeyPressed(Player inputPlayer) => 
            inputPlayer.GetButton("PrimarySkill") || !inputPlayer.GetButton("SecondarySkill") ||
            inputPlayer.GetButton("SpecialSkill") || inputPlayer.GetButton("UtilitySkill");

        public void HandleSprint(CharacterBody body, InputBankTest bodyInputs, Player inputPlayer)
        {
            bool shouldSprint = body.isSprinting;
            bool canSprint = CanSprintBeEnabled(body, out var sprintDelayTime);

            if (!shouldSprint)
            {
                timer += Time.deltaTime;
                if (!waitForAnimation)
                {
                    timer = -sprintDelayTime;
                }
                if (timer >= 0)
                {
                    shouldSprint = canSprint;
                    waitForAnimation = false;
                    timer = 0;
                }
            }
            else
            {
                timer = 0;
            }

            // Animation cancelling logic.
            if (!waitForAnimation && timer < ANIM_CANCEL_DELAY && !IsKeyPressed(inputPlayer))
            {
                timer = ANIM_CANCEL_DELAY;
                waitForAnimation = true;
            }

            // Angle check disables sprinting if the movement angle is too large
            if (shouldSprint)
            {
                var aimDirection = bodyInputs.aimDirection;
                aimDirection.y = 0f;
                aimDirection.Normalize();
                var moveVector = bodyInputs.moveVector;
                moveVector.y = 0f;
                moveVector.Normalize();
                
                if (!PluginConfig.EnableOmniSprint.Value && !body.bodyFlags.HasFlag(CharacterBody.BodyFlags.SprintAnyDirection) && Vector3.Dot(aimDirection, moveVector) < PlayerCharacterMasterController.sprintMinAimMoveDot)
                {
                    shouldSprint = false;
                }
            }

            if (PluginConfig.HoldSprintToWalk.Value)
                enableWalk = inputPlayer.GetButton("Sprint");
            else if (inputPlayer.GetButtonDown("Sprint"))
                enableWalk = !enableWalk;

            if (!enableWalk)
                bodyInputs.sprint.PushState(shouldSprint);
        }

    } // End of class RTAutoSprintEx
}
