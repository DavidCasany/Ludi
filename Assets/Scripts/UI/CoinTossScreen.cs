using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Sorteo de inicio de partido con el estilo del resto de menús (la crea GameManager en su Awake).
//
//   1. ELEGIR    · moneda flotando + CARA / CRUZ   (clic, flechas + Enter, o teclas 1 / 2)
//   2. LANZAR    · la moneda sube girando y cae   (~1,7 s de suspense; aquí va el sonido de lanzar)
//   3. RESULTADO · ¡GANAS EL SORTEO! / PIERDES EL SORTEO  +  "Saca XXX"  (sonido al caer)
//   4. se desvanece y GameManager empieza el saque
//
// El resultado lo decide GameManager (ChooseCoin); esta pantalla solo lo enseña.
// Va con tiempo escalado: si se pausa el juego a mitad de sorteo, la moneda se para también.
public class CoinTossScreen : MonoBehaviour
{
    const float CoinSize = 230f;
    const float FlipTime = 1.7f;      // suspense entre la elección y el resultado
    const float ResultTime = 1.6f;    // cuánto se ve el resultado
    const float FadeTime = 0.3f;
    const float FlipHeight = 190f;

    static readonly Color HeadsFace = new Color(1f, 0.82f, 0.3f);
    static readonly Color HeadsRim = new Color(0.78f, 0.56f, 0.12f);
    static readonly Color HeadsInk = new Color(0.33f, 0.22f, 0.04f);
    static readonly Color TailsFace = new Color(0.84f, 0.88f, 0.94f);
    static readonly Color TailsRim = new Color(0.52f, 0.58f, 0.67f);
    static readonly Color TailsInk = new Color(0.2f, 0.25f, 0.32f);

    enum Phase { Choose, Flip, Result, FadeOut, Done }

    GameManager gm;
    CanvasGroup group, choiceGroup;
    RectTransform coin, shadow;
    Image coinRim, coinFace, coinInner;
    TextMeshProUGUI coinLabel, pickText, resultTitle, resultSub;
    Button headsButton, tailsButton;
    Vector2 coinRest;

    Phase phase = Phase.Choose;
    float t;
    bool pickedHeads, resultHeads, playerWon;
    float targetAngle;

    public static CoinTossScreen Create(GameManager gm)
    {
        Canvas canvas = UIKit.CreateCanvas("CoinToss", 90);   // por debajo de la pausa (100)
        CoinTossScreen s = canvas.gameObject.AddComponent<CoinTossScreen>();
        s.gm = gm;
        gm.CoinTossHandledExternally = true;
        gm.CoinTossed += s.OnTossed;
        s.Build((RectTransform)canvas.transform);
        return s;
    }

    void Start()
    {
        // En Start y no en Create: al cargar la escena HudVisibility se reinicia después de los Awake
        HudVisibility.Hidden = true;
    }

    void OnDestroy()
    {
        if (gm != null) gm.CoinTossed -= OnTossed;
    }

    // ---------- Construcción ----------

    void Build(RectTransform root)
    {
        group = root.gameObject.AddComponent<CanvasGroup>();

        Image dim = UIKit.Image(root, "Dim", UIKit.Alpha(UIKit.Background, 0.78f), 0f, true);
        UIKit.Stretch(dim.rectTransform, 0f);

        TextMeshProUGUI title = UIKit.Text(root, "Title", "SORTEIG", 30f, UIKit.Accent, FontStyles.Bold);
        title.characterSpacing = 14f;
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(600f, 40f));

        // Sombra en el suelo (se encoge cuando la moneda sube)
        Image sh = UIKit.CircleImage(root, "Shadow", UIKit.Alpha(Color.black, 0.35f), CoinSize);
        shadow = sh.rectTransform;
        UIKit.Place(shadow, new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(CoinSize * 0.8f, 26f));
        sh.preserveAspect = false;

