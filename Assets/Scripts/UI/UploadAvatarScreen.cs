using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SnackAttack.Gameplay;
using SnackAttack.Services;
using SnackAttack.Config;

namespace SnackAttack.UI
{
    public class UploadAvatarScreen : MonoBehaviour
    {
        private enum UploadState
        {
            Input,
            Generating,
            Error,
        }

        [Header("State Panels (Optional)")]
        public GameObject inputPanel;
        public GameObject generatingPanel;
        public GameObject errorPanel;

        [Header("Inputs")]
        public TMP_InputField dogNameInput;

        [Header("Buttons")]
        public Button browseButton;
        public Button generateButton;
        public Button backButton;
        public Button retryButton;
        public Button errorBackButton;
        public Button doneBackButton;

        [Header("Feedback")]
        public TextMeshProUGUI statusText;
        public Image previewImage;
        public TextMeshProUGUI generatingStatusText;
        public TextMeshProUGUI generatingStepText;
        public TextMeshProUGUI generatingNameText;
        public Image generatingPreviewImage;
        public TextMeshProUGUI errorMessageText;

        [Header("Test Mode")]
        public bool isTest;
        public string testTemplateId = "biggie";
        public float testStepDelay = 0.08f;

        private Texture2D selectedPhoto;
        private string selectedPhotoPath = string.Empty;
        private bool generating;
        private UploadState uploadState = UploadState.Input;

        private const string ApiKeyEnvName = "OPENROUTER_API_KEY";
        private const string ApiKeyPrefsName = "OPENROUTER_API_KEY";

        private void Start()
        {
           Core.EventManager.StartListening("GAME_STATE_CHANGED", OnGameStateChanged);
           UpdateVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
        }

        private void OnDestroy()
        {
            Core.EventManager.StopListening("GAME_STATE_CHANGED", OnGameStateChanged);
        }

        private void OnEnable()
        {
            if (browseButton != null) browseButton.onClick.AddListener(OnBrowseClicked);
            if (generateButton != null) generateButton.onClick.AddListener(OnGenerateClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
            if (errorBackButton != null) errorBackButton.onClick.AddListener(OnBackClicked);
            if (doneBackButton != null) doneBackButton.onClick.AddListener(OnDoneBackClicked);
           

            string key = GetConfiguredApiKey();
            if (string.IsNullOrWhiteSpace(key))
                SetStatus("OpenRouter API key is missing. Set it in OpenRouterConfig.asset.");

            SetUploadState(UploadState.Input);
            SetDoneBackVisible(false);
        }

        private void OnDisable()
        {
            if (browseButton != null) browseButton.onClick.RemoveListener(OnBrowseClicked);
            if (generateButton != null) generateButton.onClick.RemoveListener(OnGenerateClicked);
            if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
            if (retryButton != null) retryButton.onClick.RemoveListener(OnRetryClicked);
            if (errorBackButton != null) errorBackButton.onClick.RemoveListener(OnBackClicked);
            if (doneBackButton != null) doneBackButton.onClick.RemoveListener(OnDoneBackClicked);
        }

        private void OnGameStateChanged(object stateObj)
        {
            if (stateObj is GameState state)
                UpdateVisibility(state);
        }

        private void UpdateVisibility(GameState state)
        {
            gameObject.SetActive(state == GameState.UploadAvatar);
        }

        private void OnBackClicked()
        {
            if (generating) return;
            GameManager.Instance.ChangeState(GameState.CharacterSelect);
        }

        private void OnRetryClicked()
        {
            if (generating) return;
            SetUploadState(UploadState.Input);
            SetStatus("Adjust inputs and try again.");
            SetDoneBackVisible(false);
        }

        private void OnDoneBackClicked()
        {
            if (generating) return;
            GameManager.Instance.ChangeState(GameState.CharacterSelect);
        }

        private void OnBrowseClicked()
        {
            string path = string.Empty;

#if UNITY_EDITOR
            path = UnityEditor.EditorUtility.OpenFilePanel(
                "Select a dog photo",
                string.Empty,
                "png,jpg,jpeg,bmp,webp");
#endif

            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("No photo selected.");
                return;
            }

            selectedPhotoPath = path.Trim();

            if (!TryLoadPhotoPreview(selectedPhotoPath))
                return;

            SetStatus("Photo selected.");
        }

