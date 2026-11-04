using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Lógica del menú de inicio: a qué escena va cada botón, estado del botón "Continuar torneo"
// y navegación con teclado/mando. Las escenas se dejan vacías hasta que existan: el botón solo
// escribe un aviso en la consola.
public class MainMenuController : MonoBehaviour
{
    [Header("Escenas (dejar vacío mientras no existan)")]
    [Tooltip("Vacío = la escena del cuadro que diga el TournamentConfig (HubScene)")]
    [SerializeField] string tournamentScene = "";
    [SerializeField] string pvpScene = "";
    [SerializeField] string pveScene = "";
    [SerializeField] string settingsScene = "";
    [SerializeField] string creditsScene = "";

    [Header("Torneo")]
    [SerializeField] TournamentConfig tournamentConfig;

    [Header("Demo (lo cambia Tools > Ludi > Configurar flujo de la demo)")]
    [SerializeField] bool tournamentEnabled = true;
    [SerializeField] bool pvpEnabled = true;

    [Header("Botones desactivados")]
    [Tooltip("Sello de los botones que no se pueden usar (modo apagado para la demo o escena sin asignar)")]
    [SerializeField] string unavailableLabel = "NO DISPONIBLE";
    [Tooltip("Sello de Continuar cuando no hay ningún torneo guardado")]
    [SerializeField] string noTournamentLabel = "SENSE TORNEIG";
    [Tooltip("Sello de Continuar cuando el último torneo ya terminó")]
    [SerializeField] string finishedTournamentLabel = "ACABAT";

    [Header("Botones")]
    [SerializeField] Button continueTournamentButton;
    [SerializeField] Button newTournamentButton;
    [SerializeField] Button pvpButton;
    [SerializeField] Button pveButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button creditsButton;
    [SerializeField] Button quitButton;
    [Tooltip("Botó CONTROLS (pantalla de com es juga). Buit = es crea sol just abans de CRÈDITS")]
    [SerializeField] Button controlsButton;

    [Header("Navegación con teclado / mando")]
    [Tooltip("Filas del menú de arriba abajo: la raíz de cada SplitMenuButton o el botón simple")]
    [SerializeField] GameObject[] rows;
    [SerializeField] bool wrapNavigation = true;

    readonly List<Selectable> rowSelectables = new List<Selectable>();
    bool loading;

    void Awake()
    {
        EnsureControlsButton();
        ApplyIconFrame(settingsButton);
        Bind(controlsButton, OpenControls);
        Bind(continueTournamentButton, ContinueTournament);
        Bind(newTournamentButton, NewTournament);
        Bind(pvpButton, PlayPvP);
        Bind(pveButton, PlayPvE);
        MuteSceneListener(settingsButton, nameof(OpenSettings));   // si l'escena ja el cridava, primer la volta
        Bind(settingsButton, SpinThenOpenSettings);
        Bind(creditsButton, OpenCredits);
        Bind(quitButton, Quit);
    }

    void Start()
    {
        Time.timeScale = 1f;   // por si llegamos desde el menú de pausa con el juego parado
#if UNITY_WEBGL && !UNITY_EDITOR
        // En el navegador no se puede cerrar el juego (Application.Quit no hace nada): fuera SORTIR
        if (quitButton != null) quitButton.gameObject.SetActive(false);
#endif
        RefreshAvailability();
        BuildNavigation();     // después de RefreshAvailability: un botón desactivado no entra en la navegación
    }

    void Update()
    {
        // Con ratón no queremos nada seleccionado al empezar (abriría Torneo solo).
        // La primera vez que se usa teclado/mando sin nada seleccionado, se selecciona la primera fila.
        EventSystem es = EventSystem.current;
        if (loading || es == null || rowSelectables.Count == 0 || ControlsScreen.IsOpen) return;

        GameObject current = es.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy) return;

