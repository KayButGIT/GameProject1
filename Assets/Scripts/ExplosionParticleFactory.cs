using UnityEngine;

public static class ExplosionParticleFactory
{
    private static Material orangeMaterial;
    private static Material yellowMaterial;

    public static GameObject CreateCartoonFlame(string objectName, Vector3 groundPosition)
    {
        GameObject root = new(objectName);
        root.transform.position = groundPosition + new Vector3(0f, 0.12f, 0f);

        CreateChunks(root.transform);
        return root;
    }

    private static void CreateChunks(Transform parent)
    {
        for (int i = 0; i < 34; i++)
        {
            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "Explosion Particle";
            chunk.transform.SetParent(parent);
            chunk.transform.localPosition = new Vector3(
                Random.Range(-0.42f, 0.42f),
                Random.Range(0.02f, 0.3f),
                Random.Range(-0.42f, 0.42f));
            chunk.transform.localRotation = Random.rotation;

            float size = Random.Range(0.12f, 0.28f);
            chunk.transform.localScale = Vector3.one * size;
            chunk.GetComponent<Renderer>().material = Random.value > 0.35f ? GetOrangeMaterial() : GetYellowMaterial();
            Object.Destroy(chunk.GetComponent<Collider>());

            Vector3 velocity = new(
                Random.Range(-1.25f, 1.25f),
                Random.Range(0.6f, 1.9f),
                Random.Range(-1.25f, 1.25f));
            Vector3 spin = new(
                Random.Range(-360f, 360f),
                Random.Range(-360f, 360f),
                Random.Range(-360f, 360f));
            float lifetime = Random.Range(0.38f, 0.72f);
            chunk.AddComponent<ExplosionParticlePiece>().Initialize(velocity, spin, lifetime);
        }
    }

    private static Material GetOrangeMaterial()
    {
        if (orangeMaterial == null)
        {
            orangeMaterial = BombermanMaterials.Make("Explosion Particle Orange", new Color(1f, 0.42f, 0.03f));
        }

        return orangeMaterial;
    }

    private static Material GetYellowMaterial()
    {
        if (yellowMaterial == null)
        {
            yellowMaterial = BombermanMaterials.Make("Explosion Particle Yellow", new Color(1f, 0.86f, 0.06f));
        }

        return yellowMaterial;
    }
}

public sealed class ExplosionParticlePiece : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 spin;
    private float lifetime;
    private float age;
    private Vector3 startScale;

    public void Initialize(Vector3 initialVelocity, Vector3 angularVelocity, float maxLifetime)
    {
        velocity = initialVelocity;
        spin = angularVelocity;
        lifetime = maxLifetime;
        startScale = transform.localScale;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        velocity += Physics.gravity * (0.35f * Time.deltaTime);
        transform.localPosition += velocity * Time.deltaTime;
        transform.Rotate(spin * Time.deltaTime, Space.Self);
        transform.localScale = startScale * (1f - age / lifetime);
    }
}
