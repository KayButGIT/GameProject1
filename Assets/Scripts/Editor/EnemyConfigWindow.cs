using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class EnemyConfigWindow : EditorWindow
{
    private readonly List<EnemyController> enemies = new();
    private string[] labels = new string[0];
    private int selected;
    private Editor enemyEditor;
    private Vector2 scrollPosition;
    private GameObject previewRoot;
    private GameObject previewLineup;
    private PreviewRenderUtility livePreview;
    private GameObject livePreviewRoot;
    private bool previewOpen = true;
    private const string PreviewObjectName = "Enemy Config Preview";
    private const string PreviewLineupName = "Enemy Config Preview Lineup";
    private readonly List<GameObject> previewEnemies = new();
    private int previewThemeSignature;
    private bool windowEnabled;
    private bool livePreviewFailed;
    public int EnemyCount => enemies.Count;
    public Editor SelectedEnemyEditor => enemyEditor;

    [MenuItem("Tools/Bomberman/Enemy Config")]
    public static EnemyConfigWindow Open()
    {
        EnemyConfigWindow window = GetWindow<EnemyConfigWindow>("Enemy Config");
        window.minSize = new Vector2(340f, 400f);
        window.RefreshPrefabs();
        window.Show();
        return window;
    }

    private void OnEnable()
    {
        windowEnabled = true;
        EditorApplication.update += MonitorPreviewTheme;
        EditorApplication.update += RepaintLivePreview;
        livePreview = new PreviewRenderUtility();
        RefreshPrefabs();
    }

    private void OnDisable()
    {
        windowEnabled = false;
        EditorApplication.update -= MonitorPreviewTheme;
        EditorApplication.update -= RepaintLivePreview;
        if (enemyEditor != null) DestroyImmediate(enemyEditor);
        enemyEditor = null;
        CleanupLivePreview();
        CleanupPreview();
    }

    private void RefreshPrefabs()
    {
        enemies.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EnemyController enemy = prefab != null ? prefab.GetComponent<EnemyController>() : null;
            if (enemy != null) enemies.Add(enemy);
        }
        enemies.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        labels = enemies.ConvertAll(enemy => enemy.name).ToArray();
        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, enemies.Count - 1));
        BindEditor();
        BuildPreviewLineup();
    }

    private void BindEditor()
    {
        EnemyController target = enemies.Count > 0 ? enemies[selected] : null;
        if (enemyEditor != null && enemyEditor.target == target) return;
        if (enemyEditor != null) DestroyImmediate(enemyEditor);
        enemyEditor = target != null ? Editor.CreateEditor(target) : null;
    }

    private void OnGUI()
    {
        if (enemies.Count == 0)
        {
            EditorGUILayout.HelpBox("No enemy prefabs with EnemyController were found in Assets/Prefabs/Enemies.", MessageType.Warning);
            if (GUILayout.Button("Refresh")) RefreshPrefabs();
            return;
        }

        int next = EditorGUILayout.Popup("Enemy Prefab", selected, labels);
        if (next != selected)
        {
            selected = next;
            BindEditor();
            RebuildLivePreview();
        }
        EditorGUILayout.HelpBox("Changes are saved to the selected enemy prefab and apply to newly spawned copies.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select Prefab")) Selection.activeObject = enemies[selected].gameObject;
            if (GUILayout.Button("Ping")) EditorGUIUtility.PingObject(enemies[selected].gameObject);
            if (GUILayout.Button("Refresh")) RefreshPrefabs();
        }

        EditorGUILayout.Space(4f);
        previewOpen = EditorGUILayout.Foldout(previewOpen, "Live Preview (beside showcase)", true, EditorStyles.foldoutHeader);
        if (previewOpen)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("Idle motion plays in this preview and scene lineup.", MessageType.None);
                Rect previewRect = GUILayoutUtility.GetRect(100f, 180f, GUILayout.ExpandWidth(true));
                DrawLivePreview(previewRect);
                if (GUILayout.Button("Frame Preview in Scene")) FramePreviewInScene();
            }
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        try
        {
        EnemyVisualScale visualScale = enemies[selected].GetComponent<EnemyVisualScale>();
        if (visualScale != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Model Size (world units)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float height = EditorGUILayout.Slider("Height", visualScale.VisualHeight, 0.2f, Mathf.Max(1.25f, visualScale.OriginalHeight));
            float width = EditorGUILayout.Slider("Width", visualScale.VisualWidth, 0.2f, Mathf.Max(1.25f, visualScale.OriginalWidth));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(visualScale, "Adjust Enemy Visual Size");
                if (visualScale.ModelRoot != null) Undo.RecordObject(visualScale.ModelRoot, "Adjust Enemy Visual Size");
                visualScale.VisualHeight = height;
                visualScale.VisualWidth = width;
                visualScale.ApplyScale();
                SyncSelectedPreviewSize(height, width);
                EditorUtility.SetDirty(visualScale);
                if (visualScale.ModelRoot != null)
                {
                    EditorUtility.SetDirty(visualScale.ModelRoot);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visualScale.ModelRoot);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(visualScale);
                AssetDatabase.SaveAssets();
                SceneView.RepaintAll();
                Repaint();
            }
            EditorGUILayout.LabelField($"Original imported size: H {visualScale.OriginalHeight:0.###} × W {visualScale.OriginalWidth:0.###} units", EditorStyles.miniLabel);
            if (GUILayout.Button("Reset to Original Model Size"))
            {
                Undo.RecordObject(visualScale, "Reset Enemy to Original Model Size");
                if (visualScale.ModelRoot != null) Undo.RecordObject(visualScale.ModelRoot, "Reset Enemy to Original Model Size");
                visualScale.ResetToOriginalModelSize();
                SyncSelectedPreviewSize(visualScale.VisualHeight, visualScale.VisualWidth);
                EditorUtility.SetDirty(visualScale);
                if (visualScale.ModelRoot != null)
                {
                    EditorUtility.SetDirty(visualScale.ModelRoot);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visualScale.ModelRoot);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(visualScale);
                AssetDatabase.SaveAssets();
                SceneView.RepaintAll();
                Repaint();
            }
            EditorGUILayout.HelpBox("Height and width are independent. Changes resize this prefab's model and trigger collider.", MessageType.None);
        }

        EditorGUILayout.Space();
        if (enemyEditor != null)
        {
            EditorGUI.BeginChangeCheck();
            enemyEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(enemies[selected]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemies[selected]);
                AssetDatabase.SaveAssets();
                if (selected < previewEnemies.Count && previewEnemies[selected] != null)
                    EditorUtility.SetDirty(previewEnemies[selected]);
                SceneView.RepaintAll();
            }
        }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private void BuildPreviewLineup()
    {
        CleanupPreview();
        if (enemies.Count == 0) return;

        previewLineup = new GameObject(PreviewLineupName);
        previewLineup.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        Vector3 start = GetPreviewStartPosition();
        const float spacing = 1.15f;
        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(enemies[i].gameObject);
            if (instance == null) instance = Instantiate(enemies[i].gameObject);
            instance.name = enemies[i].name + " Preview";
            instance.transform.SetParent(previewLineup.transform, true);
            instance.transform.position = start + Vector3.right * (spacing * i);
            instance.transform.rotation = Quaternion.identity;
            DisablePreviewBehaviours(instance);
            previewEnemies.Add(instance);
        }

        previewRoot = previewEnemies[Mathf.Clamp(selected, 0, previewEnemies.Count - 1)];
        StageTheme stageTheme = GetPreviewTheme();
        float blockX = start.x + spacing * enemies.Count;
        CreateBlockReference(previewLineup.transform, new Vector3(blockX, start.y, start.z), stageTheme, false);
        CreateBlockReference(previewLineup.transform, new Vector3(blockX + 1.15f, start.y, start.z), stageTheme, true);
        previewThemeSignature = GetPreviewThemeSignature(stageTheme);
        RebuildLivePreview();
        SceneView.RepaintAll();
    }

    private void SyncSelectedPreviewSize(float height, float width)
    {
        if (selected < 0 || selected >= previewEnemies.Count) return;
        EnemyVisualScale previewScale = previewEnemies[selected].GetComponent<EnemyVisualScale>();
        if (previewScale == null) return;
        previewScale.VisualHeight = height;
        previewScale.VisualWidth = width;
        previewScale.ApplyScale();
        EditorUtility.SetDirty(previewScale);
        if (selected == previewEnemies.IndexOf(previewRoot))
        {
            ApplySizeToPreview(livePreviewRoot, height, width);
        }
    }

    private void ApplySizeToPreview(GameObject target, float height, float width)
    {
        if (target == null) return;
        EnemyVisualScale scale = target.GetComponent<EnemyVisualScale>();
        if (scale == null) return;
        scale.VisualHeight = height;
        scale.VisualWidth = width;
        scale.ApplyScale();
    }

    private void RebuildLivePreview()
    {
        if (livePreview != null) livePreview.Cleanup();
        livePreview = new PreviewRenderUtility();
        livePreviewRoot = null;
        livePreviewFailed = false;
        if (enemies.Count == 0 || enemies[selected] == null) return;

        livePreviewRoot = Instantiate(enemies[selected].gameObject);
        livePreviewRoot.name = enemies[selected].name + " Live Preview";
        livePreviewRoot.transform.position = Vector3.zero;
        livePreview.AddSingleGO(livePreviewRoot);
        foreach (MonoBehaviour behaviour in livePreviewRoot.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(behaviour is EnemyModelMotion)) behaviour.enabled = false;
        foreach (Collider collider in livePreviewRoot.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        Animator animator = livePreviewRoot.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.enabled = false;
        livePreview.camera.clearFlags = CameraClearFlags.Color;
        livePreview.camera.backgroundColor = new Color(0.28f, 0.28f, 0.28f, 1f);
        livePreview.camera.orthographic = true;
        livePreview.camera.nearClipPlane = 0.01f;
        livePreview.camera.farClipPlane = 100f;
    }

    private void DrawLivePreview(Rect rect)
    {
        if (livePreviewFailed)
        {
            EditorGUI.LabelField(rect, "Live preview render failed. Click Refresh to rebuild it.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        if (livePreview == null || livePreviewRoot == null)
        {
            EditorGUI.LabelField(rect, "Select an enemy prefab to preview.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        float now = (float)EditorApplication.timeSinceStartup;
        foreach (EnemyModelMotion motion in livePreviewRoot.GetComponentsInChildren<EnemyModelMotion>(true))
            motion.EvaluatePreview(now);

        Bounds bounds = GetBounds(livePreviewRoot);
        Camera camera = livePreview.camera;
        camera.aspect = Mathf.Max(0.1f, rect.width / Mathf.Max(1f, rect.height));
        camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.3f;
        Quaternion previewRotation = Quaternion.Euler(8f, 180f, 0f);
        camera.transform.rotation = previewRotation;
        camera.transform.position = bounds.center - previewRotation * Vector3.forward * Mathf.Max(3f, bounds.size.magnitude * 2f);
        if (Event.current.type != EventType.Repaint) return;
        bool previewStarted = false;
        try
        {
            livePreview.BeginPreview(rect, GUIStyle.none);
            previewStarted = true;
            camera.Render();
            Texture image = livePreview.EndPreview();
            previewStarted = false;
            if (image == null)
            {
                livePreviewFailed = true;
                EditorGUI.LabelField(rect, "Live preview returned no image. Click Refresh to rebuild it.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            GUI.DrawTexture(rect, image, ScaleMode.StretchToFill, false);
        }
        catch (System.ArgumentNullException exception)
        {
            livePreviewFailed = true;
            Debug.LogException(exception);
            EditorGUI.LabelField(rect, "Live preview failed to allocate its render image. Click Refresh to rebuild it.", EditorStyles.centeredGreyMiniLabel);
        }
        catch (System.Exception exception)
        {
            livePreviewFailed = true;
            Debug.LogException(exception);
            EditorGUI.LabelField(rect, "Live preview failed. Click Refresh to rebuild it.", EditorStyles.centeredGreyMiniLabel);
        }
        finally
        {
            if (previewStarted && livePreview != null)
            {
                try { livePreview.EndPreview(); }
                catch { }
            }
        }
    }

    private static Bounds GetBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(target.transform.position + Vector3.up * 0.5f, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void RepaintLivePreview()
    {
        if (windowEnabled && previewOpen && livePreviewRoot != null) Repaint();
    }

    private void CleanupLivePreview()
    {
        if (livePreview != null) livePreview.Cleanup();
        livePreview = null;
        livePreviewRoot = null;
    }

    private static void DisablePreviewBehaviours(GameObject instance)
    {
        foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(behaviour is EnemyModelMotion)) behaviour.enabled = false;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        Animator animator = instance.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.enabled = false;
    }

    private void CreateBlockReference(Transform parent, Vector3 position, StageTheme stageTheme, bool breakable)
    {
        float heightScale = breakable ? 0.8f : 1f;
        string blockLabel = breakable ? "Breakable Block" : "Solid Block";
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (block == null) return;
        block.name = blockLabel + " Size Reference";
        block.transform.SetParent(parent, true);
        block.transform.position = position + Vector3.up * (heightScale * 0.45f);
        block.transform.localScale = new Vector3(0.92f, heightScale, 0.92f);
        Renderer baseRenderer = block.GetComponent<Renderer>();
        if (baseRenderer != null) baseRenderer.enabled = false;
        Collider baseCollider = block.GetComponent<Collider>();
        if (baseCollider != null) baseCollider.enabled = false;

        StageTheme.VisualSlot slot = stageTheme == null ? null : breakable ? stageTheme.BreakableBlock : stageTheme.SolidBlock;
        StageTheme.VisualVariant variant = null;
        if (slot != null)
        {
            foreach (StageTheme.VisualVariant candidate in slot.Variants)
            {
                if (candidate == null || candidate.Prefab == null || candidate.Weight <= 0f) continue;
                variant = candidate;
                break;
            }
        }

        if (variant?.Prefab == null) return;
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(variant.Prefab);
        if (visual == null) visual = Instantiate(variant.Prefab);
        visual.name = blockLabel + " Visual";
        visual.transform.SetParent(block.transform, false);
        Vector3 cellScale = block.transform.localScale;
        Vector3 inverseCell = new(1f / cellScale.x, 1f / cellScale.y, 1f / cellScale.z);
        visual.transform.localPosition = Vector3.Scale(variant.Position, inverseCell);
        visual.transform.localRotation = Quaternion.Euler(variant.Rotation) * variant.Prefab.transform.localRotation;
        visual.transform.localScale = Vector3.Scale(
            Vector3.Scale(variant.Prefab.transform.localScale, variant.Scale), inverseCell);
        DisablePreviewBehaviours(visual);
    }

    private static StageTheme GetPreviewTheme()
    {
        StageManager manager = Object.FindFirstObjectByType<StageManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            if (Application.isPlaying && manager.CurrentTheme != null) return manager.CurrentTheme;
            if (manager.Sequence != null && manager.Sequence.TryResolve(manager.StartingStage, out _, out StageTheme configuredTheme))
                return configuredTheme;
        }

        StageSequence fallback = AssetDatabase.LoadAssetAtPath<StageSequence>("Assets/Stages/Main Sequence.asset");
        return fallback != null && fallback.TryResolve(1, out _, out StageTheme firstTheme) ? firstTheme : null;
    }

    private void MonitorPreviewTheme()
    {
        if (!windowEnabled) return;
        int signature = GetPreviewThemeSignature(GetPreviewTheme());
        if (signature == previewThemeSignature || previewEnemies.Count == 0) return;
        BuildPreviewLineup();
        Repaint();
    }

    private static int GetPreviewThemeSignature(StageTheme theme)
    {
        unchecked
        {
            int hash = theme != null ? theme.GetInstanceID() : 0;
            AddSlotSignature(ref hash, theme != null ? theme.SolidBlock : null);
            AddSlotSignature(ref hash, theme != null ? theme.BreakableBlock : null);
            return hash;
        }
    }

    private static void AddSlotSignature(ref int hash, StageTheme.VisualSlot slot)
    {
        if (slot?.Variants == null) return;
        foreach (StageTheme.VisualVariant variant in slot.Variants)
        {
            hash = hash * 31 + (variant?.Prefab != null ? variant.Prefab.GetInstanceID() : 0);
            if (variant == null) continue;
            hash = hash * 31 + variant.Scale.GetHashCode();
            hash = hash * 31 + variant.Position.GetHashCode();
            hash = hash * 31 + variant.Rotation.GetHashCode();
            hash = hash * 31 + variant.Weight.GetHashCode();
        }
    }

    private Vector3 GetPreviewStartPosition()
    {
        GameObject showcase = GameObject.Find("Showcase (Delete after)");
        if (showcase != null)
        {
            Renderer[] renderers = showcase.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                return new Vector3(bounds.max.x + 1.5f, bounds.min.y, bounds.center.z);
            }
        }
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 center = view != null ? view.pivot : Vector3.zero;
        return center + Vector3.right * 3f;
    }

    private void FramePreviewInScene()
    {
        if (previewLineup == null) BuildPreviewLineup();
        if (previewRoot == null) return;
        Selection.activeGameObject = previewRoot;
        EditorGUIUtility.PingObject(previewRoot);
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) view = EditorWindow.GetWindow<SceneView>();
        Bounds bounds = GetComparisonBounds();
        view.pivot = bounds.center;
        view.size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 1.65f;
        view.orthographic = false;
        view.Focus();
        view.Repaint();
    }

    private Bounds GetComparisonBounds()
    {
        Bounds bounds = GetLineupBounds();
        GameObject showcase = GameObject.Find("Showcase (Delete after)");
        if (showcase == null) return bounds;

        Renderer[] renderers = showcase.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private Bounds GetPreviewBounds()
    {
        Renderer[] renderers = previewRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(previewRoot.transform.position + Vector3.up * 0.45f, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private Bounds GetLineupBounds()
    {
        if (previewLineup == null) return GetPreviewBounds();
        Renderer[] renderers = previewLineup.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return GetPreviewBounds();
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void CleanupPreview()
    {
        if (previewRoot != null) previewRoot = null;
        foreach (GameObject instance in previewEnemies)
            if (instance != null) DestroyImmediate(instance);
        previewEnemies.Clear();
        if (previewLineup != null) DestroyImmediate(previewLineup);
        previewLineup = null;
        previewRoot = null;
    }
}
