using BepInEx;
using BepInEx.Bootstrap;

namespace AutoSprint
{
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class AutoSprintPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = $"com.{PluginAuthor}.{PluginName}";
        public const string PluginAuthor = "score";
        public const string PluginName = "AutoSprint";
        public const string PluginVersion = "1.3.0";

        public static bool RooInstalled => Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");

        public static AutoSprintPlugin Instance { get; private set; }

        public void Awake()
        {
            Instance = this;

            Log.Init(Logger);
            PluginConfig.Init(Config);
            AutoSprintManager.Init();
            Hooks.Init();
        }
    }
}
