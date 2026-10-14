using UnityEngine;

// Cosmetic energy bolt fired by SpiderBotVisual (damage is already applied by the enemy's own
// attack tick). Pooled through PrefabPool; flies on a slight arc, pops an impact, returns itself.
public class SpiderBolt : MonoBehaviour
{
    public TrailRenderer trail;
    public Transform head;
    public float speed = 38f;
    public float arc = 0.6f;
    public GameObject impactVfx;

    Vector3 from, to;
    float t, duration;
    bool flying;
    Renderer headRenderer;
    MaterialPropertyBlock mpb;
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (head != null) headRenderer = head.GetComponent<Renderer>();
    }

    public void Launch(Vector3 start, Vector3 end, Color color)
    {
        from = start; to = end;
        t = 0f;
        duration = Mathf.Max(0.05f, Vector3.Distance(start, end) / speed);
        flying = true;
        transform.position = start;
        if (trail != null)
        {
            trail.Clear();
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
        if (headRenderer != null)
        {
            mpb.SetColor(ColorId, color);
            headRenderer.SetPropertyBlock(mpb);
        }
    }

    void Update()
    {
        if (!flying) return;
        t += Time.deltaTime / duration;
        float k = Mathf.Clamp01(t);
        Vector3 p = Vector3.Lerp(from, to, k) + Vector3.up * (arc * 4f * k * (1f - k));
        transform.position = p;
        if (k >= 1f)
        {
            flying = false;
            if (impactVfx != null) PooledVfx.Play(impactVfx, to, Quaternion.identity);
            PrefabPool.Release(gameObject);
        }
    }
}
