using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SoundManagerSettings))]
public sealed class SoundManagerSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SoundManagerSettings settings = (SoundManagerSettings)target;
        StageSequence sequence = StageConfigWindow.ResolveSequenceForSoundManager();
        int phaseCount = sequence != null && sequence.Phases != null && sequence.Phases.Count > 0
            ? sequence.Phases.Count
            : 1;
        int previousPhaseCount = settings.footstepPhases != null ? settings.footstepPhases.Count : 0;
        settings.EnsureFootstepPhaseCount(phaseCount);
        if (settings.footstepPhases.Count != previousPhaseCount)
            EditorUtility.SetDirty(settings);

        serializedObject.Update();
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "footstepPhases")
            {
                DrawFootstepPhases(iterator.Copy(), sequence, phaseCount);
                continue;
            }

            if (iterator.propertyPath == "m_Script")
                continue;

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawFootstepPhases(SerializedProperty phases, StageSequence sequence, int phaseCount)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Walk by Sequence Phase", EditorStyles.boldLabel);
        if (sequence != null)
            EditorGUILayout.HelpBox($"Following {sequence.name}: {phaseCount} phase{(phaseCount == 1 ? "" : "s")}. Empty pools use Phase 1 clips at that phase's volume.", MessageType.None);
        else
            EditorGUILayout.HelpBox("No Stage Sequence is selected or available in the open game scene. Showing Phase 1. Empty pools use Phase 1 clips at that phase's volume.", MessageType.Info);

        for (int i = 0; i < phaseCount; i++)
        {
            SerializedProperty phase = phases.GetArrayElementAtIndex(i);
            SerializedProperty clips = phase.FindPropertyRelative("footsteps");
            SerializedProperty volume = phase.FindPropertyRelative("volume");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Phase {i + 1}", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(clips, new GUIContent("Footsteps"), true);
                EditorGUILayout.PropertyField(volume, new GUIContent("Footstep Volume"));
            }
        }
    }
}
