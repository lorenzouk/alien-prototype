using System.Collections.Generic;
using System.IO;
using System.Linq;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Alien Prototype > Build Basic Level
//
// Builds 5 map scenes (Map1-Map5) that get harder in order. Each room has
// outer walls, inner walls, the team's enemies, the big chasing ship, a baked
// 2D NavMesh for the ship, and doors that switch maps.
//
//   [Map 5]
//      |
//   [Map 4] -- [Map 3]
//                 |
//   [Map 1] -- [Map 2]
//
// Doors:
//   NEXT (green)  - leads to the next, harder room. LOCKED (red) until every
//                   enemy in the room is dead. Chevrons pulse toward it once open.
//   BACK (blue)   - leads to the previous room. Always open (escape hatch from
//                   the big ship).
//
// Difficulty ramps per room: more enemies, faster + more accurate enemy fire,
// the ship arrives sooner and moves faster, and the layout gets more complex.
//
// Safe to re-run: asks before overwriting the maps. SampleScene is never modified.
public static class LevelBuilder
{
    const string MapFolder = "Assets/Scenes/Maps";
    const string NavMeshFolder = "Assets/Scenes/Maps/NavMeshes";
    const string SquarePath = "Assets/Level/Art/Square.png";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    const string ShipPrefabPath = "Assets/Prefabs/BigSpaceShip.prefab";
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    // Map interior is 32 x 18 units; the camera shows the whole map.
    const float HalfW = 16f, HalfH = 9f;
    const float WallThickness = 0.5f;
    const float DoorGap = 3f;
    const float SpawnInset = 2.5f;
    const float CameraSize = HalfH + 0.6f;

    // Where the ship waits (hidden, off screen) until its spawn delay is up.
    static readonly Vector2 ShipParking = new Vector2(-22f, -11f);

    static readonly Color WallColor = new Color(0.35f, 0.38f, 0.45f);
    static readonly Color InnerWallColor = new Color(0.25f, 0.55f, 0.75f);
    static readonly Color NextDoorColor = new Color(0.25f, 1f, 0.45f);
    static readonly Color BackDoorColor = new Color(0.3f, 0.8f, 1f);
    static readonly Color LockedDoorColor = new Color(1f, 0.25f, 0.25f);

    class DoorDef
    {
        public DoorSide side;
        public string target;
        public string targetLabel;
        public bool forward; // forward doors lock until the room is cleared
    }

    class MapDef
    {
        public string scene;
        public string label;
        public Color floor;
        public Vector2? start; // only the first map
        public List<DoorDef> doors = new List<DoorDef>();
        public List<(Vector2 center, Vector2 size)> walls = new List<(Vector2, Vector2)>();
        public List<Vector2> enemies = new List<Vector2>();

        // Difficulty
        public float enemyMinCooldown = 2f, enemyMaxCooldown = 6f;
        public float enemyAimVariance = 0.1f; // lower = more accurate
        public float shipDelay = 5f;          // seconds before the big ship enters
        public float shipSpeed = 5f;          // player moves at 5
    }

    static DoorDef Next(DoorSide side, string target) =>
        new DoorDef { side = side, target = target, targetLabel = Pretty(target), forward = true };
    static DoorDef Back(DoorSide side, string target) =>
        new DoorDef { side = side, target = target, targetLabel = Pretty(target), forward = false };
    static string Pretty(string scene) => scene.Replace("Map", "MAP ");

