using UnityEngine;

// Perfil de comportamiento de un equipo controlado por la IA (o de los compañeros del humano).
// Crea uno por "estilo/dificultad": Assets > Create > Hockey > AI Profile, y asígnalo en el TeamBrain.
// Así el torneo puede dar a cada rival un perfil distinto sin tocar código.
[CreateAssetMenu(fileName = "AIProfile", menuName = "Hockey/AI Profile")]
public class AIProfile : ScriptableObject
{
    [Header("Ataque: ritmo")]
    [Tooltip("Pausa al recibir la bola antes de decidir (s). Es el 'pensar' del jugador.")]
    public float thinkTime = 0.35f;
    [Tooltip("Segundos de posesión hasta llegar a la urgencia máxima por finalizar la jugada.")]
    public float attackPatience = 8f;
    [Tooltip("Tiempo mínimo que mantiene una decisión de conducción antes de cambiar de carril (s).")]
    public float commitTime = 0.6f;
    [Tooltip("Pasado este tiempo con la bola fuerza una acción (s).")]
    public float maxHoldTime = 4.5f;

    [Header("Ataque: tiro")]
    [Range(0f, 1f), Tooltip("Calidad de tiro mínima al empezar la posesión.")]
    public float shootThreshold = 0.30f;
    [Range(0f, 1f), Tooltip("Calidad de tiro mínima con la urgencia al máximo.")]
    public float urgentShootThreshold = 0.12f;
    [Tooltip("Error angular de puntería (grados) con skill = 0.5.")]
    public float aimErrorDeg = 3.5f;
    [Tooltip("Distancia al poste a la que apunta (m). Evita tiros al palo o fuera.")]
    public float postMargin = 0.22f;

    [Header("Ataque: pase")]
    [Tooltip("Cuánta 'amenaza' extra debe ganar un pase respecto a seguir conduciendo.")]
    public float passGain = 0.08f;
    [Tooltip("Penalización de un pase hacia atrás.")]
    public float backPassPenalty = 0.12f;
    [Tooltip("Pases hacia atrás seguidos permitidos antes de obligarse a progresar.")]
    public int maxBackPassesInRow = 2;

    [Header("Defensa")]
    [Tooltip("El defensor más cercano solo sigue al portador si está a menos de esto (m).")]
    public float pressRadius = 9f;
    [Range(0.1f, 1f), Tooltip("Fracción de la distancia entre porterías, desde la nuestra, en la que se entra al robo. Fuera de ella solo se contiene.")]
    public float pressLine = 0.5f;
    [Tooltip("Distancia a la que el defensor contiene al portador sin entrarle (m).")]
    public float containDistance = 2.4f;
    [Tooltip("Retraso aleatorio (min, max) antes de entrar al robo dentro de la zona de presión (s).")]
    public Vector2 engageDelay = new Vector2(0.6f, 1.4f);
    [Range(-1f, 1f), Tooltip("Solo golpea con el stick si la bola está así de expuesta hacia él (-1 tapada, 1 de cara).")]
    public float pokeExposure = 0.2f;
    [Tooltip("Distancia de marcaje a los rivales sin bola (m).")]
    public float markDistance = 2.8f;
    [Tooltip("Distancia mínima y máxima del jugador de cobertura al portador (m).")]
    public float coverMin = 4f, coverMax = 7f;
}
