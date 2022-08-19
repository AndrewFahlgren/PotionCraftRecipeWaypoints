using PotionCraft.ObjectBased.UIElements.Books.RecipeBook;
using PotionCraft.ScriptableObjects;
using PotionCraftRecipeWaypoints.Scripts.UIComponents;
using System.Collections.Generic;
using UnityEngine;

namespace PotionCraftRecipeWaypoints.Scripts.Storage
{
    public static class StaticStorage
    {
        public const string RecipeWaypointsJsonSaveName = "FahlgorithmRecipeWaypoints";

        public static List<WaypointMapItem> Waypoints;
        public static List<int> IgnoredWaypoints = new();

        public static WaypointToggleButton WaypointToggleButton;
        public static Dictionary<RecipeBookRightPageContent, WaypointToggleButton> WaypointToggleButtonRecipeBook = new();
        public static Dictionary<RecipeBookRightPageContent, RecipeBookBrewPotionButton> WaypointBrewPotionButton = new();

        public static List<PotionBase> LoadedPotionBases;
        public static bool RecipesLoaded;
        public static Sprite RecipeWaypointSprite;
        public static bool WaypointsVisible;

        //Used only during save for a short period of time
        public static string StateJsonString;
    }
}
