using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Deja claro que un botón no se puede usar ahora mismo:
//  - el fondo pasa a un gris oscuro (disabledColor del ColorBlock),
//  - textos e iconos se desaturan y se apagan,
//  - opcionalmente aparece un sello inclinado ("NO DISPONIBLE", "SIN PARTIDA"...).
//
// No hace falta llamarlo: mira Selectable.interactable cada frame y cambia solo cuando cambia.
// Se añade por código con DisabledButtonLook.Ensure(boton, "NO DISPONIBLE") o a mano en el Inspector.
[DisallowMultipleComponent]
public class DisabledButtonLook : MonoBehaviour
{
    [SerializeField] Selectable target;
    [Tooltip("Texto del sello cuando está desactivado. Vacío = sin sello, solo se apaga")]
    [SerializeField] string tagText = "NO DISPONIBLE";

    [Header("Aspecto")]
    [SerializeField] Color disabledBackground = new Color(0.13f, 0.14f, 0.16f, 0.9f);
    [Tooltip("Cuánto se apagan textos e iconos (0 = invisibles, 1 = igual)")]
    [SerializeField, Range(0f, 1f)] float contentAlpha = 0.45f;
    [Tooltip("Cuánto se pasan a gris textos e iconos (0 = color original, 1 = gris)")]
    [SerializeField, Range(0f, 1f)] float desaturate = 0.8f;
    [SerializeField] Color tagBackground = new Color(0.08f, 0.09f, 0.11f, 0.95f);
    [SerializeField] Color tagTextColor = new Color(0.8f, 0.82f, 0.86f);
    [SerializeField] float tagAngle = -6f;

    Graphic[] graphics;
    Color[] savedColors;     // colores antes de apagar; null mientras el botón está activo
    GameObject tagObject;
    TMP_Text tagLabel;
    bool initialized;
    int lastState = -1;      // -1 = sin aplicar, 0 = desactivado, 1 = activo

    public static DisabledButtonLook Ensure(Selectable s, string tag)
    {
        if (s == null) return null;
        DisabledButtonLook look = s.GetComponent<DisabledButtonLook>();
        if (look == null) look = s.gameObject.AddComponent<DisabledButtonLook>();
        look.target = s;
        look.SetTag(tag);
        return look;
    }

    void Awake() => Init();

    void Init()
    {
        if (initialized) return;
        if (target == null) target = GetComponent<Selectable>();
        if (target == null)
        {
            Debug.LogWarning("[DisabledButtonLook] Necesita un Button/Selectable en el mismo objeto.", this);
            enabled = false;
            return;
        }
        initialized = true;

        // Con el marco dibujado (UIArt.ButtonFrame) un tinte casi negro taparía el dibujo: se usa un gris
        Image targetImage = target.targetGraphic as Image;
        bool framed = targetImage != null && targetImage.sprite != null && targetImage.sprite == UIArt.ButtonFrame;
        ColorBlock c = target.colors;
        c.disabledColor = framed ? UIArt.FrameDisabled : disabledBackground;
        target.colors = c;

        // Todo lo que hay dentro menos el propio fondo del botón
        Graphic[] all = GetComponentsInChildren<Graphic>(true);
        int n = 0;
        foreach (Graphic g in all) if (g != target.targetGraphic) n++;
        graphics = new Graphic[n];
        n = 0;
        foreach (Graphic g in all) if (g != target.targetGraphic) graphics[n++] = g;
    }

    public void SetTag(string text)
    {
        tagText = text;
        if (tagLabel != null) tagLabel.text = text;
        lastState = -1;   // se vuelve a aplicar en el próximo LateUpdate
    }

    void OnEnable() => lastState = -1;

    void LateUpdate()
    {
        if (!initialized) return;
        int state = target.interactable ? 1 : 0;
        if (state == lastState) return;
        lastState = state;
        Apply(state == 1);
    }

    void Apply(bool interactable)
    {
        if (interactable)
        {
            if (savedColors != null)
                for (int i = 0; i < graphics.Length; i++)
                    if (graphics[i] != null) graphics[i].color = savedColors[i];
            savedColors = null;
        }
        else if (savedColors == null)
        {
            savedColors = new Color[graphics.Length];
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null) continue;
                Color c = savedColors[i] = graphics[i].color;
                float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                Color dim = Color.Lerp(c, new Color(grey, grey, grey, c.a), desaturate);
                dim.a = c.a * contentAlpha;
                graphics[i].color = dim;
            }
        }

        bool showTag = !interactable && !string.IsNullOrEmpty(tagText);
        if (showTag && tagObject == null) CreateTag();
        if (tagObject != null) tagObject.SetActive(showTag);
    }

    // Sello pequeño e inclinado en la esquina superior derecha del botón
    void CreateTag()
    {
        tagObject = new GameObject("DisabledTag", typeof(RectTransform), typeof(Image));
        tagObject.transform.SetParent(transform, false);
        tagObject.transform.SetAsLastSibling();

        RectTransform rt = (RectTransform)tagObject.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-10f, -2f);
        rt.sizeDelta = new Vector2(180f, 34f);
        rt.localRotation = Quaternion.Euler(0f, 0f, tagAngle);

        Image bg = tagObject.GetComponent<Image>();
        Image source = target.targetGraphic as Image;
        if (source != null && source.sprite != null)
        {
            bg.sprite = source.sprite;
            bg.type = Image.Type.Sliced;
        }
        bg.color = tagBackground;
        bg.raycastTarget = false;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(tagObject.transform, false);
        RectTransform trt = (RectTransform)textGO.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8f, 2f);
        trt.offsetMax = new Vector2(-8f, -2f);

        tagLabel = textGO.AddComponent<TextMeshProUGUI>();
        tagLabel.text = tagText;
        tagLabel.fontStyle = FontStyles.Bold;
        tagLabel.characterSpacing = 3f;
        tagLabel.alignment = TextAlignmentOptions.Center;
        tagLabel.color = tagTextColor;
        tagLabel.raycastTarget = false;
        tagLabel.enableAutoSizing = true;
        tagLabel.fontSizeMin = 12f;
        tagLabel.fontSizeMax = 18f;
    }
}
