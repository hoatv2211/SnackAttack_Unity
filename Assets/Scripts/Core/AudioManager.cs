using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SnackAttack.Core;
using SnackAttack.Gameplay;

namespace SnackAttack.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        public AudioSource musicSource;
        public AudioSource sfxSource;

        [Header("Volume Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.6f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        public bool musicEnabled = true;
        public bool sfxEnabled = true;

        [Header("Audio Clips")]
        [Tooltip("SFX clips keyed by name. Keys are matched loosely (case/space/underscore insensitive).")]
        public List<AudioClipEntry> soundEffects = new List<AudioClipEntry>();
        [Tooltip("Optional dedicated music list. If empty, music also resolves from Sound Effects list.")]
        public List<AudioClipEntry> musicTracks = new List<AudioClipEntry>();

        private readonly Dictionary<string, AudioClip> sfxDictionary = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AudioClip> musicDictionary = new Dictionary<string, AudioClip>();
        private string currentMusicKey = string.Empty;

        private static readonly Dictionary<string, string> keyAliases = new Dictionary<string, string>
        {
            // Python keys -> Unity clip names
            { "countdown23", "23" },
            { "countdown1", "1" },
            { "gameover", "background" },
            { "mainmenu", "background" },
            { "menu", "background" },
            { "ingame", "gameplay" },
            { "go", "start" },
        };

        [System.Serializable]
        public struct AudioClipEntry
        {
            public string name;
            public AudioClip clip;
        }

        [System.Serializable]
        public class MusicRequest
        {
            public string musicName;
            public bool loop = true;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeDictionary();
                ApplyVolumeSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnValidate()
        {
            InitializeDictionary();
            ApplyVolumeSettings();
        }

        private void OnEnable()
        {
            EventManager.StartListening("PLAY_SOUND", OnPlaySound);
            EventManager.StartListening("PLAY_MUSIC", OnPlayMusic);
            EventManager.StartListening("STOP_MUSIC", OnStopMusic);
            EventManager.StartListening("GAME_STATE_CHANGED", OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventManager.StopListening("PLAY_SOUND", OnPlaySound);
            EventManager.StopListening("PLAY_MUSIC", OnPlayMusic);
            EventManager.StopListening("STOP_MUSIC", OnStopMusic);
            EventManager.StopListening("GAME_STATE_CHANGED", OnGameStateChanged);
        }

        private void InitializeDictionary()
        {
            sfxDictionary.Clear();
            musicDictionary.Clear();

            if (soundEffects != null)
            {
                foreach (var entry in soundEffects)
                {
                    RegisterClipEntry(sfxDictionary, entry);
                    if (musicTracks == null || musicTracks.Count == 0)
                        RegisterClipEntry(musicDictionary, entry);
                }
            }

            if (musicTracks != null && musicTracks.Count > 0)
            {
                foreach (var entry in musicTracks)
                    RegisterClipEntry(musicDictionary, entry);
            }
        }

        private static void RegisterClipEntry(Dictionary<string, AudioClip> dictionary, AudioClipEntry entry)
        {
            if (dictionary == null || entry.clip == null)
                return;

            AddClipMapping(dictionary, entry.name, entry.clip);
            AddClipMapping(dictionary, entry.clip.name, entry.clip);
        }

        private static void AddClipMapping(Dictionary<string, AudioClip> dictionary, string key, AudioClip clip)
        {
            string normalized = NormalizeAndAlias(key);
            if (string.IsNullOrEmpty(normalized))
                return;

            if (!dictionary.ContainsKey(normalized))
                dictionary.Add(normalized, clip);
        }

        private static string NormalizeAndAlias(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            StringBuilder builder = new StringBuilder(key.Length);
            string lowered = key.Trim().ToLowerInvariant();
            for (int i = 0; i < lowered.Length; i++)
            {
                char c = lowered[i];
                if (char.IsLetterOrDigit(c))
                    builder.Append(c);
            }

            string normalized = builder.ToString();
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;

            if (keyAliases.TryGetValue(normalized, out string aliased))
                return aliased;

            return normalized;
        }

        private static bool TryResolveClip(Dictionary<string, AudioClip> dictionary, string key, out AudioClip clip, out string resolvedKey)
        {
            resolvedKey = NormalizeAndAlias(key);
            if (string.IsNullOrEmpty(resolvedKey))
            {
                clip = null;
                return false;
            }

            return dictionary.TryGetValue(resolvedKey, out clip);
        }

        private void ApplyVolumeSettings()
        {
            float musicLevel = Mathf.Clamp01(masterVolume * musicVolume);

            if (musicSource != null)
            {
                musicSource.volume = musicLevel;
                musicSource.mute = !musicEnabled;
            }

            if (sfxSource != null)
                sfxSource.mute = !sfxEnabled;
        }

        public void PlayMusic(AudioClip musicClip, bool loop = true)
        {
            if (musicSource == null || musicClip == null)
                return;

            if (!musicEnabled)
            {
                StopMusic();
                return;
            }

            bool sameClip = musicSource.isPlaying && musicSource.clip == musicClip;
            musicSource.clip = musicClip;
            musicSource.loop = loop;
            musicSource.volume = Mathf.Clamp01(masterVolume * musicVolume);

            if (!sameClip)
                musicSource.Play();

            currentMusicKey = NormalizeAndAlias(musicClip.name);
        }

        public void PlayMusic(string musicName, bool loop = true)
        {
            if (musicSource == null)
                return;

            if (!musicEnabled)
            {
                StopMusic();
                return;
            }

            if (!TryResolveClip(musicDictionary, musicName, out AudioClip clip, out string resolvedKey))
            {
                // Fallback to SFX table in case dedicated music list wasn't filled.
                if (!TryResolveClip(sfxDictionary, musicName, out clip, out resolvedKey))
                {
                    Debug.LogWarning($"AudioManager: Music '{musicName}' not found!");
                    return;
                }
            }

            bool sameTrack = musicSource.isPlaying && musicSource.clip == clip && currentMusicKey == resolvedKey;
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = Mathf.Clamp01(masterVolume * musicVolume);

            if (!sameTrack)
                musicSource.Play();

            currentMusicKey = resolvedKey;
        }

        public void StopMusic()
        {
            if (musicSource != null)
                musicSource.Stop();

            currentMusicKey = string.Empty;
        }

        public void PlaySFX(string soundName)
        {
            if (!sfxEnabled)
                return;

            if (sfxSource == null)
                return;

            if (TryResolveClip(sfxDictionary, soundName, out AudioClip clip, out _))
            {
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(masterVolume * sfxVolume));
            }
            else
            {
                Debug.LogWarning($"AudioManager: Sound '{soundName}' not found!");
            }
        }

        // Event listener for playing sounds fired from anywhere
        private void OnPlaySound(object param)
        {
            if (param is string soundName)
            {
                PlaySFX(soundName);
            }
        }

        private void OnPlayMusic(object param)
        {
            if (param is string musicName)
            {
                PlayMusic(musicName, true);
                return;
            }

            if (param is MusicRequest request && !string.IsNullOrWhiteSpace(request.musicName))
                PlayMusic(request.musicName, request.loop);
        }

        private void OnStopMusic(object param)
        {
            StopMusic();
        }

        private void OnGameStateChanged(object param)
        {
            if (!(param is GameState state))
                return;

            switch (state)
            {
                case GameState.MainMenu:
                case GameState.CharacterSelect:
                case GameState.GameOver:
                    PlayMusic("background", true);
                    break;

                case GameState.Countdown:
                case GameState.StormIntro:
                case GameState.Playing:
                    PlayMusic("gameplay", true);
                    break;
            }
        }
    }
}
