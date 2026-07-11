using UnityEngine;
using VolumetricLines;

/// <summary>
/// Projétil "bolt" sólido (estilo blaster de Star Wars), usando o prefab de
/// laser volumétrico (VolumetricLineBehavior) como visual.
///
/// Diferente do ShotBehavior original do asset, esse script:
/// - NÃO estica a linha com a distância percorrida — o comprimento do bolt
///   é fixo (definido pelo StartPos/EndPos já configurados no prefab), ele
///   só translada no espaço, como um projétil sólido.
/// - Não usa o Input Manager legado (o asset original tinha isso no
///   CannonBehavior/ShotBehavior, incompatível com o novo Input System).
/// - Faz um raycast a cada frame (em vez de depender só de colisão física)
///   pra não atravessar objetos finos em alta velocidade (tunneling).
/// - Aplica dano via IDamageable/Damageable e dispara efeito de impacto.
/// </summary>
[RequireComponent(typeof(VolumetricLineBehavior))]
public class LaserBoltProjectile : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float speed = 40f;
    [SerializeField] private float lifetime = 0.6f;

    [Header("Colisão")]
    [SerializeField] private LayerMask hitLayerMask = ~0;
    [Tooltip("Raio usado no SphereCast do bolt, pra facilitar acertar alvos pequenos/rápidos. Deixe 0 pra usar Raycast simples.")]
    [SerializeField] private float castRadius = 0.05f;

    [Header("Impacto")]
    [Tooltip("ParticleSystem de impacto já existente na cena (mesmo padrão do WeaponShooting original). Pode deixar vazio.")]
    [SerializeField] private ParticleSystem impactParticles;
    [SerializeField] private int impactParticleCount = 10;

    private float damage;
    private VolumetricLineBehavior lineBehavior;
    private bool hasHit;

    private void Awake()
    {
        lineBehavior = GetComponent<VolumetricLineBehavior>();
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Configura o bolt no momento do disparo (chamado por quem instancia, ex: WeaponShootingLaserBolt).
    /// </summary>
    public void Initialize(float shotDamage, Color color, LayerMask layerMask, float boltSpeed, float boltLifetime, ParticleSystem impactFx = null)
    {
        damage = shotDamage;
        hitLayerMask = layerMask;
        speed = boltSpeed;
        lifetime = boltLifetime;

        if (impactFx != null)
        {
            impactParticles = impactFx;
        }

        if (lineBehavior != null)
        {
            lineBehavior.LineColor = color;
        }
    }

    private void Update()
    {
        if (hasHit) return;

        float step = speed * Time.deltaTime;

        bool didHit = castRadius > 0f
            ? Physics.SphereCast(transform.position, castRadius, transform.forward, out RaycastHit hit, step, hitLayerMask, QueryTriggerInteraction.Ignore)
            : Physics.Raycast(transform.position, transform.forward, out hit, step, hitLayerMask, QueryTriggerInteraction.Ignore);

        if (didHit)
        {
            HandleHit(hit);
            return;
        }

        // Translada o bolt inteiro pra frente — o comprimento/forma da linha
        // (StartPos -> EndPos definidos no prefab) permanece o mesmo, então
        // ele não "estica": é um segmento fixo viajando pelo espaço.
        transform.position += transform.forward * step;
    }

    private void HandleHit(RaycastHit hit)
    {
        hasHit = true;

        if (impactParticles != null)
        {
            impactParticles.transform.position = hit.point;
            impactParticles.Emit(impactParticleCount);
        }

        Damageable damageable = hit.collider.GetComponentInParent<Damageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}