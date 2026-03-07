using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using SnackAttack.Core;
using SnackAttack.Config;
using DamageNumbersPro;

namespace SnackAttack.Gameplay
{
    public enum GameState
    {
        MainMenu,
        CharacterSelect,
        StormIntro,
        Countdown,
        Playing,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        [System.Serializable]
        public class CharacterPrefabEntry
        {
            public string characterId;
            public GameObject prefab;
        }

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }

        [Header("Match Settings")]
        public float roundDuration = 60f;
        public int maxRounds = 3;

        [Header("Runtime State")]
        public float timeRemaining;
        public int player1Score = 0;
        public int player2Score = 0;
        public int currentRound = 1;
        public int p1RoundWins = 0;
        public int p2RoundWins = 0;

        [Header("Countdown")]
        public int countdownValue = 3;
        public float countdownTimer = 0f;

        [Header("Storm Intro")]
        public bool useStormIntro = true;
        [Min(0f)] public float stormIntroDuration = 1.2f;
        [Min(0f)] public float stormIntroPostDelay = 0.1f;
        [Min(0.1f)] public float stormEntryOffset = 2f;
        public bool stormIntroMoveToArenaCenter = true;
        [Tooltip("Optional. If null, GameManager auto-finds object named UI_storm_intro")]
        public CanvasGroup stormIntroOverlay;

        [Header("Scene References")]
        [Tooltip("Legacy fallback root used when mode-specific roots are not assigned")]
        public GameObject gameplayEntitiesRoot;
        [Tooltip("Used when gameMode is single/single_dog")]
        public GameObject gameplayEntitiesSingleRoot;
        [Tooltip("Used when gameMode is vsai/1p/2p")]
        public GameObject gameplayEntitiesVersusRoot;

        [Header("BattleField Arena Info")]
        [Tooltip("Optional explicit refs. If null, GameManager auto-finds by names BattleField, BattleField_1, BattleField_2")]
        public BattleFieldArena singleBattleFieldArena;
        public BattleFieldArena versusBattleFieldArenaP1;
        public BattleFieldArena versusBattleFieldArenaP2;

        [Header("Game Mode")]
        public string gameMode = "single"; // "single", "vsai", "2p" (legacy: "single_dog", "1p")
        public bool vsAI = true;

        [Header("AI Runtime")]
        [Tooltip("Name in ai_difficulty.json (easy/medium/hard)")]
        public string aiDifficulty = "hard";

        [Header("Level Runtime")]
        public int currentLevel = 1;
        public Camera gameplayCamera;

        [Header("Character Spawn")]
        public bool useCharacterPrefabs = true;
        public string selectedPlayer1CharacterId = "jazzy";
        public string selectedPlayer2CharacterId = "biggie";
        public string defaultPlayer1CharacterId = "jazzy";
        public string defaultPlayer2CharacterId = "biggie";
        public List<CharacterPrefabEntry> characterPrefabs = new List<CharacterPrefabEntry>();
        [Header("VS AI Character Random")]
        [Tooltip("If enabled, P2 character is randomized from AI list each time a VS AI match starts")]
        public bool randomizeAiCharacterInVsAi = true;
        [Tooltip("Try to avoid picking same character as Player 1 when possible")]
        public bool avoidSameCharacterAsPlayer1InVsAi = true;
        [Tooltip("Drag prefabs from Prefabs/AI here. If empty, falls back to characterPrefabs")]
        public List<CharacterPrefabEntry> aiCharacterPrefabs = new List<CharacterPrefabEntry>();
        public Transform player1SpawnOverride;
        public Transform player2SpawnOverride;
        public Transform playerSpawnParent;

        [Header("Score Popups")]
        public DamageNumber scorePopupPrefab;
        public Vector3 scorePopupOffset = new Vector3(0f, 1.2f, 0f);
        public Color positiveScoreColor = new Color(0.35f, 1f, 0.35f, 1f);
        public Color negativeScoreColor = new Color(1f, 0.35f, 0.35f, 1f);
        public bool prewarmScorePopupPool = true;
        [Min(1)] public int scorePopupPoolSize = 30;

        private Coroutine stormIntroRoutine;

        private struct StormIntroMoveData
        {
            public Transform transform;
            public Vector3 startPosition;
            public Vector3 targetPosition;
        }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            // Set state sớm để UI (Start) luôn thấy đúng state → tránh "scene 1 kiểu, in-game 1 kiểu"
            State = GameState.MainMenu;

