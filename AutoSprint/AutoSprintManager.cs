using System.Collections.Generic;
using RoR2;
using UnityEngine;
using Rewired;
using HarmonyLib;
using System;
using System.Reflection;
using System.Collections;
using EntityStates;

namespace AutoSprint
{
    public class AutoSprintManager
    {
        #region Static members
        /// <summary>
        /// Key-Value pair of (EntityStateIndex, float) --or-- (EntityStateIndex, FieldInfo)
        /// </summary>
        public static readonly Hashtable animDelayList = [];

        /// <summary>
        /// This list of strings is later converted to indexes, since it gets populated before the entitystate catalog exists
        /// Also, its a good way to store non-user generated states seperately for whenever the list is regenerated
        /// Also, its a great way to add soft compat without worrying about missing references
        /// </summary>
        public static readonly HashSet<string> sprintDisabledList = [];
        public static readonly HashSet<EntityStateIndex> sprintDisabledSet = [];

        public static readonly HashSet<BodyIndex> disabledBodies = [];

        public static readonly Dictionary<string, EntityStateIndex> typeFullNameToStateIndex = [];

        public static bool enableSprintOverride = true;
        #endregion

        public float AnimationExitDelay => PluginConfig.DelayTicks.Value * Time.fixedDeltaTime;

        public CharacterBody cachedBody;
        public EntityStateMachine[] cachedStateMachines;

        public float timer;

        public static AutoSprintManager Instance { get; private set; }

        public static void Init() => Instance ??= new AutoSprintManager();

        private AutoSprintManager()
        {
            BodyCatalog.availability.CallWhenAvailable(() =>
            {
                this.UpdateBodyDisabledList(null, null);
                PluginConfig.DisableSprintingCustomList.SettingChanged += Instance.UpdateBodyDisabledList;
            });
        }

        /// <summary>
        /// EntityStateCatalog must exist before calling this!
        /// </summary>
        public static void OnLoad()
        {
            Instance.UpdateSprintDisabledList(null, null);
            Instance.UpdateSprintDisabledList2(null, null);
            PluginConfig.DisableSprintingCustomList.SettingChanged += Instance.UpdateSprintDisabledList;
            PluginConfig.DisableSprintingCustomList2.SettingChanged += Instance.UpdateSprintDisabledList2;
        }

        public void AddItem(string typeFullName, string fieldName)
        {
            if (typeFullNameToStateIndex.TryGetValue(typeFullName, out var index))
            {
                var type = EntityStateCatalog.GetStateType(index);

                if (type != null)
                {
                    AddItem(type, fieldName);
                }
                else
                {
                    Log.Error($"Type: {typeFullName} | Field: {fieldName} | The state exists in the EntityStateCatalog but the type is null.");
                }
            }
            else
            {
                Log.Error($"Type: {typeFullName} | Field: {fieldName} | The type does not exist in the EntityStateCatalog.");
            }
        }

        public void AddItem<T>(string name)
        {
            if (typeFullNameToStateIndex.TryGetValue(typeof(T).FullName, out var index))
            {
                if (float.TryParse((name ?? "0").Replace(" ", string.Empty), out var val))
                {
                    Log.Message($"Type: {typeof(T).FullName} | Field: {val} | Has been added to the custom entity state list.");
                    animDelayList[index] = val;
                }
                else
                {
                    var field = AccessTools.DeclaredField(typeof(T), name);
                    if (field is null)
                    {
                        Log.Error($"\r\nField with that name could not be found and wasnt able to parse the value as numeric. Type not added.\r\n{typeof(T).FullName} : {name ?? "NULL"}");
                    }
                    else if (field.FieldType != typeof(float))
                    {
                        Log.Error($"\r\nField must be a float, but the field does exist. Type not added.\r\n{typeof(T).FullName} : {field.Name}");
                    }
                    else
                    {
                        if (animDelayList.ContainsKey(index))
                            Log.Warning($"\r\nOverwriting duplicate entry\r\n{typeof(T).FullName} : {field.Name}\r\nold {animDelayList[index]?.ToString() ?? "NULL"} | new {field.Name}");
                        else
                            Log.Message($"Type: {typeof(T).FullName} | Field: {name} | Has been added to the custom entity state list.");

                        animDelayList[index] = field;
                    }
                }
            }
            else
                Log.Error($"\r\nType name could not be found in the entityStateCatalog\r\n{typeof(T).FullName} : {name}");
        }

