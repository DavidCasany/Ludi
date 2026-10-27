using System.Collections.Generic;
using UnityEngine;

// Contactos bola-jugador "a lo hockey patines". La bola NO choca físicamente con los jugadores de campo
// (desactivado en la matriz de colisiones 2D: Ball x Skaters): cuando una bola rápida pasa pegada a un
// jugador, se tira UN dado con tres resultados posibles:
//
//   PASA     : sigue recta, como si pasara entre los patines (lo más habitual).
//   DESVÍO   : el jugador la toca y le cambia la trayectoria unos grados, conservando casi toda la velocidad.
//              Solo con TIROS. Puede desviar cualquiera (también un compañero del que tira, nunca el propio
//              tirador). Más probable en tiros EXTERIORES. El portero tiene que volver a reaccionar: hay goles
//              "de rebote" y paradas a contrapié.
//   BLOQUEO  : solo los defensores. La bola rebota en el cuerpo y pierde casi toda la velocidad.
//
// JUGADA: a partir del 2º tiro seguido del mismo equipo (rebotes incluidos, sin que el rival recupere),
// los defensores bloquean y desvían más: la defensa se cierra delante de la portería.
//
// Va en el GameObject de la bola. El portero sigue chocando con la bola de forma normal.
[RequireComponent(typeof(Ball))]
public class BallBlocker : MonoBehaviour
{
    [Header("Cuándo hay contacto")]
    [Tooltip("Por debajo de esta velocidad no hay contacto: la bola se recoge (igual que autoPickupMaxSpeed)")]
    [SerializeField] float minSpeed = 7f;
    [Tooltip("Margen añadido al radio del jugador + radio de la bola (m)")]
    [SerializeField] float extraRadius = 0.1f;
    [Tooltip("Un mismo jugador solo tira el dado una vez cada este tiempo (s)")]
    [SerializeField] float rerollTime = 0.4f;

    [Header("Bloqueo (solo defensores)")]
    [SerializeField, Range(0f, 1f)] float centerChance = 0.6f;  // bola directa al cuerpo
    [SerializeField, Range(0f, 1f)] float edgeChance = 0.1f;    // bola que solo le roza
    [SerializeField, Range(0f, 1f)] float facingBonus = 0.15f;  // si está de cara a la bola
    [Tooltip("A esta velocidad la probabilidad de bloqueo queda a la mitad (los tiros fuertes se bloquean menos)")]
    [SerializeField] float fastSpeed = 24f;
    [SerializeField, Range(0f, 1f)] float blockKeepSpeed = 0.35f;   // velocidad que conserva tras un bloqueo
    [SerializeField] float blockScatterAngle = 70f;                 // dispersión del rebote del bloqueo

    [Header("Desvío (solo tiros)")]
    [SerializeField, Range(0f, 1f)] float defenderDeflectChance = 0.18f;
    [SerializeField, Range(0f, 1f)] float attackerDeflectChance = 0.12f;   // un compañero del tirador
    [Tooltip("Un tiro desde más lejos que esto (m) es EXTERIOR")]
    [SerializeField] float exteriorDistance = 7f;
    [Tooltip("Los tiros exteriores se desvían esto veces más")]
    [SerializeField] float exteriorMultiplier = 1.8f;
    [SerializeField] float minDeflectAngle = 6f;
    [SerializeField] float maxDeflectAngle = 28f;
    [SerializeField, Range(0f, 1f)] float deflectKeepMin = 0.75f;   // velocidad que conserva tras un desvío
    [SerializeField, Range(0f, 1f)] float deflectKeepMax = 0.95f;

    [Header("Jugada (tiros seguidos del mismo equipo)")]
    [Tooltip("Por cada tiro extra en la misma jugada, bloqueos y desvíos son esto más probables (0.5 = +50 %)")]
    [SerializeField] float streakBonus = 0.5f;
    [SerializeField] float maxStreakMultiplier = 2f;

    [Header("Feedback")]
    [SerializeField] bool showPopup = true;
    [SerializeField] bool debugLog = false;

