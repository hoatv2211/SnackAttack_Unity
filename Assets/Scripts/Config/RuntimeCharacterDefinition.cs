using System;
using UnityEngine;

namespace SnackAttack.Config
{
    [Serializable]
    public class RuntimeCharacterDefinition
    {
        public string id;
        public string displayName;
        public string breedDescription;
        public bool isCustom = true;

        public float speedMultiplier = 1f;
        public float collisionRadius = 0.52f;

        public string profilePath;
        public string runSpritePath;
        public string eatSpritePath;
        public string walkSpritePath;
        public string boostSpritePath;

        public Sprite profileSprite;
        public Sprite[] runFrames;
        public Sprite[] eatFrames;
        public Sprite[] walkFrames;
        public Sprite[] boostFrames;

        public long createdAtTicks = DateTime.UtcNow.Ticks;
    }
}
