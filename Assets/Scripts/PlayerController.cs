using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    private float moveSpeed = 5f;
    private float turnSpeed = 100f;

    [Header("Combat")]
    private float shootCooldown = 0.5f;
    private int maxHealth = 100;

    [Header("Shooting")]
    private GameObject bulletPrefab;
    private Transform shootPoint;

    private Rigidbody2D rb;
    private float moveInput;
    private float turnInput;
    private float nextShootTime;
    private int currentHealth;
    public int CurrentHealth => currentHealth;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.freezeRotation = true;

        currentHealth = maxHealth;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        if (shootPoint == null)
            shootPoint = transform;

        if (!IsOwner)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam) cam.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Movement input
        moveInput = keyboard.wKey.isPressed ? 1 : (keyboard.sKey.isPressed ? -1 : 0);
        turnInput = keyboard.aKey.isPressed ? 1 : (keyboard.dKey.isPressed ? -1 : 0);

        // Shooting
        if (keyboard.spaceKey.wasPressedThisFrame && Time.time >= nextShootTime)
        {
            // Server instantiates bullet
            ShootServerRpc(shootPoint.position, shootPoint.rotation);
            nextShootTime = Time.time + shootCooldown;
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        float turn = turnInput * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation + turn);

        if (moveInput != 0)
        {
            Vector2 movement = transform.up * moveInput * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    [ServerRpc]
    void ShootServerRpc(Vector3 position, Quaternion rotation)
    {
        if (bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab, position, rotation);
        NetworkObject netObj = bullet.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
        else
        {
            Debug.LogError("Bullet prefab missing NetworkObject!");
            Destroy(bullet);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        Bullet bullet = other.GetComponent<Bullet>();
        if (bullet != null)
        {
            TakeDamage(bullet.Damage);
            bullet.GetComponent<NetworkObject>().Despawn();
        }
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer) return;
        currentHealth -= damage;
        Debug.Log($"Player took {damage} damage. Health: {currentHealth}");
        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        if (!IsServer) return;
        GetComponent<NetworkObject>().Despawn();
        Destroy(gameObject);
    }
}