using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Text;
using SnackAttack.Core;
using SnackAttack.Config;
using DamageNumbersPro;

namespace SnackAttack.Gameplay
{
    public enum GameState
    {
        MainMenu,
        CharacterSelect,
        UploadAvatar,
        Settings,
        StormIntro,
        Countdown,
        Playing,
        GameOver
    }

    public enum CrowdChaosVotingMode
    {
        Treat,
        Action,
        Trivia
    }

    public class GameManager : MonoBehaviour
    {
        [System.Serializable]
        public class CharacterPrefabEntry
        {
            public string characterId;
            public GameObject prefab;
        }

        [System.Serializable]
        public class CrowdChaosVoteOption
        {
            public string id;
            public string label;
            [NonSerialized] public int votes;
        }

        [System.Serializable]
        public class CrowdChaosTriviaPrompt
        {
            [TextArea(2, 3)] public string question;
            public string[] options = new string[4];
            [Range(0, 3)] public int correctOptionIndex = 0;
        }

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }

        public bool CrowdChaosOverlayVisible => crowdChaosCountdownActive || crowdChaosVotingActive || crowdChaosResultTimer > 0f;
        public bool CrowdChaosDangerVisible => crowdChaosCountdownActive || crowdChaosVotingActive;
        public string CrowdChaosTitle => BuildCrowdChaosTitle();
        public string CrowdChaosBody => BuildCrowdChaosBody();
        public string CrowdChaosOptions => crowdChaosVotingActive ? BuildCrowdChaosOptionsText() : string.Empty;
        public string CrowdChaosDangerText => CrowdChaosDangerVisible ? "DANGER: BATTLEFIELD INSTABILITY" : string.Empty;
        public float CrowdChaosTintAlpha => ComputeCrowdChaosTintAlpha();

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
        public Vector2 stormIntroLeftStartPosition = new Vector2(-10f, -7.5f);
        public Vector2 stormIntroRightStartPosition = new Vector2(10f, -7.5f);
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
        [Tooltip("Template prefab used to spawn runtime-generated custom characters. If empty, falls back to first mapped character prefab.")]
        public GameObject runtimeCharacterTemplatePrefab;
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

        [Header("Crowd Chaos")]
        public bool enableCrowdChaos = true;
        [Min(0f)] public float crowdChaosTriggerTimeSeconds = 35f;
        [Min(1f)] public float crowdChaosCountdownSeconds = 5f;
        [Min(1f)] public float crowdChaosVotingWindowSeconds = 10f;
        [Min(0f)] public float crowdChaosResultDurationSeconds = 3f;
        [Min(0f)] public float crowdChaosActionEffectDuration = 7f;
        [Range(0.2f, 1f)] public float crowdChaosYankWidthScale = 0.45f;
        [Min(1)] public int crowdChaosTreatBurstCount = 12;
        [Range(1f, 3f)] public float crowdChaosTreatScaleMultiplier = 1.35f;
        public bool enableKeyboardDebugVotes = true;
        public KeyCode voteOption1Key = KeyCode.Alpha1;
        public KeyCode voteOption2Key = KeyCode.Alpha2;
        public KeyCode voteOption3Key = KeyCode.Alpha3;
        public KeyCode voteOption4Key = KeyCode.Alpha4;

        [Header("Crowd Chaos Trivia")]
        public List<CrowdChaosTriviaPrompt> crowdChaosTriviaPrompts = new List<CrowdChaosTriviaPrompt>();
        public int crowdChaosTriviaCorrectBonusScore = 250;
        public int crowdChaosTriviaWrongPenaltyScore = -100;

        [Header("Crowd Chaos Visuals")]
        [Range(0f, 1f)] public float crowdChaosTintMaxAlpha = 0.35f;
        [Min(0f)] public float crowdChaosTintPulseSpeed = 5f;
        [Min(0f)] public float crowdChaosShakeIntensity = 0.12f;
        [Min(0f)] public float crowdChaosShakeDuration = 0.8f;

        private Coroutine stormIntroRoutine;
        private static readonly int IntroIsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly string[] CrowdChaosPreferredTreatIds = { "red_bull", "steak", "spicy_pepper", "broccoli", "bone" };

        private readonly List<CrowdChaosVoteOption> crowdChaosVoteOptions = new List<CrowdChaosVoteOption>(4);
        private readonly Dictionary<string, string> crowdChaosVotesByVoter = new Dictionary<string, string>();
        private bool crowdChaosTriggeredThisRound;
        private bool crowdChaosCountdownActive;
        private float crowdChaosCountdownRemaining;
        private bool crowdChaosVotingActive;
        private float crowdChaosVotingRemaining;
        private CrowdChaosVotingMode crowdChaosVotingMode = CrowdChaosVotingMode.Treat;
        private string crowdChaosTriviaQuestion = string.Empty;
        private string crowdChaosTriviaCorrectOptionId = string.Empty;
        private string crowdChaosResultTitle = string.Empty;
        private string crowdChaosResultBody = string.Empty;
        private float crowdChaosResultTimer;
        private bool crowdChaosActionEffectActive;
        private bool crowdChaosActionUnleashed;
        private float crowdChaosActionEffectRemaining;

