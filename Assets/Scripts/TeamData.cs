using UnityEngine;

// Datos de un equipo del torneo. Crear: Project > clic derecho > Create > Hockey > Equipo.
[CreateAssetMenu(menuName = "Hockey/Equipo", fileName = "Team_")]
public class TeamData : ScriptableObject
{
    [SerializeField] string teamName = "Equipo";
    [SerializeField] string shortName = "EQU";
    [SerializeField] Sprite crest;

    [Header("Equipación")]
    [Tooltip("Camiseta")]
    [SerializeField] Color primaryColor = Color.white;
    [Tooltip("Guantes, ribetes, número")]
    [SerializeField] Color secondaryColor = Color.white;
    [Tooltip("Segunda camiseta: se usa si el rival tiene un color demasiado parecido")]
    [SerializeField] Color alternateColor = Color.white;
    [Tooltip("Patrón de la camiseta (franjas, banda, mitades...). Vacío = camiseta lisa")]
    [SerializeField] Sprite jerseyPattern;
    [Tooltip("Patrón de la 2ª camiseta. Vacío = el mismo que la primera")]
    [SerializeField] Sprite alternatePattern;
    [SerializeField] Color goalkeeperColor = Color.gray;

    [Header("Nivel")]
    [Tooltip("Fuerza para simular los partidos IA vs IA (1-100)")]
    [SerializeField, Range(1, 100)] int rating = 50;
    [Tooltip("SkaterAI.skill que tendrán sus jugadores cuando juegues contra ellos")]
    [SerializeField, Range(0f, 1f)] float aiSkill = 0.7f;

    public string TeamName => teamName;
    public string ShortName => shortName;
    public Color PrimaryColor => primaryColor;
    public Color SecondaryColor => secondaryColor;
    public Color AlternateColor => alternateColor;
    public Sprite JerseyPattern => jerseyPattern;
    public Sprite AlternatePattern => alternatePattern != null ? alternatePattern : jerseyPattern;
    public Color GoalkeeperColor => goalkeeperColor;
    public Sprite Crest => crest;
    public int Rating => rating;
    public float AiSkill => aiSkill;
}
