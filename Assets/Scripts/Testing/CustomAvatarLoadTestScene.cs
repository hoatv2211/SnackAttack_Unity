using System.Collections.Generic;
using System.IO;
using SnackAttack.Config;
using SnackAttack.Gameplay;
using UnityEngine;

namespace SnackAttack.Testing
{
    public class CustomAvatarLoadTestScene : MonoBehaviour
    {
        [Header("Spawn")]
        public GameObject characterTemplatePrefab;
        public Transform spawnPoint;
        public string characterId;
        public bool autoPickLatestIfEmpty = true;
        public Vector3 defaultSpawnPosition = new Vector3(0f, -3f, 0f);

        [Header("Preview")]
        public bool createProfilePreview = true;
        public Vector3 profilePreviewOffset = new Vector3(-3.5f, 0f, 0f);

        [Header("Input")]
        public KeyCode toggleMoveKey = KeyCode.M;
        public KeyCode triggerEatKey = KeyCode.E;
        public KeyCode toggleBoostKey = KeyCode.B;
        public KeyCode toggleAirborneKey = KeyCode.F;
        public KeyCode nextAvatarKey = KeyCode.Period;
        public KeyCode previousAvatarKey = KeyCode.Comma;

        [Header("Sample Data")]
        public bool seedFromSpriteSheetsIfEmpty = true;

        public List<string> availableIds = new List<string>();
        private int selectedIndex = -1;

        private GameObject activeCharacter;
        private RuntimeSpriteAnimationPlayer animationPlayer;
        private RuntimeCharacterDefinition currentDefinition;
        private SpriteRenderer profilePreviewRenderer;

        private bool isMoving;
        private bool isBoost;
        private bool isAirborne;

        private void Start()
        {
            EnsureCamera();
            RuntimeCharacterRegistry.EnsureLoadedFromDisk();
            RefreshAvailableIds();

#if UNITY_EDITOR
            if (availableIds.Count == 0 && seedFromSpriteSheetsIfEmpty)
            {
                int seeded = SeedSampleDataFromSpriteSheets(Path.Combine(Application.persistentDataPath, "custom_avatars"));
                if (seeded > 0)
                {
                    RuntimeCharacterRegistry.EnsureLoadedFromDisk();
                    RefreshAvailableIds();
                }
            }
#endif

            if (availableIds.Count == 0)
            {
                Debug.LogWarning("CustomAvatarLoadTestScene: No runtime avatars found in persistentDataPath/custom_avatars.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(characterId))
            {
                int idx = availableIds.FindIndex(id => id == characterId.Trim().ToLowerInvariant());
                selectedIndex = idx >= 0 ? idx : 0;
            }
            else
            {
                selectedIndex = autoPickLatestIfEmpty ? availableIds.Count - 1 : 0;
            }

            LoadSelectedAvatar();
        }

        private void Update()
        {
            if (animationPlayer == null)
                return;

            if (Input.GetKeyDown(toggleMoveKey))
            {
                isMoving = !isMoving;
                animationPlayer.SetMoving(isMoving);
            }

            if (Input.GetKeyDown(toggleBoostKey))
            {
                isBoost = !isBoost;
                animationPlayer.SetBoost(isBoost);
            }

            if (Input.GetKeyDown(toggleAirborneKey))
            {
                isAirborne = !isAirborne;
                animationPlayer.SetAirborne(isAirborne);
            }

            if (Input.GetKeyDown(triggerEatKey))
                animationPlayer.TriggerEat();

            if (Input.GetKeyDown(nextAvatarKey))
                CycleAvatar(1);

            if (Input.GetKeyDown(previousAvatarKey))
                CycleAvatar(-1);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 560, 220), GUI.skin.box);
            GUILayout.Label("Custom Avatar Runtime Load Test");
            GUILayout.Label("Persistent path: " + Application.persistentDataPath + "/custom_avatars");
            GUILayout.Label("Loaded avatar: " + (currentDefinition != null ? currentDefinition.id : "none"));
            GUILayout.Label("States: Move=" + isMoving + " Boost=" + isBoost + " Airborne=" + isAirborne);
            GUILayout.Space(6);
            GUILayout.Label("Keys: M(toggle move), E(eat), B(toggle boost), F(toggle airborne), ,/. switch avatar");
            GUILayout.Label("Available avatars: " + string.Join(", ", availableIds));
            GUILayout.EndArea();
        }

        private void RefreshAvailableIds()
        {
            availableIds.Clear();
            List<RuntimeCharacterDefinition> defs = RuntimeCharacterRegistry.GetAll();
            for (int i = 0; i < defs.Count; i++)
            {
                RuntimeCharacterDefinition d = defs[i];
                if (d != null && !string.IsNullOrWhiteSpace(d.id))
                    availableIds.Add(d.id);
            }
        }

        private void CycleAvatar(int direction)
        {
            if (availableIds.Count == 0)
                return;

            selectedIndex += direction;
            if (selectedIndex < 0)
                selectedIndex = availableIds.Count - 1;
            if (selectedIndex >= availableIds.Count)
                selectedIndex = 0;

            LoadSelectedAvatar();
        }

