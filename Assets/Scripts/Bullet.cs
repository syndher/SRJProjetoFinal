using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private int damage = 20;
    [SerializeField] private int maxBounces = 4;
    [SerializeField] private float lifetime = 5f;

    public int Damage => damage;

    private Rigidbody2D rb;
    private int bounceCount = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Dynamic;

        // Ensure there's a collider
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = false;
    }

    void Start()
    {
        // Set initial velocity on all instances
        rb.linearVelocity = transform.up * speed;

        // Auto‑destroy after lifetime
        if (IsServer)
            Invoke(nameof(DespawnBullet), lifetime);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Only server handles damage and despawn logic
        if (!IsServer) return;

        // Hit a player
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage);
            DespawnBullet();
            return;
        }

        // Hit a wall
        if (collision.gameObject.GetComponent<Walls>() != null)
        {
            bounceCount++;
            if (bounceCount >= maxBounces)
                DespawnBullet();
        }
    }

    private void DespawnBullet()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }
}