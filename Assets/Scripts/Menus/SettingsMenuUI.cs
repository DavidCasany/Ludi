using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Pantalla de ajustes. Lee y escribe en GameSettings; no guarda nada por sí misma.
//
// Funciona de dos formas, y lo detecta sola:
//  - Desde el menú de inicio se carga como escena normal -> "Volver" carga la escena anterior.
//  - Desde la pausa se carga ENCIMA del partido (aditiva) -> "Volver" la descarga y reaparece la pausa.
//    En ese caso quita su propia cámara, AudioListener y EventSystem para no duplicar los del partido.
public class SettingsMenuUI : MonoBehaviour
{
    [Header("Controles")]
    [Tooltip("Opcional: monitor on es mostra el joc (només surt si n'hi ha més d'un)")]
    [SerializeField] TMP_Dropdown monitorDropdown;
    [SerializeField] TMP_Dropdown resolutionDropdown;
    [SerializeField] Toggle fullscreenToggle;
    [SerializeField] Slider musicSlider;
    [SerializeField] TMP_Text musicValue;
    [SerializeField] Slider sfxSlider;
    [SerializeField] TMP_Text sfxValue;
    [SerializeField] Toggle passArrowsToggle;

    [Header("Controles táctiles (opcionales: los crea Tools > Ludi > Crear menú de ajustes)")]
    [SerializeField] TMP_Dropdown controlModeDropdown;
    [SerializeField] Slider touchScaleSlider;
    [SerializeField] TMP_Text touchScaleValue;
    [SerializeField] Slider touchOpacitySlider;
    [SerializeField] TMP_Text touchOpacityValue;
    [SerializeField] Toggle leftHandedToggle;

    [Tooltip("Opcional: vuelve a los valores por defecto")]
    [SerializeField] Button resetButton;
    [SerializeField] Button backButton;

    [Header("Volver")]
    [Tooltip("Si no se sabe la escena anterior (p. ej. abres Settings directamente en el editor)")]
    [SerializeField] string fallbackScene = "MainMenu";

    readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    readonly List<float> renderScales = new List<float>();   // web: el desplegable elige la resolución de render
    bool renderMode;
    bool overlay, leaving;
    GameObject checkedSelection;
    bool checkedSelectionValid;

    void Awake()
    {
        overlay = SceneManager.sceneCount > 1;
        if (overlay) RemoveDuplicatesForOverlay();

        // Escritorio: resolución de la ventana. Web (ordenador, móvil, tableta): la ventana la pone el navegador,
        // así que el desplegable pasa a ser la resolución interna del canvas (nitidez vs. rendimiento).
        // Build nativa de móvil: ni resolución ni pantalla completa.
        if (GameSettings.IsNativeMobile)
        {
            Hide(resolutionDropdown, true);
            Hide(fullscreenToggle, false);
        }
        else if (GameSettings.IsWeb)
        {
            SetupRenderResolution();
            if (!WebPlatform.FullscreenSupported()) Hide(fullscreenToggle, false);   // iPhone: Safari no lo permite
        }
        SetupMonitors();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        // Rang abans dels listeners: si canviar min/max retalla el valor, no s'ha de guardar com a volum
        musicSlider.minValue = sfxSlider.minValue = 0f;
        musicSlider.maxValue = sfxSlider.maxValue = 1f;
        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        passArrowsToggle.onValueChanged.AddListener(GameSettings.SetPassArrows);
        SetupControls();
        if (resetButton != null) resetButton.onClick.AddListener(ResetDefaults);   // opcional
        backButton.onClick.AddListener(Back);
    }

    void Start()
    {
        Refresh();
        // Desde la pausa casi siempre se llega con teclado/mando: dejamos el primer control seleccionado
        if (overlay) Select(musicSlider);
    }

