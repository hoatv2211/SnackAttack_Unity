using UnityEngine;
using System.Collections.Generic;
using System;
using SnackAttack.Config;
using SnackAttack.Core;

namespace SnackAttack.Gameplay
{
    /// <summary>Active effect instance (matches Python player.active_effects).</summary>
    [System.Serializable]
    public struct ActiveEffect
    {
        public SnackEffectType type;
        public float magnitude;
        public float timeRemaining;
        public Sprite effectSprite;
    }

    [RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public int playerIndex = 1;
        public SpriteRenderer spriteRenderer;
        public Animator animator;

        [Header("Movement")]
        public float baseSpeed = 5f;
        public float arenaMinX = -4f;
        public float arenaMaxX = 4f;
        public float groundY = -3f;

        [Header("Flight (Boost)")]
        public bool horizontalOnly = true;
        public float arenaTopY = 4f;
        [Range(0.05f, 1f)]
        public float flightHeightFraction = 0.35f;
        public float returnToGroundSpeed = 5f;
        public float flightCeilingDampenDistance = 0.5f;

        [Header("Effect Visuals")]
        public bool showEffectSprites = true;
        [Range(1, 5)]
        public int maxEffectSprites = 3;
        public Vector2 effectAnchorOffset = new Vector2(0f, 0.85f);
        public float effectOrbitRadius = 0.55f;
        public float effectOrbitSpeed = 2.2f;
        public float effectIconScale = 0.04f;
        public int effectSortingOffset = 3;

        [Header("Ring Effect")]
        public bool useEffectRing = true;
        public string ringObjectName = "Ring";
        public GameObject ringObject;
        [Range(0f, 1f)]
        public float ringAlpha = 0.9f;
        public float ringBlinkMinInterval = 0.08f;
        public float ringBlinkMaxInterval = 0.22f;
        public Color boostRingColor = new Color32(80, 160, 255, 255);
        public Color speedBoostRingColor = new Color32(255, 200, 80, 255);
        public Color invincibilityRingColor = new Color32(255, 255, 200, 255);
        public Color chaosRingColor = new Color32(255, 60, 60, 255);
        public Color slowRingColor = new Color32(60, 180, 60, 255);

        private Rigidbody2D rb;
        private Vector2 movement;
        private float restingY;

        // Animator parameter hashes (mirrors Python animation flow: is_moving, eat, chili, fly).
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int FlyHash = Animator.StringToHash("Fly");
        private static readonly int IsAirborneHash = Animator.StringToHash("IsAirborne");
        private static readonly int EatHash = Animator.StringToHash("Eat");
        private static readonly int ChiliHash = Animator.StringToHash("Chili");
        private bool hasIsMovingParam;
        private bool hasFlyParam;
        private bool hasIsAirborneParam;
        private bool hasEatParam;
        private bool hasChiliParam;

        // Effects (match Python: active_effects, is_invincible, controls_flipped)
        private List<ActiveEffect> activeEffects = new List<ActiveEffect>();
        private readonly List<ActiveEffect> visualEffects = new List<ActiveEffect>();
        private readonly List<SpriteRenderer> effectSpriteRenderers = new List<SpriteRenderer>();
        private readonly List<Color> ringActiveColors = new List<Color>();
        private readonly List<SpriteRenderer> ringSpriteRenderers = new List<SpriteRenderer>();
        private readonly List<Renderer> ringRenderers = new List<Renderer>();
        private float ringBlinkTimer;
        private Color ringCurrentColor = Color.white;
        private bool controlsFlipped;

        private KeyCode moveLeftKey = KeyCode.A;
        private KeyCode moveRightKey = KeyCode.D;
        private KeyCode moveUpKey = KeyCode.W;
        private KeyCode moveDownKey = KeyCode.S;

        private static readonly KeyCode SoloAltLeftKey = KeyCode.LeftArrow;
        private static readonly KeyCode SoloAltRightKey = KeyCode.RightArrow;
        private static readonly KeyCode SoloAltUpKey = KeyCode.UpArrow;
        private static readonly KeyCode SoloAltDownKey = KeyCode.DownArrow;

        /// <summary>Score multiplier from Boost (Red Bull) effect. Python: get_score_multiplier().</summary>
        public float GetScoreMultiplier()
        {
            for (int i = 0; i < activeEffects.Count; i++)
                if (activeEffects[i].type == SnackEffectType.Boost)
                    return activeEffects[i].magnitude;
            return 1f;
        }

        /// <summary>Speed multiplier from SpeedBoost and Slow. Python: get_speed_multiplier().</summary>
        public float GetSpeedMultiplier()
        {
            float m = 1f;
            foreach (var e in activeEffects)
            {
                if (e.type == SnackEffectType.SpeedBoost) m *= e.magnitude;
                if (e.type == SnackEffectType.Slow) m *= e.magnitude;
                if (e.type == SnackEffectType.Boost) m *= e.magnitude;
            }
            return m;
        }

        public bool IsInvincible => activeEffects.Exists(e => e.type == SnackEffectType.Invincibility);

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            restingY = groundY;
            CacheAnimatorParameters();
            EnsureEffectSpriteRenderers();
            CacheRingVisuals();
            SetRingVisible(false);
        }