    // ---- Edit the maps here ---------------------------------------------------
    // Map spans x -16..16, y -9..9. Doors sit in the middle of a side.
    // Spawns next to doors: West (-13.5,0), East (13.5,0), South (0,-6.5), North (0,6.5).
    // Keep walls out of the doorways so the path from door to door stays clear.
    // Inner walls: (center, size) in world units.
    static List<MapDef> Layout() => new List<MapDef>
    {
        // 1 - EASY. Open room, one pillar to learn cover. Start west, exit east.
        new MapDef
        {
            scene = "Map1", label = "Map 1", floor = new Color(0.08f, 0.10f, 0.14f),
            start = new Vector2(-12f, 0f),
            doors = { Next(DoorSide.East, "Map2") },
            walls =
            {
                (new Vector2(0f, 0f), new Vector2(0.5f, 6f)),      // center pillar, blocks the straight shot
                (new Vector2(-6f, 5f), new Vector2(3f, 0.5f)),     // small cover blocks
                (new Vector2(-6f, -5f), new Vector2(3f, 0.5f)),
            },
            enemies = { new Vector2(6f, 5f), new Vector2(7f, -5f) },
            enemyMinCooldown = 3.5f, enemyMaxCooldown = 6f, enemyAimVariance = 0.35f,
            shipDelay = 15f, shipSpeed = 2.5f,
        },

        // 2 - Two lanes. A long wall splits the room; walk the top lane to the
        // north exit, but one enemy hides below the wall.
        new MapDef
        {
            scene = "Map2", label = "Map 2", floor = new Color(0.08f, 0.11f, 0.10f),
            doors = { Back(DoorSide.West, "Map1"), Next(DoorSide.North, "Map3") },
            walls =
            {
                (new Vector2(2f, -3f), new Vector2(14f, 0.5f)),    // long lane divider
                (new Vector2(-6f, 4f), new Vector2(0.5f, 4f)),     // cover pillars on the top lane
                (new Vector2(5f, 4f), new Vector2(0.5f, 5f)),
            },
            enemies = { new Vector2(9f, 5f), new Vector2(-10f, 6f), new Vector2(8f, -6f) },
            enemyMinCooldown = 3f, enemyMaxCooldown = 5f, enemyAimVariance = 0.25f,
            shipDelay = 12f, shipSpeed = 3f,
        },

        // 3 - S-curve. Enter south, go west UNDER the tall wall, then turn north
        // through the gap at the far left to reach the west exit.
        new MapDef
        {
            scene = "Map3", label = "Map 3", floor = new Color(0.12f, 0.10f, 0.07f),
            doors = { Back(DoorSide.South, "Map2"), Next(DoorSide.West, "Map4") },
            walls =
            {
                (new Vector2(-5f, 2f), new Vector2(0.5f, 14f)),    // tall wall, gap at the bottom
                (new Vector2(-8.5f, -3f), new Vector2(7f, 0.5f)),  // shelf, gap at the far left
                (new Vector2(6f, 2f), new Vector2(0.5f, 6f)),      // east side cover
                (new Vector2(10f, -2f), new Vector2(6f, 0.5f)),
            },
            enemies = { new Vector2(-10f, 5f), new Vector2(-9f, -6.5f), new Vector2(9f, 5f), new Vector2(6f, -6f) },
            enemyMinCooldown = 2.5f, enemyMaxCooldown = 4.5f, enemyAimVariance = 0.2f,
            shipDelay = 10f, shipSpeed = 3.5f,
        },

        // 4 - Gauntlet. Enter east into a sealed column, drop under it at the
        // bottom, cut through the middle and up the center gap to the north exit.
        new MapDef
        {
            scene = "Map4", label = "Map 4", floor = new Color(0.13f, 0.08f, 0.07f),
            doors = { Back(DoorSide.East, "Map3"), Next(DoorSide.North, "Map5") },
            walls =
            {
                (new Vector2(8f, 1.5f), new Vector2(0.5f, 15f)),   // seals the east column (gap at the bottom)
                (new Vector2(-6f, 4f), new Vector2(8f, 0.5f)),     // two walls guard the exit,
                (new Vector2(5f, 4f), new Vector2(6f, 0.5f)),      //   leaving a center gap
                (new Vector2(-4f, 0f), new Vector2(0.5f, 5f)),     // middle corridor walls
                (new Vector2(3f, -3.5f), new Vector2(6f, 0.5f)),
            },
            enemies = { new Vector2(12f, -6f), new Vector2(3f, -6.5f), new Vector2(-10f, -5f), new Vector2(4f, 0f), new Vector2(6f, 6f) },
            enemyMinCooldown = 2f, enemyMaxCooldown = 4f, enemyAimVariance = 0.15f,
            shipDelay = 8f, shipSpeed = 4f,
        },

        // 5 - HARD. Fortress. A bunker in the middle opens only at the top, so
        // you have to flank around it. Clear all 7 to finish.
        new MapDef
        {
            scene = "Map5", label = "Map 5", floor = new Color(0.15f, 0.05f, 0.06f),
            doors = { Back(DoorSide.South, "Map4") },
            walls =
            {
                (new Vector2(0f, -1f), new Vector2(10f, 0.5f)),    // bunker: solid bottom faces the entrance
                (new Vector2(-5f, 2f), new Vector2(0.5f, 6f)),     //   sides
                (new Vector2(5f, 2f), new Vector2(0.5f, 6f)),
                (new Vector2(-3.5f, 5f), new Vector2(3f, 0.5f)),   //   top, open in the middle
                (new Vector2(3.5f, 5f), new Vector2(3f, 0.5f)),
                (new Vector2(-10f, 1f), new Vector2(0.5f, 6f)),    // flank pillars
                (new Vector2(10f, 1f), new Vector2(0.5f, 6f)),
                (new Vector2(-6f, -5f), new Vector2(4f, 0.5f)),    // cover near the entrance
                (new Vector2(6f, -5f), new Vector2(4f, 0.5f)),
            },
            enemies =
            {
                new Vector2(0f, 2.5f), new Vector2(-3f, 1f), new Vector2(3f, 1f),  // inside the bunker
                new Vector2(-13f, 6f), new Vector2(13f, 6f), new Vector2(-13f, -6f), new Vector2(13f, -6f),
            },
            enemyMinCooldown = 1.5f, enemyMaxCooldown = 3.5f, enemyAimVariance = 0.1f,
            shipDelay = 6f, shipSpeed = 4.5f,
        },
    };
    // ---------------------------------------------------------------------------

