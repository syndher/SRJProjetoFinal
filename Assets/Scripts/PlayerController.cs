using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 100f;

    [Header("Combat")]
    public float shootCooldown = 0.5f;
    public int maxHealth = 100;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public Transform shootPoint;

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

        // Force a non‑trigger collider on the player
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        if (shootPoint == null)
            shootPoint = transform;
        else
            shootPoint.localRotation = Quaternion.identity;
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
            turnInput = 1;
        else if (keyboard.dKey.isPressed)
            turnInput = -1;
        else
            turnInput = 0;

        // Shooting: Space
        if (keyboard.spaceKey.wasPressedThisFrame && Time.time >= nextShootTime)
        {
            Shoot();
            nextShootTime = Time.time + shootCooldown;
        }

        // Stop movement if no input
        if (turnInput == 0 && moveInput == 0)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
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
            bulletScript.SetOwner(gameObject);
    }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Player collided with {other.gameObject.name}");
        if (other.GetComponent<Bullet>() != null)
        {
            Bullet bullet = other.GetComponent<Bullet>();
            if (bullet != null)
            {
                TakeDamage(20);
                Destroy(other.gameObject);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"Player took {damage} damage! Health: {currentHealth}");

        if (currentHealth <= 0)
            Die();
    }
    void Die()
    {
        Debug.Log("Player died!");
        Destroy(gameObject);
    }
}