        private GameObject shakeGameplayRoot;
        private Vector3 shakeGameplayRootBasePosition;
        private float shakeTimeRemaining;
        private float shakeCurrentIntensity;

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

            string path = Application.persistentDataPath;
            Debug.Log(path);
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
            EnsureDefaultCrowdChaosTriviaPrompts();
            ResetCrowdChaosForNewRound();
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

            UpdateBattlefieldShake(Time.deltaTime);
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

            UpdateCrowdChaos(Time.deltaTime);

            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                EndRound();
            }
        }

        private void EndRound()
        {
            ClearCrowdChaosRuntime(resetMovementBounds: true, resetRoundTrigger: true);

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
            ResetCrowdChaosForNewRound();

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
            bool hasMoves = moves.Count > 0;
            if (hasMoves)
                SetStormIntroRunAnimation(moves, true);

            float duration = Mathf.Max(0f, stormIntroDuration);

            if (duration > 0f && hasMoves)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (State != GameState.StormIntro)
                    {
                        SetStormIntroRunAnimation(moves, false);
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

            if (hasMoves)
                SetStormIntroRunAnimation(moves, false);

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

            Vector2 configuredStart = fromLeft
                ? stormIntroLeftStartPosition
                : stormIntroRightStartPosition;

            Vector3 start = new Vector3(configuredStart.x, configuredStart.y, target.z);

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

        private void SetStormIntroRunAnimation(List<StormIntroMoveData> moves, bool isRunning)
        {
            if (moves == null)
                return;

            for (int i = 0; i < moves.Count; i++)
                SetStormIntroRunState(moves[i].transform, isRunning);
        }

        private void SetStormIntroRunStateForCurrentPlayers(bool isRunning)
        {
            GameObject gameplayRoot = GetActiveGameplayRoot();
            SetStormIntroRunState(ResolveSpawnedPlayerTransform(gameplayRoot, "Player1"), isRunning);
            SetStormIntroRunState(ResolveSpawnedPlayerTransform(gameplayRoot, "Player2"), isRunning);
        }

        private static void SetStormIntroRunState(Transform playerTransform, bool isRunning)
        {
            if (playerTransform == null)
                return;

            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            if (playerController != null)
                playerController.SetStormIntroRunOverride(isRunning);

            AIPlayerController aiController = playerTransform.GetComponent<AIPlayerController>();
            if (aiController != null)
                aiController.SetStormIntroRunOverride(isRunning);

            Animator animator = playerTransform.GetComponent<Animator>();
            if (animator == null)
                animator = playerTransform.GetComponentInChildren<Animator>();

            if (animator != null && AnimatorHasBoolParameter(animator, IntroIsMovingHash))
                animator.SetBool(IntroIsMovingHash, isRunning);
        }

        private static bool AnimatorHasBoolParameter(Animator animator, int nameHash)
        {
            if (animator == null)
                return false;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == nameHash)
                    return true;
            }

            return false;
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

        public bool SubmitCrowdChaosVote(string optionId, string voterId = null)
        {
            return RegisterCrowdChaosVote(optionId, voterId);
        }

        private void UpdateCrowdChaos(float dt)
        {
            if (crowdChaosResultTimer > 0f)
                crowdChaosResultTimer = Mathf.Max(0f, crowdChaosResultTimer - dt);

            UpdateCrowdChaosActionEffect(dt);

            if (!enableCrowdChaos)
                return;

            if (crowdChaosVotingActive)
            {
                crowdChaosVotingRemaining -= dt;
                HandleKeyboardDebugVotes();

                if (crowdChaosVotingRemaining <= 0f)
                    FinalizeCrowdChaosVote();

                return;
            }

            if (crowdChaosCountdownActive)
            {
                crowdChaosCountdownRemaining -= dt;
                if (crowdChaosCountdownRemaining <= 0f)
                    ActivateCrowdChaosVoting();

                return;
            }

            if (crowdChaosTriggeredThisRound)
                return;

            float elapsedRoundTime = Mathf.Max(0f, roundDuration - timeRemaining);
            float minTimeRequired = crowdChaosCountdownSeconds + 1f;
            if (elapsedRoundTime >= crowdChaosTriggerTimeSeconds && timeRemaining > minTimeRequired)
                StartCrowdChaosCountdown();
        }

        private void StartCrowdChaosCountdown()
        {
            crowdChaosTriggeredThisRound = true;
            crowdChaosCountdownActive = true;
            crowdChaosCountdownRemaining = Mathf.Max(1f, crowdChaosCountdownSeconds);
            crowdChaosResultTimer = 0f;
            crowdChaosResultTitle = string.Empty;
            crowdChaosResultBody = string.Empty;

            StartBattlefieldShake(crowdChaosShakeIntensity * 0.75f, Mathf.Max(0.25f, crowdChaosShakeDuration));
            EventManager.TriggerEvent("PLAY_SOUND", "countdown_1");
        }

        private void ActivateCrowdChaosVoting()
        {
            crowdChaosCountdownActive = false;
            crowdChaosVotingActive = true;
            crowdChaosVotingRemaining = Mathf.Max(1f, crowdChaosVotingWindowSeconds);
            crowdChaosVotingMode = ResolveCrowdChaosVotingModeForRound(currentRound);
            crowdChaosVotesByVoter.Clear();

            BuildCrowdChaosVoteOptions();
            StartBattlefieldShake(crowdChaosShakeIntensity, Mathf.Max(crowdChaosVotingWindowSeconds, crowdChaosShakeDuration));
        }

        private CrowdChaosVotingMode ResolveCrowdChaosVotingModeForRound(int roundNumber)
        {
            int cycleIndex = Mathf.Abs(roundNumber - 1) % 3;
            switch (cycleIndex)
            {
                case 0:
                    return CrowdChaosVotingMode.Treat;
                case 1:
                    return CrowdChaosVotingMode.Action;
                default:
                    return CrowdChaosVotingMode.Trivia;
            }
        }

        private void BuildCrowdChaosVoteOptions()
        {
            crowdChaosVoteOptions.Clear();
            crowdChaosTriviaQuestion = string.Empty;
            crowdChaosTriviaCorrectOptionId = string.Empty;

            switch (crowdChaosVotingMode)
            {
                case CrowdChaosVotingMode.Action:
                    AddCrowdChaosVoteOption("unleashed", "UNLEASHED");
                    AddCrowdChaosVoteOption("yanked", "YANKED");
                    break;
                case CrowdChaosVotingMode.Treat:
                    List<string> treatIds = BuildTreatVoteOptionIds();
                    for (int i = 0; i < treatIds.Count; i++)
                    {
                        if (TryGetSnackDataById(treatIds[i], out SnackData snackData))
                            AddCrowdChaosVoteOption(treatIds[i], snackData.snackName.ToUpperInvariant());
                    }
                    break;
                case CrowdChaosVotingMode.Trivia:
                    BuildTriviaVoteOptions();
                    break;
            }

            if (crowdChaosVoteOptions.Count == 0)
            {
                AddCrowdChaosVoteOption("unleashed", "UNLEASHED");
                AddCrowdChaosVoteOption("yanked", "YANKED");
                crowdChaosVotingMode = CrowdChaosVotingMode.Action;
            }
        }

        private void AddCrowdChaosVoteOption(string id, string label)
        {
            string normalizedId = NormalizeVoteValue(id);
            if (string.IsNullOrEmpty(normalizedId))
                return;

            crowdChaosVoteOptions.Add(new CrowdChaosVoteOption
            {
                id = normalizedId,
                label = string.IsNullOrWhiteSpace(label) ? normalizedId.ToUpperInvariant() : label,
                votes = 0,
            });
        }

        private List<string> BuildTreatVoteOptionIds()
        {
            List<string> selected = new List<string>(4);
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < CrowdChaosPreferredTreatIds.Length; i++)
            {
                string candidate = NormalizeVoteValue(CrowdChaosPreferredTreatIds[i]);
                if (string.IsNullOrEmpty(candidate) || seen.Contains(candidate))
                    continue;

                if (!TryGetSnackDataById(candidate, out SnackData _))
                    continue;

                seen.Add(candidate);
                selected.Add(candidate);
                if (selected.Count >= 4)
                    return selected;
            }

            LevelConfigData levelConfig = ConfigManager.Instance != null
                ? ConfigManager.Instance.GetLevelConfigForRound(currentRound)
                : null;

            if (levelConfig != null && levelConfig.snack_pool != null)
            {
                for (int i = 0; i < levelConfig.snack_pool.Length; i++)
                {
                    string candidate = NormalizeVoteValue(levelConfig.snack_pool[i]);
                    if (string.IsNullOrEmpty(candidate) || seen.Contains(candidate))
                        continue;

                    if (!TryGetSnackDataById(candidate, out SnackData _))
                        continue;

                    seen.Add(candidate);
                    selected.Add(candidate);
                    if (selected.Count >= 4)
                        return selected;
                }
            }

            if (ConfigManager.Instance != null && ConfigManager.Instance.snackConfig != null && ConfigManager.Instance.snackConfig.snacks != null)
            {
                List<SnackData> snacks = ConfigManager.Instance.snackConfig.snacks;
                for (int i = 0; i < snacks.Count; i++)
                {
                    SnackData snack = snacks[i];
                    if (snack == null)
                        continue;

                    string candidate = NormalizeVoteValue(snack.snackId);
                    if (string.IsNullOrEmpty(candidate) || seen.Contains(candidate))
                        continue;

                    seen.Add(candidate);
                    selected.Add(candidate);
                    if (selected.Count >= 4)
                        return selected;
                }
            }

            return selected;
        }

        private void BuildTriviaVoteOptions()
        {
            EnsureDefaultCrowdChaosTriviaPrompts();
            if (crowdChaosTriviaPrompts == null || crowdChaosTriviaPrompts.Count == 0)
                return;

            int index = UnityEngine.Random.Range(0, crowdChaosTriviaPrompts.Count);
            CrowdChaosTriviaPrompt prompt = crowdChaosTriviaPrompts[index];
            if (prompt == null || prompt.options == null || prompt.options.Length < 4)
                return;

            crowdChaosTriviaQuestion = string.IsNullOrWhiteSpace(prompt.question)
                ? "TRIVIA: PICK THE CORRECT ANSWER"
                : prompt.question.Trim();

            const string optionPrefix = "ABCD";
            int safeCorrectIndex = Mathf.Clamp(prompt.correctOptionIndex, 0, 3);

            for (int i = 0; i < 4; i++)
            {
                char key = optionPrefix[i];
                string optionLabel = string.IsNullOrWhiteSpace(prompt.options[i])
                    ? "..."
                    : prompt.options[i].Trim();

                string optionId = key.ToString().ToLowerInvariant();
                AddCrowdChaosVoteOption(optionId, $"{key}. {optionLabel}");

                if (i == safeCorrectIndex)
                    crowdChaosTriviaCorrectOptionId = optionId;
            }
        }

        private bool RegisterCrowdChaosVote(string optionId, string voterId)
        {
            if (!crowdChaosVotingActive)
                return false;

            if (!TryResolveCrowdChaosOptionId(optionId, out string resolvedOptionId))
                return false;

            if (!string.IsNullOrWhiteSpace(voterId))
            {
                string voterKey = NormalizeVoteValue(voterId);
                if (!string.IsNullOrEmpty(voterKey) && crowdChaosVotesByVoter.TryGetValue(voterKey, out string previousChoice))
                {
                    if (previousChoice == resolvedOptionId)
                        return true;

                    AdjustCrowdChaosVoteCount(previousChoice, -1);
                }

                if (!string.IsNullOrEmpty(voterKey))
                    crowdChaosVotesByVoter[voterKey] = resolvedOptionId;
            }

            AdjustCrowdChaosVoteCount(resolvedOptionId, 1);
            return true;
        }

        private bool TryResolveCrowdChaosOptionId(string rawOption, out string resolvedOptionId)
        {
            resolvedOptionId = string.Empty;
            string normalized = NormalizeVoteValue(rawOption);
            if (string.IsNullOrEmpty(normalized) || crowdChaosVoteOptions.Count == 0)
                return false;

            if (int.TryParse(normalized, out int oneBasedIndex))
            {
                int idx = oneBasedIndex - 1;
                if (idx >= 0 && idx < crowdChaosVoteOptions.Count)
                {
                    resolvedOptionId = crowdChaosVoteOptions[idx].id;
                    return true;
                }
            }

            for (int i = 0; i < crowdChaosVoteOptions.Count; i++)
            {
                CrowdChaosVoteOption option = crowdChaosVoteOptions[i];
                if (option == null)
                    continue;

                if (option.id == normalized || NormalizeVoteValue(option.label) == normalized)
                {
                    resolvedOptionId = option.id;
                    return true;
                }
            }

            return false;
        }

        private void AdjustCrowdChaosVoteCount(string optionId, int delta)
        {
            if (delta == 0)
                return;

            for (int i = 0; i < crowdChaosVoteOptions.Count; i++)
            {
                CrowdChaosVoteOption option = crowdChaosVoteOptions[i];
                if (option == null || option.id != optionId)
                    continue;

                option.votes = Mathf.Max(0, option.votes + delta);
                return;
            }
        }

        private void HandleKeyboardDebugVotes()
        {
            if (!enableKeyboardDebugVotes || !crowdChaosVotingActive)
                return;

            if (Input.GetKeyDown(voteOption1Key))
                RegisterCrowdChaosVoteByIndex(0);

            if (Input.GetKeyDown(voteOption2Key))
                RegisterCrowdChaosVoteByIndex(1);

            if (Input.GetKeyDown(voteOption3Key))
                RegisterCrowdChaosVoteByIndex(2);

            if (Input.GetKeyDown(voteOption4Key))
                RegisterCrowdChaosVoteByIndex(3);
        }

        private void RegisterCrowdChaosVoteByIndex(int index)
        {
            if (index < 0 || index >= crowdChaosVoteOptions.Count)
                return;

            CrowdChaosVoteOption option = crowdChaosVoteOptions[index];
            if (option == null)
                return;

            RegisterCrowdChaosVote(option.id, null);
        }

        private void FinalizeCrowdChaosVote()
        {
            crowdChaosVotingActive = false;
            crowdChaosVotingRemaining = 0f;

            CrowdChaosVoteOption winner = ResolveCrowdChaosVoteWinner();
            if (winner == null)
            {
                crowdChaosResultTitle = "CROWD CHAOS";
                crowdChaosResultBody = "No votes were registered.";
                crowdChaosResultTimer = crowdChaosResultDurationSeconds;
                crowdChaosVoteOptions.Clear();
                crowdChaosVotesByVoter.Clear();
                return;
            }

            crowdChaosResultTitle = $"{crowdChaosVotingMode.ToString().ToUpperInvariant()} WINNER: {winner.label}";
            crowdChaosResultBody = BuildCrowdChaosResultMessage(winner);
            crowdChaosResultTimer = crowdChaosResultDurationSeconds;

            crowdChaosVoteOptions.Clear();
            crowdChaosVotesByVoter.Clear();

            StartBattlefieldShake(crowdChaosShakeIntensity * 1.2f, Mathf.Max(0.2f, crowdChaosShakeDuration));
        }

        private CrowdChaosVoteOption ResolveCrowdChaosVoteWinner()
        {
            if (crowdChaosVoteOptions.Count == 0)
                return null;

            int bestVotes = int.MinValue;
            List<CrowdChaosVoteOption> leaders = new List<CrowdChaosVoteOption>();

            for (int i = 0; i < crowdChaosVoteOptions.Count; i++)
            {
                CrowdChaosVoteOption option = crowdChaosVoteOptions[i];
                if (option == null)
                    continue;

                if (option.votes > bestVotes)
                {
                    bestVotes = option.votes;
                    leaders.Clear();
                    leaders.Add(option);
                }
                else if (option.votes == bestVotes)
                {
                    leaders.Add(option);
                }
            }

            if (leaders.Count == 0)
                return crowdChaosVoteOptions[UnityEngine.Random.Range(0, crowdChaosVoteOptions.Count)];

            if (bestVotes <= 0)
                return crowdChaosVoteOptions[UnityEngine.Random.Range(0, crowdChaosVoteOptions.Count)];

            return leaders[UnityEngine.Random.Range(0, leaders.Count)];
        }

        private string BuildCrowdChaosResultMessage(CrowdChaosVoteOption winner)
        {
            if (winner == null)
                return "No crowd effect was applied.";

            switch (crowdChaosVotingMode)
            {
                case CrowdChaosVotingMode.Action:
                {
                    bool unleashed = winner.id == "unleashed";
                    StartCrowdChaosActionEffect(unleashed);
                    return unleashed
                        ? "UNLEASHED: Players can cross both sides for a short time."
                        : "YANKED: Movement is restricted in each battlefield.";
                }
                case CrowdChaosVotingMode.Treat:
                {
                    SpawnCrowdChaosTreatBurst(winner.id);
                    return $"{winner.label} storm incoming.";
                }
                case CrowdChaosVotingMode.Trivia:
                {
                    bool answeredCorrectly = !string.IsNullOrEmpty(crowdChaosTriviaCorrectOptionId) && winner.id == crowdChaosTriviaCorrectOptionId;
                    int scoreDelta = answeredCorrectly ? crowdChaosTriviaCorrectBonusScore : crowdChaosTriviaWrongPenaltyScore;
                    if (scoreDelta != 0)
                    {
                        AddScore(1, scoreDelta);
                        if (!IsSingleMode())
                            AddScore(2, scoreDelta);
                    }

                    return answeredCorrectly
                        ? (IsSingleMode()
                            ? $"Correct answer. +{crowdChaosTriviaCorrectBonusScore} points."
                            : $"Correct answer. Both players receive {crowdChaosTriviaCorrectBonusScore} points.")
                        : (IsSingleMode()
                            ? $"Wrong answer. {crowdChaosTriviaWrongPenaltyScore} points."
                            : $"Wrong answer. Both players receive {crowdChaosTriviaWrongPenaltyScore} points.");
                }
                default:
                    return winner.label;
            }
        }

        private void StartCrowdChaosActionEffect(bool unleashed)
        {
            crowdChaosActionEffectActive = true;
            crowdChaosActionUnleashed = unleashed;
            crowdChaosActionEffectRemaining = Mathf.Max(0f, crowdChaosActionEffectDuration);

            ApplyCrowdChaosActionBounds();
            StartBattlefieldShake(crowdChaosShakeIntensity * 1.1f, Mathf.Max(crowdChaosShakeDuration, 0.25f));
        }

        private void UpdateCrowdChaosActionEffect(float dt)
        {
            if (!crowdChaosActionEffectActive)
                return;

            crowdChaosActionEffectRemaining -= dt;
            if (crowdChaosActionEffectRemaining <= 0f)
                StopCrowdChaosActionEffect(resetMovementBounds: true);
        }

        private void StopCrowdChaosActionEffect(bool resetMovementBounds)
        {
            bool wasActive = crowdChaosActionEffectActive;

            crowdChaosActionEffectActive = false;
            crowdChaosActionUnleashed = false;
            crowdChaosActionEffectRemaining = 0f;

            if (resetMovementBounds && wasActive)
                ReapplyDefaultMovementBoundsForCurrentPlayers();
        }

        private void ApplyCrowdChaosActionBounds()
        {
            if (!crowdChaosActionEffectActive)
                return;

            GameObject gameplayRoot = GetActiveGameplayRoot();
            if (gameplayRoot == null)
                return;

            Transform player1 = ResolveSpawnedPlayerTransform(gameplayRoot, "Player1");
            Transform player2 = ResolveSpawnedPlayerTransform(gameplayRoot, "Player2");

            BattleFieldArena arenaP1 = ResolveArenaForPlayer(gameplayRoot, 1);
            BattleFieldArena arenaP2 = ResolveArenaForPlayer(gameplayRoot, 2);

            if (crowdChaosActionUnleashed)
            {
                if (!IsSingleMode() && arenaP1 != null && arenaP2 != null && arenaP1 != arenaP2)
                {
                    float minX = Mathf.Min(arenaP1.arenaMinX, arenaP2.arenaMinX);
                    float maxX = Mathf.Max(arenaP1.arenaMaxX, arenaP2.arenaMaxX);
                    float ground = Mathf.Min(arenaP1.groundY, arenaP2.groundY);
                    float top = Mathf.Max(arenaP1.arenaTopY, arenaP2.arenaTopY);

                    ApplyBoundsToPlayer(player1, minX, maxX, ground, top);
                    ApplyBoundsToPlayer(player2, minX, maxX, ground, top);
                    return;
                }

                ReapplyDefaultMovementBoundsForCurrentPlayers();
                return;
            }

            ApplyYankedBounds(player1, arenaP1);
            if (!IsSingleMode())
                ApplyYankedBounds(player2, arenaP2);
        }

        private void ApplyYankedBounds(Transform playerTransform, BattleFieldArena arena)
        {
            if (playerTransform == null || arena == null)
                return;

            float width = Mathf.Max(0.35f, Mathf.Abs(arena.arenaMaxX - arena.arenaMinX) * Mathf.Clamp(crowdChaosYankWidthScale, 0.2f, 1f));
            float center = (arena.arenaMinX + arena.arenaMaxX) * 0.5f;
            float halfWidth = width * 0.5f;

            ApplyBoundsToPlayer(playerTransform, center - halfWidth, center + halfWidth, arena.groundY, arena.arenaTopY);
        }

        private void ReapplyDefaultMovementBoundsForCurrentPlayers()
        {
            GameObject gameplayRoot = GetActiveGameplayRoot();
            if (gameplayRoot == null)
                return;

            Transform player1 = ResolveSpawnedPlayerTransform(gameplayRoot, "Player1");
            Transform player2 = ResolveSpawnedPlayerTransform(gameplayRoot, "Player2");

            BattleFieldArena arenaP1 = ResolveArenaForPlayer(gameplayRoot, 1);
            BattleFieldArena arenaP2 = ResolveArenaForPlayer(gameplayRoot, 2);

            if (arenaP1 != null)
                ApplyBoundsToPlayer(player1, arenaP1.arenaMinX, arenaP1.arenaMaxX, arenaP1.groundY, arenaP1.arenaTopY);

            if (!IsSingleMode() && arenaP2 != null)
                ApplyBoundsToPlayer(player2, arenaP2.arenaMinX, arenaP2.arenaMaxX, arenaP2.groundY, arenaP2.arenaTopY);
        }

        private static void ApplyBoundsToPlayer(Transform playerTransform, float minX, float maxX, float groundY, float topY)
        {
            if (playerTransform == null)
                return;

            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            if (playerController != null)
                playerController.ApplyArenaBounds(minX, maxX, groundY, topY);

            AIPlayerController aiController = playerTransform.GetComponent<AIPlayerController>();
            if (aiController != null)
                aiController.ApplyArenaBounds(minX, maxX, groundY, topY);
        }

        private void SpawnCrowdChaosTreatBurst(string snackId)
        {
            GameObject gameplayRoot = GetActiveGameplayRoot();
            SnackSpawner[] spawners = gameplayRoot != null
                ? gameplayRoot.GetComponentsInChildren<SnackSpawner>(true)
                : FindObjectsOfType<SnackSpawner>(true);

            if (spawners == null || spawners.Length == 0)
                return;

            int targetBurst = Mathf.Max(1, crowdChaosTreatBurstCount);
            int perSpawner = Mathf.Max(1, Mathf.CeilToInt((float)targetBurst / spawners.Length));

            for (int i = 0; i < spawners.Length; i++)
            {
                SnackSpawner spawner = spawners[i];
                if (spawner == null)
                    continue;

                spawner.SpawnSpecificSnack(snackId, perSpawner, crowdChaosTreatScaleMultiplier);
            }
        }

        private string BuildCrowdChaosTitle()
        {
            if (crowdChaosCountdownActive)
                return $"CROWD CHAOS IN {Mathf.CeilToInt(crowdChaosCountdownRemaining)}";

            if (crowdChaosVotingActive)
                return "CROWD CHAOS LIVE";

            if (crowdChaosResultTimer > 0f)
                return crowdChaosResultTitle;

            return string.Empty;
        }

        private string BuildCrowdChaosBody()
        {
            if (crowdChaosCountdownActive)
                return "Prepare for battlefield instability.";

            if (crowdChaosVotingActive)
            {
                switch (crowdChaosVotingMode)
                {
                    case CrowdChaosVotingMode.Action:
                        return "Vote the action effect now.";
                    case CrowdChaosVotingMode.Treat:
                        return "Vote the snack storm type.";
                    case CrowdChaosVotingMode.Trivia:
                        return string.IsNullOrWhiteSpace(crowdChaosTriviaQuestion)
                            ? "TRIVIA: Pick the correct answer."
                            : crowdChaosTriviaQuestion;
                }
            }

            if (crowdChaosResultTimer > 0f)
                return crowdChaosResultBody;

            return string.Empty;
        }

        private string BuildCrowdChaosOptionsText()
        {
            if (crowdChaosVoteOptions.Count == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder(96);
            for (int i = 0; i < crowdChaosVoteOptions.Count; i++)
            {
                CrowdChaosVoteOption option = crowdChaosVoteOptions[i];
                if (option == null)
                    continue;

                if (builder.Length > 0)
                    builder.Append('\n');

                builder.Append(i + 1);
                builder.Append(") ");
                builder.Append(option.label);
                builder.Append(" [");
                builder.Append(option.votes);
                builder.Append(']');
            }

            return builder.ToString();
        }

        private float ComputeCrowdChaosTintAlpha()
        {
            if (!CrowdChaosDangerVisible)
                return 0f;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, crowdChaosTintPulseSpeed));
            return Mathf.Clamp01(crowdChaosTintMaxAlpha * pulse);
        }

        private void StartBattlefieldShake(float intensity, float duration)
        {
            if (duration <= 0f || intensity <= 0f)
                return;

            GameObject activeRoot = GetActiveGameplayRoot();
            if (activeRoot == null)
                return;

            if (shakeGameplayRoot != activeRoot)
            {
                ResetBattlefieldShake();
                shakeGameplayRoot = activeRoot;
                shakeGameplayRootBasePosition = activeRoot.transform.localPosition;
            }
            else if (shakeTimeRemaining <= 0f)
            {
                shakeGameplayRootBasePosition = activeRoot.transform.localPosition;
            }

            shakeCurrentIntensity = Mathf.Max(shakeCurrentIntensity, intensity);
            shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
        }

        private void UpdateBattlefieldShake(float dt)
        {
            if (shakeGameplayRoot == null)
            {
                shakeTimeRemaining = 0f;
                shakeCurrentIntensity = 0f;
                return;
            }

            if (shakeTimeRemaining <= 0f)
            {
                ResetBattlefieldShake();
                return;
            }

            shakeTimeRemaining -= dt;

            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * shakeCurrentIntensity;
            shakeGameplayRoot.transform.localPosition = shakeGameplayRootBasePosition + new Vector3(randomOffset.x, randomOffset.y, 0f);

            if (shakeTimeRemaining <= 0f)
                ResetBattlefieldShake();
        }

        private void ResetBattlefieldShake()
        {
            if (shakeGameplayRoot != null)
                shakeGameplayRoot.transform.localPosition = shakeGameplayRootBasePosition;

            shakeGameplayRoot = null;
            shakeCurrentIntensity = 0f;
            shakeTimeRemaining = 0f;
        }

        private void ResetCrowdChaosForNewRound()
        {
            ClearCrowdChaosRuntime(resetMovementBounds: true, resetRoundTrigger: true);
        }

        private void ClearCrowdChaosRuntime(bool resetMovementBounds, bool resetRoundTrigger)
        {
            crowdChaosCountdownActive = false;
            crowdChaosCountdownRemaining = 0f;
            crowdChaosVotingActive = false;
            crowdChaosVotingRemaining = 0f;
            crowdChaosVoteOptions.Clear();
            crowdChaosVotesByVoter.Clear();
            crowdChaosResultTitle = string.Empty;
            crowdChaosResultBody = string.Empty;
            crowdChaosResultTimer = 0f;
            crowdChaosTriviaQuestion = string.Empty;
            crowdChaosTriviaCorrectOptionId = string.Empty;

            StopCrowdChaosActionEffect(resetMovementBounds);
            ResetBattlefieldShake();

            if (resetRoundTrigger)
                crowdChaosTriggeredThisRound = false;
        }

        private static string NormalizeVoteValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToLowerInvariant();
        }

        private bool TryGetSnackDataById(string snackId, out SnackData snackData)
        {
            snackData = null;
            if (ConfigManager.Instance == null || ConfigManager.Instance.snackConfig == null || ConfigManager.Instance.snackConfig.snacks == null)
                return false;

            string normalized = NormalizeVoteValue(snackId);
            if (string.IsNullOrEmpty(normalized))
                return false;

            List<SnackData> snacks = ConfigManager.Instance.snackConfig.snacks;
            for (int i = 0; i < snacks.Count; i++)
            {
                SnackData candidate = snacks[i];
                if (candidate == null)
                    continue;

                if (NormalizeVoteValue(candidate.snackId) != normalized)
                    continue;

                snackData = candidate;
                return true;
            }

            return false;
        }

        private void EnsureDefaultCrowdChaosTriviaPrompts()
        {
            if (crowdChaosTriviaPrompts != null && crowdChaosTriviaPrompts.Count > 0)
                return;

            crowdChaosTriviaPrompts = new List<CrowdChaosTriviaPrompt>
            {
                new CrowdChaosTriviaPrompt
                {
                    question = "TRIVIA: Which snack grants invincibility?",
                    options = new[] { "Bone", "Steak", "Broccoli", "Spicy Pepper" },
                    correctOptionIndex = 1,
                },
                new CrowdChaosTriviaPrompt
                {
                    question = "TRIVIA: Which effect flips controls?",
                    options = new[] { "Chaos", "Boost", "Slow", "Invincibility" },
                    correctOptionIndex = 0,
                },
                new CrowdChaosTriviaPrompt
                {
                    question = "TRIVIA: Which snack usually has a negative score?",
                    options = new[] { "Red Bull", "Steak", "Broccoli", "Bone" },
                    correctOptionIndex = 2,
                },
            };
        }

        public void ChangeState(GameState newState)
        {
            if (newState != GameState.StormIntro && stormIntroRoutine != null)
            {
                StopCoroutine(stormIntroRoutine);
                stormIntroRoutine = null;
            }

            if (newState != GameState.StormIntro)
                SetStormIntroRunStateForCurrentPlayers(false);

            if (newState != GameState.Playing)
                ClearCrowdChaosRuntime(resetMovementBounds: true, resetRoundTrigger: true);

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
            ResetCrowdChaosForNewRound();
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
            RuntimeCharacterRegistry.EnsureLoadedFromDisk();

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

            bool isRuntimeCharacter = RuntimeCharacterRegistry.TryGetOrLoad(characterId, out RuntimeCharacterDefinition runtimeDefinition);
            GameObject selectedPrefab = isRuntimeCharacter
                ? ResolveRuntimeCharacterTemplatePrefab(forceAIController)
                : FindCharacterPrefab(characterId, forceAIController);

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

            ConfigureSpawnedPlayerController(spawned, playerIndex, forceAIController, characterId, arenaInfo, runtimeDefinition);
        }

        private void ConfigureSpawnedPlayerController(
            GameObject spawned,
            int playerIndex,
            bool forceAIController,
            string characterId,
            BattleFieldArena arenaInfo,
            RuntimeCharacterDefinition runtimeDefinition)
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
                aiController.runtimeAnimationPlayer = SetupRuntimeAnimationPlayer(spawned, sr, runtimeDefinition, animator);
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
            playerController.runtimeAnimationPlayer = SetupRuntimeAnimationPlayer(spawned, sr, runtimeDefinition, animator);
            if (arenaInfo != null)
                playerController.ApplyArenaBounds(arenaInfo.arenaMinX, arenaInfo.arenaMaxX, arenaInfo.groundY, arenaInfo.arenaTopY);
            playerController.ApplyRuntimeConfig();
        }

        private RuntimeSpriteAnimationPlayer SetupRuntimeAnimationPlayer(
            GameObject spawned,
            SpriteRenderer spriteRenderer,
            RuntimeCharacterDefinition runtimeDefinition,
            Animator animator)
        {
            if (spawned == null)
                return null;

            RuntimeSpriteAnimationPlayer runtimePlayer = spawned.GetComponent<RuntimeSpriteAnimationPlayer>();

            if (runtimeDefinition == null)
            {
                if (runtimePlayer != null)
                    runtimePlayer.enabled = false;

                if (animator != null)
                    animator.enabled = true;

                return null;
            }

            if (runtimePlayer == null)
                runtimePlayer = spawned.AddComponent<RuntimeSpriteAnimationPlayer>();

            runtimePlayer.enabled = true;
            runtimePlayer.Initialize(runtimeDefinition, spriteRenderer);

            if (animator != null)
                animator.enabled = false;

            return runtimePlayer;
        }

        private GameObject ResolveRuntimeCharacterTemplatePrefab(bool preferAiList)
        {
            if (runtimeCharacterTemplatePrefab != null)
                return runtimeCharacterTemplatePrefab;

            if (preferAiList)
            {
                GameObject aiFallback = FindFirstPrefab(aiCharacterPrefabs);
                if (aiFallback != null)
                    return aiFallback;
            }

            GameObject fallback = FindFirstPrefab(characterPrefabs);
            if (fallback != null)
                return fallback;

            return FindFirstPrefab(aiCharacterPrefabs);
        }

        private static GameObject FindFirstPrefab(List<CharacterPrefabEntry> entries)
        {
            if (entries == null)
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterPrefabEntry entry = entries[i];
                if (entry != null && entry.prefab != null)
                    return entry.prefab;
            }

            return null;
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

        private void OnCreateDogClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameState.UploadAvatar);
        }
    }
}
