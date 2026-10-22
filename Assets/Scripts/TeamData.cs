using UnityEngine;

// Datos de un equipo del torneo. Crear: Project > clic derecho > Create > Hockey > Equipo.
[CreateAssetMenu(menuName = "Hockey/Equipo", fileName = "Team_")]
public class TeamData : ScriptableObject
{
    [SerializeField] string teamName = "Equipo";
    [SerializeField] string shortName = "EQU";
    [SerializeField] Color primaryColor = Color.white;
    [SerializeField] Color goalkeeperColor = Color.gray;
    [SerializeField] Sprite crest;

    [Header("Nivel")]
    [Tooltip("Fuerza para simular los partidos IA vs IA (1-100)")]
    [SerializeField, Range(1, 100)] int rating = 50;
    [Tooltip("SkaterAI.skill que tendrán sus jugadores cuando juegues contra ellos")]
    [SerializeField, Range(0f, 1f)] float aiSkill = 0.7f;

    public string TeamName => teamName;
    public string ShortName => shortName;
    public Color PrimaryColor => primaryColor;
    public Color GoalkeeperColor => goalkeeperColor;
    public Sprite Crest => crest;
    public int Rating => rating;
    public float AiSkill => aiSkill;
}
