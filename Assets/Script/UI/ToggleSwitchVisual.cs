using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Industrial on/off switch look for a uGUI Toggle: slides the knob and swaps track colour/label.
[RequireComponent(typeof(Toggle))]
public class ToggleSwitchVisual : MonoBehaviour
{
    public RectTransform knob;
    public Image track;
    public TMP_Text stateLabel;
    public float travel = 34f;
    public Color onColor = new Color(1f, 0.72f, 0.1f);
    public Color offColor = new Color(0.22f, 0.23f, 0.25f);

    Toggle toggle;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(v => Apply(v, true));
    }

    void OnEnable() { if (toggle == null) Awake(); Apply(toggle.isOn, false); }

    void Apply(bool on, bool animate)
    {
        Vector2 to = new Vector2(on ? travel : -travel, 0f);
        if (knob != null)
        {
            if (animate) { Vector2 from = knob.anchoredPosition; var k = knob; UITween.Run(knob, 0.15f, x => { if (k) k.anchoredPosition = Vector2.Lerp(from, to, x); }); }
            else knob.anchoredPosition = to;
        }
        if (track != null) track.color = on ? onColor : offColor;
        if (stateLabel != null)
        {
            stateLabel.text = on ? "ON" : "OFF";
            stateLabel.rectTransform.anchoredPosition = new Vector2(on ? -travel * 0.8f : travel * 0.8f, 0f);
            stateLabel.color = on ? new Color(0.08f, 0.07f, 0.05f) : new Color(0.65f, 0.66f, 0.68f);
        }
    }
}
