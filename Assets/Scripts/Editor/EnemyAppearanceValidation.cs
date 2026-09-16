using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class EnemyAppearanceValidation
{
    public static void Run()
    {
        int samples = 0;
        foreach (string name in new[] { "Valcom", "O'neal", "Dahl", "Minuo", "Ovape", "Doria", "Pass", "Pontan" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.PrefabFolder + "/" + name + ".prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Transform model = instance.transform.Find("VisualRoot/Model");
                Transform mesh = model.Find("Mesh");
                Material face = mesh.Find("Facing").GetComponent<Renderer>().sharedMaterial;
                if (face == null || face.shader.name != "Universal Render Pipeline/Lit" || face.color != Color.white)
                    throw new Exception(name + " must use a white URP face material.");
                foreach (string state in new[] { "Idle", "Walk", "Death" })
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Enemies/Enemy" + state + ".anim");
                    for (int frame = 0; frame <= 30; frame++)
                    {
                        clip.SampleAnimation(model.gameObject, clip.length * frame / 30f);
                        AssertWidth(instance);
                        if (state != "Death" && Mathf.Abs(mesh.localScale.y - 0.55f) > 0.0001f)
                            throw new Exception("Idle/Walk height changed.");
                        samples++;
                    }
                    if (state == "Death" && Mathf.Abs(mesh.localScale.y - 0.02f) > 0.0001f)
                        throw new Exception("Death squash was lost.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        Debug.Log("ENEMY APPEARANCE PASSED: " + samples + " animation samples and eight white URP faces.");
    }

    public static void AssertWidth(GameObject root)
    {
        Vector3 scale = root.transform.Find("VisualRoot/Model/Mesh").localScale;
        if (Mathf.Abs(scale.x - 0.6f) > 0.0001f || Mathf.Abs(scale.z - 0.6f) > 0.0001f)
            throw new Exception(root.name + " animation changed placeholder width: " + scale);
    }

    // Used only by the isolated smoke run; camera pose and lighting are identical relative to each model.
    public static void Capture(GameObject target, string filename)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        Transform[] transforms = target.GetComponentsInChildren<Transform>();
        int[] layers = new int[transforms.Length];
        Light[] oldLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        int[] masks = new int[oldLights.Length];
        Camera camera = new GameObject("Appearance Camera").AddComponent<Camera>();
        Light light = new GameObject("Appearance Light").AddComponent<Light>();
        RenderTexture texture = new(640, 640, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            for (int i = 0; i < transforms.Length; i++) { layers[i] = transforms[i].gameObject.layer; transforms[i].gameObject.layer = 31; }
            for (int i = 0; i < oldLights.Length; i++) { masks[i] = oldLights[i].cullingMask; oldLights[i].cullingMask &= ~(1 << 31); }
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.21f);
            camera.orthographic = true; camera.orthographicSize = 0.85f;
            camera.transform.position = target.transform.TransformPoint(new Vector3(2f, 1.7f, 3f));
            camera.transform.LookAt(target.transform.TransformPoint(new Vector3(0f, 0.55f, 0f)));
            light.type = LightType.Directional; light.intensity = 1.5f; light.cullingMask = 1 << 31;
            light.transform.rotation = target.transform.rotation * Quaternion.Euler(40f, -30f, 0f);
            texture.Create();
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = texture });
            RenderTexture.active = texture;
            pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); pixels.Apply();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../AppearanceProof"));
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, filename + ".png"), pixels.EncodeToPNG());
            Debug.Log("Enemy appearance render saved: " + Path.Combine(output, filename + ".png"));
        }
        finally
        {
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            for (int i = 0; i < oldLights.Length; i++) if (oldLights[i] != null) oldLights[i].cullingMask = masks[i];
            RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(light.gameObject);
        }
    }
}
