using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Dibuja la pista de HOCKEY PATINES (solo visual: no toca colliders, porterías ni porteros).
//
// Marcas según el reglamento técnico de World Skate (pista de referencia 40 x 20 m):
//   - Área de penalti: rectángulo de 9 m (paralelo a la línea de gol) x 5,40 m (hacia el centro).
//   - Zona de protección del portero: semicírculo centrado en el medio de la línea de gol.
//   - Línea de gol entre los postes.
//   - Punto de penalti a 5,40 m y punto de falta directa a 7,40 m del centro de la línea de gol.
//   - Línea central y círculo central de 3 m de radio con su punto central.
//
// Las medidas reales (metros) se pasan a unidades de Unity con una escala automática:
//   escala = min(anchoPista / 40, altoPista / 20)
// y todo lo que depende de la portería se mide desde su BOCA real (Goal.Mouth), así las marcas
// siempre cuadran con las porterías de la escena aunque no estén a la distancia reglamentaria.
//
// El suelo es una textura procedural (parquet o terrazo) generada al cargar la escena.
// Se crea solo en cualquier escena con GameManager.rinkWalls. Si quieres ajustar valores en el
// Inspector, añade este componente a un objeto de la escena: entonces se usa ese y no se crea otro.
public class RinkVisual : MonoBehaviour
{
    public enum FloorType { Parquet, Terrazo }

    [Header("Suelo")]
    [SerializeField] FloorType floorType = FloorType.Parquet;
    [Tooltip("Resolución de la textura del suelo (píxeles por unidad de Unity)")]
    [SerializeField, Range(32, 160)] int pixelsPerUnit = 96;
    [Tooltip("Oscurece los bordes de la pista (efecto foco). 0 = sin efecto")]
    [SerializeField, Range(0f, 0.4f)] float vignette = 0.15f;
    [SerializeField] int seed = 7;

    [Header("Parquet (tablas a lo largo de la pista)")]
    [SerializeField] Color woodColor = new Color(0.86f, 0.64f, 0.40f);
    [SerializeField, Range(0f, 0.3f)] float plankTintVariation = 0.09f;
    [Tooltip("Ancho de cada tabla en unidades de Unity (exagerado respecto a la realidad para que se lea en pantalla)")]
    [SerializeField] float plankWidth = 0.42f;
    [SerializeField] float plankLength = 3.4f;

    [Header("Terrazo")]
    [SerializeField] Color terrazzoBase = new Color(0.80f, 0.79f, 0.76f);
    [SerializeField] Color[] terrazzoChips =
    {
        new Color(0.55f, 0.53f, 0.50f),
        new Color(0.30f, 0.29f, 0.28f),
        new Color(0.93f, 0.92f, 0.89f),
        new Color(0.66f, 0.50f, 0.40f),
        new Color(0.45f, 0.52f, 0.55f),
    };
    [SerializeField] float terrazzoTileSize = 6f;

    [Header("Medidas reales (metros, reglamento World Skate)")]
    [SerializeField] float realRinkLength = 40f;
    [SerializeField] float realRinkWidth = 20f;
    [SerializeField] float penaltyAreaWidth = 9f;
    [SerializeField] float penaltyAreaDepth = 5.4f;
    [Tooltip("Radio del semicírculo de protección del portero")]
    [SerializeField] float creaseRadius = 1.5f;
    [SerializeField] float penaltySpotDistance = 5.4f;
    [SerializeField] float freeHitSpotDistance = 7.4f;
    [SerializeField] float centerCircleRadius = 3f;

    [Header("Aspecto de las marcas (unidades de Unity)")]
    [Tooltip("Grosor de las líneas. Real: 8 cm; aquí algo más gruesas para que se vean")]
    [SerializeField] float lineWidth = 0.1f;
    [SerializeField] float spotRadius = 0.12f;
    [SerializeField] Color lineColor = new Color(0.10f, 0.20f, 0.52f, 0.95f);
    [SerializeField] Color goalLineColor = new Color(0.85f, 0.15f, 0.12f, 1f);
    [Tooltip("Relleno del semicírculo del portero (alpha 0 = sin relleno)")]
    [SerializeField] Color creaseFill = new Color(0.10f, 0.20f, 0.52f, 0.22f);
    [Tooltip("Relleno del área de penalti (alpha 0 = sin relleno)")]
    [SerializeField] Color penaltyAreaFill = new Color(0.10f, 0.20f, 0.52f, 0.06f);

    [Header("Bandas y fondo")]
    [SerializeField] Color outsideColor = new Color(0.08f, 0.10f, 0.14f);
    [SerializeField] Color boardColor = new Color(0.10f, 0.14f, 0.22f);
    [SerializeField] float boardWidth = 0.5f;

