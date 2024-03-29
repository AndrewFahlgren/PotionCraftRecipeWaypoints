﻿using PotionCraft.Core.Cursor;
using PotionCraft.ManagersSystem;
using PotionCraft.ObjectBased.InteractiveItem;
using PotionCraft.ObjectBased.RecipeMap;
using PotionCraft.ObjectBased.UIElements.Tooltip;
using PotionCraftRecipeWaypoints.Scripts.Services;
using UnityEngine;

namespace PotionCraftRecipeWaypoints.Scripts.UIComponents
{
    public class WaypointMapItem : PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.RecipeMapItem, IPrimaryCursorEventsHandler, ICustomCursorStateOnUse, ICustomCursorStateOnHover, IHoverable
    {
        public const float WaypointAlpha = 0.75f;

        public RecipeIndex Recipe;
        public SpriteRenderer IconRenderer;
        public CircleCollider2D circleCollider;
        public GameObject path;
        public bool IsTailEndWaypoint;
        private bool loadedPath;

        public void OnPrimaryCursorClick()
        {
            RecipeService.OpenPageOnWaypointClick(this);
        }

        public bool OnPrimaryCursorRelease()
        {
            return true;
        }

        public override TooltipContent GetTooltipContent()
        {
            return Recipe.Recipe.GetTooltipContent(1);
        }

        public CursorVisualState CursorStateOnUse() => CursorVisualState.Pressed;
        public CursorVisualState CursorStateOnHover() => CursorVisualState.Pressed;

        public void SetHovered(bool hovered)
        {
            if (hovered && !loadedPath)
            {
                UIService.CreateWaypointHoverPath(this);
                loadedPath = true;
            }
            path.SetActive(hovered);
            if (IsTailEndWaypoint)
            {
                IconRenderer.enabled = !hovered;
            }
        }
    }
}
