using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;

namespace ScriptGenerator {
static class DeepSeekAPI {
    

#pragma warning disable 0649
    [Serializable]
    public struct Request {
        public string model;
        public RequestMessage[] messages;
    }

    [Serializable]
    public struct RequestMessage {
        public string role;
        public string content;
    }

    [Serializable]
    public struct Response {
        public string id;
        public ResponseChoice[] choices;
    }

    [Serializable]
    public struct ResponseChoice {
        public int index;
        public ResponseMessage message;
    }

    [Serializable]
    public struct ResponseMessage {
        public string role;
        public string content;
    }
#pragma warning restore 0649

    public static string Submit(string prompt) {
        var settings = ScriptGeneratorSettings.instance;
        var request = new Request {
            model = settings.model, messages = new[] { new RequestMessage() { role = "user", content = prompt } }
        };

        var requestJson = JsonUtility.ToJson(request);

#if UNITY_2022_2_OR_NEWER
        using var post = UnityWebRequest.Post(settings.Url, requestJson, "application/json");
#else
        using var post = new UnityWebRequest(settings.Url, "POST");
        post.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(requestJson));
        post.downloadHandler = new DownloadHandlerBuffer();
        post.SetRequestHeader("Content-Type", "application/json");
#endif

        post.timeout = settings.timeout;

        var userEmail = CloudProjectSettings.userName;
        if (string.IsNullOrEmpty(userEmail)) {
            var m = "[DeepSeek Script Generator] You must be signed in to Unity to use this feature.";
            throw new Exception(m);
        }

        var key = Encryption.Decrypt(settings.apiKey, userEmail);
        post.SetRequestHeader("Authorization", "Bearer " + key);

        var webRequest = post.SendWebRequest();

        var time = Time.realtimeSinceStartup;
        var cancel = false;
        var progress = 0f;

        while (!webRequest.isDone && !cancel) {
            cancel = EditorUtility.DisplayCancelableProgressBar("DeepSeek Script Generator", "Generating script...",
                                                                progress += 0.01f);
            System.Threading.Thread.Sleep(100);

            var timeout = settings.useTimeout ? settings.timeout + 1f : float.PositiveInfinity;
            if (Time.realtimeSinceStartup - time > timeout) {
                EditorUtility.ClearProgressBar();
                throw new TimeoutException("[DeepSeek Script Generator] Request timed out");
            }
        }

        EditorUtility.ClearProgressBar();

        var responseJson = post.downloadHandler.text;
        if (!string.IsNullOrEmpty(post.error)) {
            throw new Exception($"[DeepSeek Script Generator] {post.error}");
        }

        if (string.IsNullOrEmpty(responseJson)) {
            throw new Exception("[DeepSeek Script Generator] No response received");
        }

        var data = JsonUtility.FromJson<Response>(responseJson);
        if (data.choices == null || data.choices.Length == 0) {
            throw new Exception("[DeepSeek Script Generator] No choices received");
        }

        return data.choices[0].message.content;
    }
}
}