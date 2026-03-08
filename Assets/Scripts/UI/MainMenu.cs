using UnityEngine;
using UnityEngine.UI;
using SnackAttack.Gameplay;

namespace SnackAttack.UI
{
    public class MainMenu : MonoBehaviour
    {
        public Button play1PButton;
        public Button playVsAIButton;
        public Button play2PButton;
        public Button settingsButton;
        public Button quitButton;

        private void Start()
        {
            AutoWireButtonsIfMissing();

            if (play1PButton != null) play1PButton.onClick.AddListener(OnPlay1PClicked);
            if (playVsAIButton != null) playVsAIButton.onClick.AddListener(OnPlayVsAIClicked);
            if (play2PButton != null) play2PButton.onClick.AddListener(OnPlay2PClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null)   quitButton.onClick.AddListener(OnQuitClicked);

            Core.EventManager.StartListening("GAME_STATE_CHANGED", OnGameStateChanged);
            UpdateVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
        }

        private void OnDestroy()
        {
            Core.EventManager.StopListening("GAME_STATE_CHANGED", OnGameStateChanged);
            if (play1PButton != null) play1PButton.onClick.RemoveListener(OnPlay1PClicked);
            if (playVsAIButton != null) playVsAIButton.onClick.RemoveListener(OnPlayVsAIClicked);
            if (play2PButton != null) play2PButton.onClick.RemoveListener(OnPlay2PClicked);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (quitButton != null)   quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        private void OnGameStateChanged(object stateObj)
        {
            if (stateObj is GameState state)
                UpdateVisibility(state);
        }

        private void UpdateVisibility(GameState state)
        {
            gameObject.SetActive(state == GameState.MainMenu);
        }

        private void OnPlay1PClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            GameManager.Instance.gameMode = "single";
            GameManager.Instance.vsAI = false;
            EnsureScreenActive("CharacterSelectScreen");
            GameManager.Instance.ChangeState(GameState.CharacterSelect);
        }

        private void OnPlayVsAIClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            GameManager.Instance.gameMode = "vsai";
            GameManager.Instance.vsAI = true;
            EnsureScreenActive("CharacterSelectScreen");
            GameManager.Instance.ChangeState(GameState.CharacterSelect);
        }

        private void OnPlay2PClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            GameManager.Instance.gameMode = "2p";
            GameManager.Instance.vsAI = false;
            EnsureScreenActive("CharacterSelectScreen");
            GameManager.Instance.ChangeState(GameState.CharacterSelect);
        }

        private void OnSettingsClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            EnsureScreenActive("SettingsScreen");
            GameManager.Instance.ChangeState(GameState.Settings);
        }

        private void OnQuitClicked()
        {
            Core.EventManager.TriggerEvent("PLAY_SOUND", "select");
            Application.Quit();
        }

        private void AutoWireButtonsIfMissing()
        {
            if (play1PButton == null)
                play1PButton = FindButton("Btn_1P");

            if (playVsAIButton == null)
                playVsAIButton = FindButton("Btn_1P_Vs_AI");

            if (play2PButton == null)
                play2PButton = FindButton("Btn_2P");

            if (settingsButton == null)
                settingsButton = FindButton("Btn_Settings");

            if (quitButton == null)
                quitButton = FindButton("Btn_Quit");
        }

        private Button FindButton(string buttonName)
        {
            Transform target = transform.Find("MenuContainer/" + buttonName);
            if (target == null)
                target = transform.Find(buttonName);

            return target != null ? target.GetComponent<Button>() : null;
        }

        private static void EnsureScreenActive(string screenObjectName)
        {
            if (string.IsNullOrWhiteSpace(screenObjectName))
                return;

            GameObject target = GameObject.Find(screenObjectName);
            if (target != null && !target.activeSelf)
                target.SetActive(true);
        }
    }
}
