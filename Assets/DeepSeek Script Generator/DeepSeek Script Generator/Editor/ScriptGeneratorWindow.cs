using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace ScriptGenerator {
public sealed class ScriptGeneratorWindow : EditorWindow {
    private const string ApiKeyErrorText = "API Key hasn't been set. Please check the project settings " +
                                           "(Edit > Project Settings > DeepSeek Script Generator > API Key).";
    private string _prompt = "";
    private string _className = "";
    private bool _forgetHistory;
    private bool _deletePrevious;

    private bool IsApiKeyOk => !string.IsNullOrEmpty(ScriptGeneratorSettings.instance.apiKey);

    private static string WrapPrompt(string input) =>
        "Write a Unity script.\n" + " - It is a C# MonoBehaviour.\n " + //
        " - Include all the necessary imports.\n" + //
        " - Add a RequireComponent attribute to any components used.\n" + //
        " - Generate tooltips for all properties.\n" + //
        " - All properties should have default values.\n" + //
        " - I only need the script body. Don’t add any explanation.\n" + // 
        " - Do not start and end the result with ```csharp and ```.\n" + // 
        "The task is described as follows:\n" + input;

    void RunGenerator(bool forgetHistory = false) {
        var prompt = forgetHistory ? "Forget everything I said before.\n" : string.Empty;
        prompt += WrapPrompt(_prompt);
        var code = DeepSeekAPI.Submit(prompt);
        CreateScriptAsset(code);
    }

    void CreateScriptAsset(string code) {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(ProjectWindowUtil).GetMethod("CreateScriptAssetWithContent", flags);

        // Extract the class name from the code. It is the last word before the colon.
        var everythingBeforeColon = code.Split(':')[0];
        var words = everythingBeforeColon.Split(' ');
        words = words.Where(w => !string.IsNullOrEmpty(w)).ToArray();
        _className = words[words.Length - 1];

        Debug.Log($"[DeepSeek Script Generator] Created class {_className}");

        // If the class name already exists, add a number to the end.
        var i = 1;
        while (Resources.FindObjectsOfTypeAll<MonoScript>().Any(s => s.name == _className)) {
            _className = words[words.Length - 1] + i++;
        }

        // Save the class name to the EditorPrefs.
        EditorPrefs.SetString("AiEngineerClassName", _className);

        var settings = ScriptGeneratorSettings.instance;
        if (!AssetDatabase.IsValidFolder(settings.path)) {
            // Remove "Assets/" from the path.
            var folder = settings.path.Substring(7);
            // Remove trailing slash.
            if (folder.EndsWith("/")) {
                folder = folder.Substring(0, folder.Length - 1);
            }

            AssetDatabase.CreateFolder("Assets", folder);
        }

        var path = $"{settings.path}/{_className}.cs";
        method!.Invoke(null, new object[] { path, code });
    }

    void OnGUI() {
        if (IsApiKeyOk) {
            // Read the last prompt from the EditorPrefs.
            if (string.IsNullOrEmpty(_prompt)) {
                _prompt = EditorPrefs.GetString("AiEngineerPrompt", "");
            }

            _prompt = EditorGUILayout.TextArea(_prompt, GUILayout.ExpandHeight(true));
            _forgetHistory = EditorGUILayout.Toggle("Forget prior commands", _forgetHistory);
            var className = EditorPrefs.GetString("AiEngineerClassName", "");
            _deletePrevious =
                !(string.IsNullOrEmpty(className) || !Selection.activeGameObject ||
                  !Selection.activeGameObject.GetComponent(className)) &&
                EditorGUILayout.Toggle($"Overwrite {className}", _deletePrevious);

            if (GUILayout.Button("Generate and Add")) {
                EditorPrefs.SetString("AiEngineerPrompt", _prompt);

                if (_deletePrevious) {
                    var gameObject = Selection.activeGameObject;
                    if (gameObject != null) {
                        var component = gameObject.GetComponent(className);
                        if (component != null) {
                            DestroyImmediate(component);
                        }
                    }

                    var settings = ScriptGeneratorSettings.instance;
                    var file = AssetDatabase.LoadAssetAtPath<MonoScript>($"{settings.path}{className}.cs");
                    if (file != null) {
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(file));
                    }
                }

                RunGenerator(_forgetHistory);
            }
        } else {
            EditorGUILayout.HelpBox(ApiKeyErrorText, MessageType.Error);
        }
    }

    void OnEnable() => AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

    void OnDisable() => AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;

    void OnAfterAssemblyReload() {
        var script = Resources.FindObjectsOfTypeAll<MonoScript>().FirstOrDefault(s => s.name == _className);
        if (script != null) {
            var gameObject = ScriptGeneratorButton.SelectedGameObject;
            if (gameObject == null) {
                gameObject = Selection.activeGameObject;
            }

            if (gameObject != null) {
                gameObject.AddComponent(script.GetClass());
            } else {
                Debug.LogError("[DeepSeek Script Generator] Target GameObject not found.");
            }
        } else {
            Debug.LogError("[DeepSeek Script Generator] Script not found.");
        }

        Close();
    }
}
}