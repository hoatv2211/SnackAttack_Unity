#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class SeedCustomAvatarSampleData
{
    private const string SourceRoot = "Assets/Sprite sheets";

    [MenuItem("SnackAttack/Seed Sample Custom Avatars To PersistentDataPath")]
    public static void SeedToPersistentDataPath()
    {
        string destinationRoot = Path.Combine(Application.persistentDataPath, "custom_avatars");
        int count = Seed(destinationRoot);

        EditorUtility.DisplayDialog(
            "Seed Sample Custom Avatars",
            $"Seeded {count} avatar folders to:\n{destinationRoot}",
            "OK");
    }

    public static int Seed(string destinationRoot)
    {
        if (!Directory.Exists(SourceRoot))
        {
            Debug.LogError("SeedCustomAvatarSampleData: Source folder not found: " + SourceRoot);
            return 0;
        }

        Directory.CreateDirectory(destinationRoot);

        string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceRoot });
        Dictionary<string, AvatarSourceSet> byId = new Dictionary<string, AvatarSourceSet>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < pngGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(pngGuids[i]);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                continue;

            string fileName = Path.GetFileName(assetPath);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            string id = ParseCharacterId(fileName);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!byId.TryGetValue(id, out AvatarSourceSet set))
            {
                set = new AvatarSourceSet();
                byId[id] = set;
            }

            string lower = fileName.ToLowerInvariant();
            if (assetPath.IndexOf("/Profile/", StringComparison.OrdinalIgnoreCase) >= 0)
                set.profile = assetPath;
            else if (lower.Contains("face_camera_flight"))
                set.boost = assetPath;
            else if (lower.Contains("run"))
                set.run = assetPath;
            else if (lower.Contains("eat") || lower.Contains("attack"))
                set.eat = assetPath;
            else if (lower.Contains("walking") || lower.Contains("walk"))
                set.walk = assetPath;
            else if (lower.Contains("boost") || lower.Contains("wing"))
                set.boost = assetPath;
        }

        int copiedCount = 0;
        foreach (var kv in byId)
        {
            string id = kv.Key;
            AvatarSourceSet set = kv.Value;

            if (string.IsNullOrWhiteSpace(set.profile) || string.IsNullOrWhiteSpace(set.run))
                continue;

            string targetDir = Path.Combine(destinationRoot, id);
            Directory.CreateDirectory(targetDir);

            CopyAssetFile(set.profile, Path.Combine(targetDir, "profile.png"));
            CopyAssetFile(set.run, Path.Combine(targetDir, "run.png"));
            CopyAssetFile(!string.IsNullOrWhiteSpace(set.eat) ? set.eat : set.run, Path.Combine(targetDir, "eat.png"));
            CopyAssetFile(!string.IsNullOrWhiteSpace(set.walk) ? set.walk : set.run, Path.Combine(targetDir, "walk.png"));
            CopyAssetFile(!string.IsNullOrWhiteSpace(set.boost) ? set.boost : set.profile, Path.Combine(targetDir, "boost.png"));

            copiedCount++;
        }

        Debug.Log($"SeedCustomAvatarSampleData: Seeded {copiedCount} avatar folders to {destinationRoot}");
        return copiedCount;
    }

    private static void CopyAssetFile(string assetPath, string destinationPath)
    {
        string fullSourcePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullSourcePath))
            return;

        File.Copy(fullSourcePath, destinationPath, true);
    }

    private static string ParseCharacterId(string fileName)
    {
        string withoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(withoutExt))
            return string.Empty;

        Match m = Regex.Match(withoutExt, "^[A-Za-z]+");
        if (!m.Success)
            return string.Empty;

        string raw = m.Value.Trim().ToLowerInvariant();
        if (raw == "boost" || raw == "wings")
            return string.Empty;

        return raw;
    }

    private class AvatarSourceSet
    {
        public string profile;
        public string run;
        public string eat;
        public string walk;
        public string boost;
    }
}
#endif
