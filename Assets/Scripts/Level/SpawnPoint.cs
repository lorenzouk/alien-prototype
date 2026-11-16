using UnityEngine;

// Where the player appears when entering a room. Doors point at a spawn by id
// (the builder names them after the side they sit next to: "North", "West", ...).
public class SpawnPoint : MonoBehaviour
{
    public string id;

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.8f);
    }
}
