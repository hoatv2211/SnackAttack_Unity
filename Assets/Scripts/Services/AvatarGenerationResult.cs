using System;

namespace SnackAttack.Services
{
    [Serializable]
    public class AvatarGenerationResult
    {
        public bool success;
        public string characterId;
        public string displayName;
        public string errorMessage;

        public string profilePath;
        public string runSpritePath;
        public string eatSpritePath;
        public string walkSpritePath;
        public string boostSpritePath;
    }
}
