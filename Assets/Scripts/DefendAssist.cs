using UnityEngine;

// Defensa asistida del jugador humano (mantener CLIC DERECHO sin bola).
// El jugador controlado se coloca solo entre el portador y tu portería, acompaña su velocidad
// y lanza el golpe de stick automáticamente en cuanto la bola queda expuesta.
// El WASD sigue influyendo (inputBlend) para que puedas cerrar una línea de pase o anticiparte.
// Va en el GameObject del TeamManager humano (TeamManager lo añade si falta).
public class DefendAssist : MonoBehaviour
{
    [SerializeField] float containDistance = 1.6f;              // más cerca que la IA: aquí mandas tú
    [SerializeField, Range(-1f, 1f)] float engageExposure = 0.15f;
    [SerializeField, Range(0f, 1f)] float inputBlend = 0.35f;   // peso del WASD sobre la asistencia
    [SerializeField] float slowRadius = 1.2f;
    [SerializeField] float bodyClearance = 0.45f;

    // Devuelve true si ha tomado el control este frame (solo cuando un rival lleva la bola)
    public bool Tick(Skater me, Vector2 rawInput, Transform ownGoal)
    {
        Ball b = Ball.Instance;
        if (b == null || ownGoal == null || !b.IsHeld || b.Holder.team == me.team || me.HasBall) return false;

        Skater carrier = b.Holder;
        Vector2 pos = me.transform.position;
        Vector2 cp = carrier.transform.position;
        Vector2 ballPos = b.rb.position;

        Vector2 toGoal = (Vector2)ownGoal.position - cp;
        Vector2 goalSide = toGoal.sqrMagnitude > 0.01f ? toGoal.normalized : Vector2.zero;

        bool open = !carrier.IsProtected && carrier.BallExposureTo(pos) >= engageExposure;
        Vector2 target = open
            ? ballPos + goalSide * me.stickReach * 0.5f    // a por la bola por el lado bueno
            : cp + goalSide * containDistance;              // contener: entre él y la portería

        Vector2 waypoint = GoalNav.Route(pos, target, bodyClearance);
        Vector2 toW = waypoint - pos;
        float d = Vector2.Distance(pos, target);

        Vector2 desire = d < 0.15f || toW.sqrMagnitude < 0.0001f
            ? Vector2.zero
            : toW.normalized * Mathf.Clamp01(d / slowRadius);
        desire += carrier.Velocity / Mathf.Max(me.TopSpeed, 0.1f) * 0.8f;

        me.MoveInput = Vector2.ClampMagnitude(Vector2.Lerp(desire, rawInput, inputBlend), 1f);

        Vector2 toBall = ballPos - pos;
        if (toBall.sqrMagnitude > 0.001f) me.AimDir = toBall.normalized;

        if (open && me.CanStick && toBall.magnitude <= me.stickReach * 0.9f) me.Stick();
        return true;
    }
}
