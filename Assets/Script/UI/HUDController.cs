using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Drives the gameplay HUD from the existing gameplay managers. It only reads their values;
// gameplay logic is untouched. Text/fill updates happen only when a value changes.
public class HUDController : MonoBehaviour
{
    [Header("Total orbs")]
    public AnimatedCounter totalOrbs;
    public RectTransform orbIcon;

    [Header("Level / XP")]
    public TMP_Text levelText;
    public TMP_Text xpText;
    public Image xpFill;
    public Image xpGainFill;   // brighter fill that leads the main fill when XP is gained

    [Header("Orbs in train")]
    public TMP_Text trainOrbsText;
    public Image trainFill;
    public Image trainPanelAccent;
    public TMP_Text trainFullTag;

    [Header("Hull")]
    public TMP_Text hullText;
    public Image hullFill;

    [Header("Noise (worm threat)")]
    public Image noiseFill;
    public TMP_Text noiseLabel;

    [Header("Upgrade button")]
    public UIPulse upgradePulse;
    public GameObject upgradeBadge;

    [Header("Icons for toasts")]
    public Sprite levelIcon;
    public Sprite electricIcon;
    public Sprite rocketIcon;
    public Sprite blockIcon;
    public Sprite warningIcon;

    TrainStorageManager storage;
    TrainHealth health;

    long lastOrbs = long.MinValue;
    int lastLevel = -1, lastXp = -1, lastXpNeeded = -1;
    int lastTrainOrbs = -1, lastTrainCap = -1;
    int lastHull = -1, lastHullMax = -1;
    float xpShown, xpTarget;
    float trainWarnT;
    float affordCheck;
    bool wormWarned;

    static readonly Color AmberText = new Color(1f, 0.82f, 0.35f);
    static readonly Color Danger = new Color(1f, 0.3f, 0.2f);

    void Start()
    {
        storage = FindAnyObjectByType<TrainStorageManager>();
        health = FindAnyObjectByType<TrainHealth>();
        if (trainFullTag) trainFullTag.gameObject.SetActive(false);
        Poll(true);
    }

    void Update()
    {
        Poll(false);
        AnimateBars();
        if (orbIcon) orbIcon.Rotate(0f, 0f, -25f * Time.unscaledDeltaTime);
    }