        private void Start()
        {
            ApplyRuntimeConfig();
        }

        public void ApplyRuntimeConfig()
        {
            ApplyControlConfig();
            ApplyPowerupVisualConfig();
        }

        public void ApplyArenaBounds(float minX, float maxX, float ground, float top)
        {
            arenaMinX = minX;
            arenaMaxX = maxX;
            groundY = ground;
            arenaTopY = top;
            restingY = groundY;

            if (rb == null)
                return;

            Vector2 pos = rb.position;
            pos.x = Mathf.Clamp(pos.x, arenaMinX, arenaMaxX);
            float minY = Mathf.Min(groundY, arenaTopY);
            float maxY = Mathf.Max(groundY, arenaTopY);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            rb.position = pos;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                movement = Vector2.zero;
                UpdateAnimatorLocomotion(false);
                UpdateAnimatorFlight(false);
                UpdateAnimatorAirborne(false);
                HideEffectSprites();
                SetRingVisible(false);
                return;
            }

            UpdateEffects(Time.deltaTime);

            // Player 1: A/D keys; Player 2: Arrow keys
            float h = 0f;
            float v = 0f;
            bool canMoveVertical = !horizontalOnly || HasEffect(SnackEffectType.Boost);
            bool splitTwoPlayerControls = IsTwoPlayerSplitMode();

            if (playerIndex == 1)
            {
                h = GetAxisFromKeys(moveLeftKey, moveRightKey);
                if (canMoveVertical)
                    v = GetAxisFromKeys(moveDownKey, moveUpKey);

                if (!splitTwoPlayerControls)
                {
                    h = MergeAxes(h, GetAxisFromKeys(SoloAltLeftKey, SoloAltRightKey));
                    if (canMoveVertical)
                        v = MergeAxes(v, GetAxisFromKeys(SoloAltDownKey, SoloAltUpKey));
                }
            }
            else if (playerIndex == 2)
            {
                h = GetAxisFromKeys(moveLeftKey, moveRightKey);

                if (canMoveVertical)
                    v = GetAxisFromKeys(moveDownKey, moveUpKey);
            }

            // Chaos: flip controls (Python: controls_flipped)
            if (controlsFlipped)
            {
                h = -h;
                v = -v;
            }

            movement = new Vector2(h, canMoveVertical ? v : 0f);
            if (movement.sqrMagnitude > 1f)
                movement.Normalize();

            bool isMoving = Mathf.Abs(movement.x) > 0.01f || Mathf.Abs(movement.y) > 0.01f;

            UpdateAnimatorLocomotion(isMoving);
            UpdateAnimatorFlight(HasEffect(SnackEffectType.Boost));

