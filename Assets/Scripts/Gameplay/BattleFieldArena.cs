using UnityEngine;

namespace SnackAttack.Gameplay
{
    /// <summary>
    /// Attach this to each BattleField object to define arena bounds for players/spawners.
    /// Single mode can use one BattleFieldArena, while vs/2p can use BattleField_1 and BattleField_2.
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleFieldArena : MonoBehaviour
    {
        [Header("Arena Bounds")]
        public float arenaMinX = -4f;
        public float arenaMaxX = 4f;
        public float groundY = -3f;
        public float arenaTopY = 4f;

        [Header("Spawn")]
        [Tooltip("Optional explicit spawn point for the player in this arena")]
        public Transform playerSpawnPoint;

        [Header("Auto Fit From Sprite")]
        public bool autoFitFromSpriteRenderer = false;
        public SpriteRenderer sourceSpriteRenderer;
        public float horizontalPadding = 0.2f;
        public float topPadding = 0.1f;
        public float bottomPadding = 0.1f;

        [Header("Snack Spawn (Optional)")]
        [Tooltip("Spawn Y is calculated from arenaTopY + this offset")]
        public float snackSpawnYOffset = 0.5f;
        [Tooltip("Snack despawn ground Y is calculated from groundY + this offset")]
        public float snackGroundYOffset = 0f;

        public float SnackSpawnY => arenaTopY + snackSpawnYOffset;
        public float SnackGroundY => groundY + snackGroundYOffset;

        private void OnValidate()
        {
            if (autoFitFromSpriteRenderer)
                RecalculateFromSpriteBounds();
        }

        [ContextMenu("Recalculate Bounds From Sprite")]
        public void RecalculateFromSpriteBounds()
        {
            SpriteRenderer source = sourceSpriteRenderer;
            if (source == null)
                source = GetComponent<SpriteRenderer>();

            if (source == null)
                source = GetComponentInChildren<SpriteRenderer>();

            if (source == null)
                return;

            Bounds b = source.bounds;
            arenaMinX = b.min.x + horizontalPadding;
            arenaMaxX = b.max.x - horizontalPadding;
            groundY = b.min.y + bottomPadding;
            arenaTopY = b.max.y - topPadding;
        }
    }
}