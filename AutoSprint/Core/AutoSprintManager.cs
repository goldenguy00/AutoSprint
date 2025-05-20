using RoR2;
using UnityEngine;
using Rewired;
using System.Reflection;
using EntityStates;

namespace AutoSprint.Core
{
    public static class AutoSprintManager
    {
        public static float AnimationExitDelay => PluginConfig.DelayTicks.Value * Time.fixedDeltaTime;
        public static float BaseExitDelay => PluginConfig.BaseDelayTicks.Value * Time.fixedDeltaTime;
        public static CharacterBody CachedBody => _cachedBody;

        private static CharacterBody _cachedBody;
        private static EntityStateMachine[] _cachedStateMachines;

        private static float _delayTimer;
        private static bool _enableSprintOverride = true;

        public static void TryHandleSprint(PlayerCharacterMasterController pcmc, Player inputPlayer, bool isSprinting)
        {
            if (StateManager.DisabledBodies.Contains(pcmc.body.bodyIndex))
                return;

            // update walk
            if (PluginConfig.HoldSprintToWalk.Value)
                _enableSprintOverride = !inputPlayer.GetButton("Sprint");
            else if (pcmc.sprintInputPressReceived)
                _enableSprintOverride = !_enableSprintOverride;

            // nothing to do
            if (!_enableSprintOverride || isSprinting || pcmc.bodyInputs.moveVector == Vector3.zero)
            {
                _delayTimer = BaseExitDelay;
                return;
            }
            
            // check state
            if (!CanSprintBeEnabled(pcmc.body, out var activeDelayTimer))
                _delayTimer = activeDelayTimer;

            // do thing or not
            if (_delayTimer > 0f)
                _delayTimer -= Time.fixedDeltaTime;
            else
                pcmc.sprintInputPressReceived = true;
        }

        private static bool CanSprintBeEnabled(CharacterBody targetBody, out float activeDelayTimer)
        {
            activeDelayTimer = 0f;

            if (targetBody != _cachedBody)
            {
                Log.Info(BodyCatalog.GetBodyName(targetBody.bodyIndex));

                _cachedBody = targetBody;
                _cachedStateMachines = targetBody.GetComponents<EntityStateMachine>();
            }

            for (var i = 0; i < _cachedStateMachines.Length; i++)
            {
                var stateMachine = _cachedStateMachines[i];
                if (stateMachine?.state is not null)
                {
                    var stateIndex = EntityStateCatalog.GetStateIndex(stateMachine.state.GetType());

                    if (StateManager.EntityStateDisabledSet.Contains(stateIndex))
                        activeDelayTimer = Mathf.Max(activeDelayTimer, BaseExitDelay);

                    if (StateManager.EntityStateDelayTable.ContainsKey(stateIndex))
                        activeDelayTimer = Mathf.Max(activeDelayTimer, GetDurationRemaining(stateMachine.state, stateIndex));
                }
            }

            return activeDelayTimer == 0f;
        }

        private static float GetDurationRemaining(EntityState state, EntityStateIndex stateIndex)
        {
            if (StateManager.EntityStateDelayTable[stateIndex] is float duration)
                return AnimationExitDelay + (duration - state.fixedAge);

            if (StateManager.EntityStateDelayTable[stateIndex] is FieldInfo info && info.FieldType == typeof(float) && info.DeclaringType.IsAssignableFrom(state.GetType()))
                return AnimationExitDelay + ((float)info.GetValue(state) - state.fixedAge);

            Log.Error($"Field for {state.GetType().FullName} is invalid, default is delay is {AnimationExitDelay}.");
            return AnimationExitDelay - state.fixedAge;
        }

    } // End of class RTAutoSprintEx
}
