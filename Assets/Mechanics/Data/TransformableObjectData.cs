using UnityEngine;

[CreateAssetMenu(fileName = "NewTransformableForm", menuName = "Shapeshift/Transformable Object Data")]
public class TransformableObjectData : ScriptableObject
{
    [Header("Identidade")]
    public string formName;

    [Header("Visual")]
    [Tooltip("Prefab (ou mesh) que substitui o visualBody do player enquanto nessa forma.")]
    public GameObject visualPrefab;
    public float visualScale = 1f;

    [Header("Física / Collider")]
    public bool usePhysicsMovement = true;
    public float colliderRadius = 0.5f;
    public float controllerHeight = 1f;

    [Header("Movimento")]
    public float moveSpeed = 5f;
    public float jumpForce = 6f;
    public bool canJump = true;

    [Header("Habilidade especial (opcional)")]
    [Tooltip("Ex: 'explode', 'flutua', 'atravessa grade fina' - trate isso no ShapeshiftAbility ou num script separado por forma.")]
    public float detectionRange = 6f;
    public string specialAbilityId;
}