using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de personagem em terceira pessoa usando o novo Input System.
/// Suporta DOIS modos de movimento, alternáveis por um bool no Inspector (ou em runtime):
///
///   - Transform (CharacterController): o modo original. Movimento "kinemático",
///     sem física de verdade. O corpo visual apenas gira (Slerp) para encarar a
///     direção do movimento, sem realmente "rolar".
///
///   - Física (Rigidbody): a cápsula raiz vira um corpo físico de verdade (gravidade,
///     colisões respondendo com física, pode ser empurrada/empurrar outros Rigidbodies).
///     Além disso, a esfera visual GIRA de verdade como se estivesse rolando pelo chão,
///     calculado a partir da velocidade e do raio da esfera (v = ω * r).
///
/// Requer um CharacterController E um Rigidbody no mesmo GameObject (a "cápsula").
/// O script ativa/desativa cada um automaticamente conforme o modo escolhido, então
/// não tem problema os dois existirem juntos no mesmo objeto.
///
/// HIERARQUIA SUGERIDA:
/// Player (CharacterController + Rigidbody + este script)
///  ├── Foot (Empty, na altura do chão, mesma ideia do FirstPersonController)
///  ├── Visual (esfera verde, filha, representando o personagem por enquanto - sem animação)
///  ├── CameraPivot (Empty, na altura dos "ombros"/cabeça - só marca o CENTRO da órbita, não gira)
///  └── Main Camera (NÃO precisa ser filha do pivot - a posição dela é calculada em
///       espaço de mundo todo frame, orbitando ao redor do CameraPivot)
///
/// CONFIGURAÇÃO DAS ACTIONS (mesmo Input Actions asset do FPS):
/// - Move   -> Value / Vector2  (ex: WASD, Left Stick)
/// - Look   -> Value / Vector2  (ex: Mouse Delta, Right Stick)
/// - Jump   -> Button           (ex: Space)
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Rigidbody))]
public class ThirdPhysicPersonController : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Transform vazio (pivot) ao redor do qual a câmera orbita. Fica na altura da 'cabeça' do personagem.")]
    public Transform cameraPivot;
    [Tooltip("A própria câmera, filha do cameraPivot, posicionada atrás do personagem (ex: local pos 0,0,-4).")]
    public Transform cameraTransform;
    [Tooltip("Transform visual do personagem (por enquanto, a esfera verde). É ele que gira/rola.")]
    public Transform visualBody;
    [Tooltip("Objeto vazio filho, posicionado no 'pé' do personagem. Usado para calcular o offset do collider, igual ao FirstPersonController.")]
    public Transform footReference;

    [Header("Inputs (New Input System)")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Modo de Movimento")]
    [Tooltip("Desmarcado = Transform (CharacterController, sem física). Marcado = Física (Rigidbody, esfera rola de verdade).")]
    public bool usePhysicsMovement = false;

    [Header("Movimento (Transform)")]
    public float walkSpeed = 5f;
    public float gravity = -20f;
    public float jumpHeight = 1.2f;
    [Tooltip("Velocidade com que o corpo gira para encarar a direção do movimento (graus/seg aproximado via Slerp). Usado só no modo Transform.")]
    public float bodyRotationSpeed = 12f;

    [Header("Movimento (Física / Rigidbody)")]
    [Tooltip("Velocidade alvo horizontal quando em modo Física.")]
    public float physicsMoveSpeed = 5f;
    [Tooltip("Quão rápido a velocidade atual converge para a velocidade alvo (aceleração 'macia').")]
    public float physicsAcceleration = 20f;
    [Tooltip("Força de impulso aplicada no pulo (modo Física).")]
    public float physicsJumpForce = 6f;
    [Tooltip("Raio da esfera visual, usado para calcular a rotação de rolagem (v = ω * r). Deve bater com o raio da SphereCollider do visualBody.")]
    public float sphereRadius = 0.5f;
    [Tooltip("Distância do SphereCast de chão usado para detectar 'grounded' no modo Física.")]
    public float groundCheckDistance = 0.2f;
    public LayerMask groundMask = ~0;

    [Header("Órbita da Câmera (Mouse Look)")]
    public float mouseSensitivity = 0.1f;
    public float minPitch = -30f;
    public float maxPitch = 70f;
    [Tooltip("Distância da câmera até o pivot.")]
    public float cameraDistance = 4f;
    [Tooltip("Se true, a câmera aproxima do pivot ao colidir com cenário (evita atravessar paredes).")]
    public bool cameraCollision = true;
    public LayerMask cameraCollisionMask = ~0;
    [Tooltip("Raio da esfera usada no SphereCast de colisão da câmera.")]
    public float cameraCollisionRadius = 0.2f;

    [Header("Corpo / Câmera")]
    public float standingHeight = 2f;
    [Tooltip("Altura do pivot da câmera, relativa ao pé do personagem.")]
    public float pivotY = 1.6f;

    [Header("Trasformação")]
    public GameObject OriginalBody;
    [Tooltip("Escala do player na forma original para teste (ex: personagem pequeno = 0.4,0.4,0.4).")]
    [SerializeField] private Vector3 originalScale = new Vector3(0.4f, 0.4f, 0.4f);
    private Transform originalFootReference;

    private CharacterController controller;
    private Rigidbody rb;
    private SphereCollider visualBodyCollider; // já existe no visualBody; o Rigidbody usa colliders de filhos automaticamente
    private Vector3 velocity; // usado só no modo Transform
    private float yaw = 0f;
    private float pitch = 15f;
    private float footYOffset = 0f;
    private Vector3 pivotLocalPos;
    private bool appliedModeLastFrame; // para detectar troca do bool em runtime (ex: via Inspector em Play Mode)
    private LayerMask originalGroundMask;
    private int originalLayer;

    public void ApllyOriginalForm()
    {
        visualBody = OriginalBody.transform;
        OriginalBody.SetActive(true);
        usePhysicsMovement = true;
        DestroyClonedChildren();

        transform.localScale = originalScale;

        groundMask = originalGroundMask;
        gameObject.layer = originalLayer;

        // Reatribui o collider correto ANTES de qualquer coisa depender dele
        //visualBodyCollider = visualBody.GetComponent<SphereCollider>();

        jumpHeight = 1.2f;
        physicsJumpForce = 6f;

        footReference = originalFootReference;
        footYOffset = (footReference != null) ? footReference.localPosition.y : 0f;

        controller.height = standingHeight;
        controller.center = new Vector3(0f, footYOffset + standingHeight / 2f, 0f);

        // Força a reaplicação do modo, mesmo que o bool não tenha mudado
        ApplyMovementMode();
    }

    /// <summary>
    /// Destrói qualquer filho direto de gameObject cujo nome termine com "(Clone)",
    /// ou seja, qualquer visual instanciado dinamicamente pelo ShapeshiftAbility.
    /// Não afeta o visualBody original nem outros filhos "fixos" da hierarquia.
    /// </summary>
    void DestroyClonedChildren(GameObject exclude = null)
    {
        // Percorre de trás pra frente porque vamos remover filhos durante o loop
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (exclude != null && child.gameObject == exclude)
            {
                continue; // não destrói o objeto excluído
            }

            if (child.name.EndsWith("(Clone)"))
            {
                Debug.Log($"[ThirdPhysicPersonController] Destruindo clone: {child.name}");
                Destroy(child.gameObject);
            }
        }
    }

    public void ApplyForm(float colliderRadius, float controllerHeight, float moveSpeed, float jumpForce, bool physicsMovement, LayerMask newGroundMask, LayerMask excludedLayers, GameObject objectBody, Transform footReferenceOverride = null)
    {

        usePhysicsMovement = physicsMovement;
        if (!usePhysicsMovement)
            rb.isKinematic = true;

        // Redefine o footReference pra essa forma específica
        if (footReferenceOverride != null)
        {
            footReference.position = footReferenceOverride.position;
            footYOffset = footReference.localPosition.y;
        }
        else
        {
            GameObject fallbackFoot = new GameObject("Foot (Clone)");
            fallbackFoot.transform.SetParent(objectBody != null ? objectBody.transform : transform);
            fallbackFoot.transform.localPosition = Vector3.zero;

            footReference.position = fallbackFoot.transform.position;
            footYOffset = 0f;
        }

        DestroyClonedChildren(exclude: objectBody);

        visualBody.gameObject.SetActive(false);

        sphereRadius = colliderRadius;
        walkSpeed = moveSpeed;
        physicsMoveSpeed = moveSpeed;
        physicsJumpForce = jumpForce;
        jumpHeight = jumpForce;
        groundMask = newGroundMask;

        controller.excludeLayers = excludedLayers;
        //gameObject.layer = LayerMaskToLayer(newObjectLayer);

        visualBody = objectBody?.transform;
        OriginalBody.transform.position = objectBody.transform.position;

        controller.height = controllerHeight;
        controller.center = new Vector3(0f, footYOffset + controllerHeight / 2f, 0f);

        if (visualBodyCollider != null)
            visualBodyCollider.radius = colliderRadius;

        transform.localScale = Vector3.one;
    }

    private int LayerMaskToLayer(LayerMask mask)
    {
        int value = mask.value;
        if (value == 0) return gameObject.layer; // nada marcado, mantém a atual

        int layerNumber = 0;
        while ((value & 1) == 0)
        {
            value >>= 1;
            layerNumber++;
        }
        return layerNumber;
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        jumpAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        lookAction.action.Disable();
        jumpAction.action.Disable();
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        originalGroundMask = groundMask;
        originalLayer = gameObject.layer;

        if (visualBody != null)
        {
            visualBodyCollider = visualBody.GetComponent<SphereCollider>();
            if (visualBodyCollider == null)
            {
                Debug.LogWarning("ThirdPersonController: visualBody não tem SphereCollider. O modo Física não vai ter com o que colidir.", this);
            }
            else if (visualBodyCollider.isTrigger)
            {
                Debug.LogWarning("ThirdPersonController: a SphereCollider do visualBody está marcada como 'Is Trigger' - ela não vai gerar colisão física de verdade. Desmarque 'Is Trigger' para o modo Física funcionar.", this);
            }
        }

        originalFootReference = footReference;
        footYOffset = (footReference != null) ? footReference.localPosition.y : 0f;

        controller.height = standingHeight;
        controller.center = new Vector3(0f, footYOffset + standingHeight / 2f, 0f);

        if (cameraPivot != null)
        {
            pivotLocalPos = cameraPivot.localPosition;
            pivotLocalPos.y = footYOffset + pivotY;
            cameraPivot.localPosition = pivotLocalPos;
            yaw = cameraPivot.eulerAngles.y;
        }

        // A câmera pode continuar como filha do cameraPivot na hierarquia (ou não - tanto faz):
        // HandleCameraOrbit() define transform.position/rotation em espaço de MUNDO todo
        // frame, então o parenting não interfere no resultado.

        // Trava a rotação física do Rigidbody: quem gira o visual é o script (rolagem calculada),
        // não a física de colisão, senão a esfera tomba/rola de forma incontrolável.
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        ApplyMovementMode();
        HandleCameraOrbit(); // posiciona a câmera corretamente já no primeiro frame, sem esperar o Update

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Permite alternar o modo em runtime (ex: mudando o bool pelo Inspector em Play Mode, ou por código).
        if (usePhysicsMovement != appliedModeLastFrame)
        {
            ApplyMovementMode();
        }

        HandleMouseOrbit();

        if (usePhysicsMovement)
        {
            HandleMovementPhysicsInput(); // leitura de jump aqui; o resto acontece no FixedUpdate
        }
        else
        {
            HandleMovementTransform();
        }

        HandleCameraOrbit();
    }

    void FixedUpdate()
    {
        if (usePhysicsMovement)
        {
            HandleMovementPhysicsFixed();
        }
    }

    /// <summary>
    /// Liga/desliga CharacterController e Rigidbody conforme o modo escolhido,
    /// para os dois não brigarem pelo controle do movimento.
    /// </summary>
    void ApplyMovementMode()
    {
        if (usePhysicsMovement)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            controller.enabled = false;
            if (visualBodyCollider != null) visualBodyCollider.enabled = true; // agora o Rigidbody tem com o que colidir
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        else
        {
            rb.isKinematic = true; // fica "desligado" fisicamente, o CharacterController assume
            rb.useGravity = false;
            if (visualBodyCollider != null) visualBodyCollider.enabled = false; // evita colidir "em dobro" com o CharacterController
            controller.enabled = true;

        }

        appliedModeLastFrame = usePhysicsMovement;
    }

    void HandleMouseOrbit()
    {
        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // O pivot em si não gira mais: ele só marca o ponto (posição) ao redor do qual
        // a câmera orbita. Quem calcula a órbita de verdade é HandleCameraOrbit().
    }

    // ---------------------------------------------------------------
    // MODO TRANSFORM (CharacterController) - comportamento original
    // ---------------------------------------------------------------
    void HandleMovementTransform()
    {
        bool isGrounded = controller.isGrounded;

        //Debug.Log($"[Transform] isGrounded={isGrounded}, controller.center={controller.center}, controller.height={controller.height}, controller.radius={controller.radius}, controller.skinWidth={controller.skinWidth}, footYOffset={footYOffset}, transform.position.y={transform.position.y}");

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // mantém o personagem "grudado" no chão
        }

        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        // Direção de movimento relativa ao "para onde a câmera está olhando" (só o yaw, ignora pitch).
        Vector3 moveDir = Vector3.zero;
        if (inputDir.sqrMagnitude > 0.0001f)
        {
            Quaternion camYawRotation = Quaternion.Euler(0f, yaw, 0f);
            moveDir = camYawRotation * inputDir;
            moveDir.y = 0f;
            moveDir.Normalize();

            // Gira o corpo visual suavemente para encarar a direção do movimento (não é rolagem física).
            if (visualBody != null)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                visualBody.rotation = Quaternion.Slerp(visualBody.rotation, targetRot, bodyRotationSpeed * Time.deltaTime);
            }
        }

        controller.Move(moveDir * walkSpeed * Time.deltaTime);

        if (jumpAction.action.WasPressedThisFrame() && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
    // ---------------------------------------------------------------
    // MODO FÍSICA (Rigidbody) - esfera rola de verdade
    // ---------------------------------------------------------------
    private Vector3 pendingMoveDir;
    private bool jumpRequested;

    void HandleMovementPhysicsInput()
    {
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        Quaternion camYawRotation = Quaternion.Euler(0f, yaw, 0f);
        pendingMoveDir = camYawRotation * inputDir;
        pendingMoveDir.y = 0f;

        if (jumpAction.action.WasPressedThisFrame())
        {
            jumpRequested = true;
        }
    }

    [Tooltip("Margem extra além do sphereRadius para o SphereCast de chão (não é mais a distância total - é só a folga).")]
    public float groundCheckMargin = 0.05f;

    bool IsGroundedPhysics()
    {
        if (footReference == null) return false;

        float radius = sphereRadius * 0.9f;

        // Origem logo acima do pé, só o suficiente pra sphere não nascer cravada no chão
        Vector3 origin = footReference.position + Vector3.up * radius;
        float castDistance = radius + groundCheckMargin;

        bool hit = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hitInfo, castDistance, groundMask, QueryTriggerInteraction.Ignore);

        //Debug.Log($"[IsGroundedPhysics] origin={origin}, radius={radius}, castDistance={castDistance}, hit={hit}, hitDistance={(hit ? hitInfo.distance.ToString() : "N/A")}");

        return hit;
    }

    void HandleMovementPhysicsFixed()
    {
        bool isGrounded = IsGroundedPhysics();

        // Acelera suavemente até a velocidade alvo (em vez de trocar a velocidade instantaneamente).
        Vector3 targetVelocity = pendingMoveDir * physicsMoveSpeed;
        Vector3 currentVelocity = rb.linearVelocity; // Unity < 6: troque para rb.velocity
        Vector3 newHorizontal = Vector3.MoveTowards(
            new Vector3(currentVelocity.x, 0f, currentVelocity.z),
            targetVelocity,
            physicsAcceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newHorizontal.x, currentVelocity.y, newHorizontal.z); // Unity < 6: rb.velocity

        if (jumpRequested && isGrounded)
        {
            rb.AddForce(Vector3.up * physicsJumpForce, ForceMode.VelocityChange);
        }
        jumpRequested = false;

        RollVisualBody(newHorizontal);
    }

    /// <summary>
    /// Gira a esfera visual como se estivesse rolando pelo chão, a partir da velocidade horizontal.
    /// Fórmula clássica de rolamento sem deslizar: ω = v / r, eixo perpendicular à direção do movimento.
    /// </summary>
    void RollVisualBody(Vector3 horizontalVelocity)
    {
        if (visualBody == null || sphereRadius <= 0f) return;

        float speed = horizontalVelocity.magnitude;
        if (speed < 0.01f) return;

        Vector3 moveDirNormalized = horizontalVelocity.normalized;
        // Eixo de rotação perpendicular à direção do movimento e ao "up" (regra da mão direita para rolar pra frente).
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDirNormalized);

        float angularSpeedDeg = (speed / sphereRadius) * Mathf.Rad2Deg;
        visualBody.Rotate(rotationAxis, angularSpeedDeg * Time.fixedDeltaTime, Space.World);
    }

    /// <summary>
    /// Faz a câmera orbitar de verdade ao redor do player: calcula a posição da câmera
    /// em coordenadas esféricas (yaw, pitch, distância) a partir do cameraPivot, que
    /// segue a posição do player mas não gira sozinho. Roda depois do movimento, para
    /// a câmera já orbitar em torno da posição atualizada do player no mesmo frame.
    /// </summary>
    [Header("Colisão da Câmera")]
    [Tooltip("Margem extra além do near clip plane da câmera, para não colar na parede.")]
    public float cameraCollisionBuffer = 0.05f;

    void HandleCameraOrbit()
    {
        if (cameraPivot == null || cameraTransform == null) return;

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 orbitDirection = orbitRotation * Vector3.back;

        float desiredDistance = cameraDistance;

        // Distância mínima segura = near clip da câmera + margem, nunca menos que isso
        Camera cam = cameraTransform.GetComponent<Camera>();
        float minDistance = (cam != null ? cam.nearClipPlane : 0.05f) + cameraCollisionBuffer;

        if (cameraCollision)
        {
            // SphereCast principal, ignorando a camada do próprio player
            LayerMask effectiveMask = cameraCollisionMask & ~(1 << gameObject.layer);

            if (Physics.SphereCast(cameraPivot.position, cameraCollisionRadius, orbitDirection,
                out RaycastHit hit, cameraDistance, effectiveMask, QueryTriggerInteraction.Ignore))
            {
                desiredDistance = hit.distance;
            }

            // Raycast extra fino, cobre cantos que o SphereCast (mais largo) às vezes escapa
            if (Physics.Raycast(cameraPivot.position, orbitDirection, out RaycastHit thinHit,
                cameraDistance, effectiveMask, QueryTriggerInteraction.Ignore))
            {
                desiredDistance = Mathf.Min(desiredDistance, thinHit.distance);
            }

            desiredDistance = Mathf.Max(desiredDistance, minDistance);
        }

        cameraTransform.position = cameraPivot.position + orbitDirection * desiredDistance;
        cameraTransform.rotation = orbitRotation;
    }


    void OnDrawGizmosSelected()
    {
        if (footReference == null) return;

        float radius = sphereRadius * 0.9f;
        Vector3 origin = footReference.position + Vector3.up * radius;
        float castDistance = radius + groundCheckMargin;
        Vector3 endPoint = origin + Vector3.down * castDistance;

        bool grounded = Application.isPlaying && usePhysicsMovement && IsGroundedPhysics();
        Gizmos.color = grounded ? Color.green : Color.red;

        Gizmos.DrawWireSphere(origin, radius);
        Gizmos.DrawWireSphere(endPoint, radius);
        Gizmos.DrawLine(origin, endPoint);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(footReference.position, 0.02f);
    }

}