using UnityEngine;
using UnityEngine.Events;

public class Damageable : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Morte por Queda")]
    [Tooltip("Se o objeto cair abaixo desse valor no eixo Y, morre automaticamente")]
    [SerializeField] private float fallDeathY = -50f;
    [SerializeField] private bool checkFallDeath = true;

    [Header("Referências")]
    [SerializeField] private HealthBarUI healthBarUI;

    [Header("Eventos")]
    public UnityEvent onDamaged;
    public UnityEvent onDeath;
    public UnityEvent onRevive;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    // Bool explícito de morte, setado em Die() e limpo em ResetForSpawn()
    public bool IsDead { get; private set; }

    private void Awake()
    {
        currentHealth = maxHealth;
        IsDead = false;
    }

    private void Start()
    {
        if (healthBarUI != null)
        {
            healthBarUI.UpdateHealthBar(currentHealth, maxHealth);
        }
    }

    private void Update()
    {
        if (!checkFallDeath || IsDead) return;

        if (transform.position.y < fallDeathY)
        {
            Die();
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (healthBarUI != null)
        {
            healthBarUI.UpdateHealthBar(currentHealth, maxHealth);
        }

        onDamaged?.Invoke();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        if (healthBarUI != null)
        {
            healthBarUI.UpdateHealthBar(currentHealth, maxHealth);
        }
    }

    private void Die()
    {
        IsDead = true;
        onDeath?.Invoke();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Chamado pelo spawner ao reaproveitar este objeto do pool.
    /// Restaura vida e limpa o estado de morte, sem precisar recriar o objeto.
    /// </summary>
    public void ResetForSpawn()
    {
        currentHealth = maxHealth;
        IsDead = false;

        if (healthBarUI != null)
        {
            healthBarUI.UpdateHealthBar(currentHealth, maxHealth);
        }

        onRevive?.Invoke();
    }
}