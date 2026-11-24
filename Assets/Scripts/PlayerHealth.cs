using UnityEngine;

public class PlayerHealth : Health
{
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 0.35f;
    bool isDying;

    //override death func
    public override void Death()
    {
        if (isDying)
        {
            return;
        }
        isDying = true;

        if (explosion != null)
        {
            GameObject soundObject = new GameObject("Player Death Sound");
            soundObject.transform.position = transform.position;
            AudioSource deathAudioSource = soundObject.AddComponent<AudioSource>();
            deathAudioSource.spatialBlend = 0f;
            deathAudioSource.PlayOneShot(explosion, explosionVolume);
            Destroy(soundObject, explosion.length);
        }
        else
        {
            Debug.LogError("Player explosion failed.", this);
        }

        GameObject particles = Instantiate(explosionParticles, transform.position, Quaternion.identity);
        particles.transform.SetParent(null);
        if (particles.TryGetComponent(out ParticleSystem particleSystem))
        {
            particleSystem.Play();
        }
        ScoreManager.Instance.HandlePlayerDeathAfterDelay(3f);
        Destroy(gameObject);
    }
}
