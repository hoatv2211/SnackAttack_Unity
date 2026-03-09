using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SnackAttack.Gameplay;
using SnackAttack.Config;
using System.Collections.Generic;

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

        [Header("Character Card Source")]
        // Legacy arrays still used as fallback/cache source.
        public Button[] characterCardButtons;
        public Image[] characterCardBorders;
        public string[] characterIdsByCard = { "jazzy", "biggie", "dash", "snowy", "prissy", "rex" };
        public Button characterCardPrefab;
        public Transform characterCardsParent;
        public bool autoUseFirstSceneCardAsPrefab = true;
        [Header("Auto Grid")]
        public bool useAutoGridLayout = true;
        public int gridColumns = 3;
        public Vector2 gridCellSize = new Vector2(220f, 260f);
        public Vector2 gridSpacing = new Vector2(24f, 24f);
        public RectOffset gridPadding;

        public TextMeshProUGUI selectionPromptText;
        public Color defaultBorderColor = new Color32(249, 203, 111, 255);
        public Color player1BorderColor = new Color32(100, 180, 255, 255);
        public Color player2BorderColor = new Color32(255, 120, 120, 255);
        public Color bothPlayersBorderColor = new Color32(190, 110, 240, 255);

        private int selectedIndex = 0;
        private int selectedPlayer1Index = 0;
        private int selectedPlayer2Index = -1;
        private SelectionPhase selectionPhase = SelectionPhase.Player1;
        private string[] configuredBaseIds;
        private readonly Dictionary<string, CardVisual> baseVisualById = new Dictionary<string, CardVisual>();
        private readonly List<Button> generatedCardButtons = new List<Button>();

        private sealed class CharacterCardModel
        {
            public string id;
            public string label;
            public Sprite sprite;
        }

        private sealed class CardVisual
        {
            public Sprite sprite;
            public string label;
        }

        private void Awake()
        {
            EnsureGridPadding();
        }

        private void OnValidate()
        {
            EnsureGridPadding();
        }

        private void EnsureGridPadding()
        {
            if (gridPadding == null)
                gridPadding = new RectOffset(24, 24, 16, 16);
        }

        private void Start()
        {
            RuntimeCharacterRegistry.EnsureLoadedFromDisk();
            AutoWireOptionalButtons();
            CacheConfiguredBaseIds();
            CacheBaseVisualsFromSceneCards();
            ResolveCardPrefab();

            if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGameClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            if (createDogButton != null) createDogButton.onClick.AddListener(OnCreateDogClicked);

            Core.EventManager.StartListening("GAME_STATE_CHANGED", OnGameStateChanged);
            RuntimeCharacterRegistry.RegistryChanged += OnRuntimeRegistryChanged;
            UpdateVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
            RebuildCharacterCards();
            EnsurePromptText();
            ResetSelectionFlow();
            HighlightSelected();
        }

        private void OnDestroy()
        {
            Core.EventManager.StopListening("GAME_STATE_CHANGED", OnGameStateChanged);
            RuntimeCharacterRegistry.RegistryChanged -= OnRuntimeRegistryChanged;
            if (startGameButton != null) startGameButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();
            if (createDogButton != null) createDogButton.onClick.RemoveAllListeners();
            ClearGeneratedCards();
        }

        private void OnRuntimeRegistryChanged()
        {
            RebuildCharacterCards();
            ResetSelectionFlow();
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
                {
                    RebuildCharacterCards();
                    ResetSelectionFlow();
                }
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

        private void CacheConfiguredBaseIds()
        {
            if (characterIdsByCard != null && characterIdsByCard.Length > 0)
            {
                configuredBaseIds = new string[characterIdsByCard.Length];
                for (int i = 0; i < characterIdsByCard.Length; i++)
                    configuredBaseIds[i] = NormalizeId(characterIdsByCard[i]);
            }
            else
            {
                configuredBaseIds = new[] { "jazzy", "biggie", "dash", "snowy", "prissy", "rex" };
            }
        }

        private void CacheBaseVisualsFromSceneCards()
        {
            baseVisualById.Clear();

            int count = Mathf.Min(
                characterIdsByCard != null ? characterIdsByCard.Length : 0,
                characterCardButtons != null ? characterCardButtons.Length : 0);

            for (int i = 0; i < count; i++)
            {
                string id = NormalizeId(characterIdsByCard[i]);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                Button sourceButton = characterCardButtons[i];
                if (sourceButton == null)
                    continue;

                Image img = sourceButton.image != null ? sourceButton.image : sourceButton.GetComponent<Image>();
                TextMeshProUGUI label = sourceButton.GetComponentInChildren<TextMeshProUGUI>(true);

                baseVisualById[id] = new CardVisual
                {
                    sprite = img != null ? img.sprite : null,
                    label = label != null ? label.text : id.ToUpperInvariant(),
                };
            }
        }

        private void ResolveCardPrefab()
        {
            if (characterCardPrefab == null && autoUseFirstSceneCardAsPrefab && characterCardButtons != null && characterCardButtons.Length > 0)
                characterCardPrefab = characterCardButtons[0];

            if (characterCardsParent == null && characterCardPrefab != null)
                characterCardsParent = characterCardPrefab.transform.parent;

            if (characterCardPrefab != null && characterCardPrefab.gameObject.activeSelf)
                characterCardPrefab.gameObject.SetActive(false);
        }

        private void RebuildCharacterCards()
        {
            ClearGeneratedCards();

            List<CharacterCardModel> models = BuildCharacterCardModels();
            if (characterCardPrefab == null || characterCardsParent == null || models.Count == 0)
                return;

            List<Button> buttons = new List<Button>(models.Count);
            List<Image> borders = new List<Image>(models.Count);
            List<string> ids = new List<string>(models.Count);

            for (int i = 0; i < models.Count; i++)
            {
                CharacterCardModel m = models[i];
                if (m == null || string.IsNullOrWhiteSpace(m.id))
                    continue;

                Button card = Instantiate(characterCardPrefab, characterCardsParent);
                card.gameObject.SetActive(true);
                card.name = "Card_" + m.id;
                ApplyCardVisual(card, m);

                buttons.Add(card);
                borders.Add(card.image != null ? card.image : card.GetComponent<Image>());
                ids.Add(m.id);
                generatedCardButtons.Add(card);
            }

            characterCardButtons = buttons.ToArray();
            characterCardBorders = borders.ToArray();
            characterIdsByCard = ids.ToArray();

            ApplyAutoGridLayout(models.Count);
            WireCharacterCardClicks();
        }

        private void ApplyAutoGridLayout(int itemCount)
        {
            if (!useAutoGridLayout || characterCardsParent == null)
                return;

            GridLayoutGroup grid = characterCardsParent.GetComponent<GridLayoutGroup>();
            if (grid == null)
                grid = characterCardsParent.gameObject.AddComponent<GridLayoutGroup>();

            int safeCount = Mathf.Max(1, itemCount);
            int columns;
            int rows;

            // Layout rule:
            // - Up to 6 cards: 3x2
            // - More than 6 cards: 4 columns and wrap by rows
            if (safeCount <= 6)
            {
                columns = 3;
                rows = 2;
            }
            else
            {
                columns = 4;
                rows = Mathf.CeilToInt(safeCount / 4f);
            }

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = gridCellSize;
            grid.spacing = gridSpacing;
            grid.padding = gridPadding;
            grid.childAlignment = TextAnchor.UpperCenter;

            RectTransform rt = characterCardsParent as RectTransform;
            if (rt == null)
                return;

            float width = gridPadding.left + gridPadding.right + (columns * gridCellSize.x) + ((columns - 1) * gridSpacing.x);
            float height = gridPadding.top + gridPadding.bottom + (rows * gridCellSize.y) + ((rows - 1) * gridSpacing.y);

            Vector2 size = rt.sizeDelta;
            size.x = width;
            size.y = height;
            rt.sizeDelta = size;
        }

        private List<CharacterCardModel> BuildCharacterCardModels()
        {
            List<CharacterCardModel> models = new List<CharacterCardModel>();
            Dictionary<string, int> idToIndex = new Dictionary<string, int>();

            if (configuredBaseIds != null)
            {
                for (int i = 0; i < configuredBaseIds.Length; i++)
                {
                    string id = NormalizeId(configuredBaseIds[i]);
                    if (string.IsNullOrWhiteSpace(id) || idToIndex.ContainsKey(id))
                        continue;

                    CardVisual v = GetBaseVisual(id);
                    CharacterCardModel model = new CharacterCardModel
                    {
                        id = id,
                        label = v != null && !string.IsNullOrWhiteSpace(v.label) ? v.label : id.ToUpperInvariant(),
                        sprite = v != null ? v.sprite : null,
                    };

                    idToIndex[id] = models.Count;
                    models.Add(model);
                }
            }

            var runtimeCharacters = RuntimeCharacterRegistry.GetAll();
            for (int i = 0; i < runtimeCharacters.Count; i++)
            {
                RuntimeCharacterDefinition runtime = runtimeCharacters[i];
                if (runtime == null || string.IsNullOrWhiteSpace(runtime.id))
                    continue;

                string id = NormalizeId(runtime.id);
                string label = string.IsNullOrWhiteSpace(runtime.displayName)
                    ? id.ToUpperInvariant()
                    : runtime.displayName.ToUpperInvariant();

                if (idToIndex.TryGetValue(id, out int idx))
                {
                    models[idx].label = label;
                    models[idx].sprite = runtime.profileSprite != null ? runtime.profileSprite : models[idx].sprite;
                }
                else
                {
                    idToIndex[id] = models.Count;
                    models.Add(new CharacterCardModel
                    {
                        id = id,
                        label = label,
                        sprite = runtime.profileSprite,
                    });
                }
            }

            return models;
        }

        private CardVisual GetBaseVisual(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            baseVisualById.TryGetValue(id, out CardVisual visual);
            return visual;
        }

        private void ApplyCardVisual(Button card, CharacterCardModel model)
        {
            if (card == null || model == null)
                return;

            Image img = card.image != null ? card.image : card.GetComponent<Image>();
            if (img != null && model.sprite != null)
                img.sprite = model.sprite;

            TextMeshProUGUI label = card.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                string text = string.IsNullOrWhiteSpace(model.label)
                    ? model.id.ToUpperInvariant()
                    : model.label;
                label.text = text;
            }
        }

        private void ClearGeneratedCards()
        {
            for (int i = 0; i < generatedCardButtons.Count; i++)
            {
                Button b = generatedCardButtons[i];
                if (b == null)
                    continue;

                Destroy(b.gameObject);
            }

            generatedCardButtons.Clear();
            characterCardButtons = System.Array.Empty<Button>();
            characterCardBorders = System.Array.Empty<Image>();
            characterIdsByCard = System.Array.Empty<string>();
        }

        private static string NormalizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            return raw.Trim().ToLowerInvariant();
        }

        private void WireCharacterCardClicks()
        {
            if (characterCardButtons == null)
                return;

            for (int i = 0; i < characterCardButtons.Length; i++)
            {
                Button button = characterCardButtons[i];
                if (button == null)
                    continue;

                button.onClick.RemoveAllListeners();
                int idx = i;
                button.onClick.AddListener(() => SelectCharacter(idx));
            }
        }
    }
}
