using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using PotionCraftRecipeWaypoints.Scripts.Storage;
using System.Linq;
using System.Reflection;

namespace PotionCraftRecipeWaypoints
{
    [BepInPlugin(PLUGIN_GUID, "PotionCraftRecipeWaypoints", "1.0.5.2")]
    [BepInProcess("Potion Craft.exe")]
    [BepInDependency("com.fahlgorithm.potioncraftbrewfromhere", BepInDependency.DependencyFlags.SoftDependency)]
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

            StaticStorage.BrewFromHereInstalled = Chainloader.PluginInfos.Any(plugin => plugin.Value.Metadata.GUID.Equals("com.fahlgorithm.potioncraftbrewfromhere"));
        }
    }
}
