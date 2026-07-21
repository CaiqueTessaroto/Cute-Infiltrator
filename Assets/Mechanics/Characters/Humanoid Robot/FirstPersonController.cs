using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de personagem em primeira pessoa simples usando o novo Input System.
/// Requer um CharacterController no mesmo GameObject (a "cápsula").
/// A câmera deve ser filha desse GameObject (ex: um Empty "CameraHolder" na altura dos olhos, com a Camera dentro).
///
/// CONFIGURAÇÃO DAS ACTIONS (no seu Input Actions asset):
/// - Move   -> Value / Vector2  (ex: WASD, Left Stick)
/// - Look   -> Value / Vector2  (ex: Mouse Delta, Right Stick)
/// - Jump   -> Button           (ex: Space)
/// - Crouch -> Button           (ex: Left Ctrl / C)
/// Arraste as referências dessas actions nos campos abaixo no Inspector.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Transform da câmera (ou de um holder pai da câmera). Usado para olhar para cima/baixo.")]
    public Transform cameraTransform;
    [Tooltip("Objeto vazio filho, posicionado no 'pé' do personagem. Usado para calcular o offset do collider e da câmera, independente de onde está o pivot do objeto raiz.")]
    public Transform footReference;

    [Header("Inputs (New Input System)")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference crouchAction;

    [Header("Movimento")]
    public float walkSpeed = 5f;
    public float crouchSpeed = 2.5f;
    public float gravity = -20f;
    public float jumpHeight = 1.2f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.1f;
    public float minPitch = -85f;
    public float maxPitch = 85f;

    [Header("Agachar (Crouch)")]
    public float standingHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 10f;
    [Tooltip("Altura da câmera em pé, relativa ao pé do personagem.")]
    public float standingCameraY = 1.7f;
    [Tooltip("Altura da câmera agachado, relativa ao pé do personagem.")]
    public float crouchCameraY = 0.9f;

    [Header("Animação")]
    public CharacterAnimatorController animController;

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch = 0f;
    private bool isCrouching = false;
    private float targetHeight;
    private Vector3 cameraLocalPos;
    private float footYOffset = 0f;

    void OnEnable()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        jumpAction.action.Enable();
        crouchAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        lookAction.action.Disable();
        jumpAction.action.Disable();
        crouchAction.action.Disable();
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        targetHeight = standingHeight;

        // Calcula o offset entre o pivot do objeto raiz e o "pé" real do personagem.
        // Assim o collider fica alinhado ao chão independente de onde o pivot do modelo está.
        footYOffset = (footReference != null) ? footReference.localPosition.y : 0f;

        standingHeight = controller.height;
        //controller.center = new Vector3(0f, footYOffset + standingHeight / 2f, 0f);

        if (cameraTransform != null)
        {
            cameraLocalPos = cameraTransform.localPosition;
            cameraLocalPos.y = footYOffset + standingCameraY;
            cameraTransform.localPosition = cameraLocalPos;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        // Gira o corpo (cápsula) no eixo Y
        transform.Rotate(Vector3.up * mouseX);

        // Gira a câmera no eixo X (olhar para cima/baixo), com limite
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // mantém o personagem "grudado" no chão
        }

        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        move = Vector3.ClampMagnitude(move, 1f);

        if (animController != null)
            animController.SetMovementSpeed(move.magnitude);

        float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Pulo (não permitido agachado, comportamento comum em FPS)
        if (jumpAction.action.WasPressedThisFrame() && isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (animController != null)
                animController.TriggerJump();
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleCrouch()
    {
        isCrouching = crouchAction.action.IsPressed();

        targetHeight = isCrouching ? crouchHeight : standingHeight;

        // Suaviza a transição de altura do CharacterController
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        //controller.center = new Vector3(0f, footYOffset + controller.height / 2f, 0f);

        // Suaviza a altura da câmera
        if (cameraTransform != null)
        {
            float targetCamY = footYOffset + (isCrouching ? crouchCameraY : standingCameraY);
            cameraLocalPos.y = Mathf.Lerp(cameraLocalPos.y, targetCamY, crouchTransitionSpeed * Time.deltaTime);
            cameraTransform.localPosition = cameraLocalPos;
        }
    }
}