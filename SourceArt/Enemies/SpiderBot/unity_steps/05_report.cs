using System.Linq;
using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { result.Log("not playing"); return; }
        string name = System.IO.File.ReadAllText(@"D:\Drillex\SourceArt\Enemies\SpiderBot\_preview\shot_name.txt").Trim();
        ScreenCapture.CaptureScreenshot(@"D:\Drillex\SourceArt\Enemies\SpiderBot\_preview\" + name + ".png", 1);
        var vis = Object.FindObjectsByType<SpiderBotVisual>(FindObjectsSortMode.None);
        var drill = GameObject.FindGameObjectWithTag("Drill").transform;
        int walking = 0, attacking = 0, dying = 0;
        foreach (var v in vis)
        {
            var st = v.animator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName("Attack")) attacking++; else if (st.IsName("Death")) dying++; else walking++;
        }
        var footGap = vis.Where(v => v.transform.parent != null).Select(v => {
            var rb = v.GetComponentInParent<Rigidbody>();
            return Physics.Raycast(v.transform.position + Vector3.up * 2f, Vector3.down, out var h, 10f, Object.FindAnyObjectByType<EnemySpawner>().groundLayer) ? v.transform.position.y - h.point.y : 99f;
        }).ToArray();
        result.Log(string.Format("t {0:0.0} spiders {1} loco {2} attack {3} death {4} fps {5:0} feet-to-ground avg {6:0.00} min {7:0.00} max {8:0.00} train hp? bolts active {9}",
            Time.time, vis.Length, walking, attacking, dying, 1f / Time.smoothDeltaTime,
            footGap.Length > 0 ? footGap.Average() : 0, footGap.Length > 0 ? footGap.Min() : 0, footGap.Length > 0 ? footGap.Max() : 0,
            Object.FindObjectsByType<SpiderBolt>(FindObjectsSortMode.None).Length) + " timescale " + Time.timeScale);
        var near = vis.OrderBy(v => Vector3.Distance(v.transform.position, drill.position)).FirstOrDefault();
        if (near != null) result.Log(string.Format("nearest {0:0.0} m speed param {1:0.00} animator speed {2:0.00}", Vector3.Distance(near.transform.position, drill.position), near.animator.GetFloat("Speed"), near.animator.speed));
    }
}
