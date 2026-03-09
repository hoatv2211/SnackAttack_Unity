#if UNITY_EDITOR
using SnackAttack.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateCustomAvatarLoadTestScene
{
    [MenuItem("SnackAttack/Create Custom Avatar Load Test Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = new GameObject("CustomAvatarLoadTestRoot");
        CustomAvatarLoadTestScene test = root.AddComponent<CustomAvatarLoadTestScene>();

        // Try to auto-assign a reasonable template prefab from CharacterPrefab list if found.
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            if (prefab.GetComponentInChildren<SpriteRenderer>(true) != null)
            {
                test.characterTemplatePrefab = prefab;
                break;
            }
        }

        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.position = new Vector3(0f, -3f, 0f);
        test.spawnPoint = spawn.transform;

        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camObj.transform.position = new Vector3(0f, 0f, -10f);
        camObj.tag = "MainCamera";

        string scenePath = "Assets/Scenes/CustomAvatarLoadTest.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Custom Avatar Test Scene",
            "Created scene at: " + scenePath + "\n\nOpen it and press Play to test loading from persistentDataPath/custom_avatars.",
            "OK");
    }
}
#endif
