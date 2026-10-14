using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One card in the workshop. Shows the live state of an UpgradeCatalog entry; the shop refreshes
// cards when the panel opens and after every purchase, never per frame.
public class UpgradeCard : MonoBehaviour
{
    public UpgradeId id;

    [Header("Parts")]
    public Button button;
    public Image icon;
    public Image iconPlate;
    public Image accentBar;
    public Image frameGlow;
    public TMP_Text title;
    public TMP_Text stats;
    public TMP_Text costText;
    public GameObject costGroup;
    public Image badge;
    public TMP_Text badgeText;
    public TMP_Text ownedText;
    public Transform pipRoot;
    public Image pipPrefab;
    public GameObject lockOverlay;
    public TMP_Text lockText;
    public TMP_Text buyLabel;

    [HideInInspector] public UpgradeEntry entry;
    Image[] pips;
    UpgradeState lastState = (UpgradeState)(-1);

    public void Bind(UpgradeEntry e, Sprite iconSprite)
    {
        entry = e;
        title.text = e.title;
        if (icon != null && iconSprite != null) icon.sprite = iconSprite;
        if (icon != null) icon.color = e.accent;
        if (accentBar != null) accentBar.color = e.accent;
        if (frameGlow != null) { var c = e.accent; c.a = 1f; frameGlow.color = c; }
        if (iconPlate != null) { var c = e.accent * 0.35f; c.a = 1f; iconPlate.color = c; }

        if (pipRoot != null && pipPrefab != null && e.kind == UpgradeKind.Upgrade)
        {
            int max = Mathf.Clamp(e.maxLevel(), 1, 10);
            pips = new Image[max];
            pipPrefab.gameObject.SetActive(false);
            for (int i = 0; i < max; i++)
            {
                pips[i] = Instantiate(pipPrefab, pipRoot);
                pips[i].gameObject.SetActive(true);
            }
        }
        else if (pipRoot != null) pipRoot.gameObject.SetActive(false);
    }

    public UpgradeState Refresh()
    {
        if (entry == null) return UpgradeState.Unavailable;
        var state = UpgradeCatalog.StateOf(entry);
        int cost = entry.cost();
        stats.text = entry.stats != null ? entry.stats() : "";

        costGroup.SetActive(cost > 0 && state != UpgradeState.Maxed && state != UpgradeState.Locked);
        costText.text = cost.ToString("N0");
        costText.color = state == UpgradeState.NeedOrbs ? new Color(1f, 0.38f, 0.3f) : new Color(1f, 0.86f, 0.45f);

        lockOverlay.SetActive(state == UpgradeState.Locked);
        if (lockText != null) lockText.text = "REACH LEVEL " + entry.unlockLevel();

        string badgeLabel;
        Color badgeColor;
        switch (state)
        {
            case UpgradeState.Locked: badgeLabel = "LOCKED"; badgeColor = new Color(0.3f, 0.31f, 0.33f); break;
            case UpgradeState.Maxed: badgeLabel = "MAXED"; badgeColor = new Color(0.36f, 0.75f, 0.4f); break;
            case UpgradeState.NeedOrbs: badgeLabel = "NEED ORBS"; badgeColor = new Color(0.55f, 0.2f, 0.16f); break;
            case UpgradeState.Unavailable: badgeLabel = entry.unavailableReason != null ? entry.unavailableReason() ?? "" : ""; badgeColor = new Color(0.3f, 0.31f, 0.33f); break;
            default:
                badgeLabel = entry.kind == UpgradeKind.Upgrade && entry.level() > 0 ? "UPGRADE" : "AVAILABLE";
                badgeColor = entry.accent;
                break;
        }
        badgeText.text = badgeLabel;
        badge.color = badgeColor;
        badgeText.color = state == UpgradeState.Available ? new Color(0.06f, 0.06f, 0.07f) : new Color(0.92f, 0.93f, 0.95f);
        badge.gameObject.SetActive(!string.IsNullOrEmpty(badgeLabel));

        if (ownedText != null)
        {
            int owned = entry.kind == UpgradeKind.Block ? UpgradeCatalog.OwnedCount(entry) : 0;
            ownedText.gameObject.SetActive(owned > 0);
            ownedText.text = "EQUIPPED x" + owned;
        }

        if (pips != null)
        {
            int lvl = entry.level();
            for (int i = 0; i < pips.Length; i++)
                pips[i].color = i < lvl ? entry.accent : new Color(1f, 1f, 1f, 0.12f);
        }

        if (buyLabel != null)
        {
            buyLabel.text = entry.kind == UpgradeKind.Block ? "BUY" : entry.kind == UpgradeKind.Upgrade ? "UPGRADE" : (cost > 0 ? "REPAIR" : "DETACH");
        }

        // still clickable when short of orbs so the player gets "need X more" feedback
        button.interactable = state == UpgradeState.Available || state == UpgradeState.NeedOrbs;
        if (frameGlow != null) frameGlow.canvasRenderer.SetAlpha(state == UpgradeState.Available ? 0.55f : 0f);

        if (lastState == UpgradeState.Locked && state != UpgradeState.Locked) PlayUnlocked();
        lastState = state;
        return state;
    }

    public void PlayPurchased()
    {
        UITween.Punch(transform, 0.06f, 0.3f);
        if (frameGlow != null)
        {
            var g = frameGlow;
            UITween.Run(g, 0.6f, k => { if (g) g.canvasRenderer.SetAlpha(Mathf.Lerp(1f, 0.55f, k)); });
        }
    }

    public void PlayDenied()
    {
        var rt = (RectTransform)transform.GetChild(0);
        UITween.Shake(rt, 10f, 0.3f);
    }

    void PlayUnlocked()
    {
        if (!isActiveAndEnabled) return;
        UITween.Punch(transform, 0.08f, 0.4f);
    }
}
