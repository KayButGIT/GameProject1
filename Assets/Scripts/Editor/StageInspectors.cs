using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageSequence))]
public sealed class StageSequenceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        StageSequence sequence = (StageSequence)target;
        if (!sequence.Validate(out string error))
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
            return;
        }
        long start = 1;
        for (int i = 0; i < sequence.Phases.Count; i++)
        {
            StageSequence.Phase phase = sequence.Phases[i];
            long end = start + phase.StageCount - 1;
            EditorGUILayout.LabelField($"Phase {i + 1}: {phase.Theme.name}", $"Stages {start}–{end}");
            start = end + 1;
        }
        EditorGUILayout.HelpBox("After the final range, the last theme continues indefinitely.", MessageType.Info);
    }
}

[CustomEditor(typeof(StageManager))]
public sealed class StageManagerEditor : Editor
{
    private int requestedStage = 1;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        StageManager manager = (StageManager)target;
        if (manager.StartingStage < 1) EditorGUILayout.HelpBox("Starting stage must be positive.", MessageType.Error);
        if (manager.Sequence != null && !manager.Sequence.Validate(out string error))
            EditorGUILayout.HelpBox(error, MessageType.Error);
        if (manager.Sequence == null) EditorGUILayout.HelpBox("No sequence: original primitive appearance.", MessageType.Info);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Current Stage", manager.CurrentStage.ToString());
        EditorGUILayout.LabelField("Current Phase", manager.CurrentPhase == 0 ? "None" : manager.CurrentPhase.ToString());
        EditorGUILayout.ObjectField("Current Theme", manager.CurrentTheme, typeof(StageTheme), false);
        requestedStage = EditorGUILayout.IntField("Load Stage Number", requestedStage);
        if (requestedStage < 1) EditorGUILayout.HelpBox("Stage number must be positive.", MessageType.Error);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || requestedStage < 1))
            if (GUILayout.Button("Load Stage")) manager.LoadStage(requestedStage);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || manager.CurrentStage < 1))
        {
            if (GUILayout.Button("Next Stage")) manager.NextStage();
            if (GUILayout.Button("Restart Stage")) manager.RestartStage();
        }
        if (!Application.isPlaying)
        {
            using (new EditorGUI.DisabledScope(requestedStage < 1))
                if (GUILayout.Button("Preview Stage In Scene")) StagePreview.Build(manager, requestedStage);
            if (GUILayout.Button("Clear Scene Preview")) StagePreview.Clear();
            using SerializedObject gameSettings = new(manager.GetComponent<BombermanPrototype>());
            if (gameSettings.FindProperty("randomSeed").intValue == 0)
                EditorGUILayout.HelpBox("Random Seed is 0, so each preview and Play session gets a new layout.", MessageType.Info);
        }
        if (Application.isPlaying) Repaint();
    }
}
