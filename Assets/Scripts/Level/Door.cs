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
//
// Forward doors (to the next, harder room) start LOCKED until every enemy in
// the room is dead. Back doors are always open so the player can still escape
// the big ship. The door shows its state with color, a label and chevrons.
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    public DoorSide side;
    public string targetScene;
    public string targetSpawn;

    [Header("Lock")]
    [Tooltip("Door stays locked until the room is cleared of enemies.")]
    public bool requiresClear;
    [Tooltip("Solid collider that blocks the doorway while locked.")]
    public Collider2D barrier;

    [Header("Visuals")]
    public SpriteRenderer fill;
    public SpriteRenderer[] frame;
    public SpriteRenderer[] chevrons;
    public TextMesh label;
    public string openText = "NEXT";
    public Color openColor = new Color(0.25f, 1f, 0.45f);
    public Color lockedColor = new Color(1f, 0.25f, 0.25f);

    [Header("Audio (optional)")]
    public AudioClip unlockSound;

    Room room;
    bool wasLocked;
    float unlockFlash;

    public bool Locked => requiresClear && room != null && !room.IsCleared;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        room = GetComponentInParent<Room>();
        wasLocked = Locked;
        Refresh(0f);
    }

    void Update()
    {
        bool locked = Locked;
        if (wasLocked && !locked)
        {
            if (unlockSound != null)
                AudioSource.PlayClipAtPoint(unlockSound, transform.position);
            unlockFlash = 1f;
        }
        wasLocked = locked;

        unlockFlash = Mathf.MoveTowards(unlockFlash, 0f, Time.unscaledDeltaTime * 1.5f);
        Refresh(Time.unscaledTime);
    }

    void Refresh(float t)
    {
        bool locked = Locked;
        if (barrier != null) barrier.enabled = locked;

        Color baseColor = locked ? lockedColor : openColor;

        // Open doors pulse; locked doors sit still. A white flash plays
        // the moment a door unlocks.
        float pulse = locked ? 0.55f : 0.6f + 0.4f * Mathf.Sin(t * 5f);
        Color c = Color.Lerp(baseColor, Color.white, unlockFlash);

        if (fill != null) fill.color = new Color(c.r, c.g, c.b, locked ? 0.85f : pulse);
        if (frame != null)
            foreach (var f in frame)
                if (f != null) f.color = c;

        // Chevrons light up one after another (innermost first), pointing out
        // through the door. Each chevron is two bars, listed in pairs.
        if (chevrons != null)
        {
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] == null) continue;
                chevrons[i].enabled = !locked;
                int step = i / 2;
                float phase = Mathf.Repeat(t * 2f - step * 0.33f, 1f);
                float a = Mathf.Lerp(0.15f, 1f, 1f - phase);
                chevrons[i].color = new Color(c.r, c.g, c.b, a);
            }
        }

        if (label != null)
        {
            label.text = locked ? "LOCKED\nclear the room" : openText;
            label.color = c;
        }
    }

    // Stay (not Enter) so a player already standing in the doorway when it
    // unlocks still goes through.
    void OnTriggerStay2D(Collider2D other)
    {
        if (Locked || RoomTransition.IsBusy) return;

        // The player is the object tagged "Player" (the builder sets this).
        var body = other.attachedRigidbody;
        if (body == null || !body.CompareTag("Player")) return;

        RoomTransition.Go(targetScene, targetSpawn);
    }
}