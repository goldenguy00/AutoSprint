using System;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;

namespace AutoSprint
{
    internal static class PluginConfig
    {
        // CONFIGURATION
        public static ConfigFile myConfig;
        public static ConfigEntry<bool> HoldSprintToWalk { get; set; }
        public static ConfigEntry<bool> DisableSprintingCrosshair { get; set; }
        public static ConfigEntry<int> DelayTicks { get; set; }
        public static ConfigEntry<bool> EnableOmniSprint { get; set; }
        public static ConfigEntry<bool> EnableDebugMode { get; set; }
        public static ConfigEntry<string> DisabledBodies { get; set; }
        public static ConfigEntry<string> DisableSprintingCustomList { get; set; }
        public static ConfigEntry<string> DisableSprintingCustomList2 { get; set; }

        public static void Init(ConfigFile cfg)
        {
            myConfig = cfg;

            HoldSprintToWalk = BindOption(
                "General",
                "Hold Sprint To Walk",
                true,
                "Walk by holding down the sprint key. If disabled, makes the Sprint key toggle AutoSprinting functionality on and off.");

            DisableSprintingCrosshair = BindOption(
                "General",
                "Disable Sprinting Crosshair",
                true,
                "Disables the special sprinting chevron crosshair.");

            EnableOmniSprint = BindOption(
                "General",
                "Enable OmniSprint",
                false,
                "Allows sprinting in all directions. This is generally considered cheating, use with discretion.");

            DelayTicks = BindOptionSlider(
                "General",
                "DelayTicks",
                5,
                "How long to wait before sprinting. A tick == 60hz == 16ms",
                0, 60);

            EnableDebugMode = BindOption(
                "General",
                "Enable Debug Mode",
                false,
                "Prints every entity state that your character changes to.");

            //  advanced

            DisabledBodies = BindOption(
                "Advanced",
                "Disable Body",
                "",
                "Custom body name list, has to match body catalog name.");

            DisableSprintingCustomList = BindOption(
                "Advanced",
                "Disable Sprint Custom List",
                "",
                "Custom EntityState list for when broken things break, separated by commas." +
                "\r\nUse console (Ctrl Alt ~) and enter dump_state to find this info (last couple lines)." +
                "\r\nMany skills have multiple states, so it may help to pause while doing this." +
                "\r\n\r\nExample -> PaladinMod.States.Spell.ChannelCruelSun");

            DisableSprintingCustomList2 = BindOption(
                "Advanced",
                "Disable Sprint With Duration List",
                "(EntityStates.Croco.Slash, durationBeforeInterruptable) (EntityStates.Toolbot.ToolbotDualWieldStart, 0.9)",
                "(typeFullName, fieldName) --or-- (typeFullName, ###)\r\n\r\n" +
                "Example: (EntityStates.Toolbot.ToolbotDualWieldStart, baseDuration)\r\n" +
                " --or--  (EntityStates.Toolbot.ToolbotDualWieldStart, 0.75)");

        } // End of SetupConfiguration()


        #region Config Binding
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void InitRoO()
        {
            RiskOfOptions.ModSettingsManager.SetModDescription("AutoSprint, as God intended.");
        }

        public static ConfigEntry<T> BindOption<T>(string section, string name, T defaultValue, string description = "", bool restartRequired = false)
        {
            if (defaultValue is int or float && !typeof(T).IsEnum)
            {
#if DEBUG
                Log.Warning($"Config entry {name} in section {section} is a numeric {typeof(T).Name} type, " +
                    $"but has been registered without using {nameof(BindOptionSlider)}. " +
                    $"Lower and upper bounds will be set to the defaults [0, 20]. Was this intentional?");
#endif
                return BindOptionSlider(section, name, defaultValue, description, 0, 20, restartRequired);
            }
            if (string.IsNullOrEmpty(description))
                description = name;

            if (restartRequired)
                description += " (restart required)";

            AcceptableValueBase range = null;
            if (typeof(T).IsEnum)
                range = new AcceptableValueList<string>(Enum.GetNames(typeof(T)));

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, range));
            TryRegisterOption(configEntry, restartRequired);

            return configEntry;
        }

        public static ConfigEntry<T> BindOptionSlider<T>(string section, string name, T defaultValue, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (!(defaultValue is int or float && !typeof(T).IsEnum))
            {
#if DEBUG
                Log.Warning($"Config entry {name} in section {section} is a not a numeric {typeof(T).Name} type, " +
                    $"but has been registered as a slider option using {nameof(BindOptionSlider)}. Was this intentional?");
#endif
                return BindOption(section, name, defaultValue, description, restartRequired);
            }

            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            AcceptableValueBase range = typeof(T) == typeof(int)
                ? new AcceptableValueRange<int>((int)min, (int)max)
                : new AcceptableValueRange<float>(min, max);

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, range));

            TryRegisterOptionSlider(configEntry, min, max, restartRequired);

            return configEntry;
        }
        public static ConfigEntry<T> BindOptionSteppedSlider<T>(string section, string name, T defaultValue, float increment = 1f, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

            TryRegisterOptionSteppedSlider(configEntry, increment, min, max, restartRequired);

            return configEntry;
        }
        #endregion

        #region RoO
        public static void TryRegisterOption<T>(ConfigEntry<T> entry, bool restartRequired)
        {
            if (entry is ConfigEntry<string> stringEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StringInputFieldOption(stringEntry, new RiskOfOptions.OptionConfigs.InputFieldConfig()
                {
                    submitOn = RiskOfOptions.OptionConfigs.InputFieldConfig.SubmitEnum.OnExitOrSubmit,
                    restartRequired = restartRequired
                }));
            }
            else if (entry is ConfigEntry<bool> boolEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(boolEntry, restartRequired));
            }
            else if (entry is ConfigEntry<KeyboardShortcut> shortCutEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(shortCutEntry, restartRequired));
            }
            else if (typeof(T).IsEnum)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ChoiceOption(entry, restartRequired));
            }
            else
            {
#if DEBUG
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOption)}.");
#endif
            }
        }

        public static void TryRegisterOptionSlider<T>(ConfigEntry<T> entry, float min, float max, bool restartRequired)
        {
            if (entry is ConfigEntry<int> intEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(intEntry, new RiskOfOptions.OptionConfigs.IntSliderConfig()
                {
                    min = (int)min,
                    max = (int)max,
                    formatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
            }
            else if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = min,
                    max = max,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
            }
            else
            {
#if DEBUG
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOptionSlider)}.");
#endif
            }
        }
        public static void TryRegisterOptionSteppedSlider<T>(ConfigEntry<T> entry, float increment, float min, float max, bool restartRequired)
        {
            if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(floatEntry, new RiskOfOptions.OptionConfigs.StepSliderConfig()
                {
                    increment = increment,
                    min = min,
                    max = max,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
            }
            else
            {
#if DEBUG
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOptionSteppedSlider)}.");
#endif
            }
        }
        #endregion
    }
}
