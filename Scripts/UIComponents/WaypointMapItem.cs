using PotionCraft.Core.Cursor;
using PotionCraft.ManagersSystem;
using PotionCraft.ObjectBased.InteractiveItem;
using PotionCraft.ObjectBased.RecipeMap;
using PotionCraftRecipeWaypoints.Scripts.Services;
using TooltipSystem;
using UnityEngine;

namespace PotionCraftRecipeWaypoints.Scripts.UIComponents
{
    public class WaypointMapItem : PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.RecipeMapItem, IPrimaryCursorEventsHandler, ICustomCursorVisualStateOnUse, ICustomCursorVisualStateOnHover, IHoverable
    {
        public const float WaypointAlpha = 0.75f;

        public RecipeIndex Recipe;
        public SpriteRenderer IconRenderer;
        public CircleCollider2D circleCollider;
        public GameObject path;
        public bool IsTailEndWaypoint;
        private bool loadedPath;

        private bool isHovered;
        public bool IsHovered { get => isHovered; set => SetHovered(value); }

        public override void OnPrimaryCursorClick()
        {
            RecipeService.OpenPageOnWaypointClick(this);
        }

        public new bool OnPrimaryCursorRelease()
        {
            return true;
        }

        public override TooltipContent GetTooltipContent()
        {
            return Recipe.Recipe.GetTooltipContent(1);
        }

        public CursorVisualState GetCursorVisualStateOnUse() => CursorVisualState.Pressed;
        public CursorVisualState GetCursorVisualStateOnHover() => CursorVisualState.Pressed;

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
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
