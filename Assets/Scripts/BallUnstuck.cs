using UnityEngine;

// Red de seguridad: si la bola suelta se queda parada pegada a una red (zona donde ningún jugador puede
// entrar), tras 'stuckTime' segundos se empuja suavemente hacia fuera de esa zona.
// Va en el mismo GameObject que Ball.
[RequireComponent(typeof(Ball))]
public class BallUnstuck : MonoBehaviour
{
    [SerializeField] float stuckSpeed = 0.6f;     // por debajo de esto se considera parada
    [SerializeField] float stuckTime = 1.2f;
    [SerializeField] float detectExtra = 0.35f;   // margen extra sobre la zona prohibida
    [SerializeField] float nudgeSpeed = 4f;

    Ball ball;
    float stuckSince = -1f;

    void Awake() { ball = GetComponent<Ball>(); }

    void FixedUpdate()
    {
        GameManager gm = GameManager.Instance;
        bool playing = gm == null || gm.State == MatchState.Playing;

        if (!playing || ball.IsHeld || ball.Speed > stuckSpeed)
        {
            stuckSince = -1f;
            return;
        }

        Vector2 p = ball.rb.position;
        Vector2 normal = Vector2.zero;
        bool inside = false;

        for (int i = 0; i < Goal.All.Count; i++)
        {
            Vector2 q = p;
            Vector2 n;
            if (Goal.All[i].TryPushOut(ref q, detectExtra, out n)) { inside = true; normal = n; break; }
        }

        if (!inside) { stuckSince = -1f; return; }
        if (stuckSince < 0f) { stuckSince = Time.time; return; }
        if (Time.time - stuckSince < stuckTime) return;

        ball.rb.linearVelocity = normal * nudgeSpeed;
        stuckSince = -1f;
    }
}
