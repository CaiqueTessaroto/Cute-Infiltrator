using UnityEngine;

/// <summary>
/// Controla o Animator do personagem de forma isolada do script de movimento.
/// O FirstPersonController (ou qualquer outro script de movimento) apenas chama
/// os métodos públicos abaixo, sem precisar conhecer os parâmetros internos do Animator.
///
/// CONFIGURAÇÃO NO ANIMATOR CONTROLLER:
/// - Parâmetro Float  "Speed" -> usado num Blend Tree (0 = Idle, 1 = Run) para transição suave.
/// - Parâmetro Trigger "Jump" -> dispara o state de pulo.
///
/// Anexe este script no mesmo GameObject que tem o componente Animator
/// (geralmente o modelo 3D, que pode ser filho do objeto com o CharacterController).
/// </summary>
[RequireComponent(typeof(Animator))]
public class CharacterAnimatorController : MonoBehaviour
{
    [Header("Nomes dos parâmetros no Animator")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string jumpParam = "Jump";

    [Header("Suavização")]
    [Tooltip("Velocidade de interpolação do parâmetro Speed (maior = responde mais rápido).")]
    [SerializeField] private float speedDampTime = 0.1f;

    private Animator animator;
    private int speedHash;
    private int jumpHash;

    void Awake()
    {
        animator = GetComponent<Animator>();
        speedHash = Animator.StringToHash(speedParam);
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
    /// Dispara a animação de pulo. Chame no momento em que o pulo é executado
    /// (ex: dentro do "if (jumpAction.action.WasPressedThisFrame() && isGrounded)").
    /// </summary>
    public void TriggerJump()
    {
        animator.SetTrigger(jumpHash);
    }
}