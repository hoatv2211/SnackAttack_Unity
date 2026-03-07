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

        private int lastDisplayedTime = -1;
        private string player1DisplayName = "P1";
        private string player2DisplayName = "P2";

        private void Start()
        {
            AutoWireBackButtonIfMissing();

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
