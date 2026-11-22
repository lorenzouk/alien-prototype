using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// One per map scene.
// - Moves the player to the spawn point matching the door they came through
//   (or the default spawn when you press Play in this scene).
// - Tracks the enemies under "Enemies". When they're all dead the room is
//   cleared, which unlocks its forward door. Cleared rooms stay cleared for the
//   rest of the session (even after dying), so backtracking isn't a chore.
// - Tells the big ship to come in through the door the player used.
// - Draws a small HUD: room number, enemies left, and a "room clear" banner.
[DefaultExecutionOrder(-100)]
public class Room : MonoBehaviour
{
    public string roomName = "Map";
    [Tooltip("Spawn used when the scene is played directly (no door was used).")]
    public string defaultSpawn = "Start";

    [Header("Progression")]
    public int roomNumber = 1;
    public int totalRooms = 5;
    [Tooltip("Parent of this room's enemies. Room is cleared when it has none left.")]
    public Transform enemiesRoot;
    [Tooltip("Optional sound when the room is cleared.")]
    public AudioClip clearSound;

    public bool IsCleared { get; private set; }

    static readonly HashSet<string> clearedRooms = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => clearedRooms.Clear();

    float bannerTime = -100f;
    GUIStyle bannerStyle;

    bool IsFinalRoom => roomNumber >= totalRooms;

    void Awake()
    {
         // Older maps don't have enemiesRoot set: find the "Enemies" child.
        if (enemiesRoot == null) enemiesRoot = transform.Find("Enemies");

        var target = PlacePlayer();

        // Big ship (Evil Otto) enters where the player entered.
        if (target != null)
        {
            var ship = FindAnyObjectByType<BigSpaceShipAI>();
            if (ship != null) ship.playerSpawnPoint = target.transform;
        }

        // Already cleared earlier this session: remove the enemies, doors open.
        if (clearedRooms.Contains(gameObject.scene.name))
        {
            IsCleared = true;
            if (enemiesRoot != null)
                foreach (Transform e in enemiesRoot)
                {
                    e.gameObject.SetActive(false);
                    Destroy(e.gameObject);
                }
        }
    }

    SpawnPoint PlacePlayer()
    {
        var spawns = GetComponentsInChildren<SpawnPoint>();
        SpawnPoint target =
            spawns.FirstOrDefault(s => s.id == RoomTransition.PendingSpawn) ??
            spawns.FirstOrDefault(s => s.id == defaultSpawn);

        var player = GameObject.FindWithTag("Player");
        if (player == null || target == null) return target;

        player.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = target.transform.position;
            rb.rotation = target.transform.eulerAngles.z;
            rb.linearVelocity = Vector2.zero;
        }
        return target;
    }

    int EnemiesLeft()
    {
        if (enemiesRoot == null) return 0;
        int n = 0;
        foreach (Transform e in enemiesRoot)
            if (e.gameObject.activeInHierarchy) n++;
        return n;
    }

    void Update()
    {
        // Older maps don't have enemiesRoot set: find the "Enemies" child.
        if (enemiesRoot == null) enemiesRoot = transform.Find("Enemies");
        if (enemiesRoot == null) return; // no enemies to track

        if (IsCleared || EnemiesLeft() > 0) return;

        IsCleared = true;
        clearedRooms.Add(gameObject.scene.name);
        bannerTime = Time.unscaledTime;
        if (clearSound != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(clearSound, Camera.main.transform.position);
    }
    void OnGUI()
    {
        if (bannerStyle == null)
        {
            bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        bool recent = Time.unscaledTime - bannerTime < 2.5f;
        if (IsCleared && (IsFinalRoom || recent))
        {
            string text = IsFinalRoom ? "ALL ROOMS CLEARED!" : " NEXT ROOM IS OPEN";
            bannerStyle.normal.textColor = IsFinalRoom ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.5f);
            GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 60), text, bannerStyle);
        }
    }
}