using System.Collections.Generic;
using UnityEngine;

// SONIDOS DEL PARTIDO
// -------------------------------------------------------------------------------------------
// Lo añade AudioManager automáticamente al GameManager de cada partido (no hay que ponerlo a mano).
// Observa el estado del juego cada frame y lanza los sonidos; no modifica nada del partido.
//
//  Bola y stick ... tiro, pase, golpe de stick al aire, robo, intercepción, choque de sticks,
//                   rebote en la valla (3 fuerzas), parada del portero, control de bola
//  Jugadores ...... patines (loop según la velocidad), sprint, agotado, regate, choques
//  QTE / lenta .... entrada y salida de cámara lenta, tic de la barra, ¡PARADÓN! / ¡FALLO!
//  Flujo .......... silbato de saque, descanso, final, bocina y público en el gol,
//                   "oooh" del público en las paradas, ambiente del pabellón, moneda del sorteo
public class MatchAudio : MonoBehaviour
{
    // Volumen base de cada sonido (1 = tal cual está el .wav)
    static readonly Dictionary<string, float> Gain = new Dictionary<string, float>
    {
        { "sfx_shot_1", 0.9f }, { "sfx_shot_2", 0.9f }, { "sfx_pass", 0.6f }, { "sfx_stick_swing", 0.5f },
        { "sfx_steal", 0.8f }, { "sfx_intercept", 0.75f }, { "sfx_stick_block", 0.7f },
        { "sfx_ball_wall_soft", 0.5f }, { "sfx_ball_wall_mid", 0.65f }, { "sfx_ball_wall_hard", 0.8f },
        { "sfx_juke", 0.65f }, { "sfx_keeper_save", 0.85f }, { "sfx_ball_pickup", 0.45f },
        { "sfx_sprint", 0.5f }, { "sfx_exhausted", 0.55f }, { "sfx_player_bump", 0.6f },
        { "sfx_slowmo_in", 0.7f }, { "sfx_slowmo_out", 0.6f }, { "sfx_qte_success", 0.8f },
        { "sfx_qte_fail", 0.7f }, { "sfx_qte_tick", 0.45f },
        { "sfx_whistle_short", 0.7f }, { "sfx_whistle_long", 0.7f }, { "sfx_whistle_end", 0.75f },
        { "sfx_goal_horn", 0.75f }, { "sfx_crowd_cheer", 0.8f }, { "sfx_crowd_ooh", 0.6f },
    };

    const float CrowdLoopVolume = 0.3f;
    const float SkateVolume = 0.45f;
    const float OthersSkateVolume = 0.18f;
    const float QteTickInterval = 0.12f;   // segundos reales entre tics de la barra del QTE
    const float InterceptMinSpeed = 9f;     // bola más rápida que esto + robada al rival = intercepción
    const float SaveDistance = 1.8f;        // distancia bola-portero para considerar que ha parado

    GameManager gm;
    Ball ball;
    Camera cam;
    readonly List<AudioSource> voices = new List<AudioSource>();
    int nextVoice;
    AudioSource skateMain, skateOthers, crowdLoop;
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    // Estado anterior (para detectar cambios)
    MatchState lastState;
    bool firstFrame = true;
    int lastShotId, lastDeflectId;
    Skater lastHolder;
    bool lastIsShot;
    float lastBallSpeed;
    Team lastTouch;
    class SkaterState { public bool sprint, exhausted, juke; }
    readonly Dictionary<Skater, SkaterState> skaterStates = new Dictionary<Skater, SkaterState>();

    GoalkeeperQTE[] qtes = new GoalkeeperQTE[0];
    QTEState[] lastQte = new QTEState[0];
    Goalkeeper[] keepers = new Goalkeeper[0];
    float nextTick;
    bool lastBulletTime;

    Transform feedbackRoot;
    readonly HashSet<int> seenSwings = new HashSet<int>();
    float nextLookup;

