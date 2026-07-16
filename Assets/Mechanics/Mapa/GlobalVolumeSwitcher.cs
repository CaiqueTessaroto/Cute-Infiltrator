using UnityEngine;
using UnityEngine.Rendering;

public class GlobalVolumeSwitcher : MonoBehaviour
{
    [Header("Volumes")]
    [SerializeField] private Volume normalVolume;
    [SerializeField] private Volume horrorVolume;

    [Header("Transição")]
    [SerializeField] private float transitionDuration = 1.5f;

    private Coroutine transitionRoutine;

    private void Start()
    {
        // Garante estado inicial: normal ligado, terror zerado
        SetWeightsInstant(normal: 1f, horror: 0f);
    }

    /// Chame isso quando o bichinho transformar em monstro
    public void EnterHorrorMode()
    {
        StartTransition(targetNormalWeight: 0f, targetHorrorWeight: 1f);
    }

    /// Chame isso quando voltar a ser fofo
    public void ExitHorrorMode()
    {
        StartTransition(targetNormalWeight: 1f, targetHorrorWeight: 0f);
    }

    private void StartTransition(float targetNormalWeight, float targetHorrorWeight)
    {
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionRoutine(targetNormalWeight, targetHorrorWeight));
    }

    private System.Collections.IEnumerator TransitionRoutine(float targetNormal, float targetHorror)
    {
        float startNormal = normalVolume.weight;
        float startHorror = horrorVolume.weight;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            normalVolume.weight = Mathf.Lerp(startNormal, targetNormal, t);
            horrorVolume.weight = Mathf.Lerp(startHorror, targetHorror, t);

            yield return null;
        }

        normalVolume.weight = targetNormal;
        horrorVolume.weight = targetHorror;
    }

    private void SetWeightsInstant(float normal, float horror)
    {
        normalVolume.weight = normal;
        horrorVolume.weight = horror;
    }
}