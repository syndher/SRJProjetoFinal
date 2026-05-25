using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    private float _speed = 10f;
    private int _damage = 20;
    public int Damage => _damage;
    [SerializeField] private PhysicsMaterial2D _bouncyMaterial;
    private float _lifetime = 3f;
    private int _maxBounces = 2;
    private int _bounceCount = 0;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;   // needed for physics bounce
        rb.gravityScale = 0;
        rb.freezeRotation = true;
        rb.linearVelocity = transform.up * _speed;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null) circle = gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = false;
        circle.radius = 0.2f;
        circle.sharedMaterial = _bouncyMaterial; // assign here too

        Destroy(gameObject, _lifetime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServer) return;

        // Hit player
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(_damage);
            GetComponent<NetworkObject>().Despawn();
            return;
        }

        // Hit wall
        if (collision.gameObject.GetComponent<Walls>() != null)
        {
            _bounceCount++;
            if (_bounceCount >= _maxBounces)
                GetComponent<NetworkObject>().Despawn();
        }
    }
}