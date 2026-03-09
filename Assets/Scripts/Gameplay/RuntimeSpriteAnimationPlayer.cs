using SnackAttack.Config;
using UnityEngine;

namespace SnackAttack.Gameplay
{
    public class RuntimeSpriteAnimationPlayer : MonoBehaviour
    {
        public SpriteRenderer spriteRenderer;
        public float runFps = 8f;
        public float eatFps = 10f;
        public float walkFps = 7f;
        public float boostFps = 8f;

        private RuntimeCharacterDefinition definition;
        private bool isMoving;
        private bool isBoost;
        private bool isAirborne;
        private float eatTimer;
        private float chiliTimer;
        private float frameTimer;
        private int frameIndex;

        public void Initialize(RuntimeCharacterDefinition runtimeDefinition, SpriteRenderer renderer)
        {
            definition = runtimeDefinition;
            spriteRenderer = renderer != null ? renderer : spriteRenderer;
            frameTimer = 0f;
            frameIndex = 0;
            ApplyCurrentSprite();
        }

        public void SetMoving(bool moving)
        {
            isMoving = moving;
        }

        public void SetBoost(bool boost)
        {
            isBoost = boost;
        }

        public void SetAirborne(bool airborne)
        {
            isAirborne = airborne;
        }

        public void TriggerEat(float duration = 0.35f)
        {
            eatTimer = Mathf.Max(eatTimer, duration);
        }

        public void TriggerChili(float duration = 0.45f)
        {
            chiliTimer = Mathf.Max(chiliTimer, duration);
        }

        private void Update()
        {
            if (definition == null || spriteRenderer == null)
                return;

            if (eatTimer > 0f)
                eatTimer -= Time.deltaTime;
            if (chiliTimer > 0f)
                chiliTimer -= Time.deltaTime;

            // Idle state keeps a static first frame from run.png.
            bool isIdle = !isMoving && !isBoost && !isAirborne && eatTimer <= 0f && chiliTimer <= 0f;
            if (isIdle)
            {
                if (definition.runFrames != null && definition.runFrames.Length > 0)
                    spriteRenderer.sprite = definition.runFrames[0];
                else if (definition.walkFrames != null && definition.walkFrames.Length > 0)
                    spriteRenderer.sprite = definition.walkFrames[0];
                else if (definition.profileSprite != null)
                    spriteRenderer.sprite = definition.profileSprite;

                frameIndex = 0;
                frameTimer = 0f;
                return;
            }

            Sprite[] frames = SelectFrames(out float fps);
            if (frames == null || frames.Length == 0)
                return;

            if (frames.Length == 1)
            {
                spriteRenderer.sprite = frames[0];
                return;
            }

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, fps);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex = (frameIndex + 1) % frames.Length;
            }

            if (frameIndex >= frames.Length)
                frameIndex = 0;

            spriteRenderer.sprite = frames[frameIndex];
        }

        private Sprite[] SelectFrames(out float fps)
        {
            fps = runFps;

            if (eatTimer > 0f || chiliTimer > 0f)
            {
                fps = eatFps;
                if (definition.eatFrames != null && definition.eatFrames.Length > 0)
                    return definition.eatFrames;
            }

            if (isBoost || isAirborne)
            {
                fps = boostFps;
                if (definition.boostFrames != null && definition.boostFrames.Length > 0)
                    return definition.boostFrames;
            }

            // When not moving, keep walk animation state instead of static portrait.
            if (!isMoving)
            {
                if (definition.runFrames != null && definition.runFrames.Length > 0)
                {
                    fps = runFps;
                    return definition.runFrames;
                }

                if (definition.walkFrames != null && definition.walkFrames.Length > 0)
                {
                    fps = walkFps;
                    return definition.walkFrames;
                }

                if (definition.profileSprite != null)
                    return new[] { definition.profileSprite };

                return definition.runFrames;
            }

            if (isMoving)
            {
                if (definition.runFrames != null && definition.runFrames.Length > 0)
                {
                    fps = runFps;
                    return definition.runFrames;
                }

                if (definition.walkFrames != null && definition.walkFrames.Length > 0)
                {
                    fps = walkFps;
                    return definition.walkFrames;
                }
            }

            return definition.walkFrames;
        }

        private void ApplyCurrentSprite()
        {
            if (spriteRenderer == null || definition == null)
                return;

            if (definition.runFrames != null && definition.runFrames.Length > 0)
                spriteRenderer.sprite = definition.runFrames[0];
            else if (definition.walkFrames != null && definition.walkFrames.Length > 0)
                spriteRenderer.sprite = definition.walkFrames[0];
            else if (definition.profileSprite != null)
                spriteRenderer.sprite = definition.profileSprite;
        }
    }
}
