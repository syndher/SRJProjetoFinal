using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 200f;

    [Header("Combat")]
    public float shootCooldown = 0.5f;
    public int maxHealth = 100;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public Transform shootPoint;

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isAlive = new NetworkVariable<bool>(true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public static System.Action<ulong, int> OnAnyPlayerHealthChanged;
    public static System.Action<ulong> OnAnyPlayerDied;

    private Rigidbody2D rb;
    private List<Collider2D> allColliders = new List<Collider2D>();
    private List<Renderer> allRenderers = new List<Renderer>();
    
    private float moveInput;
    private float turnInput;
    private float nextShootTime;
    private bool localIsAlive = true;
    private GameMatchManager matchManager;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Dynamic;

        allColliders.Clear();
        allColliders.AddRange(GetComponentsInChildren<Collider2D>());
        if (allColliders.Count == 0)
            allColliders.Add(gameObject.AddComponent<BoxCollider2D>());
        foreach (var col in allColliders)
            col.isTrigger = false;

        allRenderers.Clear();
        allRenderers.AddRange(GetComponentsInChildren<Renderer>());
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        NetworkSetup.AllPlayers.Add(this);
        matchManager = FindFirstObjectByType<GameMatchManager>();

        if (IsClient)
        {
            currentHealth.OnValueChanged += (oldVal, newVal) =>
            {
                OnAnyPlayerHealthChanged?.Invoke(OwnerClientId, newVal);
            };
            OnAnyPlayerHealthChanged?.Invoke(OwnerClientId, currentHealth.Value);

            isAlive.OnValueChanged += (oldVal, newVal) =>
            {
                localIsAlive = newVal;
                UpdateTankState(newVal);
            };
            localIsAlive = isAlive.Value;
            UpdateTankState(isAlive.Value);
        }

        if (!IsOwner)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam) cam.gameObject.SetActive(false);
        }
    }

    private void UpdateTankState(bool alive)
    {
        foreach (var rend in allRenderers)
            rend.enabled = alive;
        foreach (var col in allColliders)
            col.enabled = alive;
        if (rb != null)
            rb.simulated = alive;
    }

    public override void OnNetworkDespawn()
    {
        NetworkSetup.AllPlayers.Remove(this);
        base.OnNetworkDespawn();
    }

    private bool IsGameReady()
    {
        if (matchManager == null) return false;
        return matchManager.gameReady.Value;
    }

    void Update()
    {
        if (!IsOwner || !localIsAlive || !IsGameReady()) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        moveInput = keyboard.wKey.isPressed ? 1 : (keyboard.sKey.isPressed ? -1 : 0);
        turnInput = keyboard.aKey.isPressed ? 1 : (keyboard.dKey.isPressed ? -1 : 0);

        if (keyboard.spaceKey.wasPressedThisFrame && Time.time >= nextShootTime)
        {
            ShootServerRpc(shootPoint.position, shootPoint.rotation);
            nextShootTime = Time.time + shootCooldown;
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner || !localIsAlive || !IsGameReady()) return;

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
        if (bulletPrefab == null || !isAlive.Value) return;
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
        if (!IsServer || !isAlive.Value) return;
        Bullet bullet = other.GetComponent<Bullet>();
        if (bullet != null)
        {
            TakeDamage(bullet.Damage);
            // The bullet will be despawned by its own collision handler, not here.
        }
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer || !isAlive.Value) return;
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        if (currentHealth.Value <= 0)
            Die();
    }

    void Die()
    {
        if (!IsServer || !isAlive.Value) return;
        isAlive.Value = false;
        OnAnyPlayerDied?.Invoke(OwnerClientId);
    }

    public void ServerRespawn(Vector3 position, Quaternion rotation)
    {
        if (!IsServer) return;
        transform.position = position;
        transform.rotation = rotation;
        currentHealth.Value = maxHealth;
        rb.linearVelocity = Vector2.zero;
        isAlive.Value = true;
        RespawnClientRpc(position, rotation);
    }

    [ClientRpc]
    private void RespawnClientRpc(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
        UpdateTankState(true);
        localIsAlive = true;
        if (!IsServer)
            OnAnyPlayerHealthChanged?.Invoke(OwnerClientId, maxHealth);
    }
}