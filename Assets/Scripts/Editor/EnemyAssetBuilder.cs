using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class EnemyAssetBuilder
{
    public const string PrefabFolder = "Assets/Prefabs/Enemies";
    [MenuItem("Tools/Bomberman/Create Missing Enemy Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory("Assets/Animations/Enemies");
        Directory.CreateDirectory("Assets/Materials/Enemies");
        AssetDatabase.Refresh();
        Material faceMaterial = GetFaceMaterial();
        const string animationFolder = "Assets/Animations/Enemies/";
        AnimationClip idle = Clip(animationFolder + "EnemyIdle.anim", "Idle", true, false);
        AnimationClip walk = Clip(animationFolder + "EnemyWalk.anim", "Walk", true, true);
        AnimationClip death = Clip(animationFolder + "EnemyDeath.anim", "Death", false, false);
        string controllerPath = animationFolder + "EnemyTemplate.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState("Idle"); idleState.motion = idle;
            AnimatorState walkState = machine.AddState("Walk"); walkState.motion = walk;
            AnimatorState deathState = machine.AddState("Death"); deathState.motion = death;
            machine.defaultState = idleState;
            Transition(idleState.AddTransition(walkState), "IsMoving", AnimatorConditionMode.If);
            Transition(walkState.AddTransition(idleState), "IsMoving", AnimatorConditionMode.IfNot);
            AnimatorStateTransition die = machine.AddAnyStateTransition(deathState);
            Transition(die, "Die", AnimatorConditionMode.If);
            die.canTransitionToSelf = false;
        }
        string[] names = { "Valcom", "O'neal", "Dahl", "Minuo", "Ovape", "Doria", "Pass", "Pontan" };
        Type[] types = { typeof(ValcomEnemy), typeof(OnealEnemy), typeof(DahlEnemy), typeof(MinuoEnemy), typeof(OvapeEnemy), typeof(DoriaEnemy), typeof(PassEnemy), typeof(PontanEnemy) };
        float[] speeds = { 2f, 3.5f, 3.5f, 4f, 1.5f, 1f, 4.5f, 5.5f };
        int[] ranges = { 0, 4, 0, 5, 4, 6, 7, 9 };
        for (int i = 0; i < names.Length; i++)
        {
            string path = PrefabFolder + "/" + names[i] + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
            string materialPath = "Assets/Materials/Enemies/" + names[i] + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = names[i], color = Color.HSVToRGB(i / 8f, 0.65f, 0.95f) };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            string overridePath = animationFolder + names[i] + ".overrideController";
            AnimatorOverrideController overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (overrides == null)
            {
                overrides = new AnimatorOverrideController(controller) { name = names[i] };
                AssetDatabase.CreateAsset(overrides, overridePath);
            }
            GameObject root = new(names[i]);
            EnemyController enemy = (EnemyController)root.AddComponent(types[i]);
            enemy.MoveSpeed = speeds[i];
            enemy.DetectionRange = ranges[i];
            enemy.Chase = i == 1 || i == 4 ? EnemyController.ChaseMode.Timed : i == 3 ? EnemyController.ChaseMode.Escape
                : i == 5 ? EnemyController.ChaseMode.Memory : i == 6 ? EnemyController.ChaseMode.PersistentAfterSight
                : i == 7 ? EnemyController.ChaseMode.Always : EnemyController.ChaseMode.None;
            if (i == 2) { enemy.PatrolMinCells = 6; enemy.PatrolMaxCells = 10; }
            if (i == 4)
            {
                enemy.AcquisitionProbability = 0.2f;
                enemy.ChaseMinSeconds = enemy.ChaseMaxSeconds = 2f;
                enemy.DetectionCooldown = 3f;
            }
            if (i == 5) { enemy.MemorySeconds = 3f; enemy.AvoidBombLanes = true; }
            GameObject visual = new("VisualRoot"); visual.transform.SetParent(root.transform, false);
            GameObject model = new("Model"); model.transform.SetParent(visual.transform, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Mesh"; body.transform.SetParent(model.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.55f, 0.6f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            enemy.BodyRenderer = body.GetComponent<Renderer>(); enemy.BodyRenderer.sharedMaterial = material;
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "Facing"; face.transform.SetParent(body.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.4f, 0.48f);
            face.transform.localScale = new Vector3(0.45f, 0.2f, 0.12f);
            UnityEngine.Object.DestroyImmediate(face.GetComponent<Collider>());
            face.GetComponent<Renderer>().sharedMaterial = faceMaterial;
            enemy.ModelAnimator = model.AddComponent<Animator>();
            enemy.ModelAnimator.runtimeAnimatorController = overrides;
            enemy.ModelAnimator.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Enemy assets generated: eight prefabs, eight overrides, three clips, shared controller.");
    }
    private static void Transition(AnimatorStateTransition transition, string parameter, AnimatorConditionMode condition)
    {
        transition.hasExitTime = false; transition.duration = 0.05f;
        transition.AddCondition(condition, 0f, parameter);
    }

    private static Material GetFaceMaterial()
    {
        const string path = "Assets/Materials/Enemies/EnemyFace.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Enemy placeholders require URP/Lit.");
        material = new Material(shader) { name = "EnemyFace", color = Color.white };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    [MenuItem("Tools/Bomberman/Repair Placeholder Appearance")]
    public static void RepairPlaceholderAppearance()
    {
        Material faceMaterial = GetFaceMaterial();
        foreach (string name in new[] { "Idle", "Walk", "Death" })
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Enemies/Enemy" + name + ".anim");
            if (clip == null) throw new InvalidOperationException("Missing shipped placeholder clip: " + name);
            SetPlaceholderWidth(clip);
            EditorUtility.SetDirty(clip);
        }
        foreach (string name in new[] { "Valcom", "O'neal", "Dahl", "Minuo", "Ovape", "Doria", "Pass", "Pontan" })
        {
            string path = PrefabFolder + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform face = root.transform.Find("VisualRoot/Model/Mesh/Facing");
                if (face == null || !face.TryGetComponent(out Renderer renderer)) continue;
                MeshFilter body = face.parent.GetComponent<MeshFilter>();
                if (body == null || body.sharedMesh == null || body.sharedMesh.name != "Capsule") continue;
                // Only replace Unity's built-in default material, never team-assigned materials.
                Material current = renderer.sharedMaterial;
                if (current != null && AssetDatabase.GetAssetPath(current) != "Resources/unity_builtin_extra") continue;
                renderer.sharedMaterial = faceMaterial;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void SetPlaceholderWidth(AnimationClip clip)
    {
        float duration = clip.length;
        foreach (string axis in new[] { "x", "z" })
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Mesh", typeof(Transform), "m_LocalScale." + axis),
                AnimationCurve.Constant(0f, duration, 0.6f));
    }
    private static AnimationClip Clip(string path, string name, bool loop, bool walk)
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;
        AnimationClip clip = new() { name = name, frameRate = 30f };
        if (name == "Death")
        {
            clip.SetCurve("Mesh", typeof(Transform), "localScale.y", AnimationCurve.EaseInOut(0f, 0.55f, 0.75f, 0.02f));
            clip.SetCurve("Mesh", typeof(Transform), "localPosition.y", AnimationCurve.EaseInOut(0f, 0.55f, 0.75f, 0.02f));
        }
        else
        {
            float duration = walk ? 0.4f : 1f;
            clip.SetCurve("Mesh", typeof(Transform), "localPosition.y", new AnimationCurve(
                new Keyframe(0f, 0.55f), new Keyframe(duration / 2f, walk ? 0.67f : 0.575f), new Keyframe(duration, 0.55f)));
            clip.SetCurve("Mesh", typeof(Transform), "localScale.y", AnimationCurve.Constant(0f, duration, 0.55f));
        }
        SetPlaceholderWidth(clip);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }
}
