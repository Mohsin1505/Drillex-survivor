using UnityEngine;

// One-shot effect that restarts its particles (and lightning) every time it is spawned from the
// PrefabPool and returns itself after `lifetime` seconds of game time (so it freezes while paused).
public class PooledVfx : MonoBehaviour
{
    public float lifetime = 1.5f;

    ParticleSystem[] systems;
    TrailRenderer[] trails;
    float age;

    void Awake()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
        trails = GetComponentsInChildren<TrailRenderer>(true);
    }

    void OnEnable()
    {
        age = 0f;
        if (systems == null) Awake();
        foreach (var t in trails) t.Clear();
        foreach (var ps in systems)
        {
            ps.Clear(false);
            if (ps.main.playOnAwake || ps.transform == transform) ps.Play(false);
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime) PrefabPool.Release(gameObject);
    }

    public static void Play(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab != null) PrefabPool.Spawn(prefab, position, rotation);
    }
}
