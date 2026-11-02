using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Estado de pausa del partido: para el tiempo, el audio y avisa a quien le interese.
// No sabe nada de botones ni de pantallas; eso es cosa de PauseMenuUI.
//
// Otros scripts consultan PauseController.IsPaused (estático) para no leer input ni dibujar
// su HUD de OnGUI mientras el juego está en pausa.
public class PauseController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    // true al pausar, false al reanudar
    public static event Action<bool> PauseChanged;

    [SerializeField] PauseMenuUI ui;

    float savedTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsPaused = false;
        PauseChanged = null;
    }

    void Update()
    {
        if (!PausePressed()) return;

        if (!IsPaused) Pause();
        else if (ui == null || !ui.HandleBack()) Resume();   // Esc primero cierra la confirmación, si está abierta
    }

    // Con el partido terminado sale el botón CONTINUAR del torneo: ahí no tiene sentido pausar
    bool CanPause => GameManager.Instance == null || GameManager.Instance.State != MatchState.FullTime;

    public void Pause()
    {
        if (IsPaused || !CanPause) return;

        savedTimeScale = Time.timeScale;   // puede ser < 1 si se pausa en pleno bullet time
        Time.timeScale = 0f;
        AudioListener.pause = true;
        IsPaused = true;
        PauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = savedTimeScale;   // BulletTime vuelve a llevarlo a 1 suavemente
        AudioListener.pause = false;
        PauseChanged?.Invoke(false);
    }

    // Si se cambia de escena estando en pausa (Salir al menú), el juego no debe quedarse congelado
    void OnDestroy()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    static bool PausePressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        return (k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame)) ||
               (g != null && g.startButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) ||
               Input.GetKeyDown(KeyCode.JoystickButton7);   // Start en mando de Xbox
#endif
    }
}
