using UnityEngine;

namespace SnackAttack.Config
{
    public class ConfigManager : MonoBehaviour
    {
        public static ConfigManager Instance { get; private set; }

        public GameSettingsSO gameSettings;
        public CharacterConfigSO characterConfig;
        public SnackConfigSO snackConfig;

        public ControlsConfig controlsConfig { get; private set; } = new ControlsConfig();
        public AiDifficultyConfig aiDifficultyConfig { get; private set; } = new AiDifficultyConfig();
        public PowerupVisualsConfig powerupVisualsConfig { get; private set; } = new PowerupVisualsConfig();
        public LevelsConfig levelsConfig { get; private set; } = new LevelsConfig();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadRuntimeJsonConfigs();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [ContextMenu("Reload Runtime JSON Configs")]
        public void LoadRuntimeJsonConfigs()
        {
            controlsConfig = LoadJsonConfig("Config/controls", new ControlsConfig());
            aiDifficultyConfig = LoadJsonConfig("Config/ai_difficulty", new AiDifficultyConfig());
            powerupVisualsConfig = LoadJsonConfig("Config/powerup_visuals", new PowerupVisualsConfig());
            levelsConfig = LoadJsonConfig("Config/levels", new LevelsConfig());
        }

        public AiDifficultyEntry GetDifficultyConfig(string difficultyName)
        {
            if (aiDifficultyConfig == null)
                aiDifficultyConfig = new AiDifficultyConfig();

            return aiDifficultyConfig.GetDifficultyOrDefault(difficultyName);
        }

        public LevelConfigData GetLevelConfigForRound(int roundNumber)
        {
            if (levelsConfig == null)
                levelsConfig = new LevelsConfig();

            return levelsConfig.GetByRoundProgression(roundNumber);
        }

        private static T LoadJsonConfig<T>(string resourcePath, T fallback) where T : class
        {
            TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                Debug.LogWarning($"ConfigManager: Missing JSON resource at Resources/{resourcePath}.json");
                return fallback;
            }

            try
            {
                T parsed = JsonUtility.FromJson<T>(textAsset.text);
                return parsed ?? fallback;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ConfigManager: Failed to parse {resourcePath}.json: {ex.Message}");
                return fallback;
            }
        }
    }
}
