using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class EnemyAppearanceValidation
{
    private static readonly string[] Names = { "Valcom", "O'neal", "Dahl", "Minuo", "Ovape", "Doria", "Pass", "Pontan" };
    private static readonly string[] Models =
    {
        "Valcom.fbx", "O'neal_Rig_Withwalk.fbx", "Dahl.fbx", "Minuo_Rig_Withwalk.fbx",
        "Ovape.fbx", "Doria.prefab", "Pass_Rig_Withwalk.fbx", "Pontan_Rig_Withwalk.fbx"
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
    private static readonly Vector2[] ExpectedVisualDimensions =
    {
        new(0.399f, 0.7044f), new(0.72f, 0.9f), new(0.6384f, 0.7776f), new(0.7116f, 0.7338f),
        new(0.4434f, 0.5748f), new(0.6528f, 0.6138f), new(0.72f, 0.9f), new(0.72f, 0.9f)
    };

    public static void Run()
    {
        int checks = 0;
        for (int i = 0; i < Names.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{Names[i]}.prefab");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Enemy/{Models[i]}");
            if (prefab == null || source == null) throw new System.Exception($"{Names[i]} prefab/model is missing.");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                EnemyController enemy = instance.GetComponent<EnemyController>();
                Transform model = instance.transform.Find("VisualRoot/Model");
                if (enemy == null || model == null || enemy.ModelAnimator == null)
                    throw new System.Exception($"{Names[i]} must expose an animated replaceable model.");
                if (enemy.DeathEffect != DeathEffects[i]) throw new System.Exception($"{Names[i]} death effect is not configured.");
                if (i == 5 && enemy.BodyRenderer.sharedMaterial != AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Enemy MAT/Doria.mat"))
                    throw new System.Exception("Doria prefab is not using its imported blue slime material.");
                BoxCollider collider = instance.GetComponent<BoxCollider>();
                EnemyVisualScale visualScale = instance.GetComponent<EnemyVisualScale>();
                Vector2 expectedDimensions = ExpectedVisualDimensions[i];
                if (visualScale == null || Mathf.Abs(visualScale.VisualHeight - expectedDimensions.y) > 0.001f
                    || Mathf.Abs(visualScale.VisualWidth - expectedDimensions.x) > 0.001f)
                    throw new System.Exception($"{Names[i]} visual width/height controls are not set from showcase bounds.");
                if (collider == null || !collider.isTrigger
                    || !Mathf.Approximately(collider.size.y, visualScale.VisualHeight)
                    || !Mathf.Approximately(collider.size.x, visualScale.VisualWidth))
                    throw new System.Exception($"{Names[i]} gameplay collider is not fitted to the grid.");
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new System.Exception($"{Names[i]} model has no renderers.");
                Bounds bounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                float horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
                if (Mathf.Abs(bounds.size.y - visualScale.VisualHeight) > 0.12f
                    || Mathf.Abs(horizontalSize - visualScale.VisualWidth) > 0.12f)
                {
                    string parts = string.Join(", ", System.Array.ConvertAll(renderers,
                        renderer => $"{renderer.name}:{renderer.GetType().Name}={renderer.bounds.size.y:F2}"));
                    string detail = renderers[0] is SkinnedMeshRenderer skinned
                        ? $"localBounds={skinned.localBounds.size.y:F2}, modelScale={model.localScale.y:F3}, rendererLossyScale={skinned.transform.lossyScale.y:F3}"
                        : "";
                    throw new System.Exception($"{Names[i]} model does not match its width/height controls: {horizontalSize:F2}x{bounds.size.y:F2} ({parts}; {detail}).");
                }
                if (enemy.ModelAnimator.applyRootMotion || !(enemy.ModelAnimator.runtimeAnimatorController is AnimatorOverrideController))
                    throw new System.Exception($"{Names[i]} animator settings are invalid.");
                AnimatorOverrideController overrides = (AnimatorOverrideController)enemy.ModelAnimator.runtimeAnimatorController;
                List<KeyValuePair<AnimationClip, AnimationClip>> pairs = new();
                overrides.GetOverrides(pairs);
                bool hasImportedWalk = pairs.Exists(pair => pair.Key != null && pair.Key.name.ToLowerInvariant().Contains("walk")
                    && pair.Value != null && pair.Value.name == Names[i].Replace("'", "") + " Walk");
                bool expectsImportedWalk = i == 1 || i == 3 || i == 6 || i == 7;
                if (hasImportedWalk != expectsImportedWalk)
                    throw new System.Exception($"{Names[i]} imported walk animation mapping is incorrect.");
                EnemyModelMotion motion = model.GetComponent<EnemyModelMotion>();
                if (motion == null || (i == 0 || i == 2 || i == 4 || i == 5) != (motion.Amplitude > 0f))
                    throw new System.Exception($"{Names[i]} idle motion is not configured.");
                checks += 10 + (i == 5 ? 1 : 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        Debug.Log($"ENEMY APPEARANCE PASSED: {checks} checks across eight imported models, scale fits, walk mappings, idle motion, and death styles.");
    }

    public static void AssertWidth(GameObject root)
    {
        Transform model = root.transform.Find("VisualRoot/Model");
        if (model == null) throw new System.Exception(root.name + " has no model.");
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new System.Exception(root.name + " has no visible model renderer.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        EnemyVisualScale visualScale = root.GetComponent<EnemyVisualScale>();
        float horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
        if (visualScale == null || Mathf.Abs(bounds.size.y - visualScale.VisualHeight) > 0.15f
            || Mathf.Abs(horizontalSize - visualScale.VisualWidth) > 0.15f)
            throw new System.Exception(root.name + $" model size changed: {horizontalSize}x{bounds.size.y}");
    }

    public static void Capture(GameObject target, string filename)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        Transform[] transforms = target.GetComponentsInChildren<Transform>(true);
        int[] layers = new int[transforms.Length];
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        int[] masks = new int[lights.Length];
        Camera camera = new GameObject("Appearance Camera").AddComponent<Camera>();
        Light light = new GameObject("Appearance Light").AddComponent<Light>();
        RenderTexture texture = new(640, 640, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            for (int i = 0; i < transforms.Length; i++) { layers[i] = transforms[i].gameObject.layer; transforms[i].gameObject.layer = 31; }
            for (int i = 0; i < lights.Length; i++) { masks[i] = lights[i].cullingMask; lights[i].cullingMask &= ~(1 << 31); }
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.21f);
            camera.orthographic = true;
            camera.orthographicSize = 0.85f;
            Bounds bounds = target.GetComponentInChildren<Renderer>().bounds;
            camera.transform.position = bounds.center + new Vector3(2f, 1.7f, 3f);
            camera.transform.LookAt(bounds.center);
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            texture.Create();
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = texture });
            RenderTexture.active = texture;
            pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
            pixels.Apply();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../AppearanceProof"));
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, filename + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].cullingMask = masks[i];
            RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
        }
    }
}
