using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.Potion;
using PotionCraft.ObjectBased.AlchemyMachine;
using PotionCraft.ObjectBased.Potion;
using PotionCraft.ObjectBased.UIElements.Books.RecipeBook;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.AlchemyMachineProducts;
using PotionCraftRecipeWaypoints.Scripts.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using static PotionCraft.SaveLoadSystem.ProgressState;
using static PotionCraft.ScriptableObjects.Potion;
using static PotionCraft.ScriptableObjects.Potion.UsedComponent;

namespace PotionCraftRecipeWaypoints.Scripts.Services
{
    /// <summary>
    /// Responsible for interacting with the Potion Craft recipe system
    /// </summary>
    public static class RecipeService
    {
        public static void LoadWaypointsForRecipeLoad()
        {
            StaticStorage.RecipesLoaded = true;
            Managers.Potion.recipeBook.onRecipeAdded.AddListener(RecipeAdded);
            SaveLoadService.LoadWaypoints();
        }

        public static Vector2 GetMapPositionForRecipe(RecipeIndex recipe)
        {
            return GetMapPositionForRecipe(recipe.Recipe);
        }
        public static Vector2 GetMapPositionForRecipe(Potion recipe)
        {
            return recipe.potionFromPanel.serializedPath.indicatorTargetPosition;
        }

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

        public static void RecipeDeleted(Potion recipe)
        {
            var matchingWaypoint = StaticStorage.Waypoints.FirstOrDefault(w => w.Recipe.Recipe == recipe);
            if (matchingWaypoint == null)
            {
                Plugin.PluginLogger.LogError("Error: unable to find saved waypoint for recipe deletion!");
                return;
            }
            UIService.DeleteWaypoint(matchingWaypoint);
        }

        public static void RecipeAdded()
        {
            var existingWaypointIndexes = StaticStorage.Waypoints.Select(w => w.Recipe.Index).ToList();
            var newRecipe = GetWaypointRecipes(UIService.GetCurrentPotionBase()).FirstOrDefault(p => !existingWaypointIndexes.Contains(p.Index));
            UIService.AddWaypointToMap(newRecipe);
        }

        public static bool IsWaypointRecipe(Potion recipe, bool returnIgnored = false)
        {
            const int waypointGenerationDistance = 5;
            if (recipe == null) return false;
            if (!returnIgnored && StaticStorage.IgnoredWaypoints.Contains(Managers.Potion.recipeBook.savedRecipes.IndexOf(recipe))) return false;
            if (IsLegendaryRecipe(recipe)) return false;
            var recipeMapPosition = GetMapPositionForRecipe(recipe);
            //Don't make waypoints within 5 indicator diameters of the center
            if (Vector2.Distance(Vector2.zero, recipeMapPosition) < UIService.GetIndicatorDiameter() * waypointGenerationDistance) return false;
            if (recipe.Effects.Length == 0 || recipe.Effects[0] == null) return true;
            var distanceToEffect = Vector2.Distance(recipeMapPosition, UIService.GetEffectMapLocation(recipe.Effects.Last(), recipe.potionBase));
            if (distanceToEffect >= UIService.GetIndicatorDiameter() * waypointGenerationDistance) return true;
            return false;
        }

        public static RecipeIndex GetWaypointRecipeAtIndex(int index)
        {
            var recipe = Managers.Potion.recipeBook.savedRecipes[index];
            if (!IsWaypointRecipe(recipe, true)) return null;
            return new RecipeIndex { Index = index, Recipe = recipe };
        }

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
                RecipeDeleted(recipe.Recipe);
            }
            UIService.UpdateCurrentRecipePage();
        }

        public static bool IsLegendaryRecipe(Potion recipe)
        {
            return recipe.potionFromPanel.recipeMarks.Count(m => m.type == SerializedRecipeMark.Type.PotionBase) > 1;
        }

        public static RecipeIndex GetRecipeIndexObject(Potion recipe)
        {
            return new RecipeIndex { Recipe = recipe, Index = Managers.Potion.recipeBook.savedRecipes.IndexOf(recipe) };
        }
    }
}
