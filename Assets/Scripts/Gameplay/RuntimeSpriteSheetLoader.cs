using System;
using System.Collections.Generic;
using System.IO;
using SnackAttack.Config;
using SnackAttack.Services;
using UnityEngine;

namespace SnackAttack.Gameplay
{
    public static class RuntimeSpriteSheetLoader
    {
        public static bool TryLoadDefinitionFromPersistentPath(string characterId, out RuntimeCharacterDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(characterId))
                return false;

            string normalizedId = characterId.Trim().ToLowerInvariant();
            string folder = Path.Combine(Application.persistentDataPath, "custom_avatars", normalizedId);
            return TryLoadDefinitionFromFolder(normalizedId, folder, out definition);
        }

        public static bool TryLoadDefinitionFromFolder(string characterId, string folderPath, out RuntimeCharacterDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return false;

            string profile = Path.Combine(folderPath, "profile.png");
            if (!File.Exists(profile))
                return false;

            AvatarGenerationResult result = new AvatarGenerationResult
            {
                success = true,
                characterId = characterId,
                displayName = characterId,
                profilePath = profile,
                runSpritePath = Path.Combine(folderPath, "run.png"),
                eatSpritePath = Path.Combine(folderPath, "eat.png"),
                walkSpritePath = Path.Combine(folderPath, "walk.png"),
                boostSpritePath = Path.Combine(folderPath, "boost.png"),
            };

            definition = BuildDefinition(result);
            return definition != null;
        }

        public static RuntimeCharacterDefinition BuildDefinition(AvatarGenerationResult result)
        {
            if (result == null || !result.success || string.IsNullOrWhiteSpace(result.characterId))
                return null;

            RuntimeCharacterDefinition def = new RuntimeCharacterDefinition
            {
                id = result.characterId,
                displayName = result.displayName,
                profilePath = result.profilePath,
                runSpritePath = result.runSpritePath,
                eatSpritePath = result.eatSpritePath,
                walkSpritePath = result.walkSpritePath,
                boostSpritePath = result.boostSpritePath,
                createdAtTicks = DateTime.UtcNow.Ticks,
            };

            def.profileSprite = LoadSingleSprite(def.profilePath);
            def.runFrames = SliceHorizontal(def.runSpritePath, 3);
            def.eatFrames = SliceHorizontal(def.eatSpritePath, 3);
            def.walkFrames = SliceHorizontal(def.walkSpritePath, 5);
            def.boostFrames = SliceHorizontal(def.boostSpritePath, 1);

            return def;
        }

        public static Sprite LoadSingleSprite(string path)
        {
            Texture2D texture = LoadTexture(path);
            if (texture == null)
                return null;

            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        public static Sprite[] SliceHorizontal(string path, int frameCount)
        {
            if (frameCount <= 0)
                return Array.Empty<Sprite>();

            Texture2D texture = LoadTexture(path);
            if (texture == null)
                return Array.Empty<Sprite>();

            int frameWidth = Mathf.Max(1, texture.width / frameCount);
            List<Sprite> sprites = new List<Sprite>(frameCount);

            for (int i = 0; i < frameCount; i++)
            {
                int x = Mathf.Clamp(i * frameWidth, 0, texture.width - 1);
                int w = i == frameCount - 1 ? texture.width - x : frameWidth;
                Rect rect = new Rect(x, 0, Mathf.Max(1, w), texture.height);
                sprites.Add(Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f));
            }

            return sprites.ToArray();
        }

        private static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return tex.LoadImage(bytes) ? tex : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
