using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using SnackAttack.Core;
using SnackAttack.Config;
using SnackAttack.Gameplay;
using SnackAttack.UI;
using SnackAttack.Audio;

namespace SnackAttack.EditorScripts
{
    public class SceneSetupEditor
    {
        [MenuItem("SnackAttack/Setup Main Scene")]
        public static void SetupScene()
        {
            FixTextureTypes(); // CRITICAL: Fix images imported as Texture2D

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // CAMERA
            GameObject cameraGO = new GameObject("Main Camera");
            Camera cam = cameraGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            cameraGO.transform.position = new Vector3(0, 0, -10f); // Standard 2D camera position
            cameraGO.tag = "MainCamera";
            cameraGO.AddComponent<AudioListener>();

            // MANAGERS
            GameObject managersGO = new GameObject("Managers");
            SetupAudioManager(managersGO);
            SetupConfigManager(managersGO);
            GameManager gm = managersGO.AddComponent<GameManager>();

            // GAMEPLAY WORLD ENTITIES (Players, Spawners)
            GameObject gameplayRoot = CreateGameplayEntities();
            gm.gameplayEntitiesRoot = gameplayRoot;

            // CANVAS — ScreenSpaceOverlay so world sprites show through transparent areas
            GameObject canvasGO = new GameObject("Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1000, 1000); 
            canvasGO.AddComponent<GraphicRaycaster>();

            // EVENT SYSTEM
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // SCREENS — tạo xong chỉ bật MainMenu, tắt hết để scene = in-game (tránh "scene 1 kiểu, in-game 1 kiểu")
            CreateMainMenu(canvasGO);
            CreateSettingsScreen(canvasGO);
            CreateCharacterSelect(canvasGO);
            CreateGameplayHUD(canvasGO);
            CreateGameOverScreen(canvasGO);
            ApplyDefaultScreenVisibility(canvasGO);

            // SAVE SCENE
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            
            string scenePath = "Assets/Scenes/MainScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log("Main Scene Setup Complete! Visuals applied.");
        }

        
        //[MenuItem("SnackAttack/Fix gameplay screen visibility (current scene)")]
        public static void FixCurrentSceneScreenVisibility()
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogWarning("SnackAttack: Không tìm thấy Canvas trong scene. Chạy 'Setup Main Scene' trước.");
                return;
            }
            ApplyDefaultScreenVisibility(canvas);
            GameObject gameplayRoot = GameObject.Find("GameplayEntities");
            if (gameplayRoot != null && gameplayRoot.activeSelf)
            {
                gameplayRoot.SetActive(false);
                Debug.Log("SnackAttack: Đã tắt GameplayEntities (chỉ bật khi Countdown/Playing).");
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("SnackAttack: Đã đặt visibility màn hình (chỉ MainMenu bật). Lưu scene để giữ thay đổi.");
        }

        private static void FixTextureTypes()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spritePixelsPerUnit = 100;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
        }

