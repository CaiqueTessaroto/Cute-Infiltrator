using UnityEngine;

/// <summary>
/// Esconde o braço direito animado pelo Mixamo (zerando a escala do bone raiz do braço
/// após o Animator processar o frame) e controla um braço/arma substituto, que não faz
/// parte do esqueleto animado, apontando-o na direção da mira do jogador.
///
/// CONFIGURAÇÃO:
/// 1. Arraste o bone raiz do braço direito original (ex: "RightShoulder" ou "RightArm")
///    em 'originalArmRootBone'.
/// 2. Crie/arraste o braço substituto (modelo estático, sem skin) em 'replacementArm'.
///    Recomendado: deixe-o como filho do MESMO bone de ombro (mas de um bone que NÃO
///    será zerado, ex: o pai do 'originalArmRootBone'), assim ele acompanha a posição
///    do corpo, mas você controla a rotação manualmente.
/// 3. Arraste a câmera do jogador em 'cameraTransform'.
///
/// Deve rodar em LateUpdate para sobrescrever o resultado do Animator no mesmo frame.
/// </summary>
public class RobotArmOverride : MonoBehaviour
{
    [Header("Braço original (Mixamo)")]
    [Tooltip("Bone raiz do braço direito animado. Será escondido (escala 0) a cada frame.")]
    public Transform originalArmRootBone;

    [Header("Braço substituto")]
    [Tooltip("GameObject do braço/arma que substitui visualmente o braço animado.")]
    public Transform replacementArm;
    [Tooltip("Ajuste de rotação caso o eixo 'forward' do braço substituto não esteja alinhado com o cano da arma.")]
    public Vector3 rotationOffsetEuler = Vector3.zero;

    [Header("Mira")]
    public Transform cameraTransform;
    [Tooltip("Distância máxima do raycast e também a distância usada se o raycast não acertar nada.")]
    public float aimDistance = 100f;
    [Tooltip("Layers que o raycast de mira deve considerar. Exclua a layer do próprio player pra não acertar o corpo do robô.")]
    public LayerMask aimLayerMask = ~0;

    [Header("Debug")]
    [Tooltip("Ponto no mundo que a câmera está mirando neste frame. Atualizado automaticamente, apenas leitura.")]
    public Vector3 targetPoint;
    [Tooltip("Desenha um gizmo no targetPoint na Scene View.")]
    public bool drawTargetGizmo = true;

    private bool _drawTargetGizmo = false;
    private bool overrideEnabled = true;

    /// <summary>
    /// Calcula o ponto real que a câmera está mirando (via raycast a partir da câmera,
    /// não a partir da mão), evitando erro de paralaxe em câmeras orbitais de 3ª pessoa
    /// onde a câmera fica longe do braço. Atualiza o campo 'targetPoint'.
    /// </summary>
    /// 

    void Awake()
    {
        _drawTargetGizmo = drawTargetGizmo;
    }
    private void UpdateTargetPoint()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, aimDistance, aimLayerMask, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = cameraTransform.position + cameraTransform.forward * aimDistance;
        }
    }

    /// <summary>
    /// Orienta o braço substituto para apontar em direção ao 'targetPoint' já calculado.
    /// </summary>
    private void AimReplacementArmAtTarget()
    {
        if (replacementArm == null)
            return;

        Vector3 direction = (targetPoint - replacementArm.position).normalized;

        if (direction.sqrMagnitude < 0.0001f)
            return; // evita LookRotation com direção zero (braço e alvo no mesmo ponto)

        Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);
        replacementArm.rotation = lookRotation * Quaternion.Euler(rotationOffsetEuler);
    }

    void LateUpdate()
    {
        if (!overrideEnabled)
            return;

        // 1. Esconde o braço original (colapsa a mesh dele visualmente)
        if (originalArmRootBone != null)
        {
            originalArmRootBone.localScale = Vector3.zero;
        }

        // 2. Calcula o targetPoint com base na câmera
        if (cameraTransform != null)
        {
            UpdateTargetPoint();
        }

        // 3. Usa o targetPoint como referência para apontar o braço substituto
        AimReplacementArmAtTarget();
    }

    /// <summary>
    /// Liga/desliga o override (ex: desativar se o robô perder o braço numa cutscene,
    /// ou trocar de arma e precisar recalcular referências antes).
    /// </summary>
    public void SetOverrideEnabled(bool enabled)
    {
        overrideEnabled = enabled;

        // Se desativado, restaura a escala original do braço animado
        if (!enabled && originalArmRootBone != null)
        {
            originalArmRootBone.localScale = Vector3.one;
        }
    }

    void OnDrawGizmos()
    {
        if (!_drawTargetGizmo)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(targetPoint, 0.08f);

        if (replacementArm != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(replacementArm.position, targetPoint);
        }
    }
}