    // Orden de dibujo (todo por debajo de jugadores y bola)
    const int FloorOrder = -100;
    const int FillOrder = -98;
    const int LineOrder = -95;
    const int SpotOrder = -94;
    const int BoardOrder = -90;

    Material mat;
    readonly List<Object> generated = new List<Object>();   // texturas, materiales y mallas creadas en runtime

    // Se suscribe a sceneLoaded UNA vez al arrancar el juego, antes de cargar la primera escena.
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

    void OnDestroy()
    {
        Clear();
    }

    // En Play: menú ⋮ del componente > "Reconstruir" para ver los cambios del Inspector al momento
    [ContextMenu("Reconstruir")]
    void Rebuild()
    {
        Clear();
        Build();
    }

    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        for (int i = 0; i < generated.Count; i++) if (generated[i] != null) Destroy(generated[i]);
        generated.Clear();
    }

    void Build()
    {
        Bounds b;
        if (!Rink.TryGetBounds(out b)) return;

        // Las mallas y líneas usan coordenadas de mundo: el objeto padre tiene que estar en el origen
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;

        EdgeCollider2D edge = GameManager.Instance.rinkWalls;
        mat = Track(new Material(Shader.Find("Sprites/Default")));

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

        // Metros -> unidades de Unity (uniforme, para que los círculos sean círculos)
        float s = Mathf.Min(b.size.x / realRinkLength, b.size.y / realRinkWidth);
        Vector3 c = b.center;

        // Línea central, círculo central y punto central
        MakeLine("LineaCentral", new[] { new Vector3(c.x, b.min.y, 0f), new Vector3(c.x, b.max.y, 0f) },
                 lineWidth, lineColor, false, LineOrder);
        MakeLine("CirculoCentral", Circle(c, centerCircleRadius * s, 72), lineWidth, lineColor, true, LineOrder);
        MakeFill("PuntoCentral", Circle(c, spotRadius, 20), lineColor, SpotOrder);

        for (int i = 0; i < Goal.All.Count; i++)
            BuildGoalEnd(Goal.All[i], i, c, b, s);

        // Bandas por encima de todo lo del suelo
        MakeLine("Bandas", outline.ToArray(), boardWidth, boardColor, true, BoardOrder);
    }

    // Marcas de un fondo de pista: área de penalti, semicírculo del portero, línea de gol y puntos
    void BuildGoalEnd(Goal goal, int index, Vector3 center, Bounds rink, float s)
    {
        Vector2 mouth2 = goal.Mouth;
        Vector3 mouth = new Vector3(mouth2.x, mouth2.y, 0f);
        float front = mouth.x < center.x ? 1f : -1f;   // dirección hacia el centro de la pista

        // Si en la escena la portería está más cerca del centro que en la realidad, se encogen las marcas
        // de este fondo para que el punto de falta directa no se coma el círculo central.
        float distToCenter = Mathf.Abs(center.x - mouth.x);
        float sg = Mathf.Min(s, distToCenter / (freeHitSpotDistance + centerCircleRadius + 2f));

        float depth = penaltyAreaDepth * sg;
        float halfW = Mathf.Min(penaltyAreaWidth * 0.5f * sg, rink.extents.y - lineWidth);

        // Área de penalti: rectángulo que arranca en la línea de gol (prolongada) y va hacia el centro
        Vector3[] rect =
        {
            mouth + new Vector3(0f, -halfW, 0f),
            mouth + new Vector3(front * depth, -halfW, 0f),
            mouth + new Vector3(front * depth, halfW, 0f),
            mouth + new Vector3(0f, halfW, 0f),
        };
        if (penaltyAreaFill.a > 0.001f) MakeFill("AreaPenaltiRelleno" + index, rect, penaltyAreaFill, FillOrder);
        MakeLine("AreaPenalti" + index, rect, lineWidth, lineColor, true, LineOrder);

        // Semicírculo de protección del portero (nunca más estrecho que la boca de la portería)
        float r = Mathf.Max(creaseRadius * sg, goal.HalfWidth + lineWidth);
        Vector3[] arc = Arc(mouth, r, front, 40);
        if (creaseFill.a > 0.001f)
        {
            Vector3[] fill = new Vector3[arc.Length + 1];
            fill[0] = mouth;
            for (int k = 0; k < arc.Length; k++) fill[k + 1] = arc[k];
            MakeFill("SemicirculoRelleno" + index, fill, creaseFill, FillOrder + 1);
        }
        MakeLine("Semicirculo" + index, arc, lineWidth, lineColor, false, LineOrder);

        // Línea de gol, solo entre los postes (encima de la línea del área)
        float hw = goal.HalfWidth;
        MakeLine("LineaGol" + index, new[] { mouth + new Vector3(0f, -hw, 0f), mouth + new Vector3(0f, hw, 0f) },
                 lineWidth * 1.3f, goalLineColor, false, LineOrder + 1);

        // Punto de penalti y de falta directa
        MakeFill("PuntoPenalti" + index, Circle(mouth + new Vector3(front * penaltySpotDistance * sg, 0f, 0f), spotRadius, 20),
                 lineColor, SpotOrder);
        MakeFill("PuntoFaltaDirecta" + index, Circle(mouth + new Vector3(front * freeHitSpotDistance * sg, 0f, 0f), spotRadius, 20),
                 lineColor, SpotOrder);
    }

    // ---------------------------------------------------------------- SUELO

    // Abanico de triángulos desde el centro (la pista es convexa). UV en coordenadas de mundo para que
    // la textura se repita sin estirarse; color de vértice para el viñeteado (centro claro, bordes oscuros).
    void BuildFloor(List<Vector3> outline, Vector3 center)
    {
        GameObject go = new GameObject("Suelo");
        go.transform.SetParent(transform, false);

        Texture2D tex;
        Vector2 tile;   // tamaño de una repetición de la textura en unidades de Unity
        if (floorType == FloorType.Parquet) tex = MakeParquet(out tile);
        else tex = MakeTerrazzo(out tile);

        Vector3[] verts = new Vector3[outline.Count + 1];
        Vector2[] uvs = new Vector2[verts.Length];
        Color[] cols = new Color[verts.Length];

        verts[0] = center;
        cols[0] = Color.white;
        Color edgeCol = new Color(1f - vignette, 1f - vignette, 1f - vignette, 1f);
        for (int i = 0; i < outline.Count; i++)
        {
            verts[i + 1] = outline[i];
            cols[i + 1] = edgeCol;
        }
        for (int i = 0; i < verts.Length; i++)
            uvs[i] = new Vector2(verts[i].x / tile.x, verts[i].y / tile.y);

        int[] tris = new int[outline.Count * 3];
        for (int i = 0; i < outline.Count; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = 1 + i;
            tris[i * 3 + 2] = 1 + (i + 1) % outline.Count;
        }

        Mesh mesh = Track(new Mesh());
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = cols;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        Material floorMat = Track(new Material(Shader.Find("Sprites/Default")));
        floorMat.mainTexture = tex;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = floorMat;
        mr.sortingOrder = FloorOrder;
    }

    // Parquet: filas de tablas a lo largo de la pista (eje X), juntas desalineadas entre filas,
    // cada tabla con su tono y veta propia. La textura es periódica en X e Y (se repite sin costuras).
    Texture2D MakeParquet(out Vector2 tile)
    {
        const int rows = 8;            // filas de tablas por repetición
        const int planksPerRow = 2;    // tablas por fila y repetición

        tile = new Vector2(plankLength * planksPerRow, plankWidth * rows);
        int w = Mathf.Clamp(Mathf.RoundToInt(tile.x * pixelsPerUnit), 64, 2048);
        int h = Mathf.Clamp(Mathf.RoundToInt(tile.y * pixelsPerUnit), 64, 2048);

        System.Random rng = new System.Random(seed);
        int n = rows * planksPerRow;
        float[] jointOffset = new float[rows];
        float[] tint = new float[n];
        float[] phase = new float[n];
        float[] grainLines = new float[n];
        for (int r = 0; r < rows; r++) jointOffset[r] = (float)rng.NextDouble() * w;
        for (int i = 0; i < n; i++)
        {
            tint[i] = (float)rng.NextDouble() * 2f - 1f;
            phase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
            grainLines[i] = 3f + (float)rng.NextDouble() * 4f;
        }

        float rowH = h / (float)rows;
        float plankPx = w / (float)planksPerRow;
        Color32[] px = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            int r = Mathf.Min(rows - 1, (int)(y / rowH));
            float vy = (y - r * rowH) / rowH;                        // 0..1 dentro de la fila
            float distEdge = Mathf.Min(vy, 1f - vy) * rowH;          // píxeles hasta el canto largo

            for (int x = 0; x < w; x++)
            {
                float u = x - jointOffset[r];
                if (u < 0f) u += w;
                int seg = Mathf.Min(planksPerRow - 1, (int)(u / plankPx));
                float us = u - seg * plankPx;
                float distJoint = Mathf.Min(us, plankPx - us);       // píxeles hasta la testa
                int id = r * planksPerRow + seg;

                // Veta: líneas a lo largo de la tabla, onduladas con senos de periodo entero (sin costuras)
                float xn = x / (float)w * Mathf.PI * 2f;
                float bend = 0.30f * Mathf.Sin(xn * 2f + phase[id]) + 0.12f * Mathf.Sin(xn * 5f + phase[id] * 2.3f);
                float wave = Mathf.Sin((vy * grainLines[id] + bend) * Mathf.PI * 2f);
                float fine = Hash(x, y) - 0.5f;

                float shade = 1f + tint[id] * plankTintVariation;
                shade *= 1f + 0.05f * wave + 0.035f * fine;
                shade *= 1f - 0.04f * Mathf.Abs(vy - 0.5f);           // canto un poco más oscuro (bisel)

                float joint = Mathf.Min(distEdge, distJoint);
                if (joint < 1f) shade *= 0.55f;
                else if (joint < 2f) shade *= 0.82f;

                Color col = woodColor * shade;
                col.a = 1f;
                px[y * w + x] = col;
            }
        }

        return FinishTexture(px, w, h);
    }

    // Terrazo: base clara con granos de piedra de varios colores y tamaños (pocos grandes, muchos pequeños).
    Texture2D MakeTerrazzo(out Vector2 tile)
    {
        tile = new Vector2(terrazzoTileSize, terrazzoTileSize);
        int size = Mathf.Clamp(Mathf.RoundToInt(terrazzoTileSize * pixelsPerUnit), 64, 2048);
        Color32[] px = new Color32[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float shade = 1f + 0.05f * (Hash(x, y) - 0.5f);
                Color col = terrazzoBase * shade;
                col.a = 1f;
                px[y * size + x] = col;
            }

        if (terrazzoChips != null && terrazzoChips.Length > 0)
        {
            System.Random rng = new System.Random(seed);
            float maxR = 0.07f * pixelsPerUnit;
            float minR = 0.012f * pixelsPerUnit + 0.8f;
            int chips = Mathf.RoundToInt(size * size * 0.004f);

            for (int i = 0; i < chips; i++)
            {
                float cx = (float)rng.NextDouble() * size;
                float cy = (float)rng.NextDouble() * size;
                float t = (float)rng.NextDouble();
                float radius = Mathf.Lerp(minR, maxR, t * t * t);
                float aspect = 0.55f + (float)rng.NextDouble() * 0.45f;
                float ang = (float)rng.NextDouble() * Mathf.PI;
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                float lobe = (float)rng.NextDouble() * Mathf.PI * 2f;

                Color chip = terrazzoChips[rng.Next(terrazzoChips.Length)];
                chip *= 0.92f + (float)rng.NextDouble() * 0.16f;
                chip.a = 1f;
                Color32 chip32 = chip;

                int ri = Mathf.CeilToInt(radius) + 1;
                for (int dy = -ri; dy <= ri; dy++)
                    for (int dx = -ri; dx <= ri; dx++)
                    {
                        // Elipse girada con el borde irregular (aspecto de piedra triturada)
                        float lx = (dx * ca + dy * sa) / radius;
                        float ly = (-dx * sa + dy * ca) / (radius * aspect);
                        float a = Mathf.Atan2(ly, lx);
                        float edge = 1f + 0.18f * Mathf.Sin(a * 3f + lobe) + 0.08f * Mathf.Sin(a * 5f + lobe * 1.7f);
                        if (lx * lx + ly * ly > edge * edge) continue;

                        int x = ((int)(cx + dx) % size + size) % size;   // envuelve: textura sin costuras
                        int y = ((int)(cy + dy) % size + size) % size;
                        px[y * size + x] = chip32;
                    }
            }
        }

        return FinishTexture(px, size, size);
    }

    Texture2D FinishTexture(Color32[] px, int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.SetPixels32(px);
        tex.Apply(true, true);   // genera mipmaps (evita el parpadeo de las juntas al alejar) y libera la copia en CPU
        return Track(tex);
    }

    // Ruido blanco determinista 0..1 por píxel
    static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    // ---------------------------------------------------------------- UTILIDADES

    T Track<T>(T obj) where T : Object
    {
        generated.Add(obj);
        return obj;
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

    // Semicírculo de -90º a 90º mirando hacia 'front' (+1 derecha, -1 izquierda)
    Vector3[] Arc(Vector3 center, float radius, float front, int segments)
    {
        Vector3[] p = new Vector3[segments + 1];
        for (int k = 0; k <= segments; k++)
        {
            float a = Mathf.Lerp(-90f, 90f, k / (float)segments) * Mathf.Deg2Rad;
            p[k] = center + new Vector3(front * Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
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
        lr.numCornerVertices = 2;
        lr.sortingOrder = order;
        lr.positionCount = points.Length;
        lr.SetPositions(points);
    }

    // Polígono convexo relleno (abanico desde el primer punto)
    void MakeFill(string objName, Vector3[] poly, Color color, int order)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform, false);

        Color[] cols = new Color[poly.Length];
        for (int i = 0; i < cols.Length; i++) cols[i] = color;

        int[] tris = new int[(poly.Length - 2) * 3];
        for (int i = 0; i < poly.Length - 2; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 2;
            tris[i * 3 + 2] = i + 1;
        }

        Mesh mesh = Track(new Mesh());
        mesh.vertices = poly;
        mesh.colors = cols;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder = order;
    }
}
