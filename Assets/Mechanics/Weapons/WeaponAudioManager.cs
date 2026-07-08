using System.Collections;
using UnityEngine;

/// <summary>
/// MonoBehaviour dedicado a gerenciar o áudio de armas.
/// Suporta tiro único (não atropela o clip anterior) e loop contínuo (laser),
/// com duração mínima garantida e troca de arma sem cortes bruscos.
/// Anexe este componente na arma (ou num GameObject central de armas)
/// e arraste um AudioSource exclusivo pra ele.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WeaponAudioManager : MonoBehaviour
{
    [Header("Fonte de Áudio Exclusiva da Arma")]
    [SerializeField] private AudioSource weaponSource;

    [Header("Configurações Padrão")]
    [SerializeField] private float defaultMinDuration = 0.1f;

    private AudioClip currentWeaponClip;
    private bool currentWeaponIsLooping;
    private float weaponMinEndTime;
    private Coroutine weaponStopRoutine;

    private void Awake()
    {
        if (weaponSource == null)
        {
            weaponSource = GetComponent<AudioSource>();
        }

        weaponSource.playOnAwake = false;
    }

    /// <summary>
    /// Método único pra gerenciar o áudio de qualquer arma.
    /// - isFiring = true  -> pedido de tocar (tiro único ou segurando o botão)
    /// - isFiring = false -> pedido de parar (botão solto), respeita duração mínima
    /// - loopMode = true  -> comporta como laser (loop contínuo enquanto isFiring for true)
    /// - loopMode = false -> comporta como disparo único (só toca se o anterior já terminou)
    /// </summary>
    public void ManageWeaponSound(AudioClip clip, bool isFiring, bool loopMode, float pitch = 1f, float? minDuration = null)
    {
        if (weaponSource == null || clip == null) return;
        
        float minDur = minDuration ?? defaultMinDuration;

        // --- Pedido de PARAR (soltou o botão) ---
        if (!isFiring)
        {
            StopWeaponSound();
            return;
        }

        // --- Trocou de arma/clip enquanto tocava -> corta a anterior antes de iniciar a nova ---
        bool switchedWeapon = currentWeaponClip != null && currentWeaponClip != clip;
        if (switchedWeapon)
        {
            CancelStopRoutine();
            weaponSource.Stop();
        }

        if (loopMode)
        {
            // Já tocando esse mesmo clip em loop? não reinicia.
            if (weaponSource.isPlaying && currentWeaponClip == clip && currentWeaponIsLooping) return;

            CancelStopRoutine();
            weaponSource.loop = true;
            weaponSource.clip = clip;
            weaponSource.pitch = pitch;
            weaponSource.Play();

            currentWeaponClip = clip;
            currentWeaponIsLooping = true;
            weaponMinEndTime = Time.time + minDur;
        }
        else
        {
            // Tiro único: só toca se o slot estiver livre (evita atropelar o clip anterior)
            if (weaponSource.isPlaying && !switchedWeapon) return;

            weaponSource.loop = false;
            weaponSource.clip = clip;
            weaponSource.pitch = pitch;
            weaponSource.Play();

            currentWeaponClip = clip;
            currentWeaponIsLooping = false;
            weaponMinEndTime = Time.time + minDur;
        }
    }

    /// <summary>
    /// Para o som da arma respeitando a duração mínima já definida.
    /// </summary>
    public void StopWeaponSound()
    {
        if (weaponSource == null || !weaponSource.isPlaying) return;

        float remaining = weaponMinEndTime - Time.time;

        if (remaining <= 0f)
        {
            weaponSource.Stop();
            currentWeaponClip = null;
        }
        else if (weaponStopRoutine == null)
        {
            weaponStopRoutine = StartCoroutine(StopWeaponAfter(remaining));
        }
    }

    private IEnumerator StopWeaponAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        weaponSource.Stop();
        currentWeaponClip = null;
        weaponStopRoutine = null;
    }

    private void CancelStopRoutine()
    {
        if (weaponStopRoutine != null)
        {
            StopCoroutine(weaponStopRoutine);
            weaponStopRoutine = null;
        }
    }

    /// <summary>Útil se você quiser checar de fora se a arma está tocando algo no momento.</summary>
    public bool IsPlaying => weaponSource != null && weaponSource.isPlaying;
}


//[SerializeField] private WeaponAudioManager weaponAudio;

// tiro único
//float pitch = Random.Range(shotPitchRange.x, shotPitchRange.y);
//weaponAudio.ManageWeaponSound(shotSound, true, loopMode: false, pitch: pitch, minDuration: 0.05f);

// laser (loop) - no Update
//bool isPressed = fireAction.action.IsPressed();
//weaponAudio.ManageWeaponSound(laserSound, isPressed, loopMode: true, minDuration: 0.15f);