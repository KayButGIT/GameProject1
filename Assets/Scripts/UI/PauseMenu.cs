using UnityEngine;

public sealed class PauseMenu
{
    private readonly GameObject panel;

    private PauseMenu(GameObject panel)
    {
        this.panel = panel;
    }

    public static PauseMenu Create()
    {
        GameObject canvasObject = new("Pause Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject panel = new("Pause Panel");
        panel.transform.SetParent(canvasObject.transform);

        UnityEngine.UI.Image image = panel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform panelTransform = panel.GetComponent<RectTransform>();
        panelTransform.anchorMin = Vector2.zero;
        panelTransform.anchorMax = Vector2.one;
        panelTransform.offsetMin = Vector2.zero;
        panelTransform.offsetMax = Vector2.zero;

        CreateText(panel.transform);
        panel.SetActive(false);
        return new PauseMenu(panel);
    }

    public void SetVisible(bool visible)
    {
        panel.SetActive(visible);
    }

    private static void CreateText(Transform parent)
    {
        GameObject textObject = new("Pause Text");
        textObject.transform.SetParent(parent);

        UnityEngine.UI.Text text = textObject.AddComponent<UnityEngine.UI.Text>();
        text.text = "PAUSED\nEsc to resume";
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 42;
        text.color = Color.white;

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;
    }
}
