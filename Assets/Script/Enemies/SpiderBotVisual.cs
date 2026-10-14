using UnityEngine;

// Presentation for the Spider Robot enemy. Sits on the "Visual" child of an enemy that keeps its
// existing SmallWorm gameplay (health, chase, damage ticks); this only animates and adds effects:
//   - locomotion blend (Idle / Roam / Walk) from the enemy's real ground speed, so feet don't slide
//   - Attack: crouch, eye charge, energy bolt towards whatever the damage tick hit
//   - Death: detaches from the destroyed enemy, flashes, sparks, plays the collapse, then cleans up
// No per-frame allocations; one MaterialPropertyBlock per spider drives the eye glow.
public class SpiderBotVisual : MonoBehaviour
{
    public Animator animator;
    public SkinnedMeshRenderer body;
    [Tooltip("Bolt spawn point (the exposed Eye bone).")]
    public Transform muzzle;
    [ColorUsage(false, true)] public Color glow = new Color(1f, 0.69f, 0.13f);
    public float baseGlow = 2.2f;

    [Header("Attack")]
    public GameObject boltPrefab;
    [Tooltip("Seconds into the Attack clip when the bolt leaves the eye (frame 13 at 30 fps).")]
    public float fireDelay = 0.4f;

    [Header("Death")]
    public GameObject deathVfx;
    public float deathDuration = 1.7f;

    [Header("Ground")]
    [Tooltip("Feet follow the terrain: the gameplay root moves at a fixed height (Y is frozen), the visual snaps to the ground under it.")]
    public LayerMask groundMask = 1 << 6;

    [Header("Locomotion")]
    [Tooltip("Ground speed (m/s) the Walk clip was authored for; faster movement speeds the clip up.")]
    public float walkClipSpeed = 3.4f;
    public float maxPlaybackSpeed = 2.6f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int DeathHash = Animator.StringToHash("Death");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    SmallWorm owner;
    MaterialPropertyBlock mpb;
    Vector3 lastPos;
    float speed;
    float attackClock = -1f;
    Transform attackTarget;
    Vector3 attackPoint;
    bool fired;
    bool dead;
    float deathClock;
    float glowLevel;
    float sink;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        owner = GetComponentInParent<SmallWorm>();
        if (owner != null) { owner.Attacked += OnAttacked; owner.Died += OnDied; }
        lastPos = transform.position;
        glowLevel = baseGlow;
        ApplyGlow(baseGlow);
    }

    void OnDestroy()
    {
        if (owner != null) { owner.Attacked -= OnAttacked; owner.Died -= OnDied; }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        SnapToGround(dt);
        if (dead) { UpdateDeath(dt); return; }

        // locomotion from actual movement (SmallWorm moves the root by transform)
        Vector3 p = transform.position;
        Vector3 d = p - lastPos; d.y = 0f;
        lastPos = p;
        speed = Mathf.Lerp(speed, d.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, Mathf.Min(speed, walkClipSpeed));
            animator.speed = attackClock >= 0f ? 1f : Mathf.Clamp(speed / walkClipSpeed, 1f, maxPlaybackSpeed);
        }

        // attack: eye charges until the bolt fires, flashes, then settles
        float target = baseGlow;
        if (attackClock >= 0f)
        {
            attackClock += dt;
            if (!fired)
            {
                target = Mathf.Lerp(baseGlow, baseGlow * 3.5f, attackClock / fireDelay);
                if (attackTarget != null) attackPoint = attackTarget.position;
                if (attackClock >= fireDelay) Fire();
            }
            else if (attackClock > fireDelay + 0.6f) attackClock = -1f;
        }
        glowLevel = fired && attackClock >= 0f && attackClock < fireDelay + 0.12f ? baseGlow * 5f : Mathf.MoveTowards(glowLevel, target, baseGlow * 8f * dt);
        ApplyGlow(glowLevel);
    }

    void OnAttacked(Transform hit)
    {
        if (dead || attackClock >= 0f) return;
        attackClock = 0f;
        fired = false;
        attackTarget = hit;
        attackPoint = hit != null ? hit.position : transform.position + transform.forward * 4f;
        if (animator != null) animator.SetTrigger(AttackHash);
    }

    void Fire()
    {
        fired = true;
        if (boltPrefab == null) return;
        Vector3 from = muzzle != null ? muzzle.position + transform.forward * 0.2f : transform.position + Vector3.up * 0.7f;
        var go = PrefabPool.Spawn(boltPrefab, from, Quaternion.identity);
        var bolt = go != null ? go.GetComponent<SpiderBolt>() : null;
        if (bolt != null) bolt.Launch(from, attackPoint + Vector3.up * 0.6f, glow);
    }

    void OnDied()
    {
        if (dead) return;
        dead = true;
        deathClock = 0f;
        owner.Attacked -= OnAttacked; owner.Died -= OnDied;
        owner = null;
        // outlive the gameplay object, which is destroyed right after this event
        transform.SetParent(null, true);
        if (animator != null) { animator.speed = 1f; animator.SetTrigger(DeathHash); }
        if (deathVfx != null) PooledVfx.Play(deathVfx, transform.position + Vector3.up * 0.6f, Quaternion.identity);
    }

    void UpdateDeath(float dt)
    {
        deathClock += dt;
        // white-hot flash, sputter, then the eye goes dark
        float g = deathClock < 0.12f ? baseGlow * 6f
                : deathClock < 0.7f ? baseGlow * (Mathf.PingPong(deathClock * 14f, 1f) > 0.5f ? 2.5f : 0.6f)
                : Mathf.MoveTowards(glowLevel, 0f, baseGlow * 3f * dt);
        glowLevel = g;
        ApplyGlow(g);
        if (deathClock > deathDuration - 0.4f) sink += 1.2f * dt; // sink into the sand
        if (deathClock >= deathDuration) Destroy(gameObject);
    }

    void SnapToGround(float dt)
    {
        Vector3 p = transform.position;
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, groundMask, QueryTriggerInteraction.Ignore))
        {
            float y = hit.point.y - sink;
            p.y = Mathf.Abs(p.y - y) > 1f ? y : Mathf.Lerp(p.y, y, 1f - Mathf.Exp(-20f * dt));
            transform.position = p;
        }
    }

    void ApplyGlow(float intensity)
    {
        if (body == null) return;
        body.GetPropertyBlock(mpb);
        mpb.SetColor(EmissionId, glow * intensity);
        body.SetPropertyBlock(mpb);
    }
}
