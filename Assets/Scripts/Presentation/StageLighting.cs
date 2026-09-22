using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Applies a stage theme's sun, ambient light, sky or background, and post-processing at runtime.
public sealed class StageLighting
{
    private static readonly StageTheme.ThemeLighting DefaultLighting = new();

    public const string SunName = "Stage Sun";
    public const string VolumeName = "Stage Post Processing";

    private readonly Volume volume;
    private readonly List<Light> sceneLightsTurnedOff = new();
    private GameObject sunRoot;
    private GameObject builtSunPrefab;
    private readonly AmbientMode sceneAmbientMode;
    private readonly Color sceneAmbientSky;
    private readonly Color sceneAmbientEquator;
    private readonly Color sceneAmbientGround;
    private readonly Material sceneSkybox;
    private readonly float sceneAmbientIntensity;
    private Camera recordedCamera;
    private CameraClearFlags sceneClearFlags;
    private Color sceneBackground;
    private VolumeProfile defaultProfile;
    private bool themeApplied;

    private StageLighting(Volume volume)
    {
        this.volume = volume;
        sceneAmbientMode = RenderSettings.ambientMode;
        sceneAmbientSky = RenderSettings.ambientSkyColor;
        sceneAmbientEquator = RenderSettings.ambientEquatorColor;
        sceneAmbientGround = RenderSettings.ambientGroundColor;
        sceneSkybox = RenderSettings.skybox;
        sceneAmbientIntensity = RenderSettings.ambientIntensity;
    }

    public static StageLighting Create()
    {
        // A domain reload wipes whoever owned the last pair while the objects themselves survive,
        // so old ones are swept first: two global volumes would apply the profile twice.
        SweepOwnObjects();
        GameObject volumeObject = new(VolumeName);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        return new StageLighting(volume);
    }

    public static StageTheme.ThemeLighting Resolve(StageTheme.ThemeLighting lighting)
    {
        return lighting ?? DefaultLighting;
    }

