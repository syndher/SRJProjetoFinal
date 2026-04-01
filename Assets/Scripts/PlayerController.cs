using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 100f;

    [Header("Combat")]
    public float shootCooldown = 0.5f;
    public int maxHealth = 100;

    [Header("Shooting")]
    public GameObject bulletPrefab;   // assign in inspector
    public Transform shootPoint;      // child object for spawn position

    private Rigidbody2D rb;
    private float moveInput;
    private float turnInput;
    private float nextShootTime;
    private int currentHealth;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;
        }

        currentHealth = maxHealth;

        if (shootPoint == null)
        {
            shootPoint = transform;
        }
        else
        {
            // Force the shootPoint's local rotation to identity
            shootPoint.localRotation = Quaternion.identity;
        }

        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
        }
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Movement: W / S
        if (keyboard.wKey.isPressed)
            moveInput = 1;
        else if (keyboard.sKey.isPressed)
            moveInput = -1;
        else
            moveInput = 0;

        // Turning: A / D
        if (keyboard.aKey.isPressed)
            turnInput = -1;
        else if (keyboard.dKey.isPressed)
            turnInput = 1;
        else
            turnInput = 0;

        // Shooting: Space
        if (keyboard.spaceKey.wasPressedThisFrame && Time.time >= nextShootTime)
        {
            Shoot();
            nextShootTime = Time.time + shootCooldown;
        }
    }

    void FixedUpdate()
    {
        float turn = turnInput * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation + turn);

        if (moveInput != 0)
        {
            Vector2 movement = transform.up * moveInput * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("Bullet prefab not assigned!");
            return;
        }

        Vector3 spawnPos = shootPoint.position;
        GameObject newBullet = Instantiate(bulletPrefab, spawnPos, transform.rotation);
        Bullet bulletScript = newBullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetOwner(gameObject);
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"Player took {damage} damage! Health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Player died!");
        Destroy(gameObject);
    }
}