        public void AddItem(Type T, string name)
        {
            if (typeFullNameToStateIndex.TryGetValue(T.FullName, out var index))
            {
                if (float.TryParse((name ?? "0").Replace(" ", string.Empty), out var val))
                {
                    Log.Message($"Type: {T.FullName} | Field: {val} | Has been added to the custom entity state list.");
                    animDelayList[index] = val;
                }
                else
                {
                    var field = AccessTools.DeclaredField(T, name);
                    if (field is null)
                    {
                        Log.Error($"\r\nField with that name could not be found and wasnt able to parse the value as numeric. Type not added.\r\n{T.FullName} : {name ?? "NULL"}");
                    }
                    else if (field.FieldType != typeof(float))
                    {
                        Log.Error($"\r\nField must be a float, but the field does exist. Type not added.\r\n{T.FullName} : {field.Name}");
                    }
                    else
                    {
                        if (animDelayList.ContainsKey(index))
                            Log.Warning($"\r\nOverwriting duplicate entry\r\n{T.FullName} : {field.Name}\r\nold {animDelayList[index]?.ToString() ?? "NULL"} | new {field.Name}");
                        else
                            Log.Message($"Type: {T.FullName} | Field: {name} | Has been added to the custom entity state list.");

                        animDelayList[index] = field;
                    }
                }
            }
            else
                Log.Error($"\r\nType name could not be found in the entityStateCatalog\r\n{T.FullName} : {name}");
        }

        private void UpdateSprintDisabledList2(object _, EventArgs __)
        {
            animDelayList.Clear();

            AddItem<EntityStates.Toolbot.ToolbotDualWieldStart>(nameof(EntityStates.Toolbot.ToolbotDualWieldStart.baseDuration));
            AddItem<EntityStates.Croco.Slash>(nameof(EntityStates.Croco.Slash.durationBeforeInterruptable));
            AddItem<EntityStates.Croco.Bite>(nameof(EntityStates.Croco.Bite.durationBeforeInterruptable));

            foreach (var stateString in PluginConfig.DisableSprintingCustomList2.Value.Replace(" ", string.Empty).Split(')'))
            {
                //(x,y
                if (!string.IsNullOrEmpty(stateString))
                {
                    var pair = stateString.Replace("(", string.Empty).Split(',');
                    if (pair.Length == 2)
                    {
                        AddItem(pair[0], pair[1]);
                    }
                    else
                    {
                        Log.Warning($"{stateString} is not in the valid (key, value) pair format, skipping...");
                    }
                }
            }
        }

        private void UpdateBodyDisabledList(object _, EventArgs __)
        {
            disabledBodies.Clear();
            List<string> bodies = ["JohnnyBody", "PantheraBody", "RA2ChronoBody"];

            foreach (var item in PluginConfig.DisabledBodies.Value.Replace(" ", string.Empty).Split(','))
            {
                if (!string.IsNullOrEmpty(item))
                    bodies.Add(item);
            }

            foreach (var item in bodies)
            {
                var index = BodyCatalog.FindBodyIndex(item);
                if (index != BodyIndex.None)
                {
                    disabledBodies.Add(index);
                    Log.Message($"{item} added to the disabled bodies list.");
                }
                else
                {
                    Log.Warning($"{item} is not a valid body, skipping...");
                }
            }
        }