    void Awake()
    {
        gm = GetComponent<GameManager>();

        for (int i = 0; i < 8; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            voices.Add(s);
        }
        skateMain = NewLoop("sfx_skate_loop");
        skateOthers = NewLoop("sfx_skate_loop");
        crowdLoop = NewLoop("sfx_crowd_loop");

        // Sorteo: CoinTossScreen ya tiene huecos para estos sonidos; si están vacíos, se rellenan
        if (gm != null)
        {
            if (gm.coinFlipClip == null) gm.coinFlipClip = AudioManager.GetSfx("sfx_coin_flip");
            if (gm.coinLandClip == null) gm.coinLandClip = AudioManager.GetSfx("sfx_coin_land");
        }
    }

    AudioSource NewLoop(string clipName)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.loop = true;
        s.volume = 0f;
        s.clip = AudioManager.GetSfx(clipName);
        if (s.clip != null)
        {
            s.Play();
            s.timeSamples = Random.Range(0, s.clip.samples);   // que los dos loops de patines no vayan iguales
        }
        return s;
    }

    void Start()
    {
        RefreshReferences();
    }

    void RefreshReferences()
    {
        ball = Ball.Instance;
        cam = Camera.main;
        qtes = FindObjectsByType<GoalkeeperQTE>(FindObjectsSortMode.None);
        lastQte = new QTEState[qtes.Length];
        keepers = FindObjectsByType<Goalkeeper>(FindObjectsSortMode.None);

        if (ball != null && ball.GetComponent<MatchAudioRelay>() == null)
            ball.gameObject.AddComponent<MatchAudioRelay>().Setup(this, MatchAudioRelay.Kind.Ball);
        foreach (Skater s in Skater.All)
            if (s != null && s.GetComponent<MatchAudioRelay>() == null)
                s.gameObject.AddComponent<MatchAudioRelay>().Setup(this, MatchAudioRelay.Kind.Skater);
    }

    void Update()
    {
        if (gm == null) return;

        if (Time.unscaledTime >= nextLookup)
        {
            nextLookup = Time.unscaledTime + 1f;
            if (ball == null || qtes.Length == 0 || cam == null) RefreshReferences();
            if (feedbackRoot == null)
            {
                GameObject f = GameObject.Find("Feedback");
                if (f != null) feedbackRoot = f.transform;
            }
        }

        UpdateFlow();
        UpdateBall();
        UpdateSwings();
        UpdateSkaters();
        UpdateQte();
        UpdateLoops();
    }

    // ---------- Flujo del partido ----------

    void UpdateFlow()
    {
        MatchState state = gm.State;
        if (firstFrame) { lastState = state; firstFrame = false; return; }
        if (state == lastState) return;

        if (lastState == MatchState.Kickoff && state == MatchState.Playing) Play("sfx_whistle_short");
        else if (state == MatchState.GoalPause)
        {
            Play("sfx_goal_horn");
            Play("sfx_crowd_cheer");
        }
        else if (state == MatchState.HalfTime || state == MatchState.OvertimeBreak) Play("sfx_whistle_long");
        else if (state == MatchState.FullTime) Play("sfx_whistle_end");

        lastState = state;
    }

    // ---------- Bola ----------

    void UpdateBall()
    {
        if (ball == null) return;
        Vector2 pos = ball.rb.position;
        bool playing = gm.State == MatchState.Playing || gm.State == MatchState.Kickoff;

        // Tiro nuevo
        if (ball.ShotId != lastShotId)
        {
            if (playing) Play(Random.value < 0.5f ? "sfx_shot_1" : "sfx_shot_2", pos, 0.06f);
            lastShotId = ball.ShotId;
        }

        // Un jugador ha tocado el tiro y le ha cambiado la trayectoria
        if (ball.DeflectId != lastDeflectId)
        {
            if (playing) Play("sfx_stick_block", pos, 0.08f);
            lastDeflectId = ball.DeflectId;
        }

        // Cambios de quién lleva la bola
        Skater holder = ball.Holder;
        if (holder != lastHolder && playing)
        {
            if (holder != null && lastHolder != null && holder.team != lastHolder.team)
                Play("sfx_steal", pos, 0.05f);                                  // robo con el stick
            else if (holder != null && lastHolder == null)
            {
                if (lastBallSpeed > InterceptMinSpeed && lastTouch != holder.team) Play("sfx_intercept", pos, 0.05f);
                else Play("sfx_ball_pickup", pos, 0.1f);
            }
            else if (holder == null && lastHolder != null && ball.IsPassFor(lastHolder.team))
                Play("sfx_pass", pos, 0.08f);                                   // pase
        }

        // Parada: el tiro deja de serlo junto a un portero (con choque o con el QTE)
        if (lastIsShot && !ball.IsShot && !ball.IsHeld && gm.State == MatchState.Playing && NearKeeper(pos))
        {
            Play("sfx_keeper_save", pos, 0.05f);
            if (lastBallSpeed > 12f && Random.value < 0.65f) Play("sfx_crowd_ooh");
        }

        lastHolder = holder;
        lastIsShot = ball.IsShot;
        lastBallSpeed = ball.Speed;
        lastTouch = ball.LastTouchTeam;
    }

    bool NearKeeper(Vector2 p)
    {
        foreach (Goalkeeper k in keepers)
            if (k != null && Vector2.Distance(k.transform.position, p) < SaveDistance) return true;
        return false;
    }

    // Lo llama MatchAudioRelay (componente que se pone solo en la bola)
    public void OnBallCollision(Collision2D c)
    {
        if (gm == null || gm.State != MatchState.Playing) return;
        if (c.collider.GetComponent<Skater>() != null) return;                 // controles y robos ya suenan
        float speed = c.relativeVelocity.magnitude;
        Vector2 pos = c.contactCount > 0 ? c.GetContact(0).point : (ball != null ? ball.rb.position : Vector2.zero);

        if (c.collider.GetComponent<Goalkeeper>() != null)
        {
            if (speed > 4f) Play("sfx_keeper_save", pos, 0.05f);
            return;
        }
        if (speed > 15f) Play("sfx_ball_wall_hard", pos, 0.06f);
        else if (speed > 7f) Play("sfx_ball_wall_mid", pos, 0.06f);
        else if (speed > 2.5f) Play("sfx_ball_wall_soft", pos, 0.08f);
    }

    // Lo llama MatchAudioRelay en cada jugador
    public void OnSkaterCollision(Skater self, Collision2D c)
    {
        if (gm == null || gm.State != MatchState.Playing) return;
        if (c.collider.GetComponent<Skater>() == null) return;
        if (c.relativeVelocity.magnitude < 4f) return;
        Play("sfx_player_bump", self.transform.position, 0.08f);
    }

    // ---------- Golpes de stick (Feedback dibuja un arco "StickSwing" en cada golpe) ----------

    void UpdateSwings()
    {
        if (feedbackRoot == null) return;
        for (int i = 0; i < feedbackRoot.childCount; i++)
        {
            Transform child = feedbackRoot.GetChild(i);
            if (child.name != "StickSwing" || !seenSwings.Add(child.GetInstanceID())) continue;
            LineRenderer lr = child.GetComponent<LineRenderer>();
            Vector2 at = lr != null && lr.positionCount > 0 ? (Vector2)lr.GetPosition(0) : Vector2.zero;
            Play("sfx_stick_swing", at, 0.1f);
        }
        if (seenSwings.Count > 500) seenSwings.Clear();
    }

    // ---------- Jugadores ----------

    void UpdateSkaters()
    {
        bool playing = gm.State == MatchState.Playing;
        foreach (Skater s in Skater.All)
        {
            if (s == null) continue;
            SkaterState st;
            if (!skaterStates.TryGetValue(s, out st)) { st = new SkaterState(); skaterStates[s] = st; }

            bool juke = s.IsDribbling;
            if (juke && !st.juke && playing) Play("sfx_juke", s.transform.position, 0.06f);
            st.juke = juke;

            // Sprint y agotamiento: solo el jugador que controla el humano (si no, sería un ruido constante)
            bool sprint = s.IsSprinting;
            if (sprint && !st.sprint && playing && s.IsControlled) Play("sfx_sprint", s.transform.position, 0.06f);
            st.sprint = sprint;

            bool tired = s.IsExhausted;
            if (tired && !st.exhausted && playing && s.IsControlled) Play("sfx_exhausted", s.transform.position, 0.05f);
            st.exhausted = tired;
        }
    }

    void UpdateLoops()
    {
        float sfx = GameSettings.SfxVolume;
        bool playing = gm.State == MatchState.Playing || gm.State == MatchState.Kickoff;
        float dt = Time.unscaledDeltaTime;

        float mine = 0f, others = 0f;
        int otherCount = 0;
        foreach (Skater s in Skater.All)
        {
            if (s == null) continue;
            float v = Mathf.Clamp01(s.Velocity.magnitude / Mathf.Max(1f, s.TopSpeed * 1.3f));
            if (s.IsControlled) mine = Mathf.Max(mine, v);
            else { others += v; otherCount++; }
        }
        if (otherCount > 0) others /= otherCount;
        if (!playing) { mine = 0f; others = 0f; }

        float slow = Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(Time.timeScale));     // en cámara lenta suenan más graves
        skateMain.volume = Mathf.MoveTowards(skateMain.volume, Mathf.Pow(mine, 1.2f) * SkateVolume * sfx, dt * 3f);
        skateMain.pitch = (0.8f + 0.4f * mine) * slow;
        skateOthers.volume = Mathf.MoveTowards(skateOthers.volume, others * OthersSkateVolume * sfx, dt * 2f);
        skateOthers.pitch = (0.75f + 0.35f * others) * slow;
        crowdLoop.volume = Mathf.MoveTowards(crowdLoop.volume, CrowdLoopVolume * sfx, dt * 0.5f);
    }

    // ---------- QTE del portero y cámara lenta del tiro ----------

    void UpdateQte()
    {
        bool anyRunning = false;
        for (int i = 0; i < qtes.Length; i++)
        {
            if (qtes[i] == null) continue;
            QTEState s = qtes[i].State;
            QTEState prev = lastQte[i];
            if (s != prev)
            {
                if (s == QTEState.Running) { Play("sfx_slowmo_in"); nextTick = Time.unscaledTime + 0.2f; }
                else if (s == QTEState.Success && prev == QTEState.Running) { Play("sfx_qte_success"); Play("sfx_slowmo_out"); }
                else if (s == QTEState.Fail && prev == QTEState.Running) { Play("sfx_qte_fail"); Play("sfx_slowmo_out"); }
                lastQte[i] = s;
            }
            if (s == QTEState.Running) anyRunning = true;
        }

        if (anyRunning && Time.unscaledTime >= nextTick)
        {
            nextTick = Time.unscaledTime + QteTickInterval;
            Play("sfx_qte_tick");
        }

        // Bullet time al apuntar un tiro
        BulletTime bt = BulletTime.Instance;
        bool active = bt != null && bt.Active;
        if (active != lastBulletTime && !anyRunning) Play(active ? "sfx_slowmo_in" : "sfx_slowmo_out");
        lastBulletTime = active;
    }

    // ---------- Reproducción ----------

    void Play(string name) { Play(name, null, 0f); }

    void Play(string name, Vector2? at, float pitchJitter)
    {
        float now = Time.unscaledTime;
        float last;
        if (lastPlayed.TryGetValue(name, out last) && now - last < 0.04f) return;   // evita duplicados en el mismo instante

        AudioClip clip = AudioManager.GetSfx(name);
        if (clip == null) return;
        lastPlayed[name] = now;

        float gain;
        if (!Gain.TryGetValue(name, out gain)) gain = 1f;

        AudioSource v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Count;
        v.panStereo = at.HasValue ? Pan(at.Value) : 0f;
        v.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
        v.PlayOneShot(clip, gain * GameSettings.SfxVolume);
    }

    // Izquierda / derecha según dónde pasa en la pista (suave, para no marear)
    float Pan(Vector2 p)
    {
        if (cam == null) return 0f;
        float half = cam.orthographic ? cam.orthographicSize * cam.aspect : 10f;
        return Mathf.Clamp((p.x - cam.transform.position.x) / Mathf.Max(1f, half), -1f, 1f) * 0.6f;
    }
}
