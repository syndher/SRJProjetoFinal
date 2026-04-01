using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float turnSpeed = 100f;
    public float shootCooldown = 0.5f;
    public int maxHealth = 100;
    
    private Rigidbody2D rb;
    private float moveInput;
    private float turnInput;
    private float nextShootTime;
    private int currentHealth;
    
    [SerializeField]
    private GameObject bullet;
    
    [SerializeField]
    private Transform shootPoint;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
        
        if (shootPoint == null)
            shootPoint = transform;
    }
    
    void Update()
    {
        moveInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");
        CheckForShoot();
    }
    
    void FixedUpdate()
    {
        float turn = -turnInput * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation + turn);
        
        if (moveInput != 0)
        {
            Vector2 movement = transform.up * moveInput * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);
        }   
    }
    
    void CheckForShoot()
    {
        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextShootTime)
        {
            Shoot();
            nextShootTime = Time.time + shootCooldown;
        }
    }
    
    void Shoot()
    {
        GameObject newBullet = Instantiate(bullet, shootPoint.position, shootPoint.rotation);
        Bullet bulletScript = newBullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDirection(transform.up);
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