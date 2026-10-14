using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class SoundManagerWindow : EditorWindow
{
    private const string SettingsPath = "Assets/Resources/BombermanSoundSettings.asset";
    private const string ResourceFolder = "Assets/Resources";

    [SerializeField] private Vector2 scroll;
    private SoundManagerSettings settings;
    private Editor settingsEditor;

    [MenuItem("Tools/Bomberman/Sound Manager")]
    public static void Open()
    {
        SoundManagerWindow window = GetWindow<SoundManagerWindow>("Sound Manager");
        window.minSize = new Vector2(360f, 420f);
        window.EnsureSettings();
        window.Show();
    }

    private void OnEnable()
    {
        EnsureSettings();
    }

    private void OnDisable()
    {
        if (settingsEditor != null) DestroyImmediate(settingsEditor);
        settingsEditor = null;
    }

    private void OnInspectorUpdate() => Repaint();

    private void EnsureSettings()
    {
        SoundManagerSettings found = AssetDatabase.LoadAssetAtPath<SoundManagerSettings>(SettingsPath);
        if (found == null)
        {
            Directory.CreateDirectory(ResourceFolder);
            found = CreateInstance<SoundManagerSettings>();
            found.name = "Bomberman Sound Settings";
            AssetDatabase.CreateAsset(found, SettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (settings == found) return;
        if (settingsEditor != null) DestroyImmediate(settingsEditor);
        settings = found;
        settingsEditor = Editor.CreateEditor(settings);
    }

    private void OnGUI()
    {
        EnsureSettings();
        EditorGUILayout.HelpBox(
            "These shared settings load automatically in every scene. No Sound Manager GameObject is needed in scene hierarchies.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField("Settings Asset", settings, typeof(SoundManagerSettings), false);
            if (GUILayout.Button("Ping", GUILayout.Width(48f))) EditorGUIUtility.PingObject(settings);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        settingsEditor?.OnInspectorGUI();
        EditorGUILayout.EndScrollView();

        if (GUI.changed && settings != null)
        {
            EditorUtility.SetDirty(settings);
        }
    }
}