            if (h != 0 && spriteRenderer != null)
                spriteRenderer.flipX = h < 0;
        }

        private void LateUpdate()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                HideEffectSprites();
                SetRingVisible(false);
                return;
            }

            UpdateEffectRing(Time.deltaTime);
            UpdateEffectSprites(Time.time);
        }

        private void UpdateEffects(float dt)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var e = activeEffects[i];
                e.timeRemaining -= dt;
                if (e.timeRemaining <= 0)
                {
                    if (e.type == SnackEffectType.Chaos) controlsFlipped = false;
                    activeEffects.RemoveAt(i);
                }
                else
                    activeEffects[i] = e;
            }
        }

        /// <summary>Apply effect from snack (Python: player.apply_effect). Invincibility blocks slow/chaos.</summary>
        public void ApplyEffect(SnackEffectType type, float magnitude, float duration, Sprite effectSprite = null)
        {
            if (type == SnackEffectType.None || duration <= 0) return;
            activeEffects.Add(new ActiveEffect
            {
                type = type,
                magnitude = magnitude,
                timeRemaining = duration,
                effectSprite = effectSprite,
            });
            if (type == SnackEffectType.Chaos) controlsFlipped = true;

            if (type == SnackEffectType.Chaos)
                TriggerChiliAnimation();
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            bool canMoveVertical = !horizontalOnly || HasEffect(SnackEffectType.Boost);
            float speed = baseSpeed * GetSpeedMultiplier();
            Vector2 effectiveMovement = movement;

            // Match Python's soft ceiling approach: upward movement is damped shortly
            // before reaching the flight ceiling so the dog doesn't snap hard to the cap.
            if (canMoveVertical && horizontalOnly && movement.y > 0f)
            {
                float ceiling = GetFlightCeiling();
                float distanceToCeiling = ceiling - rb.position.y;
                if (distanceToCeiling <= flightCeilingDampenDistance)
                {
                    float damp = Mathf.Clamp01(distanceToCeiling / Mathf.Max(0.0001f, flightCeilingDampenDistance));
                    effectiveMovement.y *= damp;
                }
            }

            Vector2 newPos = rb.position + effectiveMovement * speed * Time.fixedDeltaTime;

            newPos.x = Mathf.Clamp(newPos.x, arenaMinX, arenaMaxX);

            if (canMoveVertical)
            {
                if (horizontalOnly)
                {
                    float flightCeiling = GetFlightCeiling();
                    float minY = Mathf.Min(restingY, flightCeiling);
                    float maxY = Mathf.Max(restingY, flightCeiling);
                    newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
                }
                else
                {
                    float minY = Mathf.Min(groundY, arenaTopY);
                    float maxY = Mathf.Max(groundY, arenaTopY);
                    newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
                }
            }
            else
            {
                float returnStep = baseSpeed * returnToGroundSpeed * Time.fixedDeltaTime;
                newPos.y = Mathf.MoveTowards(rb.position.y, restingY, returnStep);
            }

            rb.MovePosition(newPos);
            UpdateAnimatorAirborne(newPos.y > restingY + 0.05f);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            Snack snack = collision.GetComponent<Snack>();
            if (snack == null || snack.snackData == null) return;

            var data = snack.snackData;
            int points = Mathf.RoundToInt(data.points * GetScoreMultiplier());
            GameManager.Instance.AddScore(playerIndex, points);
            PlaySnackCollectSounds(data.snackId);
            TriggerEatAnimation();
            SimplePool.Despawn(snack.gameObject);

            // Apply effect (Python: invincible skips slow/chaos)
            if (data.effectType == SnackEffectType.None) return;
            if (IsInvincible && (data.effectType == SnackEffectType.Slow || data.effectType == SnackEffectType.Chaos))
                return;
            ApplyEffect(data.effectType, data.effectMagnitude, data.effectDuration, data.sprite);
        }

        private static string NormalizeSnackId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            return raw.Trim().ToLowerInvariant();
        }

        private static void PlaySnackCollectSounds(string snackIdRaw)
        {
            EventManager.TriggerEvent("PLAY_SOUND", "dog_eat");

            string snackId = NormalizeSnackId(snackIdRaw);
            if (snackId == "broccoli")
            {
                EventManager.TriggerEvent("PLAY_SOUND", "broccoli");
                return;
            }

            if (snackId == "red_bull")
            {
                EventManager.TriggerEvent("PLAY_SOUND", "red_bull");
                return;
            }

            if (snackId == "chilli" || snackId == "spicy_pepper")
            {
                EventManager.TriggerEvent("PLAY_SOUND", "chilli");
                return;
            }

            EventManager.TriggerEvent("PLAY_SOUND", "point_earned");
        }

        private bool HasEffect(SnackEffectType type)
        {
            for (int i = 0; i < activeEffects.Count; i++)
                if (activeEffects[i].type == type)
                    return true;
            return false;
        }

        private float GetFlightCeiling()
        {
            float span = arenaTopY - restingY;
            return restingY + span * Mathf.Clamp01(flightHeightFraction);
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null) return;

            foreach (var p in animator.parameters)
            {
                if (p.nameHash == IsMovingHash) hasIsMovingParam = true;
                else if (p.nameHash == FlyHash) hasFlyParam = true;
                else if (p.nameHash == IsAirborneHash) hasIsAirborneParam = true;
                else if (p.nameHash == EatHash) hasEatParam = true;
                else if (p.nameHash == ChiliHash) hasChiliParam = true;
            }
        }

        private void UpdateAnimatorLocomotion(bool isMoving)
        {
            if (animator != null && hasIsMovingParam)
                animator.SetBool(IsMovingHash, isMoving);
        }

        private void UpdateAnimatorFlight(bool isFlying)
        {
            if (animator != null && hasFlyParam)
                animator.SetBool(FlyHash, isFlying);
        }

        private void UpdateAnimatorAirborne(bool isAirborne)
        {
            if (animator != null && hasIsAirborneParam)
                animator.SetBool(IsAirborneHash, isAirborne);
        }

        private void TriggerEatAnimation()
        {
            if (animator != null && hasEatParam)
                animator.SetTrigger(EatHash);
        }

        private void TriggerChiliAnimation()
        {
            if (animator != null && hasChiliParam)
                animator.SetTrigger(ChiliHash);
        }

        private void EnsureEffectSpriteRenderers()
        {
            while (effectSpriteRenderers.Count < maxEffectSprites)
            {
                int idx = effectSpriteRenderers.Count;
                GameObject icon = new GameObject($"EffectIcon_{idx}");
                icon.transform.SetParent(transform, false);

                SpriteRenderer sr = icon.AddComponent<SpriteRenderer>();
                sr.enabled = false;
                effectSpriteRenderers.Add(sr);
            }
        }

        private void HideEffectSprites()
        {
            for (int i = 0; i < effectSpriteRenderers.Count; i++)
            {
                if (effectSpriteRenderers[i] != null)
                    effectSpriteRenderers[i].enabled = false;
            }
        }

        private void UpdateEffectSprites(float now)
        {
            if (!showEffectSprites || maxEffectSprites <= 0)
            {
                HideEffectSprites();
                return;
            }

            EnsureEffectSpriteRenderers();
            BuildVisualEffectList();

            if (visualEffects.Count == 0)
            {
                HideEffectSprites();
                return;
            }

            int visibleCount = Mathf.Min(visualEffects.Count, effectSpriteRenderers.Count);
            for (int i = 0; i < effectSpriteRenderers.Count; i++)
            {
                SpriteRenderer sr = effectSpriteRenderers[i];
                if (sr == null) continue;

                if (i >= visibleCount)
                {
                    sr.enabled = false;
                    continue;
                }

                ActiveEffect effect = visualEffects[i];
                float angle = now * effectOrbitSpeed + (Mathf.PI * 2f * i / visibleCount);
                float x = Mathf.Cos(angle) * effectOrbitRadius;
                float y = Mathf.Sin(angle) * effectOrbitRadius * 0.45f;
                float pulse = 0.9f + Mathf.Sin(now * 6f + i) * 0.1f;

                sr.sprite = effect.effectSprite;
                sr.transform.localPosition = new Vector3(effectAnchorOffset.x + x, effectAnchorOffset.y + y, 0f);
                sr.transform.localScale = Vector3.one * (effectIconScale * pulse);
                sr.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : sr.sortingLayerID;
                sr.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 0) + effectSortingOffset;
                sr.color = new Color(1f, 1f, 1f, 0.95f);
                sr.enabled = true;
            }
        }

        private void BuildVisualEffectList()
        {
            visualEffects.Clear();

            for (int i = 0; i < activeEffects.Count; i++)
            {
                ActiveEffect effect = activeEffects[i];
                if (effect.effectSprite == null || effect.timeRemaining <= 0f)
                    continue;

                int existingIndex = visualEffects.FindIndex(e => e.type == effect.type);
                if (existingIndex >= 0)
                {
                    if (effect.timeRemaining > visualEffects[existingIndex].timeRemaining)
                        visualEffects[existingIndex] = effect;
                }
                else
                {
                    visualEffects.Add(effect);
                }
            }

            visualEffects.Sort((a, b) => GetEffectPriority(a.type).CompareTo(GetEffectPriority(b.type)));
            if (visualEffects.Count > maxEffectSprites)
                visualEffects.RemoveRange(maxEffectSprites, visualEffects.Count - maxEffectSprites);
        }

        private static int GetEffectPriority(SnackEffectType type)
        {
            switch (type)
            {
                case SnackEffectType.Boost:
                    return 0;
                case SnackEffectType.SpeedBoost:
                    return 1;
                case SnackEffectType.Invincibility:
                    return 2;
                case SnackEffectType.Chaos:
                    return 3;
                case SnackEffectType.Slow:
                    return 4;
                default:
                    return 99;
            }
        }

        private bool IsTwoPlayerSplitMode()
        {
            if (GameManager.Instance == null || string.IsNullOrWhiteSpace(GameManager.Instance.gameMode))
                return false;

            return string.Equals(GameManager.Instance.gameMode.Trim(), "2p", StringComparison.OrdinalIgnoreCase);
        }

        private static float GetAxisFromKeys(KeyCode negativeKey, KeyCode positiveKey)
        {
            float axis = 0f;
            if (Input.GetKey(negativeKey)) axis -= 1f;
            if (Input.GetKey(positiveKey)) axis += 1f;
            return axis;
        }

        private static float MergeAxes(float primary, float secondary)
        {
            if (Mathf.Abs(primary) > 0.01f)
                return Mathf.Clamp(primary, -1f, 1f);

            return Mathf.Clamp(secondary, -1f, 1f);
        }

        private void ApplyControlConfig()
        {
            PlayerControlsConfig controls = null;
            if (ConfigManager.Instance != null && ConfigManager.Instance.controlsConfig != null)
            {
                controls = playerIndex == 2
                    ? ConfigManager.Instance.controlsConfig.player2
                    : ConfigManager.Instance.controlsConfig.player1;
            }

            KeyCode defaultLeft = playerIndex == 2 ? KeyCode.LeftArrow : KeyCode.A;
            KeyCode defaultRight = playerIndex == 2 ? KeyCode.RightArrow : KeyCode.D;
            KeyCode defaultUp = playerIndex == 2 ? KeyCode.UpArrow : KeyCode.W;
            KeyCode defaultDown = playerIndex == 2 ? KeyCode.DownArrow : KeyCode.S;

            if (controls == null)
            {
                moveLeftKey = defaultLeft;
                moveRightKey = defaultRight;
                moveUpKey = defaultUp;
                moveDownKey = defaultDown;
                return;
            }

            moveLeftKey = RuntimeConfigUtil.KeyCodeFromConfig(controls.left, defaultLeft);
            moveRightKey = RuntimeConfigUtil.KeyCodeFromConfig(controls.right, defaultRight);
            moveUpKey = RuntimeConfigUtil.KeyCodeFromConfig(controls.up, defaultUp);
            moveDownKey = RuntimeConfigUtil.KeyCodeFromConfig(controls.down, defaultDown);
        }

        private void ApplyPowerupVisualConfig()
        {
            if (ConfigManager.Instance == null)
                return;

            PowerupVisualsConfig visualCfg = ConfigManager.Instance.powerupVisualsConfig;
            if (visualCfg == null)
                return;

            if (visualCfg.status_indicator != null)
                showEffectSprites = visualCfg.status_indicator.enabled;

            AuraVisualConfig aura = visualCfg.aura;
            if (aura == null)
                return;

            useEffectRing = aura.enabled;
            ringAlpha = Mathf.Clamp01(aura.base_alpha / 255f);

            AuraColorConfig colors = aura.colors;
            if (colors == null)
                return;

            boostRingColor = RuntimeConfigUtil.ColorFromRgb255(colors.boost, boostRingColor);
            speedBoostRingColor = RuntimeConfigUtil.ColorFromRgb255(colors.speed_boost, speedBoostRingColor);
            invincibilityRingColor = RuntimeConfigUtil.ColorFromRgb255(colors.invincibility, invincibilityRingColor);
            chaosRingColor = RuntimeConfigUtil.ColorFromRgb255(colors.chaos, chaosRingColor);
            slowRingColor = RuntimeConfigUtil.ColorFromRgb255(colors.slow, slowRingColor);
        }

        private void CacheRingVisuals()
        {
            ringSpriteRenderers.Clear();
            ringRenderers.Clear();

            if (ringObject == null)
            {
                Transform[] all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    string name = all[i].name;
                    if (string.Equals(name, ringObjectName, StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith(ringObjectName, StringComparison.OrdinalIgnoreCase))
                    {
                        ringObject = all[i].gameObject;
                        break;
                    }
                }
            }

            if (ringObject == null)
                return;

            ringSpriteRenderers.AddRange(ringObject.GetComponentsInChildren<SpriteRenderer>(true));

            Renderer[] renderers = ringObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!(renderers[i] is SpriteRenderer))
                    ringRenderers.Add(renderers[i]);
            }
        }

        private void SetRingVisible(bool visible)
        {
            if (ringObject == null)
                return;

            if (ringObject.activeSelf != visible)
                ringObject.SetActive(visible);
        }

        private void UpdateEffectRing(float dt)
        {
            if (!useEffectRing)
            {
                SetRingVisible(false);
                return;
            }

            if (ringObject == null)
                CacheRingVisuals();

            if (ringObject == null)
                return;

            ringActiveColors.Clear();
            for (int i = 0; i < activeEffects.Count; i++)
            {
                ActiveEffect effect = activeEffects[i];
                if (effect.timeRemaining <= 0f)
                    continue;

                if (!TryGetRingColor(effect.type, out Color c))
                    continue;

                if (!ContainsColor(ringActiveColors, c))
                    ringActiveColors.Add(c);
            }

            if (ringActiveColors.Count == 0)
            {
                SetRingVisible(false);
                return;
            }

            SetRingVisible(true);

            Color target;
            if (ringActiveColors.Count == 1)
            {
                target = ringActiveColors[0];
                ringBlinkTimer = 0f;
            }
            else
            {
                ringBlinkTimer -= dt;
                if (ringBlinkTimer <= 0f)
                {
                    int idx = UnityEngine.Random.Range(0, ringActiveColors.Count);
                    Color chosen = ringActiveColors[idx];
                    if (ringActiveColors.Count > 1 && AreColorsClose(chosen, ringCurrentColor))
                    {
                        chosen = ringActiveColors[(idx + 1) % ringActiveColors.Count];
                    }

                    ringCurrentColor = chosen;
                    float minInterval = Mathf.Max(0.02f, ringBlinkMinInterval);
                    float maxInterval = Mathf.Max(minInterval, ringBlinkMaxInterval);
                    ringBlinkTimer = UnityEngine.Random.Range(minInterval, maxInterval);
                }

                target = ringCurrentColor;
            }

            target.a = ringAlpha;
            ApplyRingColor(target);
        }

        private void ApplyRingColor(Color color)
        {
            for (int i = 0; i < ringSpriteRenderers.Count; i++)
            {
                SpriteRenderer sr = ringSpriteRenderers[i];
                if (sr == null) continue;
                sr.color = color;
            }

            for (int i = 0; i < ringRenderers.Count; i++)
            {
                Renderer r = ringRenderers[i];
                if (r == null || r.material == null) continue;

                if (r.material.HasProperty("_BaseColor"))
                    r.material.SetColor("_BaseColor", color);
                else if (r.material.HasProperty("_Color"))
                    r.material.SetColor("_Color", color);
            }
        }

        private bool TryGetRingColor(SnackEffectType type, out Color color)
        {
            switch (type)
            {
                case SnackEffectType.Boost:
                    color = boostRingColor;
                    return true;
                case SnackEffectType.SpeedBoost:
                    color = speedBoostRingColor;
                    return true;
                case SnackEffectType.Invincibility:
                    color = invincibilityRingColor;
                    return true;
                case SnackEffectType.Chaos:
                    color = chaosRingColor;
                    return true;
                case SnackEffectType.Slow:
                    color = slowRingColor;
                    return true;
                default:
                    color = Color.white;
                    return false;
            }
        }

        private static bool ContainsColor(List<Color> colors, Color color)
        {
            for (int i = 0; i < colors.Count; i++)
            {
                if (AreColorsClose(colors[i], color))
                    return true;
            }

            return false;
        }

        private static bool AreColorsClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f &&
                   Mathf.Abs(a.g - b.g) < 0.01f &&
                   Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }
}
