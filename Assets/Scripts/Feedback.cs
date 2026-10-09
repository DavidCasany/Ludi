using System.Collections.Generic;
using UnityEngine;

// Feedback visual sencillo para que se entienda qué pasa:
//  - Textos flotantes ("¡ROBO!", "¡PROTEGIDA!", "¡REGATE!"...)
//  - El arco del stick cuando alguien intenta robar o interceptar.
// Se crea solo la primera vez que se usa (no hay que añadirlo a la escena).
public class Feedback : MonoBehaviour
{
    static Feedback instance;

    class PopupData
    {
        public Vector2 pos;
        public string text;
        public Color color;
        public float born;
        public float life;
    }

    class SwingData
    {
        public GameObject go;
        public float expire;
    }

    readonly List<PopupData> popups = new List<PopupData>();
    readonly List<SwingData> swings = new List<SwingData>();
    Material lineMat;
    Camera cam;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    static Feedback Get()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("Feedback");
            instance = go.AddComponent<Feedback>();
        }
        return instance;
    }

    // ---------- API ----------

    public static void Popup(Vector2 worldPos, string text, Color color, float life = 0.9f)
    {
        PopupData p = new PopupData();
        p.pos = worldPos;
        p.text = text;
        p.color = color;
        p.born = Time.unscaledTime;
        p.life = life;
        Get().popups.Add(p);
    }

    public static void Swing(Vector2 origin, Vector2 dir, float reach, float arcDegrees)
    {
        Get().SpawnSwing(origin, dir, reach, arcDegrees);
    }

    // ---------- Arco del stick ----------

    void SpawnSwing(Vector2 origin, Vector2 dir, float reach, float arc)
    {
        if (lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default"));

        GameObject go = new GameObject("StickSwing");
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = lineMat;
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.07f;
        lr.sortingOrder = 40;
        lr.startColor = new Color(1f, 1f, 1f, 0.55f);
        lr.endColor = new Color(1f, 1f, 1f, 0.55f);

        const int steps = 8;
        lr.positionCount = steps + 3;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lr.SetPosition(0, origin);
        for (int i = 0; i <= steps; i++)
        {
            float a = (baseAngle - arc * 0.5f + arc * i / steps) * Mathf.Deg2Rad;
            lr.SetPosition(i + 1, origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach);
        }
        lr.SetPosition(steps + 2, origin);

        SwingData s = new SwingData();
        s.go = go;
        s.expire = Time.unscaledTime + 0.15f;
        swings.Add(s);
    }

    void Update()
    {
        for (int i = swings.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime < swings[i].expire) continue;
            Destroy(swings[i].go);
            swings.RemoveAt(i);
        }

        for (int i = popups.Count - 1; i >= 0; i--)
            if (Time.unscaledTime - popups[i].born >= popups[i].life) popups.RemoveAt(i);
    }

    // ---------- Textos flotantes ----------

    void OnGUI()
    {
        if (popups.Count == 0) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 22;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
        }

        for (int i = 0; i < popups.Count; i++)
        {
            PopupData p = popups[i];
            float t = (Time.unscaledTime - p.born) / p.life;
            if (t >= 1f) continue;

            Vector3 sp = cam.WorldToScreenPoint(p.pos);
            if (sp.z < 0f) continue;

            float x = sp.x;
            float y = Screen.height - sp.y - 60f - 50f * t;   // sube mientras se desvanece
            float alpha = 1f - t * t;

            Rect r = new Rect(x - 120f, y - 15f, 240f, 30f);

            style.normal.textColor = new Color(0f, 0f, 0f, alpha);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), p.text, style);

            Color c = p.color;
            c.a = alpha;
            style.normal.textColor = c;
            GUI.Label(r, p.text, style);
        }
    }
}
