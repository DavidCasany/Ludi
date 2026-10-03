using UnityEngine;

// BoxCollider2D con "Is Trigger" activado, colocado dentro de la red (detrás de la línea de gol).
[RequireComponent(typeof(Collider2D))]
public class Goal : MonoBehaviour
{
    public Team defendingTeam; // equipo que defiende esta portería

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Ball>() == null) return;

        Team scorer = defendingTeam == Team.Home ? Team.Away : Team.Home;
        GameManager.Instance.GoalScored(scorer);
    }
    public void SwapDefendingTeam()
    {
        defendingTeam = defendingTeam == Team.Home ? Team.Away : Team.Home;
    }
}