        private static void SetupAudioManager(GameObject parent)
        {
            AudioManager audioMgr = parent.AddComponent<AudioManager>();
            audioMgr.musicSource = new GameObject("MusicSource").AddComponent<AudioSource>();
            audioMgr.musicSource.transform.SetParent(parent.transform);
            audioMgr.sfxSource = new GameObject("SFXSource").AddComponent<AudioSource>();
            audioMgr.sfxSource.transform.SetParent(parent.transform);

            audioMgr.soundEffects = new System.Collections.Generic.List<AudioManager.AudioClipEntry>();
            string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
            foreach (string guid in audioGuids)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null)
                {
                    AudioManager.AudioClipEntry entry = new AudioManager.AudioClipEntry();
                    entry.name = clip.name;
                    entry.clip = clip;
                    audioMgr.soundEffects.Add(entry);
                }
            }
        }

        private static void SetupConfigManager(GameObject parent)
        {
            ConfigManager configMgr = parent.AddComponent<ConfigManager>();
            configMgr.gameSettings = AssetDatabase.LoadAssetAtPath<GameSettingsSO>("Assets/Resources/ConfigSO/GameSettings.asset");
            configMgr.characterConfig = AssetDatabase.LoadAssetAtPath<CharacterConfigSO>("Assets/Resources/ConfigSO/CharacterConfig.asset");
            configMgr.snackConfig = AssetDatabase.LoadAssetAtPath<SnackConfigSO>("Assets/Resources/ConfigSO/SnackConfig.asset");
        }

        private static GameObject CreatePanel(string name, GameObject parent, string spriteName)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent.transform, false);
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = panel.AddComponent<Image>();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/{spriteName}");
            if (sprite != null) img.sprite = sprite;
            
            return panel;
        }

        private static Button CreateButton(string name, GameObject parent, Vector2 anchoredPos, Vector2 size, string spriteName)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/{spriteName}");
            if (sprite != null) img.sprite = sprite;
            else img.color = new Color(1, 1, 1, 0.2f); // Debug visibility if missing

            return btnObj.AddComponent<Button>();
        }

        private static void CreateMainMenu(GameObject canvas)
        {
            GameObject main = CreatePanel("MainMenu", canvas, "Home background.png");
            MainMenu mmScript = main.AddComponent<MainMenu>();

            // Top logo
            GameObject logo = new GameObject("Logo");
            logo.transform.SetParent(main.transform, false);
            RectTransform logoRT = logo.AddComponent<RectTransform>();
            logoRT.anchoredPosition = new Vector2(0, 250);
            logoRT.sizeDelta = new Vector2(500, 250);
            Image logoImg = logo.AddComponent<Image>();
            logoImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Jazzy logo sml.png");

            // Menu Container Board
            GameObject menuContainer = new GameObject("MenuContainer");
            menuContainer.transform.SetParent(main.transform, false);
            RectTransform mcRT = menuContainer.AddComponent<RectTransform>();
            mcRT.anchoredPosition = new Vector2(0, -150);
            mcRT.sizeDelta = new Vector2(500, 450);
            Image mcImg = menuContainer.AddComponent<Image>();
            mcImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Menu tall.png");

            // Buttons inside Menu Container
            mmScript.play1PButton = CreateButton("Btn_1P", menuContainer, new Vector2(0, 100), new Vector2(350, 60), "single_player.png");
            mmScript.playVsAIButton = CreateButton("Btn_1P_Vs_AI", menuContainer, new Vector2(0, 30), new Vector2(350, 60), "1 play vs ai.png");
            mmScript.play2PButton = CreateButton("Btn_2P", menuContainer, new Vector2(0, -40), new Vector2(350, 60), "2 players.png");
            mmScript.settingsButton = CreateButton("Btn_Settings", menuContainer, new Vector2(0, -110), new Vector2(350, 60), "settings button.png");
            mmScript.quitButton = CreateButton("Btn_Quit", menuContainer, new Vector2(0, -180), new Vector2(350, 60), "quit button.png");
            
            // Text at bottom
            TextMeshProUGUI instructions = CreateTMPText("Instructions", main, new Vector2(0, -450));
            instructions.text = "ARROW KEYS + ENTER TO SELECT";
            instructions.fontSize = 24;
            instructions.color = new Color32(147, 76, 48, 255);
        }

        private static void CreateSettingsScreen(GameObject canvas)
        {
            GameObject panel = CreatePanel("SettingsScreen", canvas, "Settings background.png");
            SettingsScreen settingsScript = panel.AddComponent<SettingsScreen>();

            GameObject title = new GameObject("Title");
            title.transform.SetParent(panel.transform, false);
            RectTransform titleRT = title.AddComponent<RectTransform>();
            titleRT.anchoredPosition = new Vector2(0f, 360f);
            titleRT.sizeDelta = new Vector2(560f, 120f);
            Image titleImg = title.AddComponent<Image>();
            titleImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/settings text.png");

            GameObject menuContainer = new GameObject("MenuContainer");
            menuContainer.transform.SetParent(panel.transform, false);
            RectTransform menuRT = menuContainer.AddComponent<RectTransform>();
            menuRT.anchoredPosition = new Vector2(0f, -40f);
            menuRT.sizeDelta = new Vector2(700f, 620f);
            Image menuImg = menuContainer.AddComponent<Image>();
            menuImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Menu tall.png");

            TextMeshProUGUI labelMusic;
            TextMeshProUGUI labelSfx;
            TextMeshProUGUI labelMusicVolume;
            TextMeshProUGUI labelSfxVolume;
            TextMeshProUGUI labelMasterVolume;

            settingsScript.musicEnabledToggle = CreateSettingsToggleRow(
                menuContainer,
                rowName: "Row_Music",
                label: "Music",
                y: 170f,
                out labelMusic,
                out settingsScript.musicEnabledValueText);

            settingsScript.sfxEnabledToggle = CreateSettingsToggleRow(
                menuContainer,
                rowName: "Row_Sfx",
                label: "Sound Effects",
                y: 90f,
                out labelSfx,
                out settingsScript.sfxEnabledValueText);

            settingsScript.musicVolumeSlider = CreateSettingsSliderRow(
                menuContainer,
                rowName: "Row_MusicVolume",
                label: "Music Volume",
                y: 10f,
                out labelMusicVolume);

            settingsScript.sfxVolumeSlider = CreateSettingsSliderRow(
                menuContainer,
                rowName: "Row_SfxVolume",
                label: "SFX Volume",
                y: -70f,
                out labelSfxVolume);

            settingsScript.masterVolumeSlider = CreateSettingsSliderRow(
                menuContainer,
                rowName: "Row_MasterVolume",
                label: "Master Volume",
                y: -150f,
                out labelMasterVolume);

            ApplySettingsLabelStyle(labelMusic);
            ApplySettingsLabelStyle(labelSfx);
            ApplySettingsLabelStyle(labelMusicVolume);
            ApplySettingsLabelStyle(labelSfxVolume);
            ApplySettingsLabelStyle(labelMasterVolume);

            settingsScript.backButton = CreateButton("Btn_Back", panel, new Vector2(0f, -430f), new Vector2(250f, 70f), "");
            Image backImage = settingsScript.backButton.GetComponent<Image>();
            if (backImage != null)
                backImage.color = new Color32(77, 43, 31, 220);

            TextMeshProUGUI backTxt = CreateTMPText("BackTxt", settingsScript.backButton.gameObject, Vector2.zero);
            backTxt.text = "BACK";
            backTxt.color = Color.white;
            backTxt.fontSize = 36;

            TextMeshProUGUI instructions = CreateTMPText("Instructions", panel, new Vector2(0f, -470f));
            instructions.text = "ESC OR BACK TO RETURN";
            instructions.fontSize = 24;
            instructions.color = new Color32(147, 76, 48, 255);
        }

        private static void ApplySettingsLabelStyle(TextMeshProUGUI label)
        {
            if (label == null)
                return;

            label.fontSize = 36;
            label.color = new Color32(77, 43, 31, 255);
            label.alignment = TextAlignmentOptions.Left;
        }

        private static Toggle CreateSettingsToggleRow(
            GameObject parent,
            string rowName,
            string label,
            float y,
            out TextMeshProUGUI labelText,
            out TextMeshProUGUI valueText)
        {
            GameObject row = new GameObject(rowName);
            row.transform.SetParent(parent.transform, false);
            RectTransform rowRT = row.AddComponent<RectTransform>();
            rowRT.anchoredPosition = new Vector2(0f, y);
            rowRT.sizeDelta = new Vector2(620f, 64f);

            labelText = CreateTMPText("Label", row, new Vector2(-250f, 0f));
            labelText.text = label;

            Toggle toggle = CreateBasicToggle("Toggle", row, new Vector2(150f, 0f));

            valueText = CreateTMPText("Value", row, new Vector2(250f, 0f));
            valueText.text = "ON";
            valueText.fontSize = 32;
            valueText.color = new Color32(81, 180, 71, 255);

            return toggle;
        }

        private static Slider CreateSettingsSliderRow(
            GameObject parent,
            string rowName,
            string label,
            float y,
            out TextMeshProUGUI labelText)
        {
            GameObject row = new GameObject(rowName);
            row.transform.SetParent(parent.transform, false);
            RectTransform rowRT = row.AddComponent<RectTransform>();
            rowRT.anchoredPosition = new Vector2(0f, y);
            rowRT.sizeDelta = new Vector2(620f, 64f);

            labelText = CreateTMPText("Label", row, new Vector2(-250f, 0f));
            labelText.text = label;

            Slider slider = CreateBasicSlider("Slider", row, new Vector2(100f, 0f), new Vector2(240f, 28f));

            return slider;
        }

        private static Toggle CreateBasicToggle(string name, GameObject parent, Vector2 anchoredPos)
        {
            GameObject toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(parent.transform, false);

            RectTransform toggleRT = toggleObj.AddComponent<RectTransform>();
            toggleRT.anchoredPosition = anchoredPos;
            toggleRT.sizeDelta = new Vector2(44f, 44f);

            Image background = toggleObj.AddComponent<Image>();
            background.color = new Color32(77, 43, 31, 220);

            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.targetGraphic = background;

            GameObject checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(toggleObj.transform, false);
            RectTransform checkRT = checkmarkObj.AddComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0.2f, 0.2f);
            checkRT.anchorMax = new Vector2(0.8f, 0.8f);
            checkRT.offsetMin = Vector2.zero;
            checkRT.offsetMax = Vector2.zero;

            Image checkmarkImage = checkmarkObj.AddComponent<Image>();
            checkmarkImage.color = new Color32(81, 180, 71, 255);

            toggle.graphic = checkmarkImage;
            toggle.isOn = true;

            return toggle;
        }

        private static Slider CreateBasicSlider(string name, GameObject parent, Vector2 anchoredPos, Vector2 size)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent.transform, false);

            RectTransform sliderRT = sliderObj.AddComponent<RectTransform>();
            sliderRT.anchoredPosition = anchoredPos;
            sliderRT.sizeDelta = size;

            Image sliderBg = sliderObj.AddComponent<Image>();
            sliderBg.color = new Color32(255, 255, 255, 120);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.wholeNumbers = false;

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRT = fillArea.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRT.offsetMin = new Vector2(10f, 0f);
            fillAreaRT.offsetMax = new Vector2(-10f, 0f);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color32(81, 180, 71, 255);

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRT = handleArea.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10f, 0f);
            handleAreaRT.offsetMax = new Vector2(-10f, 0f);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRT = handle.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(22f, 38f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color32(255, 200, 0, 255);

            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        private static void CreateCharacterSelect(GameObject canvas)
        {
            GameObject panel = CreatePanel("CharacterSelectScreen", canvas, "Choose your dog background.png");
            CharacterSelectScreen csScript = panel.AddComponent<CharacterSelectScreen>();

            // Title
            GameObject title = new GameObject("Title");
            title.transform.SetParent(panel.transform, false);
            RectTransform titleRT = title.AddComponent<RectTransform>();
            titleRT.anchoredPosition = new Vector2(0, 380);
            titleRT.sizeDelta = new Vector2(500, 120);
            Image titleImg = title.AddComponent<Image>();
            titleImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Choose your dog ui.png");

            // Character Grid (3 columns x 2 rows)
            string[] charNames = { "Jazzy", "Biggie", "Dash", "Snowy", "Prissy", "Rex" };
            float startX = -250f;
            float startY = 150f;
            float spacingX = 250f;
            float spacingY = -230f;

            Button[] cardButtons = new Button[charNames.Length];
            Image[] cardBorders = new Image[charNames.Length];

            for (int i = 0; i < charNames.Length; i++)
            {
                int row = i / 3;
                int col = i % 3;
                Vector2 pos = new Vector2(startX + (col * spacingX), startY + (row * spacingY));

                GameObject cardPanel = new GameObject($"Card_{charNames[i]}");
                cardPanel.transform.SetParent(panel.transform, false);
                RectTransform cardRT = cardPanel.AddComponent<RectTransform>();
                cardRT.anchoredPosition = pos;
                cardRT.sizeDelta = new Vector2(210, 210);
                Image cardBg = cardPanel.AddComponent<Image>();
                cardBg.color = new Color32(249, 203, 111, 255);
                Button cardBtn = cardPanel.AddComponent<Button>();
                cardButtons[i] = cardBtn;
                cardBorders[i] = cardBg;

                GameObject profile = new GameObject("ProfilePic");
                profile.transform.SetParent(cardPanel.transform, false);
                RectTransform profileRT = profile.AddComponent<RectTransform>();
                profileRT.anchoredPosition = new Vector2(0, -10);
                profileRT.sizeDelta = new Vector2(160, 160);
                Image profileImg = profile.AddComponent<Image>();
                profileImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/{charNames[i]}.png");
                profileImg.raycastTarget = false;

                TextMeshProUGUI nameTxt = CreateTMPText("NameTxt", cardPanel, new Vector2(0, 75));
                nameTxt.text = charNames[i].ToUpper();
                nameTxt.fontSize = 28;
                nameTxt.color = new Color32(77, 43, 31, 255);
                nameTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 40);
                nameTxt.raycastTarget = false;
            }

            // Wire card arrays to script
            csScript.characterCardButtons = cardButtons;
            csScript.characterCardBorders = cardBorders;

            // Start + Back buttons
            csScript.startGameButton = CreateButton("Btn_Start", panel, new Vector2(200, -420), new Vector2(250, 70), "");
            csScript.startGameButton.GetComponent<Image>().color = new Color32(77, 43, 31, 200);
            TextMeshProUGUI startTxt = CreateTMPText("StartTxt", csScript.startGameButton.gameObject, Vector2.zero);
            startTxt.text = "START";
            startTxt.color = Color.white;
            startTxt.fontSize = 36;

            csScript.backButton = CreateButton("Btn_Back", panel, new Vector2(-200, -420), new Vector2(250, 70), "");
            csScript.backButton.GetComponent<Image>().color = new Color32(77, 43, 31, 200);
            TextMeshProUGUI backTxt = CreateTMPText("BackTxt", csScript.backButton.gameObject, Vector2.zero);
            backTxt.text = "BACK";
            backTxt.color = Color.white;
            backTxt.fontSize = 36;
        }

        private static void CreateGameplayHUD(GameObject canvas)
        {
            // HUD is transparent overlay - backgrounds are world-space sprites
            GameObject panel = new GameObject("GameplayHUD");
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            // NO Image component - fully transparent overlay for text only

            GameplayHUD hudScript = panel.AddComponent<GameplayHUD>();

            // Top Menu Bar
            GameObject topBar = new GameObject("TopBar");
            topBar.transform.SetParent(panel.transform, false);
            RectTransform barRT = topBar.AddComponent<RectTransform>();
            barRT.anchoredPosition = new Vector2(0, 420);
            barRT.sizeDelta = new Vector2(900, 80);
            Image barImg = topBar.AddComponent<Image>();
            barImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Menu bar yellow.png");

            hudScript.player1ScoreText = CreateTMPText("P1Score", topBar, new Vector2(-250, 0));
            hudScript.player1ScoreText.fontSize = 32;
            hudScript.player1ScoreText.color = new Color32(147, 76, 48, 255);

            hudScript.player2ScoreText = CreateTMPText("P2Score", topBar, new Vector2(250, 0));
            hudScript.player2ScoreText.fontSize = 32;
            hudScript.player2ScoreText.color = new Color32(147, 76, 48, 255);

            hudScript.timerText = CreateTMPText("Timer", panel, new Vector2(-400, 420));
            hudScript.timerText.fontSize = 42;
            hudScript.timerText.color = new Color32(77, 43, 31, 255);
            hudScript.timerText.alignment = TextAlignmentOptions.Left;

            // Countdown text (big, centered)
            hudScript.countdownText = CreateTMPText("CountdownText", panel, Vector2.zero);
            hudScript.countdownText.fontSize = 120;
            hudScript.countdownText.color = new Color32(255, 200, 0, 255);
            hudScript.countdownText.enableWordWrapping = false;

            // Round text
            hudScript.roundText = CreateTMPText("RoundText", topBar, new Vector2(0, -50));
            hudScript.roundText.fontSize = 20;
            hudScript.roundText.color = new Color32(77, 43, 31, 255);

            // Back button (return to Main Menu)
            hudScript.backButton = CreateButton("Btn_Back", topBar, new Vector2(-360, 0), new Vector2(170, 52), "");
            Image backImage = hudScript.backButton.GetComponent<Image>();
            if (backImage != null)
                backImage.color = new Color32(77, 43, 31, 220);

            TextMeshProUGUI backText = CreateTMPText("BackTxt", hudScript.backButton.gameObject, Vector2.zero);
            backText.text = "BACK";
            backText.fontSize = 28;
            backText.color = Color.white;
        }

        private static void CreateGameOverScreen(GameObject canvas)
        {
            GameObject panel = CreatePanel("GameOverScreen", canvas, "Win screen.png");
            GameOverScreen goScript = panel.AddComponent<GameOverScreen>();

            goScript.resultText = CreateTMPText("ResultText", panel, new Vector2(0, -50));
            goScript.resultText.fontSize = 72;
            goScript.resultText.color = new Color32(255, 255, 255, 255);
            goScript.resultText.alignment = TextAlignmentOptions.Center;

            goScript.playAgainButton = CreateButton("Btn_PlayAgain", panel, new Vector2(0, -150), new Vector2(200, 60), "");
            CreateTMPText("PlayAgainTxt", goScript.playAgainButton.gameObject, Vector2.zero).text = "PLAY AGAIN";
            
            goScript.mainMenuButton = CreateButton("Btn_Menu", panel, new Vector2(0, -230), new Vector2(200, 60), "");
            CreateTMPText("MenuTxt", goScript.mainMenuButton.gameObject, Vector2.zero).text = "MAIN MENU";

            // Visibility set in ApplyDefaultScreenVisibility so scene matches in-game.
        }

        /// <summary>Chỉ bật MainMenu trong scene; tắt GameplayHUD, GameOverScreen, CharacterSelect để scene = in-game.</summary>
        private static void ApplyDefaultScreenVisibility(GameObject canvas)
        {
            if (canvas == null) return;
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                Transform child = canvas.transform.GetChild(i);
                bool shouldBeActive = child.name == "MainMenu";
                child.gameObject.SetActive(shouldBeActive);
            }
        }

        private static TextMeshProUGUI CreateTMPText(string name, GameObject parent, Vector2 pos)
        {
            GameObject txtObj = new GameObject(name);
            txtObj.transform.SetParent(parent.transform, false);
            RectTransform rt = txtObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(400, 100);

            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "000";
            tmp.fontSize = 48;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }
        private static GameObject CreateGameplayEntities()
        {
            GameObject parent = new GameObject("GameplayEntities");
            parent.SetActive(false); // Hidden by default, GameManager will turn it on

            // ---- WORLD-SPACE BACKGROUNDS ----
            // Sky background (behind everything)
            GameObject sky = new GameObject("SkyBackground");
            sky.transform.SetParent(parent.transform);
            sky.transform.position = new Vector3(0, 0, 5f); // Far back
            SpriteRenderer skySr = sky.AddComponent<SpriteRenderer>();
            skySr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Battle screen background.png");
            skySr.sortingOrder = -10;
            skySr.drawMode = SpriteDrawMode.Simple;
            // Scale to fill camera view (ortho size 5 = 10 units tall, 1:1 aspect = 10 wide)
            if (skySr.sprite != null)
            {
                float sprW = skySr.sprite.bounds.size.x;
                float sprH = skySr.sprite.bounds.size.y;
                sky.transform.localScale = new Vector3(12f / sprW, 12f / sprH, 1f);
            }

            // Battle field in center
            GameObject field = new GameObject("BattleField");
            field.transform.SetParent(parent.transform);
            field.transform.position = new Vector3(0, -1f, 2f);
            SpriteRenderer fieldSr = field.AddComponent<SpriteRenderer>();
            fieldSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/battle field.png");
            fieldSr.sortingOrder = -5;
            if (fieldSr.sprite != null)
            {
                float sprW = fieldSr.sprite.bounds.size.x;
                float sprH = fieldSr.sprite.bounds.size.y;
                field.transform.localScale = new Vector3(8f / sprW, 10f / sprH, 1f);
            }

            // ---- SNACK PREFAB ----
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            
            GameObject snackBase = new GameObject("Snack_Prefab");
            SpriteRenderer snackSr = snackBase.AddComponent<SpriteRenderer>();
            snackSr.sortingOrder = 5;
            BoxCollider2D snackCol = snackBase.AddComponent<BoxCollider2D>();
            snackCol.isTrigger = true;
            snackCol.size = new Vector2(0.5f, 0.5f);
            snackBase.AddComponent<SnackAttack.Gameplay.Snack>();
            GameObject savedSnackPrefab = PrefabUtility.SaveAsPrefabAsset(snackBase, "Assets/Prefabs/Snack.prefab");
            Object.DestroyImmediate(snackBase);

            // 2. Create Snack Spawner
            GameObject spawnerGO = new GameObject("SnackSpawner");
            spawnerGO.transform.SetParent(parent.transform);
            SnackAttack.Gameplay.SnackSpawner spawner = spawnerGO.AddComponent<SnackAttack.Gameplay.SnackSpawner>();
            spawner.snackPrefab = savedSnackPrefab;
            spawner.arenaMinX = -3.5f;
            spawner.arenaMaxX = 3.5f;
            spawner.spawnY = 4.5f;
            spawner.groundY = -3.5f;

            // 3. Create Player 1
            GameObject p1 = new GameObject("Player1");
            p1.transform.SetParent(parent.transform);
            p1.transform.position = new Vector3(-2f, -3f, 0f);
            SpriteRenderer p1Sr = p1.AddComponent<SpriteRenderer>();
            p1Sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Jazzy.png");
            p1Sr.sortingOrder = 10;
            BoxCollider2D p1Col = p1.AddComponent<BoxCollider2D>();
            p1Col.isTrigger = true;
            p1Col.size = new Vector2(1f, 1f);
            Rigidbody2D p1Rb = p1.AddComponent<Rigidbody2D>();
            p1Rb.isKinematic = true;
            SnackAttack.Gameplay.PlayerController p1Ctrl = p1.AddComponent<SnackAttack.Gameplay.PlayerController>();
            p1Ctrl.playerIndex = 1;
            p1Ctrl.spriteRenderer = p1Sr;
            p1Ctrl.baseSpeed = 5f;
            p1Ctrl.arenaMinX = -4f;
            p1Ctrl.arenaMaxX = 4f;
            p1Ctrl.groundY = -3f;

            // 4. Create Player 2
            GameObject p2 = new GameObject("Player2");
            p2.transform.SetParent(parent.transform);
            p2.transform.position = new Vector3(2f, -3f, 0f);
            SpriteRenderer p2Sr = p2.AddComponent<SpriteRenderer>();
            p2Sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Biggie.png");
            p2Sr.sortingOrder = 10;
            p2Sr.flipX = true; // Player 2 faces left by default
            BoxCollider2D p2Col = p2.AddComponent<BoxCollider2D>();
            p2Col.isTrigger = true;
            p2Col.size = new Vector2(1f, 1f);
            Rigidbody2D p2Rb = p2.AddComponent<Rigidbody2D>();
            p2Rb.isKinematic = true;
            SnackAttack.Gameplay.PlayerController p2Ctrl = p2.AddComponent<SnackAttack.Gameplay.PlayerController>();
            p2Ctrl.playerIndex = 2;
            p2Ctrl.spriteRenderer = p2Sr;
            p2Ctrl.baseSpeed = 5f;
            p2Ctrl.arenaMinX = -4f;
            p2Ctrl.arenaMaxX = 4f;
            p2Ctrl.groundY = -3f;

            return parent;
        }
    }
}
