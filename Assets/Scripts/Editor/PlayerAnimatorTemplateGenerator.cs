using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerAnimatorTemplateGenerator
{
    private const string FolderPath = "Assets/Animations/Player";
    private const string ControllerPath = FolderPath + "/BombermanPlayerTemplate.controller";
    private const string IdleClipPath = FolderPath + "/PlayerIdle.anim";
    private const string WalkClipPath = FolderPath + "/PlayerWalk.anim";
    private const string DeadClipPath = FolderPath + "/PlayerDead.anim";
    private const string SourceIdleClipPath = "Assets/Animations/Transfer/BombermanAnimation_Idle.fbx";
    private const string SourceWalkClipPath = "Assets/Animations/Transfer/BombermanAnimation_Walk.fbx";
    private const string PrototypeScenePath = "Assets/Scenes/Prototype.unity";

    [InitializeOnLoadMethod]
    private static void EnsureSourceClipsLoopOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            ConfigureLoopingSourceClip(SourceIdleClipPath);
            ConfigureLoopingSourceClip(SourceWalkClipPath);
        };
    }

    [MenuItem("Bomberman/Create Player Animator Template")]
    public static void CreateTemplate()
    {
        EnsureFolder(FolderPath);
        ConfigureLoopingSourceClip(SourceIdleClipPath);
        ConfigureLoopingSourceClip(SourceWalkClipPath);

        AnimationClip idleClip = LoadSourceClip(SourceIdleClipPath) ?? LoadOrCreateClip(IdleClipPath, ConfigureIdleClip);
        AnimationClip walkClip = LoadSourceClip(SourceWalkClipPath) ?? LoadOrCreateClip(WalkClipPath, ConfigureWalkClip);
        AnimationClip deadClip = LoadOrCreateClip(DeadClipPath, ConfigureDeadClip);

        AnimatorController controller = LoadOrCreateController();
        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "WalkCycleSpeed", AnimatorControllerParameterType.Float, 1f);
        EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = EnsureState(stateMachine, "Idle", idleClip, new Vector3(250f, 80f, 0f));
        AnimatorState walkState = EnsureState(stateMachine, "Walk", walkClip, new Vector3(250f, 180f, 0f));
        AnimatorState deadState = EnsureState(stateMachine, "Dead", deadClip, new Vector3(520f, 130f, 0f));
        walkState.speedParameterActive = true;
        walkState.speedParameter = "WalkCycleSpeed";
        stateMachine.defaultState = idleState;

        EnsureBoolTransition(idleState, walkState, "IsMoving", true);
        EnsureBoolTransition(walkState, idleState, "IsMoving", false);
        EnsureTriggerTransition(stateMachine, deadState, "Die");

        AssignControllerToPrototype(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Player animator template ready: {ControllerPath}");
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static AnimationClip LoadOrCreateClip(string clipPath, System.Action<AnimationClip> configure)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        configure(clip);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip LoadSourceClip(string clipPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(clipPath);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip)
            {
                return clip;
            }
        }

        return null;
    }

    private static void ConfigureLoopingSourceClip(string clipPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips == null || clips.Length == 0)
        {
            return;
        }

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            changed |= !clip.loopTime
                || !clip.loopPose
                || !clip.lockRootRotation
                || !clip.lockRootHeightY
                || !clip.lockRootPositionXZ
                || !clip.keepOriginalPositionY
                || !clip.keepOriginalPositionXZ
                || clip.wrapMode != WrapMode.Loop;

            clip.loopTime = true;
            clip.loopPose = true;
            clip.wrapMode = WrapMode.Loop;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clips[i] = clip;
        }

        importer.clipAnimations = clips;
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static AnimatorController LoadOrCreateController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        return controller != null
            ? controller
            : AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
    }

    private static void ConfigureIdleClip(AnimationClip clip)
    {
        clip.name = Path.GetFileNameWithoutExtension(IdleClipPath);
        clip.frameRate = 30f;
        clip.wrapMode = WrapMode.Loop;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.SetCurve("Body", typeof(Transform), "m_LocalPosition.y", AnimationCurve.Constant(0f, 1f, 0.55f));
    }

    private static void ConfigureWalkClip(AnimationClip clip)
    {
        clip.name = Path.GetFileNameWithoutExtension(WalkClipPath);
        clip.frameRate = 30f;
        clip.wrapMode = WrapMode.Loop;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.SetCurve("Body", typeof(Transform), "m_LocalPosition.y", new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.12f, 0.64f),
            new Keyframe(0.24f, 0.55f),
            new Keyframe(0.36f, 0.64f),
            new Keyframe(0.48f, 0.55f)));
    }

    private static void ConfigureDeadClip(AnimationClip clip)
    {
        clip.name = Path.GetFileNameWithoutExtension(DeadClipPath);
        clip.frameRate = 30f;
        clip.wrapMode = WrapMode.Once;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.SetCurve("Body", typeof(Transform), "m_LocalScale.y", new AnimationCurve(
            new Keyframe(0f, 0.8f),
            new Keyframe(0.25f, 0.45f),
            new Keyframe(0.55f, 0.18f)));
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType parameterType,
        float defaultFloat = 0f)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
            {
                if (parameter.type == AnimatorControllerParameterType.Float)
                {
                    parameter.defaultFloat = defaultFloat;
                }

                return;
            }
        }

        AnimatorControllerParameter newParameter = new()
        {
            name = parameterName,
            type = parameterType,
            defaultFloat = defaultFloat
        };
        controller.AddParameter(newParameter);
    }

    private static AnimatorState EnsureState(
        AnimatorStateMachine stateMachine,
        string stateName,
        Motion motion,
        Vector3 position)
    {
        AnimatorState state = FindState(stateMachine, stateName);
        if (state == null)
        {
            state = stateMachine.AddState(stateName, position);
        }

        state.motion = motion;
        return state;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                return childState.state;
            }
        }

        return null;
    }

    private static void EnsureBoolTransition(
        AnimatorState source,
        AnimatorState destination,
        string parameterName,
        bool value)
    {
        foreach (AnimatorStateTransition transition in source.transitions)
        {
            if (transition.destinationState == destination && HasBoolCondition(transition, parameterName, value))
            {
                return;
            }
        }

        AnimatorStateTransition newTransition = source.AddTransition(destination);
        ConfigureInstantTransition(newTransition);
        newTransition.AddCondition(
            value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            parameterName);
    }

    private static void EnsureTriggerTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState destination,
        string parameterName)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            if (transition.destinationState == destination && HasTriggerCondition(transition, parameterName))
            {
                return;
            }
        }

        AnimatorStateTransition newTransition = stateMachine.AddAnyStateTransition(destination);
        ConfigureInstantTransition(newTransition);
        newTransition.canTransitionToSelf = false;
        newTransition.AddCondition(AnimatorConditionMode.If, 0f, parameterName);
    }

    private static void ConfigureInstantTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = 0.16f;
        transition.offset = 0f;
        transition.exitTime = 0f;
    }

    private static bool HasBoolCondition(AnimatorStateTransition transition, string parameterName, bool value)
    {
        AnimatorConditionMode expectedMode = value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == parameterName && condition.mode == expectedMode)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTriggerCondition(AnimatorStateTransition transition, string parameterName)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == parameterName && condition.mode == AnimatorConditionMode.If)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssignControllerToPrototype(RuntimeAnimatorController controller)
    {
        if (!File.Exists(PrototypeScenePath))
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
        BombermanPrototype prototype = Object.FindFirstObjectByType<BombermanPrototype>();
        if (prototype == null)
        {
            return;
        }

        SerializedObject serializedPrototype = new(prototype);
        SerializedProperty property = serializedPrototype.FindProperty("playerAnimatorController");
        if (property == null)
        {
            return;
        }

        property.objectReferenceValue = controller;
        serializedPrototype.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
