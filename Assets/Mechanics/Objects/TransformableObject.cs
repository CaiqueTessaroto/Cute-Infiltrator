using UnityEngine;

/// <summary>
/// Coloque em qualquer objeto da cena que o player deva poder "virar".
/// </summary>
[RequireComponent(typeof(Collider))]
public class TransformableObject : MonoBehaviour
{
    public TransformableObjectData data;

    [Header("Feedback visual")]
    public GameObject highlightVFX; // ex: outline, glow - ativa quando o player está perto olhando pra ele

    public void SetHighlighted(bool state)
    {
        if (highlightVFX != null) highlightVFX.SetActive(state);
    }
}