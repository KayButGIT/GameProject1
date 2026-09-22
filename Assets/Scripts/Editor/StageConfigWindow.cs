using UnityEditor;
using UnityEngine;

// The Stage Theme fields in a window of their own, picked from a Stage Sequence,
// so the Inspector stays free for anything else.
public sealed class StageConfigWindow : EditorWindow
{
    private const string ThemeKey = "Bomberman.StageConfigTheme";
    private const string SequenceKey = "Bomberman.StageConfigSequence";
    private const string FoldoutKey = "Bomberman.StageConfigSequenceOpen";

    [SerializeField] private StageSequence sequence;
    [SerializeField] private StageTheme theme;
    [SerializeField] private Vector2 scroll;
    private Editor themeEditor;
    private Editor sequenceEditor;

    /// <summary>True while this window draws the theme's own editor, which then hides its Open button.</summary>
    public static bool DrawingEmbedded { get; private set; }

    public StageSequence Sequence => sequence;
    public StageTheme Theme => theme;
    public Editor ThemeEditor => themeEditor;

    [MenuItem("Tools/Bomberman/Stage Config")]
    public static StageConfigWindow Open()
    {
        StageConfigWindow window = GetWindow<StageConfigWindow>("Stage Config");
        window.minSize = new Vector2(320f, 300f);
        window.Show();
        return window;
    }

    public void Bind(StageTheme next)
    {
        if (theme != next)
        {
            theme = next;
            DestroyEditor(ref themeEditor);
        }

        if (theme != null && themeEditor == null) themeEditor = Editor.CreateEditor(theme);
        EditorPrefs.SetString(ThemeKey, GuidOf(theme));
        Repaint();
    }

    public void BindSequence(StageSequence next)
    {
        if (sequence != next)
        {
            sequence = next;
            DestroyEditor(ref sequenceEditor);
        }

        if (sequence != null && sequenceEditor == null) sequenceEditor = Editor.CreateEditor(sequence);
        EditorPrefs.SetString(SequenceKey, GuidOf(sequence));
        // A sequence always brings its own first theme, unless the bound one is already part of it.
        if (sequence != null && !Holds(sequence, theme)) Bind(FirstTheme(sequence));
        Repaint();
    }

    /// <summary>Phase labels with their stage ranges, as the Stage Sequence inspector prints them.</summary>
    public string[] PhaseLabels()
    {
        if (sequence == null || sequence.Phases.Count == 0) return new string[0];
        string[] labels = new string[sequence.Phases.Count];
        long start = 1;
        for (int i = 0; i < labels.Length; i++)
        {
            StageSequence.Phase phase = sequence.Phases[i];
            StageTheme phaseTheme = phase != null ? phase.Theme : null;
            int count = phase != null ? Mathf.Max(1, phase.StageCount) : 1;
            long end = start + count - 1;
            // The final phase keeps running once its range is over.
            string range = i == labels.Length - 1 ? $"Stages {start} and up" : $"Stages {start}–{end}";
            labels[i] = $"Phase {i + 1} — {(phaseTheme == null ? "None" : phaseTheme.name)} ({range})";
            start = end + 1;
        }

        return labels;
    }

    private void OnEnable()
    {
        BindSequence(sequence != null ? sequence : Remembered<StageSequence>(SequenceKey) ?? SceneSequence());
        if (theme == null) Bind(Remembered<StageTheme>(ThemeKey) ?? FirstTheme(sequence));
        else Bind(theme);
    }

    private void OnDisable()
    {
        DestroyEditor(ref themeEditor);
        DestroyEditor(ref sequenceEditor);
    }

    // Values can change from the preview or another window, so the view keeps up on its own.
    private void OnInspectorUpdate() => Repaint();

    private void DestroyEditor(ref Editor editor)
    {
        if (editor != null) DestroyImmediate(editor);
        editor = null;
    }

    private static string GuidOf(Object asset)
    {
        return asset == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    }

    private static T Remembered<T>(string key) where T : Object
    {
        string path = AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(key, ""));
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static StageSequence SceneSequence()
    {
        StageManager manager = FindFirstObjectByType<StageManager>();
        return manager != null ? manager.Sequence : null;
    }

    private static StageTheme FirstTheme(StageSequence from)
    {
        if (from == null) return null;
        foreach (StageSequence.Phase phase in from.Phases)
            if (phase != null && phase.Theme != null) return phase.Theme;
        return null;
    }

    private static bool Holds(StageSequence from, StageTheme wanted)
    {
        if (from == null || wanted == null) return false;
        foreach (StageSequence.Phase phase in from.Phases)
            if (phase != null && phase.Theme == wanted) return true;
        return false;
    }

    private void OnGUI()
    {
        StageSequence pickedSequence = (StageSequence)EditorGUILayout.ObjectField("Sequence", sequence, typeof(StageSequence), false);
        if (pickedSequence != sequence) BindSequence(pickedSequence);

        DrawPhasePicker();
        StageTheme picked = (StageTheme)EditorGUILayout.ObjectField("Stage Theme", theme, typeof(StageTheme), false);
        if (picked != theme) Bind(picked);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSequenceFoldout();

        if (theme == null)
        {
            EditorGUILayout.HelpBox("Pick a Stage Theme, or a Stage Sequence whose phases have themes.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.Space();
            if (themeEditor == null) themeEditor = Editor.CreateEditor(theme);
            // The theme's own editor is reused, so this window and the Inspector always show the same controls.
            DrawingEmbedded = true;
            try { themeEditor.OnInspectorGUI(); }
            finally { DrawingEmbedded = false; }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPhasePicker()
    {
        string[] labels = PhaseLabels();
        if (labels.Length == 0)
        {
            EditorGUILayout.HelpBox(sequence == null
                ? "No sequence yet. Drag Main Sequence in, or open the game scene."
                : "That sequence has no phases.", MessageType.None);
            return;
        }

        int current = -1;
        for (int i = 0; i < labels.Length; i++)
        {
            StageSequence.Phase phase = sequence.Phases[i];
            if (phase != null && phase.Theme != null && phase.Theme == theme) current = i;
        }

        int selected = EditorGUILayout.Popup("Phase", current, labels);
        if (selected >= 0 && selected != current) Bind(sequence.Phases[selected].Theme);
    }

    private void DrawSequenceFoldout()
    {
        if (sequence == null) return;
        bool open = EditorGUILayout.Foldout(EditorPrefs.GetBool(FoldoutKey, false), "Sequence", true);
        EditorPrefs.SetBool(FoldoutKey, open);
        if (!open) return;

        if (sequenceEditor == null) sequenceEditor = Editor.CreateEditor(sequence);
        EditorGUI.indentLevel++;
        sequenceEditor.OnInspectorGUI();
        EditorGUI.indentLevel--;
        // Editing a phase can swap the theme under the bound one, so the window follows.
        if (!Holds(sequence, theme)) Bind(FirstTheme(sequence));
        EditorGUILayout.Space();
    }
}