        if (NavigationPressed()) es.SetSelectedGameObject(rowSelectables[0].gameObject);
    }

    // ---------- Acciones de los botones ----------

    // Escena del cuadro: la del campo, o si está vacío la del TournamentConfig
    string TournamentScene => !string.IsNullOrEmpty(tournamentScene) ? tournamentScene
                            : tournamentConfig != null ? tournamentConfig.HubScene : "";

    public void ContinueTournament()
    {
        if (!tournamentEnabled || ConfirmDialog.IsOpen) return;
        TryLoad(TournamentScene, "Continuar torneo", () => TournamentSession.TryResume(tournamentConfig));
    }

    // Si hay un torneo a medias, se pide confirmación antes de borrarlo
    public void NewTournament()
    {
        if (!tournamentEnabled || loading || ConfirmDialog.IsOpen) return;

        if (tournamentConfig != null &&
            TournamentSession.GetSaveState(tournamentConfig) == TournamentSession.SaveState.InProgress)
        {
            ConfirmDialog.Show("Vols començar un torneig nou?",
                               "Tens un torneig a mitges. Si en comences un altre, es perdrà.",
                               "COMENÇAR", "CANCEL·LAR", StartNewTournament,
                               () => UIKit.Select(newTournamentButton));
            return;
        }
        StartNewTournament();
    }

    void StartNewTournament()
    {
        TryLoad(TournamentScene, "Nuevo torneo", () =>
        {
            if (tournamentConfig == null)
            {
                Debug.LogError("[MainMenu] Asigna un TournamentConfig al MainMenuController.", this);
                return false;
            }
            TournamentSession.ClearSave();
            return TournamentSession.StartNew(tournamentConfig);
        });
    }

    public void PlayPvP()
    {
        if (pvpEnabled) TryLoad(pvpScene, "1 VS 1 · PvP", () => { MatchLaunch.Mode = MatchMode.PvP; return true; });
    }
    public void PlayPvE() => TryLoad(pveScene, "1 VS 1 · PvE", () => { MatchLaunch.Mode = MatchMode.PvE; return true; });
    public void OpenSettings() => TryLoad(settingsScene, "Ajustes", null);

    // Engranatge: fa una volta (ràpida al principi i frenant) i després obre Configuració
    [Header("Animació de l'engranatge")]
    [SerializeField] float gearSpinTime = 0.45f;
    [SerializeField] float gearSpinDegrees = 360f;
    bool gearSpinning;

    void SpinThenOpenSettings()
    {
        if (loading || gearSpinning || ConfirmDialog.IsOpen || ControlsScreen.IsOpen) return;
        Transform icon = GearIcon();
        if (icon == null || gearSpinTime <= 0f || !SceneReady(settingsScene)) { OpenSettings(); return; }
        StartCoroutine(SpinGear(icon));
    }

    System.Collections.IEnumerator SpinGear(Transform icon)
    {
        gearSpinning = true;
        Quaternion start = icon.localRotation;
        for (float t = 0f; t < gearSpinTime; t += Time.unscaledDeltaTime)
        {
            float k = t / gearSpinTime;
            k = 1f - (1f - k) * (1f - k) * (1f - k);              // surt de pressa i frena
            icon.localRotation = start * Quaternion.Euler(0f, 0f, -gearSpinDegrees * k);   // sentit horari
            float pop = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);      // petit "pop" a mitja volta
            icon.localScale = new Vector3(pop, pop, 1f);
            yield return null;
        }
        icon.localRotation = start;
        icon.localScale = Vector3.one;
        gearSpinning = false;
        OpenSettings();
    }
    public void OpenCredits() => TryLoad(creditsScene, "Créditos", null);

    public void OpenControls()
    {
        if (loading || ConfirmDialog.IsOpen || ControlsScreen.IsOpen) return;
        ControlsScreen.Show(() => UIKit.Select(controlsButton));
    }

    // Si l'escena no té botó de CONTROLS, se'n fa una còpia de CRÈDITS (mateix estil) just a sobre
    // i s'afegeix a la navegació amb teclat/mando
    void EnsureControlsButton()
    {
        if (controlsButton != null || creditsButton == null) return;
        controlsButton = ControlsScreen.CloneButton(creditsButton, "Controls", "CONTROLS", true);
        if (controlsButton == null || rows == null) return;

        int i = Array.IndexOf(rows, creditsButton.gameObject);
        if (i < 0) return;
        List<GameObject> list = new List<GameObject>(rows);
        list.Insert(i, controlsButton.gameObject);
        rows = list.ToArray();
    }

    public void Quit()
    {
        Debug.Log("[MainMenu] Salir del juego");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Comprueba la escena ANTES de preparar nada: así "Nuevo torneo" no borra la partida guardada
    // si la escena todavía no existe.
    void TryLoad(string scene, string label, Func<bool> prepare)
    {
        if (loading) return;

        if (string.IsNullOrEmpty(scene))
        {
            Debug.Log($"[MainMenu] «{label}»: escena sin asignar todavía.", this);
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"[MainMenu] «{label}»: la escena '{scene}' no está en la lista de escenas del build " +
                             "o el nombre no coincide.", this);
            return;
        }
        if (prepare != null && !prepare()) return;

        loading = true;
        SceneManager.LoadScene(scene);
    }

    // ---------- Estado ----------

    // Decide qué botones se pueden usar y les pone el aspecto de desactivado (DisabledButtonLook):
    //  - Torneo y PvP se pueden apagar para la demo,
    //  - un botón cuya escena no está asignada o no está en Build Profiles también se apaga,
    //  - Continuar, además, necesita un torneo guardado.
    public void RefreshAvailability()
    {
        bool tournamentReady = tournamentEnabled && tournamentConfig != null && SceneReady(TournamentScene);
        TournamentSession.SaveState save = tournamentReady ? TournamentSession.GetSaveState(tournamentConfig)
                                                           : TournamentSession.SaveState.None;

        // Continuar: solo con un torneo a medias. Si no, el sello dice por qué (no hay / ya terminó / apagado)
        string continueLabel = !tournamentReady ? unavailableLabel
                             : save == TournamentSession.SaveState.Finished ? finishedTournamentLabel
                             : noTournamentLabel;
        SetAvailable(newTournamentButton, tournamentReady, unavailableLabel);
        SetAvailable(continueTournamentButton, save == TournamentSession.SaveState.InProgress, continueLabel);
        SetAvailable(pvpButton, pvpEnabled && SceneReady(pvpScene), unavailableLabel);
        SetAvailable(pveButton, SceneReady(pveScene), unavailableLabel);
        SetAvailable(creditsButton, SceneReady(creditsScene), unavailableLabel);
        SetAvailable(settingsButton, SceneReady(settingsScene), null);   // icono pequeño: sin sello

        // Filas divididas: si no queda ningún sub-botón usable, el principal también se apaga
        foreach (GameObject row in rows)
        {
            if (row == null) continue;
            SplitMenuButton split = row.GetComponent<SplitMenuButton>();
            if (split == null) continue;
            split.RefreshAvailability();
            DisabledButtonLook.Ensure(split.MainButton, unavailableLabel);
        }
    }

    // Se mantiene por compatibilidad (antes solo miraba el botón Continuar)
    public void RefreshContinueButton() => RefreshAvailability();

    static void SetAvailable(Button b, bool available, string disabledLabel)
    {
        if (b == null) return;
        b.interactable = available;
        DisabledButtonLook.Ensure(b, disabledLabel);
    }

    static bool SceneReady(string scene) => !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);

    // ---------- Navegación ----------

    void BuildNavigation()
    {
        rowSelectables.Clear();
        List<SplitMenuButton> splits = new List<SplitMenuButton>();

        foreach (GameObject row in rows)
        {
            if (row == null) continue;
            SplitMenuButton split = row.GetComponent<SplitMenuButton>();
            Selectable sel = split != null ? split.MainButton : row.GetComponent<Selectable>();
            if (sel == null)
            {
                Debug.LogWarning($"[MainMenu] La fila '{row.name}' no tiene ni SplitMenuButton ni Button.", row);
                continue;
            }
            if (!sel.interactable || !sel.gameObject.activeInHierarchy) continue;   // fila apagada u oculta: se salta
            rowSelectables.Add(sel);
            splits.Add(split);
        }

        int n = rowSelectables.Count;
        if (n == 0) return;

        // Botón de Ajustes fuera de la columna (engranaje en la esquina superior derecha):
        // se llega a él pulsando ↑ desde la primera fila, y con ↓ o ← se vuelve a la columna.
        Selectable corner = settingsButton != null && settingsButton.interactable && !rowSelectables.Contains(settingsButton) ? settingsButton : null;
        Selectable first = rowSelectables[0];
        Selectable last = rowSelectables[n - 1];

        for (int i = 0; i < n; i++)
        {
            Selectable up = i > 0 ? rowSelectables[i - 1]
                          : corner != null ? corner
                          : (wrapNavigation ? last : null);
            Selectable down = i < n - 1 ? rowSelectables[i + 1] : (wrapNavigation ? first : null);

            if (splits[i] != null) splits[i].SetVerticalNeighbours(up, down);
            else rowSelectables[i].navigation = SplitMenuButton.ExplicitNav(up, down, null, null);
        }

        if (corner != null)
            corner.navigation = SplitMenuButton.ExplicitNav(wrapNavigation ? last : null, first, first, null);
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

    // Botó d'icona (l'engranatge): el mateix metall amb cargols que la resta de botons (ui_steel en 9-slice)
    // i els mateixos tints al passar-hi el ratolí. El marc ample dels botons de text no hi cabria quadrat.
    static void ApplyIconFrame(Button b)
    {
        if (b == null || UIArt.SteelPanel == null) return;
        Image img = b.targetGraphic as Image;
        if (img == null) return;

        img.sprite = UIArt.SteelPanel;
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.color = Color.white;
        float size = Mathf.Min(((RectTransform)b.transform).rect.width, ((RectTransform)b.transform).rect.height);
        float border = Mathf.Max(12f, size * 0.26f);   // vora proporcional: els cargols no es deformen
        img.pixelsPerUnitMultiplier = UIArt.PanelSpriteBorder / border;
        b.colors = UIArt.FrameColors();

        // La icona, dins de la vora metàl·lica
        RectTransform icon = IconOf(b);
        if (icon != null)
        {
            icon.anchorMin = Vector2.zero;
            icon.anchorMax = Vector2.one;
            float pad = border * 0.8f;
            icon.offsetMin = new Vector2(pad, pad);
            icon.offsetMax = new Vector2(-pad, -pad);
        }
    }

    Transform GearIcon() => IconOf(settingsButton);

    // La icona del botó: el fill "Icon" o, si es diu d'una altra manera, la primera imatge filla
    static RectTransform IconOf(Button b)
    {
        if (b == null) return null;
        Transform t = b.transform.Find("Icon");
        if (t != null) return t as RectTransform;
        foreach (Image img in b.GetComponentsInChildren<Image>(true))
            if (img.gameObject != b.gameObject) return img.rectTransform;
        return null;
    }

    // Apaga una crida a 'method' d'aquest controlador posada a mà a l'On Click() de l'Inspector:
    // si no, obriria l'escena a l'instant i no es veuria l'animació
    void MuteSceneListener(Button b, string method)
    {
        if (b == null) return;
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            if (b.onClick.GetPersistentTarget(i) == this && b.onClick.GetPersistentMethodName(i) == method)
                b.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
    }

    static void Bind(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b != null) b.onClick.AddListener(action);
    }
}
