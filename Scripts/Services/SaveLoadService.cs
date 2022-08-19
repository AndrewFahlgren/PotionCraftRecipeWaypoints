using PotionCraft.SaveFileSystem;
using PotionCraft.SaveLoadSystem;
using PotionCraftRecipeWaypoints.Scripts.Storage;
using System;
using System.Text;
using System.Linq;
using PotionCraft.ManagersSystem;
using PotionCraft.ScriptableObjects;
using System.Collections.Generic;

namespace PotionCraftRecipeWaypoints.Scripts.Services
{
    public static class SaveLoadService
    {
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

        public static bool RetreiveStoredIgnoredWaypoints(Type type)
        {
            if (type != typeof(ProgressState)) return true;
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
            //Determine the start of the list object containing the data
            var startSavedIgnoredWaypointsIndex = stateJsonString.IndexOf('[', keyIndex);
            if (startSavedIgnoredWaypointsIndex == -1)
            {
                Plugin.PluginLogger.LogInfo("Error: ignored waypoints are not formed correctly. No ignored waypoints will be loaded (bad start index).");
                return true;
            }

            //Find the closing bracket of the list
            var endSavedIgnoredWaypointsIndex = GetEndJsonIndex(stateJsonString, startSavedIgnoredWaypointsIndex, true);
            if (endSavedIgnoredWaypointsIndex >= stateJsonString.Length)
            {
                Plugin.PluginLogger.LogInfo("Error: ignored waypoints are not formed correctly. No ignored waypoints will be loaded (bad end index).");
                return true;
            }

            //Read through the list parsing each int manually since list deserialization is not supported in unity
            var savedIgnoredWaypointsJson = stateJsonString.Substring(startSavedIgnoredWaypointsIndex, endSavedIgnoredWaypointsIndex - startSavedIgnoredWaypointsIndex);
            if (savedIgnoredWaypointsJson.Length <= 2)
            {
                Plugin.PluginLogger.LogInfo("No existing ignored waypoints found during load");
                return true;
            }
            var objectEndIndex = 0;
            //Continue parsing list until we find a non-comma character after the parsed object
            while (objectEndIndex == 0 || savedIgnoredWaypointsJson[objectEndIndex] != ',')
            {
                var objectStartIndex = objectEndIndex + 1;
                objectEndIndex = savedIgnoredWaypointsJson.IndexOf(',', objectStartIndex);
                var curIndexJson = savedIgnoredWaypointsJson.Substring(objectStartIndex, objectEndIndex - objectStartIndex);
                if (!int.TryParse(curIndexJson, out int savedIndex))
                {
                    Plugin.PluginLogger.LogInfo("Error: ignored waypoints are not in sync with save file. Some ignored waypoints may have been loaded (failed to parse index).");
                    break;
                }

                StaticStorage.IgnoredWaypoints.Add(savedIndex);
            }
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

        public static bool ClearFileSpecificDataOnFileLoad()
        {
            StaticStorage.Waypoints.ForEach(waypoint =>
            {
                UnityEngine.Object.Destroy(waypoint.gameObject);
            });

            StaticStorage.LoadedPotionBases.Clear();
            StaticStorage.Waypoints.Clear();
            StaticStorage.IgnoredWaypoints.Clear();
            StaticStorage.RecipesLoaded = false;
            return true;
        }


        public static void LoadWaypoints()
        {
            Plugin.PluginLogger.LogInfo($"LoadWaypoints - start");
            if (!StaticStorage.RecipesLoaded) return;
            if (StaticStorage.LoadedPotionBases == null) StaticStorage.LoadedPotionBases = new List<PotionBase>();
            var loadedPotionBase = UIService.GetCurrentPotionBase();
            if (loadedPotionBase == null)
            {
                Plugin.PluginLogger.LogInfo($"LoadWaypoints - loadedPotionBase == null");
                return;
            }
            if (StaticStorage.LoadedPotionBases.Any(b => b.name == loadedPotionBase.name))
            {
                Plugin.PluginLogger.LogInfo($"LoadWaypoints - already loaded potion base");
                return;
            }
            StaticStorage.LoadedPotionBases.Add(loadedPotionBase);
            UIService.AddWaypointsToMap(RecipeService.GetWaypointRecipes(loadedPotionBase));
            UIService.CreateWaypointToggleButton(Managers.RecipeMap.recipeMapObject.followIndicatorButton);
            Plugin.PluginLogger.LogInfo($"LoadWaypoints - end");
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
