using PotionCraft.ScriptableObjects;
using PotionCraftRecipeWaypoints.Scripts.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace PotionCraftRecipeWaypoints.Scripts
{
    public class RecipeIndex
    {
        public Potion Recipe;
        public int Index;
        public bool IsWaypoint => !StaticStorage.IgnoredWaypoints.Contains(Index);
    }
}
