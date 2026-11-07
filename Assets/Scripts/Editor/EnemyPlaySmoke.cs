using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyPlaySmoke
{
    private const string Running = "Bomberman.EnemySmoke.Running";
    private static BombermanPrototype game;
    private static EnemyController[] enemies;
    private static Vector3[] positions;
    private static double deadline;
    private static double watchdog;
    private static int phase;
    private static EnemyController dying, trapped, specialDying;
    private static EnemyController jointedDying;
    private static EnemyController[] onealDeaths;
    private static ParticleSystem[] onealChunks;
    private static Material[] onealChunkBurnMaterials;
    private static int[] pausedChunkCounts;
    private static float[] pausedChunkClippings;
    private static Transform[] ragdollTransforms;
    private static Vector3[] ragdollPositions;
    private static Vector3 knockbackStart;
    private static bool failed;
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run smoke checks in an isolated batch project.");
        EnemyValidation.Run();
        EnemyAppearanceValidation.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        game = new GameObject("Smoke Game").AddComponent<BombermanPrototype>();
        SerializedObject settings = new(game);
        settings.FindProperty("spawnEnemies").boolValue = true;
        settings.FindProperty("useOriginalStageEnemies").boolValue = false;
        settings.FindProperty("destructibleDensity").floatValue = 0f;
        settings.FindProperty("randomSeed").intValue = 123;
        string[] counts = { "valcomCount", "onealCount", "dahlCount", "minuoCount", "ovapeCount", "doriaCount", "passCount", "pontanCount" };
        foreach (string count in counts) settings.FindProperty(count).intValue = count == "onealCount" ? 2 : 1;
        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyAssetBuilder.PrefabFolder });
        SerializedProperty prefabs = settings.FindProperty("enemyPrefabs");
        prefabs.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++) prefabs.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(paths[i])).GetComponent<EnemyController>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        // A ninth, scene-placed enemy uses a replacement visual and override clips.
        GameObject placed = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.PrefabFolder + "/Valcom.prefab"));
        placed.transform.position = new Vector3(0.5f, 0f, -3.5f);
        EnemyController actor = placed.GetComponent<EnemyController>();
        Transform previous = placed.transform.Find("VisualRoot/Model");
        GameObject replacement = UnityEngine.Object.Instantiate(previous.gameObject, previous.parent);
        replacement.name = "Model";
        UnityEngine.Object.DestroyImmediate(previous.gameObject);
        actor.ModelAnimator = replacement.GetComponent<Animator>();
        actor.BodyRenderer = replacement.GetComponentInChildren<Renderer>();
        AnimatorOverrideController replacementController = new(actor.ModelAnimator.runtimeAnimatorController);
        foreach (AnimationClip original in actor.ModelAnimator.runtimeAnimatorController.animationClips)
        {
            AnimationClip custom = UnityEngine.Object.Instantiate(original);
            custom.name = "Replacement" + original.name;
            AssetDatabase.CreateAsset(custom, "Assets/EnemySmoke" + original.name + ".anim");
            replacementController[original.name] = custom;
        }
        AssetDatabase.CreateAsset(replacementController, "Assets/EnemySmokeOverride.overrideController");
        actor.ModelAnimator.runtimeAnimatorController = replacementController;
        replacement.transform.localScale = Vector3.one * 0.8f;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/EnemySmoke.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Running, false)) return;
        deadline = EditorApplication.timeSinceStartup + 90;
        watchdog = deadline;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    private static void Log(string message, string stack, LogType type)
    {
        // Unity 6000.3 can throw while creating its search index in a fresh batch project.
        // This editor-only subsystem is unrelated to the runtime under test.
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Exception || type == LogType.Error) failed = true;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    private static void Pause(bool value) => typeof(BombermanPrototype).GetMethod("SetPaused", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { value });
    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            if (failed) throw new Exception("Unexpected runtime error; see log.");
            double now = EditorApplication.timeSinceStartup;
            if (now > watchdog) throw new Exception("Play smoke timed out.");
            // Gameplay deadlines use scaled game time; only the paused phase uses wall time.
            // Batch editor stalls otherwise let assertions outrun movement and animation.
            double clock = phase == 2 || phase == 4 || phase == 7 || phase == 12 || phase == 16
                ? now : Time.timeAsDouble;
            if (phase == 0)
            {
                game = UnityEngine.Object.FindFirstObjectByType<BombermanPrototype>();
                if (game == null || !game.IsReady)
                {
                    if (now > deadline) throw new Exception("Game startup timed out.");
                    return;
                }
                if (game.IsPaused) return;
                enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
                Check(enemies.Length == 10, "Nine spawned enemies plus one scene-placed enemy expected.");
                GameObject reference = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.PrefabFolder + "/Doria.prefab"));
                reference.GetComponent<EnemyController>().enabled = false;
                reference.GetComponentInChildren<Animator>().enabled = false;
                EnemyAppearanceValidation.Capture(reference, "prefab");
                UnityEngine.Object.DestroyImmediate(reference);
                positions = enemies.Select(e => e.transform.position).ToArray();
                phase = 1; deadline = Time.timeAsDouble + 1.2;
            }
            else if (clock >= deadline && phase == 1)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Check(enemies[i].transform.position != positions[i], enemies[i].name + " did not move.");
                    Check(enemies[i].ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), "Walk transition missing.");
                    EnemyAppearanceValidation.AssertWidth(enemies[i].gameObject);
                }
                EnemyAppearanceValidation.Capture(enemies.First(e => e is DoriaEnemy).gameObject, "play-mode");
                Pause(true); positions = enemies.Select(e => e.transform.position).ToArray();
                phase = 2; deadline = now + 0.25;
            }
            else if (clock >= deadline && phase == 2)
            {
                for (int i = 0; i < enemies.Length; i++)
                    Check(enemies[i].transform.position == positions[i] && enemies[i].ModelAnimator.speed == 0f, "Pause failed.");
                Pause(false);
                dying = enemies.First(e => e is OvapeEnemy);
                knockbackStart = dying.transform.position;
                var map = (BombermanMap)typeof(BombermanPrototype).GetField("map", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                var cells = (System.Collections.Generic.Dictionary<Vector2Int, CellKind>)typeof(BombermanMap).GetField("cells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map);
                cells[dying.Cell] = CellKind.Destructible;
                MethodInfo explode = typeof(BombermanPrototype).GetMethod("Explode", BindingFlags.Instance | BindingFlags.NonPublic);
                explode.Invoke(game, new object[] { dying.Cell + Vector2Int.left });
                Check(map.GetCellKind(dying.Cell) == CellKind.Empty, "Wall containing enemy was not destroyed.");
                int particles = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Enemy Death Particles");
                explode.Invoke(game, new object[] { dying.Cell + Vector2Int.left });
                Check(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Enemy Death Particles") == particles, "Duplicate death particles.");
                trapped = enemies.First(e => e is ValcomEnemy && !e.IsDead);
                foreach (Vector2Int d in BombermanPrototype.Directions) cells[trapped.Cell + d] = CellKind.Solid;
                Check(dying.IsDead && !dying.IsMoving, "Death gameplay state failed.");
                Check(dying.GetComponent<Rigidbody>() != null && !dying.GetComponent<Rigidbody>().isKinematic,
                    "Knockback death did not enable the single-body physics response.");
                Check(Mathf.Approximately(dying.DeathKnockbackForce, 5.25f)
                    && dying.GetComponent<Rigidbody>().linearVelocity.magnitude > 5.3f,
                    "Bomb death did not apply the increased knockback impulse.");
                CheckDeathCollisionsIgnored(dying);
                phase = 3; deadline = Time.timeAsDouble + 0.6;
            }
            else if (clock >= deadline && phase == 3)
            {
                Check(dying != null && !dying.ModelAnimator.enabled, "Death presentation did not stop the walk animator.");
                Check(dying.transform.position != knockbackStart, "Knockback did not move the enemy.");
                EnemyAppearanceValidation.AssertWidth(dying.gameObject);
                Check(!trapped.IsMoving && trapped.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Trapped enemy did not transition to Idle.");
                Pause(true);
                positions = new[] { dying.transform.position };
                phase = 4; deadline = now + 0.25;
            }
            else if (clock >= deadline && phase == 4)
            {
                Check(dying != null && dying.transform.position == positions[0], "Death physics continued while paused.");
                Pause(false);
                phase = 5; deadline = Time.timeAsDouble + 3.2;
            }
            else if (clock >= deadline && phase == 5)
            {
                Check(dying == null, "Dead enemy was not removed.");
                BeginOnealDebrisDeath();
                phase = 6; deadline = Time.timeAsDouble + 0.65;
            }
            else if (clock >= deadline && phase == 6)
            {
                for (int i = 0; i < onealChunks.Length; i++)
                {
                    Check(onealChunks[i] != null && onealChunks[i].particleCount == 20,
                        "O'neal wall-style debris chunks did not remain visible during their burn phase.");
                    Check(onealChunkBurnMaterials[i] != null && onealChunkBurnMaterials[i].GetFloat("_Clipping") < 1f,
                        "O'neal debris chunks did not start burning/dissolving after the hold delay.");
                }
                Pause(true);
                pausedChunkCounts = onealChunks.Select(chunks => chunks.particleCount).ToArray();
                pausedChunkClippings = onealChunkBurnMaterials.Select(material => material.GetFloat("_Clipping")).ToArray();
                phase = 7; deadline = now + 0.25;
            }
            else if (clock >= deadline && phase == 7)
            {
                for (int i = 0; i < onealChunks.Length; i++)
                    Check(onealChunks[i] != null && onealChunks[i].particleCount == pausedChunkCounts[i]
                        && Mathf.Approximately(onealChunkBurnMaterials[i].GetFloat("_Clipping"), pausedChunkClippings[i]),
                        "O'neal debris simulation or burn dissolve advanced while paused.");
                Pause(false);
                phase = 8; deadline = Time.timeAsDouble + 2.6;
            }
            else if (clock >= deadline && phase == 8)
            {
                Check(onealDeaths.All(enemy => enemy == null), "O'neal did not dissolve and get removed after its debris burst.");
                BeginSpecialDeath(typeof(ValcomEnemy), "Valcom Balloon Pop");
                phase = 9; deadline = Time.timeAsDouble + 1.05;
            }
            else if (clock >= deadline && phase == 9)
            {
                Check(specialDying == null, "Valcom was not removed after its particle-only death.");
                BeginSpecialDeath(typeof(DoriaEnemy), "Doria Fluid Burst");
                phase = 10; deadline = Time.timeAsDouble + 1.05;
            }
            else if (clock >= deadline && phase == 10)
            {
                Check(specialDying == null, "Doria was not removed after its particle-only death.");
                BeginJointedDeath(typeof(PassEnemy));
                phase = 11; deadline = Time.timeAsDouble + 0.35;
            }
            else if (clock >= deadline && phase == 11)
            {
                CheckRagdollMoved("Pass");
                Pause(true);
                CaptureRagdollPositions();
                phase = 12; deadline = now + 0.25;
            }
            else if (clock >= deadline && phase == 12)
            {
                CheckRagdollPositionsFrozen("Pass");
                Pause(false);
                CaptureRagdollPositions();
                phase = 13; deadline = Time.timeAsDouble + 0.35;
            }
            else if (clock >= deadline && phase == 13)
            {
                CheckRagdollMoved("Pass after resume");
                phase = 14; deadline = Time.timeAsDouble + 2.85;
            }
            else if (clock >= deadline && phase == 14)
            {
                Check(jointedDying == null, "Pass was not dissolved and removed after its ragdoll death.");
                BeginJointedDeath(typeof(PontanEnemy));
                phase = 15; deadline = Time.timeAsDouble + 0.35;
            }
            else if (clock >= deadline && phase == 15)
            {
                CheckRagdollMoved("Pontan");
                Pause(true);
                CaptureRagdollPositions();
                phase = 16; deadline = now + 0.25;
            }
            else if (clock >= deadline && phase == 16)
            {
                CheckRagdollPositionsFrozen("Pontan");
                Pause(false);
                CaptureRagdollPositions();
                phase = 17; deadline = Time.timeAsDouble + 0.35;
            }
            else if (clock >= deadline && phase == 17)
            {
                CheckRagdollMoved("Pontan after resume");
                phase = 18; deadline = Time.timeAsDouble + 2.85;
            }
            else if (clock >= deadline && phase == 18)
            {
                Check(jointedDying == null, "Pontan was not dissolved and removed after its ragdoll death.");
                Debug.Log("ENEMY PLAY SMOKE PASSED: spawning, pause/resume, knockback, articulated ragdolls, dissolve, particle-only deaths, delayed removal.");
                Finish(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }
    private static void BeginSpecialDeath(Type enemyType, string burstName)
    {
        specialDying = enemies.First(e => e != null && enemyType.IsInstanceOfType(e) && !e.IsDead);
        specialDying.Die();
        Check(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name == burstName),
            burstName + " effect was not spawned.");
        if (enemyType == typeof(ValcomEnemy))
        {
            Color[] particleColors = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == burstName + " Particle")
                .Select(t => t.GetComponent<Renderer>().sharedMaterial.color)
                .ToArray();
            Check(particleColors.Length == 26, "Valcom balloon burst particle count changed.");
            Check(particleColors.All(color => color.r > color.g && color.g > color.b),
                "Valcom balloon burst contains purple or non-yellow particle colors.");
        }
        Check(!specialDying.GetComponentsInChildren<Renderer>().Any(r => r.enabled && r is not ParticleSystemRenderer),
            burstName + " did not hide the model for its particle-only death.");
        bool dissolving = (bool)typeof(EnemyController).GetField("deathDissolving", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(specialDying);
        Material[] materials = (Material[])typeof(EnemyController).GetField("burnMaterials", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(specialDying);
        Check(!dissolving && materials == null, burstName + " unexpectedly started a dissolve.");
    }
    private static void BeginOnealDebrisDeath()
    {
        onealDeaths = enemies.Where(e => e != null && e is OnealEnemy && !e.IsDead).Take(2).ToArray();
        Check(onealDeaths.Length == 2, "Smoke scene needs separate O'neal instances for bomb and non-bomb deaths.");
        onealDeaths[0].Die();
        onealDeaths[1].DieFromBomb(onealDeaths[1].DeathDissolveDuration,
            onealDeaths[1].transform.position + Vector3.left);
        Check(onealDeaths[0].GetComponentsInChildren<Renderer>().Any(r => r.enabled && r is not ParticleSystemRenderer),
            "Non-bomb O'neal should keep its character visible for body dissolve.");
        Check(!onealDeaths[1].GetComponentsInChildren<Renderer>().Any(r => r.enabled && r is not ParticleSystemRenderer),
            "Bomb-hit O'neal should hide its character like Doria.");
        Check(!(bool)typeof(EnemyController).GetField("deathBurstPending", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(onealDeaths[0])
            && (bool)typeof(EnemyController).GetField("deathBurstPending", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(onealDeaths[1]),
            "Only bomb-hit O'neal should use the short disappear-after-burst timing.");

        Transform[] debrisRoots = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name == "O'neal Death Debris").Take(2).ToArray();
        Check(debrisRoots.Length == 2, "O'neal deaths did not create two debris effects.");
        onealChunks = new ParticleSystem[debrisRoots.Length];
        onealChunkBurnMaterials = new Material[debrisRoots.Length];
        for (int i = 0; i < debrisRoots.Length; i++)
        {
            onealChunks[i] = debrisRoots[i].Find("Chunks")?.GetComponent<ParticleSystem>();
            Check(onealChunks[i] != null && onealChunks[i].particleCount == 20,
                "O'neal death did not explicitly emit 20 wall-style chunks.");

            ParticleSystemRenderer renderer = onealChunks[i].GetComponent<ParticleSystemRenderer>();
            Check(renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh
                && renderer.mesh == Resources.GetBuiltinResource<Mesh>("Cube.fbx"),
                "O'neal debris is not using the wall-style mesh chunks.");
            Check(renderer.GetComponent<WallDebrisBurn>() != null,
                "O'neal debris chunks are missing their burn dissolve effect.");
            onealChunkBurnMaterials[i] = renderer.sharedMaterial;
            Check(onealChunkBurnMaterials[i] != null && onealChunkBurnMaterials[i].HasProperty("_Clipping"),
                "O'neal debris burn material does not expose clipping.");
        }
    }
    private static void BeginJointedDeath(Type enemyType)
    {
        jointedDying = enemies.First(e => e != null && enemyType.IsInstanceOfType(e) && !e.IsDead);
        jointedDying.Die();
        ragdollTransforms = jointedDying.GetComponentsInChildren<CharacterJoint>(true).Select(joint => joint.transform).ToArray();
        Check(ragdollTransforms.Length >= 7, enemyType.Name + " did not activate its articulated ragdoll.");
        Rigidbody[] bodies = jointedDying.GetComponentsInChildren<Rigidbody>(true);
        Check(jointedDying.GetComponent<Rigidbody>() == null && bodies.Length == ragdollTransforms.Length + 1
            && bodies.All(body => !body.isKinematic), enemyType.Name + " ragdoll Rigidbody setup is invalid.");
        Rigidbody rootBody = ragdollTransforms[0].GetComponent<Rigidbody>();
        Check(Mathf.Approximately(jointedDying.DeathKnockbackForce, 5.25f)
            && rootBody.linearVelocity.magnitude > 4f,
            enemyType.Name + " ragdoll did not receive the increased death impulse.");
        CheckDeathCollisionsIgnored(jointedDying);
        CaptureRagdollPositions();
    }
    private static void CheckDeathCollisionsIgnored(EnemyController enemy)
    {
        PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        Check(player != null, "Player collider missing from death collision check.");
        Collider[] enemyColliders = enemy.GetComponentsInChildren<Collider>(true).Where(collider => collider.enabled).ToArray();
        Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true).Where(collider => collider.enabled).ToArray();
        Check(enemyColliders.Length > 0 && playerColliders.Length > 0, "Death/player colliders missing.");
        foreach (Collider enemyCollider in enemyColliders)
            foreach (Collider playerCollider in playerColliders)
                Check(Physics.GetIgnoreCollision(enemyCollider, playerCollider),
                    enemy.name + " death collider still collides with the player.");
    }
    private static void CaptureRagdollPositions()
    {
        ragdollPositions = ragdollTransforms.Select(bone => bone.position).ToArray();
    }
    private static void CheckRagdollMoved(string enemyName)
    {
        bool moved = false;
        if (jointedDying != null)
            for (int i = 0; i < ragdollTransforms.Length; i++)
                if (Vector3.Distance(ragdollTransforms[i].position, ragdollPositions[i]) > 0.01f) { moved = true; break; }
        Check(moved, enemyName + " ragdoll bones did not move under physics.");
    }
    private static void CheckRagdollPositionsFrozen(string enemyName)
    {
        bool frozen = jointedDying != null;
        for (int i = 0; i < ragdollTransforms.Length && frozen; i++)
            frozen &= Vector3.Distance(ragdollTransforms[i].position, ragdollPositions[i]) < 0.0001f;
        Check(frozen, enemyName + " ragdoll physics continued while paused.");
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



