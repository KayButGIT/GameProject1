using UnityEngine;

[ExecuteAlways]
public sealed class EnemyModelMotion : MonoBehaviour
{
    [Min(0f)] public float Amplitude = 0.08f;
    [Min(0.1f)] public float Frequency = 1.5f;

    private Vector3 basePosition;
    private float phase;

    private void OnEnable()
    {
        basePosition = transform.localPosition;
        phase = (GetInstanceID() & 1023) * (Mathf.PI * 2f / 1024f);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying && (transform.root.gameObject.hideFlags & HideFlags.DontSaveInEditor) == 0) return;
        EvaluatePreview(Application.isPlaying ? Time.time : Time.realtimeSinceStartup);
    }

    public void EvaluatePreview(float time)
    {
        if (Amplitude <= 0f) return;
        transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(time * Frequency * Mathf.PI * 2f + phase) * Amplitude);
    }
}