    public void Apply(StageTheme.ThemeLighting lighting, Camera camera)
    {
        lighting = Resolve(lighting);
        if (recordedCamera != camera)
        {
            recordedCamera = camera;
            sceneClearFlags = camera.clearFlags;
            sceneBackground = camera.backgroundColor;
        }

        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        if (!lighting.Enabled)
        {
            // Restore once, so scene lighting stays editable while theme lighting is off.
            if (themeApplied)
            {
                RestoreScene(camera);
                themeApplied = false;
            }

            volume.enabled = false;
            return;
        }

        themeApplied = true;

        BuildSun(lighting);
        RenderSettings.skybox = lighting.Skybox != null ? lighting.Skybox : sceneSkybox;
        if (lighting.Skybox != null && lighting.AmbientFromSkybox)
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = lighting.SkyboxAmbientIntensity;
            // The ambient probe is built from the sky image, so it has to be rebuilt when the sky changes.
            DynamicGI.UpdateEnvironment();
        }
        else
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = lighting.AmbientSky;
            RenderSettings.ambientEquatorColor = lighting.AmbientEquator;
            RenderSettings.ambientGroundColor = lighting.AmbientGround;
        }

        camera.clearFlags = lighting.Skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        camera.backgroundColor = lighting.Background;
        volume.sharedProfile = lighting.PostProcessing != null ? lighting.PostProcessing : DefaultProfile;
        volume.enabled = true;
    }

    // Hands the scene back its own lighting, for the editor preview and for turning theme lighting off.
    public void Restore(Camera camera)
    {
        if (!themeApplied) return;
        RestoreScene(camera);
        themeApplied = false;
    }

    public static void ConfigureDefaultProfile(VolumeProfile profile)
    {
        profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);

        ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>();
        colorAdjustments.postExposure.Override(0.15f);
        colorAdjustments.contrast.Override(15f);
        colorAdjustments.saturation.Override(12f);

        profile.Add<WhiteBalance>().temperature.Override(6f);

        // Slightly cool shadows and warm highlights read as sunlight.
        ShadowsMidtonesHighlights tones = profile.Add<ShadowsMidtonesHighlights>();
        tones.shadows.Override(new Vector4(0.95f, 0.98f, 1.08f, 0f));
        tones.highlights.Override(new Vector4(1.05f, 1f, 0.95f, 0f));

        Bloom bloom = profile.Add<Bloom>();
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.65f);

        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.45f);
    }

    private VolumeProfile DefaultProfile
    {
        get
        {
            if (defaultProfile == null)
            {
                defaultProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                defaultProfile.name = "Day Post Processing";
                ConfigureDefaultProfile(defaultProfile);
            }

            return defaultProfile;
        }
    }

    // The theme lights the stage with a light of its own, so the scene's sun steps aside instead of being edited.
    private void BuildSun(StageTheme.ThemeLighting lighting)
    {
        TurnOffSceneSuns();
        if (sunRoot != null && builtSunPrefab == lighting.SunPrefab && lighting.SunPrefab != null) return;
        if (sunRoot != null) DestroySun();

        sunRoot = new GameObject(SunName);
        builtSunPrefab = lighting.SunPrefab;
        Light sun = null;
        if (lighting.SunPrefab != null)
        {
            GameObject instance = Object.Instantiate(lighting.SunPrefab, sunRoot.transform);
            instance.name = "Light";
            sun = instance.GetComponentInChildren<Light>();
            if (sun == null) Debug.LogWarning($"{lighting.SunPrefab.name} has no Light, so the theme's Sun settings are used instead.", lighting.SunPrefab);
        }

        if (sun != null) return;

        // No prefab: one directional light driven by the theme's own sun settings.
        sun = sunRoot.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = lighting.SunColor;
        sun.intensity = lighting.SunIntensity;
        sunRoot.transform.rotation = Quaternion.Euler(lighting.SunRotation);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = lighting.ShadowStrength;
    }

    public static void SweepOwnObjects()
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate == null || candidate.transform.parent != null) continue;
            if (candidate.name != VolumeName && candidate.name != SunName) continue;
            if (candidate.scene.IsValid() == false) continue;
            DestroyOwned(candidate);
        }
    }

    private static void DestroyOwned(GameObject owned)
    {
        owned.SetActive(false);
        if (Application.isPlaying) Object.Destroy(owned);
        else Object.DestroyImmediate(owned);
    }

    private void TurnOffSceneSuns()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional || !light.enabled) continue;
            if (sunRoot != null && light.transform.IsChildOf(sunRoot.transform)) continue;
            light.enabled = false;
            sceneLightsTurnedOff.Add(light);
        }
    }

    private void DestroySun()
    {
        if (sunRoot == null) return;
        // Deferred destruction keeps the old object alive until the frame ends, so it stops lighting and answering Find right away.
        DestroyOwned(sunRoot);
        sunRoot = null;
        builtSunPrefab = null;
    }

    private void RestoreScene(Camera camera)
    {
        DestroySun();
        foreach (Light light in sceneLightsTurnedOff)
            if (light != null) light.enabled = true;
        sceneLightsTurnedOff.Clear();
        RenderSettings.ambientMode = sceneAmbientMode;
        RenderSettings.ambientSkyColor = sceneAmbientSky;
        RenderSettings.ambientEquatorColor = sceneAmbientEquator;
        RenderSettings.ambientGroundColor = sceneAmbientGround;
        RenderSettings.skybox = sceneSkybox;
        RenderSettings.ambientIntensity = sceneAmbientIntensity;
        // The editor preview can lose its camera before the scene lighting is handed back.
        if (camera != null)
        {
            camera.clearFlags = sceneClearFlags;
            camera.backgroundColor = sceneBackground;
        }

        volume.enabled = false;
    }
}
