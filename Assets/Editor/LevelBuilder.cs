using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Alien Prototype > Build Basic Level
//
// Builds 4 simple map scenes (Map1-Map4) with outer walls, a few inner walls,
// enemies, and doorways that switch maps. Uses the team's Player and Enemy prefabs.
//
//   [Map 4] -- [Map 3]
//      |          |
//   [Map 1] -- [Map 2]
//
// Safe to re-run: asks before overwriting the maps. SampleScene is never modified.
public static class LevelBuilder
{
    const string MapFolder = "Assets/Scenes/Maps";
    const string SquarePath = "Assets/Level/Art/Square.png";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    // Map interior is 32 x 18 units; the camera shows the whole map.
    const float HalfW = 16f, HalfH = 9f;
    const float WallThickness = 0.5f;
    const float DoorGap = 3f;
    const float SpawnInset = 2.5f;
    const float CameraSize = HalfH + 0.6f;

    static readonly Color WallColor = new Color(0.35f, 0.38f, 0.45f);
    static readonly Color InnerWallColor = new Color(0.25f, 0.55f, 0.75f);
    static readonly Color DoorColor = new Color(0.3f, 0.9f, 1f, 0.5f);

    class MapDef
    {
        public string scene;
        public string label;
        public Color floor;
        public Vector2? start; // only the first map
        public List<(DoorSide side, string target)> doors = new List<(DoorSide, string)>();
        public List<(Vector2 center, Vector2 size)> walls = new List<(Vector2, Vector2)>();
        public List<Vector2> enemies = new List<Vector2>();
    }

