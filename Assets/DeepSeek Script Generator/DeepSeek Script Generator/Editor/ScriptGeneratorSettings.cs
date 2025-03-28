using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ScriptGenerator {
[FilePath("UserSettings/DeepSeekScriptGeneratorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class ScriptGeneratorSettings : ScriptableSingleton<ScriptGeneratorSettings> {

    public string Url = "https://api.deepseek.com/v1/chat/completions";
    public string apiKey;
    public string model = "gpt-3.5-turbo";
    public string path = "Assets/Scripts/";
    public bool useTimeout = true;
    public int timeout = 45;

    public void Save() => Save(true);
    void OnDisable() => Save();
   
    }

sealed class ScriptGeneratorSettingsProvider : SettingsProvider {
    private ScriptGeneratorSettingsProvider() : base("Project/DeepSeek Script Generator", SettingsScope.Project) { }
    private Texture2D _logo;
    
    public override void OnGUI(string search) {
            _logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DeepSeek Script Generator/Editor/Logo.png");
            GUILayout.Label(_logo, GUILayout.Width(780), GUILayout.Height(165));
            var settings = ScriptGeneratorSettings.instance;
        using var rsa = new RSACryptoServiceProvider();

        var userEmail = CloudProjectSettings.userName;
        if (string.IsNullOrEmpty(userEmail)) {
            EditorGUILayout.HelpBox("You must be signed in to Unity to use this feature.", MessageType.Error);
            return;
        }
        var url = settings.Url;
        var key = string.IsNullOrEmpty(settings.apiKey) ? "" : Encryption.Decrypt(settings.apiKey, userEmail);
        var model = settings.model;
        var useTimeout = settings.useTimeout;
        var timeout = settings.timeout;
        var path = settings.path;
           
            EditorGUILayout.HelpBox("Get an API key from the OpenAI website:\n" + //
                                "    1. Sign up or log in to your OpenAI account using the button below.\n" + // 
                                "    2. Navigate to the 'View API Keys' section in your account dashboard.\n" + //
                                "    3. Click 'Create new secret key' and copy the generated key.", MessageType.Info);

        if (GUILayout.Button("DeepSeek Website", GUILayout.ExpandHeight(false))) {
            Application.OpenURL("https://platform.deepseek.com/");
        }

        EditorGUILayout.Space(20);

      
        EditorGUI.BeginChangeCheck();
      
        url = EditorGUILayout.TextField("URL", url);
        key = EditorGUILayout.PasswordField("API Key", key);
        EditorGUILayout.HelpBox("The API key is stored in the following file: " + //
                                "UserSettings/DeepSeekScriptGeneratorSettings.asset. \n" + //
                                "When sharing your project with others, be sure to exclude the 'UserSettings' " + //
                                "directory to prevent unauthorized use of your API key.", MessageType.Warning);

        EditorGUILayout.Space(20);

        EditorGUILayout.BeginHorizontal();
        model = EditorGUILayout.TextField("Model", model);
        if (GUILayout.Button("DeepSeek Models")) {
            Application.OpenURL("https://api-docs.deepseek.com/zh-cn/quick_start/pricing");
        }

        EditorGUILayout.EndHorizontal();

        path = EditorGUILayout.TextField("Scripts output path", path);
        if (!path.EndsWith("/")) path += "/";
        if (!path.StartsWith("Assets/")) path = "Assets/" + path;

        EditorGUILayout.Space(20);

        useTimeout = EditorGUILayout.Toggle("Timeout", useTimeout);

        if (useTimeout) {
            EditorGUI.indentLevel++;
            timeout = EditorGUILayout.IntField("Duration (seconds)", timeout);
            if (timeout < 1) timeout = 1;
            EditorGUI.indentLevel--;
        }

        if (EditorGUI.EndChangeCheck()) {
            settings.apiKey = Encryption.Encrypt(key, userEmail);
            settings.model = model;
            settings.useTimeout = useTimeout;
            settings.timeout = timeout;
            settings.path = path;
            settings.Url = url;
            settings.Save();
        }
    }

    [SettingsProvider]
    public static SettingsProvider CreateCustomSettingsProvider() => new ScriptGeneratorSettingsProvider();
}
} // namespace AICommand