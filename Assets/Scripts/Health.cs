using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHealth = 2;
    public AudioSource audioSource;
    public AudioClip explosion;
    public GameObject explosionParticles;
    public int scoreValue = 100;
    int currentHealth;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = maxHealth;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ApplyDamage(int damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Death();
        }
    }

    public virtual void Death()
    {
        //sfx and anims
        AudioSource.PlayClipAtPoint(explosion, transform.position);
        GameObject particles = Instantiate(explosionParticles, transform.position, Quaternion.identity);
        particles.GetComponent<ParticleSystem>().Play();
        //score
        ScoreManager.Instance.AddScore(scoreValue);
        Destroy(gameObject);
    }
}
