using UnityEngine;

/// <summary>
/// Coloque em qualquer objeto da cena que o player deva poder "virar".
///
/// Por padrão, o visual usado na transformação é o PRÓPRIO gameObject onde esse
/// componente está (clonado dinamicamente). Isso permite reaproveitar o mesmo
/// TransformableObjectData (movimento, física, etc.) em vários objetos diferentes
/// na cena, sem precisar criar um asset novo pra cada um só por causa do prefab visual.
///
/// Se data.visualPrefab estiver preenchido, ele tem prioridade (útil quando o
/// objeto da cena tem coisas que você NÃO quer clonar, tipo triggers, luzes, etc,
/// e prefere apontar pra um prefab "limpo" só com o visual).
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

    /// <summary>
    /// Retorna o prefab/objeto a ser instanciado como visual do player nessa forma.
    /// Prioriza data.visualPrefab (override manual); se não tiver, usa o próprio
    /// gameObject como "molde".
    /// </summary>
    public GameObject GetVisualSource()
    {
        if (data != null && data.visualPrefab != null)
        {
            return data.visualPrefab;
        }

        return gameObject;
    }
}