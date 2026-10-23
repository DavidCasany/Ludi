using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Dibuja la pista (hielo, bandas, línea central, círculo central y áreas de portería) a partir del
// EdgeCollider2D de GameManager.rinkWalls. Con una cámara que sigue la bola, un fondo vacío no deja
// notar el movimiento; esto da referencias visuales. Se crea solo cada vez que se carga una escena
// que tiene un GameManager con rinkWalls. Para quitarlo, borra este archivo.
public class RinkVisual : MonoBehaviour
{
    public Color iceColor = new Color(0.52f, 0.68f, 0.80f);
    public Color outsideColor = new Color(0.08f, 0.10f, 0.14f);
    public Color boardColor = new Color(0.10f, 0.14f, 0.22f);
    public Color lineColor = new Color(0.95f, 0.95f, 1f, 0.55f);
    public Color centerLineColor = new Color(0.9f, 0.25f, 0.25f, 0.7f);

    public float boardWidth = 0.5f;
    public float centerCircleRadius = 3.5f;
    public float creaseRadius = 3f;

    Material mat;

    // Se suscribe a sceneLoaded UNA vez al arrancar el juego, antes de cargar la primera escena.
    // Así se crea la pista en cualquier escena con GameManager, sea la primera o una cargada después
    // con SceneManager.LoadScene (por ejemplo, del torneo al partido).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // evita suscribirse dos veces si el Domain Reload está desactivado
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<RinkVisual>() != null) return;

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm == null || gm.rinkWalls == null) return;

        // Se crea en la escena recién cargada, así se destruye con ella al volver al torneo
        GameObject go = new GameObject("RinkVisual");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<RinkVisual>();
    }

    void Start()
    {
        Build();
    }

    void Build()
    {
        Bounds b;
        if (!Rink.TryGetBounds(out b)) return;

        EdgeCollider2D edge = GameManager.Instance.rinkWalls;
        mat = new Material(Shader.Find("Sprites/Default"));

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = outsideColor;
        }

        // Contorno de la pista (puntos del collider en coordenadas de mundo)
        Vector2[] local = edge.points;
        List<Vector3> outline = new List<Vector3>();
        int count = local.Length;
        if (count > 1 && Vector2.Distance(local[0], local[count - 1]) < 0.001f) count--;   // el último repite el primero
        for (int i = 0; i < count; i++)
            outline.Add(edge.transform.TransformPoint(local[i] + edge.offset));

        BuildFloor(outline, b.center);

        // Bandas
        MakeLine("Bandas", outline.ToArray(), boardWidth, boardColor, true, -90);

        // Línea central y círculo
        Vector3 c = b.center;
        MakeLine("LineaCentral", new[] { new Vector3(c.x, b.min.y, 0f), new Vector3(c.x, b.max.y, 0f) },
                 0.25f, centerLineColor, false, -95);
        MakeLine("CirculoCentral", Circle(c, centerCircleRadius, 64), 0.15f, lineColor, true, -95);

        // Áreas de portería (semicírculo delante de cada portería, hacia el centro de la pista)
        for (int i = 0; i < Goal.All.Count; i++)
        {
            Vector3 gp = Goal.All[i].transform.position;
            float front = gp.x < c.x ? 1f : -1f;

            Vector3[] arc = new Vector3[25];
            for (int k = 0; k < arc.Length; k++)
            {
                float a = Mathf.Lerp(-90f, 90f, k / 24f) * Mathf.Deg2Rad;
                arc[k] = gp + new Vector3(front * Mathf.Cos(a), Mathf.Sin(a), 0f) * creaseRadius;
            }
            MakeLine("AreaPorteria" + i, arc, 0.15f, lineColor, false, -95);
        }
    }

    // El hielo es un abanico de triángulos desde el centro (la pista es convexa)
    void BuildFloor(List<Vector3> outline, Vector3 center)
    {
        GameObject go = new GameObject("Hielo");
        go.transform.SetParent(transform, false);

        Vector3[] verts = new Vector3[outline.Count + 1];
        verts[0] = center;
        for (int i = 0; i < outline.Count; i++) verts[i + 1] = outline[i];

        int[] tris = new int[outline.Count * 3];
        for (int i = 0; i < outline.Count; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = 1 + i;
            tris[i * 3 + 2] = 1 + (i + 1) % outline.Count;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        Material floorMat = new Material(Shader.Find("Sprites/Default"));
        floorMat.color = iceColor;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = floorMat;
        mr.sortingOrder = -100;
    }

    Vector3[] Circle(Vector3 center, float radius, int points)
    {
        Vector3[] p = new Vector3[points];
        for (int i = 0; i < points; i++)
        {
            float a = i / (float)points * Mathf.PI * 2f;
            p[i] = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
        }
        return p;
    }

    void MakeLine(string objName, Vector3[] points, float width, Color color, bool loop, int order)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = mat;
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.widthMultiplier = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = order;
        lr.positionCount = points.Length;
        lr.SetPositions(points);
    }
}