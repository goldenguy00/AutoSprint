using RoR2;
using UnityEngine;
using Rewired;
using System.Reflection;
using EntityStates;

namespace AutoSprint.Core
{
    public class AutoSprintManager
    {
        public float AnimationExitDelay => PluginConfig.DelayTicks.Value * Time.fixedDeltaTime;
        public CharacterBody CachedBody => _cachedBody;

        private CharacterBody _cachedBody;
        private EntityStateMachine[] _cachedStateMachines;

        private float _delayTimer;
        private bool _enableSprintOverride = true;

        public static AutoSprintManager Instance { get; private set; }

        public static void Init() => Instance ??= new AutoSprintManager();

        private AutoSprintManager() { }

        public static void TryHandleSprint(PlayerCharacterMasterController pcmc, Player inputPlayer, bool isSprinting)
        {
            if (Instance is null)
            {
                Log.Error("You must be fucking retarded, how did you manage that");
                return;
            }

            if (StateManager.DisabledBodies.Contains(pcmc.body.bodyIndex))
                return;

            Instance.HandleSprint(pcmc, inputPlayer, isSprinting);
        }

        private void HandleSprint(PlayerCharacterMasterController pcmc, Player inputPlayer, bool isSprinting)
        {
            if (PluginConfig.HoldSprintToWalk.Value)
                _enableSprintOverride = !inputPlayer.GetButton("Sprint");
            else if (pcmc.sprintInputPressReceived)
                _enableSprintOverride = !_enableSprintOverride;

            // nothing to do
            if (!_enableSprintOverride || isSprinting)
            {
                _delayTimer = PluginConfig.BaseDelayTicks.Value * Time.fixedDeltaTime;
                return;
            }
            
            if (!CanSprintBeEnabled(pcmc.body, out var activeDelayTimer))
                _delayTimer = activeDelayTimer;

            if (_delayTimer > 0f)
                _delayTimer -= Time.fixedDeltaTime;
            else
                pcmc.sprintInputPressReceived = true;
        }

        private bool CanSprintBeEnabled(CharacterBody targetBody, out float activeDelayTimer)
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
                        activeDelayTimer = Mathf.Max(activeDelayTimer, AnimationExitDelay);

                    if (StateManager.EntityStateDelayTable.ContainsKey(stateIndex))
                        activeDelayTimer = Mathf.Max(activeDelayTimer, GetDurationRemaining(stateMachine.state, stateIndex));
                }
            }

            return activeDelayTimer == 0f;
        }

        private float GetDurationRemaining(EntityState state, EntityStateIndex stateIndex)
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
