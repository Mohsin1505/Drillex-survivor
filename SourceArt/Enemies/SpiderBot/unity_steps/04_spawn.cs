using UnityEngine;
using UnityEditor;
// Play mode: unpause, spawn 12 spiders (3 per colour) around the train and route the zone spawner
// to the spider variants for this session only (no asset is changed).
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { result.Log("not playing"); return; }
        var help = Object.FindAnyObjectByType<HelpPanelManager>(FindObjectsInactive.Include);
        if (help != null) help.CloseHelp();
        Time.timeScale = 1f;
        // test only: make the train survive the swarm so the whole attack cycle can be watched
        var th = Object.FindAnyObjectByType<TrainHealth>();
        if (th != null) { th.IncreaseMaxHealth(1000000f); th.FullRepair(); if (th.gameOverPanel != null) th.gameOverPanel.SetActive(false); }
        Time.timeScale = 1f;
        string[] vars = { "Red", "Blue", "Green", "Purple" };
        var prefabs = new GameObject[4];
        for (int i = 0; i < 4; i++) prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexSpiderSetup.EnemyPath(vars[i]));
        var spawner = Object.FindAnyObjectByType<EnemySpawner>();
        foreach (var z in spawner.zones) z.enemies = prefabs;
        var drill = GameObject.FindGameObjectWithTag("Drill").transform;
        Vector3 fwd = drill.forward; fwd.y = 0; fwd.Normalize();
        for (int i = 0; i < 12; i++)
        {
            float ang = (i / 12f) * 300f - 150f;
            Vector3 dir = Quaternion.Euler(0, ang, 0) * fwd;
            Vector3 p = drill.position + dir * (30f + (i % 3) * 4f);
            if (Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out var hit, 400f, spawner.groundLayer)) p = hit.point;
            Object.Instantiate(prefabs[i % 4], p + Vector3.up * spawner.enemyHeightOffset, Quaternion.LookRotation(-dir));
        }
        result.Log("spawned 12 spiders at t {0}", Time.time);
    }
}
