using UnityEngine;
using UnityEngine.UI;
using SnackAttack.Gameplay;
using SnackAttack.Config;
using TMPro;

namespace SnackAttack.UI
{
    public class GameplayHUD : MonoBehaviour
    {
        public TextMeshProUGUI timerText;
        public TextMeshProUGUI player1ScoreText;
        public TextMeshProUGUI player2ScoreText;
        public TextMeshProUGUI countdownText;
        public TextMeshProUGUI roundText;
        public Button backButton;

        [Header("Crowd Chaos UI")]
        public GameObject crowdChaosPanel;
        public TextMeshProUGUI crowdChaosHeaderText;
        public TextMeshProUGUI crowdChaosBodyText;
        public TextMeshProUGUI crowdChaosOptionsText;
        public TextMeshProUGUI crowdChaosDangerText;
        public Image crowdChaosTintOverlay;

        private int lastDisplayedTime = -1;
        private string player1DisplayName = "P1";
        private string player2DisplayName = "P2";

        private void Start()
        {
            AutoWireBackButtonIfMissing();
            AutoWireCrowdChaosUi();

            Core.EventManager.StartListening("GAME_STATE_CHANGED", OnGameStateChanged);
            Core.EventManager.StartListening("SCORE_UPDATED", OnScoreUpdated);
            Core.EventManager.StartListening("COUNTDOWN_TICK", OnCountdownTick);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            // Fallback MainMenu when GameManager chưa chạy → chỉ MainMenu hiển thị (tránh scene khác in-game)
            UpdateVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
        }

        private void OnDestroy()
        {
            Core.EventManager.StopListening("GAME_STATE_CHANGED", OnGameStateChanged);
            Core.EventManager.StopListening("SCORE_UPDATED", OnScoreUpdated);
            Core.EventManager.StopListening("COUNTDOWN_TICK", OnCountdownTick);

            if (backButton != null)
                backButton.onClick.RemoveListener(OnBackClicked);
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;

            var state = GameManager.Instance.State;
            if (state == GameState.Playing)
            {
                int timeInt = Mathf.CeilToInt(GameManager.Instance.timeRemaining);
                if (timeInt != lastDisplayedTime)
                {
                    lastDisplayedTime = timeInt;
                    if (timerText != null) timerText.text = timeInt.ToString();
                }
                if (countdownText != null) countdownText.gameObject.SetActive(false);
            }
            else if (state == GameState.Countdown)
            {
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(true);
                    countdownText.text = GameManager.Instance.countdownValue.ToString() + "s";
                }
            }

            UpdateCrowdChaosOverlay(state);

            if (!gameObject.activeInHierarchy)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
                OnBackClicked();
        }

        private void OnGameStateChanged(object stateObj)
        {
            if (stateObj is GameState state)
            {
                UpdateVisibility(state);

                if (state == GameState.Playing || state == GameState.Countdown)
                {
                    UpdateScores();
                    UpdateRoundText();
                    lastDisplayedTime = -1;
                }

                UpdateCrowdChaosOverlay(state);
            }
        }

        private void OnCountdownTick(object param)
        {
            if (countdownText != null && param is int value)
            {
                countdownText.text = value.ToString() +"s";
            }
        }

        private void OnScoreUpdated(object param) => UpdateScores();

        private void UpdateScores()
        {
            if (GameManager.Instance == null) return;

            RefreshCharacterNames();

            if (player1ScoreText != null)
                player1ScoreText.text = FormatScoreText(player1DisplayName, GameManager.Instance.player1Score);

            if (player2ScoreText != null)
                player2ScoreText.text = FormatScoreText(player2DisplayName, GameManager.Instance.player2Score);
        }

        private static string FormatScoreText(string characterName, int score)
        {
            return $"{characterName} <size=50%>Score [{score}]</size>";
        }