        private void UpdateSprintDisabledList(object _, EventArgs __)
        {
            sprintDisabledSet.Clear();

            foreach (var item in sprintDisabledList)
            {
                if (typeFullNameToStateIndex.TryGetValue(item, out var index))
                {
                    sprintDisabledSet.Add(index);
                    Log.Debug($"{item} added to the custom entity state list.");
                }
                else
                {
                    Log.Warning($"{item} is not a valid entity state, skipping...");
                }
            }

            List<string> customList =
            [
                "LeeHyperrealMod.SkillStates.LeeHyperreal.Secondary.EnterSnipe",
                "LeeHyperrealMod.SkillStates.LeeHyperreal.Secondary.Snipe",
                "LeeHyperrealMod.SkillStates.LeeHyperreal.Secondary.ExitSnipe",
                "PaladinMod.States.Spell.ChannelCruelSun",
                "PaladinMod.States.Spell.ChannelHealZone",
                "PaladinMod.States.Spell.ChannelSmallHeal",
                "PaladinMod.States.Spell.ChannelTorpor",
                "PaladinMod.States.Spell.ChannelWarcry",
                "PaladinMod.States.Spell.CastCruelSun",
                "PaladinMod.States.Spell.CastChanneledWarcry",
                "PaladinMod.States.Spell.CastChanneledTorpor",
                "PaladinMod.States.Spell.CastChanneledHealZone",
            ];

            var states = PluginConfig.DisableSprintingCustomList.Value.Replace(" ", string.Empty).Split(',');
            foreach (var state in states)
            {
                if (!string.IsNullOrWhiteSpace(state))
                {
                    customList.Add(state);
                }
            }

            foreach (var item in customList)
            {
                if (typeFullNameToStateIndex.TryGetValue(item, out var index))
                {
                    sprintDisabledSet.Add(index);
                    Log.Info($"{item} added to the custom entity state list.");
                }
                else
                {
                    Log.Warning($"{item} is not a valid entity state, skipping...");
                }
            }
        }

        public static void Sprint(PlayerCharacterMasterController pcmc, Player inputPlayer, bool isSprinting)
        {
            if (Instance is null || disabledBodies.Contains(pcmc.body.bodyIndex))
                return;

            if (PluginConfig.HoldSprintToWalk.Value)
                enableSprintOverride = !inputPlayer.GetButton("Sprint");
            else if (pcmc.sprintInputPressReceived)
                enableSprintOverride = !enableSprintOverride;

            if (enableSprintOverride && !isSprinting)
                Instance.HandleSprint(pcmc);
            else
                Instance.timer = Instance.AnimationExitDelay;
        }

        private void HandleSprint(PlayerCharacterMasterController pcmc)
        {
            bool shouldSprint = CanSprintBeEnabled(pcmc.body, out var sprintDelayTime);

            if (!shouldSprint)
                timer = sprintDelayTime;

            if (timer > 0)
                timer -= Time.fixedDeltaTime;
            else
                pcmc.sprintInputPressReceived |= shouldSprint;
        }

        private bool CanSprintBeEnabled(CharacterBody targetBody, out float sprintDelayTime)
        {
            sprintDelayTime = 0f;

            if (targetBody != cachedBody)
            {
                Log.Info(BodyCatalog.GetBodyName(targetBody.bodyIndex));

                cachedBody = targetBody;
                cachedStateMachines = targetBody.GetComponents<EntityStateMachine>();
            }

            for (int i = 0; i < cachedStateMachines.Length; i++)
            {
                var stateMachine = cachedStateMachines[i];
                if (stateMachine && stateMachine.state is not null)
                {
                    var stateIndex = EntityStateCatalog.GetStateIndex(stateMachine.state.GetType());
                    if (sprintDisabledSet.Contains(stateIndex))
                    {
                        sprintDelayTime = Mathf.Max(sprintDelayTime, AnimationExitDelay);
                    }
                    if (animDelayList.ContainsKey(stateIndex))
                    {
                        float duration = GetDuration(animDelayList[stateIndex], stateMachine.state);
                        sprintDelayTime = Mathf.Max(sprintDelayTime, duration - stateMachine.state.fixedAge + AnimationExitDelay);
                    }
                }
            }

            return sprintDelayTime == 0f;
        }

        private float GetDuration(object field, EntityState state)
        {
            if (field is float duration)
                return duration;

            if (field is FieldInfo info && info.FieldType == typeof(float) && info.DeclaringType.IsAssignableFrom(state.GetType()))
                return (float)info.GetValue(state);

            Log.Error($"Field is invalid, default is ~0.1s delay. {state.GetType().FullName}");
            return 0f;
        }

    } // End of class RTAutoSprintEx
}
