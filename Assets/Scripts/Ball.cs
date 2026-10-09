using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Ball : MonoBehaviour
{
    public static Ball Instance { get; private set; }

    [HideInInspector] public Rigidbody2D rb;
    [HideInInspector] public Collider2D col;

    public Skater Holder { get; private set; }
    public bool IsHeld => Holder != null;
    public Team LastTouchTeam { get; private set; }
    public float Speed => rb.linearVelocity.magnitude;

    // Ventana de pase: tras un pase, los compañeros del pasador pueden controlar la bola aunque vaya rápida.
    // Los rivales no: solo la interceptan si van lentos o con un golpe de stick bien dado.
    public Team PassTeam { get; private set; }
    float passUntil;

    public bool IsPassFor(Team team) => !IsHeld && Time.time < passUntil && PassTeam == team;

    void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    public void Grab(Skater skater)
    {
        Holder = skater;
        LastTouchTeam = skater.team;
        passUntil = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void Release(Vector2 velocity)
    {
        Holder = null;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = velocity;
    }

    public void MarkPass(Team team, float duration)
    {
        PassTeam = team;
        passUntil = Time.time + duration;
    }

    // Para toques que no son de un Skater (p. ej. el portero)
    public void Touch(Team team)
    {
        LastTouchTeam = team;
        passUntil = 0f;
    }

    public void ResetTo(Vector2 position)
    {
        Holder = null;
        passUntil = 0f;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = position;
    }
}