        // Moneda: canto + cara + texto
        coinRim = UIKit.CircleImage(root, "Coin", HeadsRim, CoinSize);
        coin = coinRim.rectTransform;
        coinRest = new Vector2(0f, 95f);
        UIKit.Place(coin, new Vector2(0.5f, 0.5f), coinRest, new Vector2(CoinSize, CoinSize));
        coinFace = UIKit.CircleImage(coin, "Face", HeadsFace, CoinSize * 0.84f);
        UIKit.Place(coinFace.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CoinSize * 0.84f, CoinSize * 0.84f));
        Image ring = UIKit.CircleImage(coin, "Ring", UIKit.Alpha(Color.white, 0.25f), CoinSize * 0.7f);
        UIKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CoinSize * 0.7f, CoinSize * 0.7f));
        // Aro fino claro: un círculo blanco translúcido con otro del color de la cara encima, algo menor
        coinInner = UIKit.CircleImage(ring.transform, "Inner", HeadsFace, CoinSize * 0.66f);
        UIKit.Place(coinInner.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CoinSize * 0.66f, CoinSize * 0.66f));
        coinLabel = UIKit.Text(coin, "Label", "CARA", 46f, HeadsInk, FontStyles.Bold);
        coinLabel.characterSpacing = 4f;
        UIKit.Stretch(coinLabel.rectTransform, 0f);

        // Elección
        RectTransform choice = UIKit.Rect(root, "Choice");
        UIKit.Stretch(choice, 0f);
        choiceGroup = choice.gameObject.AddComponent<CanvasGroup>();

        headsButton = UIKit.Button(choice, "Cara", "CARA", false, 36f);
        UIKit.Place((RectTransform)headsButton.transform, new Vector2(0.5f, 0.5f), new Vector2(-160f, -150f), new Vector2(280f, 96f));
        headsButton.onClick.AddListener(() => Pick(true));
        headsButton.gameObject.AddComponent<HoverPop>();

        tailsButton = UIKit.Button(choice, "Cruz", "CREU", false, 36f);
        UIKit.Place((RectTransform)tailsButton.transform, new Vector2(0.5f, 0.5f), new Vector2(160f, -150f), new Vector2(280f, 96f));
        tailsButton.onClick.AddListener(() => Pick(false));
        tailsButton.gameObject.AddComponent<HoverPop>();

        headsButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = tailsButton };
        tailsButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = headsButton };

        TextMeshProUGUI hint = UIKit.Text(choice, "Hint", "Qui guanyi, treu", 24f, UIKit.TextSoft);
        UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(600f, 34f));

        // Lo que se ve después de elegir
        pickText = UIKit.Text(root, "Pick", "", 28f, UIKit.TextSoft);
        UIKit.Place(pickText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(800f, 40f));

        resultTitle = UIKit.Text(root, "Result", "", 64f, UIKit.Good, FontStyles.Bold);
        resultTitle.characterSpacing = 6f;
        UIKit.Place(resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1200f, 80f));

        resultSub = UIKit.Text(root, "Serve", "", 30f, UIKit.TextMain, FontStyles.Bold);
        resultSub.characterSpacing = 6f;
        UIKit.Place(resultSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(800f, 40f));

        SetAlpha(pickText, 0f);
        SetAlpha(resultTitle, 0f);
        SetAlpha(resultSub, 0f);
        ShowFace(true, 1f);
    }

    // ---------- Flujo ----------

    void Pick(bool heads)
    {
        if (phase != Phase.Choose || PauseController.IsPaused) return;
        pickedHeads = heads;
        gm.ChooseCoin(heads);   // GameManager decide y llama a OnTossed
    }

    void OnTossed(bool heads, bool won)
    {
        resultHeads = heads;
        playerWon = won;

        // Medias vueltas: par = acaba en la cara con la que empieza (CARA), impar = CRUZ
        int halfTurns = 12 + (heads ? 0 : 1);
        targetAngle = Mathf.PI * halfTurns;

        pickText.text = "Has triat " + (pickedHeads ? "CARA" : "CREU");
        choiceGroup.interactable = choiceGroup.blocksRaycasts = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        phase = Phase.Flip;
        t = 0f;
        PlaySound(gm.coinFlipClip);
    }

    void Update()
    {
        float dt = Time.deltaTime;   // escalado: la pausa congela el sorteo
        t += dt;

        switch (phase)
        {
            case Phase.Choose: UpdateChoose(); break;
            case Phase.Flip: UpdateFlip(); break;
            case Phase.Result: UpdateResult(); break;
            case Phase.FadeOut: UpdateFadeOut(); break;
        }
    }

    void UpdateChoose()
    {
        // La moneda flota y gira despacio mientras se elige
        coin.anchoredPosition = coinRest + Vector2.up * Mathf.Sin(t * 2f) * 8f;
        float angle = Mathf.Sin(t * 1.3f) * 0.5f;
        ShowFace(true, Mathf.Cos(angle));

        if (PauseController.IsPaused) return;
        int n = UIKit.NumberPressed();
        if (n == 1) Pick(true);
        else if (n == 2) Pick(false);
        else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && ArrowPressed())
            UIKit.Select(headsButton);
    }

    void UpdateFlip()
    {
        float u = Mathf.Clamp01(t / FlipTime);
        choiceGroup.alpha = Mathf.Clamp01(1f - t / 0.2f);
        SetAlpha(pickText, Mathf.Clamp01((t - 0.15f) / 0.25f));

        // Gira rápido al principio y frena al caer
        float angle = targetAngle * (1f - Mathf.Pow(1f - u, 3f));
        float arc = Mathf.Sin(u * Mathf.PI);
        coin.anchoredPosition = coinRest + Vector2.up * arc * FlipHeight;
        float s = 1f + 0.22f * arc;                                   // más grande arriba (más cerca)
        coin.localScale = new Vector3(s, s, 1f);
        shadow.localScale = new Vector3(1f - 0.45f * arc, 1f - 0.45f * arc, 1f);

        int halfTurn = Mathf.FloorToInt((angle + Mathf.PI * 0.5f) / Mathf.PI);   // cambia de cara a 90°
        ShowFace(halfTurn % 2 == 0, Mathf.Cos(angle));

        if (u >= 1f) ShowResult();
    }

    void ShowResult()
    {
        phase = Phase.Result;
        t = 0f;
        coin.anchoredPosition = coinRest;
        shadow.localScale = Vector3.one;
        ShowFace(resultHeads, 1f);
        PlaySound(gm.coinLandClip);

        SetAlpha(pickText, 0f);
        resultTitle.text = playerWon ? "GUANYES EL SORTEIG!" : "PERDS EL SORTEIG";
        resultTitle.color = playerWon ? UIKit.Good : UIKit.Bad;

        Team server = playerWon ? gm.PlayerTeam : (gm.PlayerTeam == Team.Home ? Team.Away : Team.Home);
        resultSub.text = "TREU " + (server == Team.Home ? gm.HomeName : gm.AwayName);
    }

    void UpdateResult()
    {
        // Golpe al caer + aparecen los textos
        float punch = t < 0.25f ? 1f + 0.12f * Mathf.Sin(t / 0.25f * Mathf.PI) : 1f;
        coin.localScale = new Vector3(punch, punch, 1f);
        SetAlpha(resultTitle, Mathf.Clamp01(t / 0.2f));
        SetAlpha(resultSub, Mathf.Clamp01((t - 0.2f) / 0.25f));

        if (t >= ResultTime)
        {
            phase = Phase.FadeOut;
            t = 0f;
        }
    }

    void UpdateFadeOut()
    {
        group.alpha = 1f - Mathf.Clamp01(t / FadeTime);
        if (t < FadeTime) return;

        phase = Phase.Done;
        HudVisibility.Hidden = false;
        gm.CompleteCoinToss();
        Destroy(gameObject);
    }

    // ---------- Moneda ----------

    // 'cos' = coseno del giro: 1 de frente, 0 de canto. El ancho de la moneda es |cos| y,
    // casi de canto, se oscurece hacia el color del canto para que se lea como un disco girando.
    void ShowFace(bool heads, float cos)
    {
        float w = Mathf.Max(0.04f, Mathf.Abs(cos));
        Vector3 sc = coin.localScale;
        coinRim.rectTransform.localScale = new Vector3(w * Mathf.Abs(sc.y), sc.y, 1f);

        Color face = heads ? HeadsFace : TailsFace;
        Color rim = heads ? HeadsRim : TailsRim;
        coinRim.color = rim;
        coinFace.color = coinInner.color = Color.Lerp(rim, face, Mathf.Clamp01(w * 1.4f));
        coinLabel.text = heads ? "CARA" : "CREU";
        coinLabel.color = heads ? HeadsInk : TailsInk;
        SetAlpha(coinLabel, Mathf.Clamp01((w - 0.3f) / 0.4f));   // de canto no se lee el texto
    }

    static void SetAlpha(TMP_Text t, float a)
    {
        Color c = t.color;
        c.a = a;
        t.color = c;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        Camera cam = Camera.main;
        Vector3 at = cam != null ? cam.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, at, GameSettings.SfxVolume);
    }

    static bool ArrowPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        var g = UnityEngine.InputSystem.Gamepad.current;
        return (k != null && (k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame ||
                              k.aKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame)) ||
               (g != null && g.dpad.ReadValue().sqrMagnitude > 0.25f);
#else
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
               Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Tab);
#endif
    }
}
