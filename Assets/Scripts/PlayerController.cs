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

    // Optional visual/audio feedback
    public GameObject muzzleFlashPrefab;
    public AudioClip shootSound;

    private Rigidbody2D rb;
    private float moveInput;
    private float turnInput;
    private float nextShootTime;
    private int currentHealth;

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

        // Only the owning client should control this tank
        if (!IsOwner)
        {
            // Disable any local camera or audio listener on non‑owner instances
            Camera cam = GetComponentInChildren<Camera>();
            if (cam) cam.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!IsOwner) return;
        if (!GameState.IsGameRunning) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Movement input
        moveInput = keyboard.wKey.isPressed ? 1 : (keyboard.sKey.isPressed ? -1 : 0);
        turnInput = keyboard.aKey.isPressed ? 1 : (keyboard.dKey.isPressed ? -1 : 0);

        // Shooting – local feedback + server request
        if (keyboard.spaceKey.wasPressedThisFrame && Time.time >= nextShootTime)
        {
            // Local visual/audio (only seen/heard by this client)
            if (muzzleFlashPrefab != null)
            {
                GameObject flash = Instantiate(muzzleFlashPrefab, shootPoint.position, shootPoint.rotation);
                Destroy(flash, 0.1f);
            }
            if (shootSound != null)
                AudioSource.PlayClipAtPoint(shootSound, shootPoint.position);

            // Ask the server to spawn the bullet
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
            Bullet bulletScript = bullet.GetComponent<Bullet>();
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