        private void OnGenerateClicked()
        {
            if (generating) return;

            string dogName = dogNameInput != null ? dogNameInput.text.Trim() : string.Empty;

            if (isTest)
            {
                StartCoroutine(GenerateFromTemplateFlow(dogName));
                return;
            }

            string apiKey = GetConfiguredApiKey();
            string photoPath = selectedPhotoPath;

            if (string.IsNullOrWhiteSpace(dogName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(photoPath))
            {
                SetStatus("Dog name, API key, and photo are required.");
                return;
            }

            if (generatingNameText != null)
                generatingNameText.text = dogName.ToUpperInvariant();

            if (!TryLoadPhotoPreview(photoPath))
                return;

            selectedPhotoPath = photoPath;

            PlayerPrefs.SetString(ApiKeyPrefsName, apiKey);
            PlayerPrefs.Save();
            CacheApiKeyToConfig(apiKey);

            generating = true;
            SetUploadState(UploadState.Generating);
            SetDoneBackVisible(false);
            SetStatus("Generating avatar...");
            UpdateGeneratingStatus("Preparing photo...");

            StartCoroutine(OpenRouterAvatarService.GenerateProfileAvatar(
                apiKey,
                dogName,
                selectedPhoto,
                onStatus: status =>
                {
                    UpdateGeneratingStatus(status);
                },
                onSuccess: result =>
                {
                    HandleGenerationSuccess(result, dogName, true);
                },
                onError: err =>
                {
                    HandleGenerationError($"Generation failed: {err}");
                }));
        }

        private IEnumerator GenerateFromTemplateFlow(string dogName)
        {
            string displayName = string.IsNullOrWhiteSpace(dogName)
                ? testTemplateId
                : dogName.Trim();

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "TestDog";

            string templateId = NormalizeId(testTemplateId);
            if (string.IsNullOrWhiteSpace(templateId))
                templateId = "biggie";

            string sourceDir = Path.Combine(Application.streamingAssetsPath, "template", templateId);
            if (!Directory.Exists(sourceDir))
            {
                HandleGenerationError($"Template folder not found: {sourceDir}");
                yield break;
            }

            generating = true;
            SetUploadState(UploadState.Generating);
            SetDoneBackVisible(false);

            if (generatingNameText != null)
                generatingNameText.text = displayName.ToUpperInvariant();

            string characterId = NormalizeId(displayName);
            string targetDir = Path.Combine(Application.persistentDataPath, "custom_avatars", characterId);
            Directory.CreateDirectory(targetDir);

            string[] files = { "profile.png", "run.png", "eat.png", "walk.png", "boost.png" };

            UpdateGeneratingStatus("Step 1/7: Loading test template...");
            yield return new WaitForSeconds(testStepDelay);

            for (int i = 0; i < files.Length; i++)
            {
                string fileName = files[i];
                string src = Path.Combine(sourceDir, fileName);
                string dst = Path.Combine(targetDir, fileName);

                if (!File.Exists(src))
                {
                    HandleGenerationError($"Template file missing: {src}");
                    yield break;
                }

                UpdateGeneratingStatus($"Step {Mathf.Clamp(i + 2, 2, 6)}/7: Copying {fileName}...");
                try
                {
                    File.Copy(src, dst, true);
                }
                catch (Exception ex)
                {
                    HandleGenerationError($"Cannot copy template file: {ex.Message}");
                    yield break;
                }

                yield return new WaitForSeconds(testStepDelay);
            }

            UpdateGeneratingStatus($"Step 7/7: Registering {displayName}...");
            yield return new WaitForSeconds(testStepDelay);

            AvatarGenerationResult result = new AvatarGenerationResult
            {
                success = true,
                characterId = characterId,
                displayName = displayName,
                profilePath = Path.Combine(targetDir, "profile.png"),
                runSpritePath = Path.Combine(targetDir, "run.png"),
                eatSpritePath = Path.Combine(targetDir, "eat.png"),
                walkSpritePath = Path.Combine(targetDir, "walk.png"),
                boostSpritePath = Path.Combine(targetDir, "boost.png"),
            };

            HandleGenerationSuccess(result, displayName, false);
        }

        private void HandleGenerationSuccess(AvatarGenerationResult result, string fallbackName, bool mirrorToStreamingAssets)
        {
            generating = false;
            SetUploadState(UploadState.Generating);

            if (mirrorToStreamingAssets)
                TryMirrorGeneratedAvatarToStreamingAssets(result);

            RuntimeCharacterDefinition runtimeDef = RuntimeSpriteSheetLoader.BuildDefinition(result);
            if (runtimeDef != null)
                RuntimeCharacterRegistry.Register(runtimeDef);

            string displayName = string.IsNullOrWhiteSpace(result != null ? result.displayName : string.Empty)
                ? fallbackName
                : result.displayName;

            SetStatus($"SUCCESS! {displayName} is ready.");
            UpdateGeneratingStatus("Avatar generation complete.");
            SetDoneBackVisible(true);

            if (generatingNameText != null)
                generatingNameText.text = (displayName ?? string.Empty).ToUpperInvariant();

            string profilePath = result != null ? result.profilePath : string.Empty;
            Sprite generatedSprite = TryLoadSpriteFromPath(profilePath);
            if (generatedSprite != null)
            {
                if (previewImage != null)
                    previewImage.sprite = generatedSprite;
                if (generatingPreviewImage != null)
                    generatingPreviewImage.sprite = generatedSprite;
            }
        }

        private void HandleGenerationError(string message)
        {
            generating = false;
            SetUploadState(UploadState.Error);
            SetDoneBackVisible(false);
            SetStatus(message);
            if (errorMessageText != null)
                errorMessageText.text = message;
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        private static string GetConfiguredApiKey()
        {
            OpenRouterConfigSO cfg = ConfigManager.Instance != null
                ? ConfigManager.Instance.openRouterConfig
                : null;

            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.openRouterApiKey))
                return cfg.openRouterApiKey.Trim();

            string key = Environment.GetEnvironmentVariable(ApiKeyEnvName);
            if (!string.IsNullOrWhiteSpace(key))
                return key.Trim();

            return PlayerPrefs.GetString(ApiKeyPrefsName, string.Empty).Trim();
        }

