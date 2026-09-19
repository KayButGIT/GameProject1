using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class GrassBlockValidation
{
    private const string Running = "Bomberman.GrassValidation";
    private static int phase;
    private static double deadline, watchdog;
    private static Camera camera;
    private static GrassBlock sample;
    private static Material material;
    private static Color32[] stillFrame, windFrame;
    private static BombermanPrototype game;
    private static CellObject destroyedBlock;
    private static bool failed;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project with graphics enabled.");
        GrassBlockBuilder.BuildFloor();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassBlockBuilder.PrefabPath);
        Check(prefab != null && prefab.GetComponentsInChildren<Collider>().Length == 0, "Prefab has no gameplay colliders");
        GrassBlock authoring = prefab.GetComponent<GrassBlock>();
        Mesh mesh = authoring.GrassMesh.sharedMesh;
        Check(mesh.vertexCount == 1120 && mesh.triangles.Length == 2400, "160 blades in one mesh");
        Check(prefab.transform.localScale == Vector3.one && prefab.transform.localPosition == Vector3.zero, "Identity prefab root");
        Check(prefab.transform.Find("Soil").GetComponent<MeshFilter>().sharedMesh.bounds.size == Vector3.one, "Unit soil cube");
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (uv[i].y == 0) Check(Mathf.Abs(vertices[i].y - 0.502f) < 0.0001f, "Fixed blade roots");
            if (uv[i].y == 1) Check(vertices[i].y >= 0.652f && vertices[i].y <= 0.8021f, "Blade height range");
            foreach (Vector3 offset in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                Check(mesh.bounds.Contains(vertices[i] + offset * 0.12f), "Wind-aware bounds");
        }
        Mesh rebuilt = GrassBlockBuilder.BuildGrass(authoring);
        Check(rebuilt.vertices.SequenceEqual(vertices), "Seeded rebuild is reproducible");
        Object.DestroyImmediate(rebuilt);
        GameObject scratch = new("Settings check");
        GrassBlock changed = scratch.AddComponent<GrassBlock>();
        changed.BladeCount = 80;
        changed.MinimumHeight = changed.MaximumHeight = 0.4f;
        Mesh custom = GrassBlockBuilder.BuildGrass(changed);
        Check(custom.vertexCount == 560 && custom.vertices.Max(v => v.y) > 0.90f, "Editable density and height");
        Object.DestroyImmediate(custom);
        Object.DestroyImmediate(scratch);

        PrefabUtility.InstantiatePrefab(prefab);
        Camera preview = new GameObject("Grass Preview Camera").AddComponent<Camera>();
        preview.tag = "MainCamera";
        preview.transform.position = new Vector3(1.65f, 1.45f, -2.5f);
        preview.transform.LookAt(new Vector3(0, 0.12f, 0));
        preview.orthographic = true;
        preview.orthographicSize = 0.98f;
        preview.clearFlags = CameraClearFlags.SolidColor;
        preview.backgroundColor = new Color(0.12f, 0.19f, 0.23f);
        preview.allowHDR = false;
        preview.allowMSAA = false;
        UniversalAdditionalCameraData data = preview.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        Light sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(45, -35, 0);
        sun.intensity = 1.0f;
        sun.shadows = LightShadows.Hard;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.4f, 0.45f, 0.5f);
        RenderSettings.fog = false;
        EditorSceneManager.SaveScene(preview.gameObject.scene, "Assets/GrassPreviewSmoke.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Running, false)) return;
        watchdog = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }

    private static void Log(string message, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception) failed = true;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Grass validation: " + message);
    }
    private static Color32[] Capture(string filename = null)
    {
        RenderTexture target = RenderTexture.GetTemporary(768, 768, 24, RenderTextureFormat.ARGB32);
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D texture = new(768, 768, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
        texture.Apply();
        Color32[] pixels = texture.GetPixels32();
        if (filename != null)
        {
            Directory.CreateDirectory("GrassValidationOutput");
            File.WriteAllBytes("GrassValidationOutput/" + filename, texture.EncodeToPNG());
        }
        Object.Destroy(texture);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        return pixels;
    }
    private static int Difference(Color32[] a, Color32[] b)
    {
        int count = 0;
        for (int i = 0; i < a.Length; i++)
            if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 6) count++;
        return count;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime or shader error.");
            if (EditorApplication.timeSinceStartup > watchdog) throw new Exception("Grass validation timed out.");
            if (phase == 0)
            {
                sample = Object.FindFirstObjectByType<GrassBlock>();
                camera = Camera.main;
                if (sample == null || camera == null) return;
                // Batch Mode has no visible Game view to initialize the render pipeline.
                if (RenderPipelineManager.currentPipeline == null) camera.Render();
                material = new Material(sample.GrassMesh.GetComponent<Renderer>().sharedMaterial);
                sample.GrassMesh.GetComponent<Renderer>().sharedMaterial = material;
                material.SetFloat("_WindStrength", 0);
                // Allow the first rendering frame to initialize lighting and shadows.
                phase = 1; deadline = Time.timeAsDouble + 0.5;
            }
            else if (phase == 1 && Time.timeAsDouble >= deadline)
            {
                stillFrame = Capture();
                phase = 2; deadline = Time.timeAsDouble + 0.7;
            }
            else if (phase == 2 && Time.timeAsDouble >= deadline)
            {
                Check(Difference(stillFrame, Capture()) == 0, "Zero wind produces identical rendered frames");
                material.SetFloat("_WindStrength", 0.045f);
                windFrame = Capture("GrassBlock.png");
                phase = 3; deadline = Time.timeAsDouble + 0.8;
            }
            else if (phase == 3 && Time.timeAsDouble >= deadline)
            {
                int changed = Difference(windFrame, Capture("GrassBlock_Wind.png"));
                Check(changed > 100, "Wind visibly animates blades");
                Check(!ShaderUtil.ShaderHasError(material.shader), "Shader compiled without errors");
                // Freeze shader time by pausing the simulation; shift object and camera equally.
                Time.timeScale = 0;
                phase = 4;
            }
            else if (phase == 4)
            {
                windFrame = Capture();
                Vector3 shift = Vector3.right * 1.2f;
                sample.transform.position += shift;
                camera.transform.position += shift;
                Check(Difference(windFrame, Capture()) > 100, "World position changes wind phase");
                sample.gameObject.SetActive(false);
                Time.timeScale = 1;
                StageTheme theme = ScriptableObject.CreateInstance<StageTheme>();
                theme.BreakableBlock.Variants.Add(new StageTheme.VisualVariant { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassBlockBuilder.PrefabPath) });
                theme.Floor.Variants.Add(new StageTheme.VisualVariant { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassBlockBuilder.FloorPrefabPath) });
                StageSequence sequence = ScriptableObject.CreateInstance<StageSequence>();
                sequence.Phases.Add(new StageSequence.Phase { Theme = theme, StageCount = 10 });
                game = new GameObject("Grass Integration Game").AddComponent<BombermanPrototype>();
                SerializedObject settings = new(game);
                settings.FindProperty("width").intValue = 9;
                settings.FindProperty("height").intValue = 7;
                settings.FindProperty("randomSeed").intValue = 123;
                settings.FindProperty("destructibleDensity").floatValue = 0.4f;
                settings.FindProperty("spawnEnemies").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                StageManager manager = game.gameObject.AddComponent<StageManager>();
                settings = new SerializedObject(manager);
                settings.FindProperty("sequence").objectReferenceValue = sequence;
                settings.ApplyModifiedPropertiesWithoutUndo();
                phase = 5;
            }
            else if (phase == 5 && game.IsReady)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                BombermanMap map = (BombermanMap)typeof(BombermanPrototype).GetField("map", flags).GetValue(game);
                Transform floor = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t => t.name == "Floor 1,1");
                GrassBlock floorGrass = floor.GetComponentInChildren<GrassBlock>();
                Check(floorGrass != null, "Floor slot creates GrassFloor");
                Check(floorGrass.transform.Find("Soil").GetComponent<Renderer>().bounds.size.y < 0.081f, "Floor soil stays thin");
                Mesh blades = floorGrass.GrassMesh.sharedMesh;
                Vector3[] bladeVertices = blades.vertices;
                Vector2[] bladeUV = blades.uv;
                float rootY = floor.position.y + 0.08f * 0.502f;
                float tallestBlade = 0;
                for (int i = 0; i < bladeVertices.Length; i++)
                {
                    float worldY = floorGrass.GrassMesh.transform.TransformPoint(bladeVertices[i]).y;
                    if (bladeUV[i].y == 0) Check(Mathf.Abs(worldY - rootY) < 0.0001f, "Floor blade roots touch cap");
                    if (bladeUV[i].y == 1)
                    {
                        float height = worldY - rootY;
                        Check(height >= 0.1499f && height <= 0.3001f, "Floor preserves full blade height");
                        tallestBlade = Mathf.Max(tallestBlade, height);
                    }
                }
                Check(tallestBlade > 0.28f && map.IsWalkable(new Vector2Int(1, 1)), "Tall grass remains walkable");
                Check(floor.GetComponentsInChildren<Collider>().Length == 0, "Floor adds no collider");
                destroyedBlock = Object.FindObjectsByType<CellObject>(FindObjectsSortMode.None).First(c => map.GetCellKind(c.Cell) == CellKind.Destructible);
                Check(destroyedBlock.transform.Find("Visual").GetComponent<GrassBlock>() != null, "Theme slot creates grass prefab");
                Check(destroyedBlock.GetComponentsInChildren<Collider>().Length == 1, "Only original gameplay collider");
                Check(destroyedBlock.GetComponent<BoxCollider>().size == Vector3.one && destroyedBlock.transform.localScale == new Vector3(0.92f, 0.8f, 0.92f), "Existing block dimensions preserved");
                typeof(BombermanPrototype).GetMethod("Explode", flags).Invoke(game, new object[] { destroyedBlock.Cell + Vector2Int.left });
                Check(map.IsWalkable(destroyedBlock.Cell) && !destroyedBlock.gameObject.activeSelf, "Explosion clears cell and model");
                phase = 6; deadline = Time.timeAsDouble + 0.1;
            }
            else if (phase == 6 && Time.timeAsDouble >= deadline)
            {
                Check(destroyedBlock == null, "Entire block and grass mesh instance removed");
                StageTheme previewTheme = Object.Instantiate(AssetDatabase.LoadAssetAtPath<StageTheme>("Assets/Stages/Brick.asset"));
                previewTheme.Floor.Variants.Clear();
                previewTheme.Floor.Variants.Add(new StageTheme.VisualVariant { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassBlockBuilder.FloorPrefabPath) });
                game.GetComponent<StageManager>().Sequence.Phases[0].Theme = previewTheme;
                game.GetComponent<StageManager>().RestartStage();
                camera.orthographicSize = 4.3f;
                camera.transform.position = new Vector3(-0.5f, 8f, -7f);
                camera.transform.LookAt(new Vector3(-0.5f, 0, -0.5f));
                Capture("GrassFloor_Stage.png");
                Debug.Log("GRASS_VALIDATION_PASS: saved mesh, dimensions, seeded rebuild, density, heights, bounds, zero-wind frames, visible sway, world-phase variation, shader, collision, explosion removal, thin floor, full-height rooted grass, walkable floor.");
                Finish(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        Time.timeScale = 1;
        EditorApplication.Exit(code);
    }
}
