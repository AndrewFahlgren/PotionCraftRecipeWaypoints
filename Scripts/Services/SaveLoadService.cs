using PotionCraft.SaveFileSystem;
using PotionCraft.SaveLoadSystem;
using PotionCraftRecipeWaypoints.Scripts.Storage;
using System;
using System.Text;
using System.Linq;
using PotionCraft.ManagersSystem;
using PotionCraft.ScriptableObjects;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PotionCraftRecipeWaypoints.Scripts.Services
{
    public static class SaveLoadService
    {
        private class IgnoredWaypointsDeserialized
        {
            [JsonProperty(StaticStorage.RecipeWaypointsJsonSaveName)]
            public List<int> IgnoredWaypoints { get; set; }
        }

        /// <summary>
        /// Postfix method for StoreIgnoredWaypointsPatch
        /// Stores ignored waypoints list at the end of the save file with a custom field name
        /// </summary>
        public static void StoreIgnoredWaypoints(ref string result)
        {
            string modifiedResult = null;
            var savedStateJson = result;
            Ex.RunSafe(() =>
            {
                if (!StaticStorage.IgnoredWaypoints.Any()) return;
                //Serialize recipies to json
                var sbIgnoredWaypoints = new StringBuilder();
                sbIgnoredWaypoints.Append('[');
                var firstRecipe = true;
                StaticStorage.IgnoredWaypoints.ForEach(index =>
                {
                    if (!firstRecipe)
                    {
                        sbIgnoredWaypoints.Append(',');
                    }
                    firstRecipe = false;
                    sbIgnoredWaypoints.Append(index);
                });
                sbIgnoredWaypoints.Append(']');
                var serialized = $",\"{StaticStorage.RecipeWaypointsJsonSaveName}\":{sbIgnoredWaypoints}";
                //Insert custom field at the end of the save file at the top level
                var insertIndex = savedStateJson.LastIndexOf('}');
                modifiedResult = savedStateJson.Insert(insertIndex, serialized);
            });
            if (!string.IsNullOrEmpty(modifiedResult))
            {
                result = modifiedResult;
            }
        }

        /// <summary>
        /// Prefix method for RetreiveStoredIgnoredWaypointsPatch
        /// Reads the raw json string to find our custom field and parse any ignored waypoints within it
        /// </summary>
        public static bool RetreiveStoredIgnoredWaypoints()
        {
            var stateJsonString = StaticStorage.StateJsonString;
            StaticStorage.StateJsonString = null;
            if (string.IsNullOrEmpty(stateJsonString))
            {
                Plugin.PluginLogger.LogInfo("Error: stateJsonString is empty. Cannot load ignored waypoints");
                return true;
            }
            //Check if there are any existing ignored waypoints in save file
            var keyIndex = stateJsonString.IndexOf(StaticStorage.RecipeWaypointsJsonSaveName);
            if (keyIndex == -1)
            {
                Plugin.PluginLogger.LogInfo("No existing ignored recipes found during load");
                return true;
            }

            //Deserialize the bookmark groups from json using our dummy class
            var deserialized = JsonConvert.DeserializeObject<IgnoredWaypointsDeserialized>(stateJsonString, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            if (deserialized.IgnoredWaypoints == null)
            {
                Plugin.PluginLogger.LogError("Error: An error occured during ignored recipie deserialization");
                return true;
            }

            StaticStorage.IgnoredWaypoints = deserialized.IgnoredWaypoints;

            return true;
        }

        /// <summary>
        /// Prefix method for RetrieveStateJsonStringPatch.
        /// This method retrieves the raw json string and stores it in static storage for later use.
        /// The StateJsonString is inaccessible later on when we need it so this method is necessary to provide access to it.
        /// </summary>
        public static bool RetrieveStateJsonString(File instance)
        {
            StaticStorage.StateJsonString = instance.StateJsonString;
            return true;
        }

        /// <summary>
        /// Prefix method for ClearFileSpecificDataOnFileLoadPatch
        /// Clears out any stored static data from a previous game file if this isn't the first load of the session
        /// </summary>
        /// <returns></returns>
        public static bool ClearFileSpecificDataOnFileLoad()
        {
            StaticStorage.Waypoints.ForEach(waypoint => UnityEngine.Object.Destroy(waypoint.gameObject));
            StaticStorage.LoadedPotionBases.Clear();
            StaticStorage.Waypoints.Clear();
            StaticStorage.IgnoredWaypoints.Clear();
            StaticStorage.RecipesLoaded = false;
            return true;
        }

        /// <summary>
        /// Postfix method for LoadWaypoints
        /// Loads the waypoints for the current map if the recipe book is already loaded
        /// </summary>
        public static void LoadWaypoints()
        {
            if (!StaticStorage.RecipesLoaded) return;
            if (StaticStorage.LoadedPotionBases == null) StaticStorage.LoadedPotionBases = new List<PotionBase>();
            var loadedPotionBase = UIService.GetCurrentPotionBase();
            if (loadedPotionBase == null)
            {
                Plugin.PluginLogger.LogInfo($"Error: failed to get current potion base");
                return;
            }
            if (StaticStorage.LoadedPotionBases.Any(b => b.name == loadedPotionBase.name))
            {
                Plugin.PluginLogger.LogInfo($"Error: already loaded potion base");
                return;
            }
            StaticStorage.LoadedPotionBases.Add(loadedPotionBase);
            UIService.AddWaypointsToMap(RecipeService.GetWaypointRecipes(loadedPotionBase));
            UIService.CreateWaypointToggleButton(Managers.RecipeMap.recipeMapObject.followIndicatorButton);
        }

        /// <summary>
        /// Manually parses the json to find the closing bracket for this json object.
        /// </summary>
        /// <param name="input">the json string to parse.</param>
        /// <param name="startIndex">the openning bracket of this object/list.</param>
        /// <param name="useBrackets">if true this code will look for closing brackets and if false this code will look for curly braces.</param>
        /// <returns></returns>
        private static int GetEndJsonIndex(string input, int startIndex, bool useBrackets)
        {
            var endIndex = startIndex + 1;
            var unclosedCount = 1;
            var openChar = useBrackets ? '[' : '{';
            var closeChar = useBrackets ? ']' : '}';
            while (unclosedCount > 0 && endIndex < input.Length - 1)
            {
                var nextOpenIndex = input.IndexOf(openChar, endIndex);
                var nextCloseIndex = input.IndexOf(closeChar, endIndex);
                if (nextOpenIndex == -1 && nextCloseIndex == -1)
                {
                    break;
                }
                if (nextOpenIndex == -1) nextOpenIndex = int.MaxValue;
                if (nextCloseIndex == -1) nextCloseIndex = int.MaxValue;
                if (nextOpenIndex < nextCloseIndex)
                {
                    endIndex = nextOpenIndex + 1;
                    unclosedCount++;
                }
                else if (nextCloseIndex < nextOpenIndex)
                {
                    endIndex = nextCloseIndex + 1;
                    unclosedCount--;
                }
            }
            return endIndex;
        }
    }
}