    Ball ball;
    readonly Dictionary<Skater, float> lastRoll = new Dictionary<Skater, float>();
    static readonly Color blockColor = new Color(0.5f, 0.85f, 1f);
    static readonly Color deflectColor = new Color(1f, 0.75f, 0.3f);

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
            if (ball.IsShot && s == ball.Shooter) continue;            // nadie desvía su propio tiro
            if (!ball.IsShot && s.team == ball.LastTouchTeam) continue; // pases: los compañeros no la tocan

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

            float closeness = Mathf.Clamp01(1f - dist / reach);   // 1 = le va al centro del cuerpo, 0 = le roza
            float pBlock, pDeflect;
            Chances(s, dir, speed, closeness, out pBlock, out pDeflect);

            float roll = Random.value;
            if (debugLog) Debug.Log($"Contacto {s.name}: bloqueo {pBlock:P0}, desvío {pDeflect:P0} (jugada x{ball.ShotStreak})");

            if (roll < pBlock) { Block(s, dir, speed); return; }
            if (roll < pBlock + pDeflect) { DeflectShot(s, dir, speed); return; }
            // si no: pasa entre los patines
        }
    }

    void Chances(Skater s, Vector2 dir, float speed, float closeness, out float pBlock, out float pDeflect)
    {
        bool defender = ball.IsShot ? s.team != ball.ShotTeam : s.team != ball.LastTouchTeam;

        float streakMul = 1f;
        if (ball.IsShot && ball.ShotStreak > 1)
            streakMul = Mathf.Min(maxStreakMultiplier, 1f + streakBonus * (ball.ShotStreak - 1));

        // Bloqueo: solo defensores, más probable si le va al cuerpo
        pBlock = 0f;
        if (defender)
        {
            pBlock = Mathf.Lerp(edgeChance, centerChance, closeness);
            if (Vector2.Dot(s.AimDir, -dir) > 0.5f) pBlock += facingBonus;                 // de cara a la bola
            pBlock *= Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(minSpeed, fastSpeed, speed));  // tiro fuerte: más difícil
            if (s.IsStaggered) pBlock *= 0.5f;                                              // descolocado
            if (ball.IsShot) pBlock *= streakMul;
        }

        // Desvío: solo tiros, cualquiera menos el tirador; más probable si solo le roza y en tiros exteriores
        pDeflect = 0f;
        if (ball.IsShot)
        {
            pDeflect = defender ? defenderDeflectChance : attackerDeflectChance;
            pDeflect *= Mathf.Lerp(1.3f, 0.6f, closeness);   // rozar desvía, ir al cuerpo bloquea
            if (ball.ShotDistance > exteriorDistance) pDeflect *= exteriorMultiplier;
            if (defender) pDeflect *= streakMul;
        }

        // Siempre queda al menos un 5 % de que la bola pase
        float total = pBlock + pDeflect;
        if (total > 0.95f)
        {
            float k = 0.95f / total;
            pBlock *= k;
            pDeflect *= k;
        }
    }

    void Block(Skater s, Vector2 dir, float speed)
    {
        Vector2 normal = ball.rb.position - (Vector2)s.transform.position;
        normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : -dir;

        Vector2 outDir = Rotate(Vector2.Reflect(dir, normal), Random.Range(-blockScatterAngle, blockScatterAngle) * 0.5f);
        ball.rb.linearVelocity = outDir * speed * blockKeepSpeed;
        ball.Touch(s.team);   // el tiro se acaba (la jugada no: el rebote puede volver a tirarse)

        if (showPopup) Feedback.Popup(ball.rb.position, "¡BLOQUEO!", blockColor);
    }

    // Toque que cambia la trayectoria: unos grados a un lado u otro, conservando casi toda la velocidad
    void DeflectShot(Skater s, Vector2 dir, float speed)
    {
        float sign = Random.value < 0.5f ? -1f : 1f;
        float angle = sign * Random.Range(minDeflectAngle, maxDeflectAngle);
        ball.rb.linearVelocity = Rotate(dir, angle) * speed * Random.Range(deflectKeepMin, deflectKeepMax);
        ball.Deflect(s.team);   // sigue siendo el mismo tiro: el portero tiene que volver a reaccionar

        if (showPopup) Feedback.Popup(ball.rb.position, "¡DESVÍO!", deflectColor);
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * sn, v.x * sn + v.y * c);
    }
}