    void Update()
    {
        if (leaving) return;

        bool listOpen = resolutionDropdown.IsExpanded || (controlModeDropdown != null && controlModeDropdown.IsExpanded) ||
                        (monitorDropdown != null && monitorDropdown.IsExpanded);
        if (BackPressed() && !listOpen)   // con la lista abierta, Esc solo la cierra
        {
            Back();
            return;
        }

        // Primera pulsación de teclado/mando sin nada usable seleccionado -> primer control
        if (!HasUsableSelection() && NavigationPressed()) Select(musicSlider);
    }

    // ---------- Mostrar valores actuales ----------

    void Refresh()
    {
        if (renderMode) RefreshRenderResolutions();
        else RefreshResolutions();

        // En el navegador se puede salir de pantalla completa con Esc: se lee el estado real
        fullscreenToggle.SetIsOnWithoutNotify(GameSettings.IsWeb ? Screen.fullScreen : GameSettings.Fullscreen);
        musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        passArrowsToggle.SetIsOnWithoutNotify(GameSettings.PassArrows);
        SetPercent(musicValue, GameSettings.MusicVolume);
        SetPercent(sfxValue, GameSettings.SfxVolume);
        RefreshControls();
        RefreshMonitors();
    }

    // ---------- Monitor (ordinador amb dues pantalles o més) ----------

    void SetupMonitors()
    {
        if (monitorDropdown == null) return;
        if (!GameSettings.CanChooseMonitor)
        {
            Hide(monitorDropdown, true);   // una sola pantalla, web o mòbil: no cal triar
            monitorDropdown = null;
            return;
        }
        monitorDropdown.onValueChanged.AddListener(GameSettings.SetMonitor);
    }

    void RefreshMonitors()
    {
        if (monitorDropdown == null) return;
        List<DisplayInfo> list = GameSettings.Monitors();
        List<string> options = new List<string>();
        for (int i = 0; i < list.Count; i++) options.Add($"Pantalla {i + 1} · {list[i].width} × {list[i].height}");

        monitorDropdown.ClearOptions();
        monitorDropdown.AddOptions(options);
        monitorDropdown.SetValueWithoutNotify(GameSettings.CurrentMonitor());
        monitorDropdown.RefreshShownValue();
    }

    // En acabar de moure's la finestra, la llista de resolucions és la del monitor nou
    void OnDisplayChanged()
    {
        if (this != null) Refresh();
    }

    void OnEnable() => GameSettings.DisplayChanged += OnDisplayChanged;
    void OnDisable() => GameSettings.DisplayChanged -= OnDisplayChanged;

    void RefreshResolutions()
    {
        resolutions.Clear();
        resolutions.AddRange(GameSettings.AvailableResolutions());

        List<string> options = new List<string>();
        int current = 0;
        for (int i = 0; i < resolutions.Count; i++)
        {
            options.Add($"{resolutions[i].x} × {resolutions[i].y}");
            if (resolutions[i].x == GameSettings.Width && resolutions[i].y == GameSettings.Height) current = i;
        }

        // ...WithoutNotify: rellenar la UI no debe volver a aplicar ni guardar los ajustes
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(current);
        resolutionDropdown.RefreshShownValue();
    }

    // ---------- Web: resolución de render ----------

    // El canvas ocupa la ventana del navegador (o la pantalla del móvil); lo que se elige es con cuántos
    // píxeles se dibuja. "Automàtica" = como mucho el doble de píxeles CSS: nítido y fluido en móviles x3.
    void SetupRenderResolution()
    {
        renderMode = true;
        Transform block = resolutionDropdown.transform.parent;
        Transform label = block != null ? block.Find("Label") : null;
        TMP_Text t = label != null ? label.GetComponent<TMP_Text>() : null;
        if (t != null) t.text = "Resolució de renderitzat";
    }

