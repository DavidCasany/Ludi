using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Pantalla de créditos (en catalán) presentada como la ALINEACIÓN de un equipo de hockey:
//   1. cae el título,
//   2. entran las "camisetas" del equipo una a una,
//   3. el marcador final suma los goles uno a uno: EQUIP LUDI 3 – 0 BUGS.
// Por detrás una bola rebota por la pista.
//
// Cualquier tecla o clic durante la animación la termina de golpe. Esc / TORNAR vuelve a la escena anterior.
// Los textos del equipo se editan en el Inspector (lista "members").
public class CreditsUI : MonoBehaviour
{
    [Serializable]
    public class Member
    {
        public string fullName;
        public int number = 1;
        [Tooltip("Frase corta en el color del miembro, debajo del nombre")]
        public string tagline;
        [TextArea(2, 5)] public string role;
        public Color color = Color.white;
    }

    [Header("Equipo")]
    [SerializeField] Member[] members;

    [Header("Cartas")]
    [SerializeField] CreditCard cardTemplate;
    [SerializeField] Transform cardRow;
    [Tooltip("Inclinación final de cada carta, por orden (se repite si hay más cartas)")]
    [SerializeField] float[] cardTilts = { -2.5f, 1.5f, -1f };

    [Header("Cabecera y pie")]
    [SerializeField] RectTransform header;
    [SerializeField] CanvasGroup headerGroup;
    [SerializeField] CanvasGroup footerGroup;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] RectTransform scoreBox;

    [Header("Marcador")]
    [SerializeField] string homeName = "EQUIP LUDI";
    [SerializeField] string awayName = "BUGS";
    [SerializeField] int homeGoals = 3;
    [SerializeField] int awayGoals = 0;

    [Header("Bola")]
    [SerializeField] RectTransform ball;
    [Tooltip("Zona donde rebota la bola (normalmente la pista del fondo)")]
    [SerializeField] RectTransform ballArea;
    [SerializeField] float ballSpeed = 380f;

    [Header("Tiempos")]
    [SerializeField] float headerDuration = 0.5f;
    [SerializeField] float cardInterval = 0.3f;
    [SerializeField] float goalInterval = 0.45f;

    [Header("Volver")]
    [SerializeField] Button backButton;
    [Tooltip("Si no se sabe la escena anterior (p. ej. abres Crèdits directamente en el editor)")]
    [SerializeField] string fallbackScene = "MainMenu";

    readonly System.Collections.Generic.List<CreditCard> cards = new System.Collections.Generic.List<CreditCard>();
    Vector2 ballVelocity;
    Vector2 headerRestPos;
    bool skip, introDone, leaving;

    void Awake()
    {
        backButton.onClick.AddListener(Back);
        BuildCards();
        if (header != null) headerRestPos = header.anchoredPosition;

        // Dirección inicial con algo de ángulo para que no rebote siempre igual
        float angle = UnityEngine.Random.Range(20f, 70f) * Mathf.Deg2Rad;
        ballVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ballSpeed;
        if (UnityEngine.Random.value < 0.5f) ballVelocity.x = -ballVelocity.x;
    }

    void Start()
    {
        Time.timeScale = 1f;
        StartCoroutine(Intro());
    }

    void Update()
    {
        MoveBall(Time.unscaledDeltaTime);
        if (leaving) return;

        if (BackPressed())
        {
            Back();
            return;
        }
        if (!introDone && AnyPressed()) skip = true;

        // Con teclado/mando, la primera pulsación deja seleccionado TORNAR
        EventSystem es = EventSystem.current;
        if (introDone && es != null && es.currentSelectedGameObject == null && NavigationPressed())
            es.SetSelectedGameObject(backButton.gameObject);
    }

    // ---------- Animación de entrada ----------

    void BuildCards()
    {
        cardTemplate.gameObject.SetActive(false);
        if (members == null) return;

        for (int i = 0; i < members.Length; i++)
        {
            CreditCard card = Instantiate(cardTemplate, cardRow);
            card.gameObject.SetActive(true);
            card.name = "Card_" + members[i].fullName;
            float tilt = cardTilts != null && cardTilts.Length > 0 ? cardTilts[i % cardTilts.Length] : 0f;
            card.Setup(members[i], tilt);
            cards.Add(card);
        }
    }

    IEnumerator Intro()
    {
        if (headerGroup != null) headerGroup.alpha = 0f;
        if (footerGroup != null) footerGroup.alpha = 0f;
        SetScore(0, 0);

        // 1. Título: cae desde arriba
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / headerDuration)
        {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            if (headerGroup != null) headerGroup.alpha = e;
            if (header != null) header.anchoredPosition = headerRestPos + Vector2.up * (80f * (1f - e));
            yield return null;
        }
        if (headerGroup != null) headerGroup.alpha = 1f;
        if (header != null) header.anchoredPosition = headerRestPos;

        // 2. Alineación: una carta tras otra
        foreach (CreditCard c in cards)
        {
            if (skip) break;
            c.PlayIntro();
            yield return Wait(cardInterval);
        }
        yield return Wait(0.35f);

        // 3. Marcador: aparece y suma los goles uno a uno
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 0.3f)
        {
            if (footerGroup != null) footerGroup.alpha = t;
            yield return null;
        }
        if (footerGroup != null) footerGroup.alpha = 1f;

        int h = 0, a = 0;
        while (!skip && (h < homeGoals || a < awayGoals))
        {
            yield return Wait(goalInterval);
            if (skip) break;
            if (h < homeGoals) h++; else a++;
            SetScore(h, a);
            yield return Punch(scoreBox);
        }

        FinishIntro();
    }

    void FinishIntro()
    {
        foreach (CreditCard c in cards) c.Finish();
        if (headerGroup != null) headerGroup.alpha = 1f;
        if (header != null) header.anchoredPosition = headerRestPos;
        if (footerGroup != null) footerGroup.alpha = 1f;
        if (scoreBox != null) scoreBox.localScale = Vector3.one;
        SetScore(homeGoals, awayGoals);
        introDone = true;
    }

    IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds && !skip; t += Time.unscaledDeltaTime) yield return null;
    }

    // Pequeño "golpe" de escala al marcar
    IEnumerator Punch(RectTransform rt)
    {
        if (rt == null) yield break;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 0.25f)
        {
            float s = 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    void SetScore(int h, int a)
    {
        if (scoreText != null) scoreText.text = $"{homeName}   <b>{h} – {a}</b>   {awayName}";
    }

    // ---------- Bola ----------

    // Rebota contra los bordes de la pista (como la bola real contra las vallas)
    void MoveBall(float dt)
    {
        if (ball == null || ballArea == null) return;

        Rect r = ballArea.rect;
        float radius = ball.rect.width * 0.5f;
        Vector2 p = ball.anchoredPosition + ballVelocity * dt;

        if (p.x < r.xMin + radius || p.x > r.xMax - radius)
        {
            ballVelocity.x = -ballVelocity.x;
            p.x = Mathf.Clamp(p.x, r.xMin + radius, r.xMax - radius);
        }
        if (p.y < r.yMin + radius || p.y > r.yMax - radius)
        {
            ballVelocity.y = -ballVelocity.y;
            p.y = Mathf.Clamp(p.y, r.yMin + radius, r.yMax - radius);
        }

        ball.anchoredPosition = p;
        ball.Rotate(0f, 0f, -ballVelocity.x * dt * 0.8f);
    }

    // ---------- Volver ----------

    public void Back()
    {
        if (leaving) return;

        string target = SceneHistory.Previous;
        if (string.IsNullOrEmpty(target) || target == gameObject.scene.name || !Application.CanStreamedLevelBeLoaded(target))
            target = fallbackScene;

        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogWarning($"[Crèdits] No se puede volver a '{target}': revisa Build Profiles.", this);
            return;
        }
        leaving = true;
        SceneManager.LoadScene(target);
    }

    // ---------- Input ----------

    static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
#endif
    }

    static bool AnyPressed()
    {
        if (TouchInput.AnyBegan) return true;   // un toque también salta la animación
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        Gamepad g = Gamepad.current;
        return (k != null && k.anyKey.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame) ||
               (g != null && g.buttonSouth.wasPressedThisFrame);
#else
        return Input.anyKeyDown;
#endif
    }

    static bool NavigationPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        if (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                          k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame ||
                          k.tabKey.wasPressedThisFrame))
            return true;
        Gamepad g = Gamepad.current;
        return g != null && (g.dpad.ReadValue().sqrMagnitude > 0.25f || g.leftStick.ReadValue().sqrMagnitude > 0.25f);
#else
        return Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f;
#endif
    }
}
