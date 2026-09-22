using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleScreenSmoke
{
    private const string Running = "Bomberman.TitleScreenSmoke";
    private const string SavedStageKey = "Bomberman.TitleScreenSmoke.SavedStage";
    private static int phase;
    private static double deadline, watchdog;
    private static bool failed;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        TitleSceneBuilder.Rebuild();
        Check(File.Exists(TitleSceneBuilder.ScenePath), "Builder creates the title scene");
        string[] buildScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        Check(buildScenes.Length == 2 && buildScenes[0] == TitleSceneBuilder.ScenePath && buildScenes[1] == TitleSceneBuilder.GameScenePath, "Both scenes are in Build Settings");

        Scene scene = EditorSceneManager.OpenScene(TitleSceneBuilder.ScenePath, OpenSceneMode.Single);
        Check(UnityEngine.Object.FindFirstObjectByType<Canvas>() != null, "Title scene has a canvas");
        EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        Check(eventSystem != null && eventSystem.GetComponent<InputSystemUIInputModule>() != null, "Event System uses the Input System module");
        TitleMenu menu = UnityEngine.Object.FindFirstObjectByType<TitleMenu>();
        Check(menu != null, "Title scene has a Title Menu");
        CheckButton(menu, "Start Button", nameof(TitleMenu.StartNewGame));
        CheckButton(menu, "Continue Button", nameof(TitleMenu.ContinueGame));
        CheckButton(menu, "Exit Button", nameof(TitleMenu.QuitGame));

        VerticalLayoutGroup layout = UnityEngine.Object.FindFirstObjectByType<VerticalLayoutGroup>();
        Check(layout != null && layout.childControlWidth && layout.childControlHeight, "Button column controls child size");
        LayoutElement buttonSize = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .First(b => b.name == "Start Button").GetComponent<LayoutElement>();
        Check(buttonSize.preferredWidth >= 400f && buttonSize.preferredHeight >= 80f, "Buttons are wide enough for their labels");

        // The isolated copy shares PlayerPrefs with the real project, so the saved stage goes back afterwards.
        SessionState.SetInt(SavedStageKey, GameProgress.HasSave ? GameProgress.SavedStage : 0);
        GameProgress.Clear();
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void CheckButton(TitleMenu menu, string buttonName, string methodName)
    {
        Button button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == buttonName);
        Check(button != null && button.onClick.GetPersistentEventCount() == 1
            && ReferenceEquals(button.onClick.GetPersistentTarget(0), menu)
            && button.onClick.GetPersistentMethodName(0) == methodName, $"{buttonName} calls {methodName}");
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
        if (!condition) throw new Exception("Title screen smoke: " + message);
    }

    private static TitleMenu Menu => UnityEngine.Object.FindFirstObjectByType<TitleMenu>();
    private static StageManager Stage => UnityEngine.Object.FindFirstObjectByType<StageManager>();
    private static Button ContinueButton => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "Continue Button");

    // Renders the title through the camera so the overlay UI shows up in the image.
    private static void Capture(string path)
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        Camera camera = Camera.main;
        if (canvas == null || camera == null) return;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();

        RenderTexture target = new(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
        image.Apply();
        camera.targetTexture = null;
        RenderTexture.active = previous;
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime error.");
            if (EditorApplication.timeSinceStartup > watchdog) throw new Exception("Title screen smoke timed out.");
            if (phase == 0)
            {
                TitleMenu menu = Menu;
                if (menu == null) return;
                Check(ContinueButton != null && !ContinueButton.interactable, "Continue is disabled without a save");
                Capture("TitleScreen.png");
                menu.StartNewGame();
                phase = 1;
                deadline = EditorApplication.timeSinceStartup + 30;
            }
            else if (phase == 1)
            {
                BombermanPrototype game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
                if (game == null || !game.IsReady)
                {
                    Check(EditorApplication.timeSinceStartup < deadline, "Start loads the game scene");
                    return;
                }

                Check(Stage.CurrentStage == 1, "Start begins at stage 1");
                Stage.LoadStage(3);
                Check(GameProgress.HasSave && GameProgress.SavedStage == 3, "Reaching a stage saves it");
                SceneManager.LoadScene(System.IO.Path.GetFileNameWithoutExtension(TitleSceneBuilder.ScenePath));
                phase = 2;
                deadline = EditorApplication.timeSinceStartup + 30;
            }
            else if (phase == 2)
            {
                TitleMenu menu = Menu;
                if (menu == null || ContinueButton == null || !ContinueButton.interactable)
                {
                    Check(EditorApplication.timeSinceStartup < deadline, "Continue turns on after a save");
                    return;
                }

                Check(ContinueButton.GetComponentInChildren<Text>().text == "Continue (Stage 3)", "Continue shows the saved stage");
                menu.ContinueGame();
                phase = 3;
                deadline = EditorApplication.timeSinceStartup + 30;
            }
            else if (phase == 3)
            {
                BombermanPrototype game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
                if (game == null || !game.IsReady)
                {
                    Check(EditorApplication.timeSinceStartup < deadline, "Continue loads the game scene");
                    return;
                }

                Check(Stage.CurrentStage == 3, "Continue resumes the saved stage");
                // The last life is spent, so the game must end and return to the title.
                typeof(BombermanPrototype).GetField("livesLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, 1);
                typeof(BombermanPrototype).GetMethod("StartPlayerDeath", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(game, new[] { Enum.Parse(typeof(BombermanPrototype).GetNestedType("PlayerDeathCause", BindingFlags.NonPublic), "Bomb") });
                phase = 4;
                deadline = EditorApplication.timeSinceStartup + 40;
            }
            else if (phase == 4)
            {
                TitleMenu menu = Menu;
                if (menu == null)
                {
                    Check(EditorApplication.timeSinceStartup < deadline, "Game over returns to the title");
                    return;
                }

                Check(!GameProgress.HasSave && ContinueButton != null && !ContinueButton.interactable, "Game over clears the save and disables Continue");
                Debug.Log("TITLE_SCREEN_SMOKE_PASS: scene built, build settings, canvas, input module, button wiring, continue disabled, start at stage 1, progress saved, continue label, resume saved stage, game over to title.");
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
        int savedStage = SessionState.GetInt(SavedStageKey, 0);
        if (savedStage >= 1) GameProgress.Save(savedStage);
        else GameProgress.Clear();
        Time.timeScale = 1f;
        EditorApplication.Exit(code);
    }
}
