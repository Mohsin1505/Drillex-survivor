using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Main-menu crew screen: pick a callsign, suit colourway, skin tone and headgear for the Driller
// standing next to the train. Edits preview live on the stage character; CONFIRM saves to
// CrewProfile, the X button reverts. The menu camera eases in on the character while open.
[RequireComponent(typeof(UIPanel))]
public class CrewSelectPanel : MonoBehaviour
{
    [Header("Stage")]
    public CrewAppearance character;
    public MenuCameraRig cameraRig;
    public Transform crewFocus;
    [Tooltip("Main menu buttons/title, faded out while the crew screen is open.")]
    public CanvasGroup menuGroup;

    [Header("UI")]
    public TMP_InputField nameField;
    public TextMeshProUGUI suitValue, skinValue, headgearValue;
    public TextMeshProUGUI suitIndex, skinIndex, headgearIndex;
    public Image suitSwatch, suitSwatchAccent, skinSwatch;
    public TextMeshProUGUI menuNameLabel;

    [Header("Crew camera")]
    public float crewRadius = 4.4f;
    public float crewHeight = 0.55f;
    public float crewAngle = 210f;
    public float crewFramingYaw = 13f;
    public float blendTime = 0.7f;

    UIPanel panel;
    CrewLook working;
    // camera blend: 0 = menu framing, 1 = crew framing
    float blend, blendTarget;
    Transform menuFocus;
    float menuRadius, menuHeight, menuAngle, menuSwing, menuYaw;
    Vector3 menuLook;
    bool captured;

    void Awake()
    {
        panel = GetComponent<UIPanel>();
        if (nameField != null)
        {
            nameField.characterLimit = CrewProfile.MaxNameLength;
            nameField.onValidateInput += (text, index, c) => char.IsLetterOrDigit(c) || c == ' ' || c == '-' ? char.ToUpperInvariant(c) : '\0';
        }
    }

    void Start()
    {
        RefreshMenuLabel();
        CrewProfile.Changed += RefreshMenuLabel;
        if (character != null) Invoke(nameof(GreetWave), 1.4f);
    }

    void OnDestroy() => CrewProfile.Changed -= RefreshMenuLabel;

    void GreetWave() { if (character != null && !panel.IsOpen) character.Wave(); }

    void RefreshMenuLabel()
    {
        if (menuNameLabel != null) menuNameLabel.text = CrewProfile.Name;
    }

    // ---------------------------------------------------------------- open / close
    public void Open()
    {
        working = CrewProfile.Look;
        if (nameField != null) nameField.SetTextWithoutNotify(CrewProfile.Name);
        if (character != null) { character.followProfile = false; character.Apply(working); character.Wave(); }
        RefreshRows();
        panel.Show();
        SetMenuVisible(false);
        CaptureCamera();
        blendTarget = 1f;
    }

    public void Confirm()
    {
        CrewProfile.Name = nameField != null ? nameField.text : CrewProfile.Name;
        CrewProfile.Look = working;
        CrewProfile.Save();
        if (character != null) { character.followProfile = true; character.Apply(working); character.Cheer(); }
        Close();
    }

    public void Cancel()
    {
        if (character != null) { character.followProfile = true; character.Apply(CrewProfile.Look); }
        Close();
    }

    void Close()
    {
        panel.Hide();
        SetMenuVisible(true);
        blendTarget = 0f;
    }

    void SetMenuVisible(bool on)
    {
        if (menuGroup == null) return;
        menuGroup.interactable = on;
        menuGroup.blocksRaycasts = on;
        UITween.Fade(menuGroup, on ? 1f : 0f, 0.25f);
    }

    void Update()
    {
        if (panel.IsOpen && Input.GetKeyDown(KeyCode.Escape)) Cancel();
        UpdateCamera();
    }

