using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using UnityEngine;

namespace AutoSprint
{
    internal static class PluginConfig
    {
        public static ConfigEntry<bool> HoldSprintToWalk { get; set; }
        public static ConfigEntry<bool> DisableSprintingCrosshair { get; set; }
        public static ConfigEntry<int> FovSlider { get; set; }
        public static ConfigEntry<bool> DisableSprintingFOV { get; set; }
        public static ConfigEntry<bool> ForceSprintingFOV { get; set; }
        public static ConfigEntry<int> BaseDelayTicks { get; set; }
        public static ConfigEntry<int> DelayTicks { get; set; }
        public static ConfigEntry<bool> EnableOmniSprint { get; set; }
        public static ConfigEntry<bool> EnableDebugMode { get; set; }
        public static ConfigEntry<string> DisabledBodies { get; set; }
        public static ConfigEntry<string> DisableSprintingCustomList { get; set; }
        public static ConfigEntry<string> DisableSprintingCustomList2 { get; set; }

        public static void Init(ConfigFile cfg)
        {
            if (AutoSprintPlugin.RooInstalled)
                InitRoO();

            HoldSprintToWalk = cfg.BindOption(
                "General",
                "Hold Sprint To Walk",
                true,
                "Walk by holding down the sprint key. If disabled, makes the Sprint key toggle AutoSprinting functionality on and off.");

            DisableSprintingCrosshair = cfg.BindOption(
                "General",
                "Disable Sprinting Crosshair",
                true,
                "Disables the special sprinting chevron crosshair.");

            FovSlider = cfg.BindOptionSlider(
                "General",
                "Global FOV Increase",
                0,
                "Adds the configured value to all fov calculations. Set to 0 to disable.",
                0, 60);

            ForceSprintingFOV = cfg.BindOption(
                "General",
                "Force Sprinting FOV",
                true,
                "Changes the FOV to be constantly set to the 1.3x multiplier. This overrides the \"Disable Sprinting FOV Increase\" setting. Disable both for vanilla behavior.");

            DisableSprintingFOV = cfg.BindOption(
                "General",
                "Disable Sprinting FOV Increase",
                false,
                "Disables the change in FOV when sprinting. This setting requires the \"Force Sprinting FOV\" to be disabled for it to have any effect. Disable both for vanilla behavior.");

            EnableOmniSprint = cfg.BindOption(
                "General",
                "Enable OmniSprint",
                false,
                "Allows sprinting in all directions. This is generally considered cheating, use with discretion.");

            BaseDelayTicks = cfg.BindOptionSlider(
                "General",
                "Base Delay",
                5,
                "How long to wait, in game ticks, before sprinting. Game runs at 60hz, so 1 tick == 16ms",
                0, 60);

            DelayTicks = cfg.BindOptionSlider(
                "General",
                "Skill Activation Delay",
                20,
                "How long to wait, in game ticks, before sprinting after beginning certain skills. Game runs at 60hz, so 1 tick == 16ms",
                0, 60);

            EnableDebugMode = cfg.BindOption(
                "General",
                "Enable Debug Mode",
                false,
                "Prints every entity state that your character changes to.");


            //  advanced

            DisabledBodies = cfg.BindOption(
                "Advanced",
                "Disable Body",
                "",
                "Custom body name list, has to match body catalog name.");

            DisableSprintingCustomList = cfg.BindOption(
                "Advanced",
                "Disable Sprint Custom List",
                "EntityStates.Toolbot.ToolbotDualWield,",
                "Custom EntityState list for when a skill is cancelled by sprinting when it shouldn't, separated by commas." +
                "\r\nThe Debug Mode cfg option will print the state names to the Bepinex console/log output.\r\n\r\n" +
                "Example: EntityStates.Toolbot.ToolbotDualWield");

            DisableSprintingCustomList2 = cfg.BindOption(
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
            try
            {
                RiskOfOptions.ModSettingsManager.SetModDescription("AutoSprint, as God intended.", AutoSprintPlugin.PluginGUID, AutoSprintPlugin.PluginName);

                var iconStream = File.ReadAllBytes(Path.Combine(AutoSprintPlugin.Instance.DirectoryName, "icon.png"));
                var tex = new Texture2D(256, 256);
                tex.LoadImage(iconStream);
                var icon = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));

                RiskOfOptions.ModSettingsManager.SetModIcon(icon);
            }
            catch (Exception e)
            {
                Log.Debug(e.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOption<T>(this ConfigFile myConfig, string section, string name, T defaultValue, string description = "", bool restartRequired = false)
        {
            if (defaultValue is int or float && !typeof(T).IsEnum)
            {
#if DEBUG
                Log.Warning($"Config entry {name} in section {section} is a numeric {typeof(T).Name} type, " +
                    $"but has been registered without using {nameof(BindOptionSlider)}. " +
                    $"Lower and upper bounds will be set to the defaults [0, 20]. Was this intentional?");
#endif
                return myConfig.BindOptionSlider(section, name, defaultValue, description, 0, 20, restartRequired);
            }
            if (string.IsNullOrEmpty(description))
                description = name;

            description += $" (Default: {defaultValue})";

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description));

            if (AutoSprintPlugin.RooInstalled)
                TryRegisterOption(configEntry, restartRequired);

            return configEntry;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOptionSlider<T>(this ConfigFile myConfig, string section, string name, T defaultValue, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (!(defaultValue is int or float && !typeof(T).IsEnum))
            {
#if DEBUG
                Log.Warning($"Config entry {name} in section {section} is a not a numeric {typeof(T).Name} type, " +
                    $"but has been registered as a slider option using {nameof(BindOptionSlider)}. Was this intentional?");
#endif
                return myConfig.BindOption(section, name, defaultValue, description, restartRequired);
            }

            if (string.IsNullOrEmpty(description))
                description = name;

            description += $" (Default: {defaultValue})";

            if (restartRequired)
                description += " (restart required)";

            AcceptableValueBase range = typeof(T) == typeof(int)
                ? new AcceptableValueRange<int>((int)min, (int)max)
                : new AcceptableValueRange<float>(min, max);

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, range));

            if (AutoSprintPlugin.RooInstalled)
                TryRegisterOptionSlider(configEntry, min, max, restartRequired);

            return configEntry;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOptionSteppedSlider<T>(this ConfigFile myConfig, string section, string name, T defaultValue, float increment = 1f, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            description += $" (Default: {defaultValue})";

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

            if (AutoSprintPlugin.RooInstalled)
                TryRegisterOptionSteppedSlider(configEntry, increment, min, max, restartRequired);

            return configEntry;
        }
        #endregion

        #region RoO
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
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
                    restartRequired = restartRequired,
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

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
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