        private void LoadSelectedAvatar()
        {
            if (availableIds.Count == 0 || selectedIndex < 0 || selectedIndex >= availableIds.Count)
                return;

            string id = availableIds[selectedIndex];
            if (!RuntimeCharacterRegistry.TryGetOrLoad(id, out RuntimeCharacterDefinition definition) || definition == null)
            {
                Debug.LogError("CustomAvatarLoadTestScene: Failed to load runtime definition for id: " + id);
                return;
            }

            currentDefinition = definition;
            characterId = id;

            if (activeCharacter != null)
                Destroy(activeCharacter);

            if (characterTemplatePrefab == null)
            {
                Debug.LogError("CustomAvatarLoadTestScene: Assign characterTemplatePrefab in Inspector.");
                return;
            }

            Vector3 position = spawnPoint != null ? spawnPoint.position : defaultSpawnPosition;
            activeCharacter = Instantiate(characterTemplatePrefab, position, Quaternion.identity);
            activeCharacter.name = "TestAvatar_" + id;

            SpriteRenderer renderer = activeCharacter.GetComponentInChildren<SpriteRenderer>();
            Animator animator = activeCharacter.GetComponentInChildren<Animator>();

            animationPlayer = activeCharacter.GetComponent<RuntimeSpriteAnimationPlayer>();
            if (animationPlayer == null)
                animationPlayer = activeCharacter.AddComponent<RuntimeSpriteAnimationPlayer>();

            animationPlayer.Initialize(definition, renderer);
            animationPlayer.SetMoving(false);
            animationPlayer.SetBoost(false);
            animationPlayer.SetAirborne(false);

            isMoving = false;
            isBoost = false;
            isAirborne = false;

            if (animator != null)
                animator.enabled = false;

            UpdateProfilePreview(definition);
            Debug.Log("CustomAvatarLoadTestScene: Loaded avatar from disk id=" + id);
        }

        private void UpdateProfilePreview(RuntimeCharacterDefinition definition)
        {
            if (!createProfilePreview)
                return;

            if (profilePreviewRenderer == null)
            {
                GameObject preview = new GameObject("ProfilePreview");
                preview.transform.position = (spawnPoint != null ? spawnPoint.position : defaultSpawnPosition) + profilePreviewOffset;
                profilePreviewRenderer = preview.AddComponent<SpriteRenderer>();
                profilePreviewRenderer.sortingOrder = 100;
            }

            profilePreviewRenderer.sprite = definition != null ? definition.profileSprite : null;
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
                return;

            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 0f, -10f);
        }

#if UNITY_EDITOR
        private static int SeedSampleDataFromSpriteSheets(string destinationRoot)
        {
            string sourceRoot = Path.Combine(Application.dataPath, "Sprite sheets");
            if (!Directory.Exists(sourceRoot))
                return 0;

            Directory.CreateDirectory(destinationRoot);

            var map = new[]
            {
                new { id = "biggie", profile = Path.Combine("Profile", "Biggie.png"), run = "Biggie run sprite.png", eat = "Biggie eat_attack.png", walk = "Biggie walking.png", boost = "biggie_face_camera_flight.png" },
                new { id = "dash", profile = Path.Combine("Profile", "Dash.png"), run = "Dash run sprite.png", eat = "Dash eat_attack sprite.png", walk = "Dash walking.png", boost = "dash_face_camera_flight.png" },
                new { id = "jazzy", profile = Path.Combine("Profile", "Jazzy.png"), run = "Jazzy run sprite.png", eat = "Jazzy eat_attack sprite.png", walk = "Jazzy walking.png", boost = "jazzy_face_camera_flight.png" },
                new { id = "prissy", profile = Path.Combine("Profile", "Prissy.png"), run = "Prissy run sprite.png", eat = "Prissy eat_attack sprite.png", walk = "Prissy walking.png", boost = "prissy_face_camera_flight.png" },
                new { id = "rex", profile = Path.Combine("Profile", "Rex.png"), run = "Rex run sprite.png", eat = "Rex eat_attack sprite.png", walk = "Rex walking.png", boost = "rex_face_camera_flight.png" },
                new { id = "snowy", profile = Path.Combine("Profile", "Snowy.png"), run = "Snowy run sprite.png", eat = "Snowy eat_attack sprite.png", walk = "Snowy walking.png", boost = "snowy_face_camera_flight.png" },
            };

            int copied = 0;
            for (int i = 0; i < map.Length; i++)
            {
                string targetDir = Path.Combine(destinationRoot, map[i].id);
                Directory.CreateDirectory(targetDir);

                bool ok = CopyIfExists(Path.Combine(sourceRoot, map[i].profile), Path.Combine(targetDir, "profile.png"));
                ok &= CopyIfExists(Path.Combine(sourceRoot, map[i].run), Path.Combine(targetDir, "run.png"));
                CopyIfExists(Path.Combine(sourceRoot, map[i].eat), Path.Combine(targetDir, "eat.png"));
                CopyIfExists(Path.Combine(sourceRoot, map[i].walk), Path.Combine(targetDir, "walk.png"));
                CopyIfExists(Path.Combine(sourceRoot, map[i].boost), Path.Combine(targetDir, "boost.png"));

                if (ok)
                    copied++;
            }

            return copied;
        }

        private static bool CopyIfExists(string src, string dst)
        {
            if (!File.Exists(src))
                return false;

            File.Copy(src, dst, true);
            return true;
        }
#endif
    }
}
