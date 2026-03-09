using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SnackAttack.Gameplay;

namespace SnackAttack.UI
{
    public class CharacterSelectScreen : MonoBehaviour
    {
        private enum SelectionPhase
        {
            Player1,
            Player2,
        }

        public Button startGameButton;
        public Button backButton;
        public Button createDogButton;

        // Character card buttons (set by SceneSetupEditor)
        public Button[] characterCardButtons;
        public Image[] characterCardBorders;
        public string[] characterIdsByCard = { "jazzy", "biggie", "dash", "snowy", "prissy", "rex" };
        public TextMeshProUGUI selectionPromptText;
        public Color defaultBorderColor = new Color32(249, 203, 111, 255);
        public Color player1BorderColor = new Color32(100, 180, 255, 255);
        public Color player2BorderColor = new Color32(255, 120, 120, 255);
        public Color bothPlayersBorderColor = new Color32(190, 110, 240, 255);

        private int selectedIndex = 0;
        private int selectedPlayer1Index = 0;
        private int selectedPlayer2Index = -1;
        private SelectionPhase selectionPhase = SelectionPhase.Player1;

        private void Start()
        {
            AutoWireOptionalButtons();

            if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGameClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            if (createDogButton != null) createDogButton.onClick.AddListener(OnCreateDogClicked);

            // Wire up character card clicks
            if (characterCardButtons != null)
            {
                for (int i = 0; i < characterCardButtons.Length; i++)
                {
                    int idx = i;
                    if (characterCardButtons[i] != null)
                        characterCardButtons[i].onClick.AddListener(() => SelectCharacter(idx));
                }
            }

            Core.EventManager.StartListening("GAME_STATE_CHANGED", OnGameStateChanged);
            // Fallback MainMenu khi GameManager chưa có → chỉ MainMenu hiển thị lúc bật Play
            UpdateVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
            EnsurePromptText();
            ResetSelectionFlow();
            HighlightSelected();
        }

        private void OnDestroy()
        {
            Core.EventManager.StopListening("GAME_STATE_CHANGED", OnGameStateChanged);
            if (startGameButton != null) startGameButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();
            if (createDogButton != null) createDogButton.onClick.RemoveAllListeners();
        }

