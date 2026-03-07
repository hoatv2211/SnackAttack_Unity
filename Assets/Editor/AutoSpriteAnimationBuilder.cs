using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SnackAttack.EditorScripts
{
    public static class AutoSpriteAnimationBuilder
    {
        private const string DefaultConfigPath = "Assets/Editor/Generated/auto_anim_config.json";

        private static readonly string[] StateOrder =
        {
            "Idle",
            "Run",
            "Eat",
            "Chili",
            "Fly",
            "Boost",
            "Walk",
        };

        [MenuItem("SnackAttack/Animations/Build From Generated Config")]
        public static void BuildFromGeneratedConfigMenu()
        {
            string configPath = ResolveConfigPathFromArgsOrDefault(DefaultConfigPath);
            int built = BuildFromConfig(configPath);
            Debug.Log($"SnackAttack: Animation build complete. Controllers built: {built}");
        }

        public static void BuildFromGeneratedConfigBatchMode()
        {
            string configPath = ResolveConfigPathFromArgsOrDefault(DefaultConfigPath);

            try
            {
                int built = BuildFromConfig(configPath);
                Debug.Log($"SnackAttack: Animation build complete. Controllers built: {built}");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("SnackAttack: Animation build failed.");
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static int BuildFromConfig(string configPath)
        {
            string normalizedConfigPath = NormalizeAssetPath(configPath);
            string absoluteConfigPath = ToAbsolutePath(normalizedConfigPath);

            if (!File.Exists(absoluteConfigPath))
            {
                throw new FileNotFoundException($"Config not found: {normalizedConfigPath}", absoluteConfigPath);
            }

            string json = File.ReadAllText(absoluteConfigPath);
            BuildConfig config = JsonUtility.FromJson<BuildConfig>(json);
            if (config == null || config.characters == null || config.characters.Count == 0)
            {
                throw new InvalidOperationException("Config is empty or invalid.");
            }

            int builtControllerCount = 0;

            foreach (CharacterBuild character in config.characters)
            {
                if (character == null || string.IsNullOrWhiteSpace(character.name))
                {
                    Debug.LogWarning("SnackAttack: Skipped character with empty name.");
                    continue;
                }

                if (character.clips == null || character.clips.Count == 0)
                {
                    Debug.LogWarning($"SnackAttack: {character.name} has no clips. Skipped.");
                    continue;
                }

                string controllerPath = NormalizeAssetPath(character.controllerPath);
                if (string.IsNullOrWhiteSpace(controllerPath) || !controllerPath.StartsWith("Assets/"))
                {
                    Debug.LogWarning($"SnackAttack: Invalid controller path for {character.name}. Skipped.");
                    continue;
                }

                EnsureAssetFolderForAssetPath(controllerPath);
                AnimatorController controller = LoadOrCreateController(controllerPath);
                ResetController(controller);

                RemoveObsoleteCharacterClips(character);

                Dictionary<string, AnimationClip> clipsByState = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

                foreach (ClipBuild clipBuild in character.clips)
                {
                    if (clipBuild == null)
                    {
                        continue;
                    }

                    string clipPath = NormalizeAssetPath(clipBuild.clipPath);
                    if (string.IsNullOrWhiteSpace(clipPath) || !clipPath.StartsWith("Assets/"))
                    {
                        Debug.LogWarning($"SnackAttack: Invalid clip path for {character.name}/{clipBuild.state}. Skipped.");
                        continue;
                    }

                    EnsureAssetFolderForAssetPath(clipPath);

                    AnimationClip generatedClip = BuildClipFromTexture(clipBuild, config.defaultFps);
                    if (generatedClip == null)
                    {
                        Debug.LogWarning($"SnackAttack: Could not build clip {character.name}/{clipBuild.state}. Skipped.");
                        continue;
                    }

                    AnimationClip persistedClip = SaveOrUpdateClipAsset(generatedClip, clipPath);
                    clipsByState[clipBuild.state] = persistedClip;
                }

                if (clipsByState.Count == 0)
                {
                    Debug.LogWarning($"SnackAttack: No usable clips for {character.name}. Controller skipped.");
                    continue;
                }

                BuildControllerGraph(controller, character, clipsByState);
                EditorUtility.SetDirty(controller);
                builtControllerCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return builtControllerCount;
        }

        private static void BuildControllerGraph(
            AnimatorController controller,
            CharacterBuild character,
            Dictionary<string, AnimationClip> clipsByState)
        {
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>(StringComparer.OrdinalIgnoreCase);

            float y = 0f;
            foreach (string stateName in OrderStates(clipsByState.Keys))
            {
                AnimatorState state = stateMachine.AddState(stateName, new Vector3(320f, y, 0f));
                state.motion = clipsByState[stateName];
                states[stateName] = state;
                y += 85f;
            }

            AnimatorState defaultState = PickDefaultState(states, character.defaultState);
            stateMachine.defaultState = defaultState;

            AddCommonParameters(controller, states.Keys);
            AddLocomotionTransitions(states);
            AddTriggerTransition(stateMachine, states, defaultState, "Eat", "Eat");
            AddTriggerTransition(stateMachine, states, defaultState, "Chili", "Chili");
            AddBoostFlightTransitions(stateMachine, states, defaultState);
        }

        private static void AddCommonParameters(AnimatorController controller, IEnumerable<string> stateNames)
        {
            HashSet<string> set = new HashSet<string>(stateNames, StringComparer.OrdinalIgnoreCase);

            bool hasIdle = set.Contains("Idle");
            bool hasRun = set.Contains("Run");
            bool hasWalk = set.Contains("Walk");
            bool hasFly = set.Contains("Fly");
            bool hasBoost = set.Contains("Boost");
            bool hasEat = set.Contains("Eat");
            bool hasChili = set.Contains("Chili");

            if ((hasIdle && hasWalk) || (hasWalk && hasRun) || (hasIdle && hasRun))
            {
                controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            }

            if (hasFly || hasBoost)
            {
                controller.AddParameter("Fly", AnimatorControllerParameterType.Bool);
            }

            if (hasFly && hasBoost)
            {
                controller.AddParameter("IsAirborne", AnimatorControllerParameterType.Bool);
            }

            if (hasEat)
            {
                controller.AddParameter("Eat", AnimatorControllerParameterType.Trigger);
            }

            if (hasChili)
            {
                controller.AddParameter("Chili", AnimatorControllerParameterType.Trigger);
            }
        }

        private static void AddLocomotionTransitions(Dictionary<string, AnimatorState> states)
        {
            AnimatorState idle = GetState(states, "Idle");
            AnimatorState run = GetState(states, "Run");
            AnimatorState walk = GetState(states, "Walk");

            // Match Python runtime logic: move state is Run when available, else Walk fallback.
            AnimatorState moveState = run ?? walk;

            if (idle != null && moveState != null && idle != moveState)
            {
                AnimatorStateTransition idleToMove = idle.AddTransition(moveState);
                ConfigureInstantTransition(idleToMove);
                idleToMove.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

                AnimatorStateTransition moveToIdle = moveState.AddTransition(idle);
                ConfigureInstantTransition(moveToIdle);
                moveToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            }

            // Optional bridge when both walk and run are authored.
            if (walk != null && run != null && walk != run)
            {
                AnimatorStateTransition walkToRun = walk.AddTransition(run);
                ConfigureInstantTransition(walkToRun);
                walkToRun.hasExitTime = true;
                walkToRun.exitTime = 0.95f;

                AnimatorStateTransition runToWalk = run.AddTransition(walk);
                ConfigureInstantTransition(runToWalk);
                runToWalk.hasExitTime = true;
                runToWalk.exitTime = 0.95f;
            }
        }

        private static void AddTriggerTransition(
            AnimatorStateMachine stateMachine,
            Dictionary<string, AnimatorState> states,
            AnimatorState defaultState,
            string stateName,
            string triggerName)
        {
            AnimatorState targetState = GetState(states, stateName);
            if (targetState == null)
            {
                return;
            }

            AnimatorStateTransition anyToTarget = stateMachine.AddAnyStateTransition(targetState);
            ConfigureInstantTransition(anyToTarget);
            anyToTarget.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

            if (defaultState != null && defaultState != targetState)
            {
                AnimatorStateTransition targetToDefault = targetState.AddTransition(defaultState);
                targetToDefault.hasExitTime = true;
                targetToDefault.exitTime = 0.95f;
                targetToDefault.hasFixedDuration = true;
                targetToDefault.duration = 0.05f;
                targetToDefault.canTransitionToSelf = false;
            }
        }

        private static void AddBoostFlightTransitions(
            AnimatorStateMachine stateMachine,
            Dictionary<string, AnimatorState> states,
            AnimatorState defaultState)
        {
            AnimatorState fly = GetState(states, "Fly");
            AnimatorState boost = GetState(states, "Boost");

            if (fly == null && boost == null)
            {
                return;
            }

            if (fly != null && boost != null)
            {
                AnimatorStateTransition anyToFly = stateMachine.AddAnyStateTransition(fly);
                ConfigureInstantTransition(anyToFly);
                anyToFly.AddCondition(AnimatorConditionMode.If, 0f, "Fly");
                anyToFly.AddCondition(AnimatorConditionMode.If, 0f, "IsAirborne");

                AnimatorStateTransition anyToBoost = stateMachine.AddAnyStateTransition(boost);
                ConfigureInstantTransition(anyToBoost);
                anyToBoost.AddCondition(AnimatorConditionMode.If, 0f, "Fly");
                anyToBoost.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAirborne");

                AnimatorStateTransition boostToFly = boost.AddTransition(fly);
                ConfigureInstantTransition(boostToFly);
                boostToFly.AddCondition(AnimatorConditionMode.If, 0f, "Fly");
                boostToFly.AddCondition(AnimatorConditionMode.If, 0f, "IsAirborne");

                AnimatorStateTransition flyToBoost = fly.AddTransition(boost);
                ConfigureInstantTransition(flyToBoost);
                flyToBoost.AddCondition(AnimatorConditionMode.If, 0f, "Fly");
                flyToBoost.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAirborne");

                if (defaultState != null)
                {
                    if (defaultState != fly)
                    {
                        AnimatorStateTransition flyToDefault = fly.AddTransition(defaultState);
                        ConfigureInstantTransition(flyToDefault);
                        flyToDefault.AddCondition(AnimatorConditionMode.IfNot, 0f, "Fly");
                    }

                    if (defaultState != boost)
                    {
                        AnimatorStateTransition boostToDefault = boost.AddTransition(defaultState);
                        ConfigureInstantTransition(boostToDefault);
                        boostToDefault.AddCondition(AnimatorConditionMode.IfNot, 0f, "Fly");
                    }
                }

                return;
            }

            AnimatorState singleFlightState = fly ?? boost;
            AnimatorStateTransition anyToSingleFlight = stateMachine.AddAnyStateTransition(singleFlightState);
            ConfigureInstantTransition(anyToSingleFlight);
            anyToSingleFlight.AddCondition(AnimatorConditionMode.If, 0f, "Fly");

            if (defaultState != null && defaultState != singleFlightState)
            {
                AnimatorStateTransition singleFlightToDefault = singleFlightState.AddTransition(defaultState);
                ConfigureInstantTransition(singleFlightToDefault);
                singleFlightToDefault.AddCondition(AnimatorConditionMode.IfNot, 0f, "Fly");
            }
        }

        private static void ConfigureInstantTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
            transition.offset = 0f;
            transition.canTransitionToSelf = false;
        }

        private static AnimatorState PickDefaultState(
            Dictionary<string, AnimatorState> states,
            string preferred)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                AnimatorState preferredState = GetState(states, preferred);
                if (preferredState != null)
                {
                    return preferredState;
                }
            }

            foreach (string stateName in StateOrder)
            {
                AnimatorState candidate = GetState(states, stateName);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return states.Values.First();
        }

        private static IEnumerable<string> OrderStates(IEnumerable<string> stateNames)
        {
            return stateNames.OrderBy(stateName =>
            {
                int index = Array.IndexOf(StateOrder, stateName);
                return index >= 0 ? index : 999;
            }).ThenBy(stateName => stateName, StringComparer.OrdinalIgnoreCase);
        }

        private static AnimatorState GetState(Dictionary<string, AnimatorState> states, string stateName)
        {
            if (states.TryGetValue(stateName, out AnimatorState found))
            {
                return found;
            }

            return null;
        }

        private static AnimationClip BuildClipFromTexture(ClipBuild clipBuild, float fallbackFps)
        {
            string texturePath = NormalizeAssetPath(clipBuild.texturePath);
            if (string.IsNullOrWhiteSpace(texturePath))
            {
                return null;
            }

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath)
                .OfType<Sprite>()
                .ToArray();

            if (sprites.Length == 0)
            {
                Debug.LogWarning($"SnackAttack: No sprites found at {texturePath}");
                return null;
            }

            Dictionary<string, Sprite> spriteByName = sprites
                .GroupBy(s => s.name)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            List<string> frameNames = (clipBuild.frames != null && clipBuild.frames.Count > 0)
                ? clipBuild.frames
                : sprites.Select(s => s.name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

            float fps = clipBuild.fps > 0f ? clipBuild.fps : fallbackFps;
            if (fps <= 0f)
            {
                fps = 10f;
            }

            List<ObjectReferenceKeyframe> keyframes = new List<ObjectReferenceKeyframe>();
            int frameIndex = 0;

            foreach (string frameName in frameNames)
            {
                if (string.IsNullOrWhiteSpace(frameName))
                {
                    continue;
                }

                if (!spriteByName.TryGetValue(frameName, out Sprite sprite))
                {
                    Debug.LogWarning($"SnackAttack: Sprite '{frameName}' not found in {texturePath}");
                    continue;
                }

                keyframes.Add(new ObjectReferenceKeyframe
                {
                    time = frameIndex / fps,
                    value = sprite,
                });
                frameIndex++;
            }

            if (keyframes.Count == 0)
            {
                return null;
            }

            AnimationClip clip = new AnimationClip();
            clip.frameRate = fps;
            clip.name = Path.GetFileNameWithoutExtension(clipBuild.clipPath);

            EditorCurveBinding spriteBinding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite",
            };
            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes.ToArray());

            SetClipLoop(clip, clipBuild.loop);
            return clip;
        }

        private static void SetClipLoop(AnimationClip clip, bool loop)
        {
            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty clipSettings = serializedClip.FindProperty("m_AnimationClipSettings");
            if (clipSettings == null)
            {
                return;
            }

            SerializedProperty loopTime = clipSettings.FindPropertyRelative("m_LoopTime");
            if (loopTime != null)
            {
                loopTime.boolValue = loop;
            }

            SerializedProperty loopBlend = clipSettings.FindPropertyRelative("m_LoopBlend");
            if (loopBlend != null)
            {
                loopBlend.boolValue = loop;
            }

            serializedClip.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimationClip SaveOrUpdateClipAsset(AnimationClip generatedClip, string clipPath)
        {
            AnimationClip existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existingClip == null)
            {
                AssetDatabase.CreateAsset(generatedClip, clipPath);
                return generatedClip;
            }

            EditorUtility.CopySerialized(generatedClip, existingClip);
            UnityEngine.Object.DestroyImmediate(generatedClip);
            EditorUtility.SetDirty(existingClip);
            return existingClip;
        }

        private static void RemoveObsoleteCharacterClips(CharacterBuild character)
        {
            if (character?.clips == null || character.clips.Count == 0)
            {
                return;
            }

            HashSet<string> expectedClipPaths = new HashSet<string>(
                character.clips
                    .Where(clip => clip != null && !string.IsNullOrWhiteSpace(clip.clipPath))
                    .Select(clip => NormalizeAssetPath(clip.clipPath)),
                StringComparer.OrdinalIgnoreCase);

            HashSet<string> characterClipFolders = new HashSet<string>(
                expectedClipPaths
                    .Select(path => Path.GetDirectoryName(path)?.Replace("\\", "/"))
                    .Where(path => !string.IsNullOrWhiteSpace(path)),
                StringComparer.OrdinalIgnoreCase);

            foreach (string folder in characterClipFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
                foreach (string clipGuid in clipGuids)
                {
                    string clipAssetPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(clipGuid));
                    if (string.IsNullOrWhiteSpace(clipAssetPath) || expectedClipPaths.Contains(clipAssetPath))
                    {
                        continue;
                    }

                    if (AssetDatabase.DeleteAsset(clipAssetPath))
                    {
                        Debug.Log($"SnackAttack: Removed obsolete clip: {clipAssetPath}");
                    }
                }
            }
        }

        private static AnimatorController LoadOrCreateController(string controllerPath)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            if (controller.layers == null || controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }

            return controller;
        }

        private static void ResetController(AnimatorController controller)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            {
                controller.RemoveParameter(parameter);
            }

            string controllerPath = AssetDatabase.GetAssetPath(controller);
            AnimatorStateMachine freshBaseStateMachine = new AnimatorStateMachine
            {
                name = "Base Layer",
                hideFlags = HideFlags.HideInHierarchy,
            };

            AssetDatabase.AddObjectToAsset(freshBaseStateMachine, controller);

            AnimatorControllerLayer freshLayer = new AnimatorControllerLayer
            {
                name = "Base Layer",
                stateMachine = freshBaseStateMachine,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                iKPass = false,
                syncedLayerIndex = -1,
                syncedLayerAffectsTiming = false,
            };

            controller.layers = new[] { freshLayer };

            if (string.IsNullOrWhiteSpace(controllerPath))
            {
                return;
            }

            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(controllerPath);
            foreach (UnityEngine.Object subAsset in subAssets)
            {
                if (subAsset == null || subAsset == controller || subAsset == freshBaseStateMachine)
                {
                    continue;
                }

                if (subAsset is AnimatorStateMachine ||
                    subAsset is AnimatorState ||
                    subAsset is AnimatorStateTransition ||
                    subAsset is AnimatorTransition ||
                    subAsset is BlendTree ||
                    subAsset is StateMachineBehaviour)
                {
                    UnityEngine.Object.DestroyImmediate(subAsset, true);
                }
            }
        }

        private static string ResolveConfigPathFromArgsOrDefault(string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-animConfig", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace("\\", "/").Trim();

            if (Path.IsPathRooted(normalized))
            {
                string projectRoot = Directory.GetCurrentDirectory().Replace("\\", "/");
                if (normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(projectRoot.Length).TrimStart('/');
                }
            }

            return normalized;
        }

        private static string ToAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private static void EnsureAssetFolderForAssetPath(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            string folder = Path.GetDirectoryName(normalized);
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            EnsureAssetFolder(folder.Replace("\\", "/"));
        }

        private static void EnsureAssetFolder(string folderAssetPath)
        {
            string normalized = folderAssetPath.Replace("\\", "/").Trim('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new InvalidOperationException($"Folder path must start with Assets/: {folderAssetPath}");
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        [Serializable]
        private class BuildConfig
        {
            public int version = 1;
            public float defaultFps = 10f;
            public List<CharacterBuild> characters = new List<CharacterBuild>();
        }

        [Serializable]
        private class CharacterBuild
        {
            public string name;
            public string controllerPath;
            public string defaultState;
            public List<ClipBuild> clips = new List<ClipBuild>();
        }

        [Serializable]
        private class ClipBuild
        {
            public string state;
            public string clipPath;
            public string texturePath;
            public bool loop = true;
            public float fps = 0f;
            public List<string> frames = new List<string>();
        }
    }
}
