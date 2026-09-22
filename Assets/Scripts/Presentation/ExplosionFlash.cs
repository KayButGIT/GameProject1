using UnityEngine;

// A short point light at a bomb blast that fades out and removes itself.
public sealed class ExplosionFlash : MonoBehaviour
{
    private const float Duration = 0.45f;

    private Light flash;
    private float peakIntensity;
    private float elapsed;

    public static ExplosionFlash Create(Vector3 position, Color color, float intensity, float range, Transform parent)
    {
        GameObject flashObject = new("Blast Light");
        flashObject.transform.SetParent(parent, false);
        flashObject.transform.position = position;
        Light light = flashObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        ExplosionFlash explosionFlash = flashObject.AddComponent<ExplosionFlash>();
        explosionFlash.flash = light;
        explosionFlash.peakIntensity = intensity;
        return explosionFlash;
    }

    private void Update()
    {
        // Scaled time, so the flash holds while the game is paused.
        elapsed += Time.deltaTime;
        float remaining = 1f - Mathf.Clamp01(elapsed / Duration);
        flash.intensity = peakIntensity * remaining * remaining;
        if (elapsed >= Duration)
        {
            Destroy(gameObject);
        }
    }
}
