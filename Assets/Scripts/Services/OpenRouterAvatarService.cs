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
        private const int MaxTokens = 2048;

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

        public static IEnumerator GenerateFullAvatar(
            string apiKey,
            string dogName,
            Texture2D photo,
            Action<string> onStatus,
            Action<AvatarGenerationResult> onSuccess,
            Action<string> onError)
        {
            if (photo == null)
            {
                onError?.Invoke("Photo is null.");
                yield break;
            }

            string displayName = dogName == null ? string.Empty : dogName.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                onError?.Invoke("Dog name is required.");
                yield break;
            }

            string characterId = NormalizeId(displayName);
            string folder = Path.Combine(Application.persistentDataPath, "custom_avatars", characterId);
            Directory.CreateDirectory(folder);

            string profilePath = Path.Combine(folder, "profile.png");
            string runPath = Path.Combine(folder, "run.png");
            string eatPath = Path.Combine(folder, "eat.png");
            string walkPath = Path.Combine(folder, "walk.png");
            string boostPath = Path.Combine(folder, "boost.png");

            string photoB64 = Convert.ToBase64String(photo.EncodeToPNG());

            byte[] profileBytes = null;
            byte[] runBytes = null;
            byte[] eatBytes = null;
            byte[] walkBytes = null;
            byte[] boostBytes = null;
            string errorMessage = string.Empty;

            onStatus?.Invoke("Step 1/7: Analyzing dog's features...");

            onStatus?.Invoke($"Step 2/7: Creating {displayName}'s portrait...");
            yield return RequestGeneratedImage(
                apiKey,
                "Create a single square pixel-art dog portrait from this real dog photo. Transparent background. Retro style. No text.",
                photoB64,
                new List<string>(),
                "1:1",
                bytes => profileBytes = bytes,
                err => errorMessage = err);
            if (!string.IsNullOrWhiteSpace(errorMessage)) { onError?.Invoke(errorMessage); yield break; }
            File.WriteAllBytes(profilePath, profileBytes);

            string profileB64 = Convert.ToBase64String(profileBytes);

            onStatus?.Invoke($"Step 3/7: Creating {displayName}'s run animation...");
            yield return RequestGeneratedImage(
                apiKey,
                "Create a horizontal 3-frame pixel-art RUN sprite sheet of the same dog, side view facing right, transparent background.",
                photoB64,
                new List<string> { profileB64 },
                "21:9",
                bytes => runBytes = bytes,
                err => errorMessage = err);
            if (!string.IsNullOrWhiteSpace(errorMessage)) { onError?.Invoke(errorMessage); yield break; }
            File.WriteAllBytes(runPath, runBytes);

            onStatus?.Invoke($"Step 4/7: Creating {displayName}'s eat animation...");
            yield return RequestGeneratedImage(
                apiKey,
                "Create a horizontal 3-frame pixel-art EAT/ATTACK sprite sheet of the same dog, side view facing right, transparent background.",
                photoB64,
                new List<string> { profileB64 },
                "21:9",
                bytes => eatBytes = bytes,
                err => errorMessage = err);
            if (!string.IsNullOrWhiteSpace(errorMessage)) { onError?.Invoke(errorMessage); yield break; }
            File.WriteAllBytes(eatPath, eatBytes);

            onStatus?.Invoke($"Step 5/7: Creating {displayName}'s walk animation...");
            yield return RequestGeneratedImage(
                apiKey,
                "Create a horizontal 5-frame pixel-art WALK sprite sheet of the same dog, side view facing right, transparent background.",
                photoB64,
                new List<string> { profileB64 },
                "21:9",
                bytes => walkBytes = bytes,
                err => errorMessage = err);
            if (!string.IsNullOrWhiteSpace(errorMessage)) { onError?.Invoke(errorMessage); yield break; }
            File.WriteAllBytes(walkPath, walkBytes);

            onStatus?.Invoke($"Step 6/7: Creating {displayName}'s winged boost form...");
            yield return RequestGeneratedImage(
                apiKey,
                "Create a single square pixel-art BOOST sprite with wings for the same dog, side view facing right, transparent background.",
                photoB64,
                new List<string> { profileB64 },
                "1:1",
                bytes => boostBytes = bytes,
                err => errorMessage = err);
            if (!string.IsNullOrWhiteSpace(errorMessage)) { onError?.Invoke(errorMessage); yield break; }
            File.WriteAllBytes(boostPath, boostBytes);

            onStatus?.Invoke($"Step 7/7: Registering {displayName}...");

            onSuccess?.Invoke(new AvatarGenerationResult
            {
                success = true,
                characterId = characterId,
                displayName = displayName,
                profilePath = profilePath,
                runSpritePath = runPath,
                eatSpritePath = eatPath,
                walkSpritePath = walkPath,
                boostSpritePath = boostPath,
            });
        }

        public static IEnumerator GenerateProfileAvatar(
            string apiKey,
            string dogName,
            Texture2D photo,
            Action<string> onStatus,
            Action<AvatarGenerationResult> onSuccess,
            Action<string> onError)
        {
            // Backward-compatible entry point now uses the full runtime pipeline.
            return GenerateFullAvatar(apiKey, dogName, photo, onStatus, onSuccess, onError);
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

        private static IEnumerator RequestGeneratedImage(
            string apiKey,
            string prompt,
            string photoB64,
            List<string> referenceImages,
            string aspectRatio,
            Action<byte[]> onSuccess,
            Action<string> onError)
        {
            RequestPayload payload = BuildPayload(prompt, photoB64, referenceImages, aspectRatio);
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

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(BuildFriendlyError(req));
                    yield break;
                }

                byte[] imageBytes;
                if (!TryExtractImageBytes(req.downloadHandler.text, out imageBytes, out string extractError))
                {
                    onError?.Invoke(extractError);
                    yield break;
                }

                onSuccess?.Invoke(imageBytes);
            }
        }

        private static RequestPayload BuildPayload(
            string prompt,
            string photoB64,
            List<string> referenceImages,
            string aspectRatio)
        {
            List<ContentPart> content = new List<ContentPart>
            {
                new ContentPart { type = "text", text = prompt },
                new ContentPart
                {
                    type = "image_url",
                    image_url = new ImageUrl { url = $"data:image/png;base64,{photoB64}", detail = "high" }
                }
            };

            if (referenceImages != null)
            {
                for (int i = 0; i < referenceImages.Count; i++)
                {
                    string refB64 = referenceImages[i];
                    if (string.IsNullOrWhiteSpace(refB64))
                        continue;

                    content.Add(new ContentPart
                    {
                        type = "image_url",
                        image_url = new ImageUrl { url = $"data:image/png;base64,{refB64}", detail = "high" }
                    });
                }
            }

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
                        content = content.ToArray(),
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(aspectRatio))
            {
                // Include aspect ratio in prompt to avoid dynamic JSON dictionaries with JsonUtility.
                payload.messages[0].content[0].text = payload.messages[0].content[0].text +
                                                     $" Aspect ratio target: {aspectRatio}.";
            }

            return payload;
        }

        private static bool TryExtractImageBytes(string json, out byte[] imageBytes, out string error)
        {
            imageBytes = null;
            error = string.Empty;

            RootResponse root;
            try
            {
                root = JsonUtility.FromJson<RootResponse>(json);
            }
            catch (Exception ex)
            {
                error = "Invalid response JSON: " + ex.Message;
                return false;
            }

            string dataUrl = null;
            if (root?.choices != null && root.choices.Length > 0 &&
                root.choices[0]?.message?.images != null && root.choices[0].message.images.Length > 0)
            {
                dataUrl = root.choices[0].message.images[0]?.image_url?.url;
            }

            if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:"))
            {
                error = "No image returned from OpenRouter.";
                return false;
            }

            int comma = dataUrl.IndexOf(',');
            if (comma < 0)
            {
                error = "Invalid data URL.";
                return false;
            }

            try
            {
                imageBytes = Convert.FromBase64String(dataUrl.Substring(comma + 1));
                return true;
            }
            catch (Exception ex)
            {
                error = "Cannot decode image: " + ex.Message;
                return false;
            }
        }
    }
}