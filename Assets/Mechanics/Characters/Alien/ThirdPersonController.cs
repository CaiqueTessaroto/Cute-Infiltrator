using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de personagem em terceira pessoa usando o novo Input System.
/// Movimento puramente "Transform" via CharacterController (sem física real) - o corpo
/// visual apenas gira (Slerp) para encarar a direção do movimento.
///
/// (Existe também uma versão com física real/Rigidbody, onde a esfera rola de verdade:
/// ThirdPhysicPersonController.cs)
///
/// HIERARQUIA SUGERIDA:
/// Player (CharacterController + este script)
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
public class ThirdPersonController : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Transform vazio (pivot) ao redor do qual a câmera orbita. Fica na altura da 'cabeça' do personagem.")]
    public Transform cameraPivot;
    [Tooltip("A própria câmera, posicionada em relação ao cameraPivot via órbita esférica.")]
    public Transform cameraTransform;
    [Tooltip("Transform visual do personagem (por enquanto, a esfera verde). É ele que gira para encarar a direção do movimento.")]
    public Transform visualBody;
    [Tooltip("Objeto vazio filho, posicionado no 'pé' do personagem. Usado para calcular o offset do collider, igual ao FirstPersonController.")]
    public Transform footReference;

    [Header("Inputs (New Input System)")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Movimento")]
    public float walkSpeed = 5f;
    public float gravity = -20f;
    public float jumpHeight = 1.2f;
    [Tooltip("Velocidade com que o corpo gira para encarar a direção do movimento (graus/seg aproximado via Slerp).")]
    public float bodyRotationSpeed = 12f;

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

    private CharacterController controller;
    private Vector3 velocity;
    private float yaw = 0f;
    private float pitch = 15f;
    private float footYOffset = 0f;
    private Vector3 pivotLocalPos;

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

        HandleCameraOrbit(); // posiciona a câmera corretamente já no primeiro frame, sem esperar o Update

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseOrbit();
        HandleMovement();
        HandleCameraOrbit();
    }

    void HandleMouseOrbit()
    {
        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // O pivot em si não gira: ele só marca o ponto (posição) ao redor do qual
        // a câmera orbita. Quem calcula a órbita de verdade é HandleCameraOrbit().
    }

    void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;

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

            // Gira o corpo visual suavemente para encarar a direção do movimento.
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

    /// <summary>
    /// Faz a câmera orbitar de verdade ao redor do player: calcula a posição da câmera
    /// em coordenadas esféricas (yaw, pitch, distância) a partir do cameraPivot, que
    /// segue a posição do player mas não gira sozinho. Roda depois do movimento, para
    /// a câmera já orbitar em torno da posição atualizada do player no mesmo frame.
    /// </summary>
    void HandleCameraOrbit()
    {
        if (cameraPivot == null || cameraTransform == null) return;

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 orbitDirection = orbitRotation * Vector3.back; // "para trás" a partir do olhar da câmera

        float desiredDistance = cameraDistance;

        if (cameraCollision &&
            Physics.SphereCast(cameraPivot.position, cameraCollisionRadius, orbitDirection, out RaycastHit hit, cameraDistance, cameraCollisionMask, QueryTriggerInteraction.Ignore))
        {
            desiredDistance = Mathf.Max(hit.distance, 0.2f);
        }

        // Posição em coordenadas esféricas: pivot (centro da órbita) + direção * distância.
        cameraTransform.position = cameraPivot.position + orbitDirection * desiredDistance;
        cameraTransform.rotation = orbitRotation;
    }
}