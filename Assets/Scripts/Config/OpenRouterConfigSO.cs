using UnityEngine;

namespace SnackAttack.Config
{
    [CreateAssetMenu(fileName = "OpenRouterConfig", menuName = "SnackAttack/Config/OpenRouterConfig")]
    public class OpenRouterConfigSO : ScriptableObject
    {
        [Header("OpenRouter")]
        [Tooltip("Default OpenRouter key used by UploadAvatarScreen when available.")]
        public string openRouterApiKey = "";
    }
}
