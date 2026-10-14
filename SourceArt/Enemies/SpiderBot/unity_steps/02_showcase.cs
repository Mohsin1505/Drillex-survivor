using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Edit-mode crowd check: 12 spiders (3 of each colour) in front of the train in Lev1, posed by the
// Animator, rendered from the gameplay camera's angle. Uses a temporary additive scene that is
// closed without saving, so Lev1 is never modified.
internal class CommandScript : IRunCommand
{
    const string Out = @"D:\Drillex\SourceArt\Enemies\SpiderBot\_preview\";

    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.Log("SKIP: playing"); return; }
        var lev = SceneManager.GetActiveScene();
        if (lev.name != "Lev1")
        {
            if (lev.isDirty) { result.Log("SKIP: {0} has unsaved changes", lev.name); return; }
            lev = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        }

        var cm = Object.FindAnyObjectByType<CameraMovement>(FindObjectsInactive.Include);
        var train = cm != null && cm.target != null ? cm.target : GameObject.FindGameObjectWithTag("Drill").transform;
        var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            Vector3 fwd = train.forward; fwd.y = 0; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 c = train.position + fwd * 14f;
            float ground = GroundY(c, train.position.y - 1f);
            var spiders = new System.Collections.Generic.List<GameObject>();
            string[] vars = { "Red", "Blue", "Green", "Purple" };
            for (int i = 0; i < 12; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexSpiderSetup.EnemyPath(vars[i % 4]));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(p, temp);
                Vector3 pos = c + right * ((i % 4) * 3.2f - 4.8f) + fwd * ((i / 4) * 3.4f - 3f);
                pos.y = GroundY(pos, ground) + 1f;
                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation(-fwd + right * Random.Range(-0.4f, 0.4f));
                foreach (var col in go.GetComponentsInChildren<Canvas>(true)) col.gameObject.SetActive(false);
                spiders.Add(go);
            }

            var camGo = new GameObject("ShowcaseCam");
            SceneManager.MoveGameObjectToScene(camGo, temp);
            var cam = camGo.AddComponent<Camera>();
            var main = Camera.main;
            if (main != null) { cam.CopyFrom(main); }
            cam.fieldOfView = main != null ? main.fieldOfView : 50f;
            float dist = cm != null ? cm.distance : 20f;
            float pitch = cm != null && cm.startPitch > 0 ? cm.startPitch : 45f;
            camGo.transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(pitch, 0, 0);
            camGo.transform.position = c - camGo.transform.forward * (dist + 6f);
            result.Log("camera dist {0} pitch {1} fov {2}", dist, pitch, cam.fieldOfView);

            void Pose(string state, float speed, float t)
            {
                foreach (var s in spiders)
                {
                    var a = s.GetComponentInChildren<Animator>();
                    a.Rebind();
                    a.SetFloat("Speed", speed);
                    if (state == "Attack") a.Play("Attack", 0, 0f);
                    else if (state == "Death") a.Play("Death", 0, 0f);
                    a.Update(0f);
                    a.Update(t + Random.Range(0f, 0.3f));
                }
            }
            Pose("Locomotion", 3f, 0.4f); Shot(cam, Out + "unity_crowd_walk.png");
            Pose("Attack", 0f, 0.38f); Shot(cam, Out + "unity_crowd_attack.png");
            Pose("Death", 0f, 0.9f); Shot(cam, Out + "unity_crowd_death.png");

            // close-up of the four colours, from the train's side (they face the train)
            Pose("Locomotion", 0f, 0.5f);
            Vector3 mid = (spiders[0].transform.position + spiders[3].transform.position) * 0.5f + Vector3.down * 0.4f;
            camGo.transform.position = mid - fwd * 10f + Vector3.up * 4f;
            camGo.transform.rotation = Quaternion.LookRotation(mid - camGo.transform.position);
            Shot(cam, Out + "unity_variants_close.png");

            var smr = spiders[0].GetComponentInChildren<SkinnedMeshRenderer>();
            result.Log("mesh tris {0} bones {1} material {2} quality {3}", smr.sharedMesh.triangles.Length / 3, smr.bones.Length, smr.sharedMaterial.name, smr.quality);
            result.Log("transforms per spider {0}", spiders[0].GetComponentsInChildren<Transform>(true).Length);
        }
        finally
        {
            EditorSceneManager.CloseScene(temp, true);
            SceneManager.SetActiveScene(lev);
        }
        result.Log("lev1 dirty after test: {0}", lev.isDirty);
    }

    static float GroundY(Vector3 p, float fallback)
    {
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 200f, Vector3.down, 400f).OrderBy(h => h.distance))
            if (h.collider.GetComponentInParent<Terrain>() != null || h.collider is TerrainCollider || h.collider.gameObject.isStatic) return h.point.y;
        return fallback;
    }

    static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
    }
}