    // ---------------------------------------------------------------- selectors
    public void NextSuit() => Step(ref working.colourway, 1, CrewPalette.Colourways.Length);
    public void PrevSuit() => Step(ref working.colourway, -1, CrewPalette.Colourways.Length);
    public void NextSkin() => Step(ref working.skin, 1, CrewPalette.SkinTones.Length);
    public void PrevSkin() => Step(ref working.skin, -1, CrewPalette.SkinTones.Length);
    public void NextHeadgear() => Step(ref working.headgear, 1, CrewProfile.HeadgearCount);
    public void PrevHeadgear() => Step(ref working.headgear, -1, CrewProfile.HeadgearCount);

    public void Randomize()
    {
        working = CrewProfile.RandomLook();
        if (nameField != null) nameField.text = CrewProfile.RandomName();
        Changed();
        if (character != null) character.Cheer();
    }

    void Step(ref int value, int dir, int count)
    {
        value = CrewProfile.Wrap(value + dir, count);
        Changed();
    }

    void Changed()
    {
        if (character != null) character.Apply(working);
        RefreshRows();
    }

    void RefreshRows()
    {
        var w = CrewPalette.Colourways[CrewProfile.Wrap(working.colourway, CrewPalette.Colourways.Length)];
        var s = CrewPalette.SkinTones[CrewProfile.Wrap(working.skin, CrewPalette.SkinTones.Length)];
        Set(suitValue, w.name); Set(skinValue, s.name); Set(headgearValue, CrewProfile.HeadgearNames[CrewProfile.Wrap(working.headgear, CrewProfile.HeadgearCount)]);
        Set(suitIndex, $"{working.colourway + 1}/{CrewPalette.Colourways.Length}");
        Set(skinIndex, $"{working.skin + 1}/{CrewPalette.SkinTones.Length}");
        Set(headgearIndex, $"{working.headgear + 1}/{CrewProfile.HeadgearCount}");
        if (suitSwatch != null) suitSwatch.color = w.suit;
        if (suitSwatchAccent != null) suitSwatchAccent.color = w.accent;
        if (skinSwatch != null) skinSwatch.color = s.skin;
    }

    static void Set(TextMeshProUGUI t, string v) { if (t != null) t.text = v; }

    // ---------------------------------------------------------------- camera
    void CaptureCamera()
    {
        if (captured || cameraRig == null) return;
        menuFocus = cameraRig.focus; menuRadius = cameraRig.radius; menuHeight = cameraRig.height;
        menuAngle = cameraRig.startAngle; menuSwing = cameraRig.swing; menuYaw = cameraRig.framingYaw; menuLook = cameraRig.lookOffset;
        captured = true;
    }

    void UpdateCamera()
    {
        if (!captured || cameraRig == null || crewFocus == null || menuFocus == null) return;
        if (Mathf.Approximately(blend, blendTarget)) return;
        blend = Mathf.MoveTowards(blend, blendTarget, Time.unscaledDeltaTime / Mathf.Max(0.01f, blendTime));
        float k = blend * blend * (3f - 2f * blend);
        // the rig orbits rig.focus; blend by moving a proxy focus between the two targets
        if (proxy == null) { proxy = new GameObject("CrewCameraProxy").transform; proxy.SetParent(transform, false); }
        proxy.position = Vector3.Lerp(menuFocus.position, crewFocus.position, k);
        cameraRig.focus = blend <= 0f ? menuFocus : proxy;
        cameraRig.radius = Mathf.Lerp(menuRadius, crewRadius, k);
        cameraRig.height = Mathf.Lerp(menuHeight, crewHeight, k);
        cameraRig.startAngle = Mathf.LerpAngle(menuAngle, crewAngle, k);
        cameraRig.swing = Mathf.Lerp(menuSwing, 5f, k);
        cameraRig.framingYaw = Mathf.Lerp(menuYaw, crewFramingYaw, k);
        cameraRig.lookOffset = Vector3.Lerp(menuLook, Vector3.zero, k);
    }

    Transform proxy;
}
