using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// GESTOR DE AUDIO DEL JUEGO
// -------------------------------------------------------------------------------------------
// Se crea SOLO al arrancar el juego (no hay que ponerlo en ninguna escena) y sobrevive a los
// cambios de escena. No hace falta tocar ningún otro script: escucha lo que ya existe.
//
//  MÚSICA
//   - Cada escena tiene su música (tabla SceneMusic). Si la siguiente escena usa la misma
//     pista, sigue sonando sin cortes; si es otra, hace un fundido cruzado.
//   - Partido: al llegar al gol de oro cambia a music_goldengoal.
//   - Pausa (PauseController.PauseChanged): music_match pasa a music_pause en el MISMO punto
//     (son igual de largas), así suena "amortiguada" sin perder el compás.
//   - Fin del partido (GameManager.MatchFinished): stinger de victoria / derrota / campeón /
//     eliminado y después la música del cuadro (torneo) o del menú (amistoso).
//
//  EFECTOS DE INTERFAZ
//   - A todos los botones, toggles, sliders y desplegables uGUI que aparezcan se les añade
//     UISoundHook automáticamente (hover, clic, volver, jugar, desactivado...).
//   - Cartas de mejora: suena al aparecer y al elegir (según la rareza de la mejora).
//
//  Los audios se cargan de Resources/Audio/Music y Resources/Audio/SFX por nombre.
//  Volumen: GameSettings.MusicVolume y GameSettings.SfxVolume (el menú de Ajustes).
//
//  Desde cualquier script podéis usar:
//     AudioManager.PlayUI("ui_click");          // efecto por nombre de archivo (sin .wav)
//     AudioManager.PlayMusic("music_hub");       // forzar una música
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const string MusicFolder = "Audio/Music/";
    const string SfxFolder = "Audio/SFX/";

    // ---------- Música de cada escena ----------
    // Clave: nombre de la escena en minúsculas y SIN acentos ("Configuració" -> "configuracio").
    // Si renombráis una escena o añadís otra, cambiadlo aquí.
    static readonly Dictionary<string, string> SceneMusic = new Dictionary<string, string>
    {
        { "mainmenu", "music_menu" },
        { "configuracio", "music_menu" },
        { "teamselection", "music_menu" },
        { "credits", "music_credits" },
        { "tournament", "music_hub" },
        { "match", "music_match" },
    };

    // ---------- Volumen base de cada efecto (1 = tal cual está el .wav) ----------
    static readonly Dictionary<string, float> SfxGain = new Dictionary<string, float>
    {
        { "ui_hover", 0.55f }, { "ui_click", 0.55f }, { "ui_back", 0.6f }, { "ui_deny", 0.6f },
        { "ui_slider", 0.5f }, { "ui_pause_open", 0.7f }, { "ui_pause_close", 0.7f },
        { "ui_cards_appear", 0.8f }, { "ui_card_pick_common", 0.75f }, { "ui_card_pick_rare", 0.8f },
        { "ui_card_pick_epic", 0.85f }, { "ui_play_match", 0.8f },
    };

    // Tiempo mínimo entre dos veces el mismo efecto (evita "ametralladoras" al pasar el ratón)
    static readonly Dictionary<string, float> SfxCooldown = new Dictionary<string, float>
    {
        { "ui_hover", 0.05f }, { "ui_slider", 0.06f },
    };

    const float MusicGain = 0.8f;        // volumen general de la música respecto a los efectos
    const float CrossfadeTime = 0.8f;    // fundido entre dos músicas (s)
    const float PauseFadeTime = 0.25f;   // fundido al pausar / reanudar (s)
    const float PausedDuck = 0.35f;      // volumen de la música en pausa cuando no hay versión "pause"
    const int SfxVoices = 8;             // efectos que pueden sonar a la vez

    class Channel
    {
        public AudioSource source;
        public float level;              // 0..1, lo que suena ahora
        public float target;             // a dónde va
        public float speed = 1f;         // unidades por segundo
        public bool stopAtZero;          // al llegar a 0, Stop()
    }

    readonly Channel[] music = new Channel[2];
    Channel pauseChannel, stingerChannel;
    int active;
    readonly List<AudioSource> sfxVoices = new List<AudioSource>();
    int nextVoice;

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly HashSet<string> missing = new HashSet<string>();
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    GameManager trackedGame;
    bool goldenGoalMusic;
    bool paused;
    Coroutine pendingMusic;
    float nextScan;
    int lastUpgradeCount = -1;
    bool draftVisible;
    bool warnedPoll;
    string sceneTrack;          // música que debería sonar en la escena actual
    float nextWatchdog;

    // ---------- Arranque automático ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < 2; i++) music[i] = NewChannel("Music " + i);
        pauseChannel = NewChannel("Music Pause");
        stingerChannel = NewChannel("Stinger");

        for (int i = 0; i < SfxVoices; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.ignoreListenerPause = true;   // la interfaz suena aunque el juego esté en pausa
            s.spatialBlend = 0f;
            sfxVoices.Add(s);
        }
    }

    Channel NewChannel(string label)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.ignoreListenerPause = true;       // PauseController pausa el AudioListener: la música sigue
        s.spatialBlend = 0f;
        s.volume = 0f;
        return new Channel { source = s };
    }

    void Start()
    {
        // La primera escena del juego ya está cargada cuando el AudioManager arranca:
        // si su evento sceneLoaded no ha llegado, se arranca aquí su música.
        if (sceneTrack == null) OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PauseController.PauseChanged += OnPauseChanged;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PauseController.PauseChanged -= OnPauseChanged;
        if (trackedGame != null) trackedGame.MatchFinished -= OnMatchFinished;
    }

    // ---------- API pública ----------

    public static void PlayUI(string name, float pitchJitter = 0f)
    {
        if (Instance != null) Instance.PlaySfx(name, pitchJitter);
    }

    public static void PlayMusic(string name)
    {
        if (Instance != null) Instance.StartMusic(name);
    }

    // Para otros scripts de audio (MatchAudio): devuelve el clip de Resources/Audio/SFX
    public static AudioClip GetSfx(string name)
    {
        return Instance != null ? Instance.Load(SfxFolder + name) : Resources.Load<AudioClip>(SfxFolder + name);
    }

    public static void StopMusic(float fadeSeconds = 0.5f)
    {
        if (Instance != null) Instance.FadeOutMusic(fadeSeconds);
    }

    // ---------- Escenas ----------

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;   // p. ej. Ajustes abiertos encima de la pausa

        ResetPause();
        lastUpgradeCount = -1;
        draftVisible = false;
        goldenGoalMusic = false;

        string track = MusicForScene(scene);
        if (track == null) return;
        sceneTrack = track;

        if (pendingMusic != null) StopCoroutine(pendingMusic);
        // Si aún suena un stinger (p. ej. "¡CAMPEONES!"), la música de la escena entra cuando acaba
        if (stingerChannel.source.isPlaying && stingerChannel.source.clip != null)
        {
            float left = stingerChannel.source.clip.length - stingerChannel.source.time;
            pendingMusic = StartCoroutine(MusicAfter(track, left - 0.5f));
        }
        else
        {
            pendingMusic = null;
            StartMusic(track);
        }
    }

    static string MusicForScene(Scene scene)
    {
        string track;
        if (SceneMusic.TryGetValue(Key(scene.name), out track)) return track;
        // Escenas de prueba con un partido (David, Alex, SampleScene...)
        if (FindAnyObjectByType<GameManager>() != null) return "music_match";
        return null;   // escena desconocida: se mantiene la música que sonaba
    }

    IEnumerator MusicAfter(string track, float seconds)
    {
        if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
        pendingMusic = null;
        StartMusic(track);
    }

    // ---------- Música ----------

    void StartMusic(string name)
    {
        AudioClip clip = Load(MusicFolder + name);
        if (clip == null) return;

        Channel cur = music[active];
        if (cur.source.clip == clip && cur.source.isPlaying)
        {
            Fade(cur, paused ? cur.target : 1f, CrossfadeTime, false);   // ya sonaba: que siga
            return;
        }

        int next = 1 - active;
        Channel n = music[next];
        n.source.Stop();
        n.source.clip = clip;
        n.source.loop = true;
        n.source.time = 0f;
        n.level = 0f;
        n.source.Play();
        Fade(n, 1f, CrossfadeTime, false);
        Fade(cur, 0f, CrossfadeTime, true);
        active = next;
    }

    void FadeOutMusic(float seconds)
    {
        Fade(music[0], 0f, seconds, true);
        Fade(music[1], 0f, seconds, true);
        Fade(pauseChannel, 0f, seconds, true);
    }

    float PlayStinger(string name)
    {
        AudioClip clip = Load(MusicFolder + name);
        if (clip == null) return 0f;
        Channel c = stingerChannel;
        c.source.Stop();
        c.source.clip = clip;
        c.source.loop = false;
        c.level = 1f;
        c.target = 1f;
        c.stopAtZero = false;
        c.source.Play();
        return clip.length;
    }

    static void Fade(Channel c, float target, float seconds, bool stopAtZero)
    {
        c.target = target;
        c.speed = 1f / Mathf.Max(0.01f, seconds);
        c.stopAtZero = stopAtZero;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;   // funciona con el juego en pausa o en cámara lenta
        float musicVolume = GameSettings.MusicVolume * MusicGain;

        StepChannel(music[0], dt, musicVolume);
        StepChannel(music[1], dt, musicVolume);
        StepChannel(pauseChannel, dt, musicVolume);
        StepChannel(stingerChannel, dt, musicVolume);

        PollMatch();
        Watchdog();

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            HookNewSelectables();
            PollUpgrades();
        }
    }

    // Red de seguridad: si la escena tiene música y no suena nada (y no es un silencio buscado,
    // como el stinger del final del partido), se vuelve a poner.
    void Watchdog()
    {
        if (Time.unscaledTime < nextWatchdog) return;
        nextWatchdog = Time.unscaledTime + 0.5f;
        if (sceneTrack == null || pendingMusic != null || stingerChannel.source.isPlaying) return;
        if (music[0].source.isPlaying || music[1].source.isPlaying) return;
        StartMusic(sceneTrack);
    }

    static void StepChannel(Channel c, float dt, float volume)
    {
        c.level = Mathf.MoveTowards(c.level, c.target, c.speed * dt);
        c.source.volume = c.level * volume;
        if (c.stopAtZero && c.level <= 0f && c.source.isPlaying) c.source.Stop();
    }

    // ---------- Pausa ----------

    void OnPauseChanged(bool isPaused)
    {
        PlaySfx(isPaused ? "ui_pause_open" : "ui_pause_close", 0f);
        if (isPaused) EnterPause();
        else ExitPause();
    }

    void EnterPause()
    {
        paused = true;
        Channel cur = music[active];
        if (!cur.source.isPlaying || cur.source.clip == null) return;

        AudioClip pauseClip = cur.source.clip.name == "music_match" ? Load(MusicFolder + "music_pause") : null;
        if (pauseClip != null && pauseClip.samples == cur.source.clip.samples)
        {
            // La versión "amortiguada" entra en el mismo punto de la canción
            AudioSource p = pauseChannel.source;
            p.clip = pauseClip;
            p.loop = true;
            p.Play();
            p.timeSamples = cur.source.timeSamples;
            pauseChannel.level = 0f;
            Fade(pauseChannel, 1f, PauseFadeTime, false);
            Fade(cur, 0f, PauseFadeTime, false);   // sigue sonando en silencio para no desincronizarse
        }
        else
        {
            Fade(cur, PausedDuck, PauseFadeTime, false);
        }
    }

    void ExitPause()
    {
        paused = false;
        Channel cur = music[active];
        if (cur.source.isPlaying) Fade(cur, 1f, PauseFadeTime, false);
        if (pauseChannel.source.isPlaying) Fade(pauseChannel, 0f, PauseFadeTime, true);
    }

    void ResetPause()
    {
        // Salir al menú desde la pausa no lanza PauseChanged(false): se limpia aquí
        if (!paused && !pauseChannel.source.isPlaying) return;
        paused = false;
        pauseChannel.source.Stop();
        pauseChannel.level = 0f;
        Channel cur = music[active];
        if (cur.source.isPlaying && !cur.stopAtZero) Fade(cur, 1f, PauseFadeTime, false);
    }

    // ---------- Partido ----------

    void PollMatch()
    {
        GameManager gm = GameManager.Instance;
        if (gm != trackedGame)
        {
            if (trackedGame != null) trackedGame.MatchFinished -= OnMatchFinished;
            trackedGame = gm;
            goldenGoalMusic = false;
            if (gm != null)
            {
                gm.MatchFinished += OnMatchFinished;
                if (gm.GetComponent<MatchAudio>() == null) gm.gameObject.AddComponent<MatchAudio>();
            }
        }

        if (gm != null && gm.IsGoldenGoal && !goldenGoalMusic && gm.State != MatchState.FullTime)
        {
            goldenGoalMusic = true;
            StartMusic("music_goldengoal");
        }
    }

    void OnMatchFinished(int homeGoals, int awayGoals)
    {
        StartCoroutine(MatchResult(homeGoals, awayGoals, trackedGame));
    }

    IEnumerator MatchResult(int homeGoals, int awayGoals, GameManager gm)
    {
        sceneTrack = null;   // silencio buscado: que el vigilante no vuelva a poner la música
        yield return null;   // un frame: TournamentMatchBridge ya ha apuntado el resultado en el torneo

        Team player = gm != null ? gm.PlayerTeam : Team.Home;
        int mine = player == Team.Home ? homeGoals : awayGoals;
        int theirs = player == Team.Home ? awayGoals : homeGoals;
        bool tournament = MatchLaunch.Mode == MatchMode.Tournament && TournamentSession.IsActive;

        string stinger = null;
        if (tournament && TournamentSession.IsChampion) stinger = "stinger_champion";
        else if (tournament && TournamentSession.PlayerEliminated) stinger = "stinger_eliminated";
        else if (mine > theirs) stinger = "stinger_victory";
        else if (mine < theirs) stinger = "stinger_defeat";

        FadeOutMusic(0.4f);
        if (pendingMusic != null) StopCoroutine(pendingMusic);
        pendingMusic = null;
        yield return new WaitForSecondsRealtime(1.0f);   // primero el triple silbato final (MatchAudio)
        float length = stinger != null ? PlayStinger(stinger) : 0f;

        if (pendingMusic != null) StopCoroutine(pendingMusic);
        pendingMusic = StartCoroutine(MusicAfter(tournament ? "music_hub" : "music_menu", Mathf.Max(0.6f, length - 0.6f)));
    }

    // ---------- Interfaz ----------

    void HookNewSelectables()
    {
        Selectable[] all = Selectable.allSelectablesArray;   // solo los activos
        for (int i = 0; i < all.Length; i++)
        {
            Selectable s = all[i];
            if (s == null || s.GetComponent<UISoundHook>() != null) continue;
            s.gameObject.AddComponent<UISoundHook>().Init(s);
        }
    }

    // Mejoras del torneo: sonido al aparecer las cartas y al elegir una (según su rareza)
    void PollUpgrades()
    {
        try
        {
            if (!TournamentSession.IsActive || TournamentSession.PlayerTeam < 0)
            {
                lastUpgradeCount = -1;
                draftVisible = false;
                return;
            }

            var ids = TournamentSession.UpgradeIdsOf(TournamentSession.PlayerTeam);
            int count = ids.Count;
            bool picked = lastUpgradeCount >= 0 && count > lastUpgradeCount;
            if (picked)
            {
                UpgradeData u = TournamentSession.Upgrade(ids[count - 1]);
                UpgradeRarity rarity = u != null ? u.Rarity : UpgradeRarity.Comun;
                PlaySfx(rarity == UpgradeRarity.Epica ? "ui_card_pick_epic"
                      : rarity == UpgradeRarity.Rara ? "ui_card_pick_rare" : "ui_card_pick_common", 0f);
            }
            lastUpgradeCount = count;

            bool visible = FindAnyObjectByType<UpgradeDraftPanel>() != null;
            if (visible && (!draftVisible || (picked && TournamentSession.PlayerPicksPending > 0)))
                StartCoroutine(PlayDelayed("ui_cards_appear", picked ? 0.45f : 0.1f));
            draftVisible = visible;
        }
        catch (System.Exception e)
        {
            if (!warnedPoll) Debug.LogWarning("[AudioManager] No se pudo leer el estado de las mejoras: " + e.Message);
            warnedPoll = true;
        }
    }

    IEnumerator PlayDelayed(string name, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        PlaySfx(name, 0f);
    }

    void PlaySfx(string name, float pitchJitter)
    {
        float now = Time.unscaledTime;
        float cooldown, last;
        if (SfxCooldown.TryGetValue(name, out cooldown) && lastPlayed.TryGetValue(name, out last) && now - last < cooldown)
            return;

        AudioClip clip = Load(SfxFolder + name);
        if (clip == null) return;
        lastPlayed[name] = now;

        float gain;
        if (!SfxGain.TryGetValue(name, out gain)) gain = 1f;

        AudioSource voice = sfxVoices[nextVoice];
        nextVoice = (nextVoice + 1) % sfxVoices.Count;
        voice.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
        voice.PlayOneShot(clip, gain * GameSettings.SfxVolume);
    }

    // ---------- Utilidades ----------

    AudioClip Load(string path)
    {
        AudioClip clip;
        if (clips.TryGetValue(path, out clip)) return clip;
        clip = Resources.Load<AudioClip>(path);
        if (clip == null)
        {
            if (missing.Add(path)) Debug.LogWarning("[AudioManager] No encuentro Resources/" + path + ".wav");
            return null;
        }
        clips[path] = clip;
        return clip;
    }

    // "Configuració" -> "configuracio"
    public static string Key(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string d = text.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(d.Length);
        foreach (char c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString().Trim();
    }
}
