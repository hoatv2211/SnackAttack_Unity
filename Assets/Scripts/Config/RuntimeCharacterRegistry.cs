using System;
using System.Collections.Generic;
using System.IO;
using SnackAttack.Gameplay;
using SnackAttack.Services;
using UnityEngine;

namespace SnackAttack.Config
{
    public static class RuntimeCharacterRegistry
    {
        private static readonly string[] RequiredAvatarFiles =
        {
            "profile.png",
            "run.png",
            "eat.png",
            "walk.png",
            "boost.png",
        };

        private static readonly Dictionary<string, RuntimeCharacterDefinition> ById =
            new Dictionary<string, RuntimeCharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        private static bool loadedFromDisk;

        public static event Action RegistryChanged;

        public static void Register(RuntimeCharacterDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                return;

            string id = definition.id.Trim().ToLowerInvariant();
            definition.id = id;
            ById[id] = definition;
            RegistryChanged?.Invoke();
        }

        public static bool TryGet(string id, out RuntimeCharacterDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            return ById.TryGetValue(id.Trim().ToLowerInvariant(), out definition);
        }

        public static bool TryGetOrLoad(string id, out RuntimeCharacterDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            string normalizedId = id.Trim().ToLowerInvariant();
            if (ById.TryGetValue(normalizedId, out definition))
                return true;

            EnsureCharacterDefaultsFromStreamingAssets(normalizedId);

            if (!RuntimeSpriteSheetLoader.TryLoadDefinitionFromPersistentPath(normalizedId, out definition) || definition == null)
                return false;

            ById[normalizedId] = definition;
            RegistryChanged?.Invoke();
            return true;
        }

        public static List<RuntimeCharacterDefinition> GetAll()
        {
            List<RuntimeCharacterDefinition> list = new List<RuntimeCharacterDefinition>(ById.Values);
            list.Sort((a, b) => a.createdAtTicks.CompareTo(b.createdAtTicks));
            return list;
        }

        public static void EnsureLoadedFromDisk()
        {
            if (loadedFromDisk)
                return;

            loadedFromDisk = true;

            EnsureDefaultsFromStreamingAssets();

            string root = Path.Combine(Application.persistentDataPath, "custom_avatars");
            if (!Directory.Exists(root))
                return;

            string[] characterDirs;
            try
            {
                characterDirs = Directory.GetDirectories(root);
            }
            catch
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < characterDirs.Length; i++)
            {
                string dir = characterDirs[i];
                string id = Path.GetFileName(dir);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                RuntimeCharacterDefinition definition;
                if (!RuntimeSpriteSheetLoader.TryLoadDefinitionFromFolder(id, dir, out definition))
                    continue;

                if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                    continue;

                string normalizedId = definition.id.Trim().ToLowerInvariant();
                if (ById.ContainsKey(normalizedId))
                    continue;

                ById[normalizedId] = definition;
                changed = true;
            }

            if (changed)
                RegistryChanged?.Invoke();
        }

        private static void EnsureDefaultsFromStreamingAssets()
        {
            string sourceRoot = Path.Combine(Application.streamingAssetsPath, "custom_avatars");
            string targetRoot = Path.Combine(Application.persistentDataPath, "custom_avatars");

            if (!Directory.Exists(sourceRoot))
                return;

            try
            {
                Directory.CreateDirectory(targetRoot);
                string[] sourceCharacterDirs = Directory.GetDirectories(sourceRoot);
                for (int i = 0; i < sourceCharacterDirs.Length; i++)
                    CopyMissingAvatarFiles(sourceCharacterDirs[i], Path.Combine(targetRoot, Path.GetFileName(sourceCharacterDirs[i])));
            }
            catch
            {
                // Ignore sync errors and continue with whatever is available in persistent path.
            }
        }

        private static void EnsureCharacterDefaultsFromStreamingAssets(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            string sourceDir = Path.Combine(Application.streamingAssetsPath, "custom_avatars", characterId);
            string targetDir = Path.Combine(Application.persistentDataPath, "custom_avatars", characterId);
            CopyMissingAvatarFiles(sourceDir, targetDir);
        }

        private static void CopyMissingAvatarFiles(string sourceDir, string targetDir)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir) || string.IsNullOrWhiteSpace(targetDir))
                return;

            try
            {
                Directory.CreateDirectory(targetDir);
                for (int i = 0; i < RequiredAvatarFiles.Length; i++)
                {
                    string fileName = RequiredAvatarFiles[i];
                    string sourceFile = Path.Combine(sourceDir, fileName);
                    string targetFile = Path.Combine(targetDir, fileName);
                    if (!File.Exists(sourceFile) || File.Exists(targetFile))
                        continue;

                    File.Copy(sourceFile, targetFile, false);
                }
            }
            catch
            {
                // Ignore sync errors and continue runtime loading.
            }
        }
    }
}
