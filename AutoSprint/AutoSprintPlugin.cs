using System.IO;
using System.Security;
using System.Security.Permissions;
using AutoSprint.Core;
using BepInEx;
using BepInEx.Bootstrap;

[module: UnverifiableCode]
[assembly: HG.Reflection.SearchableAttribute.OptIn]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace AutoSprint
{
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class AutoSprintPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = $"com.{PluginAuthor}.{PluginName}";
        public const string PluginAuthor = "score";
        public const string PluginName = "AutoSprint";
        public const string PluginVersion = "1.4.7";

        internal static bool RooInstalled => Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");

        public static AutoSprintPlugin Instance { get; private set; }

        internal string DirectoryName => Path.GetDirectoryName(Info.Location);

        public void Awake()
        {
            Instance = this;

            Log.Init(Logger);
            PluginConfig.Init(Config);
            Hooks.Init();
        }
    }
}
