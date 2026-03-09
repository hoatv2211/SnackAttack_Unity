using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SnackAttack.Services
{
    public static class OpenRouterAvatarService
    {
        private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";
        private const string Model = "google/gemini-3.1-flash-image-preview";
        private const int MaxTokens = 1024;

        [Serializable]
        public class AvatarResult
        {
            public string characterId;
            public string displayName;
            public string profilePath;
        }

        [Serializable] private class RequestPayload { public string model; public Message[] messages; public string[] modalities; public int max_tokens; }
        [Serializable] private class Message { public string role; public ContentPart[] content; }
        [Serializable] private class ContentPart { public string type; public string text; public ImageUrl image_url; }
        [Serializable] private class ImageUrl { public string url; public string detail; }

        // Minimal response model
        [Serializable] private class RootResponse { public Choice[] choices; }
        [Serializable] private class Choice { public MessageResponse message; }
        [Serializable] private class MessageResponse { public ImageItem[] images; }
        [Serializable] private class ImageItem { public ImageUrlOut image_url; }
        [Serializable] private class ImageUrlOut { public string url; }
        [Serializable] private class ErrorEnvelope { public ErrorBody error; }
        [Serializable] private class ErrorBody { public string message; }

        public static IEnumerator GenerateProfileAvatar(
            string apiKey,
            string dogName,
            Texture2D photo,
            Action<string> onStatus,
            Action<AvatarResult> onSuccess,
            Action<string> onError)
        {
            onStatus?.Invoke("Preparing photo...");
            if (photo == null) { onError?.Invoke("Photo is null."); yield break; }

            string photoB64 = Convert.ToBase64String(photo.EncodeToPNG());
            string prompt =
                "Create a single square pixel-art dog portrait from this real dog photo. " +
                "Transparent background. Retro game style with bold outline. " +
                "Do not include text.";

            onStatus?.Invoke("Building OpenRouter request...");

            RequestPayload payload = new RequestPayload
            {
                model = Model,
                modalities = new[] { "image", "text" },
                max_tokens = MaxTokens,
                messages = new[]
                {
                    new Message
                    {
                        role = "user",
                        content = new[]
                        {
                            new ContentPart { type = "text", text = prompt },
                            new ContentPart
                            {
                                type = "image_url",
                                image_url = new ImageUrl { url = $"data:image/png;base64,{photoB64}", detail = "high" }
                            }
                        }
                    }
                }
            };

            string json = JsonUtility.ToJson(payload);
            using (UnityWebRequest req = new UnityWebRequest(ApiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();

                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                req.SetRequestHeader("HTTP-Referer", "https://snackattack.game");
                req.SetRequestHeader("X-Title", "SnackAttack Unity");

                onStatus?.Invoke("Sending request to OpenRouter...");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(BuildFriendlyError(req));
                    yield break;
                }

                onStatus?.Invoke("Parsing response...");

                RootResponse root;
                try
                {
                    root = JsonUtility.FromJson<RootResponse>(req.downloadHandler.text);
                }
                catch (Exception ex)
                {
                    onError?.Invoke("Invalid response JSON: " + ex.Message);
                    yield break;
                }

                string dataUrl = null;
                if (root?.choices != null && root.choices.Length > 0 &&
                    root.choices[0]?.message?.images != null && root.choices[0].message.images.Length > 0)
                {
                    dataUrl = root.choices[0].message.images[0]?.image_url?.url;
                }

                if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:"))
                {
                    onError?.Invoke("No image returned from OpenRouter.");
                    yield break;
                }

                int comma = dataUrl.IndexOf(',');
                if (comma < 0) { onError?.Invoke("Invalid data URL."); yield break; }

                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(dataUrl.Substring(comma + 1));
                }
                catch (Exception ex)
                {
                    onError?.Invoke("Cannot decode image: " + ex.Message);
                    yield break;
                }

                string id = NormalizeId(dogName);
                string folder = Path.Combine(Application.persistentDataPath, "custom_avatars", id);
                Directory.CreateDirectory(folder);
                string profilePath = Path.Combine(folder, "profile.png");
                onStatus?.Invoke("Saving generated avatar...");
                File.WriteAllBytes(profilePath, imageBytes);

                onStatus?.Invoke("Avatar generation complete.");

                onSuccess?.Invoke(new AvatarResult
                {
                    characterId = id,
                    displayName = dogName.Trim(),
                    profilePath = profilePath
                });
            }
        }

        private static string NormalizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "custom_dog";
            string x = raw.Trim().ToLowerInvariant().Replace(" ", "_");
            StringBuilder sb = new StringBuilder(x.Length);
            foreach (char c in x)
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                    sb.Append(c);
            }
            string result = sb.ToString();
            return string.IsNullOrEmpty(result) ? "custom_dog" : result;
        }

        private static string BuildFriendlyError(UnityWebRequest req)
        {
            string body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            string baseMessage = string.IsNullOrWhiteSpace(req.error) ? "OpenRouter request failed." : req.error;

            string apiMessage = ExtractApiMessage(body);
            if (req.responseCode == 402)
            {
                if (!string.IsNullOrWhiteSpace(apiMessage))
                    return $"OpenRouter 402: {apiMessage}";

                return "OpenRouter 402: Insufficient credits or request quota exceeded.";
            }

            if (!string.IsNullOrWhiteSpace(apiMessage))
                return $"OpenRouter {req.responseCode}: {apiMessage}";

            return string.IsNullOrWhiteSpace(body)
                ? $"OpenRouter {req.responseCode}: {baseMessage}"
                : $"OpenRouter {req.responseCode}: {baseMessage}";
        }

        private static string ExtractApiMessage(string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
                return string.Empty;

            try
            {
                ErrorEnvelope envelope = JsonUtility.FromJson<ErrorEnvelope>(rawBody);
                if (envelope != null && envelope.error != null && !string.IsNullOrWhiteSpace(envelope.error.message))
                    return envelope.error.message.Trim();
            }
            catch
            {
                // Fall through and return raw truncated body if JSON is malformed.
            }

            return rawBody.Length <= 220 ? rawBody : rawBody.Substring(0, 220) + "...";
        }
    }
}