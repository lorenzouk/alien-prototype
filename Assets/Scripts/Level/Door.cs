using UnityEngine;

public enum DoorSide { North, East, South, West }

public static class DoorSideExtensions
{
    // Direction pointing out of the room through this side.
    public static Vector2 Outward(this DoorSide side)
    {
        switch (side)
        {
            case DoorSide.North: return Vector2.up;
            case DoorSide.East: return Vector2.right;
            case DoorSide.South: return Vector2.down;
            default: return Vector2.left;
        }
    }

    public static DoorSide Opposite(this DoorSide side)
    {
        switch (side)
        {
            case DoorSide.North: return DoorSide.South;
            case DoorSide.East: return DoorSide.West;
            case DoorSide.South: return DoorSide.North;
            default: return DoorSide.East;
        }
    }
}

// Doorway trigger. When the player walks into it, load the target map and
// place the player at the spawn point with id == targetSpawn.
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    public DoorSide side;
    public string targetScene;
    public string targetSpawn;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (RoomTransition.IsBusy) return;

        // The player is the object tagged "Player" (the builder sets this).
        var body = other.attachedRigidbody;
        if (body == null || !body.CompareTag("Player")) return;

        RoomTransition.Go(targetScene, targetSpawn);
    }
}
