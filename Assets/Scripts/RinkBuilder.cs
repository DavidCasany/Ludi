using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera el perímetro de una pista con esquinas redondeadas
/// usando un EdgeCollider2D cerrado. Se actualiza en el editor
/// al cambiar cualquier valor del inspector.
/// </summary>
[RequireComponent(typeof(EdgeCollider2D))]
public class RinkBuilder : MonoBehaviour
{
    [Header("Dimensiones (unidades de Unity)")]
    public float width = 20f;
    public float height = 10f;

    [Header("Esquinas")]
    [Min(0.01f)] public float cornerRadius = 2f;
    [Range(2, 32)] public int segmentsPerCorner = 12;

    void Awake() { Build(); }
    void OnValidate() { Build(); }

    [ContextMenu("Rebuild")]
    public void Build()
    {
        var edge = GetComponent<EdgeCollider2D>();

        // El radio no puede ser mayor que la mitad del lado más corto
        float r = Mathf.Min(cornerRadius, width * 0.5f, height * 0.5f);
        float hx = width * 0.5f - r;   // centro de las esquinas en X
        float hy = height * 0.5f - r;  // centro de las esquinas en Y

        var pts = new List<Vector2>();

        // Sentido antihorario. Las líneas rectas entre esquinas se forman solas.
        AddCorner(pts, new Vector2(hx, hy), 0f, r); // arriba derecha
        AddCorner(pts, new Vector2(-hx, hy), 90f, r); // arriba izquierda
        AddCorner(pts, new Vector2(-hx, -hy), 180f, r); // abajo izquierda
        AddCorner(pts, new Vector2(hx, -hy), 270f, r); // abajo derecha

        pts.Add(pts[0]); // cerrar el perímetro
        edge.points = pts.ToArray();
    }

    void AddCorner(List<Vector2> pts, Vector2 center, float startAngleDeg, float radius)
    {
        for (int i = 0; i <= segmentsPerCorner; i++)
        {
            float a = (startAngleDeg + 90f * i / segmentsPerCorner) * Mathf.Deg2Rad;
            pts.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
    }
}