    void RefreshRenderResolutions()
    {
        renderScales.Clear();
        List<Vector2Int> seen = new List<Vector2Int>();
        List<string> options = new List<string>();
        float currentScale = GameSettings.RenderScale;

        foreach (float f in GameSettings.RenderScales)
        {
            Vector2Int r = WebPlatform.ResolutionFor(f);
            bool isCurrent = Mathf.Approximately(f, currentScale);
            if (r.y < 360 && !isCurrent) continue;   // demasiado borroso para ofrecerlo

            // Con pantallas x1 "Automàtica" y "Nativa" son lo mismo: se enseña una sola (la elegida, si es una)
            int dup = seen.IndexOf(r);
            if (dup >= 0)
            {
                if (!isCurrent) continue;
                seen.RemoveAt(dup);
                renderScales.RemoveAt(dup);
                options.RemoveAt(dup);
            }
            seen.Add(r);
            renderScales.Add(f);
            options.Add(RenderLabel(f, r));
        }

        int current = 0;
        for (int i = 0; i < renderScales.Count; i++)
            if (Mathf.Approximately(renderScales[i], currentScale)) current = i;

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(current);
        resolutionDropdown.RefreshShownValue();
    }

    static string RenderLabel(float f, Vector2Int r)
    {
        string size = $"{r.x} × {r.y}";
        if (f <= 0f) return "Automàtica · " + size;
        if (f >= 1f) return "Nativa · " + size;
        return Mathf.RoundToInt(f * 100f) + " % · " + size;
    }

    // ---------- Controles (móvil / tableta) ----------

    void SetupControls()
    {
        if (controlModeDropdown != null)
        {
            controlModeDropdown.ClearOptions();
            controlModeDropdown.AddOptions(new List<string> { "Automàtic", "Teclat i ratolí", "Tàctil" });
            controlModeDropdown.onValueChanged.AddListener(i => GameSettings.SetControls((ControlPreference)i));
        }
        if (touchScaleSlider != null)
        {
            touchScaleSlider.minValue = GameSettings.TouchScaleMin;
            touchScaleSlider.maxValue = GameSettings.TouchScaleMax;
            touchScaleSlider.onValueChanged.AddListener(v =>
            {
                GameSettings.SetTouchScale(v);
                SetPercent(touchScaleValue, v);
            });
        }
        if (touchOpacitySlider != null)
        {
            touchOpacitySlider.minValue = GameSettings.TouchOpacityMin;
            touchOpacitySlider.maxValue = GameSettings.TouchOpacityMax;
            touchOpacitySlider.onValueChanged.AddListener(v =>
            {
                GameSettings.SetTouchOpacity(v);
                SetPercent(touchOpacityValue, v);
            });
        }
        if (leftHandedToggle != null) leftHandedToggle.onValueChanged.AddListener(GameSettings.SetTouchLeftHanded);
    }

    void RefreshControls()
    {
        if (controlModeDropdown != null)
        {
            controlModeDropdown.SetValueWithoutNotify((int)GameSettings.Controls);
            controlModeDropdown.RefreshShownValue();
        }
        if (touchScaleSlider != null) touchScaleSlider.SetValueWithoutNotify(GameSettings.TouchScale);
        if (touchOpacitySlider != null) touchOpacitySlider.SetValueWithoutNotify(GameSettings.TouchOpacity);
        if (leftHandedToggle != null) leftHandedToggle.SetIsOnWithoutNotify(GameSettings.TouchLeftHanded);
        SetPercent(touchScaleValue, GameSettings.TouchScale);
        SetPercent(touchOpacityValue, GameSettings.TouchOpacity);
    }

    // Oculta un control y hace que la navegación con teclado/mando se lo salte.
    // block = true: se oculta también su bloque (etiqueta + control), que es su padre.
    void Hide(Selectable target, bool block)
    {
        if (target == null) return;
        Navigation skip = target.navigation;
        foreach (Selectable s in GetComponentsInChildren<Selectable>(true))
        {
            if (s == target) continue;
            Navigation n = s.navigation;
            if (n.mode != Navigation.Mode.Explicit) continue;
            if (n.selectOnDown == target) n.selectOnDown = skip.selectOnDown;
            if (n.selectOnUp == target) n.selectOnUp = skip.selectOnUp;
            s.navigation = n;
        }
        Transform parent = target.transform.parent;
        if (block && parent != null && parent != transform) parent.gameObject.SetActive(false);
        else target.gameObject.SetActive(false);
    }

