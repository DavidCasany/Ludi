using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Selecció per defecte NOMÉS per a teclat i mando.
//
// A Unity, un botó SELECCIONAT es pinta igual que un amb el ratolí a sobre (i HoverPop el fa créixer).
// Si una pantalla selecciona la primera carta o el botó "VER EL CUADRO" perquè es pugui navegar amb el
// teclat, amb el ratolí sembla que aquell element estigui sempre en hover.
//
// Per això UIKit.Select passa per aquí:
//  - fent servir el ratolí: l'element queda PENDENT i no se selecciona;
//  - a la primera tecla de navegació (fletxes, WASD, Tab, creueta o joystick) se selecciona;
//  - si després es torna a moure el ratolí, aquesta selecció automàtica es treu.
// Les seleccions que fa l'usuari clicant (un camp de text, un slider) no es toquen.
public static class NavigationSelector
{
    public static bool UsingNavigation { get; private set; }

    static Selectable pending;
    static GameObject autoSelected;
    static Runner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        UsingNavigation = false;
        pending = null;
        autoSelected = null;
        runner = null;
    }

    public static void Request(Selectable s)
    {
        if (s == null) return;
        EnsureRunner();
        pending = s;
        if (UsingNavigation) SelectNow(s);
    }

    static void SelectNow(Selectable s)
    {
        EventSystem es = EventSystem.current;
        if (es == null || s == null || !s.isActiveAndEnabled) return;
        es.SetSelectedGameObject(s.gameObject);
        autoSelected = s.gameObject;
    }

    static void EnsureRunner()
    {
        if (runner != null) return;
        GameObject go = new GameObject("[NavigationSelector]") { hideFlags = HideFlags.HideInHierarchy };
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        void Update()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;

            if (PointerUsed())
            {
                UsingNavigation = false;
                // Treu només la selecció que vam posar nosaltres (no la d'un clic de l'usuari)
                if (autoSelected != null && es.currentSelectedGameObject == autoSelected)
                    es.SetSelectedGameObject(null);
                autoSelected = null;
            }
            else if (NavigationPressed())
            {
                UsingNavigation = true;
                GameObject current = es.currentSelectedGameObject;
                if ((current == null || !current.activeInHierarchy) && pending != null && pending.IsInteractable())
                    SelectNow(pending);
            }
        }
    }

    static bool PointerUsed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var m = UnityEngine.InputSystem.Mouse.current;
        return m != null && (m.delta.ReadValue().sqrMagnitude > 1f || m.leftButton.wasPressedThisFrame);
#else
        return Mathf.Abs(Input.GetAxisRaw("Mouse X")) + Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.1f ||
               Input.GetMouseButtonDown(0);
#endif
    }

    static bool NavigationPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                          k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame ||
                          k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame ||
                          k.aKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame))
            return true;
        var g = UnityEngine.InputSystem.Gamepad.current;
        return g != null && (g.dpad.ReadValue().sqrMagnitude > 0.25f || g.leftStick.ReadValue().sqrMagnitude > 0.25f);
#else
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
               Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
               Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) ||
               Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Tab) ||
               Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f;
#endif
    }
}
