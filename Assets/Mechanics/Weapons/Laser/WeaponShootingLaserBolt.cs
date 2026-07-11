using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sistema de tiro "energia/laser" cyberpunk em 3D, agora usando o prefab de
/// laser volumétrico (VolumetricLineBehavior + LaserBoltProjectile) como
/// projétil, em vez de uma partícula do ParticleSystem.
///
/// Igual ao WeaponShooting original, a mira não usa raycast a partir do
/// mouse: a arma sempre mira pro centro da tela (equivalente ao olhar da
/// câmera em primeira pessoa), e um raycast a partir da câmera acha o ponto
/// exato mirado.
/// </summary>
public class WeaponShootingLaserBolt : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private InputActionReference fireAction;

    [Header("Atributos da Arma")]
    [SerializeField] float shotDamage = 10f;

    [Header("Áudio")]
    [SerializeField] private WeaponAudioManager weaponAudio;
    [SerializeField] private AudioClip shotSound;
    [SerializeField] private AudioClip impactSound;
    [Tooltip("Variação aleatória de pitch pra cada tiro não soar idêntico")]
    [SerializeField] private Vector2 shotPitchRange = new Vector2(0.95f, 1.05f);

    [Header("Referências")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private ParticleSystem muzzleFlash;

    [Header("Prefab do Laser Bolt")]
    [Tooltip("Prefab com VolumetricLineBehavior + LaserBoltProjectile (o laser pronto do asset, adaptado)")]
    [SerializeField] private LaserBoltProjectile laserBoltPrefab;
    [SerializeField] private float shotSpeed = 40f;
    [SerializeField] private float shotLifetime = 0.6f;
    [SerializeField] private float fireRate = 0.08f;
    [SerializeField] private bool autoFire = true;
    [Tooltip("Camadas que o bolt pode acertar (usado pelo LaserBoltProjectile pra colisão/dano)")]
    [SerializeField] private LayerMask hitLayerMask = ~0;

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
    private bool isWeaponSoundPlaying = false;

    private void OnEnable()
    {
        if (fireAction != null) fireAction.action.Enable();
    }

    private void OnDisable()
    {
        if (fireAction != null) fireAction.action.Disable();
    }

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
    /// a "maxAimDistance" na direção da câmera.
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

        if (laserBoltPrefab != null)
        {
            LaserBoltProjectile bolt = Instantiate(
                laserBoltPrefab,
                firePoint.position,
                Quaternion.LookRotation(direction)
            );

            Color color = neonColors[Random.Range(0, neonColors.Length)];
            bolt.Initialize(shotDamage, color, hitLayerMask, shotSpeed, shotLifetime, impactParticles);
        }

        if (muzzleFlash != null)
        {
            muzzleFlash.transform.rotation = Quaternion.LookRotation(direction);
            muzzleFlash.Play();
        }

        if (AudioManager.Instance != null && shotSound != null)
        {
            float pitch = Random.Range(shotPitchRange.x, shotPitchRange.y);
            // AudioManager.Instance.PlaySFX(shotSound, pitch);
        }
    }
}