using UnityEngine;

namespace SnackAttack.Config
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "SnackAttack/Config/GameSettings")]
    public class GameSettingsSO : ScriptableObject
    {
        [Header("General Settings")]
        public int targetFrameRate = 60;
        public int timeLimitPerRoundSeconds = 90;
        public int roundsToWin = 2;
        
        [Header("Grid & Physics")]
        public float gridCellSize = 64f;
    }
}