            AutoResolveStormIntroUiByName();
            SetStormIntroOverlayVisible(false);
        }

        private void Start()
        {
            if (ConfigManager.Instance != null && ConfigManager.Instance.gameSettings != null)
                roundDuration = ConfigManager.Instance.gameSettings.timeLimitPerRoundSeconds;

            AutoResolveGameplayRootsByName();
            AutoResolveStormIntroUiByName();

            LoadRuntimeConfigDefaults();
            ApplyLevelConfigForCurrentRound();

            if (roundDuration <= 0) roundDuration = 60f;
            timeRemaining = roundDuration;

            PrepareScorePopupPool();
            SetStormIntroOverlayVisible(false);

            ChangeState(GameState.MainMenu);
        }

        private void Update()
        {
            switch (State)
            {
                case GameState.Countdown:
                    UpdateCountdown();
                    break;
                case GameState.Playing:
                    UpdatePlaying();
                    break;
            }
        }

        private void UpdateCountdown()
        {
            countdownTimer -= Time.deltaTime;
            if (countdownTimer <= 0f)
            {
                countdownValue--;
                if (countdownValue <= 0)
                {
                    // GO! — start gameplay
                    ChangeState(GameState.Playing);
                }
                else
                {
                    countdownTimer = 1f;

                    if (countdownValue == 2)
                        EventManager.TriggerEvent("PLAY_SOUND", "countdown_2_3");
                    else if (countdownValue == 1)
                        EventManager.TriggerEvent("PLAY_SOUND", "countdown_1");

                    EventManager.TriggerEvent("COUNTDOWN_TICK", countdownValue);
                }
            }
        }

        private void UpdatePlaying()
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                EndRound();
            }
        }

        private void EndRound()
        {
            // Determine round winner
            if (player1Score > player2Score)
                p1RoundWins++;
            else if (player2Score > player1Score)
                p2RoundWins++;

            if (currentRound >= maxRounds || p1RoundWins > maxRounds / 2 || p2RoundWins > maxRounds / 2)
            {
                ChangeState(GameState.GameOver);
            }
            else
            {
                // Next round
                currentRound++;
                player1Score = 0;
                player2Score = 0;
                ApplyLevelConfigForCurrentRound();
                timeRemaining = roundDuration;
                StartCountdown();
            }
        }

        public void StartCountdown()
        {
            ClearSpawnedSnacksForNewRound();

            countdownValue = 3;
            countdownTimer = 1f;
            timeRemaining = roundDuration;
            ChangeState(GameState.Countdown);

            // Python parity: play the same sound for countdown 3 and 2.
            EventManager.TriggerEvent("PLAY_SOUND", "countdown_2_3");
        }

        private void StartStormIntro()
        {
            if (stormIntroRoutine != null)
            {
                StopCoroutine(stormIntroRoutine);
                stormIntroRoutine = null;
            }

            // Clear old snacks before showing intro so the board is already clean.
            ClearSpawnedSnacksForNewRound();

            GameObject gameplayRoot = GetActiveGameplayRoot();
            if (gameplayRoot == null)
            {
                StartCountdown();
                return;
            }

            ChangeState(GameState.StormIntro);
            stormIntroRoutine = StartCoroutine(RunStormIntroSequence(gameplayRoot));
        }

        private System.Collections.IEnumerator RunStormIntroSequence(GameObject gameplayRoot)
        {
            List<StormIntroMoveData> moves = BuildStormIntroMoveData(gameplayRoot);
            float duration = Mathf.Max(0f, stormIntroDuration);

            if (duration > 0f && moves.Count > 0)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (State != GameState.StormIntro)
                    {
                        stormIntroRoutine = null;
                        yield break;
                    }

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = Mathf.SmoothStep(0f, 1f, t);

                    for (int i = 0; i < moves.Count; i++)
                    {
                        StormIntroMoveData move = moves[i];
                        if (move.transform == null)
                            continue;

                        move.transform.position = Vector3.LerpUnclamped(move.startPosition, move.targetPosition, eased);
                    }

                    yield return null;
                }

                for (int i = 0; i < moves.Count; i++)
                {
                    StormIntroMoveData move = moves[i];
                    if (move.transform != null)
                        move.transform.position = move.targetPosition;
                }
            }

            if (stormIntroPostDelay > 0f)
                yield return new WaitForSeconds(stormIntroPostDelay);

            stormIntroRoutine = null;

            if (State == GameState.StormIntro)
                StartCountdown();
        }

        private List<StormIntroMoveData> BuildStormIntroMoveData(GameObject gameplayRoot)
        {
            List<StormIntroMoveData> moves = new List<StormIntroMoveData>(2);
            if (gameplayRoot == null)
                return moves;

            Transform p1 = ResolveSpawnedPlayerTransform(gameplayRoot, "Player1");
            Transform p2 = ResolveSpawnedPlayerTransform(gameplayRoot, "Player2");

            BattleFieldArena arenaP1 = ResolveArenaForPlayer(gameplayRoot, 1);
            BattleFieldArena arenaP2 = ResolveArenaForPlayer(gameplayRoot, 2);

            TryAddStormIntroMove(moves, p1, arenaP1, fromLeft: true);

            if (!IsSingleMode())
                TryAddStormIntroMove(moves, p2, arenaP2, fromLeft: false);

            return moves;
        }

        private Transform ResolveSpawnedPlayerTransform(GameObject gameplayRoot, string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
                return null;

            if (gameplayRoot != null)
            {
                Transform local = gameplayRoot.transform.Find(slotName);
                if (local != null)
                    return local;
            }

            if (playerSpawnParent != null)
            {
                Transform underSpawnParent = playerSpawnParent.Find(slotName);
                if (underSpawnParent != null)
                    return underSpawnParent;
            }

            GameObject global = GameObject.Find(slotName);
            if (global != null)
                return global.transform;

            Transform[] allTransforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform candidate = allTransforms[i];
                if (candidate != null && candidate.name == slotName)
                    return candidate;
            }

            return null;
        }

        private void TryAddStormIntroMove(List<StormIntroMoveData> moves, Transform playerTransform, BattleFieldArena arena, bool fromLeft)
        {
            if (moves == null || playerTransform == null)
                return;

            Vector3 target = playerTransform.position;
            if (stormIntroMoveToArenaCenter && arena != null)
            {
                target.x = (arena.arenaMinX + arena.arenaMaxX) * 0.5f;
                target.y = arena.groundY;
                playerTransform.position = target;
            }

            float offset = Mathf.Max(0.1f, stormEntryOffset);

            float edgeX;
            if (arena != null)
                edgeX = fromLeft ? arena.arenaMinX : arena.arenaMaxX;
            else
                edgeX = target.x;

            Vector3 start = target;
            start.x = fromLeft ? edgeX - offset : edgeX + offset;

            playerTransform.position = start;
            SetStormIntroFacing(playerTransform, fromLeft);

            moves.Add(new StormIntroMoveData
            {
                transform = playerTransform,
                startPosition = start,
                targetPosition = target,
            });
        }

        private static void SetStormIntroFacing(Transform playerTransform, bool fromLeft)
        {
            SpriteRenderer sprite = ResolvePlayerSpriteRenderer(playerTransform);
            if (sprite == null)
                return;

            // fromLeft: moving left->right, fromRight: moving right->left.
            sprite.flipX = !fromLeft;
        }

        private static SpriteRenderer ResolvePlayerSpriteRenderer(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            if (playerController != null && playerController.spriteRenderer != null)
                return playerController.spriteRenderer;

            AIPlayerController aiController = playerTransform.GetComponent<AIPlayerController>();
            if (aiController != null && aiController.spriteRenderer != null)
                return aiController.spriteRenderer;

            return playerTransform.GetComponentInChildren<SpriteRenderer>();
        }

        private void AutoResolveStormIntroUiByName()
        {
            if (stormIntroOverlay != null)
                return;

            GameObject stormObject = GameObject.Find("UI_storm_intro");
            if (stormObject == null)
            {
                Animator[] animators = FindObjectsOfType<Animator>(true);
                for (int i = 0; i < animators.Length; i++)
                {
                    Animator candidate = animators[i];
                    if (candidate != null && candidate.gameObject.name == "UI_storm_intro")
                    {
                        stormObject = candidate.gameObject;
                        break;
                    }
                }
            }

            if (stormObject == null)
                return;

            stormIntroOverlay = stormObject.GetComponent<CanvasGroup>();
            if (stormIntroOverlay == null)
                stormIntroOverlay = stormObject.AddComponent<CanvasGroup>();
        }

        private void SetStormIntroOverlayVisible(bool visible)
        {
            if (stormIntroOverlay == null)
                return;

            GameObject overlayObject = stormIntroOverlay.gameObject;
            if (overlayObject != null && overlayObject.activeSelf != visible)
                overlayObject.SetActive(visible);

            stormIntroOverlay.alpha = visible ? 1f : 0f;
            stormIntroOverlay.interactable = false;
            stormIntroOverlay.blocksRaycasts = visible;
        }

        private void ClearSpawnedSnacksForNewRound()
        {
            GameObject gameplayRoot = GetActiveGameplayRoot();
            SnackSpawner[] spawners = gameplayRoot != null
                ? gameplayRoot.GetComponentsInChildren<SnackSpawner>(true)
                : FindObjectsOfType<SnackSpawner>(true);

            if (spawners == null || spawners.Length == 0)
                return;

            for (int i = 0; i < spawners.Length; i++)
            {
                SnackSpawner spawner = spawners[i];
                if (spawner == null)
                    continue;

                spawner.ClearSpawnedSnacks();
            }
        }

        public void ChangeState(GameState newState)
        {
            if (newState != GameState.StormIntro && stormIntroRoutine != null)
            {
                StopCoroutine(stormIntroRoutine);
                stormIntroRoutine = null;
            }

            State = newState;
            EventManager.TriggerEvent("GAME_STATE_CHANGED", State);

            bool shouldBeActive = (newState == GameState.StormIntro || newState == GameState.Countdown || newState == GameState.Playing);
            RefreshGameplayRootActivation(shouldBeActive);

            if (newState == GameState.StormIntro)
            {
                AutoResolveStormIntroUiByName();
                SetStormIntroOverlayVisible(true);
            }
            else
            {
                SetStormIntroOverlayVisible(false);
            }

            if (State == GameState.GameOver)
            {
                Debug.Log($"Game Over! P1 Rounds: {p1RoundWins} | P2 Rounds: {p2RoundWins}");
            }
        }

        public void AddScore(int playerIndex, int amount)
        {
            AddScore(playerIndex, amount, null);
        }

        public void AddScore(int playerIndex, int amount, Transform sourceTransform)
        {
            if (State != GameState.Playing) return;

            if (playerIndex == 1)
                player1Score += amount;
            else if (playerIndex == 2)
                player2Score += amount;

            TrySpawnScorePopup(playerIndex, amount, sourceTransform);

            EventManager.TriggerEvent("SCORE_UPDATED");
        }

        private void PrepareScorePopupPool()
        {
            if (!prewarmScorePopupPool || scorePopupPrefab == null)
                return;

            scorePopupPrefab.enablePooling = true;
            scorePopupPrefab.poolSize = Mathf.Max(1, scorePopupPoolSize);
            scorePopupPrefab.PrewarmPool();
        }

        private void TrySpawnScorePopup(int playerIndex, int amount, Transform sourceTransform)
        {
            if (amount == 0 || scorePopupPrefab == null)
                return;

            Transform popupSource = ResolvePopupSource(playerIndex, sourceTransform);
            Vector3 popupPosition = popupSource != null
                ? popupSource.position + scorePopupOffset
                : scorePopupOffset;

            string signedText = amount > 0 ? $"+{amount}" : amount.ToString();
            DamageNumber popup = popupSource != null
                ? scorePopupPrefab.Spawn(popupPosition, signedText, popupSource)
                : scorePopupPrefab.Spawn(popupPosition, signedText);

            if (popup == null)
                return;

            popup.SetColor(amount > 0 ? positiveScoreColor : negativeScoreColor);
        }

        private Transform ResolvePopupSource(int playerIndex, Transform sourceTransform)
        {
            if (sourceTransform != null)
                return sourceTransform;

            GameObject gameplayRoot = GetActiveGameplayRoot();
            if (gameplayRoot == null)
                return null;

            string slotName = playerIndex == 1 ? "Player1" : "Player2";
            return gameplayRoot.transform.Find(slotName);
        }

        public void StartNewGame()
        {
            currentRound = 1;
            currentLevel = 1;
            p1RoundWins = 0;
            p2RoundWins = 0;
            player1Score = 0;
            player2Score = 0;
            LoadRuntimeConfigDefaults();
            ApplyLevelConfigForCurrentRound();
            timeRemaining = roundDuration;
            SpawnSelectedCharacters();

            if (useStormIntro)
                StartStormIntro();
            else
                StartCountdown();
        }

        public void SetSelectedPlayer1Character(string characterId)
        {
            string normalized = NormalizeCharacterId(characterId);
            if (!string.IsNullOrEmpty(normalized))
                selectedPlayer1CharacterId = normalized;
        }

        public void SetSelectedPlayer2Character(string characterId)
        {
            string normalized = NormalizeCharacterId(characterId);
            if (!string.IsNullOrEmpty(normalized))
                selectedPlayer2CharacterId = normalized;
        }

        private void SpawnSelectedCharacters()
        {
            GameObject gameplayRoot = GetActiveGameplayRoot();
            if (!useCharacterPrefabs || gameplayRoot == null)
                return;

            BattleFieldArena arenaP1 = ResolveArenaForPlayer(gameplayRoot, 1);
            BattleFieldArena arenaP2 = ResolveArenaForPlayer(gameplayRoot, 2);

            SpawnCharacterIntoSlot(
                gameplayRoot,
                slotName: "Player1",
                playerIndex: 1,
                selectedCharacterId: selectedPlayer1CharacterId,
                defaultCharacterId: defaultPlayer1CharacterId,
                spawnOverride: ResolveSpawnOverride(gameplayRoot, 1, player1SpawnOverride, arenaP1),
                arenaInfo: arenaP1,
                forceAIController: false);

            if (IsSingleMode())
            {
                SetPlayerSlotActive(gameplayRoot, "Player2", false);
                return;
            }

            bool isVsAiMode = IsVsAiMode();
            string player2CharacterIdForSpawn = selectedPlayer2CharacterId;
            if (isVsAiMode && randomizeAiCharacterInVsAi)
            {
                string randomAiId = GetRandomAiCharacterId(selectedPlayer1CharacterId);
                if (!string.IsNullOrEmpty(randomAiId))
                {
                    player2CharacterIdForSpawn = randomAiId;
                    selectedPlayer2CharacterId = randomAiId;
                }
            }

            SpawnCharacterIntoSlot(
                gameplayRoot,
                slotName: "Player2",
                playerIndex: 2,
                selectedCharacterId: player2CharacterIdForSpawn,
                defaultCharacterId: defaultPlayer2CharacterId,
                spawnOverride: ResolveSpawnOverride(gameplayRoot, 2, player2SpawnOverride, arenaP2),
                arenaInfo: arenaP2,
                forceAIController: isVsAiMode);

            SetPlayerSlotActive(gameplayRoot, "Player2", true);
        }

        private void SpawnCharacterIntoSlot(
            GameObject gameplayRoot,
            string slotName,
            int playerIndex,
            string selectedCharacterId,
            string defaultCharacterId,
            Transform spawnOverride,
            BattleFieldArena arenaInfo,
            bool forceAIController)
        {
            if (gameplayRoot == null)
                return;

            string characterId = NormalizeCharacterId(selectedCharacterId);
            if (string.IsNullOrEmpty(characterId))
                characterId = NormalizeCharacterId(defaultCharacterId);

            GameObject selectedPrefab = FindCharacterPrefab(characterId, forceAIController);
            if (selectedPrefab == null)
            {
                Debug.LogWarning($"GameManager: No prefab mapped for character '{characterId}'.");
                return;
            }

            Transform existingPlayer = gameplayRoot.transform.Find(slotName);
            Vector3 defaultPosition = playerIndex == 1 ? new Vector3(-2f, -3f, 0f) : new Vector3(2f, -3f, 0f);
            Vector3 spawnPosition = existingPlayer != null ? existingPlayer.position : defaultPosition;
            Quaternion spawnRotation = existingPlayer != null ? existingPlayer.rotation : Quaternion.identity;

            if (spawnOverride != null)
            {
                spawnPosition = spawnOverride.position;
                spawnRotation = spawnOverride.rotation;
            }

            if (existingPlayer != null)
                Destroy(existingPlayer.gameObject);

            Transform parent = playerSpawnParent != null ? playerSpawnParent : gameplayRoot.transform;
            GameObject spawned = Instantiate(selectedPrefab, spawnPosition, spawnRotation, parent);
            spawned.name = slotName;

            ConfigureSpawnedPlayerController(spawned, playerIndex, forceAIController, characterId, arenaInfo);
        }

        private void ConfigureSpawnedPlayerController(GameObject spawned, int playerIndex, bool forceAIController, string characterId, BattleFieldArena arenaInfo)
        {
            if (spawned == null)
                return;

            SpriteRenderer sr = spawned.GetComponentInChildren<SpriteRenderer>();
            Animator animator = spawned.GetComponentInChildren<Animator>();

            PlayerController playerController = spawned.GetComponent<PlayerController>();
            AIPlayerController aiController = spawned.GetComponent<AIPlayerController>();

            if (forceAIController)
            {
                float baseSpeed = playerController != null ? playerController.baseSpeed : 4.5f;
                float minX = playerController != null ? playerController.arenaMinX : -4f;
                float maxX = playerController != null ? playerController.arenaMaxX : 4f;
                float ground = playerController != null ? playerController.groundY : -3f;
                bool horizontalOnly = playerController == null || playerController.horizontalOnly;
                float arenaTop = playerController != null ? playerController.arenaTopY : 4f;
                float flightHeightFraction = playerController != null ? playerController.flightHeightFraction : 0.35f;
                float returnSpeed = playerController != null ? playerController.returnToGroundSpeed : 5f;

                if (arenaInfo != null)
                {
                    minX = arenaInfo.arenaMinX;
                    maxX = arenaInfo.arenaMaxX;
                    ground = arenaInfo.groundY;
                    arenaTop = arenaInfo.arenaTopY;
                }

                if (playerController != null)
                    Destroy(playerController);

                if (aiController == null)
                    aiController = spawned.AddComponent<AIPlayerController>();

                aiController.baseSpeed = baseSpeed;
                aiController.arenaMinX = minX;
                aiController.arenaMaxX = maxX;
                aiController.groundY = ground;
                aiController.horizontalOnly = horizontalOnly;
                aiController.arenaTopY = arenaTop;
                aiController.flightHeightFraction = flightHeightFraction;
                aiController.returnToGroundSpeed = returnSpeed;
                aiController.ApplyArenaBounds(minX, maxX, ground, arenaTop);
                aiController.spriteRenderer = sr;
                aiController.animator = animator;
                aiController.characterData = ResolveCharacterData(characterId);
                aiController.ApplyRuntimeConfig(aiDifficulty);
                return;
            }

            if (aiController != null)
                Destroy(aiController);

            if (playerController == null)
                playerController = spawned.AddComponent<PlayerController>();

            playerController.playerIndex = playerIndex;
            playerController.spriteRenderer = sr;
            playerController.animator = animator;
            if (arenaInfo != null)
                playerController.ApplyArenaBounds(arenaInfo.arenaMinX, arenaInfo.arenaMaxX, arenaInfo.groundY, arenaInfo.arenaTopY);
            playerController.ApplyRuntimeConfig();
        }

        private void SetPlayerSlotActive(GameObject gameplayRoot, string slotName, bool active)
        {
            if (gameplayRoot == null)
                return;

            Transform slot = gameplayRoot.transform.Find(slotName);
            if (slot != null && slot.gameObject.activeSelf != active)
                slot.gameObject.SetActive(active);
        }

        private CharacterData ResolveCharacterData(string characterId)
        {
            if (ConfigManager.Instance == null || ConfigManager.Instance.characterConfig == null)
                return null;

            string normalized = NormalizeCharacterId(characterId);
            if (string.IsNullOrEmpty(normalized))
                return null;

            return ConfigManager.Instance.characterConfig.GetCharacter(normalized);
        }

        private bool IsSingleMode()
        {
            string mode = NormalizeCharacterId(gameMode);
            return mode == "single" || mode == "single_dog";
        }

        private void LoadRuntimeConfigDefaults()
        {
            if (ConfigManager.Instance == null || ConfigManager.Instance.aiDifficultyConfig == null)
                return;

            string defaultDifficulty = ConfigManager.Instance.aiDifficultyConfig.default_difficulty;
            if (!string.IsNullOrWhiteSpace(defaultDifficulty))
                aiDifficulty = NormalizeCharacterId(defaultDifficulty);
        }

        private void ApplyLevelConfigForCurrentRound()
        {
            LevelConfigData levelConfig = ConfigManager.Instance != null
                ? ConfigManager.Instance.GetLevelConfigForRound(currentRound)
                : null;

            if (levelConfig == null)
            {
                currentLevel = Mathf.Max(1, currentRound);
                return;
            }

            currentLevel = levelConfig.level_number > 0
                ? levelConfig.level_number
                : Mathf.Max(1, currentRound);

            if (levelConfig.round_duration_seconds > 0f)
                roundDuration = levelConfig.round_duration_seconds;

            ApplyLevelBackground(levelConfig.background_color);
            ApplyLevelToSnackSpawners(levelConfig);
        }

        private void ApplyLevelBackground(int[] rgb)
        {
            Camera targetCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (targetCamera == null)
                return;

            targetCamera.backgroundColor = RuntimeConfigUtil.ColorFromRgb255(rgb, targetCamera.backgroundColor);
        }

        private void ApplyLevelToSnackSpawners(LevelConfigData levelConfig)
        {
            if (levelConfig == null)
                return;

            GameObject gameplayRoot = GetActiveGameplayRoot();
            SnackSpawner[] spawners = gameplayRoot != null
                ? gameplayRoot.GetComponentsInChildren<SnackSpawner>(true)
                : FindObjectsOfType<SnackSpawner>(true);

            if (spawners == null || spawners.Length == 0)
                return;

            SortSpawnersLeftToRight(spawners);

            SnackSpawner templateSpawner = null;
            for (int i = 0; i < spawners.Length; i++)
            {
                SnackSpawner spawner = spawners[i];
                if (spawner == null)
                    continue;

                spawner.ApplyLevelConfig(levelConfig);
                if (templateSpawner == null)
                    templateSpawner = spawner;
            }

            // In versus modes, keep both sides using the same spawn cadence/capacity.
            if (!IsSingleMode() && templateSpawner != null)
            {
                for (int i = 0; i < spawners.Length; i++)
                {
                    SnackSpawner spawner = spawners[i];
                    if (spawner == null || spawner == templateSpawner)
                        continue;

                    spawner.CopyCoreSettingsFrom(templateSpawner);
                }
            }

            BattleFieldArena arenaP1 = ResolveArenaForPlayer(gameplayRoot, 1);
            BattleFieldArena arenaP2 = ResolveArenaForPlayer(gameplayRoot, 2);
            bool hasDistinctVersusArenas = !IsSingleMode() && arenaP1 != null && arenaP2 != null && arenaP1 != arenaP2;
            bool useCombinedArenaForSharedSpawner = hasDistinctVersusArenas && spawners.Length == 1;

            if (useCombinedArenaForSharedSpawner)
            {
                Debug.LogWarning("GameManager: Versus mode currently has only one SnackSpawner. Add a second spawner so each battlefield side has its own lane spawner.");
            }

            for (int i = 0; i < spawners.Length; i++)
            {
                SnackSpawner spawner = spawners[i];
                if (spawner == null)
                    continue;

                if (useCombinedArenaForSharedSpawner)
                {
                    spawner.ApplyArenaBounds(
                        Mathf.Min(arenaP1.arenaMinX, arenaP2.arenaMinX),
                        Mathf.Max(arenaP1.arenaMaxX, arenaP2.arenaMaxX),
                        Mathf.Max(arenaP1.SnackSpawnY, arenaP2.SnackSpawnY),
                        Mathf.Min(arenaP1.SnackGroundY, arenaP2.SnackGroundY));
                    continue;
                }

                BattleFieldArena arena = ResolveArenaForSpawner(gameplayRoot, spawner, i);
                if (arena == null)
                    continue;

                spawner.ApplyArena(arena);
            }
        }

        private static void SortSpawnersLeftToRight(SnackSpawner[] spawners)
        {
            Array.Sort(spawners, CompareSpawnerX);
        }

        private static int CompareSpawnerX(SnackSpawner a, SnackSpawner b)
        {
            if (ReferenceEquals(a, b))
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            return a.transform.position.x.CompareTo(b.transform.position.x);
        }

        private bool IsVsAiMode()
        {
            string mode = NormalizeCharacterId(gameMode);
            if (mode == "vsai" || mode == "1p")
                return true;

            if (IsSingleMode() || IsTwoPlayerMode())
                return false;

            return vsAI;
        }

        private bool IsTwoPlayerMode()
        {
            return NormalizeCharacterId(gameMode) == "2p";
        }

        private GameObject GetActiveGameplayRoot()
        {
            if (IsSingleMode())
            {
                if (gameplayEntitiesSingleRoot != null)
                    return gameplayEntitiesSingleRoot;

                return gameplayEntitiesRoot;
            }

            if (gameplayEntitiesVersusRoot != null)
                return gameplayEntitiesVersusRoot;

            return gameplayEntitiesRoot;
        }

        private void RefreshGameplayRootActivation(bool shouldBeActive)
        {
            GameObject activeRoot = GetActiveGameplayRoot();

            SetRootActive(gameplayEntitiesRoot, shouldBeActive && activeRoot == gameplayEntitiesRoot);
            SetRootActive(gameplayEntitiesSingleRoot, shouldBeActive && activeRoot == gameplayEntitiesSingleRoot);
            SetRootActive(gameplayEntitiesVersusRoot, shouldBeActive && activeRoot == gameplayEntitiesVersusRoot);
        }

        private static void SetRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
                root.SetActive(active);
        }

        private void AutoResolveGameplayRootsByName()
        {
            if (gameplayEntitiesSingleRoot == null)
                gameplayEntitiesSingleRoot = GameObject.Find("GameplayEntities_Single");

            if (gameplayEntitiesVersusRoot == null)
                gameplayEntitiesVersusRoot = GameObject.Find("GameplayEntities_1vs1");

            if (singleBattleFieldArena == null && gameplayEntitiesSingleRoot != null)
                singleBattleFieldArena = FindArenaByName(gameplayEntitiesSingleRoot, "BattleField");

            if (versusBattleFieldArenaP1 == null && gameplayEntitiesVersusRoot != null)
                versusBattleFieldArenaP1 = FindArenaByName(gameplayEntitiesVersusRoot, "BattleField_1");

            if (versusBattleFieldArenaP2 == null && gameplayEntitiesVersusRoot != null)
                versusBattleFieldArenaP2 = FindArenaByName(gameplayEntitiesVersusRoot, "BattleField_2");
        }

        private BattleFieldArena ResolveArenaForPlayer(GameObject gameplayRoot, int playerIndex)
        {
            if (gameplayRoot == null)
                return null;

            if (IsSingleMode())
            {
                if (singleBattleFieldArena != null)
                    return singleBattleFieldArena;

                BattleFieldArena namedSingle = FindArenaByName(gameplayRoot, "BattleField");
                if (namedSingle != null)
                    return namedSingle;

                return FindArenaByOrder(gameplayRoot, 0);
            }

            if (playerIndex == 1)
            {
                if (versusBattleFieldArenaP1 != null)
                    return versusBattleFieldArenaP1;

                BattleFieldArena namedP1 = FindArenaByName(gameplayRoot, "BattleField_1");
                if (namedP1 != null)
                    return namedP1;

                return FindArenaByOrder(gameplayRoot, 0);
            }

            if (versusBattleFieldArenaP2 != null)
                return versusBattleFieldArenaP2;

            BattleFieldArena namedP2 = FindArenaByName(gameplayRoot, "BattleField_2");
            if (namedP2 != null)
                return namedP2;

            return FindArenaByOrder(gameplayRoot, 1) ?? FindArenaByOrder(gameplayRoot, 0);
        }

        private static BattleFieldArena FindArenaByOrder(GameObject gameplayRoot, int index)
        {
            if (gameplayRoot == null)
                return null;

            BattleFieldArena[] arenas = gameplayRoot.GetComponentsInChildren<BattleFieldArena>(true);
            if (arenas == null || arenas.Length == 0)
                return null;

            int clamped = Mathf.Clamp(index, 0, arenas.Length - 1);
            return arenas[clamped];
        }

        private static BattleFieldArena FindArenaByName(GameObject gameplayRoot, string objectName)
        {
            if (gameplayRoot == null)
                return null;

            Transform t = gameplayRoot.transform.Find(objectName);
            if (t == null)
                return null;

            return t.GetComponent<BattleFieldArena>();
        }

        private Transform ResolveSpawnOverride(GameObject gameplayRoot, int playerIndex, Transform inspectorOverride, BattleFieldArena arenaInfo)
        {
            if (inspectorOverride != null)
                return inspectorOverride;

            if (arenaInfo != null && arenaInfo.playerSpawnPoint != null)
                return arenaInfo.playerSpawnPoint;

            if (gameplayRoot == null)
                return null;

            string fallbackName = playerIndex == 1 ? "trSpawnP1" : "trSpawnP2";
            return gameplayRoot.transform.Find(fallbackName);
        }

        private BattleFieldArena ResolveArenaForSpawner(GameObject gameplayRoot, SnackSpawner spawner, int spawnerIndex)
        {
            if (gameplayRoot == null || spawner == null)
                return null;

            if (IsSingleMode())
                return ResolveArenaForPlayer(gameplayRoot, 1);

            // If the spawner lives under a specific BattleField, use that directly.
            BattleFieldArena parentArena = spawner.GetComponentInParent<BattleFieldArena>();
            if (parentArena != null)
                return parentArena;

            // Fallback: first spawner -> P1 arena, second spawner -> P2 arena.
            return spawnerIndex == 0
                ? ResolveArenaForPlayer(gameplayRoot, 1)
                : ResolveArenaForPlayer(gameplayRoot, 2);
        }

        private string GetRandomAiCharacterId(string player1CharacterId)
        {
            string excludedId = avoidSameCharacterAsPlayer1InVsAi
                ? NormalizeCharacterId(player1CharacterId)
                : string.Empty;

            string pickedFromAiList = PickRandomCharacterIdFromEntries(aiCharacterPrefabs, excludedId);
            if (!string.IsNullOrEmpty(pickedFromAiList))
                return pickedFromAiList;

            if (!string.IsNullOrEmpty(excludedId))
            {
                // Retry without exclusion in case AI list has only one character.
                pickedFromAiList = PickRandomCharacterIdFromEntries(aiCharacterPrefabs, string.Empty);
                if (!string.IsNullOrEmpty(pickedFromAiList))
                    return pickedFromAiList;
            }

            string pickedFallback = PickRandomCharacterIdFromEntries(characterPrefabs, excludedId);
            if (!string.IsNullOrEmpty(pickedFallback))
                return pickedFallback;

            if (!string.IsNullOrEmpty(excludedId))
                return PickRandomCharacterIdFromEntries(characterPrefabs, string.Empty);

            return string.Empty;
        }

        private static string PickRandomCharacterIdFromEntries(List<CharacterPrefabEntry> entries, string excludedId)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            List<string> candidates = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                CharacterPrefabEntry entry = entries[i];
                if (entry == null || entry.prefab == null)
                    continue;

                string normalizedId = NormalizeCharacterId(entry.characterId);
                if (string.IsNullOrEmpty(normalizedId))
                    continue;

                if (!string.IsNullOrEmpty(excludedId) && normalizedId == excludedId)
                    continue;

                candidates.Add(normalizedId);
            }

            if (candidates.Count == 0)
                return string.Empty;

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private GameObject FindCharacterPrefab(string characterId, bool preferAiList)
        {
            string normalizedId = NormalizeCharacterId(characterId);
            if (string.IsNullOrEmpty(normalizedId))
                return null;

            if (preferAiList)
            {
                GameObject aiPrefab = FindCharacterPrefabInList(aiCharacterPrefabs, normalizedId);
                if (aiPrefab != null)
                    return aiPrefab;
            }

            GameObject defaultPrefab = FindCharacterPrefabInList(characterPrefabs, normalizedId);
            if (defaultPrefab != null)
                return defaultPrefab;

            // If not found in main list, try AI list as final fallback.
            return FindCharacterPrefabInList(aiCharacterPrefabs, normalizedId);
        }

        private static GameObject FindCharacterPrefabInList(List<CharacterPrefabEntry> entries, string characterId)
        {
            if (entries == null || entries.Count == 0)
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterPrefabEntry entry = entries[i];
                if (entry == null || entry.prefab == null)
                    continue;

                if (NormalizeCharacterId(entry.characterId) == characterId)
                    return entry.prefab;
            }

            return null;
        }

        private static string NormalizeCharacterId(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return string.Empty;

            return characterId.Trim().ToLowerInvariant();
        }
    }
}
