using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 25;
    public float lifetime = 3f;

    private GameObject owner;
    private bool hasHit = false;

    void Start()
    {
        Destroy(gameObject, lifetime);

        Collider2D collider = GetComponent<Collider2D>();
        if (collider == null)
        {
            CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.isTrigger = true;
        }
        else
        {
            collider.isTrigger = true;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();
        
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0;
        rb.freezeRotation = true;
    }

    void Update()
    {
        transform.Translate(Vector3.up * speed * Time.deltaTime);
    }

    public void SetOwner(GameObject ownerObject)
    {
        owner = ownerObject;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            if (owner != null && other.gameObject == owner)
                return;

            hasHit = true;
            player.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}