using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 25;
    public float lifetime = 3f;

    private GameObject owner;
    private bool hasHit = false;
    private Rigidbody2D rb;
    private Vector2 lastPosition;
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

        Collider2D existing = GetComponent<Collider2D>();
        if (existing != null) Destroy(existing);
        CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 0.2f;

        Destroy(gameObject, lifetime);
        lastPosition = rb.position;
        spawnTime = Time.time;
    }

    void FixedUpdate()
    {
        lastPosition = rb.position;
    }

    void Update()
    {
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    public void SetOwner(GameObject ownerObject)
    {
        owner = ownerObject;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        if (other.GetComponent<PlayerController>() != null)
        {
            if (owner != null && other.gameObject == owner)
            {
                if (Time.time - spawnTime < 0.2f)
                    return;
                hasHit = true;
                Destroy(gameObject);
                return;
            }

            hasHit = true;
            Destroy(gameObject);
            return;
        }
        if (other.GetComponent<Bullet>() != null)
        {
            hasHit = true;
            Destroy(gameObject);
            return;
        }
        if (other.GetComponent<Walls>() != null)
        {
            hasHit = true;
            BounceOffWall(other);
        }
    }

    private void BounceOffWall(Collider2D wallCollider)
    {
        Vector2 closestPoint = wallCollider.ClosestPoint(rb.position);
        Vector2 normal = (rb.position - closestPoint).normalized;
        float pushDistance = 0.1f;
        rb.position = closestPoint + normal * pushDistance;
        Vector2 reflected = Vector2.Reflect(rb.linearVelocity, normal);
        rb.linearVelocity = reflected.normalized * speed;
        lastPosition = rb.position;
    }
}