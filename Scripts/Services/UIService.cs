using PotionCraft.Assemblies.GamepadNavigation;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.RecipeMap;
using PotionCraft.ManagersSystem.Room;
using PotionCraft.ManagersSystem.TMP;
using PotionCraft.ObjectBased.InteractiveItem;
using PotionCraft.ObjectBased.RecipeMap;
using PotionCraft.ObjectBased.RecipeMap.Buttons;
using PotionCraft.ObjectBased.RecipeMap.Path;
using PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.PathMapItem;
using PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.Teleportation;
using PotionCraft.ObjectBased.UIElements;
using PotionCraft.ObjectBased.UIElements.Books;
using PotionCraft.ObjectBased.UIElements.Books.RecipeBook;
using PotionCraft.ObjectBased.UIElements.FinishLegendarySubstanceMenu;
using PotionCraft.ObjectBased.UIElements.Tooltip;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.AlchemyMachineProducts;
using PotionCraft.Settings;
using PotionCraft.TMPAtlasGenerationSystem;
using PotionCraft.Utils.SortingOrderSetter;
using PotionCraftRecipeWaypoints.Scripts.Storage;
using PotionCraftRecipeWaypoints.Scripts.UIComponents;
using RecipeMapItem.RecipeMapItemsUpdater;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using static PotionCraft.SaveLoadSystem.ProgressState;
using static PotionCraft.ScriptableObjects.Potion;

namespace PotionCraftRecipeWaypoints.Scripts.Services
{
    /// <summary>
    /// Responsible for any UI modifications
    /// </summary>
    public static class UIService
    {
        public static void AddWaypointsToMap(List<RecipeIndex> waypointRecipes)
        {
            Plugin.PluginLogger.LogInfo($"AddWaypointsToMap - start - {waypointRecipes.Count}");
            StaticStorage.Waypoints = new List<WaypointMapItem>();
            waypointRecipes.ForEach(recipe =>
            {
                AddWaypointToMap(recipe);
            });
        }

