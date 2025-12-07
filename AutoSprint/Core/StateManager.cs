using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using RoR2;
using RoR2.Skills;

namespace AutoSprint.Core
{
    public static class StateManager
    {
        /// <summary>
        /// Key-Value pair of (EntityStateIndex, float) --or-- (EntityStateIndex, FieldInfo)
        /// </summary>
        public static readonly Dictionary<string, string> SprintDelayTypeNameValuePairs = [];
        public static readonly Hashtable SprintStateDelayTable = [];

        /// <summary>
        /// This list of strings is later converted to indexes, since it gets populated before the entitystate catalog exists
        /// Also, its a good way to store non-user generated states seperately for whenever the list is regenerated
        /// Also, its a great way to add soft compat without worrying about missing references
        /// </summary>
        public static readonly HashSet<string> SprintDisabledTypeNames = [];
        public static readonly HashSet<EntityStateIndex> EntityStateDisabledSet = [];

        public static readonly HashSet<BodyIndex> DisabledBodies = [];

        public static readonly Dictionary<string, EntityStateIndex> TypeFullNameToStateIndex = [];
        private static readonly Stopwatch timer = new Stopwatch();

        [SystemInitializer([typeof(SkillCatalog)])]
        internal static void UpdateFromSkillCatalog()
        {
            timer.Restart();
            var idleType = typeof(EntityStates.Idle);
            for (int i = 0; i < SkillCatalog._allSkillDefs.Length; i++)
            {
                var skill = SkillCatalog._allSkillDefs[i];
                if (skill?.forceSprintDuringState != false)
                    continue;

                if (skill.canceledFromSprinting)
                {
                    var type = skill.activationState.stateType;
                    var fullName = type?.FullName;
                    if (string.IsNullOrEmpty(fullName) || idleType.IsAssignableFrom(type))
                        continue;

                    StateManager.SprintDisabledTypeNames.Add(fullName);
                }
                else if (skill.cancelSprintingOnActivation)
                {
                    var type = skill.activationState.stateType;
                    var fullName = type?.FullName;
                    if (string.IsNullOrEmpty(fullName) || idleType.IsAssignableFrom(type))
                        continue;

                    StateManager.SprintDelayTypeNameValuePairs[fullName] = "0";
                }
            }

            StateManager.SprintDisabledTypeNames.Add(typeof(EntityStates.Toolbot.FireNailgun).FullName);
            StateManager.SprintDisabledTypeNames.Add(typeof(EntityStates.Toolbot.ToolbotDualWieldStart).FullName);
            StateManager.SprintDisabledTypeNames.Add(typeof(EntityStates.Toolbot.ToolbotDualWield).FullName);

            StateManager.SprintDisabledTypeNames.Add(typeof(EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam).FullName);

            StateManager.SprintDisabledTypeNames.Add(typeof(EntityStates.Railgunner.Scope.ActiveScopeHeavy).FullName);
            StateManager.SprintDisabledTypeNames.Add(typeof(EntityStates.Railgunner.Scope.ActiveScopeLight).FullName);
            StateManager.SprintDisabledTypeNames.Add(typeof(EntityStates.Railgunner.Scope.WindUpScopeHeavy).FullName);
            StateManager.SprintDisabledTypeNames.Add(typeof(EntityStates.Railgunner.Scope.WindUpScopeLight).FullName);

            StateManager.SprintDisabledTypeNames.Add(typeof(EntityStates.DroneTech.Weapon.ShieldFormation).FullName);

            StateManager.SprintDisabledTypeNames.Remove(typeof(EntityStates.FalseSon.LaserFather).FullName);

            // AHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH
            StateManager.SprintDisabledTypeNames.Remove(typeof(EntityStates.Croco.Slash).FullName);
            StateManager.SprintDisabledTypeNames.Remove(typeof(EntityStates.Croco.Bite).FullName);

            StateManager.SprintDelayTypeNameValuePairs[typeof(EntityStates.Toolbot.ToolbotDualWieldStart).FullName] = nameof(EntityStates.Toolbot.ToolbotDualWieldStart.duration);
            StateManager.SprintDelayTypeNameValuePairs[typeof(EntityStates.Croco.Slash).FullName] = nameof(EntityStates.Croco.Slash.durationBeforeInterruptable);
            StateManager.SprintDelayTypeNameValuePairs[typeof(EntityStates.Croco.Bite).FullName] = nameof(EntityStates.Croco.Bite.durationBeforeInterruptable);
            timer.Stop();
            Log.Info($"UpdateFromSkillCatalog: {timer.Elapsed.TotalMilliseconds}ms");
        }

        [SystemInitializer([typeof(BodyCatalog)])]
        internal static void UpdateFromBodyCatalog()
        {
            StateManager.UpdateDisabledBodies(null, null);
            PluginConfig.DisableSprintingCustomList.SettingChanged += StateManager.UpdateDisabledBodies;
        }