    // ---- Edit the maps here ---------------------------------------------------
    // Inner walls: (center, size) in world units. Map spans x -16..16, y -9..9.
    // Enemies: positions for the team's Enemy prefab (respawn each time you enter the map).
    static List<MapDef> Layout() => new List<MapDef>
    {
        new MapDef
        {
            scene = "Map1", label = "Map 1", floor = new Color(0.08f, 0.09f, 0.13f),
            start = new Vector2(-12f, -5f),
            doors = { (DoorSide.East, "Map2"), (DoorSide.North, "Map4") },
            walls =
            {
                (new Vector2(-6f, -3f), new Vector2(0.5f, 12f)),
                (new Vector2(3f, 3f), new Vector2(12f, 0.5f)),
                (new Vector2(9f, -4f), new Vector2(0.5f, 6f)),
            },
            enemies = { new Vector2(2f, -5f), new Vector2(-11f, 6f) },
        },
        new MapDef
        {
            scene = "Map2", label = "Map 2", floor = new Color(0.10f, 0.08f, 0.13f),
            doors = { (DoorSide.West, "Map1"), (DoorSide.North, "Map3") },
            walls =
            {
                (new Vector2(-9f, 3f), new Vector2(0.5f, 12f)),
                (new Vector2(0f, -3f), new Vector2(12f, 0.5f)),
                (new Vector2(6f, 4f), new Vector2(0.5f, 10f)),
                (new Vector2(11f, -5f), new Vector2(6f, 0.5f)),
            },
            enemies = { new Vector2(12f, 3f), new Vector2(0f, -7f), new Vector2(-13f, -7f) },
        },
        new MapDef
        {
            scene = "Map3", label = "Map 3", floor = new Color(0.13f, 0.08f, 0.08f),
            doors = { (DoorSide.South, "Map2"), (DoorSide.West, "Map4") },
            walls =
            {
                (new Vector2(-10f, -3f), new Vector2(8f, 0.5f)),
                (new Vector2(-3f, 3f), new Vector2(0.5f, 12f)),
                (new Vector2(5f, -1f), new Vector2(0.5f, 10f)),
                (new Vector2(11f, 3f), new Vector2(8f, 0.5f)),
            },
            enemies = { new Vector2(-10f, 6f), new Vector2(1f, 4f), new Vector2(11f, 0f), new Vector2(11f, -6f) },
        },
        new MapDef
        {
            scene = "Map4", label = "Map 4", floor = new Color(0.07f, 0.12f, 0.11f),
            doors = { (DoorSide.South, "Map1"), (DoorSide.East, "Map3") },
            walls =
            {
                (new Vector2(-8f, 2f), new Vector2(0.5f, 14f)),
                (new Vector2(0f, -4f), new Vector2(10f, 0.5f)),
                (new Vector2(0f, 4f), new Vector2(10f, 0.5f)),
                (new Vector2(8f, -2f), new Vector2(0.5f, 10f)),
            },
            enemies = { new Vector2(0f, 0f), new Vector2(-12f, 6f), new Vector2(-12f, -6f), new Vector2(12f, 7f), new Vector2(4f, 7f) },
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

        var sprite = GetSquareSprite();
        EnsureFolder(MapFolder);
        foreach (var m in maps)
            BuildMap(m, sprite, player, enemy);

        AddToBuildSettings(maps);
        AssetDatabase.SaveAssets();

        EditorSceneManager.OpenScene(ScenePath(maps[0]));
        Debug.Log("[LevelBuilder] Built " + maps.Count + " maps in " + MapFolder + ".");
    }

    static string ScenePath(MapDef m) => MapFolder + "/" + m.scene + ".unity";

    static void BuildMap(MapDef def, Sprite sprite, GameObject playerPrefab, GameObject enemyPrefab)
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
        room.defaultSpawn = def.start.HasValue ? "Start" : def.doors[0].side.ToString();

        var walls = Child("Walls", root.transform);
        var doorsParent = Child("Doors", root.transform);
        var spawnsParent = Child("Spawns", root.transform);

        MakeBox("Floor", walls, Vector2.zero, new Vector2(HalfW * 2f, HalfH * 2f), def.floor, sprite, -10, false);

        // Outer walls, with a gap wherever there's a door
        foreach (DoorSide side in System.Enum.GetValues(typeof(DoorSide)))
            BuildOuterWall(walls, side, def.doors.Any(d => d.side == side), sprite);

        // Inner walls
        foreach (var (center, size) in def.walls)
            MakeBox("Wall", walls, center, size, InnerWallColor, sprite, 0, true);

        // Doors + matching spawn points
        foreach (var (side, target) in def.doors)
        {
            bool horizontal = side == DoorSide.North || side == DoorSide.South;
            float half = horizontal ? HalfH : HalfW;
            Vector2 outward = side.Outward();
            Vector2 size = horizontal ? new Vector2(DoorGap, WallThickness) : new Vector2(WallThickness, DoorGap);

            var doorGo = MakeBox("Door_" + side, doorsParent, outward * half, size, DoorColor, sprite, -5, true);
            doorGo.GetComponent<BoxCollider2D>().isTrigger = true;
            var door = doorGo.AddComponent<Door>();
            door.side = side;
            door.targetScene = target;
            door.targetSpawn = side.Opposite().ToString(); // arrive next to the matching door

            MakeSpawn(side.ToString(), spawnsParent, outward * (half - SpawnInset), -outward);
        }

        if (def.start.HasValue)
            MakeSpawn("Start", spawnsParent, def.start.Value, Vector2.up);

        // Team enemies
        if (enemyPrefab != null)
        {
            var enemiesParent = Child("Enemies", root.transform);
            foreach (var pos in def.enemies)
            {
                var e = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
                e.transform.SetParent(enemiesParent, false);
                e.transform.position = pos;
            }
        }

        // Team player (Room moves it to the right spawn at runtime)
        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.tag = "Player";
        var startSpawn = spawnsParent.GetComponentsInChildren<SpawnPoint>().First(s => s.id == room.defaultSpawn);
        player.transform.SetPositionAndRotation(startSpawn.transform.position, startSpawn.transform.rotation);

        EditorSceneManager.SaveScene(scene, ScenePath(def));
    }

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
        MakeBox("Wall_" + side, parent, center, size, WallColor, sprite, 0, true);
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
            problem = SampleScenePath + " is missing.";
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

    static void AddToBuildSettings(List<MapDef> maps)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (var m in maps)
        {
            string path = ScenePath(m);
            if (!scenes.Any(s => s.path == path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}