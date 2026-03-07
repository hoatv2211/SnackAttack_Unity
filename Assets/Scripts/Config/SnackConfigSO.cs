using UnityEngine;
using System.Collections.Generic;

namespace SnackAttack.Config
{
    /// <summary>Matches Python snacks.json effect types: speed_boost, slow, chaos, invincibility, boost.</summary>
    public enum SnackEffectType
    {
        None,
        SpeedBoost,   // bone: magnitude 1.5, 5s
        Invincibility,// steak: 2s — blocks slow/chaos
        Slow,         // broccoli: magnitude 0.5, 3s
        Chaos,        // spicy_pepper: 4s — flip controls
        Boost         // red_bull: magnitude 2.0, 6s — speed + score x2
    }

    [System.Serializable]
    public class SnackData
    {
        public string snackId;
        public string snackName;
        public int points = 100;
        public float spawnWeight = 1.0f;

        [Header("Effects (match Python snacks.json)")]
        public SnackEffectType effectType;
        [Tooltip("e.g. 1.5 for speed, 0.5 for slow, 2.0 for boost score multiplier")]
        public float effectMagnitude = 1f;
        public float effectDuration = 0f;

        [Header("Visuals")]
        public Sprite sprite;
    }

    [CreateAssetMenu(fileName = "SnackConfig", menuName = "SnackAttack/Config/SnackConfig")]
    public class SnackConfigSO : ScriptableObject
    {
        public List<SnackData> snacks;

        public SnackData GetSnack(string id)
        {
            return snacks.Find(s => s.snackId == id);
        }
    }
}
