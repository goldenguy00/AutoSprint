using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using MiscFixes.Modules;
using UnityEngine;

namespace AutoSprint
{
    internal static class PluginConfig
    {
        public static ConfigEntry<bool> EnableMod { get; private set; }
        public static ConfigEntry<bool> HoldSprintToWalk { get; private set; }
        public static ConfigEntry<bool> DisableSprintingCrosshair { get; private set; }
        public static ConfigEntry<bool> DisableSprintingSpeedLines { get; private set; }
        public static ConfigEntry<int> FovSlider { get; private set; }
        public static ConfigEntry<bool> DisableSprintingFOV { get; private set; }
        public static ConfigEntry<bool> ForceSprintingFOV { get; private set; }
        public static ConfigEntry<bool> EnableOmniSprint { get; private set; }

        public static ConfigEntry<bool> EnableDebugMode { get; private set; }
        public static ConfigEntry<int> BaseDelayTicks { get; private set; }
        public static ConfigEntry<int> DelayTicks { get; private set; }
        public static ConfigEntry<string> DisabledBodies { get; private set; }
        public static ConfigEntry<string> DisableSprintingCustomList { get; private set; }
        public static ConfigEntry<string> DisableSprintingCustomList2 { get; private set; }

        public static void Init(ConfigFile cfg)
        {
            if (AutoSprintPlugin.RooInstalled)
                InitRoO();

            EnableMod = cfg.BindOption(
                "General",
                "Enable Mod",
                "Set to false to disable all functionality.",
                true,
                Extensions.ConfigFlags.ClientSided);

            HoldSprintToWalk = cfg.BindOption(
                "General",
                "Hold Sprint To Walk",
                "Walk by holding down the sprint key. If disabled, makes the Sprint key toggle AutoSprinting functionality on and off.",
                true,
                Extensions.ConfigFlags.ClientSided);

            DisableSprintingCrosshair = cfg.BindOption(
                "General",
                "Disable Sprinting Crosshair",
                "Disables the special sprinting chevron crosshair.",
                true,
                Extensions.ConfigFlags.ClientSided);

            DisableSprintingSpeedLines = cfg.BindOption(
                "General",
                "Disable Sprinting Speed Lines",
                "Disables the speed lines on the edges of the screen when sprinting.",
                false,
                Extensions.ConfigFlags.ClientSided);

            FovSlider = cfg.BindOptionSlider(
                "General",
                "Global FOV Increase",
                "Adds the configured value to all fov calculations. Set to 0 to disable.",
                0,
                0, 60,
                Extensions.ConfigFlags.ClientSided);

            ForceSprintingFOV = cfg.BindOption(
                "General",
                "Force Sprinting FOV",
                "Changes the FOV to be constantly set to the 1.3x multiplier. This overrides the \"Disable Sprinting FOV Increase\" setting. Disable both for vanilla behavior.",
                true,
                Extensions.ConfigFlags.ClientSided);

            DisableSprintingFOV = cfg.BindOption(
                "General",
                "Disable Sprinting FOV Increase",
                "Disables the change in FOV when sprinting. This setting requires the \"Force Sprinting FOV\" to be disabled for it to have any effect. Disable both for vanilla behavior.",
                false,
                Extensions.ConfigFlags.ClientSided);

            EnableOmniSprint = cfg.BindOption(
                "General",
                "Enable OmniSprint",
                "Allows sprinting in all directions. This is generally considered cheating, use with discretion.",
                false,
                Extensions.ConfigFlags.ClientSided);

            //  advanced

            EnableDebugMode = cfg.BindOption(
                "Advanced",
                "Enable Debug Mode",
                "Prints every entity state that your character changes to.",
                false,
                Extensions.ConfigFlags.ClientSided);

            BaseDelayTicks = cfg.BindOptionSlider(
                "Advanced",
                "Base Delay",
                "How long to wait, in game ticks, before sprinting. Game runs at 60hz, so 1 tick == 16ms",
                5,
                0, 60,
                Extensions.ConfigFlags.ClientSided);

            DelayTicks = cfg.BindOptionSlider(
                "Advanced",
                "Skill Activation Delay",
                "How long to wait, in game ticks, before sprinting after beginning certain skills. Game runs at 60hz, so 1 tick == 16ms",
                20,
                0, 60,
                Extensions.ConfigFlags.ClientSided);

            DisabledBodies = cfg.BindOption(
                "Advanced",
                "Disable Body",
                "Custom body name list, has to match body catalog name.",
                "",
                Extensions.ConfigFlags.ClientSided);

            DisableSprintingCustomList = cfg.BindOption(
                "Advanced",
                "Disable Sprint Custom List",
                "Custom EntityState list for when a skill is cancelled by sprinting when it shouldn't, separated by commas." +
                "\r\nThe Debug Mode cfg option will print the state names to the Bepinex console/log output.\r\n\r\n" +
                "Example: EntityStates.Toolbot.ToolbotDualWield",
                "",
                Extensions.ConfigFlags.ClientSided);

            DisableSprintingCustomList2 = cfg.BindOption(
                "Advanced",
                "Disable Sprint With Duration List",
                "(typeFullName, fieldName) --or-- (typeFullName, ###)\r\n\r\n" +
                "Example: (EntityStates.Toolbot.ToolbotDualWieldStart, baseDuration)\r\n" +
                " --or--  (EntityStates.Toolbot.ToolbotDualWieldStart, 0.75)",
                "", 
                Extensions.ConfigFlags.ClientSided);

        } // End of SetupConfiguration()


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
    }
}
