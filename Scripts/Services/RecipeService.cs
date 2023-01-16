using PotionCraft.ManagersSystem;
using PotionCraft.ObjectBased.Potion;
using PotionCraft.ObjectBased.UIElements.Bookmarks;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.Potion;
using PotionCraftRecipeWaypoints.Scripts.Storage;
using PotionCraftRecipeWaypoints.Scripts.UIComponents;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PotionCraftRecipeWaypoints.Scripts.Services
{
    /// <summary>
    /// Responsible for interacting with the Potion Craft recipe system
    /// </summary>
    public static class RecipeService
    {
        /// <summary>
        /// Postfix method for LoadWaypointsForRecipeLoadPatch
        /// Loads waypoints for the currently loaded map when the recipe book has finished loading
        /// </summary>
        public static void LoadWaypointsForRecipeLoad()
        {
            StaticStorage.RecipesLoaded = true;
            if (!StaticStorage.AddedListeners)
            {
                Managers.Potion.recipeBook.onRecipeAdded.AddListener(RecipeAdded);
                Managers.Potion.recipeBook.bookmarkControllersGroupController.onBookmarksRearranged.AddListener(BookmarksRearranged);
                StaticStorage.AddedListeners = true;
            }
            SaveLoadService.LoadWaypoints();
        }

        public static void OpenPageOnWaypointClick(WaypointMapItem waypoint)
        {
            if (StaticStorage.BrewFromHereInstalled 
                && waypoint != StaticStorage.TemporaryWaypoint)
            {
                //In this case a bit of logic needs to be done to ensure BrewFromHere resets the saved recipe mark index
                Managers.Potion.recipeBook.onPageChanged.Invoke(Managers.Potion.recipeBook.currentPageIndex, waypoint.Recipe.Index);
            }
            Managers.Potion.recipeBook.OpenPageAt(waypoint.Recipe.Index);
        }

        /// <summary>
        /// Returns the saved map position of the indicator for this recipe
        /// </summary>
        public static Vector2 GetMapPositionForRecipe(RecipeIndex recipe)
        {
            return GetMapPositionForRecipe(recipe.Recipe);
        }

        public static bool IsTailEndWaypoint(Potion recipe)
        {
            return GetMapPositionForRecipe(recipe, true) != GetMapPositionForRecipe(recipe);
        }

        public static bool RecipeDeletedFromBook(Potion recipe)
        {
            var recipeIndex = Managers.Potion.recipeBook.savedRecipes.IndexOf(recipe);
            //We don't want the next recipe that goes into this slot to be ignored so remove this index from the ignore list
            if (StaticStorage.IgnoredWaypoints.Contains(recipeIndex))
            {
                StaticStorage.IgnoredWaypoints.Remove(recipeIndex);
                return true;
            }
            //If this recipe is not ignored then make sure to delete the waypoint
            RemoveWaypoint(recipe);
            return true;
        }

        /// <summary>
        /// Returns the saved map position of the indicator for this recipe
        /// </summary>
        public static Vector2 GetMapPositionForRecipe(Potion recipe, bool forceReturnIndicatorPosition = false)
        {
            var pos = recipe.potionFromPanel.serializedPath.indicatorTargetPosition;
            if (forceReturnIndicatorPosition) return pos;

            //Return the tail end location for tail end waypoints
            var exclusionZone = GetWaypointExclusionZone();
            //Tail end waypoints include waypoints which are too close to the center to display or those which have a very long path already added
            if (Vector2.Distance(Vector2.zero, pos) < exclusionZone || HasLongRemainingPath(recipe))
            {
                pos = GetTailEndMapPositionForRecipe(recipe);
            }

            return pos;
        }

        /// <summary>
        /// Returns the saved map position of the indicator for this recipe
        /// </summary>
        public static Vector2 GetTailEndMapPositionForRecipe(Potion recipe)
        {
            var lastPathPoint = recipe.potionFromPanel.serializedPath.fixedPathPoints
                                                                     .LastOrDefault(fpp => fpp.graphicsPoints?.Any() ?? false)
                                                                     ?.graphicsPoints
                                                                     ?.LastOrDefault() 
                                                                     ?? Vector2.zero;
            var offset = recipe.potionFromPanel.serializedPath.indicatorTargetPosition + recipe.potionFromPanel.serializedPath.pathPosition;
            return new Vector2(lastPathPoint.x, lastPathPoint.y) + offset;
        }


        /// <summary>
        /// Returns all waypoint recipes for the specified potion base (excluding ignored)
        /// </summary>
        public static List<RecipeIndex> GetWaypointRecipes(PotionBase loadedPotionBase)
        {
            var savedRecipes = Managers.Potion.recipeBook.savedRecipes;
            var returnList = new List<RecipeIndex>();
            for (var i = 0; i < savedRecipes.Count; i++)
            {
                var curRecipe = savedRecipes[i];
                if (curRecipe == null) continue;
                if (curRecipe.potionBase.name != loadedPotionBase.name) continue;
                if (!IsWaypointRecipe(curRecipe)) continue;

                returnList.Add(new RecipeIndex { Index = i, Recipe = curRecipe });
            }
            return returnList;
        }

        /// <summary>
        /// Postfix method for RecipeDeletedPatch
        /// Finds and removes the waypoint for the deleted recipe if there was a waypoint for that recipe
        /// </summary>
        public static void RemoveWaypoint(Potion recipe)
        {
            var matchingWaypoint = StaticStorage.Waypoints.FirstOrDefault(w => w.Recipe.Recipe == recipe);
            if (matchingWaypoint == null)
            {
                return;
            }
            UIService.AddMissingWaypointsAroundDeletedWaypoint(matchingWaypoint);
            UIService.DeleteWaypoint(matchingWaypoint);
        }

        /// <summary>
        /// Finds and adds the new recipe waypoint if the new recipe is a waypoint
        /// </summary>
        public static void RecipeAdded()
        {
            var existingWaypointIndexes = StaticStorage.Waypoints.Select(w => w.Recipe.Index).ToList();
            var newRecipe = GetWaypointRecipes(UIService.GetCurrentPotionBase()).FirstOrDefault(p => !existingWaypointIndexes.Contains(p.Index));
            if (newRecipe == null) return;
            UIService.AddWaypointToMap(newRecipe);
        }

        private static void BookmarksRearranged(BookmarkController bookmarksController, List<int> intList)
        {
            var oldIgnored = StaticStorage.IgnoredWaypoints.ToList();
            var newIgnored = new List<int>();
            var oldWaypointIndexes = StaticStorage.Waypoints.Select(w => new { waypoint = w, oldIndex = w.Recipe.Index}).ToList();
            for (var newIndex = 0; newIndex < intList.Count; newIndex++)
            {
                var oldIndex = intList[newIndex];
                //This will recreate the old ignored list making sure to update any indexes along the way
                if (oldIgnored.Contains(oldIndex)) newIgnored.Add(newIndex);
                if (intList[newIndex] == newIndex) continue;
                var affectedWaypoint = oldWaypointIndexes.FirstOrDefault(w => w.oldIndex == oldIndex);
                if (affectedWaypoint == null) continue;
                affectedWaypoint.waypoint.Recipe.Index = newIndex;
            }
            StaticStorage.IgnoredWaypoints = newIgnored;
        }

        /// <summary>
        /// Returns whether or not this recipe is a waypoint recipe
        /// </summary>
        /// <param name="recipe">the recipe to check</param>
        /// <param name="returnIgnored">if true will bypass the ignore list
        /// if false this method will return false if the recipe is in the ignore list</param>
        public static bool IsWaypointRecipe(Potion recipe, bool returnIgnored = false)
        {
            if (recipe == null) return false;
            if (!returnIgnored && StaticStorage.IgnoredWaypoints.Contains(Managers.Potion.recipeBook.savedRecipes.IndexOf(recipe))) return false;
            if (IsLegendaryRecipe(recipe)) return false;
            var recipeMapPosition = GetMapPositionForRecipe(recipe, true);
            var exclusionZone = GetWaypointExclusionZone();
            //Don't make waypoints within 5 indicator diameters of the center
            if (Vector2.Distance(Vector2.zero, recipeMapPosition) < exclusionZone)
            {
                //If this is a waypoint close to the center then check to see if the tail end of the path is far enough away to still count as a waypoint.
                return Vector2.Distance(Vector2.zero, GetTailEndMapPositionForRecipe(recipe)) > exclusionZone;
            }
            if (recipe.Effects.Length == 0 || recipe.Effects[0] == null) return true;
            var distanceToEffect = Vector2.Distance(recipeMapPosition, UIService.GetEffectMapLocation(recipe.Effects.Last(), recipe.potionBase));
            if (distanceToEffect >= exclusionZone) return true;
            if (HasLongRemainingPath(recipe)) return true;
            return false;
        }

        private static bool HasLongRemainingPath(Potion recipe)
        {
            return recipe.potionFromPanel.serializedPath.fixedPathPoints.Count > 1;
        }

        public static float GetWaypointExclusionZone()
        {
            const int waypointGenerationDistance = 5;
            var exclusionZone = UIService.GetIndicatorDiameter() * waypointGenerationDistance;
            return exclusionZone;
        }

        /// <summary>
        /// Returns the recipe at the specified recipe index if the recipe is a waypoint
        /// </summary>
        public static RecipeIndex GetWaypointRecipeAtIndex(int index)
        {
            var recipe = Managers.Potion.recipeBook.savedRecipes[index];
            if (!IsWaypointRecipe(recipe, true)) return null;
            return new RecipeIndex { Index = index, Recipe = recipe };
        }

        /// <summary>
        /// Toggles the ignored status of the selected recipe
        /// </summary>
        public static void ToggleWaypointForSelectedRecipe(RecipeIndex recipe)
        {
            if (recipe == null) return;
            if (StaticStorage.IgnoredWaypoints.Contains(recipe.Index))
            {
                StaticStorage.IgnoredWaypoints.Remove(recipe.Index);
                RecipeAdded();
            }
            else
            {
                StaticStorage.IgnoredWaypoints.Add(recipe.Index);
                RemoveWaypoint(recipe.Recipe);
            }
            UIService.UpdateCurrentRecipePage();
        }

        /// <summary>
        /// Determines if this is a legendary recipe from the Alchemy Machine Recipes mod
        /// </summary>
        public static bool IsLegendaryRecipe(Potion recipe)
        {
            return recipe.potionFromPanel.recipeMarks.Count(m => m.type == SerializedRecipeMark.Type.PotionBase) > 1;
        }

        /// <summary>
        /// Returns the index for a recipe
        /// </summary>
        public static RecipeIndex GetRecipeIndexObject(Potion recipe)
        {
            return new RecipeIndex { Recipe = recipe, Index = Managers.Potion.recipeBook.savedRecipes.IndexOf(recipe) };
        }
    }
}
