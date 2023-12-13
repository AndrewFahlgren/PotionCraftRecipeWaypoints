using PotionCraft.Assemblies.GamepadNavigation;
using PotionCraft.Core.Extensions;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.RecipeMap;
using PotionCraft.ManagersSystem.Room;
using PotionCraft.ManagersSystem.TMP;
using PotionCraft.ObjectBased.InteractiveItem;
using PotionCraft.ObjectBased.RecipeMap;
using PotionCraft.ObjectBased.RecipeMap.Buttons;
using PotionCraft.ObjectBased.RecipeMap.Path;
using PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.IndicatorMapItem;
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
using UnityEngine.SceneManagement;

namespace PotionCraftRecipeWaypoints.Scripts.Services
{
    /// <summary>
    /// Responsible for any UI modifications
    /// </summary>
    public static class UIService
    {
        private const float WaypointProximityExclusionZone = 1f;

        /// <summary>
        /// Adds waypoints for the specified waypoint recipes to the map
        /// </summary>
        public static void AddWaypointsToMap(List<RecipeIndex> waypointRecipes)
        {
            StaticStorage.Waypoints = new List<WaypointMapItem>();
            waypointRecipes.ForEach(recipe =>
            {
                AddWaypointToMap(recipe);
            });
        }

        /// <summary>
        /// Adds in waypoints which may not have been added due to proximity with this waypoint.
        /// </summary>
        /// <param name="deletedWaypoint">The waypoint which is being deleted.</param>
        public static void AddMissingWaypointsAroundDeletedWaypoint(WaypointMapItem deletedWaypoint)
        {
            if (deletedWaypoint.Recipe.Recipe.potionBase != GetCurrentPotionBase()) return;
            var pos = deletedWaypoint.transform.position;
            var allNearbyWaypointRecipes = RecipeService.GetWaypointRecipes(deletedWaypoint.Recipe.Recipe.potionBase)
                                                        .Where(r => Vector2.Distance(pos, RecipeService.GetMapPositionForRecipe(r)) <= WaypointProximityExclusionZone);
            var waypointsToAdd = allNearbyWaypointRecipes.Where(r => StaticStorage.Waypoints.Any(w => w.Recipe.Index == r.Index)).ToList();
            waypointsToAdd.ForEach(recipe =>
            {
                AddWaypointToMap(recipe);
            });
        }

        /// <summary>
        /// Adds a waypoint to the map for the specified recipe
        /// </summary>
        public static WaypointMapItem AddWaypointToMap(RecipeIndex recipe, bool addToWaypointList = true)
        {
            var pos = RecipeService.GetMapPositionForRecipe(recipe);
            if (StaticStorage.Waypoints.Any(w => Vector2.Distance(w.transform.localPosition, pos) < WaypointProximityExclusionZone))
            {
                Plugin.PluginLogger.LogInfo($"Waypoint not added to map due to proximity to existing waypoint: {recipe.Recipe.GetLocalizedTitle()}");
                return null;
            }

            var gameObject = new GameObject($"waypoint ({StaticStorage.Waypoints.Count})");
            gameObject.layer = LayerMask.NameToLayer("RecipeMapContent");
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetSceneByName(recipe.Recipe.potionBase.mapSceneName));
            gameObject.transform.parent = GameObject.Find("MapItemsContainer").transform;
            var waypointMapItem = gameObject.AddComponent<WaypointMapItem>();
            waypointMapItem.IsTailEndWaypoint = RecipeService.GetMapPositionForRecipe(recipe.Recipe, true) != pos;
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

            //waypointMapItem.transform.parent = Managers.RecipeMap.currentMap.transform;
            waypointMapItem.transform.localPosition = pos;

            waypointMapItem.Recipe = recipe;
            if (addToWaypointList) StaticStorage.Waypoints.Add(waypointMapItem);
            if (!StaticStorage.WaypointsVisible) gameObject.SetActive(false);

            return waypointMapItem;
        }

        /// <summary>
        /// Prefix method for ViewWaypointsOnMapPatch
        /// Switches the current room to the lab and zooms in on the waypoint for the currently selected waypoint recipe
        /// </summary>
        public static bool ViewWaypointOnMap(RecipeBookRightPageContent rightPageContent)
        {
            var recipe = rightPageContent.pageContentPotion;
            if (!RecipeService.IsWaypointRecipe(recipe)) return true;
            MapStatesManager.SelectMapIfNotSelected(recipe.potionBase);
            var pos = RecipeService.GetMapPositionForRecipe(recipe);
            Managers.RecipeMap.CenterMapOn(pos, true, 1.0f);
            Managers.Room.GoTo(RoomManager.RoomIndex.Laboratory, true);
            if (!StaticStorage.WaypointsVisible) ShowHideWaypoints(true);
            return false;
        }

