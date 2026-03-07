using UnityEngine;
using SnackAttack.Config;
using System.Collections.Generic;
using System;

namespace SnackAttack.Gameplay
{
    public class SnackSpawner : MonoBehaviour
    {
        public GameObject snackPrefab;

        [Header("Spawn Settings (Match Python)")]
        public float baseSpawnInterval = 1.0f;
        public float spawnRateMultiplier = 1.0f;
        public int maxSnacks = 15;

        [Header("Arena Bounds")]
        public float arenaMinX = -3.5f;
        public float arenaMaxX = 3.5f;
        public float spawnY = 4.5f;
        public float groundY = -4f;

        [Header("Level Runtime")]
        [Tooltip("Filled from levels.json snack_pool")]
        public List<string> activeSnackPool = new List<string>();

        private float spawnTimer;
        private readonly HashSet<string> snackPoolLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private void Start()
        {
            spawnTimer = baseSpawnInterval;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnSnack();
                float interval = baseSpawnInterval / Mathf.Max(0.05f, spawnRateMultiplier);
                spawnTimer = interval + UnityEngine.Random.Range(-0.3f, 0.3f);
            }
        }

        public void ApplyLevelConfig(LevelConfigData levelConfig)
        {
            if (levelConfig == null)
                return;

            if (levelConfig.spawn_rate_multiplier > 0f)
                spawnRateMultiplier = levelConfig.spawn_rate_multiplier;

            SetSnackPool(levelConfig.snack_pool);
        }

        public void CopyCoreSettingsFrom(SnackSpawner source)
        {
            if (source == null || source == this)
                return;

            baseSpawnInterval = source.baseSpawnInterval;
            maxSnacks = source.maxSnacks;
            spawnRateMultiplier = source.spawnRateMultiplier;

            if (source.activeSnackPool != null && source.activeSnackPool.Count > 0)
                SetSnackPool(source.activeSnackPool.ToArray());

            spawnTimer = Mathf.Min(spawnTimer, baseSpawnInterval);
        }

        public void ApplyArena(BattleFieldArena arena)
        {
            if (arena == null)
                return;

            ApplyArenaBounds(arena.arenaMinX, arena.arenaMaxX, arena.SnackSpawnY, arena.SnackGroundY);
        }

        public void ApplyArenaBounds(float minX, float maxX, float spawnHeight, float groundHeight)
        {
            arenaMinX = Mathf.Min(minX, maxX);
            arenaMaxX = Mathf.Max(minX, maxX);
            spawnY = spawnHeight;
            groundY = groundHeight;
        }

        public void ClearSpawnedSnacks()
        {
            Snack[] spawnedSnacks = GetComponentsInChildren<Snack>(true);
            if (spawnedSnacks == null || spawnedSnacks.Length == 0)
            {
                spawnTimer = baseSpawnInterval;
                return;
            }

            for (int i = 0; i < spawnedSnacks.Length; i++)
            {
                Snack snack = spawnedSnacks[i];
                if (snack == null || snack.gameObject == null)
                    continue;

                // Use activeSelf so this still clears snacks when gameplay root is inactive.
                if (snack.gameObject.activeSelf)
                    SimplePool.Despawn(snack.gameObject);
            }

            // Reset cadence so each new round starts from a clean timer.
            spawnTimer = baseSpawnInterval;
        }

        public void SetSnackPool(string[] snackIds)
        {
            activeSnackPool.Clear();
            snackPoolLookup.Clear();

            if (snackIds == null)
                return;

            for (int i = 0; i < snackIds.Length; i++)
            {
                string id = RuntimeConfigUtil.Normalize(snackIds[i]);
                if (string.IsNullOrEmpty(id))
                    continue;

                if (snackPoolLookup.Contains(id))
                    continue;

                snackPoolLookup.Add(id);
                activeSnackPool.Add(id);
            }
        }

        private void SpawnSnack()
        {
            if (snackPrefab == null) return;
            if (transform.childCount >= maxSnacks) return;

            if (ConfigManager.Instance == null || ConfigManager.Instance.snackConfig == null) return;
            var snacks = ConfigManager.Instance.snackConfig.snacks;
            if (snacks == null || snacks.Count == 0) return;

            List<SnackData> candidates = snacks;
            if (snackPoolLookup.Count > 0)
            {
                candidates = new List<SnackData>();
                for (int i = 0; i < snacks.Count; i++)
                {
                    SnackData data = snacks[i];
                    if (data == null || string.IsNullOrWhiteSpace(data.snackId))
                        continue;

                    if (snackPoolLookup.Contains(data.snackId.Trim().ToLowerInvariant()))
                        candidates.Add(data);
                }

                if (candidates.Count == 0)
                    candidates = snacks;
            }

            // Weighted random selection (matching Python)
            float totalWeight = 0f;
            foreach (var s in candidates) totalWeight += s.spawnWeight;

            float r = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            SnackData selected = candidates[0];
            foreach (var s in candidates)
            {
                cumulative += s.spawnWeight;
                if (r <= cumulative) { selected = s; break; }
            }

            // Spawn position within arena bounds
            float x = UnityEngine.Random.Range(arenaMinX, arenaMaxX);
            Vector3 spawnPos = new Vector3(x, spawnY, 0f);

            GameObject snackGO = SimplePool.Spawn(snackPrefab, spawnPos, Quaternion.identity, transform);
            Snack snackComp = snackGO.GetComponent<Snack>();
            if (snackComp != null)
            {
                snackComp.snackData = selected;
                snackComp.groundY = groundY;
            }
        }
    }
}