        public static void AddWaypointToMap(RecipeIndex recipe)
        {
            var pos = RecipeService.GetMapPositionForRecipe(recipe);
            if (StaticStorage.Waypoints.Any(w => Vector2.Distance(w.transform.localPosition, pos) < 1))
            {
                Plugin.PluginLogger.LogInfo($"Waypoint not added to map due to proximity to existing waypoint: {recipe.Recipe.GetLocalizedTitle()}");
                return;
            }

            var gameObject = new GameObject($"waypoint ({StaticStorage.Waypoints.Count})");
            gameObject.layer = LayerMask.NameToLayer("RecipeMapContent");
            var waypointMapItem = gameObject.AddComponent<WaypointMapItem>();
            typeof(PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.RecipeMapItem)
                .GetField("canBeInteracted", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(waypointMapItem, true);
            RecipeMapItemsUpdater.generalCollection.RegisterItem(waypointMapItem);
            waypointMapItem.IconRenderer = waypointMapItem.gameObject.AddComponent<SpriteRenderer>();
            waypointMapItem.sortingGroup = gameObject.AddComponent<SortingGroup>();
            waypointMapItem.sortingGroup.sortingLayerName
                = waypointMapItem.IconRenderer.sortingLayerName = "RecipeMapContent";
            waypointMapItem.sortingGroup.sortingLayerID
                = waypointMapItem.IconRenderer.sortingLayerID = SortingLayer.NameToID(waypointMapItem.IconRenderer.sortingLayerName);
            waypointMapItem.sortingGroup.sortingOrder
                = waypointMapItem.IconRenderer.sortingOrder = 29502; //This is one higher than the experience books

            waypointMapItem.IconRenderer.sprite = GetWaypointSprite();
            waypointMapItem.IconRenderer.color = GetWaypointMapItemColor();
            var collider = gameObject.AddComponent<CircleCollider2D>();
            waypointMapItem.circleCollider = collider;
            waypointMapItem.circleCollider.radius = 0.5f;

            waypointMapItem.transform.parent = Managers.RecipeMap.currentMap.transform;
            waypointMapItem.transform.localPosition = pos;
            Plugin.PluginLogger.LogInfo($"Added waypoint: {recipe.Recipe.GetLocalizedTitle()} at {waypointMapItem.transform.localPosition}");

            waypointMapItem.Recipe = recipe;
            StaticStorage.Waypoints.Add(waypointMapItem);
        }

        public static bool ViewWaypointOnMap(RecipeBookRightPageContent rightPageContent)
        {
            var recipe = rightPageContent.currentPotion;
            if (!RecipeService.IsWaypointRecipe(recipe)) return true;
            MapLoader.SelectMapIfNotSelected(recipe.potionBase);
            Managers.RecipeMap.CenterMapOn(recipe.potionFromPanel.serializedPath.indicatorTargetPosition, true, 1.0f);
            Managers.Room.GoTo(RoomManager.RoomIndex.Laboratory, true);
            return false;
        }

        public static void CreateWaypointHoverPath(WaypointMapItem waypointMapItem)
        {
            waypointMapItem.path = new GameObject("WaypointPath");
            waypointMapItem.path.transform.parent = waypointMapItem.transform;
            var pathSettings = Settings<RecipeMapManagerPathSettings>.Asset;
            var serializedPath = waypointMapItem.Recipe.Recipe.potionFromPanel.serializedPath;
            var fixedPathPoints = serializedPath.fixedPathPoints;
            //Teleportation hints have a lot of bad side effects and aren't probably going to be encountered anyways. For now they can just be unsupported.
            if (fixedPathPoints.Any(p => p.isTeleportationHint)) return;
            var fixedPathHints = new List<FixedHint>();
            fixedPathPoints.Select(f => f.Clone()).ToList().ForEach(points =>
            {
                var component = UnityEngine.Object.Instantiate(pathSettings.nonTeleportationFixedHint, waypointMapItem.path.transform).GetComponent<FixedHint>();

                component.evenlySpacedPointsFixedPhysics = new EvenlySpacedPoints(points.physicsPoints);
                component.evenlySpacedPointsFixedGraphics = new EvenlySpacedPoints(points.graphicsPoints);
                component.SetPathStartParameters(points.pathStartParameters);
                component.SetPathEndParameters(points.pathEndParameters);
                var oldActualPathFixedPathHints = Managers.RecipeMap.path.fixedPathHints;
                var oldDeletedGraphicsSegments = Managers.RecipeMap.path.deletedGraphicsSegments; //TODO there are a bunch of these fields. Test with void salt to make sure we don't need the others
                component.UpdateState(PathMapItem.State.Showing, 0f);
                component.ShowPathEnds(true, 0f);

                Managers.RecipeMap.path.fixedPathHints = fixedPathHints;
                Managers.RecipeMap.path.deletedGraphicsSegments = serializedPath.deletedGraphicsSegments;
                component.MakePathVisible();
                Managers.RecipeMap.path.fixedPathHints = oldActualPathFixedPathHints;
                Managers.RecipeMap.path.deletedGraphicsSegments = oldDeletedGraphicsSegments;

                fixedPathHints.Add(component);
            });
            var oldActualPathFixedPathHints = Managers.RecipeMap.path.fixedPathHints;
            var oldDeletedGraphicsSegments = Managers.RecipeMap.path.deletedGraphicsSegments;

            Managers.RecipeMap.path.fixedPathHints = fixedPathHints;
            Managers.RecipeMap.path.deletedGraphicsSegments = serializedPath.deletedGraphicsSegments;
            var updatePathAlphaMethod = typeof(NonTeleportationFixedHint).GetMethod("UpdatePathAlpha", BindingFlags.NonPublic | BindingFlags.Instance);
            var updatePathAlphaEndMethod = typeof(NonTeleportationFixedHint).GetMethod("UpdatePathEndAlpha", BindingFlags.NonPublic | BindingFlags.Instance);
            fixedPathHints.ForEach(f =>
            {
                f.MakePathVisible();
                updatePathAlphaMethod.Invoke(f, new object[] { WaypointMapItem.WaypointAlpha });
                updatePathAlphaEndMethod.Invoke(f, new object[] { WaypointMapItem.WaypointAlpha });
            });
            Managers.RecipeMap.path.fixedPathHints = oldActualPathFixedPathHints;
            Managers.RecipeMap.path.deletedGraphicsSegments = oldDeletedGraphicsSegments;

            waypointMapItem.path.transform.localPosition = serializedPath.pathPosition;
        }

        public static void UpdateCurrentRecipePage()
        {
            Managers.Potion.recipeBook.GetComponentsInChildren<RecipeBookRightPageContent>().ToList().ForEach(rightPageContent =>
            {
                rightPageContent.UpdatePage(rightPageContent.currentState, rightPageContent.currentPotion);
            });
        }

        public static bool FixButtonInitializationException(Slot instance)
        {
            if (instance.cursorAnchorSubObject == null)
            {
                instance.ShowSectionEntity();
            }
            instance.cursorAnchorLocalPos = instance.cursorAnchorSubObject.transform.localPosition;
            return false;
        }

        public static void DeleteWaypoint(WaypointMapItem matchingWaypoint)
        {
            StaticStorage.Waypoints.Remove(matchingWaypoint);
            UnityEngine.Object.Destroy(matchingWaypoint.gameObject);
        }

        public static PotionBase GetCurrentPotionBase()
        {
            return Managers.RecipeMap?.currentMap?.potionBase;
        }

        public static void ModifyBrewPotionButtonForWaypointRecipes(RecipeBookBrewPotionButton instance, RecipeBookRightPageContent rightPageContent)
        {
            const float recipeIconScaleFactor = 0.65f;

            CreateRecipeBookWaypointToggleButton(rightPageContent);
            var isWaypointRecipe = RecipeService.IsWaypointRecipe(rightPageContent.currentPotion);
            if (isWaypointRecipe)
            {
                if (!StaticStorage.WaypointBrewPotionButton.ContainsKey(rightPageContent))
                {
                    var waypointInstance = UnityEngine.Object.Instantiate(instance, instance.transform.parent);
                    StaticStorage.WaypointBrewPotionButton[rightPageContent] = waypointInstance;
                    typeof(RecipeBookBrewPotionButton).GetField("rightPageContent", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(waypointInstance, rightPageContent);
                    var countText = typeof(RecipeBookBrewPotionButton).GetField("potionsCountText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(waypointInstance) as TextMeshPro;
                    var buttonText = waypointInstance.texts.Where(t => t != countText).First();
                    countText.GetComponent<MeshRenderer>().enabled = false;
                    buttonText.text = "View waypoint on map";

                    waypointInstance.hoveredSpriteIcon
                        = waypointInstance.normalSpriteIcon
                            = waypointInstance.pressedSpriteIcon
                                = waypointInstance.spriteRendererIcon.sprite = GetWaypointSprite();
                    waypointInstance.spriteRendererIcon.color = GetWaypointSpriteColor();
                    waypointInstance.spriteRendererIcon.transform.localScale *= recipeIconScaleFactor;
                    waypointInstance.GetTooltipContent();
                }
                StaticStorage.WaypointBrewPotionButton[rightPageContent].gameObject.SetActive(true);
                instance.gameObject.SetActive(false);
                var canPress = rightPageContent.currentPotion?.potionBase?.name == GetCurrentPotionBase().name || !Managers.Potion.potionCraftPanel.IsPotionBrewingStarted();
                StaticStorage.WaypointBrewPotionButton[rightPageContent].Locked = !canPress;
            }
            else
            {
                if (StaticStorage.WaypointBrewPotionButton.ContainsKey(rightPageContent))
                    StaticStorage.WaypointBrewPotionButton[rightPageContent].gameObject.SetActive(false);
                instance.gameObject.SetActive(true);
            }
        }

        public static void CreateWaypointToggleButton(FollowIndicatorButton instance)
        {
            var collider = instance.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(3, collider.size.y);
            var waypointButton = GetWaypointToggleButton(instance.transform.parent);
            waypointButton.followButton = instance;
            var gameObject = waypointButton.gameObject;
            gameObject.transform.localPosition = new Vector3(instance.transform.localPosition.x + 1.8f, instance.transform.localPosition.y, 0);
            var instanceSprite = instance.GetComponentsInChildren<SpriteRenderer>().First();
            waypointButton.sortingGroup = gameObject.AddComponent<SortingGroup>();
            waypointButton.sortingGroup.sortingLayerName =
                waypointButton.iconRenderer.sortingLayerName = instanceSprite.sortingLayerName;
            waypointButton.sortingGroup.sortingLayerID =
                waypointButton.iconRenderer.sortingLayerID = instanceSprite.sortingLayerID;
            waypointButton.sortingGroup.sortingOrder =
                waypointButton.iconRenderer.sortingOrder = instanceSprite.sortingOrder + 1;
            var tooltipPosition = new List<PositioningSettings>
            {
                new PositioningSettings
                {
                    bindingPoint = PositioningSettings.BindingPoint.TransformPosition,
                    tooltipCorner = PositioningSettings.TooltipCorner.RightBottom,
                    position = new Vector2(4.5f, -0.4f)
                }
            };
            AddTooltipProvider(waypointButton, gameObject, tooltipPosition);
            StaticStorage.WaypointToggleButton = waypointButton;
            ShowHideWaypoints(false);
        }

        public static void CreateRecipeBookWaypointToggleButton(RecipeBookRightPageContent instance)
        {
            if (!StaticStorage.WaypointToggleButtonRecipeBook.ContainsKey(instance))
            {
                var waypointButton = GetWaypointToggleButton(instance.transform);
                var gameObject = waypointButton.gameObject;
                var brewButton = typeof(RecipeBookRightPageContent).GetField("brewPotionButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance) as RecipeBookBrewPotionButton;

                gameObject.transform.localPosition = new Vector3(2.7f, 4.55f, 0);
                //Setup the raycast priority level so this button is prioritized along with other buttons on this page
                waypointButton.raycastPriorityLevel = brewButton.raycastPriorityLevel;
                var instanceSprite = brewButton.spriteRenderer;
                waypointButton.sortingGroup = gameObject.AddComponent<SortingGroup>();
                waypointButton.sortingGroup.sortingLayerName =
                    waypointButton.iconRenderer.sortingLayerName = instanceSprite.sortingLayerName;
                waypointButton.sortingGroup.sortingLayerID =
                    waypointButton.iconRenderer.sortingLayerID = instanceSprite.sortingLayerID;
                waypointButton.sortingGroup.sortingOrder =
                    waypointButton.iconRenderer.sortingOrder = instanceSprite.sortingOrder + 1;
                var tooltipPosition = new List<PositioningSettings>
                {
                    new PositioningSettings
                    {
                        bindingPoint = PositioningSettings.BindingPoint.TransformPosition,
                        tooltipCorner = PositioningSettings.TooltipCorner.LeftBottom,
                        position = new Vector2(0, -1.5f)
                    }
                };
                AddTooltipProvider(waypointButton, gameObject, tooltipPosition);
                StaticStorage.WaypointToggleButtonRecipeBook[instance] = waypointButton;
            }
            var button = StaticStorage.WaypointToggleButtonRecipeBook[instance];
            var isWaypointRecipeIgnoreIgnored = RecipeService.IsWaypointRecipe(instance.currentPotion, true);
            button.gameObject.SetActive(isWaypointRecipeIgnoreIgnored);
            if (!isWaypointRecipeIgnoreIgnored) return;
            var color = GetWaypointSpriteColor();
            button.iconRenderer.color = StaticStorage.IgnoredWaypoints.Contains(RecipeService.GetRecipeIndexObject(instance.currentPotion).Index)
                                            ? new Color(color.r, color.g, color.b, button.OffAlpha)
                                            : color;
        }

        public static void ShowHideWaypoints()
        {
            ShowHideWaypoints(!StaticStorage.WaypointsVisible);
        }

        public static Vector2 GetEffectMapLocation(PotionEffect potionEffect, PotionBase potionBase)
        {
            var map = MapLoader.loadedMaps.FirstOrDefault(m => m.potionBase.name == potionBase.name);
            if (map == null)
            {
                Plugin.PluginLogger.LogError($"Error: failed to find map for potion base {potionBase.name}");
                return Vector2.zero;
            }
            var mapEffect = map.potionEffectsOnMap.FirstOrDefault(e => e.effect.name == potionEffect.name);
            return mapEffect.thisTransform.localPosition;
        }

        public static float GetIndicatorDiameter()
        {
            return Managers.RecipeMap.indicator.circleCollider.radius * 2;
        }

        private static Sprite GetWaypointSprite()
        {
            if (StaticStorage.RecipeWaypointSprite == null)
            {
                StaticStorage.RecipeWaypointSprite = Settings<RecipeMapManagerTeleportationSettings>.Asset.spiralSprites.First().main;
            }
            return StaticStorage.RecipeWaypointSprite;
        }

        private static Color GetWaypointSpriteColor()
        {
            return Settings<RecipeMapManagerTeleportationSettings>.Asset.colorFixed;
        }

        private static Color GetWaypointMapItemColor()
        {
            var color = GetWaypointSpriteColor();
            return new Color(color.r, color.g, color.b, WaypointMapItem.WaypointAlpha);
        }

        private static Color GetWaypointToggleButtonColor()
        {
            var color = GetWaypointSpriteColor();
            if (StaticStorage.WaypointsVisible) return color;
            return new Color(color.r, color.g, color.b, StaticStorage.WaypointToggleButton.OffAlpha);
        }

        private static WaypointToggleButton GetWaypointToggleButton(Transform parent)
        {
            var gameObject = new GameObject("WaypointToggleButton");
            gameObject.layer = LayerMask.NameToLayer("UI");
            var waypointButton = gameObject.AddComponent<WaypointToggleButton>();
            waypointButton.enabled = true;
            waypointButton.spriteSlot = new GameObject("SpriteSlot");
            waypointButton.spriteSlot.transform.parent = gameObject.transform;
            //For some reason this is 2 by default (maybe with how I am setting scale above?
            waypointButton.spriteSlot.transform.localScale *= 0.5f;
            var spriteRenderer = waypointButton.spriteSlot.AddComponent<SpriteRenderer>();
            waypointButton.iconRenderer = spriteRenderer;
            var waypointCollider = gameObject.AddComponent<BoxCollider2D>();
            waypointButton.collider = waypointCollider;
            waypointCollider.enabled = true;
            waypointCollider.isTrigger = true;
            waypointCollider.size = new Vector2(0.7f, waypointCollider.size.y);
            gameObject.transform.parent = parent;
            spriteRenderer.sprite = GetWaypointSprite();
            spriteRenderer.color = GetWaypointSpriteColor();
            return waypointButton;
        }

        private static void AddTooltipProvider(InteractiveItem obj, GameObject gameObject, List<PositioningSettings> position)
        {
            var tooltipProvider = gameObject.AddComponent<TooltipContentProvider>();
            tooltipProvider.positioningSettings = position;
            typeof(InteractiveItem).GetField("tooltipContentProvider", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, tooltipProvider);
        }

        public static void ShowHideWaypoints(bool show)
        {
            if (StaticStorage.Waypoints == null) return;
            StaticStorage.Waypoints.ForEach(w => w.gameObject.SetActive(show));
            StaticStorage.WaypointsVisible = show;
            UpdateWaypointToggleButtonSprite();
        }

        private static void UpdateWaypointToggleButtonSprite()
        {
            if (StaticStorage.WaypointToggleButton == null) return;
            StaticStorage.WaypointToggleButton.iconRenderer.color = GetWaypointToggleButtonColor();
        }
    }
}
