using HarmonyLib;
using PotionCraft.ManagersSystem.SaveLoad;
using PotionCraft.ObjectBased.InteractiveItem;
using PotionCraft.ObjectBased.RecipeMap;
using PotionCraft.ObjectBased.UIElements.Books.RecipeBook;
using PotionCraft.SaveFileSystem;
using PotionCraft.SaveLoadSystem;
using PotionCraft.ScriptableObjects.Potion;
using PotionCraftRecipeWaypoints.Scripts.Services;
using System;
using TooltipSystem;

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
        static bool Prefix(IRecipeBookPageContent recipe)
        {
            return Ex.RunSafe(() => RecipeService.RecipeDeletedFromBook(recipe as Potion));
        }
    }

    [HarmonyPatch(typeof(RecipeBookBrewRecipeButton), "UpdateVisual")]
    public class ModifyBrewPotionButtonForWaypointRecipesPatch
    {
        static void Postfix(RecipeBookBrewRecipeButton __instance, RecipeBookRightPageContent ___rightPageContent)
        {
            Ex.RunSafe(() => UIService.ModifyBrewPotionButtonForWaypointRecipes(__instance, ___rightPageContent));
        }
    }

    [HarmonyPatch(typeof(RecipeBookBrewRecipeButton), "OnButtonReleasedPointerInside")]
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
                var contentPotion = rightPageContent.GetRecipeBookPageContent() as Potion;
                if (contentPotion == null) return true;
                if (!RecipeService.IsWaypointRecipe(contentPotion)) return true;
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
                if (___interactiveItem is not RecipeBookBrewRecipeButton brewButton) return;
                var rightPageContent = AccessTools.Field(brewButton.GetType(), "rightPageContent").GetValue(brewButton) as RecipeBookRightPageContent;
                if (RecipeService.IsWaypointRecipe(rightPageContent.GetRecipeBookPageContent() as Potion))
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

    [HarmonyPatch(typeof(SaveLoadManager), "LoadProgressState")]
    public class RetreiveSavedAlchemyMachineRecipesFromSavedStatePatch
    {
        static bool Prefix()
        {
            return Ex.RunSafe(() => SaveLoadService.RetreiveStoredIgnoredWaypoints());
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
