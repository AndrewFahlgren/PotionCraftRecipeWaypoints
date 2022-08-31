using HarmonyLib;
using PotionCraft.ManagersSystem.Potion;
using PotionCraft.ManagersSystem.SaveLoad;
using PotionCraft.ObjectBased.AlchemyMachine;
using PotionCraft.ObjectBased.RecipeMap;
using PotionCraft.ObjectBased.RecipeMap.Buttons;
using PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.PathMapItem;
using PotionCraft.ObjectBased.UIElements;
using PotionCraft.ObjectBased.UIElements.Books.RecipeBook;
using PotionCraft.ObjectBased.UIElements.FinishLegendarySubstanceMenu;
using PotionCraft.ObjectBased.UIElements.Tooltip;
using PotionCraft.SaveFileSystem;
using PotionCraft.SaveLoadSystem;
using PotionCraft.ScriptableObjects;
using PotionCraftRecipeWaypoints.Scripts.Services;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace PotionCraftRecipeWaypoints.Scripts
{
    [HarmonyPatch(typeof(MapState), "LoadState")]
    public class LoadWaypointsPatch
    {
        static void Postfix()
        {
            Ex.RunSafe(() => SaveLoadService.LoadWaypoints());
        }
    }

    [HarmonyPatch(typeof(RecipeBook), "OnLoad")]
    public class LoadWaypointsOnRecipeLoadPatch
    {
        static void Postfix()
        {
            Ex.RunSafe(() => RecipeService.LoadWaypointsForRecipeLoad());
        }
    }

    [HarmonyPatch(typeof(RecipeBook), "EraseRecipe")]
    public class DeleteWaypointOnRecipeDeletePatch
    {
        static void Postfix(Potion potion)
        {
            Ex.RunSafe(() => RecipeService.RecipeDeletedFromBook(potion));
        }
    }

    [HarmonyPatch(typeof(RecipeBookBrewPotionButton), "UpdateVisual")]
    public class ModifyBrewPotionButtonForWaypointRecipesPatch
    {
        static void Postfix(RecipeBookBrewPotionButton __instance, RecipeBookRightPageContent ___rightPageContent)
        {
            Ex.RunSafe(() => UIService.ModifyBrewPotionButtonForWaypointRecipes(__instance, ___rightPageContent));
        }
    }

    [HarmonyPatch(typeof(RecipeBookBrewPotionButton), "BrewPotion")]
    public class ViewWaypointOnMapPatch
    {
        static bool Prefix(RecipeBookRightPageContent ___rightPageContent)
        {
            return Ex.RunSafe(() => UIService.ViewWaypointOnMap(___rightPageContent), () => OnError(___rightPageContent));
        }

        private static bool OnError(RecipeBookRightPageContent rightPageContent)
        {
            try
            {
                if (rightPageContent.currentPotion == null) return true;
                if (!RecipeService.IsWaypointRecipe(rightPageContent.currentPotion)) return true;
                return false;
            }
            catch (Exception ex)
            {
                Ex.LogException(ex);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PotionCraft.Assemblies.GamepadNavigation.Slot), "SaveSettingsFromChild")]
    public class FixButtonInitializationExceptionPatch
    {
        static bool Prefix(PotionCraft.Assemblies.GamepadNavigation.Slot __instance)
        {
            return Ex.RunSafe(() => UIService.FixButtonInitializationException(__instance));
        }
    }

    [HarmonyPatch(typeof(RecipeBookBrewPotionButton), "GetTooltipContent")]
    public class RemoveBrewPotionTooltipForWaypointRecipePatch
    {
        static void Postfix(ref TooltipContent __result, RecipeBookRightPageContent ___rightPageContent)
        {
            try
            {
                if (RecipeService.IsWaypointRecipe(___rightPageContent.currentPotion))
                {
                    __result = null;
                }
            }
            catch(Exception ex)
            {
                Ex.LogException(ex);
            }
        }
    }

    [HarmonyPatch(typeof(SavedState), "ToJson")]
    public class InjectSavedRecipesPatch
    {
        static void Postfix(ref string __result)
        {
            SaveLoadService.StoreIgnoredWaypoints(ref __result);
        }
    }

    [HarmonyPatch(typeof(File), "Load")]
    public class RetrieveStateJsonStringPatch
    {
        static bool Prefix(File __instance)
        {
            return Ex.RunSafe(() => SaveLoadService.RetrieveStateJsonString(__instance));
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), "LoadSelectedState")]
    public class RetreiveSavedAlchemyMachineRecipesFromSavedStatePatch
    {
        static bool Prefix(Type type)
        {
            return Ex.RunSafe(() => SaveLoadService.RetreiveStoredIgnoredWaypoints(type));
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), "LoadFile")]
    public class ClearFileSpecificDataOnFileLoadPatch
    {
        static bool Prefix()
        {
            return Ex.RunSafe(() => SaveLoadService.ClearFileSpecificDataOnFileLoad());
        }
    }
}