        /// <summary>
        /// Creates a path visual similar to the visual used when continuing brewing for this waypoint and assigns it to the specified waypoint's path property
        /// </summary>
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
            var isFirst = true;
            fixedPathPoints.Select(f => f.Clone()).ToList().ForEach(points =>
            {
                var component = UnityEngine.Object.Instantiate(pathSettings.nonTeleportationFixedHint, waypointMapItem.path.transform).GetComponent<FixedHint>();

                component.evenlySpacedPointsFixedPhysics = new EvenlySpacedPoints(points.physicsPoints);
                component.evenlySpacedPointsFixedGraphics = new EvenlySpacedPoints(points.graphicsPoints);
                component.SetPathStartParameters(points.pathStartParameters);
                component.SetPathEndParameters(points.pathEndParameters);
                var oldActualPathFixedPathHints = Managers.RecipeMap.path.fixedPathHints;
                var oldDeletedGraphicsSegments = Managers.RecipeMap.path.deletedGraphicsSegments;
                component.UpdateState(PathMapItem.State.Showing, 0f);
                component.ShowPathEnds(true, 0f);

                var deletedSegments = serializedPath.deletedGraphicsSegments;
                if (isFirst)
                {
                    isFirst = false;
                    deletedSegments += 1;
                }

                Managers.RecipeMap.path.fixedPathHints = fixedPathHints;
                Managers.RecipeMap.path.deletedGraphicsSegments = deletedSegments;
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
            for (var i = 0; i < fixedPathHints.Count; i++)
            {
                var f = fixedPathHints[i];
                f.MakePathVisible();
                updatePathAlphaMethod.Invoke(f, new object[] { WaypointMapItem.WaypointAlpha });
                //Only show the path end for the last fixed path
                updatePathAlphaEndMethod.Invoke(f, new object[] { i == fixedPathHints.Count - 1 ? WaypointMapItem.WaypointAlpha : 0 });
            }
            Managers.RecipeMap.path.fixedPathHints = oldActualPathFixedPathHints;
            Managers.RecipeMap.path.deletedGraphicsSegments = oldDeletedGraphicsSegments;

            var indicatorPosition = RecipeService.GetMapPositionForRecipe(waypointMapItem.Recipe.Recipe, true);
            var waypointPosition = RecipeService.GetMapPositionForRecipe(waypointMapItem.Recipe.Recipe);
            var positionOffset = waypointMapItem.IsTailEndWaypoint
                                    ? indicatorPosition - waypointPosition
                                    : Vector2.zero;
            waypointMapItem.path.transform.localPosition = serializedPath.pathPosition + positionOffset;
            
            //Make some adjustments to the path for tail end waypoints
            if (waypointMapItem.IsTailEndWaypoint)
            {
                //Add a ghost indicator to the beginning of the path
                var indicatorObject = Managers.RecipeMap.indicator.gameObject.transform.Find("Bottle");
                var ghostIndicatorGameObject = UnityEngine.Object.Instantiate(indicatorObject, waypointMapItem.path.transform);
                var indicatorOffset = serializedPath.indicatorTargetPosition + serializedPath.pathPosition;
                ghostIndicatorGameObject.transform.localPosition = indicatorPosition - indicatorOffset;

                //Set the rotation
                ghostIndicatorGameObject.transform.eulerAngles = serializedPath.indicatorRotationValue.RecalculateEulerAngle(FloatExtension.AngleType.ZeroTo2Pi) * Vector3.forward;

                var waypointMapItemColor = GetWaypointMapItemColor();
                waypointMapItemColor = new Color(waypointMapItemColor.r, waypointMapItemColor.g, waypointMapItemColor.b, WaypointMapItem.WaypointAlpha / 2);

                var background = ghostIndicatorGameObject.transform.Find("Background").GetComponent<SpriteRenderer>();
                var spritesToUpdate = new List<SpriteRenderer>
                {
                    background,
                    ghostIndicatorGameObject.transform.Find("Liquid Sprites Container/Sprite Liquid Main").GetComponent<SpriteRenderer>(),
                    ghostIndicatorGameObject.transform.Find("Liquid Sprites Container(Clone)/Sprite Liquid Main").GetComponent<SpriteRenderer>(),
                    ghostIndicatorGameObject.transform.Find("Foreground").GetComponent<SpriteRenderer>(),
                    ghostIndicatorGameObject.transform.Find("Contour").GetComponent<SpriteRenderer>()
                };

                //Update the sprites to be the semi transparent color we use for waypoints with some additional alpha
                //Also update all the sorting groups for the ghost indicator to ensure it sorts above the path
                var fixedPathSprite = fixedPathHints.First().GetComponentInChildren<LineRenderer>();
                spritesToUpdate.ForEach(sprite =>
                {
                    if (sprite != background) sprite.color = waypointMapItemColor;
                    sprite.sortingLayerName = fixedPathSprite.sortingLayerName;
                    sprite.sortingOrder = fixedPathSprite.sortingOrder + sprite.sortingOrder;
                });

                var sortingGroupsToUpdate = new List<SortingGroup>
                {
                    ghostIndicatorGameObject.transform.Find("Liquid Sprites Container").GetComponent<SortingGroup>(),
                    ghostIndicatorGameObject.transform.Find("Liquid Sprites Container(Clone)").GetComponent<SortingGroup>()
                };

                sortingGroupsToUpdate.ForEach(group =>
                {
                    group.sortingLayerName = fixedPathSprite.sortingLayerName;
                    group.sortingOrder = fixedPathSprite.sortingOrder + group.sortingOrder;
                });


                //Hide the cork
                ghostIndicatorGameObject.transform.Find("Cork").gameObject.SetActive(false);
                //Hide the scratches
                ghostIndicatorGameObject.transform.Find("Scratches").gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Updates all content on both right page instances in the recipe book
        /// This will trigger our patch which refreshes the states of all our custom buttons
        /// </summary>
        public static void UpdateCurrentRecipePage()
        {
            Managers.Potion.recipeBook.GetComponentsInChildren<RecipeBookRightPageContent>().ToList().ForEach(rightPageContent =>
            {
                rightPageContent.UpdatePage(rightPageContent.currentState, rightPageContent.pageContentPotion);
            });
        }

        /// <summary>
        /// Prefix method for FixButtonInitializationExceptionPatch
        /// Due to the order in which things are initialized for our custom version of the brew button a nessesary field is not loaded in time
        /// This method loads that field in the case it is null to prevent an exception in this case
        /// </summary>
        public static bool FixButtonInitializationException(Slot instance)
        {
            if (instance.cursorAnchorSubObject == null)
            {
                instance.ShowSectionEntity();
            }
            instance.cursorAnchorLocalPos = instance.cursorAnchorSubObject.transform.localPosition;
            return false;
        }

        /// <summary>
        /// Removes the specified WaypointMapItem from the map and from the Waypoints list
        /// </summary>
        public static void DeleteWaypoint(WaypointMapItem matchingWaypoint)
        {
            StaticStorage.Waypoints.Remove(matchingWaypoint);
            UnityEngine.Object.Destroy(matchingWaypoint.gameObject);
        }

        /// <summary>
        /// Returns the potion base for the currently loaded map
        /// </summary>
        public static PotionBase GetCurrentPotionBase()
        {
            return Managers.RecipeMap?.currentMap?.potionBase;
        }

        /// <summary>
        /// Postfix method for ModifyBrewPotionButtonForWaypointRecipesPatch
        /// This method creates both the custom brew potion button and the recipe waypoint toggle button for this right page instance
        /// </summary>
        public static void ModifyBrewPotionButtonForWaypointRecipes(RecipeBookBrewPotionButton instance, RecipeBookRightPageContent rightPageContent)
        {
            if (StaticStorage.TemporaryWaypoint != null)
            {
                UnityEngine.Object.Destroy(StaticStorage.TemporaryWaypoint.gameObject);
            }
            StaticStorage.TemporaryWaypoint = null;


            const float recipeIconScaleFactor = 0.65f;

            CreateRecipeBookWaypointToggleButton(rightPageContent);
            var isWaypointRecipe = RecipeService.IsWaypointRecipe(rightPageContent.pageContentPotion);
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
                var canPress = rightPageContent.pageContentPotion?.potionBase?.name == GetCurrentPotionBase().name || !Managers.Potion.potionCraftPanel.IsPotionBrewingStarted();
                StaticStorage.WaypointBrewPotionButton[rightPageContent].Locked = !canPress;

                //If BrewFromhere is installed we should create temporary waypoints when a new recipe is generated
                if (StaticStorage.BrewFromHereInstalled)
                {
                    var currentIndex = Managers.Potion.recipeBook.currentPageIndex;
                    StaticStorage.TemporaryWaypoint = AddWaypointToMap(new RecipeIndex { Index = currentIndex, Recipe = rightPageContent.pageContentPotion }, false);
                }
            }
            else
            {
                if (StaticStorage.WaypointBrewPotionButton.ContainsKey(rightPageContent))
                    StaticStorage.WaypointBrewPotionButton[rightPageContent].gameObject.SetActive(false);
                instance.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Creates the main map waypoint toggle button
        /// </summary>
        public static void CreateWaypointToggleButton(FollowIndicatorButton instance)
        {
            if (StaticStorage.WaypointToggleButton != null)
            {
                ShowHideWaypoints(false);
                return;
            }
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

        /// <summary>
        /// Creates the waypoint toggle button for this recipe right page instance
        /// </summary>
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
            var isWaypointRecipeIgnoreIgnored = RecipeService.IsWaypointRecipe(instance.pageContentPotion, true);
            button.gameObject.SetActive(isWaypointRecipeIgnoreIgnored);
            if (!isWaypointRecipeIgnoreIgnored) return;
            var color = GetWaypointSpriteColor();
            button.iconRenderer.color = StaticStorage.IgnoredWaypoints.Contains(RecipeService.GetRecipeIndexObject(instance.pageContentPotion).Index)
                                            ? new Color(color.r, color.g, color.b, button.OffAlpha)
                                            : color;
        }

        /// <summary>
        /// Toggles the visibility of all waypoints on the map
        /// </summary>
        public static void ShowHideWaypoints()
        {
            ShowHideWaypoints(!StaticStorage.WaypointsVisible);
        }

        /// <summary>
        /// Returns the map coordinates of the specified potion effect for the specified potion base
        /// </summary>
        public static Vector2 GetEffectMapLocation(PotionEffect potionEffect, PotionBase potionBase)
        {
            var map = MapStatesManager.MapStates.FirstOrDefault(m => m.potionBase.name == potionBase.name);
            if (map == null)
            {
                Plugin.PluginLogger.LogError($"Error: failed to find map for potion base {potionBase.name}");
                return Vector2.zero;
            }
            var mapEffect = map.potionEffectsOnMap.FirstOrDefault(e => e.Effect.name == potionEffect.name);
            return mapEffect.thisTransform.localPosition;
        }

        /// <summary>
        /// Returns the diameter of the potion indicator
        /// </summary>
        public static float GetIndicatorDiameter()
        {
            return Managers.RecipeMap.indicator.circleCollider.radius * 2;
        }

        /// <summary>
        /// Gets the sprite used for all recipe waypoint related things
        /// </summary>
        private static Sprite GetWaypointSprite()
        {
            if (StaticStorage.RecipeWaypointSprite == null)
            {
                StaticStorage.RecipeWaypointSprite = Settings<RecipeMapManagerTeleportationSettings>.Asset.spiralSprites.First().main;
            }
            return StaticStorage.RecipeWaypointSprite;
        }

        /// <summary>
        /// Get the base color used for the waypoint sprite
        /// </summary>
        private static Color GetWaypointSpriteColor()
        {
            return Settings<RecipeMapManagerTeleportationSettings>.Asset.colorFixed;
        }

        /// <summary>
        /// Gets the specific color with alpha for the waypoint map item
        /// </summary>
        private static Color GetWaypointMapItemColor()
        {
            var color = GetWaypointSpriteColor();
            return new Color(color.r, color.g, color.b, WaypointMapItem.WaypointAlpha);
        }

        /// <summary>
        /// Gets the current color for the main map waypoint toggle button depending on its current state
        /// </summary>
        private static Color GetWaypointToggleButtonColor()
        {
            var color = GetWaypointSpriteColor();
            if (StaticStorage.WaypointsVisible) return color;
            return new Color(color.r, color.g, color.b, StaticStorage.WaypointToggleButton.OffAlpha);
        }

        /// <summary>
        /// Gets a generic waypoint toggle button
        /// </summary>
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

        /// <summary>
        /// Creates and adds a tooltip provider using the specified settings to the specified InteractiveItem
        /// </summary>
        private static void AddTooltipProvider(InteractiveItem obj, GameObject gameObject, List<PositioningSettings> position)
        {
            var tooltipProvider = gameObject.AddComponent<TooltipContentProvider>();
            tooltipProvider.positioningSettings = position;
            typeof(InteractiveItem).GetField("tooltipContentProvider", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, tooltipProvider);
        }

        /// <summary>
        /// Sets the visibility of all waypoints on the map to the specified value
        /// </summary>
        public static void ShowHideWaypoints(bool show)
        {
            if (StaticStorage.Waypoints == null) return;
            StaticStorage.Waypoints.ForEach(w => w.gameObject.SetActive(show));
            StaticStorage.WaypointsVisible = show;
            UpdateWaypointToggleButtonSprite();

            if (StaticStorage.TemporaryWaypoint != null) StaticStorage.TemporaryWaypoint.gameObject.SetActive(show);
        }

        /// <summary>
        /// Updates the color for the waypoint toggle button depending on its state
        /// </summary>
        private static void UpdateWaypointToggleButtonSprite()
        {
            if (StaticStorage.WaypointToggleButton == null) return;
            StaticStorage.WaypointToggleButton.iconRenderer.color = GetWaypointToggleButtonColor();
        }
    }
}
