using System.Collections.Generic;
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
// Look:
//   Room NOT cleared - door is dark blue, no arrows.
//   Room cleared     - door turns green and pulses, small arrows on the floor
//                      light up one after another pointing out through it.
// Doors with requiresClear also physically block the player until cleared.
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    public DoorSide side;
    public string targetScene;
    public string targetSpawn;

    [Header("Lock")]
    [Tooltip("Door blocks the player until the room is cleared of enemies.")]
    public bool requiresClear;
    [Tooltip("Solid collider that blocks the doorway while locked.")]
    public Collider2D barrier;

    [Header("Visuals")]
    public SpriteRenderer fill;
    public SpriteRenderer[] frame;
    public SpriteRenderer[] chevrons;
    public TextMesh label; // not used anymore; hidden if present
    public string openText = "NEXT";
    public Color openColor = new Color(0.25f, 1f, 0.45f);          // green, room cleared
    public Color notClearedColor = new Color(0.08f, 0.15f, 0.45f); // dark blue, room not cleared
    [HideInInspector] public Color lockedColor;                    // old setting, not used

    [Header("Audio (optional)")]
    public AudioClip unlockSound;

    Room room;
    bool wasCleared;
    float unlockFlash;

    bool RoomCleared => room == null || room.IsCleared;
    public bool Locked => requiresClear && !RoomCleared;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        room = GetComponentInParent<Room>();
        EnsureVisuals();
        if (label != null) label.gameObject.SetActive(false);
        wasCleared = RoomCleared;
        Refresh(0f);
    }

    // Maps built before the door rework have no arrows set up, so make them.
    void EnsureVisuals()
    {
        if (fill == null) fill = GetComponent<SpriteRenderer>();
        if (chevrons != null && chevrons.Length > 0) return;
        if (fill == null || fill.sprite == null) return;

        Vector2 outward = side.Outward();
        Vector3 pos = transform.position;

        // Three small "^" arrows on the floor, pointing out through the door
        const float arm = 0.5f, thick = 0.12f;
        float o = arm * 0.5f * 0.7071f;
        var bars = new List<SpriteRenderer>();
        foreach (float inset in new[] { 1.9f, 1.35f, 0.8f })
        {
            var chev = new GameObject("Chevron").transform;
            chev.SetParent(transform.parent, false);
            chev.position = pos - (Vector3)(outward * inset);
            chev.rotation = Quaternion.LookRotation(Vector3.forward, outward);
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                var bar = new GameObject("Bar");
                bar.transform.SetParent(chev, false);
                bar.transform.localPosition = new Vector3(sign * o, -o, 0f);
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, -45f * sign);
                bar.transform.localScale = new Vector3(arm, thick, 1f);
                var sr = bar.AddComponent<SpriteRenderer>();
                sr.sprite = fill.sprite;
                sr.sortingOrder = -4;
                bars.Add(sr);
            }
        }
        chevrons = bars.ToArray();
    }

    void Update()
    {
        bool cleared = RoomCleared;
        if (!wasCleared && cleared)
        {
            if (unlockSound != null)
                AudioSource.PlayClipAtPoint(unlockSound, transform.position);
            unlockFlash = 1f;
        }
        wasCleared = cleared;

        unlockFlash = Mathf.MoveTowards(unlockFlash, 0f, Time.unscaledDeltaTime * 1.5f);
        Refresh(Time.unscaledTime);
    }

    void Refresh(float t)
    {
        bool cleared = RoomCleared;
        if (barrier != null) barrier.enabled = Locked;

        // Dark blue and still until cleared; then green, pulsing, with a white
        // flash the moment it opens.
        Color baseColor = cleared ? openColor : notClearedColor;
        Color c = Color.Lerp(baseColor, Color.white, unlockFlash);
        float alpha = cleared ? 0.6f + 0.4f * Mathf.Sin(t * 5f) : 1f;

        if (fill != null) fill.color = new Color(c.r, c.g, c.b, alpha);
        if (frame != null)
            foreach (var f in frame)
                if (f != null) f.color = c;

        // Arrows only show once the room is cleared. They light up one after
        // another (innermost first). Each arrow is two bars, listed in pairs.
        if (chevrons != null)
        {
            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] == null) continue;
                chevrons[i].enabled = cleared;
                int step = i / 2;
                float phase = Mathf.Repeat(t * 2f - step * 0.33f, 1f);
                float a = Mathf.Lerp(0.15f, 1f, 1f - phase);
                chevrons[i].color = new Color(c.r, c.g, c.b, a);
            }
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