        private void OnCreateDogClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameState.UploadAvatar);
        }

        private void SelectCharacter(int index)
        {
            selectedIndex = index;

            if (IsTwoPlayerMode())
            {
                if (selectionPhase == SelectionPhase.Player1)
                    selectedPlayer1Index = index;
                else
                    selectedPlayer2Index = index;
            }
            else
            {
                selectedPlayer1Index = index;
            }

            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            HighlightSelected();
            UpdateSelectionUI();
        }

        private void HighlightSelected()
        {
            if (characterCardBorders == null) return;

            bool twoPlayer = IsTwoPlayerMode();
            for (int i = 0; i < characterCardBorders.Length; i++)
            {
                if (characterCardBorders[i] != null)
                {
                    if (!twoPlayer)
                    {
                        characterCardBorders[i].color = (i == selectedPlayer1Index)
                            ? player1BorderColor
                            : defaultBorderColor;
                        continue;
                    }

                    bool isP1 = i == selectedPlayer1Index;
                    bool isP2 = i == selectedPlayer2Index;

                    if (isP1 && isP2)
                        characterCardBorders[i].color = bothPlayersBorderColor;
                    else if (isP1)
                        characterCardBorders[i].color = player1BorderColor;
                    else if (isP2)
                        characterCardBorders[i].color = player2BorderColor;
                    else
                        characterCardBorders[i].color = defaultBorderColor;
                }
            }
        }

        private void OnGameStateChanged(object stateObj)
        {
            if (stateObj is GameState state)
            {
                UpdateVisibility(state);

                if (state == GameState.CharacterSelect)
                    ResetSelectionFlow();
            }
        }

        private void UpdateVisibility(GameState state)
        {
            gameObject.SetActive(state == GameState.CharacterSelect);
        }

        private void OnStartGameClicked()
        {
            if (GameManager.Instance == null)
                return;

            if (IsTwoPlayerMode())
            {
                if (selectionPhase == SelectionPhase.Player1)
                {
                    Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
                    GameManager.Instance.SetSelectedPlayer1Character(GetCharacterIdByIndex(selectedPlayer1Index));
                    selectionPhase = SelectionPhase.Player2;
                    if (selectedPlayer2Index < 0)
                        selectedPlayer2Index = selectedPlayer1Index;

                    selectedIndex = selectedPlayer2Index;
                    HighlightSelected();
                    UpdateSelectionUI();
                    return;
                }

                GameManager.Instance.SetSelectedPlayer2Character(GetCharacterIdByIndex(selectedPlayer2Index));
            }
            else
            {
                GameManager.Instance.SetSelectedPlayer1Character(GetCharacterIdByIndex(selectedPlayer1Index));
            }

            // Start the game flow (storm intro then countdown)
            Core.EventManager.TriggerEvent("PLAY_SOUND", "start");
            GameManager.Instance.StartNewGame();
        }

        private void OnBackClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            ResetSelectionFlow();
            GameManager.Instance.ChangeState(GameState.MainMenu);
        }

        private string GetCharacterIdByIndex(int index)
        {
            if (characterIdsByCard != null && index >= 0 && index < characterIdsByCard.Length)
            {
                string id = characterIdsByCard[index];
                if (!string.IsNullOrWhiteSpace(id))
                    return id.Trim().ToLowerInvariant();
            }

            if (characterCardButtons != null && index >= 0 && index < characterCardButtons.Length)
            {
                Button selectedButton = characterCardButtons[index];
                if (selectedButton != null)
                {
                    string objectName = selectedButton.name;
                    if (objectName.StartsWith("Card_"))
                        objectName = objectName.Substring(5);

                    if (!string.IsNullOrWhiteSpace(objectName))
                        return objectName.Trim().ToLowerInvariant();
                }
            }

            return "jazzy";
        }

        private void ResetSelectionFlow()
        {
            selectedPlayer1Index = Mathf.Clamp(selectedPlayer1Index, 0, Mathf.Max(0, GetCardCount() - 1));
            selectedPlayer2Index = IsTwoPlayerMode() ? Mathf.Clamp(selectedPlayer2Index < 0 ? 1 : selectedPlayer2Index, 0, Mathf.Max(0, GetCardCount() - 1)) : -1;

            selectionPhase = SelectionPhase.Player1;
            selectedIndex = selectedPlayer1Index;
            HighlightSelected();
            UpdateSelectionUI();
        }

        private bool IsTwoPlayerMode()
        {
            if (GameManager.Instance == null || string.IsNullOrWhiteSpace(GameManager.Instance.gameMode))
                return false;

            return GameManager.Instance.gameMode.Trim().ToLowerInvariant() == "2p";
        }

        private int GetCardCount()
        {
            if (characterCardButtons != null && characterCardButtons.Length > 0)
                return characterCardButtons.Length;

            if (characterIdsByCard != null && characterIdsByCard.Length > 0)
                return characterIdsByCard.Length;

            return 1;
        }

        private void UpdateSelectionUI()
        {
            bool twoPlayer = IsTwoPlayerMode();

            if (selectionPromptText != null)
            {
                if (!twoPlayer)
                    selectionPromptText.text = "P1 SELECT CHARACTER";
                else if (selectionPhase == SelectionPhase.Player1)
                    selectionPromptText.text = "P1 SELECT CHARACTER";
                else
                    selectionPromptText.text = "P2 SELECT CHARACTER";
            }

            if (startGameButton != null)
            {
                TextMeshProUGUI startLabel = startGameButton.GetComponentInChildren<TextMeshProUGUI>();
                if (startLabel != null)
                {
                    startLabel.text = (twoPlayer && selectionPhase == SelectionPhase.Player1) ? "NEXT" : "START";
                }
            }
        }

        private void EnsurePromptText()
        {
            if (selectionPromptText != null)
                return;

            GameObject promptObject = new GameObject("SelectionPrompt");
            promptObject.transform.SetParent(transform, false);

            RectTransform rt = promptObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -28f);
            rt.sizeDelta = new Vector2(700f, 70f);

            selectionPromptText = promptObject.AddComponent<TextMeshProUGUI>();
            selectionPromptText.fontSize = 40;
            selectionPromptText.alignment = TextAlignmentOptions.Center;
            selectionPromptText.color = new Color32(77, 43, 31, 255);
            selectionPromptText.text = "P1 SELECT CHARACTER";
            selectionPromptText.raycastTarget = false;
        }

        private void AutoWireOptionalButtons()
        {
            if (createDogButton == null)
            {
                Transform t = transform.Find("Btn_CreateDog");
                if (t == null)
                    t = transform.Find("MenuContainer/Btn_CreateDog");

                if (t != null)
                    createDogButton = t.GetComponent<Button>();
            }
        }
    }
}