        private void RefreshCharacterNames()
        {
            if (GameManager.Instance == null)
                return;

            player1DisplayName = ResolveCharacterDisplayName(GameManager.Instance.selectedPlayer1CharacterId, "P1");
            player2DisplayName = ResolveCharacterDisplayName(GameManager.Instance.selectedPlayer2CharacterId, "P2");
        }

        private static string ResolveCharacterDisplayName(string characterId, string fallback)
        {
            string normalizedId = NormalizeId(characterId);
            if (string.IsNullOrEmpty(normalizedId))
                return fallback;

            if (ConfigManager.Instance != null && ConfigManager.Instance.characterConfig != null)
            {
                CharacterData data = ConfigManager.Instance.characterConfig.GetCharacter(normalizedId);
                if (data != null && !string.IsNullOrWhiteSpace(data.characterName))
                    return data.characterName;
            }

            return ToDisplayCase(normalizedId);
        }

        private static string NormalizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            return raw.Trim().ToLowerInvariant();
        }

        private static string ToDisplayCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string[] parts = value.Replace('-', ' ').Replace('_', ' ').Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;

                string word = parts[i];
                parts[i] = char.ToUpperInvariant(word[0]) + word.Substring(1);
            }

            return string.Join(" ", parts);
        }

        private void UpdateRoundText()
        {
            if (roundText != null && GameManager.Instance != null)
                roundText.text = $"ROUND {GameManager.Instance.currentRound}/{GameManager.Instance.maxRounds}";
        }

        private void UpdateVisibility(GameState state)
        {
            gameObject.SetActive(state == GameState.Playing || state == GameState.Countdown);
        }

        private void UpdateCrowdChaosOverlay(GameState state)
        {
            if (GameManager.Instance == null)
            {
                SetCrowdChaosUiVisible(false);
                return;
            }

            bool gameplayState = state == GameState.Playing || state == GameState.Countdown;
            bool visible = gameplayState && GameManager.Instance.CrowdChaosOverlayVisible;
            SetCrowdChaosUiVisible(visible);
            if (!visible)
                return;

            if (crowdChaosHeaderText != null)
                crowdChaosHeaderText.text = GameManager.Instance.CrowdChaosTitle;

            if (crowdChaosBodyText != null)
                crowdChaosBodyText.text = GameManager.Instance.CrowdChaosBody;

            if (crowdChaosOptionsText != null)
                crowdChaosOptionsText.text = GameManager.Instance.CrowdChaosOptions;

            if (crowdChaosDangerText != null)
                crowdChaosDangerText.text = GameManager.Instance.CrowdChaosDangerText;

            if (crowdChaosTintOverlay != null)
            {
                float alpha = Mathf.Clamp01(GameManager.Instance.CrowdChaosTintAlpha);
                Color tint = crowdChaosTintOverlay.color;
                tint.r = 0.9f;
                tint.g = 0.2f;
                tint.b = 0.2f;
                tint.a = alpha;
                crowdChaosTintOverlay.color = tint;
                crowdChaosTintOverlay.gameObject.SetActive(alpha > 0.001f);
            }
        }

        private void SetCrowdChaosUiVisible(bool visible)
        {
            if (crowdChaosPanel != null)
                crowdChaosPanel.SetActive(visible);

            if (crowdChaosHeaderText != null)
                crowdChaosHeaderText.gameObject.SetActive(visible);

            if (crowdChaosBodyText != null)
                crowdChaosBodyText.gameObject.SetActive(visible);

            if (crowdChaosOptionsText != null)
                crowdChaosOptionsText.gameObject.SetActive(visible);

            if (crowdChaosDangerText != null)
                crowdChaosDangerText.gameObject.SetActive(visible);

            if (!visible && crowdChaosTintOverlay != null)
                crowdChaosTintOverlay.gameObject.SetActive(false);
        }

        private void AutoWireCrowdChaosUi()
        {
            if (crowdChaosPanel == null)
            {
                Transform panelTransform = transform.Find("CrowdChaosOverlay");
                if (panelTransform != null)
                    crowdChaosPanel = panelTransform.gameObject;
            }

            if (crowdChaosHeaderText == null)
                crowdChaosHeaderText = FindCrowdChaosText("CrowdChaosOverlay/Header");

            if (crowdChaosBodyText == null)
                crowdChaosBodyText = FindCrowdChaosText("CrowdChaosOverlay/Body");

            if (crowdChaosOptionsText == null)
                crowdChaosOptionsText = FindCrowdChaosText("CrowdChaosOverlay/Options");

            if (crowdChaosDangerText == null)
                crowdChaosDangerText = FindCrowdChaosText("CrowdChaosOverlay/Danger");

            if (crowdChaosTintOverlay == null)
            {
                Transform tintTransform = transform.Find("CrowdChaosOverlay/Tint");
                if (tintTransform != null)
                    crowdChaosTintOverlay = tintTransform.GetComponent<Image>();
            }

            if (crowdChaosPanel == null)
                CreateFallbackCrowdChaosUi();
        }

        private TextMeshProUGUI FindCrowdChaosText(string path)
        {
            Transform target = transform.Find(path);
            return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
        }

        private void CreateFallbackCrowdChaosUi()
        {
            GameObject panel = new GameObject("CrowdChaosOverlay");
            panel.transform.SetParent(transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            crowdChaosPanel = panel;

            GameObject tintObject = new GameObject("Tint");
            tintObject.transform.SetParent(panel.transform, false);
            RectTransform tintRect = tintObject.AddComponent<RectTransform>();
            tintRect.anchorMin = Vector2.zero;
            tintRect.anchorMax = Vector2.one;
            tintRect.offsetMin = Vector2.zero;
            tintRect.offsetMax = Vector2.zero;

            crowdChaosTintOverlay = tintObject.AddComponent<Image>();
            crowdChaosTintOverlay.color = new Color(0.9f, 0.2f, 0.2f, 0f);
            crowdChaosTintOverlay.raycastTarget = false;
            crowdChaosTintOverlay.gameObject.SetActive(false);

            crowdChaosHeaderText = CreateCrowdChaosText(panel.transform, "Header", new Vector2(0f, 215f), 48, TextAlignmentOptions.Center, Color.white);
            crowdChaosBodyText = CreateCrowdChaosText(panel.transform, "Body", new Vector2(0f, 150f), 32, TextAlignmentOptions.Center, new Color32(255, 238, 168, 255));
            crowdChaosOptionsText = CreateCrowdChaosText(panel.transform, "Options", new Vector2(0f, 70f), 28, TextAlignmentOptions.Center, Color.white);
            crowdChaosDangerText = CreateCrowdChaosText(panel.transform, "Danger", new Vector2(0f, 285f), 30, TextAlignmentOptions.Center, new Color32(255, 92, 92, 255));

            SetCrowdChaosUiVisible(false);
        }

        private static TextMeshProUGUI CreateCrowdChaosText(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = new Vector2(1120f, 120f);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;

            return text;
        }

        private void OnBackClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");

            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameState.MainMenu);
        }

        private void AutoWireBackButtonIfMissing()
        {
            if (backButton != null)
                return;

            Transform explicitButton = transform.Find("TopBar/Btn_Back");
            if (explicitButton != null)
                backButton = explicitButton.GetComponent<Button>();

            if (backButton != null)
                return;

            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];
                if (candidate != null && candidate.name.Equals("Btn_Back", System.StringComparison.OrdinalIgnoreCase))
                {
                    backButton = candidate;
                    break;
                }
            }

            if (backButton == null)
                backButton = CreateFallbackBackButton();
        }

        private Button CreateFallbackBackButton()
        {
            Transform parent = transform.Find("TopBar");
            if (parent == null)
                parent = transform;

            GameObject buttonObject = new GameObject("Btn_Back");
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170f, 52f);

            if (parent.name == "TopBar")
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(16f, 0f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(20f, -20f);
            }

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color32(77, 43, 31, 220);

            Button button = buttonObject.AddComponent<Button>();

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = "BACK";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 28;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }
    }
}
