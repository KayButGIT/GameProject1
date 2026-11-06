using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class EnemyPrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/Enemies";
    private const string ModelFolder = "Assets/Models/Enemy";
    private const string AnimationFolder = "Assets/Animations/Enemies";
    private static readonly string[] Names = { "Valcom", "O'neal", "Dahl", "Minuo", "Ovape", "Doria", "Pass", "Pontan" };
    private static readonly string[] ModelFiles =
    {
        "Valcom.fbx", "O'neal_Rig_Withwalk.fbx", "Dahl.fbx", "Minuo_Rig_Withwalk.fbx",
        "Ovape.fbx", "Doria.prefab", "Pass_Rig_Withwalk.fbx", "Pontan_Rig_Withwalk.fbx"
    };
    private static readonly Vector2[] ShowcaseDimensions =
    {
        new(0.665f, 1.174f), new(1.656f, 1.692f), new(1.064f, 1.296f), new(1.186f, 1.223f),
        new(0.739f, 0.958f), new(1.088f, 1.023f), new(1.522f, 2.067f), new(1.925f, 1.589f)
    };
    private static readonly EnemyController.DeathPresentation[] DeathEffects =
    {
        EnemyController.DeathPresentation.BalloonPop,
        EnemyController.DeathPresentation.DebrisBurnDissolve,
        EnemyController.DeathPresentation.RagdollBurnDissolve,
        EnemyController.DeathPresentation.RagdollBurnDissolve,
        EnemyController.DeathPresentation.RagdollBurnDissolve,
        EnemyController.DeathPresentation.FluidBurst,
        EnemyController.DeathPresentation.RagdollBurnDissolve,
        EnemyController.DeathPresentation.RagdollBurnDissolve
    };

    public static void BuildAll()
    {
        for (int i = 0; i < Names.Length; i++) BuildOne(i);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Enemy prefabs now use imported models, imported walk clips where available, and per-species death effects.");
    }

    private static void BuildOne(int index)
    {
        string prefabPath = $"{PrefabFolder}/{Names[index]}.prefab";
        string modelPath = $"{ModelFolder}/{ModelFiles[index]}";
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        AnimatorOverrideController overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
            $"{AnimationFolder}/{Names[index]}.overrideController");
        if (modelAsset == null) throw new InvalidOperationException("Missing enemy model: " + modelPath);
        if (overrides == null) throw new InvalidOperationException("Missing enemy animation controller for " + Names[index]);

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            EnemyController enemy = root.GetComponent<EnemyController>();
            if (enemy == null) throw new InvalidOperationException("Enemy root has no controller: " + prefabPath);

            Transform previousVisual = root.transform.Find("VisualRoot");
            if (previousVisual != null) UnityEngine.Object.DestroyImmediate(previousVisual.gameObject);
            GameObject visualObject = new("VisualRoot");
            visualObject.transform.SetParent(root.transform, false);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(visualObject.transform, false);

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No renderers found in " + modelPath);
            model.transform.localScale = Vector3.one;
            model.transform.localPosition = Vector3.zero;
            Bounds bounds = GetLocalBounds(model.transform, renderers);
            Vector2 showcaseSize = ShowcaseDimensions[index];
            float visualHeight = Mathf.Min(showcaseSize.y * 0.6f, 0.9f);
            float visualWidth = Mathf.Min(showcaseSize.x * 0.6f, 0.72f);
            EnemyVisualScale visualScale = root.GetComponent<EnemyVisualScale>();
            if (visualScale == null) visualScale = root.AddComponent<EnemyVisualScale>();
            visualScale.Configure(model.transform, bounds, visualHeight, visualWidth);

            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = overrides;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault(candidate => candidate.isValid);
            animator.avatar = avatar;

            AnimationClip importedWalk = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip != null && clip.length > 0.05f);
            if (importedWalk != null)
            {
                AnimationClip walkCopy = CreateLoopingCopy(importedWalk, Names[index]);
                System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>> pairs = new();
                overrides.GetOverrides(pairs);
                bool foundWalk = false;
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (pairs[i].Key == null || !pairs[i].Key.name.ToLowerInvariant().Contains("walk")) continue;
                    pairs[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, walkCopy);
                    foundWalk = true;
                    break;
                }
                if (!foundWalk) throw new InvalidOperationException("Shared enemy controller has no Walk clip.");
                overrides.ApplyOverrides(pairs);
                EditorUtility.SetDirty(overrides);
            }

            EnemyModelMotion motion = model.GetComponent<EnemyModelMotion>();
            if (motion == null) motion = model.AddComponent<EnemyModelMotion>();
            motion.Amplitude = index switch { 0 => 0.12f, 2 => 0.075f, 4 => 0.1f, 5 => 0.035f, _ => 0f };
            motion.Frequency = index == 2 ? 2.6f : 1.15f;
            enemy.ModelAnimator = animator;
            enemy.BodyRenderer = renderers[0];
            enemy.DeathEffect = DeathEffects[index];
            CapsuleCollider oldCollider = root.GetComponent<CapsuleCollider>();
            if (oldCollider != null) UnityEngine.Object.DestroyImmediate(oldCollider);
            BoxCollider gameplayCollider = root.GetComponent<BoxCollider>();
            if (gameplayCollider == null) gameplayCollider = root.AddComponent<BoxCollider>();
            gameplayCollider.center = new Vector3(0f, visualHeight * 0.5f, 0f);
            gameplayCollider.size = new Vector3(visualWidth, visualHeight, visualWidth);
            gameplayCollider.isTrigger = true;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static AnimationClip CreateLoopingCopy(AnimationClip source, string enemyName)
    {
        string folder = AnimationFolder + "/Imported Walk";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(AnimationFolder, "Imported Walk");
        string safeName = enemyName.Replace("'", "");
        string path = $"{folder}/{safeName} Walk.anim";
        AnimationClip copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (copy == null)
        {
            copy = UnityEngine.Object.Instantiate(source);
            copy.name = safeName + " Walk";
            AnimationUtility.SetAnimationClipSettings(copy, new AnimationClipSettings { loopTime = true });
            AssetDatabase.CreateAsset(copy, path);
        }
        return copy;
    }

    private static Bounds GetLocalBounds(Transform root, Renderer[] renderers)
    {
        bool hasBounds = false;
        Bounds combined = default;
        foreach (Renderer renderer in renderers)
        {
            Bounds localBounds;
            bool worldSpaceBounds = renderer is SkinnedMeshRenderer;
            if (worldSpaceBounds) localBounds = renderer.bounds;
            else
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                localBounds = filter.sharedMesh.bounds;
            }

            Vector3 extents = localBounds.extents;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = localBounds.center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                point = worldSpaceBounds ? root.InverseTransformPoint(point)
                    : root.InverseTransformPoint(renderer.transform.TransformPoint(point));
                if (!hasBounds) { combined = new Bounds(point, Vector3.zero); hasBounds = true; }
                else combined.Encapsulate(point);
            }
        }
        return hasBounds ? combined : new Bounds(Vector3.zero, Vector3.one);
    }
}