        private static void CacheApiKeyToConfig(string apiKey)
        {
            OpenRouterConfigSO cfg = ConfigManager.Instance != null
                ? ConfigManager.Instance.openRouterConfig
                : null;

            if (cfg != null)
                cfg.openRouterApiKey = apiKey ?? string.Empty;
        }

        private bool TryLoadPhotoPreview(string photoPath)
        {
            byte[] fileBytes;
            try
            {
                fileBytes = System.IO.File.ReadAllBytes(photoPath);
            }
            catch (Exception ex)
            {
                SetStatus($"Cannot read photo: {ex.Message}");
                return false;
            }

            selectedPhoto = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!selectedPhoto.LoadImage(fileBytes))
            {
                SetStatus("Invalid image format.");
                return false;
            }

            if (previewImage != null)
            {
                Sprite s = Sprite.Create(
                    selectedPhoto,
                    new Rect(0, 0, selectedPhoto.width, selectedPhoto.height),
                    new Vector2(0.5f, 0.5f));
                previewImage.sprite = s;
                if (generatingPreviewImage != null)
                    generatingPreviewImage.sprite = s;
            }

            return true;
        }

        private void SetUploadState(UploadState state)
        {
            uploadState = state;

            if (inputPanel != null)
                inputPanel.SetActive(state == UploadState.Input);

            if (generatingPanel != null)
                generatingPanel.SetActive(state == UploadState.Generating);

            if (errorPanel != null)
                errorPanel.SetActive(state == UploadState.Error);
        }

        private void UpdateGeneratingStatus(string status)
        {
            SetStatus(status);

            if (generatingStatusText != null)
                generatingStatusText.text = status;

            if (generatingStepText != null)
            {
                int step = MapStep(status);
                generatingStepText.text = $"STEP {step}/7";
            }
        }

        private static int MapStep(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return 1;

            string s = status.ToLowerInvariant();
            if (s.Contains("step 1/7")) return 1;
            if (s.Contains("step 2/7")) return 2;
            if (s.Contains("step 3/7")) return 3;
            if (s.Contains("step 4/7")) return 4;
            if (s.Contains("step 5/7")) return 5;
            if (s.Contains("step 6/7")) return 6;
            if (s.Contains("step 7/7")) return 7;
            if (s.Contains("prepar")) return 1;
            if (s.Contains("building")) return 2;
            if (s.Contains("sending")) return 3;
            if (s.Contains("parsing")) return 4;
            if (s.Contains("saving")) return 5;
            if (s.Contains("complete")) return 7;
            return 2;
        }

        private static Sprite TryLoadSpriteFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                    return null;

                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch
            {
                return null;
            }
        }

        private void SetDoneBackVisible(bool visible)
        {
            if (doneBackButton != null)
                doneBackButton.gameObject.SetActive(visible);
        }

        private static string NormalizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string x = raw.Trim().ToLowerInvariant().Replace(" ", "_");
            System.Text.StringBuilder sb = new System.Text.StringBuilder(x.Length);
            foreach (char c in x)
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private void TryMirrorGeneratedAvatarToStreamingAssets(AvatarGenerationResult result)
        {
#if UNITY_EDITOR
            if (result == null || string.IsNullOrWhiteSpace(result.characterId))
                return;

            try
            {
                string id = NormalizeId(result.characterId);
                if (string.IsNullOrWhiteSpace(id))
                    return;

                string streamingRoot = Path.Combine(Application.dataPath, "StreamingAssets", "custom_avatars", id);
                Directory.CreateDirectory(streamingRoot);

                CopyIfExists(result.profilePath, Path.Combine(streamingRoot, "profile.png"));
                CopyIfExists(result.runSpritePath, Path.Combine(streamingRoot, "run.png"));
                CopyIfExists(result.eatSpritePath, Path.Combine(streamingRoot, "eat.png"));
                CopyIfExists(result.walkSpritePath, Path.Combine(streamingRoot, "walk.png"));
                CopyIfExists(result.boostSpritePath, Path.Combine(streamingRoot, "boost.png"));

                UnityEditor.AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("UploadAvatarScreen: Cannot mirror generated avatar to StreamingAssets: " + ex.Message);
            }
#endif
        }

        private static void CopyIfExists(string src, string dst)
        {
            if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst) || !File.Exists(src))
                return;

            File.Copy(src, dst, true);
        }
    }
}