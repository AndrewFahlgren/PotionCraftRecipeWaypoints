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

        //The list of all stored waypoints
        public static List<WaypointMapItem> Waypoints = new();
        //The list of all waypoints which have been ignored by the user
        public static List<int> IgnoredWaypoints = new();

        //Temporary waypoints created if the BrewFromHere mod is also installed
        public static WaypointMapItem TemporaryWaypoint;

        //The main map waypoint toggle button
        public static WaypointToggleButton WaypointToggleButton;
        //The recipe waypoint toggle button for each right page instance
        public static Dictionary<RecipeBookRightPageContent, WaypointToggleButton> WaypointToggleButtonRecipeBook = new();
        //The custom brew potion button for each right page instance
        public static Dictionary<RecipeBookRightPageContent, RecipeBookBrewPotionButton> WaypointBrewPotionButton = new();

        //Keeps track of which maps we have loaded
        public static List<PotionBase> LoadedPotionBases = new();
        //Flag for determining if the recipe book has been populated on load
        public static bool RecipesLoaded;
        //A saved reference for ease of use to the main sprite used for waypoint related ui elements
        public static Sprite RecipeWaypointSprite;
        //The current state of the main map waypoint toggle button
        public static bool WaypointsVisible;
        //Ensures we do not add a listener more than once
        public static bool AddedListeners;

        //Used only during save for a short period of time
        public static string StateJsonString;

        public static bool BrewFromHereInstalled;
    }
}