        internal static IEnumerator EntityStateCatalog_Init(On.RoR2.EntityStateCatalog.orig_Init orig)
        {
            yield return orig();

            timer.Restart();
            for (var k = 0; k < EntityStateCatalog.stateIndexToType.Length; k++)
            {
                StateManager.TypeFullNameToStateIndex[EntityStateCatalog.stateIndexToType[k].FullName] = (EntityStateIndex)k;
            }
            timer.Stop();
            Log.Info($"EntityStateCatalog_Init: {timer.Elapsed.TotalMilliseconds}ms");

            StateManager.UpdateDisabledStates(null, null);
            StateManager.UpdateDelayStates(null, null);

            PluginConfig.DisableSprintingCustomList.SettingChanged += StateManager.UpdateDisabledStates;
            PluginConfig.DisableSprintingCustomList2.SettingChanged += StateManager.UpdateDelayStates;
        }

        internal static void UpdateDisabledBodies(object _, EventArgs __)
        {
            timer.Restart();
            DisabledBodies.Clear();
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
                    DisabledBodies.Add(index);
                    Log.Info($"{item} added to the disabled bodies list.");
                }
                else
                    Log.Info($"{item} is not a valid body, skipping...");
            }

            timer.Stop();
            Log.Info($"UpdateDisabledBodies: {timer.Elapsed.TotalMilliseconds}ms");
        }

        internal static void UpdateDisabledStates(object _, EventArgs __)
        {
            timer.Restart();
            EntityStateDisabledSet.Clear();

            List<string> customList =
            [
                .. SprintDisabledTypeNames,
                "LeeHyperrealMod.SkillStates.LeeHyperreal.Secondary.EnterSnipe",
                "LeeHyperrealMod.SkillStates.LeeHyperreal.Secondary.Snipe",
                "LeeHyperrealMod.SkillStates.LeeHyperreal.Secondary.IdleSnipe",
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
                "DanteMod.SkillStates.Dante.LockOn",
                "DanteMod.SkillStates.Dante.Swap",
                "HunkMod.SkillStates.Hunk.SteadyAim"
            ];

            foreach (var state in PluginConfig.DisableSprintingCustomList.Value.Replace(" ", string.Empty).Split(','))
            {
                if (!string.IsNullOrWhiteSpace(state))
                    customList.Add(state);
            }

            foreach (var item in customList)
            {
                if (TypeFullNameToStateIndex.TryGetValue(item, out var index))
                {
                    EntityStateDisabledSet.Add(index);
                    Log.Debug($"{item} added to the custom entity state list.");
                }
                else
                {
                    Log.Info($"{item} is not a valid entity state, skipping...");
                }
            }
            timer.Stop();
            Log.Info($"UpdateDisabledStates: {timer.Elapsed.TotalMilliseconds}ms");
        }

        internal static void UpdateDelayStates(object _, EventArgs __)
        {
            timer.Restart();
            SprintStateDelayTable.Clear();

            foreach (var kvp in SprintDelayTypeNameValuePairs)
            {
                AddSprintDelay(kvp.Key, kvp.Value);
            }

            foreach (var statePair in PluginConfig.DisableSprintingCustomList2.Value.Replace(" ", string.Empty).Split(')'))
            {
                if (!string.IsNullOrEmpty(statePair))
                {
                    // (x,y
                    var pair = statePair.Split(',');
                    if (pair.Length != 2)
                    {
                        Log.Warning($"{statePair} is not in the valid (key, value) pair format, skipping...");
                        continue;
                    }

                    AddSprintDelay(pair[0].Replace("(", string.Empty), pair[1]);
                }
            }

            timer.Stop();
            Log.Info($"UpdateDelayStates: {timer.Elapsed.TotalMilliseconds}ms");
        }

        public static void AddSprintDelay(string typeFullName, string value)
        {
            value ??= string.Empty;
            if (!TypeFullNameToStateIndex.TryGetValue(typeFullName, out var index))
            {
                Log.Warning($"Type: {typeFullName} | Field: {value} | The type does not exist in the EntityStateCatalog.");
                return;
            }
            
            var type = EntityStateCatalog.GetStateType(index);
            if (type is null)
            {
                Log.Error($"Type: {typeFullName} | Field: {value} | The state exists in the EntityStateCatalog but the type is null.");
                return;
            }

            value = value.Replace(" ", string.Empty);
            if (string.IsNullOrEmpty(value))
                value = "0";

            if (float.TryParse(value, out var val))
            {
                Log.Debug($"Type: {type.FullName} | Value: {val} | Has been added to the custom entity state list.");
                SprintStateDelayTable[index] = val;
            }
            else
                AddFieldInfo(type, index, value);
        }

        public static void AddFieldInfo(Type T, EntityStateIndex index, string name)
        {
            var field = AccessTools.Field(T, name);

            if (field is null)
            {
                Log.Error($"\r\nField with that name could not be found and wasnt able to parse the value as numeric. Type not added.\r\n{T.FullName} : {name ?? "NULL"}");
                return;
            }

            if (field.FieldType != typeof(float))
            {
                Log.Error($"\r\nField must be a float, but the field does exist. Type not added.\r\n{T.FullName} : {field.Name}");
                return;
            }

            if (SprintStateDelayTable.ContainsKey(index))
                Log.Warning($"\r\nOverwriting duplicate entry\r\n{T.FullName} : {field.Name}\r\nold {SprintStateDelayTable[index]?.ToString() ?? "NULL"} | new {field.Name}");
            else
                Log.Debug($"Type: {T.FullName} | Field: {name} | Has been added to the custom entity state list.");

            SprintStateDelayTable[index] = field;
        }
    }
}