    [MenuItem("Tools/Alien Prototype/Build Basic Level")]
    static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var player = GetPlayerPrefab(out string problem);
        if (player == null)
        {
            EditorUtility.DisplayDialog("Build Basic Level", problem, "OK");
            return;
        }

        var maps = Layout();
        if (maps.Any(m => File.Exists(ScenePath(m))) &&
            !EditorUtility.DisplayDialog("Build Basic Level",
                "Map scenes already exist in " + MapFolder + ". Overwrite them?", "Overwrite", "Cancel"))
            return;

        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (enemy == null) Debug.LogWarning("[LevelBuilder] No Enemy prefab at " + EnemyPrefabPath + ", building maps without enemies.");
        var ship = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath);
        if (ship == null) Debug.LogWarning("[LevelBuilder] No BigSpaceShip prefab at " + ShipPrefabPath + ", building maps without the ship.");

        var sprite = GetSquareSprite();
        EnsureFolder(MapFolder);
        for (int i = 0; i < maps.Count; i++)
            BuildMap(maps[i], i + 1, maps.Count, sprite, player, enemy, ship);

        UpdateBuildSettings(maps);
        AssetDatabase.SaveAssets();

        EditorSceneManager.OpenScene(ScenePath(maps[0]));
        Debug.Log("[LevelBuilder] Built " + maps.Count + " maps in " + MapFolder + ".");
    }

    static string ScenePath(MapDef m) => MapFolder + "/" + m.scene + ".unity";

    static void BuildMap(MapDef def, int number, int total, Sprite sprite,
                         GameObject playerPrefab, GameObject enemyPrefab, GameObject shipPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera: fixed, shows the whole map
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = CameraSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        var root = new GameObject(def.label);
        var room = root.AddComponent<Room>();
        room.roomName = def.label;
        room.roomNumber = number;
        room.totalRooms = total;
        var entryDoor = def.doors.FirstOrDefault(d => !d.forward) ?? def.doors[0];
        room.defaultSpawn = def.start.HasValue ? "Start" : entryDoor.side.ToString();

        var walls = Child("Walls", root.transform);
        var doorsParent = Child("Doors", root.transform);
        var spawnsParent = Child("Spawns", root.transform);

        var floor = MakeBox("Floor", walls, Vector2.zero, new Vector2(HalfW * 2f, HalfH * 2f), def.floor, sprite, -10, false);
        floor.AddComponent<NavMeshModifier>(); // walkable for the ship

        // Outer walls, with a gap wherever there's a door
        foreach (DoorSide side in System.Enum.GetValues(typeof(DoorSide)))
            BuildOuterWall(walls, side, def.doors.Any(d => d.side == side), sprite);

        // Inner walls
        foreach (var (center, size) in def.walls)
            NotWalkable(MakeBox("Wall", walls, center, size, InnerWallColor, sprite, 0, true));

        // Doors + matching spawn points
        foreach (var d in def.doors)
        {
            BuildDoor(doorsParent, d, sprite);

            bool horizontal = d.side == DoorSide.North || d.side == DoorSide.South;
            float half = horizontal ? HalfH : HalfW;
            Vector2 outward = d.side.Outward();
            MakeSpawn(d.side.ToString(), spawnsParent, outward * (half - SpawnInset), -outward);
        }

        if (def.start.HasValue)
            MakeSpawn("Start", spawnsParent, def.start.Value, Vector2.right);

        // Team enemies, tuned for this room
        var enemiesParent = Child("Enemies", root.transform);
        room.enemiesRoot = enemiesParent;
        if (enemyPrefab != null)
        {
            foreach (var pos in def.enemies)
            {
                var e = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
                e.transform.SetParent(enemiesParent, false);
                e.transform.position = pos;

                var shoot = e.GetComponentInChildren<enemyShoot>();
                if (shoot != null)
                {
                    shoot.minCooldown = def.enemyMinCooldown;
                    shoot.maxCooldown = def.enemyMaxCooldown;
                    shoot.dirVariance = def.enemyAimVariance;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(shoot);
                }
            }
        }

        // Team player (Room moves it to the right spawn at runtime)
        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.tag = "Player";
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        var startSpawn = spawnsParent.GetComponentsInChildren<SpawnPoint>().First(s => s.id == room.defaultSpawn);
        player.transform.SetPositionAndRotation(startSpawn.transform.position, startSpawn.transform.rotation);

        // Big chasing ship, tuned for this room. Room points it at the door the
        // player actually came through at runtime.
        if (shipPrefab != null)
        {
            var s = (GameObject)PrefabUtility.InstantiatePrefab(shipPrefab, scene);
            s.transform.SetParent(root.transform, false);
            s.transform.position = ShipParking;
            var ai = s.GetComponent<BigSpaceShipAI>();
            if (ai != null)
            {
                ai.spawnDelay = def.shipDelay;
                ai.speed = def.shipSpeed;
                ai.player = player.transform;
                ai.playerSpawnPoint = startSpawn.transform;
                PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
            }
        }

        BakeNavMesh(def, root.transform);

        EditorSceneManager.SaveScene(scene, ScenePath(def));
    }

    // ---- Doors -----------------------------------------------------------------

    static void BuildDoor(Transform parent, DoorDef d, Sprite sprite)
    {
        bool horizontal = d.side == DoorSide.North || d.side == DoorSide.South;
        float half = horizontal ? HalfH : HalfW;
        Vector2 outward = d.side.Outward();
        Vector2 along = horizontal ? Vector2.right : Vector2.up;
        Vector2 doorPos = outward * half;
        Vector2 size = horizontal ? new Vector2(DoorGap, WallThickness) : new Vector2(WallThickness, DoorGap);
        Color openColor = d.forward ? NextDoorColor : BackDoorColor;

        // Trigger (the doorway itself)
        var doorGo = MakeBox("Door_" + d.side, parent, doorPos, size, openColor, sprite, -5, true);
        doorGo.GetComponent<BoxCollider2D>().isTrigger = true;
        var door = doorGo.AddComponent<Door>();
        door.side = d.side;
        door.targetScene = d.target;
        door.targetSpawn = d.side.Opposite().ToString(); // arrive next to the matching door
        door.requiresClear = d.forward;
        door.openColor = openColor;
        door.lockedColor = LockedDoorColor;
        door.openText = (d.forward ? "NEXT: " : "BACK: ") + d.targetLabel;
        door.fill = doorGo.GetComponent<SpriteRenderer>();

        // Solid barrier, only enabled while locked
        var barrier = new GameObject("Barrier");
        barrier.transform.SetParent(doorGo.transform, false);
        door.barrier = barrier.AddComponent<BoxCollider2D>();

        // Visuals live under an unscaled parent so they keep their shape
        var fx = Child("DoorFx_" + d.side, parent);
        fx.localPosition = doorPos;

        // Frame posts at both ends of the doorway
        var postSize = new Vector2(0.8f, 0.8f);
        var frame = new List<SpriteRenderer>();
        foreach (float sign in new[] { -1f, 1f })
        {
            var post = MakeBox("Post", fx, along * sign * (DoorGap / 2f + 0.2f), postSize, openColor, sprite, 2, false);
            frame.Add(post.GetComponent<SpriteRenderer>());
        }
        door.frame = frame.ToArray();

        // Chevrons on the floor pointing out through the door, innermost first
        var bars = new List<SpriteRenderer>();
        foreach (float inset in new[] { 2.3f, 1.6f, 0.9f })
        {
            var chev = Child("Chevron", fx);
            chev.localPosition = -outward * inset;
            chev.localRotation = Quaternion.LookRotation(Vector3.forward, outward);
            bars.AddRange(MakeChevron(chev, sprite, openColor));
        }
        door.chevrons = bars.ToArray();

        // Label beside the doorway
        Vector2 labelPos = horizontal
            ? along * 4.4f - outward * 1.0f       // next to a top/bottom door
            : Vector2.up * 2.9f - outward * 2.4f; // above a left/right door
        door.label = MakeLabel(fx, labelPos, door.openText, openColor);
    }

    // "^" made of two bars, apex at the local origin, pointing local up.
    static SpriteRenderer[] MakeChevron(Transform parent, Sprite sprite, Color color)
    {
        const float arm = 0.8f, thick = 0.18f;
        float o = arm * 0.5f * Mathf.Cos(45f * Mathf.Deg2Rad);
        var result = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            float sign = i == 0 ? -1f : 1f;
            var bar = MakeBox("Bar", parent, new Vector2(sign * o, -o), new Vector2(arm, thick), color, sprite, -4, false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, -45f * sign);
            result[i] = bar.GetComponent<SpriteRenderer>();
        }
        return result;
    }

    static TextMesh MakeLabel(Transform parent, Vector2 localPos, string text, Color color)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) return null;

        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = 0.07f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = 20;
        return tm;
    }

    // ---- NavMesh (for the big ship) -----------------------------------------

    static void NotWalkable(GameObject go)
    {
        var mod = go.AddComponent<NavMeshModifier>();
        mod.overrideArea = true;
        mod.area = 1; // Not Walkable
    }

    static void BakeNavMesh(MapDef def, Transform root)
    {
        var go = new GameObject("NavMesh");
        go.transform.SetParent(root, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // NavMeshPlus 2D orientation

        var surface = go.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.hideEditorLogs = true;

        // In edit mode the extension's Awake doesn't run, so register it by hand.
        var collect = go.AddComponent<CollectSources2d>();
        surface.NavMeshExtensions.Add(collect, collect.Order);

        surface.BuildNavMesh();
        if (surface.navMeshData == null)
        {
            Debug.LogWarning("[LevelBuilder] NavMesh bake failed for " + def.scene + ". Bake it by hand on the NavMesh object.");
            return;
        }

        string folder = NavMeshFolder + "/" + def.scene;
        EnsureFolder(folder);
        string path = folder + "/NavMesh-NavMesh.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);
    }

    // ---- Walls / helpers ---------------------------------------------------------

    static Transform Child(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static void BuildOuterWall(Transform parent, DoorSide side, bool hasDoor, Sprite sprite)
    {
        bool horizontal = side == DoorSide.North || side == DoorSide.South;
        float length = (horizontal ? HalfW : HalfH) * 2f + WallThickness;
        Vector2 center = side.Outward() * (horizontal ? HalfH : HalfW);
        Vector2 along = horizontal ? Vector2.right : Vector2.up;

        if (!hasDoor)
        {
            OuterWallPiece(parent, side, center, length, horizontal, sprite);
            return;
        }

        float segment = (length - DoorGap) / 2f;
        float offset = DoorGap / 2f + segment / 2f;
        OuterWallPiece(parent, side, center + along * offset, segment, horizontal, sprite);
        OuterWallPiece(parent, side, center - along * offset, segment, horizontal, sprite);
    }

    static void OuterWallPiece(Transform parent, DoorSide side, Vector2 center, float length, bool horizontal, Sprite sprite)
    {
        var size = horizontal ? new Vector2(length, WallThickness) : new Vector2(WallThickness, length);
        NotWalkable(MakeBox("Wall_" + side, parent, center, size, WallColor, sprite, 0, true));
    }

    static GameObject MakeBox(string name, Transform parent, Vector2 pos, Vector2 size, Color color, Sprite sprite, int order, bool collider)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;

        if (collider)
            go.AddComponent<BoxCollider2D>(); // fits the 1x1 sprite, scaled by the transform
        return go;
    }

    static void MakeSpawn(string id, Transform parent, Vector2 pos, Vector2 facing)
    {
        var go = new GameObject("Spawn_" + id);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.forward, facing));
        go.AddComponent<SpawnPoint>().id = id;
    }

    // 1x1 world-unit white square used for floors, walls and doors.
    static Sprite GetSquareSprite()
    {
        if (!File.Exists(SquarePath))
        {
            EnsureFolder(Path.GetDirectoryName(SquarePath));
            var tex = new Texture2D(4, 4);
            tex.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray());
            tex.Apply();
            File.WriteAllBytes(SquarePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SquarePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SquarePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
    }

    // Uses Assets/Prefabs/Player.prefab if it exists, otherwise saves the team's
    // Player from SampleScene as that prefab (SampleScene itself isn't changed).
    static GameObject GetPlayerPrefab(out string problem)
    {
        problem = null;
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (existing != null) return existing;

        if (!File.Exists(SampleScenePath))
        {
            problem = "No " + PlayerPrefabPath + " and " + SampleScenePath + " is missing.";
            return null;
        }

        EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        var player = GameObject.Find("Player");
        if (player == null || player.GetComponent<Rigidbody2D>() == null)
        {
            problem = "SampleScene has no Player object with a Rigidbody2D.";
            return null;
        }
        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(player) > 0)
        {
            problem = "The Player in SampleScene has missing scripts.";
            return null;
        }

        EnsureFolder(Path.GetDirectoryName(PlayerPrefabPath));
        return PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
    }

    // Creates an Assets/... folder (and parents) through the AssetDatabase.
    static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // Map1 goes first (it's what a build starts on). Scenes that no longer
    // exist on disk (e.g. a missing SampleScene) are dropped.
    static void UpdateBuildSettings(List<MapDef> maps)
    {
        var mapPaths = maps.Select(ScenePath).ToList();
        var others = EditorBuildSettings.scenes
            .Where(s => !mapPaths.Contains(s.path) && File.Exists(s.path));
        EditorBuildSettings.scenes = mapPaths
            .Select(p => new EditorBuildSettingsScene(p, true))
            .Concat(others)
            .ToArray();
    }
}