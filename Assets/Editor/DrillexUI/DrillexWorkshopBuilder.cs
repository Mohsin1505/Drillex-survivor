using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static DrillexUIKit;

// Builds the workshop (upgrade) screen inside the existing "Upgrade Pannel" object, which
// UpgradeStation opens/closes. Cards are generated per UpgradeCatalog entry; train cars are
// ordered by their unlock level from the scene's UpgradeManager (Electric comes before Rocket).
public static class DrillexWorkshopBuilder
{
    public static void Build(GameObject panelGo, UpgradeStation station)
    {
        var t = (RectTransform)panelGo.transform;
        t.name = "Upgrade Pannel";
        Stretch(t);
        Clear(t);
        foreach (var old in panelGo.GetComponents<UpgradeUI>()) Object.DestroyImmediate(old);
        foreach (var old in panelGo.GetComponents<UpgradeShopUI>()) Object.DestroyImmediate(old);
        var img = t.GetComponent<Image>(); img.sprite = null; img.color = Overlay; img.raycastTarget = true;
        GetOrAdd<CanvasGroup>(panelGo);

        var window = Img("Window", t, "ui_panel", Color.white, true, true).rectTransform;
        Place(window, new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(1420, 900));
        var ui = GetOrAdd<UIPanel>(panelGo); ui.window = window;
        var shop = panelGo.AddComponent<UpgradeShopUI>();

        // ---------------- header ----------------
        var hIcon = Img("HeaderIcon", window, "ui_button_fill", Hazard, true);
        Place(hIcon.rectTransform, new Vector2(0, 1), new Vector2(36, -30), new Vector2(84, 84), new Vector2(0, 1));
        var hGlyph = Img("Glyph", hIcon.transform, "upgrade", TextDark); Stretch(hGlyph.rectTransform, 14, 14, 14, 14);
        var title = Text("Title", window, "WORKSHOP", 50, TextMain);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(140, -28), new Vector2(480, 60), new Vector2(0, 1));
        var lvl = Text("Level", window, "LEVEL 1", 20, Amber);
        Place(lvl.rectTransform, new Vector2(0, 1), new Vector2(142, -86), new Vector2(200, 26), new Vector2(0, 1));
        var next = Text("NextUnlock", window, "", 18, TextDim);
        Place(next.rectTransform, new Vector2(0, 1), new Vector2(300, -86), new Vector2(760, 26), new Vector2(0, 1));
        next.richText = true;

        var orbPlate = Img("Orbs", window, "ui_panel_dark", Color.white, true).rectTransform;
        Place(orbPlate, new Vector2(1, 1), new Vector2(-130, -30), new Vector2(330, 84), new Vector2(1, 1));
        var orbIcon = Img("OrbIcon", orbPlate, "ui_orb", Color.white); Place(orbIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(58, 58), new Vector2(0.5f, 0.5f));
        var orbLbl = Text("Label", orbPlate, "TOTAL ORBS", 15, TextDim); Place(orbLbl.rectTransform, new Vector2(0, 1), new Vector2(86, -12), new Vector2(220, 20), new Vector2(0, 1));
        var orbVal = Text("Value", orbPlate, "0", 34, Amber); Place(orbVal.rectTransform, new Vector2(0, 0), new Vector2(86, 8), new Vector2(230, 44), new Vector2(0, 0));
        orbVal.enableAutoSizing = true; orbVal.fontSizeMin = 18; orbVal.fontSizeMax = 34;
        var counter = orbPlate.gameObject.AddComponent<AnimatedCounter>(); counter.label = orbVal; counter.punchTarget = orbVal.transform;

