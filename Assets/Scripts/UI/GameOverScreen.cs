using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SnackAttack.Gameplay;
using SnackAttack.Config;

namespace SnackAttack.UI
{
    public class GameOverScreen : MonoBehaviour
    {
        [Header("Primary Text")]
        public TextMeshProUGUI resultText;

        [Header("Optional Win Texts")]
        [Tooltip("Example: BIGGIE ROUNDS 3")]
        public TextMeshProUGUI roundsText;
        [Tooltip("Example: BIGGIE\\nSCORE\\n11750")]
        public TextMeshProUGUI winnerScoreText;

        [Header("Buttons")]
        public Button playAgainButton;
        public Button mainMenuButton;

        private void Start()
        {
            if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);

            Core.EventManager.StartListening("GAME_STATE_CHANGED", OnGameStateChanged);
            // Fallback MainMenu khi GameManager chưa có → ẩn GameOver (tránh scene khác in-game)
            UpdateVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
        }

        private void OnDestroy()
        {
            Core.EventManager.StopListening("GAME_STATE_CHANGED", OnGameStateChanged);
            if (playAgainButton != null) playAgainButton.onClick.RemoveAllListeners();
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveAllListeners();
        }

        private void OnGameStateChanged(object stateObj)
        {
            if (stateObj is GameState state)
            {
                UpdateVisibility(state);
                if (state == GameState.GameOver)
                    UpdateResult();
            }
        }

        private void UpdateVisibility(GameState state)
        {
            gameObject.SetActive(state == GameState.GameOver);
        }

        private void UpdateResult()
        {
            if (GameManager.Instance == null)
                return;

            int p1 = GameManager.Instance.p1RoundWins;
            int p2 = GameManager.Instance.p2RoundWins;

            string p1Name = ResolveCharacterDisplayName(GameManager.Instance.selectedPlayer1CharacterId, "PLAYER 1");
            string p2Name = ResolveCharacterDisplayName(GameManager.Instance.selectedPlayer2CharacterId, "PLAYER 2");

            bool p1Wins = p1 > p2;
            bool p2Wins = p2 > p1;

            if (p1Wins)
            {
                SetWinnerTexts(p1Name, p1, GameManager.Instance.player1Score);
                return;
            }

            if (p2Wins)
            {
                SetWinnerTexts(p2Name, p2, GameManager.Instance.player2Score);
                return;
            }

            bool hasExtraTextBindings = roundsText != null || winnerScoreText != null;

            if (resultText != null)
            {
                if (hasExtraTextBindings)
                    resultText.text = "DRAW!";
                else
                    resultText.text =
                        $"DRAW!\n<size=70%>{ToUpperLabel(p1Name)} {p1} - {p2} {ToUpperLabel(p2Name)}</size>\n" +
                        $"<size=60%>{ToUpperLabel(p1Name)} SCORE {GameManager.Instance.player1Score} | {ToUpperLabel(p2Name)} SCORE {GameManager.Instance.player2Score}</size>";
            }

            if (roundsText != null)
                roundsText.text = $"{ToUpperLabel(p1Name)} {p1} - {p2} {ToUpperLabel(p2Name)}";

            if (winnerScoreText != null)
                winnerScoreText.text =
                    $"{ToUpperLabel(p1Name)} SCORE {GameManager.Instance.player1Score}\n" +
                    $"{ToUpperLabel(p2Name)} SCORE {GameManager.Instance.player2Score}";
        }

        private void SetWinnerTexts(string winnerName, int winnerRounds, int winnerScore)
        {
            string winnerLabel = ToUpperLabel(winnerName);
            bool hasExtraTextBindings = roundsText != null || winnerScoreText != null;

            if (resultText != null)
            {
                if (hasExtraTextBindings)
                    resultText.text = $"{winnerLabel} WINS!";
                else
                    resultText.text =
                        $"{winnerLabel} WINS!\n<size=70%>{winnerLabel} ROUNDS {winnerRounds}</size>\n" +
                        $"<size=60%>{winnerLabel} SCORE {winnerScore}</size>";
            }

            if (roundsText != null)
                roundsText.text = $"{winnerLabel} ROUNDS {winnerRounds}";

            if (winnerScoreText != null)
                winnerScoreText.text = $"{winnerLabel}\nSCORE\n{winnerScore}";
        }

        private static string ResolveCharacterDisplayName(string characterId, string fallback)
        {
            string normalized = NormalizeId(characterId);
            if (string.IsNullOrEmpty(normalized))
                return fallback;

            if (ConfigManager.Instance != null && ConfigManager.Instance.characterConfig != null)
            {
                CharacterData data = ConfigManager.Instance.characterConfig.GetCharacter(normalized);
                if (data != null && !string.IsNullOrWhiteSpace(data.characterName))
                    return data.characterName;
            }

            return ToDisplayCase(normalized);
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

        private static string ToUpperLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "PLAYER";

            return value.Trim().ToUpperInvariant();
        }

        private void OnPlayAgainClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            GameManager.Instance.StartNewGame();
        }

        private void OnMainMenuClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            GameManager.Instance.ChangeState(GameState.MainMenu);
        }
    }
}