    // ---------- Cambios ----------

    void OnResolutionChanged(int index)
    {
        if (renderMode)
        {
            if (index >= 0 && index < renderScales.Count) GameSettings.SetRenderScale(renderScales[index]);
            return;
        }
        if (index < 0 || index >= resolutions.Count) return;
        GameSettings.SetResolution(resolutions[index].x, resolutions[index].y);
    }

    // La lista de resoluciones cambia con el modo (en ventana no se ofrece la nativa), así que se rehace
    void OnFullscreenChanged(bool on)
    {
        GameSettings.SetFullscreen(on);
        Refresh();
    }

    void OnMusicChanged(float v)
    {
        GameSettings.SetMusicVolume(v);
        SetPercent(musicValue, v);
    }

    void OnSfxChanged(float v)
    {
        GameSettings.SetSfxVolume(v);
        SetPercent(sfxValue, v);
    }

    void ResetDefaults()
    {
        GameSettings.ResetToDefaults();
        Refresh();
    }

    static void SetPercent(TMP_Text label, float v)
    {
        if (label != null) label.text = Mathf.RoundToInt(v * 100f) + " %";
    }

    // ---------- Volver ----------

    public void Back()
    {
        if (leaving) return;
        leaving = true;

        if (overlay)
        {
            SceneManager.UnloadSceneAsync(gameObject.scene);   // PauseMenuUI se vuelve a mostrar al detectarlo
            return;
        }

        string target = SceneHistory.Previous;
        if (string.IsNullOrEmpty(target) || target == gameObject.scene.name || !Application.CanStreamedLevelBeLoaded(target))
            target = fallbackScene;

        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogWarning($"[Ajustes] No se puede volver a '{target}': revisa la lista de escenas del build.", this);
            leaving = false;
            return;
        }
        SceneManager.LoadScene(target);
    }

    // ---------- Modo superpuesto ----------

    // Los objetos de la propia escena de ajustes que ya existen en el partido
    void RemoveDuplicatesForOverlay()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (EventSystem es in root.GetComponentsInChildren<EventSystem>(true))
            {
                es.gameObject.SetActive(false);   // se desactiva ya para que no compita este frame
                Destroy(es.gameObject);
            }
            foreach (AudioListener l in root.GetComponentsInChildren<AudioListener>(true)) Destroy(l);
            foreach (Camera c in root.GetComponentsInChildren<Camera>(true)) Destroy(c.gameObject);
        }
    }

    // ---------- Input ----------

    bool HasUsableSelection()
    {
        EventSystem es = EventSystem.current;
        GameObject sel = es != null ? es.currentSelectedGameObject : null;
        if (sel != checkedSelection)   // solo se busca el componente cuando cambia la selección
        {
            checkedSelection = sel;
            checkedSelectionValid = sel != null && sel.TryGetComponent(out Selectable s) && s.IsInteractable();
        }
        return checkedSelectionValid && sel.activeInHierarchy;
    }

    // Con el dedo no se deja nada seleccionado: se vería como pulsado
    static void Select(Selectable s)
    {
        if (InputMode.IsTouch) return;
        if (EventSystem.current != null && s != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);   // B en mando Xbox
#endif
    }

    static bool NavigationPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        if (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                          k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame))
            return true;
        Gamepad g = Gamepad.current;
        return g != null && (g.dpad.ReadValue().sqrMagnitude > 0.25f || g.leftStick.ReadValue().sqrMagnitude > 0.25f);
#else
        return Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f;
#endif
    }
}