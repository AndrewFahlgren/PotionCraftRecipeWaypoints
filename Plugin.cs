using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;

namespace PotionCraftRecipeWaypoints
{
    [BepInPlugin(PLUGIN_GUID, "PotionCraftRecipeWaypoints", "0.5.0")]
    [BepInProcess("Potion Craft.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.fahlgorithm.potioncraftrecipewaypoints";

        public static ManualLogSource PluginLogger {get; private set; }

        private void Awake()
        {
            PluginLogger = Logger;
            PluginLogger.LogInfo($"Plugin {PLUGIN_GUID} is loaded!");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PLUGIN_GUID);
            PluginLogger.LogInfo($"Plugin {PLUGIN_GUID}: Patch Succeeded!");
        }
    }
}
