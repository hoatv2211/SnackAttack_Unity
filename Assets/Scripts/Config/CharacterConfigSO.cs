using UnityEngine;
using System.Collections.Generic;

namespace SnackAttack.Config
{
    [System.Serializable]
    public class CharacterData
    {
        public string characterId;
        public string characterName;
        public string description;
        public float speedMultiplier = 1f;
        public float collisionRadius = 0.5f;

        [Header("Visuals")]
        public Sprite profileImage;
        public Sprite idleSprite;
        // Animation controller or clip references would go here
    }

    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "SnackAttack/Config/CharacterConfig")]
    public class CharacterConfigSO : ScriptableObject
    {
        public List<CharacterData> characters;

        public CharacterData GetCharacter(string id)
        {
            return characters.Find(c => c.characterId == id);
        }
    }
}
