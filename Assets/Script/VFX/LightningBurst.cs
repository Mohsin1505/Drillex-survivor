using UnityEngine;

// Procedural electric arcs drawn with a handful of LineRenderers (no textures sheets, no lights).
// Each arc is a jagged polyline from the origin in a random direction inside a cone, with an
// optional fork, re-jittered `flickerRate` times per second and thinned out over `duration`.
// duration <= 0 keeps it alive (looping idle arcs on the Tesla coils, arcs around the bolt).
public class LightningBurst : MonoBehaviour
{
    public Material material;
    public Gradient color;
    public int arcs = 3;
    public int segments = 7;
    public Vector2 length = new Vector2(0.6f, 1.4f);
    [Range(0f, 180f)] public float coneAngle = 180f;
    public float jitter = 0.18f;
    public float width = 0.06f;
    public float duration = 0.18f;
    public float flickerRate = 30f;
    [Range(0f, 1f)] public float forkChance = 0.5f;
    [Tooltip("For looping arcs: chance per flicker that an arc is hidden, so it crackles instead of buzzing.")]
    [Range(0f, 1f)] public float dropout = 0f;
    public bool useWorldSpace = false;

    LineRenderer[] lines;
    Vector3[] dirs;
    float[] lens;
    float age, nextFlicker;
    Vector3[] pts;
    Vector3 lastMid;

    void OnEnable()
    {
        Build();
        age = 0f;
        nextFlicker = 0f;
        Randomise();
    }

    void Build()
    {
        int count = arcs * 2;
        if (lines != null && lines.Length == count && lines[0] != null) return;
        foreach (Transform c in transform)
            if (c.name.StartsWith("arc_")) Destroy(c.gameObject);
        lines = new LineRenderer[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("arc_" + i);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = material;
            lr.useWorldSpace = useWorldSpace;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 0;
            lr.alignment = LineAlignment.View;
            lr.colorGradient = color ?? DefaultGradient();
            lines[i] = lr;
        }
        dirs = new Vector3[count];
        lens = new float[count];
        pts = new Vector3[segments + 1];
    }

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.7f, 1f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.2f, 1f) });
        return g;
    }

    void Randomise()
    {
        for (int i = 0; i < arcs; i++)
        {
            Vector3 d = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)) * Quaternion.Euler(Random.Range(0f, coneAngle), 0f, 0f) * Vector3.forward;
            dirs[i] = d;
            lens[i] = Random.Range(length.x, length.y);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (duration > 0f && age >= duration)
        {
            foreach (var l in lines) if (l) l.enabled = false;
            return;
        }
        nextFlicker -= dt;
        if (nextFlicker > 0f) return;
        nextFlicker = 1f / Mathf.Max(1f, flickerRate);
        if (duration <= 0f) Randomise();

        float life = duration > 0f ? 1f - age / duration : 1f;
        for (int i = 0; i < arcs; i++)
        {
            bool show = dropout <= 0f || Random.value > dropout;
            Draw(lines[i], Vector3.zero, dirs[i], lens[i], width * life, show);
            // fork from a point along the main arc
            bool fork = show && Random.value < forkChance;
            if (fork)
            {
                Vector3 from = lastMid;
                Vector3 fd = (dirs[i] + Random.insideUnitSphere * 0.9f).normalized;
                Draw(lines[arcs + i], from, fd, lens[i] * 0.45f, width * 0.6f * life, true);
            }
            else lines[arcs + i].enabled = false;
        }
    }

    void Draw(LineRenderer lr, Vector3 origin, Vector3 dir, float len, float w, bool show)
    {
        lr.enabled = show;
        if (!show) return;
        lr.positionCount = segments + 1;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        side.Normalize();
        Vector3 up = Vector3.Cross(side, dir);
        for (int s = 0; s <= segments; s++)
        {
            float t = s / (float)segments;
            float j = (s == 0 || s == segments) ? 0f : jitter * len * Mathf.Sin(t * Mathf.PI);
            pts[s] = origin + dir * (len * t) + side * Random.Range(-j, j) + up * Random.Range(-j, j);
        }
        lastMid = pts[segments / 2];
        if (useWorldSpace)
            for (int s = 0; s <= segments; s++) pts[s] = transform.TransformPoint(pts[s]);
        lr.SetPositions(pts);
        lr.widthMultiplier = w;
    }
}
