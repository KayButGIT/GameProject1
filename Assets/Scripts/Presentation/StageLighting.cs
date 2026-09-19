using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Applies a stage theme's sun, ambient light, background, and post-processing at runtime.
public sealed class StageLighting
{
    private static readonly StageTheme.ThemeLighting DefaultLighting = new();

    private readonly Light sun;
    private readonly Volume volume;
    private readonly Color sceneSunColor;
    private readonly float sceneSunIntensity;
    private readonly Quaternion sceneSunRotation;
    private readonly LightShadows sceneSunShadows;
    private readonly float sceneShadowStrength;
    private readonly AmbientMode sceneAmbientMode;
    private readonly Color sceneAmbientSky;
    private readonly Color sceneAmbientEquator;
    private readonly Color sceneAmbientGround;
    private Camera recordedCamera;
    private CameraClearFlags sceneClearFlags;
    private Color sceneBackground;
    private VolumeProfile defaultProfile;
    private bool themeApplied;

    private StageLighting(Light sun, Volume volume)
    {
        this.sun = sun;
        this.volume = volume;
        sceneSunColor = sun.color;
        sceneSunIntensity = sun.intensity;
        sceneSunRotation = sun.transform.rotation;
        sceneSunShadows = sun.shadows;
        sceneShadowStrength = sun.shadowStrength;
        sceneAmbientMode = RenderSettings.ambientMode;
        sceneAmbientSky = RenderSettings.ambientSkyColor;
        sceneAmbientEquator = RenderSettings.ambientEquatorColor;
        sceneAmbientGround = RenderSettings.ambientGroundColor;
    }

    public static StageLighting Create()
    {
        Light sun = null;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                sun = light;
                break;
            }
        }

        if (sun == null)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        GameObject volumeObject = new("Stage Post Processing");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        return new StageLighting(sun, volume);
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

        sun.color = lighting.SunColor;
        sun.intensity = lighting.SunIntensity;
        sun.transform.rotation = Quaternion.Euler(lighting.SunRotation);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = lighting.ShadowStrength;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = lighting.AmbientSky;
        RenderSettings.ambientEquatorColor = lighting.AmbientEquator;
        RenderSettings.ambientGroundColor = lighting.AmbientGround;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = lighting.Background;
        volume.sharedProfile = lighting.PostProcessing != null ? lighting.PostProcessing : DefaultProfile;
        volume.enabled = true;
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

    private void RestoreScene(Camera camera)
    {
        sun.color = sceneSunColor;
        sun.intensity = sceneSunIntensity;
        sun.transform.rotation = sceneSunRotation;
        sun.shadows = sceneSunShadows;
        sun.shadowStrength = sceneShadowStrength;
        RenderSettings.ambientMode = sceneAmbientMode;
        RenderSettings.ambientSkyColor = sceneAmbientSky;
        RenderSettings.ambientEquatorColor = sceneAmbientEquator;
        RenderSettings.ambientGroundColor = sceneAmbientGround;
        camera.clearFlags = sceneClearFlags;
        camera.backgroundColor = sceneBackground;
        volume.enabled = false;
    }
}
