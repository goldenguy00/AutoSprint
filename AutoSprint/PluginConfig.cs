﻿using System.Runtime.CompilerServices;
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

            DelayTicks = BindOption(
                "General",
                "DelayTicks",
                3,
                "How long to wait before sprinting. A tick == 60hz == 16ms");

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
        public static ConfigEntry<T> BindOption<T>(string section, string name, T defaultValue, string description = "", bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, description);

            if (AutoSprintPlugin.RooInstalled)
                TryRegisterOption(configEntry, restartRequired);

            return configEntry;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOptionSlider<T>(string section, string name, T defaultValue, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, description);

            if (AutoSprintPlugin.RooInstalled)
                TryRegisterOptionSlider(configEntry, min, max, restartRequired);

            return configEntry;
        }
        #endregion

        #region RoO
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void InitRoO()
        {
            RiskOfOptions.ModSettingsManager.SetModDescription("AutoSprint");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TryRegisterOption<T>(ConfigEntry<T> entry, bool restartRequired)
        {
            if (entry is ConfigEntry<string> stringEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StringInputFieldOption(stringEntry, new RiskOfOptions.OptionConfigs.InputFieldConfig()
                {
                    restartRequired = restartRequired,
                    submitOn = RiskOfOptions.OptionConfigs.InputFieldConfig.SubmitEnum.OnExitOrSubmit
                }));
                return;
            }
            if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = 0,
                    max = 20,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
                return;
            }
            if (entry is ConfigEntry<int> intEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(intEntry, restartRequired));
                return;
            }
            if (entry is ConfigEntry<bool> boolEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(boolEntry, restartRequired));
                return;
            }
            if (entry is ConfigEntry<KeyboardShortcut> shortCutEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(shortCutEntry, restartRequired));
                return;
            }
            if (typeof(T).IsEnum)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ChoiceOption(entry, restartRequired));
                return;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
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
                return;
            }

            if (entry is ConfigEntry<float> floatEntry)
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = min,
                    max = max,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
        }
        #endregion

    }
}
