using UnityEngine;

// Lives on the DRL_Driller prefab root. Applies a CrewLook: generates the palette texture into a
// per-instance material and toggles the headgear meshes. Animation: Animator with Idle (default),
// Wave and Cheer triggers.
[DisallowMultipleComponent]
public class CrewAppearance : MonoBehaviour
{
    public SkinnedMeshRenderer body;
    [Tooltip("Hard hat, welder mask, goggle beanie - same order as CrewProfile.HeadgearNames.")]
    public SkinnedMeshRenderer[] headgear = new SkinnedMeshRenderer[CrewProfile.HeadgearCount];
    public Animator animator;
    [Tooltip("Follow the saved player profile (and update when it changes).")]
    public bool followProfile = true;

    static readonly int WaveHash = Animator.StringToHash("Wave");
    static readonly int CheerHash = Animator.StringToHash("Cheer");

    Material instance;
    Texture2D palette;
    CrewLook current;
    bool applied;

    public CrewLook Current => current;

    void OnEnable()
    {
        CrewProfile.Changed += OnProfileChanged;
        if (followProfile) Apply(CrewProfile.Look);
        else if (!applied) Apply(current);
    }

    void OnDisable()
    {
        CrewProfile.Changed -= OnProfileChanged;
    }

    void OnDestroy()
    {
        if (instance != null) Destroy(instance);
        if (palette != null) Destroy(palette);
    }

    void OnProfileChanged() { if (followProfile) Apply(CrewProfile.Look); }

    public void Apply(CrewLook look)
    {
        look.colourway = CrewProfile.Wrap(look.colourway, CrewPalette.Colourways.Length);
        look.skin = CrewProfile.Wrap(look.skin, CrewPalette.SkinTones.Length);
        look.headgear = CrewProfile.Wrap(look.headgear, CrewProfile.HeadgearCount);
        current = look;
        applied = true;

        if (body != null)
        {
            if (instance == null)
            {
                instance = new Material(body.sharedMaterial) { name = body.sharedMaterial.name + " (Crew)" };
                body.sharedMaterial = instance;
                foreach (var h in headgear) if (h != null) h.sharedMaterial = instance;
            }
            palette = CrewPalette.BaseColor(CrewPalette.Colourways[look.colourway], CrewPalette.SkinTones[look.skin], palette);
            instance.SetTexture("_BaseMap", palette);
        }
        for (int i = 0; i < headgear.Length; i++)
            if (headgear[i] != null) headgear[i].gameObject.SetActive(i == look.headgear);
    }

    public void Wave() { if (animator != null) animator.SetTrigger(WaveHash); }
    public void Cheer() { if (animator != null) animator.SetTrigger(CheerHash); }
}
