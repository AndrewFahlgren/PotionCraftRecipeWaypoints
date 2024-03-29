﻿using HarmonyLib;
using PotionCraft.ManagersSystem.SaveLoad;
using PotionCraft.ObjectBased.InteractiveItem;
using PotionCraft.ObjectBased.RecipeMap;
using PotionCraft.ObjectBased.UIElements.Books.RecipeBook;
using PotionCraft.ObjectBased.UIElements.Tooltip;
using PotionCraft.SaveFileSystem;
using PotionCraft.SaveLoadSystem;
using PotionCraft.ScriptableObjects.Potion;
using PotionCraftRecipeWaypoints.Scripts.Services;
using System;

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
        static bool Prefix(Potion potion)
        {
            return Ex.RunSafe(() => RecipeService.RecipeDeletedFromBook(potion));
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
                if (rightPageContent.pageContentPotion == null) return true;
                if (!RecipeService.IsWaypointRecipe(rightPageContent.pageContentPotion)) return true;
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

    [HarmonyPatch(typeof(TooltipContentProvider), "GetTooltipContent")]
    public class RemoveBrewPotionTooltipForWaypointRecipePatch
    {
        static void Postfix(ref TooltipContent __result, InteractiveItem ___interactiveItem)
        {
            try
            {
                if (___interactiveItem is not RecipeBookBrewPotionButton brewButton) return;
                var rightPageContent = AccessTools.Field(brewButton.GetType(), "rightPageContent").GetValue(brewButton) as RecipeBookRightPageContent;
                if (RecipeService.IsWaypointRecipe(rightPageContent.pageContentPotion))
                {
                    __result = null;
                }
            }
            catch (Exception ex)
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
