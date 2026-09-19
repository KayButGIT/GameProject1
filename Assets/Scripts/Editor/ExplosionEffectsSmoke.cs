using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ExplosionEffectsSmoke
{
    private const string Running = "Bomberman.ExplosionEffectsSmoke";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static BombermanPrototype game;
    private static Vector2Int origin, destroyedCell;
    private static int phase;
    private static double deadline, watchdog;
    private static bool failed;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        game = new GameObject("Explosion Effects Smoke Game").AddComponent<BombermanPrototype>();
        SerializedObject settings = new(game);
        settings.FindProperty("width").intValue = 9;
        settings.FindProperty("height").intValue = 7;
        settings.FindProperty("randomSeed").intValue = 123;
        settings.FindProperty("spawnEnemies").boolValue = false;
        settings.FindProperty("playerCanDieFromBomb").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(game.gameObject.scene, "Assets/ExplosionEffectsSmoke.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
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
        if (!condition) throw new Exception("Explosion effects smoke: " + message);
    }

    private static BombermanMap Map => (BombermanMap)typeof(BombermanPrototype).GetField("map", Private).GetValue(game);
    private static Transform Effect(string prefix, Vector2Int cell) => GameObject.Find($"{prefix} {cell.x},{cell.y}")?.transform;
    private static ParticleSystem ParticlesIn(Transform root, string name) => root.Find(name)?.GetComponent<ParticleSystem>();

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime error.");
            if (EditorApplication.timeSinceStartup > watchdog) throw new Exception("Explosion effects smoke timed out.");
            if (phase == 0)
            {
                game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
                if (game == null || !game.IsReady) return;
                BombermanMap map = Map;
                (origin, destroyedCell) = Enumerable.Range(1, 7).SelectMany(x => Enumerable.Range(1, 5).Select(y => new Vector2Int(x, y)))
                    .Where(cell => map.GetCellKind(cell) == CellKind.Empty)
                    .SelectMany(cell => BombermanPrototype.Directions.Select(direction => (cell, cell + direction)))
                    .First(pair => map.GetCellKind(pair.Item2) == CellKind.Destructible);
                Material blockMaterial = map.GetBlockMaterial(destroyedCell);
                Check(blockMaterial != null, "Breakable block has a material");
                typeof(BombermanPrototype).GetMethod("Explode", Private).Invoke(game, new object[] { origin });

                Transform center = Effect("Explosion", origin);
                Check(center != null && new[] { "Fireball", "Glow", "Smoke", "Sparks" }.All(name => ParticlesIn(center, name) != null), "Blast center has fire, glow, smoke, and sparks");
                Material fire = center.Find("Fireball").GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Material smoke = center.Find("Smoke").GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Check(fire.shader.name == "Universal Render Pipeline/Particles/Unlit" && fire.GetFloat("_DstBlend") == (float)BlendMode.One
                    && fire.renderQueue == (int)RenderQueue.Transparent + 1, "Fire uses additive URP particles");
                Check(smoke.GetFloat("_DstBlend") == (float)BlendMode.OneMinusSrcAlpha && smoke.renderQueue == (int)RenderQueue.Transparent, "Smoke uses alpha-blended particles");

                Transform arm = Effect("Explosion", destroyedCell);
                Check(arm != null && ParticlesIn(arm, "Sparks") == null && Mathf.Abs(ParticlesIn(arm, "Fireball").main.startDelay.constant - BlastEffects.SpreadDelay) < 0.0001f,
                    "Blast arm fire starts after the center, without sparks");
                Transform debris = Effect("Debris", destroyedCell);
                ParticleSystem chunks = debris != null ? ParticlesIn(debris, "Chunks") : null;
                Check(chunks != null && ParticlesIn(debris, "Dust") != null, "Destroyed block leaves debris and dust");
                ParticleSystemRenderer chunkRenderer = chunks.GetComponent<ParticleSystemRenderer>();
                Check(chunkRenderer.renderMode == ParticleSystemRenderMode.Mesh && chunkRenderer.sharedMaterial == blockMaterial && chunks.collision.enabled,
                    "Debris chunks use the block's material and bounce on the floor");
                phase = 1;
                deadline = Time.timeAsDouble + 0.3;
            }
            else if (phase == 1 && Time.timeAsDouble >= deadline)
            {
                Check(ParticlesIn(Effect("Explosion", origin), "Fireball").particleCount > 0, "Fireball particles are alive");
                Check(ParticlesIn(Effect("Debris", destroyedCell), "Chunks").particleCount > 0, "Debris particles are alive");
                phase = 2;
                deadline = Time.timeAsDouble + BlastEffects.Duration + BlastEffects.SpreadDelay;
            }
            else if (phase == 2 && Time.timeAsDouble >= deadline)
            {
                Check(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name.StartsWith("Explosion ") || t.name.StartsWith("Debris ")),
                    "Explosion and debris effects are removed");
                Debug.Log("EXPLOSION_EFFECTS_SMOKE_PASS: center layers, additive fire, alpha smoke, spread delay, arm without sparks, debris material, floor collision, live particles, cleanup.");
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
        Time.timeScale = 1f;
        EditorApplication.Exit(code);
    }
}