    void Poll(bool first)
    {
        // ---- total orbs ----
        if (Manager.instance != null && Manager.instance.permanentOrb != lastOrbs)
        {
            long v = Manager.instance.permanentOrb;
            if (!first && v > lastOrbs) AudioManager.Ui(l => l.orbDeposit, 0.7f);
            totalOrbs.SetValue(v, !first);
            lastOrbs = v;
            affordCheck = 0f;
        }

        // ---- level / xp ----
        var xp = ExperienceManager.instance;
        if (xp != null && (xp.currentLevel != lastLevel || xp.currentXP != lastXp || xp.xpNeeded != lastXpNeeded))
        {
            bool levelled = !first && lastLevel > 0 && xp.currentLevel > lastLevel;
            int prevLevel = lastLevel;
            levelText.SetText("LV " + xp.currentLevel);
            xpText.SetText(xp.currentXP + " / " + xp.xpNeeded + " XP");
            xpTarget = xp.xpNeeded > 0 ? Mathf.Clamp01((float)xp.currentXP / xp.xpNeeded) : 0f;
            if (first) { xpShown = xpTarget; xpFill.fillAmount = xpShown; if (xpGainFill) xpGainFill.fillAmount = xpShown; }
            if (levelled)
            {
                xpShown = 0f; // the bar wrapped: refill from empty
                UITween.Punch(levelText.transform, 0.3f, 0.4f);
                AudioManager.Ui(l => l.levelUp);
                HUDToast.Show("LEVEL " + xp.currentLevel, "NEW UPGRADES MAY BE AVAILABLE", UpgradeCatalog.Hazard, levelIcon);
                AnnounceUnlocks(prevLevel, xp.currentLevel);
            }
            else if (!first && xp.currentXP > lastXp && xpGainFill)
            {
                xpGainFill.fillAmount = xpTarget;
            }
            lastLevel = xp.currentLevel; lastXp = xp.currentXP; lastXpNeeded = xp.xpNeeded;
            affordCheck = 0f;
        }

        // ---- orbs in train ----
        if (storage != null && (storage.currentOrb != lastTrainOrbs || storage.totalStorage != lastTrainCap))
        {
            if (!first && storage.currentOrb > lastTrainOrbs) AudioManager.Ui(l => l.orbPickup, 0.45f);
            lastTrainOrbs = storage.currentOrb; lastTrainCap = storage.totalStorage;
            trainOrbsText.SetText(lastTrainOrbs + " / " + lastTrainCap);
            float f = lastTrainCap > 0 ? (float)lastTrainOrbs / lastTrainCap : 0f;
            trainFill.fillAmount = f;
            bool full = lastTrainCap > 0 && lastTrainOrbs >= lastTrainCap;
            if (trainFullTag) trainFullTag.gameObject.SetActive(full);
            trainFill.color = full ? Danger : f >= 0.75f ? UpgradeCatalog.Ember : UpgradeCatalog.Hazard;
            if (full && !first) HUDToast.Show("TRAIN STORAGE FULL", "RETURN TO THE SAFE ZONE TO BANK ORBS", Danger, warningIcon);
        }

        // ---- hull ----
        if (health != null)
        {
            int hp = Mathf.CeilToInt(Mathf.Max(0f, health.CurrentHealth));
            int max = Mathf.CeilToInt(health.maxHealth);
            if (hp != lastHull || max != lastHullMax)
            {
                if (!first && hp < lastHull && hullFill) UITween.Punch(hullFill.transform.parent, 0.05f, 0.2f);
                lastHull = hp; lastHullMax = max;
                if (hullText) hullText.SetText(hp + " / " + max);
                float f = max > 0 ? (float)hp / max : 0f;
                if (hullFill)
                {
                    hullFill.fillAmount = f;
                    hullFill.color = f < 0.3f ? Danger : f < 0.6f ? UpgradeCatalog.Ember : new Color(0.55f, 0.85f, 0.4f);
                }
            }
        }

        // ---- noise / worm threat (fills as noise builds up; full = worm hunts) ----
        var m = Manager.instance;
        if (m != null && noiseFill != null && m.maxNoise > 0f)
        {
            float threat = 1f - Mathf.Clamp01(m.currentNoise / m.maxNoise);
            noiseFill.fillAmount = threat;
            noiseFill.color = threat > 0.75f ? Danger : threat > 0.45f ? UpgradeCatalog.Ember : UpgradeCatalog.Hazard;
            bool hunting = m.currentWorm != null && m.wormWarningPanel != null && m.wormWarningPanel.activeSelf;
            if (noiseLabel) noiseLabel.color = threat > 0.75f ? Danger : new Color(0.75f, 0.77f, 0.8f);
            if (hunting && !wormWarned) { AudioManager.Ui(l => l.warning, 0.9f); }
            wormWarned = hunting;
        }

        // ---- upgrade availability (cheap, but no need every frame) ----
        affordCheck -= Time.unscaledDeltaTime;
        if (affordCheck <= 0f)
        {
            affordCheck = 0.5f;
            bool any = UpgradeCatalog.AnythingAffordable();
            if (upgradePulse) upgradePulse.pulsing = any;
            if (upgradeBadge) upgradeBadge.SetActive(any);
        }
    }

    void AnimateBars()
    {
        if (xpFill == null) return;
        if (!Mathf.Approximately(xpShown, xpTarget))
        {
            xpShown = Mathf.MoveTowards(xpShown, xpTarget, Time.unscaledDeltaTime * 1.2f);
            xpFill.fillAmount = xpShown;
        }
        if (xpGainFill && xpGainFill.fillAmount < xpShown) xpGainFill.fillAmount = xpShown;
        if (xpGainFill && xpGainFill.fillAmount > xpTarget) xpGainFill.fillAmount = xpTarget;

        if (lastTrainCap > 0 && lastTrainOrbs >= lastTrainCap && trainPanelAccent)
        {
            trainWarnT += Time.unscaledDeltaTime * 5f;
            trainPanelAccent.canvasRenderer.SetAlpha(0.5f + 0.5f * Mathf.Sin(trainWarnT));
        }
        else if (trainPanelAccent) trainPanelAccent.canvasRenderer.SetAlpha(1f);
    }

    // Unlock banners for train cars that became available at this level (electric = blue arcs, rocket = orange burst).
    void AnnounceUnlocks(int fromLevel, int toLevel)
    {
        if (UpgradeManager.instance == null) return;
        foreach (var e in UpgradeCatalog.Entries)
        {
            if (e.kind != UpgradeKind.Block) continue;
            int u = e.unlockLevel();
            if (u <= fromLevel || u > toLevel) continue;
            var style = e.id == UpgradeId.ElectricGun ? HUDToast.Style.Electric : e.id == UpgradeId.RocketGun ? HUDToast.Style.Explosive : HUDToast.Style.Normal;
            var icon = e.id == UpgradeId.ElectricGun ? electricIcon : e.id == UpgradeId.RocketGun ? rocketIcon : blockIcon;
            HUDToast.Show("UNLOCKED: " + e.title, "AVAILABLE IN THE WORKSHOP", e.accent, icon, style);
            AudioManager.Ui(l => l.unlock);
        }
    }
}
