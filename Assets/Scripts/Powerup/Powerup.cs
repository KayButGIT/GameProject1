using UnityEngine;

// A collectible revealed when the breakable block hiding it is destroyed.
// BombermanPrototype checks the pickup distance and applies the effect, so this component only draws the item.
// Give BombermanPrototype your own models in Power Up Prefabs; without one, a colored marker with a letter is used.
public sealed class PowerUp : MonoBehaviour
{
    private const float RestHeight = 0.45f;
    private const float BobHeight = 0.08f;
    private const float BobSpeed = 3f;
    private const float SpinDegreesPerSecond = 90f;

    // Your own models (Power Up Prefabs) lie on the floor. Switch these on if you want them to hover or spin instead.
    private const float ModelHeight = 0.02f;
    private static readonly bool ModelBobs = false;
    private static readonly bool ModelSpins = false;

    private static readonly Material[] MarkerMaterials = new Material[8];

    public PowerUpType Type;
    public Vector2Int Cell;

    private Transform visual;
    private Transform label;
    private Camera labelCamera;
    private float bobPhase;
    private bool usesModel;

    public static PowerUp Create(PowerUpType type, Vector2Int cell, Vector3 groundPosition, Transform parent, GameObject prefab)
    {
        GameObject root = new($"Power Up {type}");
        root.transform.SetParent(parent, true);
        root.transform.position = groundPosition;

        PowerUp item = root.AddComponent<PowerUp>();
        item.Type = type;
        item.Cell = cell;
        item.bobPhase = Random.value * Mathf.PI * 2f;
        item.BuildVisual(prefab);
        return item;
    }

    private void Update()
    {
        if (visual == null) return;

        // Scaled time, so the item holds still while the game is paused.
        float height = usesModel ? ModelHeight : RestHeight;
        if (!usesModel || ModelBobs)
        {
            bobPhase += Time.deltaTime * BobSpeed;
            height += Mathf.Sin(bobPhase) * BobHeight;
        }

        visual.localPosition = new Vector3(0f, height, 0f);
        if (!usesModel || ModelSpins)
        {
            visual.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        if (label == null) return;
        if (labelCamera == null) labelCamera = Camera.main;
        if (labelCamera != null) label.rotation = labelCamera.transform.rotation;
    }

    private void BuildVisual(GameObject prefab)
    {
        usesModel = prefab != null;
        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        visual.localPosition = new Vector3(0f, usesModel ? ModelHeight : RestHeight, 0f);

        if (prefab != null)
        {
            GameObject model = Instantiate(prefab, visual);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            foreach (Collider modelCollider in model.GetComponentsInChildren<Collider>(true)) modelCollider.enabled = false;
            return;
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Marker";
        marker.transform.SetParent(visual, false);
        marker.transform.localScale = Vector3.one * 0.42f;
        marker.GetComponent<Renderer>().sharedMaterial = GetMarkerMaterial(Type);
        Destroy(marker.GetComponent<Collider>());

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject labelObject = new("Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, RestHeight + 0.5f, 0f);
        TextMesh text = labelObject.AddComponent<TextMesh>();
        text.text = LabelFor(Type);
        text.font = font;
        text.fontSize = 64;
        text.characterSize = 0.07f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
        labelRenderer.sharedMaterial = font.material;
        labelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        labelRenderer.receiveShadows = false;
        label = labelObject.transform;
    }

    private static Material GetMarkerMaterial(PowerUpType type)
    {
        int index = (int)type;
        if (MarkerMaterials[index] == null)
        {
            MarkerMaterials[index] = BombermanMaterials.Make($"Power Up {type}", ColorFor(type));
        }

        return MarkerMaterials[index];
    }

    private static Color ColorFor(PowerUpType type)
    {
        return type switch
        {
            PowerUpType.BombUp => new Color(0.15f, 0.2f, 0.9f),
            PowerUpType.FireUp => new Color(0.95f, 0.35f, 0.05f),
            PowerUpType.SpeedUp => new Color(0.1f, 0.85f, 0.95f),
            PowerUpType.Detonator => new Color(0.95f, 0.85f, 0.1f),
            PowerUpType.BombPass => new Color(0.6f, 0.2f, 0.85f),
            PowerUpType.WallPass => new Color(0.55f, 0.35f, 0.15f),
            PowerUpType.FlamePass => new Color(0.9f, 0.1f, 0.15f),
            _ => new Color(0.95f, 0.4f, 0.8f)
        };
    }

    private static string LabelFor(PowerUpType type)
    {
        return type switch
        {
            PowerUpType.BombUp => "B+",
            PowerUpType.FireUp => "F+",
            PowerUpType.SpeedUp => "S+",
            PowerUpType.Detonator => "DET",
            PowerUpType.BombPass => "BP",
            PowerUpType.WallPass => "WP",
            PowerUpType.FlamePass => "FP",
            _ => "?"
        };
    }
}