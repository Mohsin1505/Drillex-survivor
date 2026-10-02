using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using System.Text;

// Review scene with every desert kit prefab laid out by collection, plus a PNG capture of it.
internal class CommandScript : IRunCommand
{
    const string Root = "Assets/Art/Environment/DesertKit";

    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.86f, 0.70f);
        sun.intensity = 1.6f;
        sun.shadows = LightShadows.Soft;
        sunGo.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.62f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.52f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.25f, 0.20f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Floor";
        ground.transform.localScale = new Vector3(10f, 1f, 8f);
        ground.transform.position = new Vector3(0f, 0f, -25f);
        var gm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        gm.SetColor("_BaseColor", new Color(0.78f, 0.55f, 0.38f));
        ground.GetComponent<Renderer>().sharedMaterial = gm;

        var root = new GameObject("DesertKit_All").transform;
        var colls = AssetDatabase.GetSubFolders(Root + "/Prefabs").OrderBy(p => p).ToArray();
        float z = -50f;
        int count = 0;
        foreach (var dir in colls)
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
            float rowWidth = prefabs.Sum(q => AssetDatabase.LoadAssetAtPath<GameObject>(q).GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.x + 1.2f);
            float x = -rowWidth * 0.5f, rowDepth = 0f;
            var row = new GameObject(Path.GetFileName(dir)).transform;
            row.SetParent(root);
            foreach (var p in prefabs)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p), row);
                var b = go.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                go.transform.position = new Vector3(x + b.extents.x, 0f, z);
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // asset fronts face +Z; turn them to the review camera
                x += b.size.x + 1.2f;
                rowDepth = Mathf.Max(rowDepth, b.size.z);
                count++;
            }
            z += rowDepth + 3f;
        }

        var camGo = new GameObject("ReviewCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.62f, 0.72f);
        cam.farClipPlane = 400f;
        camGo.transform.position = new Vector3(0f, 30f, -78f);
        camGo.transform.LookAt(new Vector3(0f, 0f, -30f));

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/DesertKit_Showcase.unity");

        var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 8 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        File.WriteAllBytes(@"D:\Drillex\SourceArt\DesertKit\_preview\Unity_DesertKit_Showcase.png", tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        EditorSceneManager.SaveScene(scene);
        result.Log("placed " + count + " prefabs in " + colls.Length + " rows");
    }
}