        var close = IconButton("Close", window, "close", TextMain, 84);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(84, 84), new Vector2(1, 1));
        UnityEventTools.AddPersistentListener(close.onClick, station.CloseUpgrade);

        var stripe = HazardStrip("Hazard", window, 10);
        stripe.rectTransform.anchorMin = new Vector2(0, 1); stripe.rectTransform.anchorMax = new Vector2(1, 1); stripe.rectTransform.pivot = new Vector2(0.5f, 1);
        stripe.rectTransform.offsetMin = new Vector2(36, -136); stripe.rectTransform.offsetMax = new Vector2(-36, -126);

        // ---------------- tabs ----------------
        var blocksTab = Tab(window, "TRAIN CARS", "train", new Vector2(36, -156), out var blocksFill);
        var upgradesTab = Tab(window, "UPGRADES", "upgrade", new Vector2(36 + 330, -156), out var upgradesFill);

        // ---------------- pages ----------------
        var um = Object.FindAnyObjectByType<UpgradeManager>(FindObjectsInactive.Include);
        var unlock = new Dictionary<UpgradeId, int>
        {
            { UpgradeId.StorageBlock, um.storageUnlockLevel }, { UpgradeId.BasicGun, um.basicGunUnlockLevel }, { UpgradeId.DualGun, um.dualGunUnlockLevel },
            { UpgradeId.MachineGun, um.machineGunUnlockLevel }, { UpgradeId.FlameThrower, um.flameThrowerUnlockLevel }, { UpgradeId.ElectricGun, um.electricGunUnlockLevel },
            { UpgradeId.RocketGun, um.rocketGunUnlockLevel },
        };
        var blockIds = unlock.OrderBy(kv => kv.Value).ThenBy(kv => (int)kv.Key).Select(kv => kv.Key).ToList();
        var upgradeIds = new List<UpgradeId> { UpgradeId.BasicDamage, UpgradeId.MachineDamage, UpgradeId.RocketDamage, UpgradeId.TrainSpeed, UpgradeId.Health, UpgradeId.Repair, UpgradeId.RemoveBlock };

        var blocksPage = Page(window, "BlocksPage", out var blocksContent);
        var upgradesPage = Page(window, "UpgradesPage", out var upgradesContent);

        foreach (var id in blockIds) shop.cards.Add(Card(blocksContent, id));
        foreach (var id in upgradeIds) shop.cards.Add(Card(upgradesContent, id));

        shop.orbCounter = counter; shop.levelText = lvl; shop.nextUnlockText = next; shop.closeButton = close;
        shop.blocksTab = blocksTab; shop.upgradesTab = upgradesTab; shop.blocksPage = blocksPage; shop.upgradesPage = upgradesPage;
        shop.blocksTabFill = blocksFill; shop.upgradesTabFill = upgradesFill;
        foreach (var key in new[] { "storage", "cannon", "twin", "gatling", "flame", "bolt", "missile", "speed", "shield", "repair", "detach", "orb", "upgrade", "train" })
            shop.icons.Add(new UpgradeShopUI.IconEntry { key = key, sprite = S(key) });

        panelGo.SetActive(false);
    }

    static Button Tab(RectTransform window, string label, string icon, Vector2 pos, out Image fill)
    {
        fill = Img(label, window, "ui_button_fill", Hazard, true, true);
        Place(fill.rectTransform, new Vector2(0, 1), pos, new Vector2(310, 64), new Vector2(0, 1));
        var b = fill.gameObject.AddComponent<Button>(); b.targetGraphic = fill; b.transition = Selectable.Transition.None;
        var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
        var ic = Img("Icon", fill.transform, icon, TextDark); Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(36, 36), new Vector2(0, 0.5f));
        var tx = Text("Label", fill.transform, label, 24, TextDark); Stretch(tx.rectTransform, 68, 0, 10, 0);
        fill.gameObject.AddComponent<UIButtonFX>().hoverScale = 1.02f;
        return b;
    }

    static GameObject Page(RectTransform window, string name, out RectTransform content)
    {
        var page = Rect(name, window);
        page.anchorMin = new Vector2(0, 0); page.anchorMax = new Vector2(1, 1);
        page.offsetMin = new Vector2(36, 30); page.offsetMax = new Vector2(-36, -236);
        var viewport = Img("Viewport", page, null, new Color(0, 0, 0, 0), false, true).rectTransform; Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(438, 196); grid.spacing = new Vector2(16, 24);
        grid.padding = new RectOffset(0, 0, 18, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = page.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 30; scroll.inertia = true;
        return page.gameObject;
    }

    static UpgradeCard Card(RectTransform parent, UpgradeId id)
    {
        var e = UpgradeCatalog.Get(id);
        var root = Rect("Card_" + id, parent);
        var card = root.gameObject.AddComponent<UpgradeCard>();
        card.id = id;

        // Body is child 0 (shaken on "denied" without fighting the grid layout)
        var body = Img("Body", root, "ui_panel", Color.white, true, true).rectTransform; Stretch(body);
        var glow = Img("Glow", body, "ui_frame_glow", e.accent, true); Stretch(glow.rectTransform, -16, -16, -16, -16);
        glow.transform.SetAsFirstSibling();

        if (id == UpgradeId.ElectricGun)
        {
            var arcs = Img("ElectricDecor", body, "ui_arcs", A(Electric, 0.13f)); Stretch(arcs.rectTransform, 150, 70, 14, 40);
        }
        if (id == UpgradeId.RocketGun || id == UpgradeId.RocketDamage)
        {
            var burst = Img("BlastDecor", body, "ui_burst", A(RocketRed, 0.16f)); Place(burst.rectTransform, new Vector2(0, 0.5f), new Vector2(80, 0), new Vector2(260, 260), new Vector2(0.5f, 0.5f));
        }

        var accent = Img("Accent", body, "ui_plate", e.accent, true);
        accent.rectTransform.anchorMin = new Vector2(0, 0); accent.rectTransform.anchorMax = new Vector2(0, 1); accent.rectTransform.pivot = new Vector2(0, 0.5f);
        accent.rectTransform.offsetMin = new Vector2(8, 16); accent.rectTransform.offsetMax = new Vector2(15, -16);

        var plate = Img("IconPlate", body, "ui_plate", A(e.accent * 0.35f, 1f), true);
        Place(plate.rectTransform, new Vector2(0, 1), new Vector2(28, -22), new Vector2(104, 104), new Vector2(0, 1));
        var icon = Img("Icon", plate.transform, e.icon, e.accent); Stretch(icon.rectTransform, 16, 16, 16, 16);

        var title = Text("Title", body, e.title, 25, TextMain);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(148, -26), new Vector2(272, 34), new Vector2(0, 1));
        title.enableAutoSizing = true; title.fontSizeMin = 16; title.fontSizeMax = 25;
        var stats = Text("Stats", body, "", 15, TextDim);
        Place(stats.rectTransform, new Vector2(0, 1), new Vector2(149, -62), new Vector2(270, 44), new Vector2(0, 1));
        stats.textWrappingMode = TextWrappingModes.Normal; stats.lineSpacing = 10;

        var owned = Text("Owned", body, "EQUIPPED x1", 15, Hex("#8ee06a"));
        Place(owned.rectTransform, new Vector2(0, 0), new Vector2(28, 22), new Vector2(200, 22), new Vector2(0, 0));

        var pips = Rect("Pips", body);
        Place(pips, new Vector2(0, 0), new Vector2(28, 22), new Vector2(200, 14), new Vector2(0, 0));
        var hl = pips.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 5; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
        var pip = Img("Pip", pips, "ui_plate", A(Color.white, 0.12f), true);

        // badge (state) top-right
        var badge = Img("Badge", body, "ui_plate", e.accent, true);
        // sits on the card's top edge like a tab, so it never covers the title
        Place(badge.rectTransform, new Vector2(1, 1), new Vector2(-22, 13), new Vector2(150, 28), new Vector2(1, 1));
        var bt = Text("Label", badge.transform, "AVAILABLE", 14, TextDark, TextAlignmentOptions.Center); Stretch(bt.rectTransform);

        // cost plate bottom-right
        var cost = Img("Cost", body, "ui_panel_dark", Color.white, true).rectTransform;
        Place(cost, new Vector2(1, 0), new Vector2(-16, 16), new Vector2(200, 54), new Vector2(1, 0));
        var buy = Text("Buy", cost, "BUY", 14, TextDim); Place(buy.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(84, 24), new Vector2(0, 0.5f));
        var corb = Img("Orb", cost, "ui_orb", Color.white); Place(corb.rectTransform, new Vector2(1, 0.5f), new Vector2(-104, 0), new Vector2(34, 34), new Vector2(0.5f, 0.5f));
        var cval = Text("Value", cost, "0", 22, Amber, TextAlignmentOptions.Right); Place(cval.rectTransform, new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(80, 30), new Vector2(1, 0.5f));
        cval.enableAutoSizing = true; cval.fontSizeMin = 14; cval.fontSizeMax = 22;

        // lock overlay
        var lockO = Img("Lock", body, "ui_panel_dark", A(Color.white, 0.86f), true).rectTransform; Stretch(lockO);
        var lIcon = Img("Icon", lockO, "lock", TextDim); Place(lIcon.rectTransform, new Vector2(1, 0.5f), new Vector2(-60, 8), new Vector2(54, 54), new Vector2(0.5f, 0.5f));
        var lText = Text("Text", lockO, "REACH LEVEL 10", 15, TextDim, TextAlignmentOptions.Center); Place(lText.rectTransform, new Vector2(1, 0.5f), new Vector2(-60, -36), new Vector2(160, 22), new Vector2(0.5f, 0.5f));
        // keep icon and title readable over the lock overlay
        plate.transform.SetAsLastSibling(); title.transform.SetAsLastSibling(); badge.transform.SetAsLastSibling();

        var btn = body.gameObject.AddComponent<Button>();
        btn.targetGraphic = body.GetComponent<Image>(); btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.06f, 1.06f, 1.06f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); cb.disabledColor = new Color(0.75f, 0.75f, 0.75f); btn.colors = cb;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
        var fx = body.gameObject.AddComponent<UIButtonFX>(); fx.hoverScale = 1.015f; fx.pressedScale = 0.97f; fx.clickSound = false;

        card.button = btn; card.icon = icon; card.iconPlate = plate; card.accentBar = accent; card.frameGlow = glow;
        card.title = title; card.stats = stats; card.costText = cval; card.costGroup = cost.gameObject;
        card.badge = badge; card.badgeText = bt; card.ownedText = owned; card.pipRoot = pips; card.pipPrefab = pip;
        card.lockOverlay = lockO.gameObject; card.lockText = lText; card.buyLabel = buy;
        return card;
    }
}
