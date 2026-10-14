using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var p = Object.FindAnyObjectByType<CrewSelectPanel>(FindObjectsInactive.Include);
        string act = System.IO.File.ReadAllText(@"D:\Drillex\SourceArt\Characters\Driller\_preview\act.txt").Trim();
        if (act == "open") { p.Open(); p.NextSuit(); p.NextSuit(); p.NextHeadgear(); p.NextSkin(); p.NextSkin(); }
        if (act == "confirm") { p.nameField.text = "SPARKS"; p.Confirm(); }
        var a = p.character;
        var anim = a.animator; var st = anim.GetCurrentAnimatorStateInfo(0);
        result.Log("{0}: open {1} look {2}/{3}/{4} name {5} state {6} avatar {7}", act, p.GetComponent<UIPanel>().IsOpen, a.Current.colourway, a.Current.skin, a.Current.headgear, CrewProfile.Name, st.shortNameHash == Animator.StringToHash("Idle") ? "Idle" : st.shortNameHash == Animator.StringToHash("Wave") ? "Wave" : st.shortNameHash == Animator.StringToHash("Cheer") ? "Cheer" : "?", anim.avatar);
    }
}
