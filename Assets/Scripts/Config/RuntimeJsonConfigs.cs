using System;
using UnityEngine;

namespace SnackAttack.Config
{
    [Serializable]
    public class PlayerControlsConfig
    {
        public string up = "w";
        public string down = "s";
        public string left = "a";
        public string right = "d";
    }

    [Serializable]
    public class GlobalControlsConfig
    {
        public string pause = "escape";
        public string confirm = "return";
        public string back = "backspace";
    }

    [Serializable]
    public class ControlsConfig
    {
        public PlayerControlsConfig player1 = new PlayerControlsConfig();
        public PlayerControlsConfig player2 = new PlayerControlsConfig
        {
            up = "up",
            down = "down",
            left = "left",
            right = "right",
        };

        public GlobalControlsConfig global = new GlobalControlsConfig();
    }

    [Serializable]
    public class AiDifficultyEntry
    {
        public int reaction_delay_ms = 250;
        public float decision_accuracy = 0.8f;
        public float pathfinding_efficiency = 0.85f;
        public bool avoids_penalties = true;
        public bool targets_powerups = true;
    }

    [Serializable]
    public class AiDifficultyCollection
    {
        public AiDifficultyEntry easy = new AiDifficultyEntry
        {
            reaction_delay_ms = 500,
            decision_accuracy = 0.6f,
            pathfinding_efficiency = 0.7f,
            avoids_penalties = false,
            targets_powerups = false,
        };

        public AiDifficultyEntry medium = new AiDifficultyEntry();

        public AiDifficultyEntry hard = new AiDifficultyEntry
        {
            reaction_delay_ms = 100,
            decision_accuracy = 0.95f,
            pathfinding_efficiency = 0.95f,
            avoids_penalties = true,
            targets_powerups = true,
        };

        public AiDifficultyEntry Get(string name)
        {
            string normalized = RuntimeConfigUtil.Normalize(name);
            switch (normalized)
            {
                case "easy":
                    return easy;
                case "hard":
                    return hard;
                case "medium":
                default:
                    return medium;
            }
        }
    }

    [Serializable]
    public class AiDifficultyConfig
    {
        public AiDifficultyCollection difficulties = new AiDifficultyCollection();
        public string default_difficulty = "medium";

        public AiDifficultyEntry GetDifficultyOrDefault(string requestedDifficulty)
        {
            if (difficulties == null)
                difficulties = new AiDifficultyCollection();

            string normalizedRequested = RuntimeConfigUtil.Normalize(requestedDifficulty);
            if (string.IsNullOrEmpty(normalizedRequested))
                normalizedRequested = RuntimeConfigUtil.Normalize(default_difficulty);

            return difficulties.Get(normalizedRequested);
        }
    }

    [Serializable]
    public class WingsVisualConfig
    {
        public bool enabled = true;
        public int[] color = { 255, 215, 80 };
        public int[] glow_color = { 255, 240, 150 };
        public int opacity = 180;
        public float flap_speed = 4.0f;
        public float flap_amplitude = 18f;
        public float wing_width = 38f;
        public float wing_height = 50f;
        public int feather_count = 5;
        public bool trail_particles = true;
        public float trail_particle_rate = 3f;
        public float trail_particle_lifetime = 0.6f;
    }

    [Serializable]
    public class SpeedStreaksVisualConfig
    {
        public bool enabled = true;
        public int afterimage_count = 3;
        public float afterimage_spacing = 22f;
        public int afterimage_base_alpha = 90;
        public int[] streak_color_boost = { 100, 200, 255 };
        public int[] streak_color_speed = { 255, 200, 80 };
        public float[] streak_width_range = { 40f, 80f };
        public float streak_rate = 3f;
        public float streak_lifetime = 0.25f;
        public float particle_rate = 5f;
        public float particle_lifetime = 0.4f;
        public float[] particle_size_range = { 3f, 7f };
    }

    [Serializable]
    public class AuraColorConfig
    {
        public int[] boost = { 80, 160, 255 };
        public int[] speed_boost = { 255, 200, 80 };
        public int[] invincibility = { 255, 255, 200 };
        public int[] chaos = { 255, 60, 60 };
        public int[] slow = { 60, 180, 60 };
    }

    [Serializable]
    public class AuraVisualConfig
    {
        public bool enabled = true;
        public float pulse_speed = 3f;
        public float base_radius_padding = 15f;
        public float pulse_amplitude = 8f;
        public int base_alpha = 50;
        public float ring_width = 3f;
        public AuraColorConfig colors = new AuraColorConfig();
        public int sparkle_count = 8;
        public float sparkle_speed = 2.5f;
        public float sparkle_size = 4f;
    }

