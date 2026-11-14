using UnityEngine;

public class bullet : MonoBehaviour
{
    public float speed = 20f;
    public int damage = 1;
    public Rigidbody2D rb;

    public float lifeSpan = 1;

    void Start()
    {
        rb.linearVelocity = transform.up * speed;

        //auto destroys after lifespan is over
        Destroy(gameObject, lifeSpan);
    }

    void OnTriggerEnter2D(Collider2D hitInfo)
    {
        // Add in damage logic here
        Health healthComponent = hitInfo.gameObject.GetComponent<Health>();
        if (healthComponent)
        {
            healthComponent.ApplyDamage(damage);
        }

        Destroy(gameObject);
    }
}
