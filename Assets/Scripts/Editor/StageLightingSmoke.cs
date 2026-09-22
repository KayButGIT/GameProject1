using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class StageLightingSmoke
{
    private const string Running = "Bomberman.StageLightingSmoke";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Color SceneSunColor = new(0.5f, 0.6f, 0.7f);
    private static readonly Quaternion SceneSunRotation = Quaternion.Euler(30f, 10f, 0f);
    private static readonly Color SceneAmbient = new(0.3f, 0.25f, 0.2f);
    private const string SceneSkyPath = "Assets/StageLightingSceneSky.mat";
    private const string ThemeSkyPath = "Assets/StageLightingThemeSky.mat";
    private const float SkyAmbientIntensity = 0.6f;
    private const string SunPrefabPath = "Assets/StageLightingSun.prefab";
    private static readonly Color PrefabSunColor = new(0.9f, 0.2f, 0.4f);
    private static BombermanPrototype game;
    private static StageManager manager;
    private static int phase;
    private static double deadline, watchdog;
    private static bool failed;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        // A new single scene unloads unreferenced assets, so create the scene before the test assets.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Light sceneSun = new GameObject("Scene Sun").AddComponent<Light>();
        sceneSun.type = LightType.Directional;
        sceneSun.color = SceneSunColor;
        sceneSun.intensity = 0.7f;
        sceneSun.transform.rotation = SceneSunRotation;
        sceneSun.shadows = LightShadows.Hard;
        sceneSun.shadowStrength = 0.5f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = SceneAmbient;
        RenderSettings.ambientIntensity = 0.3f;
        RenderSettings.skybox = CreateSky(SceneSkyPath);
        CreateSky(ThemeSkyPath);

        CreateSunPrefab();
        // Leftovers of an earlier session, the way a domain reload leaves them behind.
        new GameObject(StageLighting.VolumeName).AddComponent<Volume>().isGlobal = true;
        new GameObject(StageLighting.SunName).AddComponent<Light>().type = LightType.Directional;

        StageTheme theme = ScriptableObject.CreateInstance<StageTheme>();
        AssetDatabase.CreateAsset(theme, "Assets/StageLightingTheme.asset");
        StageSequence sequence = ScriptableObject.CreateInstance<StageSequence>();
        sequence.Phases.Add(new StageSequence.Phase { Theme = theme, StageCount = 1 });
        AssetDatabase.CreateAsset(sequence, "Assets/StageLightingSequence.asset");
        AssetDatabase.SaveAssets();

        game = new GameObject("Stage Lighting Smoke Game").AddComponent<BombermanPrototype>();
        SerializedObject settings = new(game);
        settings.FindProperty("width").intValue = 9;
        settings.FindProperty("height").intValue = 7;
        settings.FindProperty("randomSeed").intValue = 123;
        settings.FindProperty("spawnEnemies").boolValue = false;
        settings.FindProperty("playerCanDieFromBomb").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        manager = game.gameObject.AddComponent<StageManager>();
        settings = new SerializedObject(manager);
        settings.FindProperty("sequence").objectReferenceValue = sequence;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Check(manager.Sequence == sequence, "Test theme assigned");
        EditorSceneManager.SaveScene(game.gameObject.scene, "Assets/StageLightingSmoke.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    // A light the team could have built themselves: the theme must use it untouched.
    private static void CreateSunPrefab()
    {
        GameObject source = new("Team Sun");
        Light light = source.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = PrefabSunColor;
        light.intensity = 2.5f;
        light.shadows = LightShadows.Hard;
        PrefabUtility.SaveAsPrefabAsset(source, SunPrefabPath);
        UnityEngine.Object.DestroyImmediate(source);
    }

    private static GameObject SunPrefab => AssetDatabase.LoadAssetAtPath<GameObject>(SunPrefabPath);
    private static Light ThemeSun => GameObject.Find(StageLighting.SunName)?.GetComponentInChildren<Light>();

    private static Material CreateSky(string path)
    {
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null) shader = Shader.Find("Skybox/6 Sided");
        if (shader == null) throw new Exception("Stage lighting smoke: no skybox shader");
        Material sky = new(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        AssetDatabase.CreateAsset(sky, path);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static Material SceneSky => AssetDatabase.LoadAssetAtPath<Material>(SceneSkyPath);
    private static Material ThemeSky => AssetDatabase.LoadAssetAtPath<Material>(ThemeSkyPath);

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
        if (!condition) throw new Exception("Stage lighting smoke: " + message);
    }

    private static bool Close(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.001f && Mathf.Abs(a.g - b.g) < 0.001f && Mathf.Abs(a.b - b.b) < 0.001f;
    private static Light Sun => UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Single(light => light.name == "Scene Sun");
    private static Volume PostVolume => GameObject.Find("Stage Post Processing").GetComponent<Volume>();
    private static Light BlastLight => GameObject.Find("Blast Light")?.GetComponent<Light>();

    private static int Named(string name)
    {
        // Objects waiting for deferred destruction are deactivated first, so only live ones count.
        return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Count(t => t.name == name && t.parent == null);
    }

    private static void CheckThemeLighting(StageTheme.ThemeLighting lighting, string message)
    {
        Check(Named(StageLighting.VolumeName) == 1 && Named(StageLighting.SunName) == 1, message + ": one volume and one sun");
        Check(!Sun.enabled, message + ": the scene's own sun steps aside");
        Light sun = ThemeSun;
        Check(sun != null && sun.type == LightType.Directional, message + ": the theme brings its own sun");
        if (lighting.SunPrefab != null)
            Check(Close(sun.color, PrefabSunColor) && Mathf.Abs(sun.intensity - 2.5f) < 0.001f && sun.shadows == LightShadows.Hard,
                message + ": the sun prefab keeps its own values");
        else
            Check(Close(sun.color, lighting.SunColor) && Mathf.Abs(sun.intensity - lighting.SunIntensity) < 0.001f
                && Quaternion.Angle(sun.transform.rotation, Quaternion.Euler(lighting.SunRotation)) < 0.1f
                && sun.shadows == LightShadows.Soft && Mathf.Abs(sun.shadowStrength - lighting.ShadowStrength) < 0.001f, message + ": sun");
        bool skyAmbient = lighting.Skybox != null && lighting.AmbientFromSkybox;
        if (skyAmbient)
            Check(RenderSettings.ambientMode == AmbientMode.Skybox
                && Mathf.Abs(RenderSettings.ambientIntensity - lighting.SkyboxAmbientIntensity) < 0.001f, message + ": ambient from the sky");
        else
            Check(RenderSettings.ambientMode == AmbientMode.Trilight && Close(RenderSettings.ambientSkyColor, lighting.AmbientSky)
                && Close(RenderSettings.ambientEquatorColor, lighting.AmbientEquator) && Close(RenderSettings.ambientGroundColor, lighting.AmbientGround), message + ": ambient");
        Check(RenderSettings.skybox == (lighting.Skybox != null ? lighting.Skybox : SceneSky), message + ": sky");
        Camera camera = Camera.main;
        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        Check(camera.clearFlags == (lighting.Skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor)
            && Close(camera.backgroundColor, lighting.Background)
            && cameraData.renderPostProcessing && cameraData.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing, message + ": camera");
        Volume volume = PostVolume;
        Check(volume.enabled && volume.isGlobal && volume.sharedProfile != null && volume.sharedProfile.Has<Tonemapping>()
            && volume.sharedProfile.Has<ColorAdjustments>() && volume.sharedProfile.Has<Bloom>() && volume.sharedProfile.Has<Vignette>(), message + ": post-processing");
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime error.");
            if (EditorApplication.timeSinceStartup > watchdog) throw new Exception("Stage lighting smoke timed out.");
            if (phase == 0)
            {
                game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
                if (game == null || !game.IsReady) return;
                manager = game.GetComponent<StageManager>();
                StageTheme.ThemeLighting lighting = manager.CurrentTheme.Lighting;
                CheckThemeLighting(lighting, "Theme lighting applies on load");
                typeof(BombermanPrototype).GetMethod("Explode", Private).Invoke(game, new object[] { new Vector2Int(1, 1) });
                Light flash = BlastLight;
                Check(flash != null && flash.type == LightType.Point && Close(flash.color, lighting.ExplosionColor)
                    && Mathf.Abs(flash.intensity - lighting.ExplosionIntensity) < 0.001f, "Explosion creates a flash light");
                phase = 1;
                deadline = Time.timeAsDouble + 0.2;
            }
            else if (phase == 1 && Time.timeAsDouble >= deadline)
            {
                Light flash = BlastLight;
                Check(flash != null && flash.intensity > 0f && flash.intensity < manager.CurrentTheme.Lighting.ExplosionIntensity, "Explosion flash fades");
                phase = 2;
                deadline = Time.timeAsDouble + 0.5;
            }
            else if (phase == 2 && Time.timeAsDouble >= deadline)
            {
                Check(BlastLight == null, "Explosion flash removes itself");

                StageTheme.ThemeLighting themeLighting = manager.CurrentTheme.Lighting;
                themeLighting.Skybox = ThemeSky;
                themeLighting.SkyboxAmbientIntensity = SkyAmbientIntensity;
                manager.RestartStage();
                CheckThemeLighting(themeLighting, "A theme skybox replaces the background");

                themeLighting.AmbientFromSkybox = false;
                manager.RestartStage();
                CheckThemeLighting(themeLighting, "Ambient From Skybox off keeps the sky but uses the ambient colors");

                themeLighting.Skybox = null;
                themeLighting.AmbientFromSkybox = true;
                manager.RestartStage();
                CheckThemeLighting(themeLighting, "Without a theme skybox the scene sky and flat background come back");

                themeLighting.SunPrefab = SunPrefab;
                manager.RestartStage();
                CheckThemeLighting(themeLighting, "A sun prefab lights the stage");
                themeLighting.SunPrefab = null;
                manager.RestartStage();
                CheckThemeLighting(themeLighting, "Clearing the sun prefab goes back to the theme's sun settings");

                manager.CurrentTheme.Lighting.Enabled = false;
                manager.RestartStage();
                Light sun = Sun;
                Check(sun.enabled && Close(sun.color, SceneSunColor) && Mathf.Abs(sun.intensity - 0.7f) < 0.001f
                    && Quaternion.Angle(sun.transform.rotation, SceneSunRotation) < 0.1f
                    && sun.shadows == LightShadows.Hard && Mathf.Abs(sun.shadowStrength - 0.5f) < 0.001f, "Disabled theme lighting gives the scene sun back untouched");
                Check(ThemeSun == null, "Disabled theme lighting removes the theme's sun");
                Check(RenderSettings.ambientMode == AmbientMode.Flat && Close(RenderSettings.ambientLight, SceneAmbient), "Disabled theme lighting restores scene ambient");
                Check(RenderSettings.skybox == SceneSky && Mathf.Abs(RenderSettings.ambientIntensity - 0.3f) < 0.001f, "Disabled theme lighting restores the scene sky");
                Check(!PostVolume.enabled, "Disabled theme lighting turns post-processing volume off");
                Check(UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Length == 1, "Single post-processing volume");
                Check(Named(StageLighting.VolumeName) == 1 && Named(StageLighting.SunName) == 0, "Disabled theme lighting keeps one idle volume and no sun");

                SerializedObject settings = new(manager);
                settings.FindProperty("sequence").objectReferenceValue = null;
                settings.ApplyModifiedPropertiesWithoutUndo();
                manager.LoadStage(1);
                CheckThemeLighting(new StageTheme.ThemeLighting(), "Stages without a theme use default lighting");
                Debug.Log("STAGE_LIGHTING_SMOKE_PASS: own theme sun, swept leftovers, scene sun off, sun prefab, trilight ambient, dark background, post-processing volume, camera post and AA, explosion flash fade and removal, scene restore, theme skybox, sky ambient, sky ambient off, skybox cleared, default lighting.");
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
        Time.timeScale = 1f;
        EditorApplication.Exit(code);
    }
}
