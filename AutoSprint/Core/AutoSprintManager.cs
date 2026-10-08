using RoR2;
using UnityEngine;
using Rewired;
using System.Reflection;
using EntityStates;
using System;

namespace AutoSprint.Core
{
    public class AutoSprintManager
    {
        public float AnimationExitDelay => PluginConfig.DelayTicks.Value * Time.fixedDeltaTime;
        public float BaseExitDelay => PluginConfig.BaseDelayTicks.Value * Time.fixedDeltaTime;
        public static CharacterBody CachedBody;
        public static NetworkStateMachine CachedStateMachine;

        private float _delayTimer;
        private bool _enableSprintOverride = true;

        public static AutoSprintManager Instance { get; private set; }
        public static void Init() => Instance ??= new AutoSprintManager();
        private AutoSprintManager() { }

        // Returns the sprint state PollButtonInput should push. Since the 2026-10 game update the game
        // toggles a local from body.isSprinting instead of reading sprintInputPressReceived.
        public static bool TryHandleSprint(PlayerCharacterMasterController pcmc, Player inputPlayer, bool isSprinting)
        {
            if (pcmc.body != CachedBody)
            {
                Log.Info(BodyCatalog.GetBodyName(pcmc.body.bodyIndex));

                CachedBody = pcmc.body;
                CachedStateMachine = pcmc.body.GetComponent<NetworkStateMachine>();
            }

            if (pcmc.body.isRemoteOp || StateManager.DisabledBodies.Contains(pcmc.body.bodyIndex))
                return isSprinting;

            return Instance.HandleSprint(pcmc, inputPlayer, isSprinting);
        }

        private bool HandleSprint(PlayerCharacterMasterController pcmc, Player inputPlayer, bool isSprinting)
        {
            // update walk
            // rawSprintInput still holds last tick's state here, so justPressed fires once per press.
            if (PluginConfig.HoldSprintToWalk.Value)
                _enableSprintOverride = !inputPlayer.GetButton("Sprint");
            else if (pcmc.bodyInputs.rawSprintInput.justPressed)
                _enableSprintOverride = !_enableSprintOverride;

            // nothing to do
            if (!_enableSprintOverride || isSprinting || pcmc.bodyInputs.moveVector == Vector3.zero)
            {
                _delayTimer = BaseExitDelay;
                return isSprinting;
            }

            // check state
            if (!CanSprintBeEnabled(out var activeDelayTimer))
                _delayTimer = activeDelayTimer;

            // do thing or not
            if (_delayTimer > 0f)
            {
                _delayTimer -= Time.fixedDeltaTime;
                return isSprinting;
            }

            return true;
        }

        private bool CanSprintBeEnabled(out float activeDelayTimer)
        {
            activeDelayTimer = 0f;

            for (var i = 0; i < CachedStateMachine.stateMachines.Length; i++)
            {
                var stateMachine = CachedStateMachine.stateMachines[i];
                if (stateMachine?.state is not null)
                {
                    var stateIndex = EntityStateCatalog.GetStateIndex(stateMachine.state.GetType());

                    if (StateManager.EntityStateDisabledSet.Contains(stateIndex))
                        activeDelayTimer = Mathf.Max(activeDelayTimer, BaseExitDelay);

                    if (StateManager.SprintStateDelayTable.ContainsKey(stateIndex))
                        activeDelayTimer = Mathf.Max(activeDelayTimer, GetDurationRemaining(stateMachine.state, StateManager.SprintStateDelayTable[stateIndex]));
                }
            }

            return activeDelayTimer == 0f;
        }

        private float GetDurationRemaining(EntityState state, object value)
        {
            float maxDuration = 0f;
            if (value is float duration)
                maxDuration = duration;
            else if (value is FieldInfo info && info.FieldType == typeof(float) && (info.IsStatic || info.DeclaringType.IsAssignableFrom(state.GetType())))
                maxDuration = (float)info.GetValue(!info.IsStatic ? state : null);

            return AnimationExitDelay + (maxDuration - state.fixedAge);
        }

    } // End of class RTAutoSprintEx
}
