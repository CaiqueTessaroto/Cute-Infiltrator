using UnityEngine;
using System.Collections;
using UnityEngine.UI; // Necessário para Corrotinas

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Sources")]
    public AudioClip[] musics;
    public AudioSource musicSource;
    public AudioSource[] sfxSources;
    public AudioClip clickSound;

    [Header("Playlist Settings")]
    private AudioClip[] currentPlaylist;
    private int currentTrackIndex = 0;
    private bool isPlaylistActive = false;
    private bool isWaitingNextTrack = false; // Nova flag para o intervalo
    public float delayBetweenTracks = 180f; // 120 segundos = 2 minutos


    void Start()
    {
        if (musics != null && musics.Length > 0)
        {
            Shuffle(musics);
            PlayMusicPlaylist(musics);
        }
        //ApplySoundToAllButtons();

    }

    void Shuffle<T>(T[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (array[i], array[randomIndex]) = (array[randomIndex], array[i]);
        }
    }

    public void ApplySoundToAllButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);

        foreach (Button btn in buttons)
        {
            btn.onClick.RemoveListener(PlaySound);
            btn.onClick.AddListener(PlaySound);
        }
    }

    void PlaySound()
    {
        AudioSource src = GetFreeSFXSource();
        src.PlayOneShot(clickSound);
    }


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }


        ApplyVolumes();

    }

    void Update()
    {
        // Se a playlist está ativa, a música parou e NÃO estamos no meio de uma espera
        if (isPlaylistActive && !musicSource.isPlaying && !isWaitingNextTrack)
        {
            StartCoroutine(WaitAndPlayNext());
        }
    }

    // --- SISTEMA DE PLAYLIST COM PAUSA ---

    public void PlayMusicPlaylist(AudioClip[] playlist)
    {
        if (playlist == null || playlist.Length == 0) return;
        if (currentPlaylist == playlist) return;

        // Para qualquer espera atual se uma nova playlist for carregada
        StopAllCoroutines();
        isWaitingNextTrack = false;

        currentPlaylist = playlist;
        currentTrackIndex = 0;
        isPlaylistActive = true;

        PlayTrack(currentTrackIndex);
    }

    private void PlayTrack(int index)
    {
        if (currentPlaylist == null || index >= currentPlaylist.Length) return;

        musicSource.clip = currentPlaylist[index];
        musicSource.loop = false;
        musicSource.Play();
        isWaitingNextTrack = false;
    }

    // Corrotina para gerenciar a pausa
    IEnumerator WaitAndPlayNext()
    {
        isWaitingNextTrack = true; // Bloqueia o Update de chamar a corrotina várias vezes

        yield return new WaitForSeconds(delayBetweenTracks);

        currentTrackIndex++;
        if (currentTrackIndex >= currentPlaylist.Length)
        {
            currentTrackIndex = 0;
        }

        PlayTrack(currentTrackIndex);
    }

    // --- RESTANTE DO SCRIPT (SFX / VOLUMES) ---

    public void ApplyVolumes()
    {
        //var s = SettingsManager.Instance.Settings;
        //AudioListener.volume = s.masterVolume;
        //if (musicSource != null) musicSource.volume = s.musicVolume;
        //foreach (AudioSource sfx in sfxSources) { if (sfx != null) sfx.volume = s.sfxVolume; }
    }

    public void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        AudioSource source = GetFreeSFXSource();
        source.pitch = pitch;
        source.clip = clip;
        source.Play();
    }

    AudioSource GetFreeSFXSource()
    {
        foreach (AudioSource sfx in sfxSources) { if (!sfx.isPlaying) return sfx; }
        return sfxSources[0];
    }
}