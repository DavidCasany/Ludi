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
    [SerializeField] TMP_Dropdown resolutionDropdown;
    [SerializeField] Toggle fullscreenToggle;
    [SerializeField] Slider musicSlider;
    [SerializeField] TMP_Text musicValue;
    [SerializeField] Slider sfxSlider;
    [SerializeField] TMP_Text sfxValue;
    [SerializeField] Toggle passArrowsToggle;
    [Tooltip("Opcional: vuelve a los valores por defecto")]
    [SerializeField] Button resetButton;
    [SerializeField] Button backButton;

    [Header("Volver")]
    [Tooltip("Si no se sabe la escena anterior (p. ej. abres Settings directamente en el editor)")]
    [SerializeField] string fallbackScene = "MainMenu";

    readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    bool overlay, leaving;
    GameObject checkedSelection;
    bool checkedSelectionValid;

    void Awake()
    {
        overlay = SceneManager.sceneCount > 1;
        if (overlay) RemoveDuplicatesForOverlay();

        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        passArrowsToggle.onValueChanged.AddListener(GameSettings.SetPassArrows);
        if (resetButton != null) resetButton.onClick.AddListener(ResetDefaults);   // opcional
        backButton.onClick.AddListener(Back);

        musicSlider.minValue = sfxSlider.minValue = 0f;
        musicSlider.maxValue = sfxSlider.maxValue = 1f;
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

        if (BackPressed() && !resolutionDropdown.IsExpanded)   // con la lista abierta, Esc solo la cierra
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

        fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
        musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        passArrowsToggle.SetIsOnWithoutNotify(GameSettings.PassArrows);
        SetPercent(musicValue, GameSettings.MusicVolume);
        SetPercent(sfxValue, GameSettings.SfxVolume);
    }

    // ---------- Cambios ----------

    void OnResolutionChanged(int index)
    {
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

    static void Select(Selectable s)
    {
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