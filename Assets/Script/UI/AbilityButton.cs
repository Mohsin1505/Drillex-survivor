using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Circular power button. Finds whatever IHudAbility is equipped on the train and takes its
// icon/colour from the matching visual entry, so a different equipped power re-skins it.
public class AbilityButton : MonoBehaviour
{
    [System.Serializable]
    public class AbilityVisual
    {
        public string abilityId;
        public Sprite icon;
        public Color color = Color.white;
    }

    public Button button;
    public Image icon;
    public Image cooldownFill;     // radial, fills as it recharges
    public Image energyRing;       // tinted with the ability colour
    public Image readyGlow;
    public Image activeRing;       // spins while the ability's effect is alive
    public TMP_Text cooldownText;
    public TMP_Text nameText;
    public List<AbilityVisual> visuals = new List<AbilityVisual>();

    IHudAbility ability;
    AbilityVisual visual;
    int lastSeconds = int.MinValue;
    bool wasReady;
    float glowT;

    void Start()
    {
        Rebind();
        button.onClick.AddListener(OnPressed);
    }

    // Call again if the equipped power changes.
    public void Rebind()
    {
        ability = null;
        foreach (var mb in FindObjectsByType<MonoBehaviour>())
            if (mb is IHudAbility a && mb.isActiveAndEnabled) { ability = a; break; }

        gameObject.SetActive(ability != null);
        if (ability == null) return;

        visual = visuals.Find(v => v.abilityId == ability.AbilityId) ?? (visuals.Count > 0 ? visuals[0] : null);
        Color c = visual != null ? visual.color : Color.white;
        if (visual != null && visual.icon != null) icon.sprite = visual.icon;
        if (energyRing) energyRing.color = c;
        if (cooldownFill) cooldownFill.color = new Color(c.r, c.g, c.b, 0.9f);
        if (readyGlow) readyGlow.color = c;
        if (activeRing) activeRing.color = c;
        if (nameText) nameText.text = ability.DisplayName.ToUpperInvariant();
        wasReady = ability.CanActivate;
    }

    void OnPressed()
    {
        if (ability == null) return;
        if (!ability.CanActivate)
        {
            UITween.Shake((RectTransform)icon.transform, 6f, 0.25f);
            AudioManager.Ui(l => l.denied, 0.6f);
            return;
        }
        ability.Activate();
        UITween.Punch(icon.transform, 0.25f, 0.3f);
    }

    void Update()
    {
        if (ability == null) return;
        float p = ability.CooldownProgress;
        bool ready = ability.CanActivate;
        bool active = ability.IsActive;
        Color c = visual != null ? visual.color : Color.white;

        if (cooldownFill) cooldownFill.fillAmount = p;
        // dim the icon while recharging so the countdown reads clearly on top of it
        icon.color = ready || active ? c : new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0.28f);

        int secs = p < 1f ? Mathf.CeilToInt(ability.CooldownRemaining) : (active ? -2 : -1);
        if (secs != lastSeconds)
        {
            lastSeconds = secs;
            if (cooldownText)
            {
                cooldownText.gameObject.SetActive(secs != -1);
                cooldownText.text = secs == -2 ? "ACTIVE" : secs.ToString();
            }
        }

        if (activeRing)
        {
            activeRing.enabled = active;
            if (active) activeRing.rectTransform.Rotate(0f, 0f, -120f * Time.unscaledDeltaTime);
        }

        if (readyGlow)
        {
            glowT += Time.unscaledDeltaTime * 2.4f;
            float a = ready ? 0.35f + 0.25f * Mathf.Sin(glowT) : 0f;
            readyGlow.canvasRenderer.SetAlpha(a);
        }

        if (ready && !wasReady)
        {
            UITween.Punch(transform, 0.1f, 0.3f);
            AudioManager.Ui(l => l.uiOpen, 0.4f);
        }
        wasReady = ready;
    }
}
