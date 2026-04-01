using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float bulletSpeed = 20f;
    public float lifetime = 3f;
    public int damage = 10;
    
    private Rigidbody2D rb;
    private Vector2 direction;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        if (direction != Vector2.zero)
        {
            rb.linearVelocity = direction * bulletSpeed;
        }
        
        Destroy(gameObject, lifetime);
    }
    
    public void SetDirection(Vector2 shootDirection)
    {
        direction = shootDirection;
        if (rb != null)
        {
            rb.linearVelocity = direction * bulletSpeed;
        }
    }
}