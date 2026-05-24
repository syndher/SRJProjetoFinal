using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    public float speed = 10f;
    public int damage = 25;
    public float lifetime = 3f;
    public float gracePeriod = 0.2f;   // seconds before hitting its owner

    public GameObject Owner { get; private set; }

    private Rigidbody2D rb;
    private float spawnTime;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0;
        rb.freezeRotation = true;
        rb.linearVelocity = transform.up * speed;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
        {
            circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.2f;
        }

        spawnTime = Time.time;
        Destroy(gameObject, lifetime);
    }

    public void SetOwner(GameObject owner)
    {
        Owner = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        // Grace period: avoid hitting the owner immediately after spawn
        if (Owner != null && other.gameObject == Owner && Time.time - spawnTime < gracePeriod)
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            // Damage will be applied by the player's OnTriggerEnter2D
            // The bullet will be despawned there.
            return;
        }

        // Hit something else (wall, etc.) – destroy bullet
        GetComponent<NetworkObject>().Despawn();
    }
}