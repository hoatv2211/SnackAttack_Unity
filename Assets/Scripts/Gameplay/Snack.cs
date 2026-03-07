using UnityEngine;
using SnackAttack.Config;

namespace SnackAttack.Gameplay
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class Snack : MonoBehaviour
    {
        public SnackData snackData;
        public float fallSpeed = 2.5f; // World units/sec (Python: 180px/s ≈ 2.5 units at 72ppu)
        public float groundY = -4f;
        public SpriteRenderer spriteRenderer;

        // Rotation (matching Python random rotation)
        private float rotationAngle;
        private float rotationSpeed;
        private float spawnX;

        private void Start()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            spawnX = transform.position.x;
            rotationAngle = Random.Range(0f, 360f);
            rotationSpeed = Random.Range(30f, 60f) * (Random.value > 0.5f ? 1f : -1f);

            if (snackData != null && snackData.sprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = snackData.sprite;
            }

            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            // Fall straight down in world space (Python parity). Using local-space translation
            // would drift when the snack rotates.
            Vector3 pos = transform.position;
            pos.y -= fallSpeed * Time.deltaTime;
            pos.x = spawnX;
            transform.position = pos;

            // Rotate (visual only)
            rotationAngle += rotationSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0, 0, rotationAngle);

            // Destroy if past ground
            if (IsBelowGround())
            {
                SimplePool.Despawn(gameObject);
            }
        }

        private bool IsBelowGround()
        {
            float halfHeight = 0f;
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                halfHeight = spriteRenderer.sprite.bounds.extents.y * Mathf.Abs(transform.lossyScale.y);
            }

            // Match Python FallingSnack rule: despawn once snack has dropped past ground line.
            float topY = transform.position.y + halfHeight;
            return topY <= groundY;
        }
    }
}
