using UnityEngine;

/// <summary>
/// Controla o Animator do personagem Alien (forma original, movida por Transform/CharacterController
/// + animação) de forma isolada do script de movimento.
///
/// O ThirdPhysicPersonController (ou qualquer outro script de movimento) apenas chama
/// os métodos públicos abaixo, sem precisar conhecer os parâmetros internos do Animator.
///
/// CONFIGURAÇÃO NO ANIMATOR CONTROLLER:
/// - Parâmetro Float  "Speed"    -> usado num Blend Tree (0 = Idle, 1 = Run) para transição suave.
/// - Parâmetro Bool   "Grounded" -> alterna entre states de chão e queda/pulo no ar.
/// - Parâmetro Trigger "Jump"    -> dispara o state de pulo.
///
/// Anexe este script no mesmo GameObject que tem o componente Animator
/// (geralmente o modelo 3D do Alien, filho do objeto com o CharacterController/Rigidbody).
/// </summary>
[RequireComponent(typeof(Animator))]
public class AlienCharacterAnimatorController : MonoBehaviour
{
    [Header("Nomes dos parâmetros no Animator")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string groundedParam = "Grounded";
    [SerializeField] private string jumpParam = "Jump";

    [Header("Suavização")]
    [Tooltip("Velocidade de interpolação do parâmetro Speed (maior = responde mais rápido).")]
    [SerializeField] private float speedDampTime = 0.1f;

    private Animator animator;
    private int speedHash;
    private int groundedHash;
    private int jumpHash;

    void Awake()
    {
        animator = GetComponent<Animator>();
        speedHash = Animator.StringToHash(speedParam);
        groundedHash = Animator.StringToHash(groundedParam);
        jumpHash = Animator.StringToHash(jumpParam);
    }

    /// <summary>
    /// Atualiza o parâmetro de movimento (Idle/Run) do Animator.
    /// Chame isso a cada frame a partir do script de movimento, passando um valor
    /// normalizado de 0 (parado) a 1 (correndo) — ex: moveInput.magnitude.
    /// </summary>
    public void SetMovementSpeed(float normalizedSpeed)
    {
        animator.SetFloat(speedHash, normalizedSpeed, speedDampTime, Time.deltaTime);
    }

    /// <summary>
    /// Atualiza o estado de "no chão" do Animator, usado para alternar entre states
    /// de solo e de queda/ar. Chame a cada frame passando o resultado de isGrounded
    /// do controller de movimento.
    /// </summary>
    public void SetGrounded(bool isGrounded)
    {
        animator.SetBool(groundedHash, isGrounded);
    }

    /// <summary>
    /// Dispara a animação de pulo. Chame no momento em que o pulo é executado
    /// (ex: dentro do "if (jumpAction.action.WasPressedThisFrame() && isGrounded)").
    /// </summary>
    public void TriggerJump()
    {
        animator.SetTrigger(jumpHash);
    }
}