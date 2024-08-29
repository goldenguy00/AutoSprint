using System.Collections.Generic;
using RoR2;
using UnityEngine;
using Rewired;
using HarmonyLib;
using RoR2.Skills;

namespace AutoSprint
{
    public class AutoSprintManager
    {
        internal readonly HashSet<string> stateSprintDisableList = [];
        internal readonly HashSet<string> stateAnimationDelayList = [];

        // Checks if the duration value exists.
        private float SprintDelayTime(CharacterBody targetBody)
        {
            var duration = 0f;
            foreach (var machine in targetBody.GetComponents<EntityStateMachine>())
            {
                var currentState = machine.state;
                if (currentState != null && stateAnimationDelayList.Contains(currentState.ToString()))
                {
                    var fieldValue = AccessTools.FindIncludingBaseTypes(currentState.GetType(), t => t.GetField("duration", AccessTools.all))?.GetValue(currentState);
                    if (fieldValue is float d)
                        duration = Mathf.Max(duration, d);
                }
            }

            return duration;
        }

        // Checks if an EntityState blocks sprinting
        private bool ShouldSprintBeDisabledOnThisBody(CharacterBody targetBody)
        {
            foreach (var machine in targetBody.GetComponents<EntityStateMachine>())
            {
                var currentState = machine.state;
                if (currentState != null && stateSprintDisableList.Contains(currentState.ToString()))
                    return true;
            }
            return false;
        }

        public static AutoSprintManager Instance { get; private set; }

        public static void Init() => Instance ??= new AutoSprintManager();
        private AutoSprintManager()
        {
            On.RoR2.Skills.SkillCatalog.Init += SkillCatalog_Init;

            On.RoR2.PlayerCharacterMasterController.Update += PlayerCharacterMasterController_Update;
        } // End of Awake

        private void SkillCatalog_Init(On.RoR2.Skills.SkillCatalog.orig_Init orig)
        {
            orig();

            foreach (var skill in SkillCatalog.allSkillDefs)
            {
                if (!skill || skill.forceSprintDuringState)
                    continue;

                if (skill.canceledFromSprinting)
                    stateSprintDisableList.Add(skill.activationState.typeName);
                else if (skill.cancelSprintingOnActivation)
                    stateAnimationDelayList.Add(skill.activationState.typeName);
            }

            // special cases bandaid fix.
            // they dont list cancelledBySprinting in the skilldef,
            // but are cancelled by sprinting in the entitystate.FixedUpdate
            stateSprintDisableList.Add(typeof(EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam).AssemblyQualifiedName);
            stateSprintDisableList.Add(typeof(EntityStates.Railgunner.Scope.ActiveScopeHeavy).AssemblyQualifiedName);
            stateSprintDisableList.Add(typeof(EntityStates.Railgunner.Scope.ActiveScopeLight).AssemblyQualifiedName);
        }

        public float RT_timer;
        public float RT_animationCancelDelay = 0.15f;
        public bool RT_animationCancel;
        public bool RT_walkToggle;

        private void PlayerCharacterMasterController_Update(On.RoR2.PlayerCharacterMasterController.orig_Update orig, PlayerCharacterMasterController self)
        {
            orig(self);

            var bodyInputs = self.bodyInputs;
            if (bodyInputs)
            {
                if (self.networkUser && self.networkUser.localUser != null && !self.networkUser.localUser.isUIFocused)
                {
                    var body = self.body;
                    if (body)
                    {
                        var inputPlayer = self.networkUser.localUser.inputPlayer;
                        HandleSprint(body, inputPlayer, bodyInputs);
                    }
                }
            }
        }

        public void HandleSprint(CharacterBody body, Player inputPlayer, InputBankTest bodyInputs)
        {
            bool RT_isSprinting = body.isSprinting;
            bool disableSprint = ShouldSprintBeDisabledOnThisBody(body);

            // Periodic sprint checker
            if (!RT_isSprinting)
            {
                RT_timer += Time.deltaTime;
                if (RT_timer >= 0.05)
                {
                    if (!RT_animationCancel)
                    {
                        RT_timer = 0f - SprintDelayTime(body);
                    }
                    if (RT_timer >= 0)
                    {
                        RT_isSprinting = !disableSprint;
                        RT_animationCancel = false;
                        RT_timer = 0;
                    }
                }
            }
            else RT_timer = 0;

            // Walk Toggle logic
            if (!PluginConfig.HoldSprintToWalk.Value && inputPlayer.GetButtonDown("Sprint") && !disableSprint)
            {
                RT_walkToggle = !RT_walkToggle;
            }
            else if (inputPlayer.GetButton("Sprint"))
            {
                if (RT_isSprinting && PluginConfig.HoldSprintToWalk.Value)
                    RT_isSprinting = false;

                if (!RT_isSprinting && disableSprint)
                    RT_isSprinting = true;

                RT_timer = 0;
            }


            // Animation cancelling logic.
            if (!RT_animationCancel && RT_timer < -(RT_animationCancelDelay)
                && !inputPlayer.GetButton("PrimarySkill") && !inputPlayer.GetButton("SecondarySkill")
                && !inputPlayer.GetButton("SpecialSkill") && !inputPlayer.GetButton("UtilitySkill"))
            {
                RT_timer = -(RT_animationCancelDelay);
                RT_animationCancel = true;
            }

            // Angle check disables sprinting if the movement angle is too large
            if (RT_isSprinting)
            {
                var aimDirection = bodyInputs.aimDirection;
                aimDirection.y = 0f;
                aimDirection.Normalize();
                var moveVector = bodyInputs.moveVector;
                moveVector.y = 0f;
                moveVector.Normalize();
                
                if ((body.bodyFlags & CharacterBody.BodyFlags.SprintAnyDirection) == 0 && Vector3.Dot(aimDirection, moveVector) < PlayerCharacterMasterController.sprintMinAimMoveDot)
                {
                    RT_isSprinting = false;
                }
            }

            if (PluginConfig.HoldSprintToWalk.Value && RT_walkToggle)
                RT_walkToggle = false;

            if (!RT_walkToggle)
                bodyInputs.sprint.PushState(RT_isSprinting);
        }

    } // End of class RTAutoSprintEx
}
