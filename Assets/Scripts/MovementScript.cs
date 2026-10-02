using UnityEngine;
using UnityEngine.InputSystem;

public class MovementScript : MonoBehaviour
{
    public float acceleration = 10f;  
    public float maxSpeed = 20;

    [Header("Input System Reference")]
    public InputActionReference moveAction;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        
        moveInput = moveAction.action.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {

        if (moveInput.sqrMagnitude > 0.01f)
        {
            // Apliquem la força d'acceleració en la direcció indicada
            rb.AddForce(moveInput * acceleration, ForceMode2D.Force);

            // 2. Limitem la velocitat màxima (Clamp)
            if (rb.linearVelocity.magnitude > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }
        }
    }
}