using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ThirdPhysicPersonController))]
public class ShapeshiftAbility : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference transformAction; // botão de "virar"
    [SerializeField] private InputActionReference revertAction;    // botão de "voltar ao normal" (pode ser o mesmo botão)

    [Header("Detecção")]
    [Tooltip("Centro da esfera de detecção - geralmente o próprio player.")]
    [SerializeField] private Transform detectionOrigin;
    [Tooltip("Raio da esfera de detecção.")]
    [SerializeField] private float detectionRange = 4f;
    [Tooltip("De onde vem a direção 'pra onde estou olhando' - geralmente a câmera. Usado só pra priorizar entre múltiplos alvos na área.")]
    [SerializeField] private Transform aimDirectionReference;
    [SerializeField] private LayerMask transformableLayers;

    [Header("Forma original (pra poder reverter)")]
    [SerializeField] private TransformableObjectData originalFormData;

    // Buffer reutilizável pra evitar alocação de array a cada frame
    private readonly Collider[] detectionBuffer = new Collider[16];
    private ThirdPhysicPersonController controller;
    private TransformableObject currentTarget;   // o que está mirando agora
    private TransformableObjectData currentForm; // forma atual do player
    private GameObject spawnedVisual;

    void Awake()
    {
        controller = GetComponent<ThirdPhysicPersonController>();
    }

    void OnEnable()
    {
        transformAction.action.Enable();
        revertAction.action.Enable();
    }

    void OnDisable()
    {
        transformAction.action.Disable();
        revertAction.action.Disable();
    }

    void Update()
    {
        DetectTarget();

        if (transformAction.action.WasPressedThisFrame())
        {
            if (currentTarget != null)
            {
                TransformInto(currentTarget.data);
            }
            else
            {
                Debug.LogWarning("[Shapeshift] Nenhum alvo detectado no momento do input.");
            }
        }

        if (revertAction.action.WasPressedThisFrame() && currentForm != originalFormData)
        {
            controller.ApllyOriginalForm();
            //TransformInto(originalFormData);
        }
    }

    void DetectTarget()
    {
        if (currentTarget != null) currentTarget.SetHighlighted(false);
        currentTarget = null;

        int count = Physics.OverlapSphereNonAlloc(
            detectionOrigin.position,
            detectionRange,
            detectionBuffer,
            transformableLayers,
            QueryTriggerInteraction.Ignore);

        if (count == 0)
        {
            return;
        }

        float bestScore = float.MinValue;
        TransformableObject best = null;

        Vector3 aimOrigin = aimDirectionReference.position;
        Vector3 aimForward = aimDirectionReference.forward;

        for (int i = 0; i < count; i++)
        {
            if (!detectionBuffer[i].TryGetComponent(out TransformableObject t))
            {
                continue;
            }

            Vector3 toTarget = (detectionBuffer[i].transform.position - aimOrigin).normalized;
            float alignment = Vector3.Dot(aimForward, toTarget);

            if (alignment > bestScore)
            {
                bestScore = alignment;
                best = t;
            }
        }

        currentTarget = best;
        if (currentTarget != null)
        {
            currentTarget.SetHighlighted(true);
        }
    }

    void TransformInto(TransformableObjectData data)
    {
        if (data == null)
        {
            Debug.LogError("[Shapeshift] TransformableObjectData é NULL - o TransformableObject do alvo não tem 'data' atribuído no Inspector.");
            return;
        }

        currentForm = data;

        if (spawnedVisual != null) Destroy(spawnedVisual);

        Transform footChild = null;

        if (data.visualPrefab != null)
        {
            spawnedVisual = Instantiate(data.visualPrefab, gameObject.transform);
            spawnedVisual.transform.localPosition = Vector3.zero;
            spawnedVisual.transform.localScale = Vector3.one * data.visualScale;

            TryGetRendererBounds(spawnedVisual, out Bounds bounds);

            EnsureCollider(spawnedVisual, bounds);

            // Prioriza um "Foot" manual dentro do prefab, se existir; senão calcula pelos bounds.
            footChild = FindFootReference(spawnedVisual.transform);
            if (footChild == null)
            {
                footChild = CreateFootFromBounds(bounds, gameObject.transform);
            }
        }
        else
        {
            Debug.LogWarning($"[Shapeshift] '{data.formName}' não tem visualPrefab atribuído.");
        }

        controller.ApplyForm(
            colliderRadius: data.colliderRadius,
            controllerHeight: data.controllerHeight,
            moveSpeed: data.moveSpeed,
            jumpForce: data.jumpForce,
            objectBody: spawnedVisual,
            footReferenceOverride: footChild
        );
    }

    Transform FindFootReference(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (t.name == "Foot")
            {
                return t;
            }
        }
        return null;
    }
    /// <summary>
    /// Calcula os bounds combinados de todos os Renderers do objeto, em espaço de mundo.
    /// Retorna false se não houver nenhum Renderer.
    /// </summary>
    bool TryGetRendererBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return true;
    }

    /// <summary>
    /// Cria um GameObject vazio na base (Y mínimo) dos bounds fornecidos, como filho
    /// direto do player. Nomeado com sufixo "(Clone)" de propósito, pra ser destruído
    /// automaticamente pelo DestroyClonedChildren do controller (junto com o resto do
    /// visual antigo) quando o player transformar de novo ou reverter.
    /// </summary>
    Transform CreateFootFromBounds(Bounds bounds, Transform parent)
    {
        GameObject footGO = new GameObject("Foot (Clone)");
        footGO.transform.SetParent(parent);
        footGO.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        Debug.Log($"[Shapeshift] Foot criado automaticamente na posição Y (mundo) = {bounds.min.y}");

        return footGO.transform;
    }

    void EnsureCollider(GameObject target, Bounds bounds)
    {
        Collider existing = target.GetComponentInChildren<Collider>();
        if (existing != null)
        {
            Debug.Log($"[Shapeshift] '{target.name}' já tem um collider ({existing.GetType().Name}), não vou adicionar outro.");
            return;
        }

        BoxCollider box = target.AddComponent<BoxCollider>();

        box.center = target.transform.InverseTransformPoint(bounds.center);
        box.size = target.transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));

        Debug.Log($"[Shapeshift] BoxCollider adicionado em '{target.name}', ajustado ao Renderer (size = {box.size}).");
    }


    void OnDrawGizmosSelected()
    {
        if (detectionOrigin == null) return;

        Gizmos.color = (Application.isPlaying && currentTarget != null)
            ? new Color(0f, 1f, 0f, 0.25f)
            : new Color(1f, 1f, 0f, 0.15f);

        Gizmos.DrawSphere(detectionOrigin.position, detectionRange);

        Gizmos.color = (Application.isPlaying && currentTarget != null) ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(detectionOrigin.position, detectionRange);

        // Linha de mira (direção usada pra priorizar entre alvos na área)
        if (aimDirectionReference != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(aimDirectionReference.position, aimDirectionReference.position + aimDirectionReference.forward * detectionRange);
        }

        if (Application.isPlaying && currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentTarget.transform.position, 0.25f);
            Gizmos.DrawLine(aimDirectionReference.position, currentTarget.transform.position);
        }
    }

}