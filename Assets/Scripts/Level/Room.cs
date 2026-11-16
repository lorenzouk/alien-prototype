using System.Linq;
using UnityEngine;

// One per map scene. Moves the player to the spawn point matching the door
// they came through (or the default spawn when you press Play in this scene).
[DefaultExecutionOrder(-100)]
public class Room : MonoBehaviour
{
    public string roomName = "Map";
    [Tooltip("Spawn used when the scene is played directly (no door was used).")]
    public string defaultSpawn = "Start";

    void Awake()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;

        var spawns = GetComponentsInChildren<SpawnPoint>();
        SpawnPoint target =
            spawns.FirstOrDefault(s => s.id == RoomTransition.PendingSpawn) ??
            spawns.FirstOrDefault(s => s.id == defaultSpawn);
        if (target == null) return;

        player.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = target.transform.position;
            rb.rotation = target.transform.eulerAngles.z;
            rb.linearVelocity = Vector2.zero;
        }
    }
}
