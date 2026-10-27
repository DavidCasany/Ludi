using System.Collections.Generic;
using UnityEngine;

// Bloqueos "a lo hockey patines": la bola NO choca físicamente con los jugadores de campo
// (desactivado en la matriz de colisiones 2D: Ball x Skaters), así que pasa entre patines y sticks.
// Cuando una bola rápida pasa pegada a un rival del último que la tocó, este la bloquea con una probabilidad.
// Va en el GameObject de la bola. El portero sigue chocando con la bola de forma normal.
[RequireComponent(typeof(Ball))]
public class BallBlocker : MonoBehaviour
{
    [Header("Cuándo se puede bloquear")]
    [Tooltip("Por debajo de esta velocidad la bola no se bloquea: se recoge (igual que autoPickupMaxSpeed)")]
    [SerializeField] float minSpeed = 7f;
    [Tooltip("Margen añadido al radio del jugador + radio de la bola (m)")]
    [SerializeField] float extraRadius = 0.1f;
    [Tooltip("Un mismo jugador solo tira el dado una vez cada este tiempo (s)")]
    [SerializeField] float rerollTime = 0.4f;

    [Header("Probabilidad")]
    [SerializeField, Range(0f, 1f)] float centerChance = 0.6f;  // bola directa al cuerpo
    [SerializeField, Range(0f, 1f)] float edgeChance = 0.1f;    // bola que solo le roza
    [SerializeField, Range(0f, 1f)] float facingBonus = 0.15f;  // si está de cara a la bola
    [Tooltip("A esta velocidad la probabilidad queda a la mitad (los tiros fuertes se bloquean menos)")]
    [SerializeField] float fastSpeed = 24f;

    [Header("Desvío")]
    [SerializeField, Range(0f, 1f)] float keepSpeed = 0.35f;    // velocidad que conserva tras el bloqueo
    [SerializeField] float maxDeflectAngle = 70f;               // dispersión del rebote
    [SerializeField] bool showPopup = true;

    Ball ball;
    readonly Dictionary<Skater, float> lastRoll = new Dictionary<Skater, float>();
    static readonly Color popupColor = new Color(0.5f, 0.85f, 1f);

    void Awake()
    {
        ball = GetComponent<Ball>();
    }

    void FixedUpdate()
    {
        if (ball.IsHeld) { lastRoll.Clear(); return; }

        Vector2 v = ball.rb.linearVelocity;
        float speed = v.magnitude;
        if (speed < minSpeed) return;

        Vector2 bp = ball.rb.position;
        Vector2 dir = v / speed;
        float ballRadius = ball.col.bounds.extents.x;
        float step = speed * Time.fixedDeltaTime;   // tramo que recorrerá en este paso

        for (int i = 0; i < Skater.All.Count; i++)
        {
            Skater s = Skater.All[i];
            if (s.team == ball.LastTouchTeam) continue;   // los compañeros del que la golpeó no la bloquean

            // Distancia del jugador al tramo que recorre la bola en este paso (no se "cuela" a alta velocidad)
            Vector2 sp = s.transform.position;
            Vector2 to = sp - bp;
            float along = Mathf.Clamp(Vector2.Dot(to, dir), 0f, step);
            float dist = Vector2.Distance(sp, bp + dir * along);
            float reach = s.BodyRadius + ballRadius + extraRadius;
            if (dist > reach) continue;

            // Un solo dado por jugador y paso de bola
            float last;
            if (lastRoll.TryGetValue(s, out last) && Time.time - last < rerollTime) continue;
            lastRoll[s] = Time.time;

            if (Random.value < ChanceFor(s, dir, speed, 1f - dist / reach))
            {
                Block(s, dir, speed);
                return;
            }
        }
    }

    float ChanceFor(Skater s, Vector2 dir, float speed, float closeness)
    {
        float chance = Mathf.Lerp(edgeChance, centerChance, Mathf.Clamp01(closeness));   // por el centro del cuerpo: más bloqueo
        if (Vector2.Dot(s.AimDir, -dir) > 0.5f) chance += facingBonus;                    // de cara a la bola
        chance *= Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(minSpeed, fastSpeed, speed));     // tiro fuerte: más difícil
        if (s.IsStaggered) chance *= 0.5f;                                                  // descolocado
        return Mathf.Clamp01(chance);
    }

    void Block(Skater s, Vector2 dir, float speed)
    {
        Vector2 normal = ball.rb.position - (Vector2)s.transform.position;
        normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : -dir;

        Vector2 outDir = Rotate(Vector2.Reflect(dir, normal), Random.Range(-maxDeflectAngle, maxDeflectAngle) * 0.5f);
        ball.rb.linearVelocity = outDir * speed * keepSpeed;
        ball.Touch(s.team);

        if (showPopup) Feedback.Popup(ball.rb.position, "¡BLOQUEO!", popupColor);
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * sn, v.x * sn + v.y * c);
    }
}