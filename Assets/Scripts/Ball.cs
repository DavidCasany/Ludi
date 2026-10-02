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

    // Para toques que no son de un Skater (p. ej. el portero)
    public void Touch(Team team) => LastTouchTeam = team;

    public void ResetTo(Vector2 position)
    {
        Holder = null;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = position;
    }
}
