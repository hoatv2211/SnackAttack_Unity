using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SnackAttack.Audio;
using SnackAttack.Gameplay;

namespace SnackAttack.UI
{
    public class SettingsScreen : MonoBehaviour
    {
        [Header("Audio Toggles")]
        public Toggle musicEnabledToggle;
        public Toggle sfxEnabledToggle;

        [Header("Audio Sliders")]
        public Slider musicVolumeSlider;
        public Slider sfxVolumeSlider;
        public Slider masterVolumeSlider;

        [Header("Value Labels")]
        public TextMeshProUGUI musicEnabledValueText;
        public TextMeshProUGUI sfxEnabledValueText;

        [Header("Toggle Value Colors")]
        public Color toggleOnColor = new Color32(81, 180, 71, 255);
        public Color toggleOffColor = new Color32(222, 97, 91, 255);

        [Header("Buttons")]
        public Button backButton;

        private bool suppressCallbacks;

        private void Start()
        {
            AutoWireReferencesIfMissing();
            DisableLegacyVolumeValueTexts();
            BindCallbacks();

            Core.EventManager.StartListening("GAME_STATE_CHANGED", OnGameStateChanged);
            UpdateVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
            SyncFromAudioManager();
        }

        private void OnDestroy()
        {
            Core.EventManager.StopListening("GAME_STATE_CHANGED", OnGameStateChanged);
            UnbindCallbacks();
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                OnBackClicked();
        }

        private void OnGameStateChanged(object stateObj)
        {
            if (!(stateObj is GameState state))
                return;

            UpdateVisibility(state);

            if (state == GameState.Settings)
                SyncFromAudioManager();
        }

        private void UpdateVisibility(GameState state)
        {
            gameObject.SetActive(state == GameState.Settings);
        }

        private void BindCallbacks()
        {
            if (musicEnabledToggle != null)
                musicEnabledToggle.onValueChanged.AddListener(OnMusicEnabledChanged);

            if (sfxEnabledToggle != null)
                sfxEnabledToggle.onValueChanged.AddListener(OnSfxEnabledChanged);

            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        private void UnbindCallbacks()
        {
            if (musicEnabledToggle != null)
                musicEnabledToggle.onValueChanged.RemoveListener(OnMusicEnabledChanged);

            if (sfxEnabledToggle != null)
                sfxEnabledToggle.onValueChanged.RemoveListener(OnSfxEnabledChanged);

            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);

            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);

            if (backButton != null)
                backButton.onClick.RemoveListener(OnBackClicked);
        }

        private void SyncFromAudioManager()
        {
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager == null)
                return;

            suppressCallbacks = true;

            if (musicEnabledToggle != null)
                musicEnabledToggle.SetIsOnWithoutNotify(audioManager.MusicEnabled);

            if (sfxEnabledToggle != null)
                sfxEnabledToggle.SetIsOnWithoutNotify(audioManager.SfxEnabled);

            if (musicVolumeSlider != null)
                musicVolumeSlider.SetValueWithoutNotify(audioManager.MusicVolume);

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.SetValueWithoutNotify(audioManager.SfxVolume);

            if (masterVolumeSlider != null)
                masterVolumeSlider.SetValueWithoutNotify(audioManager.MasterVolume);

            suppressCallbacks = false;
            RefreshValueTexts();
        }

        private void OnMusicEnabledChanged(bool value)
        {
            if (suppressCallbacks)
                return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMusicEnabled(value);

            RefreshValueTexts();
        }

        private void OnSfxEnabledChanged(bool value)
        {
            if (suppressCallbacks)
                return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetSfxEnabled(value);

            RefreshValueTexts();
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (suppressCallbacks)
                return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMusicVolume(value);

            RefreshValueTexts();
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (suppressCallbacks)
                return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetSfxVolume(value);

            RefreshValueTexts();
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (suppressCallbacks)
                return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMasterVolume(value);

            RefreshValueTexts();
        }

        private void RefreshValueTexts()
        {
            UpdateToggleValueText(musicEnabledValueText, musicEnabledToggle);
            UpdateToggleValueText(sfxEnabledValueText, sfxEnabledToggle);
        }

        private void UpdateToggleValueText(TextMeshProUGUI valueText, Toggle toggle)
        {
            if (valueText == null || toggle == null)
                return;

            bool isOn = toggle.isOn;
            valueText.text = isOn ? "ON" : "OFF";
            valueText.color = isOn ? toggleOnColor : toggleOffColor;
        }

        private void OnBackClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");

            if (GameManager.Instance != null)
                GameManager.Instance.ChangeState(GameState.MainMenu);
        }

        private void AutoWireReferencesIfMissing()
        {
            if (musicEnabledToggle == null)
                musicEnabledToggle = FindToggle("MenuContainer/Row_Music/Toggle");

            if (sfxEnabledToggle == null)
                sfxEnabledToggle = FindToggle("MenuContainer/Row_Sfx/Toggle");

            if (musicVolumeSlider == null)
                musicVolumeSlider = FindSlider("MenuContainer/Row_MusicVolume/Slider");

            if (sfxVolumeSlider == null)
                sfxVolumeSlider = FindSlider("MenuContainer/Row_SfxVolume/Slider");

            if (masterVolumeSlider == null)
                masterVolumeSlider = FindSlider("MenuContainer/Row_MasterVolume/Slider");

            if (musicEnabledValueText == null)
                musicEnabledValueText = FindText("MenuContainer/Row_Music/Value");

            if (sfxEnabledValueText == null)
                sfxEnabledValueText = FindText("MenuContainer/Row_Sfx/Value");

            if (backButton == null)
                backButton = FindButton("Btn_Back");
        }

        private void DisableLegacyVolumeValueTexts()
        {
            DisableChildIfExists("MenuContainer/Row_MusicVolume/Value");
            DisableChildIfExists("MenuContainer/Row_SfxVolume/Value");
            DisableChildIfExists("MenuContainer/Row_MasterVolume/Value");
        }

        private void DisableChildIfExists(string path)
        {
            Transform target = transform.Find(path);
            if (target != null && target.gameObject.activeSelf)
                target.gameObject.SetActive(false);
        }

        private Toggle FindToggle(string path)
        {
            Transform target = transform.Find(path);
            return target != null ? target.GetComponent<Toggle>() : null;
        }

        private Slider FindSlider(string path)
        {
            Transform target = transform.Find(path);
            return target != null ? target.GetComponent<Slider>() : null;
        }

        private Button FindButton(string name)
        {
            Transform target = transform.Find(name);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private TextMeshProUGUI FindText(string path)
        {
            Transform target = transform.Find(path);
            return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
        }
    }
}
