using Unity.VisualScripting;
using UnityEngine;

public class enemyShoot : MonoBehaviour
{
    public float minCooldown = 2f;
    public float maxCooldown = 6f;
    public GameObject bulletPrefab;
    public AudioSource audioSource;
    public AudioClip shootSound;
    
    public float dirVariance = 0.1f;

    Transform player;
    float shootCooldown;
    float cooldownTimer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        shootCooldown = Random.Range(minCooldown, maxCooldown);
        player = FindAnyObjectByType<playerMovement>().transform;
    }

    // Update is called once per frame
    void Update()
    {
        //timer code
        cooldownTimer += Time.deltaTime;
        if (cooldownTimer > shootCooldown)
        {
            cooldownTimer = 0f;
            shoot();
        }
        //face player
        transform.up = transform.position - player.position;
    }

    void shoot()
    {
        //------------------------find shoot angle------------------------------
        Vector2 shootDir;

        Vector2 rawDir = player.position - transform.position;

        //dot between vector pointing towards player and universal up
        float dot = Vector2.Dot(Vector2.Normalize(rawDir), Vector2.up);

        dot += Random.Range(-dirVariance, dirVariance);

        //choose direction, ignoring if player is to the left or right
        if (dot > 0.875)
        {
            shootDir = new Vector2(0, 1);
        }
        else if (dot > 0.375)
        {
            shootDir = new Vector2(1, 1);
        }
        else if (dot > -0.375)
        {
            shootDir = new Vector2(1, 0);
        }
        else if (dot > -0.875)
        {
            shootDir = new Vector2(1, -1);
        }
        else
        {
            shootDir = new Vector2(0, -1);
        }

        //add left or right
        if (rawDir.x >  0)
        {
            shootDir.x *= -1;
        }


        //----------------------------actually fire--------------------------------
        audioSource.PlayOneShot(shootSound);
        float angle = Mathf.Atan2(shootDir.x, shootDir.y) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0, 0, angle);

        Instantiate(bulletPrefab, transform.position, rotation);
    }
}
