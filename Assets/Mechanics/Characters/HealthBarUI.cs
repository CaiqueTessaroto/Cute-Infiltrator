using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla uma barra de vida em World Space que fica acima do objeto
/// e sempre encara a câmera (billboard).
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Image fillImage; // Image com Type = Filled, Fill Method = Horizontal
    [SerializeField] private Transform target; // objeto que a barra segue (opcional)
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, 0f);

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
        }

        // Billboard: sempre de frente pra câmera
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }

    public void UpdateHealthBar(float current, float max)
    {
        if (fillImage == null) return;
        fillImage.fillAmount = Mathf.Clamp01(current / max);
    }
}