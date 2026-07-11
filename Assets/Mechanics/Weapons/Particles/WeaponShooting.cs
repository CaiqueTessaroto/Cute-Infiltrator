using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sistema de tiro "energia/laser" cyberpunk em 3D, usando apenas o próprio
/// ParticleSystem como projétil (sem Rigidbody, sem prefab de bala).
/// Cada disparo emite UMA partícula com velocidade e cor definidas na hora,
/// e a colisão da partícula gera o efeito de impacto.
///
/// Versão adaptada para primeira pessoa (usada com FirstPersonController):
/// - A mira não usa mais raycast a partir da posição do mouse na tela.
///   O olhar (câmera) já é controlado pelo FirstPersonController via mouse look,
///   então aqui a arma sempre mira na direção que a câmera já está olhando
///   (equivalente a um crosshair fixo no centro da tela).
/// - Faz um raycast a partir da câmera (playerCamera.forward) pra achar o ponto
///   exato mirado; assim o tiro acerta o que está sob a mira mesmo que o
///   "firePoint" (cano da arma) esteja deslocado do centro da tela.
/// </summary>
public class WeaponShooting : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private InputActionReference fireAction;

    [Header("Atributos da Arma")]
    [SerializeField] float shotDamage = 10f;
    [SerializeField] private ParticleCollisionForwarder collisionForwarder;

    [Header("Áudio")]
    [SerializeField] private WeaponAudioManager weaponAudio;
    [SerializeField] private AudioClip shotSound;
    [SerializeField] private AudioClip impactSound;
    [Tooltip("Variação aleatória de pitch pra cada tiro não soar idêntico")]
    [SerializeField] private Vector2 shotPitchRange = new Vector2(0.95f, 1.05f);

    [Header("Referências")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private ParticleSystem muzzleFlash;

    [Header("Sistema de Partículas do Tiro")]
    [Tooltip("ParticleSystem configurado com Emission = 0 (só emite via script), Simulation Space = World, Collision habilitada")]
    [SerializeField] private ParticleSystem shotParticles;
    [SerializeField] private float shotSpeed = 40f;
    [SerializeField] private float shotLifetime = 0.6f;
    [SerializeField] private float fireRate = 0.08f;
    [SerializeField] private bool autoFire = true;

    [Header("Mira 3D (FPS)")]
    [Tooltip("Câmera do jogador (a mesma controlada pelo FirstPersonController). O tiro sempre mira pro centro da tela, como numa crosshair.")]
    [SerializeField] private Transform playerCamera;
    [Tooltip("Camadas contra as quais o raycast de mira pode colidir (chão, inimigos, cenário, etc.)")]
    [SerializeField] private LayerMask aimLayerMask = ~0;
    [Tooltip("Distância máxima do raycast de mira e distância de fallback caso o raycast não acerte nada")]
    [SerializeField] private float maxAimDistance = 100f;
    [Tooltip("Se true, este transform (modelo da arma) gira pra apontar exatamente pro ponto mirado. Deixe false se a arma já é filha da câmera e não precisa girar sozinha.")]
    [SerializeField] private bool rotateWeaponTowardsAim = false;

    [Header("Visual Cyberpunk")]
    [Tooltip("Cores neon alternadas a cada disparo")]
    [SerializeField]
    private Color[] neonColors = new Color[]
        {
        new Color(0f, 1f, 1f),   // ciano
        new Color(1f, 0f, 1f),   // magenta
        new Color(0.6f, 0f, 1f)  // roxo elétrico
        };

    [Header("Impacto")]
    [SerializeField] private ParticleSystem impactParticles;

    private float nextFireTime;
    private ParticleSystem.EmitParams emitParams;
    private List<ParticleCollisionEvent> collisionEvents;

    private void Awake()
    {
        emitParams = new ParticleSystem.EmitParams();
        collisionEvents = new List<ParticleCollisionEvent>();
    }

    private void OnEnable()
    {
        if (fireAction != null) fireAction.action.Enable();

        if (collisionForwarder != null)
            collisionForwarder.OnCollision += HandleParticleCollision;
    }

    private void OnDisable()
    {
        if (fireAction != null) fireAction.action.Disable();

        if (collisionForwarder != null)
            collisionForwarder.OnCollision -= HandleParticleCollision;
    }

    private bool isWeaponSoundPlaying = false;

    private void Update()
    {
        if (rotateWeaponTowardsAim)
        {
            AimTowardsCrosshair();
        }

        bool isPressed = fireAction.action.IsPressed();

        bool wantsToShoot = autoFire
            ? isPressed
            : fireAction.action.WasPressedThisFrame();

        if (wantsToShoot && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;

            isWeaponSoundPlaying = true;

            if (weaponAudio != null)
            {
                weaponAudio.ManageWeaponSound(shotSound, isPressed, loopMode: true, minDuration: 0.15f);
                weaponAudio.StopWeaponSound(); //remover quando achar um som melhor /----
            }

        }
        else if (isWeaponSoundPlaying == true && !isPressed)
        {
            isWeaponSoundPlaying = false;

            weaponAudio?.StopWeaponSound();
        }

    }

    /// <summary>
    /// Acha o ponto que a câmera está mirando (centro da tela / crosshair).
    /// Se o raio não acertar nada no aimLayerMask, usa um ponto de fallback
    /// a "maxAimDistance" na direção da câmera (pra sempre ter um alvo válido,
    /// ex: mirar pro vazio/céu ainda precisa de uma direção de tiro).
    /// </summary>
    private Vector3 GetAimPoint()
    {
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimLayerMask))
        {
            return hit.point;
        }

        return ray.origin + ray.direction * maxAimDistance;
    }

    /// <summary>
    /// Gira o modelo da arma (este transform) pra apontar exatamente pro ponto mirado
    /// pela câmera. Só é necessário se a arma NÃO for filha direta da câmera
    /// (senão ela já acompanha o olhar automaticamente pela hierarquia).
    /// </summary>
    private void AimTowardsCrosshair()
    {
        Vector3 aimPoint = GetAimPoint();
        Vector3 direction = aimPoint - transform.position;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void Shoot()
    {
        Vector3 aimPoint = GetAimPoint();
        Vector3 direction = (aimPoint - firePoint.position).normalized;

        emitParams.position = firePoint.position;
        // IMPORTANTE: sem isso, a Unity ainda aplica o offset do Shape Module
        // (cone/esfera/etc.) em cima do emitParams.position, usando a rotação
        // do próprio GameObject do shotParticles — é isso que fazia o tiro
        // nascer deslocado (acima/atrás) do firePoint. Com "false", a partícula
        // nasce exatamente na posição que passamos, sem esse offset extra.
        emitParams.applyShapeToPosition = false;
        emitParams.velocity = direction * shotSpeed;
        emitParams.startLifetime = shotLifetime;
        emitParams.startColor = neonColors[Random.Range(0, neonColors.Length)];
        // Rotaciona a partícula na direção do tiro (3 eixos, útil se o shape dela for alongado, tipo um "traço" de laser)
        emitParams.rotation3D = Quaternion.LookRotation(direction).eulerAngles;

        shotParticles.Emit(emitParams, 1);

        if (muzzleFlash != null)
        {
            muzzleFlash.transform.rotation = Quaternion.LookRotation(direction);
            muzzleFlash.Play();
        }

        if (AudioManager.Instance != null && shotSound != null)
        {
            float pitch = Random.Range(shotPitchRange.x, shotPitchRange.y);

            //bool isPressed = fireAction.action.IsPressed();
            //weaponAudio.ManageWeaponSound(shotSound, isPressed, loopMode: true, minDuration: 0.15f);

            //AudioManager.Instance.PlaySFX(laserSound, pitch);
        }
    }

    // Chamado automaticamente pela Unity quando o ParticleSystem "shotParticles"
    // colide com algo (precisa da Collision Module habilitada + Send Collision Messages = true)
    private void HandleParticleCollision(GameObject other)
    {
        //Debug.Log($"WeaponShooting: colisão com {other.name}");

        if (impactParticles != null)
        {
            int numEvents = shotParticles.GetCollisionEvents(other, collisionEvents);
            for (int i = 0; i < numEvents; i++)
            {
                Vector3 impactPos = collisionEvents[i].intersection;
                impactParticles.transform.position = impactPos;
                impactParticles.Emit(10);
            }
        }

        Damageable damageable = other.GetComponentInParent<Damageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(shotDamage);
        }

        if (AudioManager.Instance != null && impactSound != null)
        {
            AudioManager.Instance.PlaySFX(impactSound);
        }

    }

}