    [Serializable]
    public class StatusIndicatorVisualConfig
    {
        public bool enabled = true;
        public float bar_width = 50f;
        public float bar_height = 6f;
        public float bar_offset_y = -12f;
        public float icon_size = 24f;
        public float icon_offset_y = -38f;
        public float icon_bob_speed = 2f;
        public float icon_bob_amplitude = 3f;
    }

    [Serializable]
    public class SnackGlowVisualConfig
    {
        public bool enabled = true;
        public string[] powerup_snack_ids = { "bone", "red_bull", "steak" };
        public float glow_radius_padding = 12f;
        public float glow_pulse_speed = 3.5f;
        public int glow_base_alpha = 40;
        public int glow_pulse_alpha = 30;
        public int sparkle_count = 6;
        public float sparkle_orbit_speed = 2f;
        public float sparkle_orbit_radius = 20f;
        public float sparkle_size = 3f;
        public bool beam_enabled = true;
        public float beam_width = 4f;
        public float beam_height = 30f;
        public int beam_alpha = 60;
    }

    [Serializable]
    public class PickupFlashVisualConfig
    {
        public bool enabled = true;
        public float duration = 0.3f;
        public int max_alpha = 80;
        public float ring_expand_speed = 300f;
        public float ring_max_radius = 60f;
    }

    [Serializable]
    public class PowerupVisualsConfig
    {
        public WingsVisualConfig wings = new WingsVisualConfig();
        public SpeedStreaksVisualConfig speed_streaks = new SpeedStreaksVisualConfig();
        public AuraVisualConfig aura = new AuraVisualConfig();
        public StatusIndicatorVisualConfig status_indicator = new StatusIndicatorVisualConfig();
        public SnackGlowVisualConfig snack_glow = new SnackGlowVisualConfig();
        public PickupFlashVisualConfig pickup_flash = new PickupFlashVisualConfig();
    }

    [Serializable]
    public class LevelObstacleConfig
    {
        public string type;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public class LevelConfigData
    {
        public int level_number = 1;
        public string name = "Level";
        public int[] background_color = { 200, 200, 200 };
        public float round_duration_seconds = 60f;
        public float spawn_rate_multiplier = 1f;
        public string[] snack_pool;
        public LevelObstacleConfig[] obstacles;
    }

    [Serializable]
    public class LevelsConfig
    {
        public LevelConfigData[] levels;

        public int Count => levels == null ? 0 : levels.Length;

        public LevelConfigData GetLevel(int levelNumber)
        {
            if (levels == null || levels.Length == 0)
                return null;

            for (int i = 0; i < levels.Length; i++)
            {
                LevelConfigData level = levels[i];
                if (level != null && level.level_number == levelNumber)
                    return level;
            }

            return null;
        }

        public LevelConfigData GetByRoundProgression(int roundNumber)
        {
            if (levels == null || levels.Length == 0)
                return null;

            int safeRound = Mathf.Max(1, roundNumber);
            LevelConfigData exact = GetLevel(safeRound);
            if (exact != null)
                return exact;

            int index = Mathf.Clamp(safeRound - 1, 0, levels.Length - 1);
            return levels[index];
        }
    }

    public static class RuntimeConfigUtil
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToLowerInvariant();
        }

        public static Color ColorFromRgb255(int[] rgb, Color fallback)
        {
            if (rgb == null || rgb.Length < 3)
                return fallback;

            return new Color(
                Mathf.Clamp01(rgb[0] / 255f),
                Mathf.Clamp01(rgb[1] / 255f),
                Mathf.Clamp01(rgb[2] / 255f),
                fallback.a);
        }

        public static KeyCode KeyCodeFromConfig(string keyName, KeyCode fallback)
        {
            string normalized = Normalize(keyName);
            if (string.IsNullOrEmpty(normalized))
                return fallback;

            switch (normalized)
            {
                case "up":
                    return KeyCode.UpArrow;
                case "down":
                    return KeyCode.DownArrow;
                case "left":
                    return KeyCode.LeftArrow;
                case "right":
                    return KeyCode.RightArrow;
                case "return":
                case "enter":
                    return KeyCode.Return;
                case "escape":
                case "esc":
                    return KeyCode.Escape;
                case "backspace":
                    return KeyCode.Backspace;
                case "space":
                    return KeyCode.Space;
            }

            if (normalized.Length == 1)
            {
                char c = normalized[0];
                if (c >= 'a' && c <= 'z')
                {
                    string enumKey = char.ToUpperInvariant(c).ToString();
                    if (Enum.TryParse(enumKey, true, out KeyCode alphaKey))
                        return alphaKey;
                }
            }

            if (Enum.TryParse(normalized, true, out KeyCode parsed))
                return parsed;

            return fallback;
        }
    }
}