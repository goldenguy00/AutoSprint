using System.Collections.Generic;
using RoR2;
using UnityEngine;
using Rewired;
using HarmonyLib;
using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using EntityStates;
using System.Diagnostics;

namespace AutoSprint
{
    public class AutoSprintManager
    {
        internal static readonly HashSet<BodyIndex> disabledBodies = [];
        internal static readonly HashSet<string> sprintDisabledList = [];

        internal static readonly HashSet<EntityStateIndex> sprintDisabledSet = [];
        internal static readonly Hashtable animDelayList = [];

        internal static readonly Dictionary<string, EntityStateIndex> typeFullNameToStateIndex = [];

        public float AnimationExitDelay => PluginConfig.DelayTicks.Value * Time.fixedDeltaTime;

        public CharacterBody cachedBody;
        public EntityStateMachine[] cachedStateMachines;

        public static bool enableSprintOverride = true;

        public float timer;

        public static AutoSprintManager Instance { get; private set; }

        public static void Init() => Instance ??= new AutoSprintManager();

        private AutoSprintManager()
        {
        }

        public static void OnLoad()
        {
            Instance.UpdateSprintDisabledList(null, null);
            Instance.UpdateSprintDisabledList2(null, null);
            PluginConfig.DisableSprintingCustomList.SettingChanged += Instance.UpdateSprintDisabledList;
            PluginConfig.DisableSprintingCustomList2.SettingChanged += Instance.UpdateSprintDisabledList2;

            BodyCatalog.availability.CallWhenAvailable(() =>
            {
                Instance.UpdateBodyDisabledList(null, null);
                PluginConfig.DisableSprintingCustomList.SettingChanged += Instance.UpdateBodyDisabledList;
            });
        }

        public void AddItem(string typeFullName, string fieldName)
        {
            if (typeFullNameToStateIndex.TryGetValue(typeFullName, out var index))
            {
                var type = EntityStateCatalog.GetStateType(index);

                if (type != null)
                {
                    var field = AccessTools.DeclaredField(type, fieldName);
                    if (field != null)
                    {
                        animDelayList[index] = field;
                        Log.Info($"Type: {typeFullName} | Field: {fieldName} | Has been added to the custom entity state list.");
                    }
                    else
                    {
                        Log.Error($"Type: {typeFullName} | Field: {fieldName} | The field does not exist in this type.");
                    }
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
                var field = AccessTools.DeclaredField(typeof(T), name);

                if (field is null)
                {
                    Log.Error($"\r\nField is null. Attempting to add numeric...\r\n{typeof(T).FullName} : {name ?? "NULL"}");
                    AddItemWithValue<T>(name ?? "0");
                }
                else if (field.FieldType != typeof(float))
                {
                    Log.Error($"\r\nField must be a float\r\n{typeof(T).FullName} : {field.Name}");
                    AddItemWithValue<T>(name ?? "0");
                }
                else
                {
                    if (animDelayList.ContainsKey(index))
                        Log.Warning($"\r\nOverwriting duplicate entry\r\n{typeof(T).FullName} : {field.Name}\r\nold {animDelayList[index]?.ToString() ?? "NULL"} | new {field.Name}");
                    else
                        Log.Info($"Type: {typeof(T).FullName} | Field: {name} | Has been added to the custom entity state list.");

                    animDelayList[index] = field;
                }
            }
            else
                Log.Error($"\r\nType name could not be found in the entityStateCatalog\r\n{typeof(T).FullName} : {name}");
        }

        public void AddItemWithValue<T>(string name)
        {
            if (typeFullNameToStateIndex.TryGetValue(typeof(T).FullName, out var index))
            {
                name = (name ?? "0").Replace(" ", string.Empty);

                if (!float.TryParse(name, out var val))
                {
                    Log.Error($"\r\nCould not parse the string. value will default to 0\r\n{typeof(T).FullName} : {name}");
                    val = 0;
                }
                Log.Info($"Type: {typeof(T).FullName} | Field: {name} | Has been added to the custom entity state list.");
                animDelayList[index] = val;
            }
            else
                Log.Error($"\r\nType name could not be found in the entityStateCatalog\r\n{typeof(T).FullName} : {name}");
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
                        Log.Info($"{stateString} is not in the valid (key, value) pair format, skipping...");
                    }
                }
            }
        }

        private void UpdateBodyDisabledList(object _, EventArgs __)
        {
            disabledBodies.Clear();
            if (!string.IsNullOrWhiteSpace(PluginConfig.DisabledBodies.Value) && BodyCatalog.availability.available)
            {
                var bodies = PluginConfig.DisabledBodies.Value.Split(',');
                foreach (var item in bodies)
                {
                    var index = BodyCatalog.FindBodyIndex(item);
                    if (index != BodyIndex.None)
                    {
                        disabledBodies.Add(index);
                        Log.Info($"{item} added to the disabled bodies list.");
                    }
                    else
                    {
                        Log.Warning($"{item} is not a valid body, skipping...");
                    }
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
                    Log.Info($"{item} added to the custom entity state list.");
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
                "RA2Mod.Survivors.Chrono.States.ChronoCharacterMain",
            ];

            // lazy paladin/lee compat
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
                Instance.timer = 0f;
        }
        private readonly Stopwatch stopwatch = new();
        private readonly List<TimeSpan> times = new(110);
        private void HandleSprint(PlayerCharacterMasterController pcmc)
        {
            stopwatch.Start();
            bool shouldSprint = CanSprintBeEnabled(pcmc.body, out var sprintDelayTime);

            if (!shouldSprint)
                timer = sprintDelayTime;

            if (timer > 0)
                timer -= Time.fixedDeltaTime;
            else
                pcmc.sprintInputPressReceived |= shouldSprint;

            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
            stopwatch.Reset();
            if (times.Count > 100)
            {
                var average = TimeSpan.FromTicks((long)times.Average(t => t.Ticks)).TotalMilliseconds;
                Log.Debug("Time: " + ((int)(average * 100) / 100));
                times.Clear();
            }
        }

        // Checks if an EntityState blocks sprinting
        private bool CanSprintBeEnabled(CharacterBody targetBody, out float sprintDelayTime)
        {
            sprintDelayTime = 0f;

            if (targetBody != cachedBody)
            {
                if (PluginConfig.EnableDebugMode.Value)
                    Log.Info(targetBody.baseNameToken);

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
                        if (PluginConfig.EnableDebugMode.Value)
                            Log.Info(EntityStateCatalog.GetStateTypeName(stateIndex));
                        sprintDelayTime = Mathf.Max(sprintDelayTime, AnimationExitDelay);
                    }
                    else if (animDelayList.ContainsKey(stateIndex))
                    {
                        if (PluginConfig.EnableDebugMode.Value)
                            Log.Info(EntityStateCatalog.GetStateTypeName(stateIndex));
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
