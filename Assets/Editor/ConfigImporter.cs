using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System;
using SnackAttack.Config;

namespace SnackAttack.EditorScripts
{
    public class ConfigImporter
    {
        [MenuItem("SnackAttack/Import JSON Configs")]
        public static void ImportConfigs()
        {
            string configPath = Application.dataPath + "/Resources/Config/";

            // 1. Import Game Settings
            string gsJsonPath = configPath + "game_settings.json";
            if (File.Exists(gsJsonPath))
            {
                string json = File.ReadAllText(gsJsonPath);
                GameSettingsData rawData = JsonUtility.FromJson<GameSettingsData>(json);

                GameSettingsSO so = ScriptableObject.CreateInstance<GameSettingsSO>();
                if (rawData != null && rawData.gameplay != null)
                {
                    so.timeLimitPerRoundSeconds = rawData.gameplay.round_duration_seconds;
                    so.roundsToWin = rawData.gameplay.rounds_per_game;
                }

                CreateAsset(so, "Assets/Resources/ConfigSO/GameSettings.asset");
            }

            // 2. Import Characters
            string charJsonPath = configPath + "characters.json";
            if (File.Exists(charJsonPath))
            {
                string json = File.ReadAllText(charJsonPath);
                CharacterList rawList = JsonUtility.FromJson<CharacterList>(json);

                CharacterConfigSO so = ScriptableObject.CreateInstance<CharacterConfigSO>();
                so.characters = new List<CharacterData>();
                
                if (rawList != null && rawList.characters != null)
                {
                    foreach (var rawChar in rawList.characters)
                    {
                        CharacterData data = new CharacterData();
                        data.characterId = rawChar.id;
                        data.characterName = rawChar.name;
                        data.description = "Breed: " + rawChar.breed; 
                        data.speedMultiplier = rawChar.base_speed;
                        // Default hitbox width approx to collisionRadius
                        if (rawChar.hitbox != null && rawChar.hitbox.Length > 0)
                            data.collisionRadius = rawChar.hitbox[0] / 100f; 
                        
                        // Load sprites based on name
                        string profilePath = $"Assets/Sprites/{rawChar.name}.png";
                        string idlePath = $"Assets/Sprites/{rawChar.name} walking.png";
                        data.profileImage = AssetDatabase.LoadAssetAtPath<Sprite>(profilePath);
                        data.idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(idlePath);

                        so.characters.Add(data);
                    }
                }

                CreateAsset(so, "Assets/Resources/ConfigSO/CharacterConfig.asset");
            }

            // 3. Import Snacks
            string snacksJsonPath = configPath + "snacks.json";
            if (File.Exists(snacksJsonPath))
            {
                string json = File.ReadAllText(snacksJsonPath);
                SnackList rawList = JsonUtility.FromJson<SnackList>(json);

                SnackConfigSO so = ScriptableObject.CreateInstance<SnackConfigSO>();
                so.snacks = new List<SnackData>();

                if (rawList != null && rawList.snacks != null)
                {
                    foreach (var rawSnack in rawList.snacks)
                    {
                        SnackData data = new SnackData();
                        data.snackId = rawSnack.id;
                        data.snackName = rawSnack.name;
                        data.points = rawSnack.point_value;
                        data.spawnWeight = rawSnack.spawn_weight;

                        // Parse effect directly from JSON to stay in sync with Python source.
                        data.effectType = MapEffectType(rawSnack.effect != null ? rawSnack.effect.type : null);
                        data.effectMagnitude = rawSnack.effect != null ? rawSnack.effect.magnitude : 1f;
                        data.effectDuration = rawSnack.effect != null ? rawSnack.effect.duration_seconds : 0f;

                        so.snacks.Add(data);
                    }
                }

                CreateAsset(so, "Assets/Resources/ConfigSO/SnackConfig.asset");
            }

            // 4. Ensure OpenRouter config exists (manual key management via Inspector)
            // Do not overwrite existing asset to avoid wiping saved API key.
            const string openRouterPath = "Assets/Resources/ConfigSO/OpenRouterConfig.asset";
            OpenRouterConfigSO existingOpenRouter = AssetDatabase.LoadAssetAtPath<OpenRouterConfigSO>(openRouterPath);
            if (existingOpenRouter == null)
            {
                OpenRouterConfigSO openRouterConfig = ScriptableObject.CreateInstance<OpenRouterConfigSO>();
                CreateAsset(openRouterConfig, openRouterPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Successfully imported JSON configs to ScriptableObjects!");
        }

        private static void CreateAsset(ScriptableObject asset, string path)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/ConfigSO"))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "ConfigSO");
            }

            ScriptableObject existingAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existingAsset != null)
            {
                EditorUtility.CopySerialized(asset, existingAsset);
            }
            else
            {
                AssetDatabase.CreateAsset(asset, path);
            }
        }

        private static SnackEffectType MapEffectType(string effectType)
        {
            if (string.IsNullOrWhiteSpace(effectType))
                return SnackEffectType.None;

            string key = effectType.Trim().ToLowerInvariant();
            switch (key)
            {
                case "speed_boost":
                    return SnackEffectType.SpeedBoost;
                case "invincibility":
                    return SnackEffectType.Invincibility;
                case "slow":
                    return SnackEffectType.Slow;
                case "chaos":
                    return SnackEffectType.Chaos;
                case "boost":
                    return SnackEffectType.Boost;
                default:
                    Debug.LogWarning($"Unknown snack effect type '{effectType}', defaulting to None.");
                    return SnackEffectType.None;
            }
        }

        // --- Helper classes for JSON Deserialization ---
        [System.Serializable]
        private class GameSettingsData
        {
            public GameplayData gameplay;
        }

        [System.Serializable]
        private class GameplayData
        {
            public int round_duration_seconds;
            public int rounds_per_game;
        }

        [System.Serializable]
        private class CharacterList
        {
            public List<RawCharacter> characters;
        }

        [System.Serializable]
        private class RawCharacter
        {
            public string id;
            public string name;
            public string breed;
            public float base_speed;
            public float[] hitbox;
        }

        [System.Serializable]
        private class SnackList
        {
            public List<RawSnack> snacks;
        }

        [System.Serializable]
        private class RawSnack
        {
            public string id;
            public string name;
            public int point_value;
            public float spawn_weight;
            public RawSnackEffect effect;
        }

        [System.Serializable]
        private class RawSnackEffect
        {
            public string type;
            public float magnitude;
            public float duration_seconds;
        }
    }
}
