using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class BigSpaceShipAI : MonoBehaviour
{
    public float spawnDelay = 5f;
    public float speed = 5; // Add in a speed modify that increases the speed of the ship over time
    public int damage = 1;
    public Transform playerSpawnPoint;
    public Transform player;
    NavMeshAgent agent;
    SpriteRenderer spriteRenderer;
    private bool chase = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = speed; // Adjust this
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;
        // Find the player object in the scene if not assigned
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }
        StartCoroutine(SpawnShip());
    }

    //Spawn ship after 5 seconds
    IEnumerator SpawnShip()
    {
        yield return new WaitForSeconds(spawnDelay);
        spriteRenderer.enabled = true;
        // supposed to spawn the ship at the spawn point, can't figure out how to figure the spawn points
        transform.position = playerSpawnPoint.position;
        chase = true;
    }

    void Update()
    {
        if (!chase || player == null)
        {
            return;
        }
        agent.SetDestination(player.position);
        // rotate ship in direction of movement
        Vector2 direction = agent.velocity.normalized;
        if (direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle + 90);
        }
    }

    void OnTriggerEnter2D(Collider2D hitInfo)
    {
        Health healthComponent = hitInfo.gameObject.GetComponent<Health>();
        if (healthComponent)
        {
            healthComponent.ApplyDamage(damage);
        }
    }
}
