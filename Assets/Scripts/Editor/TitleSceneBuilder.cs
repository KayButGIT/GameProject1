using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Creates the title scene once. The team then edits it like any other scene:
// buttons call TitleMenu methods through their On Click lists, so no code changes are needed.
public static class TitleSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Title.unity";
    public const string GameScenePath = "Assets/Scenes/Prototype.unity";
    private const string TitleModelPath = "Assets/Prefabs/BombermanTitlescreen.prefab";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    // Run from the smoke tests and from -executeMethod; no menu entry on purpose.
    public static void Build()
    {
        if (File.Exists(ScenePath))
        {
            AddScenesToBuildSettings();
            Debug.Log($"{ScenePath} already exists, so it was left untouched. Use Rebuild Title Scene to replace it.");
            return;
        }

        Rebuild();
    }

    // Replaces the scene with a fresh layout. The team's own edits are lost, so it asks first.
    // Run from the smoke tests and from -executeMethod; no menu entry on purpose.
    public static void Rebuild()
    {
        if (!Application.isBatchMode && File.Exists(ScenePath)
            && !EditorUtility.DisplayDialog("Rebuild title scene", $"Replace {ScenePath} with a fresh layout? Edits made to that scene are lost.", "Rebuild", "Cancel"))
        {
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        CreateSun();
        CreateTitleModel();
        Canvas canvas = CreateCanvas();
        CreateEventSystem();
        CreateTitleText(canvas.transform);
        CreateHintText(canvas.transform);

        RectTransform buttons = CreateButtonColumn(canvas.transform);
        Button startButton = CreateButton(buttons, "Start Button", "Start");
        Button continueButton = CreateButton(buttons, "Continue Button", "Continue");
        Button exitButton = CreateButton(buttons, "Exit Button", "Exit");

        TitleMenu menu = new GameObject("Title Menu").AddComponent<TitleMenu>();
        SerializedObject settings = new(menu);
        settings.FindProperty("gameSceneName").stringValue = Path.GetFileNameWithoutExtension(GameScenePath);
        settings.FindProperty("continueButton").objectReferenceValue = continueButton;
        settings.FindProperty("continueLabel").objectReferenceValue = continueButton.GetComponentInChildren<Text>();
        settings.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(startButton.onClick, menu.StartNewGame);
        UnityEventTools.AddPersistentListener(continueButton.onClick, menu.ContinueGame);
        UnityEventTools.AddPersistentListener(exitButton.onClick, menu.QuitGame);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddScenesToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log($"TITLE_SCENE_CREATED: {ScenePath}");
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new("Main Camera") { tag = "MainCamera" };
        cameraObject.transform.SetPositionAndRotation(new Vector3(0.5f, 1.35f, -4.2f), Quaternion.Euler(7f, -6f, 0f));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
        camera.fieldOfView = 45f;
        cameraObject.AddComponent<AudioListener>();
    }

    private static void CreateSun()
    {
        Light sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.94f, 0.82f);
        sun.intensity = 1.3f;
        sun.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
    }

    // The model is authored about 0.095 units tall, so this stands it around 2.4 units high.
    // The team can move or rescale the holder in the scene.
    private const float TitleModelScale = 25f;

    private static void CreateTitleModel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TitleModelPath);
        if (prefab == null) return;
        // The prefab instance keeps its own transform, so a plain holder carries the placement.
        GameObject holder = new("Title Model");
        holder.transform.SetPositionAndRotation(new Vector3(1.7f, 0.25f, 0f), Quaternion.Euler(0f, 200f, 0f));
        holder.transform.localScale = Vector3.one * TitleModelScale;
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.transform.SetParent(holder.transform, false);
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new("Title Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new("Event System");
        eventSystem.AddComponent<EventSystem>();
        // The project uses the new Input System, so the old StandaloneInputModule would not respond.
        InputSystemUIInputModule module = eventSystem.AddComponent<InputSystemUIInputModule>();
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (actions != null) module.actionsAsset = actions;
    }

    private static void CreateTitleText(Transform parent)
    {
        Text text = CreateText(parent, "Title Text", "BOMBERMAN", 110, TextAnchor.MiddleCenter, Color.white);
        text.fontStyle = FontStyle.Bold;
        text.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4f, -4f);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -240f);
        rect.offsetMax = new Vector2(0f, -70f);
    }

    private static void CreateHintText(Transform parent)
    {
        Text text = CreateText(parent, "Hint Text", "Move: WASD or Arrows     Bomb: Space     Pause: Esc", 28, TextAnchor.MiddleCenter, new Color(0.72f, 0.74f, 0.8f));
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(0f, 40f);
        rect.offsetMax = new Vector2(0f, 100f);
    }

    // Menu sits on the left half, so the title model stays visible on the right.
    private static RectTransform CreateButtonColumn(Transform parent)
    {
        GameObject column = new("Buttons");
        column.transform.SetParent(parent, false);
        RectTransform rect = column.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(260f, -40f);
        VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        // Without these the buttons keep their default size and the labels wrap.
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = column.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rect;
    }

    private static Button CreateButton(RectTransform parent, string objectName, string label)
    {
        GameObject buttonObject = new(objectName);
        buttonObject.transform.SetParent(parent, false);
        Image background = buttonObject.AddComponent<Image>();
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        background.color = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.5f, 1f);
        colors.pressedColor = new Color(0.8f, 0.85f, 1.1f, 1f);
        colors.selectedColor = new Color(1.2f, 1.2f, 1.35f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.5f);
        button.colors = colors;
        LayoutElement size = buttonObject.AddComponent<LayoutElement>();
        size.preferredWidth = 440f;
        size.preferredHeight = 88f;

        Text text = CreateText(buttonObject.transform, "Text", label, 40, TextAnchor.MiddleCenter, Color.white);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private static Text CreateText(Transform parent, string objectName, string content, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new(objectName);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void AddScenesToBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
    }
}
