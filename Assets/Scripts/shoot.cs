using UnityEngine;
using UnityEngine.InputSystem;

public class shoot : MonoBehaviour
{

    public Transform firePoint;
    [SerializeField] private float shootCooldown = 2f;
    private float nextShootTime = 0f;
    public GameObject bulletPrefab;
    public AudioSource audioSource;
    public AudioClip shootSound;

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    void Shoot()
    {
        if (Time.time >= nextShootTime)
        {
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
            audioSource.PlayOneShot(shootSound);
            nextShootTime = Time.time + shootCooldown;
        }
    }
}
