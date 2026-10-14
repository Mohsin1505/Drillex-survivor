using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Workshop screen (opened at the safe zone). Builds nothing at runtime except binding the
// pre-built cards; purchases go through the existing UpgradeManager methods.
public class UpgradeShopUI : MonoBehaviour
{
    [System.Serializable]
    public struct IconEntry { public string key; public Sprite sprite; }

    public List<UpgradeCard> cards = new List<UpgradeCard>();
    public List<IconEntry> icons = new List<IconEntry>();
    public AnimatedCounter orbCounter;
    public TMP_Text levelText;
    public TMP_Text nextUnlockText;
    public Button closeButton;

    [Header("Tabs")]
    public Button blocksTab;
    public Button upgradesTab;
    public GameObject blocksPage;
    public GameObject upgradesPage;
    public Image blocksTabFill;
    public Image upgradesTabFill;

    bool bound;

    void Awake() { Bind(); }

    void Bind()
    {
        if (bound) return;
        bound = true;
        foreach (var card in cards)
        {
            var e = UpgradeCatalog.Get(card.id);
            if (e == null) continue;
            card.Bind(e, IconFor(e.icon));
            var c = card;
            card.button.onClick.AddListener(() => Buy(c));
        }
        if (blocksTab) blocksTab.onClick.AddListener(() => ShowPage(true));
        if (upgradesTab) upgradesTab.onClick.AddListener(() => ShowPage(false));
        ShowPage(true);
    }

    public Sprite IconFor(string key)
    {
        foreach (var i in icons) if (i.key == key) return i.sprite;
        return null;
    }

    void OnEnable()
    {
        Bind();
        if (orbCounter) orbCounter.SetValue(UpgradeCatalog.Orbs, false);
        RefreshAll();
    }

    void ShowPage(bool blocks)
    {
        if (blocksPage) blocksPage.SetActive(blocks);
        if (upgradesPage) upgradesPage.SetActive(!blocks);
        SetTab(blocksTabFill, blocks);
        SetTab(upgradesTabFill, !blocks);
    }

    static void SetTab(Image fill, bool on)
    {
        if (fill == null) return;
        fill.color = on ? UpgradeCatalog.Hazard : new Color(0.16f, 0.17f, 0.19f);
        var label = fill.GetComponentInChildren<TMP_Text>();
        if (label) label.color = on ? new Color(0.07f, 0.06f, 0.05f) : new Color(0.75f, 0.77f, 0.8f);
    }

    public void RefreshAll()
    {
        foreach (var c in cards) c.Refresh();
        if (orbCounter) orbCounter.SetValue(UpgradeCatalog.Orbs);
        int lvl = ExperienceManager.instance != null ? ExperienceManager.instance.currentLevel : 1;
        if (levelText) levelText.text = "LEVEL " + lvl;
        if (nextUnlockText) nextUnlockText.text = NextUnlockLine(lvl);
    }

    // "NEXT: TESLA ARC CAR AT LEVEL 11" - tells the player what to aim for.
    string NextUnlockLine(int lvl)
    {
        UpgradeEntry best = null;
        foreach (var e in UpgradeCatalog.Entries)
        {
            if (e.kind != UpgradeKind.Block) continue;
            int u = e.unlockLevel();
            if (u > lvl && (best == null || u < best.unlockLevel())) best = e;
        }
        return best == null ? "ALL TRAIN CARS UNLOCKED" : "NEXT UNLOCK  <color=#" + ColorUtility.ToHtmlStringRGB(best.accent) + ">" + best.title + "</color>  AT LEVEL " + best.unlockLevel();
    }

    void Buy(UpgradeCard card)
    {
        var e = card.entry;
        var state = UpgradeCatalog.StateOf(e);
        if (state == UpgradeState.NeedOrbs)
        {
            card.PlayDenied();
            AudioManager.Ui(l => l.denied, 0.9f);
            HUDToast.Show("NOT ENOUGH ORBS", "NEED " + (e.cost() - UpgradeCatalog.Orbs).ToString("N0") + " MORE", new Color(1f, 0.38f, 0.3f), IconFor("orb"));
            return;
        }
        if (state != UpgradeState.Available) { card.PlayDenied(); AudioManager.Ui(l => l.denied, 0.9f); return; }

        long orbsBefore = UpgradeCatalog.Orbs;
        int ownedBefore = UpgradeCatalog.OwnedCount(e);
        int levelBefore = e.kind == UpgradeKind.Upgrade ? e.level() : 0;
        var train = FindAnyObjectByType<TrainMovement>();
        int carsBefore = train != null ? train.BlockCount : 0;
        e.buy();

        bool success = UpgradeCatalog.Orbs < orbsBefore
            || UpgradeCatalog.OwnedCount(e) != ownedBefore
            || (e.kind == UpgradeKind.Upgrade && e.level() != levelBefore)
            || (train != null && train.BlockCount != carsBefore);

        if (success)
        {
            card.PlayPurchased();
            AudioManager.Ui(l => l.purchase);
            string sub = e.kind == UpgradeKind.Block ? "COUPLED TO YOUR TRAIN"
                : e.kind == UpgradeKind.Upgrade ? "LEVEL " + e.level() + " / " + e.maxLevel()
                : e.id == UpgradeId.Repair ? "HULL RESTORED" : "REAR CAR DETACHED";
            HUDToast.Show(e.title, sub, e.accent, IconFor(e.icon));
        }
        else
        {
            card.PlayDenied();
            AudioManager.Ui(l => l.denied, 0.9f);
        }
        RefreshAll();
    }
}
