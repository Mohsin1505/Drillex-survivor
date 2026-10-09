using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Lev1 desert canyon, assembled from the existing DesertKit prefabs.
// * Terrain (Default layer, so the spawner's Ground raycasts still hit the original zone planes):
//   flat playfield at y = -0.05, dunes and rocky slopes rising outside the arena into the canyon walls.
// * Old floor / boundary-wall renderers are switched off (their colliders stay: spawning + bounds unchanged).
// * Everything lives under DESERT_ENVIRONMENT and is rebuilt deterministically each run.
internal class CommandScript : IRunCommand
{
    const string Kit = "Assets/Art/Environment/DesertKit/Prefabs";
    const string Dir = "Assets/Art/Environment/DesertLevel";
    // playable arena (inside the invisible boundary walls)
    const float AX0 = -246f, AX1 = 236f, AZ0 = -296f, AZ1 = 438f;
    const float ROUTE_HALF = 12f;
    // terrain extents
    const float TX0 = -560f, TZ0 = -480f, TW = 1120f, TL = 1260f, TH = 160f;
    const float BASE_Y = -0.05f;

    Dictionary<string, GameObject> prefabs;
    System.Random rng;
    Terrain terrain;
    List<Vector3> blockers = new List<Vector3>();   // x, z, radius of large placed items
    Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
    int placed;
    StringBuilder log = new StringBuilder();
    List<Vector4> mounds = new List<Vector4>();     // x, z, radius, height

    float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    T Pick<T>(params T[] a) => a[rng.Next(a.Length)];
    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

    // winding gameplay route (centre line), starts at the train (x = 0) and leads to the arch at the far end
    static float PX(float z) { float u = z + 272f; return 22f * Mathf.Sin(u / 120f) + 9f * Mathf.Sin(u / 53f); }
    static float RouteDist(float x, float z) => Mathf.Abs(x - PX(z));
    static float OutsideDist(float x, float z)
    {
        float dx = Mathf.Max(AX0 - x, 0f, x - AX1);
        float dz = Mathf.Max(AZ0 - z, 0f, z - AZ1);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    float Height(float x, float z)
    {
        float d = OutsideDist(x, z);
        float h = 0f;
        // dunes and rocky slopes outside the playfield, climbing into the canyon walls
        if (d > 0f)
        {
            float dune = Mathf.PerlinNoise(x * 0.012f + 31f, z * 0.012f + 7f);
            float ripple = Mathf.PerlinNoise(x * 0.05f + 3f, z * 0.05f + 91f);
            h = Smooth(0f, 34f, d) * (5f + dune * 9f) + Smooth(20f, 120f, d) * 22f + ripple * 1.2f * Smooth(0f, 10f, d);
        }
        // raised sandstone mounds under the mesa clusters inside the field
        foreach (var m in mounds)
        {
            float dist = Vector2.Distance(new Vector2(x, z), new Vector2(m.x, m.y));
            if (dist < m.z) { float t = 1f - dist / m.z; h = Mathf.Max(h, m.w * t * t * (3 - 2 * t)); }
        }
        return h;
    }

    // ---------------------------------------------------------------- textures / terrain
    Texture2D MakeTex(string name, Color a, Color b, float ripple, float speckle, int seed)
    {
        string path = Dir + "/Textures/" + name + ".png";
        const int N = 512;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var pr = new System.Random(seed);
        // tileable noise: sum of integer-frequency waves
        var waves = new List<Vector4>();
        for (int i = 0; i < 14; i++) waves.Add(new Vector4(pr.Next(1, 9) * (pr.Next(2) * 2 - 1), pr.Next(0, 9), (float)pr.NextDouble() * 6.28f, 1f / (1 + i * 0.6f)));
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float n = 0f, wsum = 0f;
                foreach (var w in waves) { n += Mathf.Sin(6.2831853f * (w.x * u + w.y * v) + w.z) * w.w; wsum += w.w; }
                n = n / wsum * 0.5f + 0.5f;
                float rip = Mathf.Sin(6.2831853f * (14f * v + 2f * u) + n * 5f) * 0.5f + 0.5f;
                float s = (float)pr.NextDouble();
                Color c = Color.Lerp(a, b, Mathf.Clamp01(n * 1.15f - 0.08f));
                c *= 1f - ripple * 0.5f + ripple * rip;
                if (s < speckle) c *= 0.82f + (float)pr.NextDouble() * 0.1f;
                else c *= 0.985f + (float)pr.NextDouble() * 0.03f;
                c.a = 0.04f;   // TerrainLit reads diffuse alpha as smoothness: keep the sand matte
                px[y * N + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        Directory.CreateDirectory(Dir + "/Textures");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Repeat; ti.maxTextureSize = 512; ti.anisoLevel = 4; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = false;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        foreach (var plat in new[] { "Android", "iPhone" })
        {
            var st = ti.GetPlatformTextureSettings(plat);
            st.overridden = true; st.maxTextureSize = 512; st.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(st);
        }
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    TerrainLayer Layer(string name, Texture2D tex, float tile, float smooth)
    {
        string path = Dir + "/" + name + ".terrainlayer";
        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
        l.diffuseTexture = tex;
        l.tileSize = new Vector2(tile, tile);
        l.smoothness = smooth;
        l.metallic = 0f;
        EditorUtility.SetDirty(l);
        return l;
    }

    void BuildTerrain(Transform root)
    {
        var sand = MakeTex("T_Desert_Sand", new Color(0.78f, 0.50f, 0.32f), new Color(0.88f, 0.60f, 0.39f), 0.035f, 0.02f, 11);
        var path = MakeTex("T_Desert_Path", new Color(0.86f, 0.60f, 0.38f), new Color(0.94f, 0.70f, 0.47f), 0.04f, 0.05f, 23);
        var rock = MakeTex("T_Desert_RedRock", new Color(0.55f, 0.24f, 0.15f), new Color(0.74f, 0.38f, 0.23f), 0.02f, 0.18f, 37);
        var layers = new[] { Layer("TL_Desert_Sand", sand, 22f, 0.0f), Layer("TL_Desert_Path", path, 12f, 0.0f), Layer("TL_Desert_RedRock", rock, 14f, 0.0f) };

        string tdPath = Dir + "/TD_DesertLevel.asset";
        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath);
        bool newTd = td == null;
        if (newTd) td = new TerrainData();
        td.heightmapResolution = 513;
        td.size = new Vector3(TW, TH, TL);
        int hr = td.heightmapResolution;
        var hs = new float[hr, hr];
        for (int j = 0; j < hr; j++)
            for (int i = 0; i < hr; i++)
            {
                float x = TX0 + TW * i / (hr - 1f), z = TZ0 + TL * j / (hr - 1f);
                hs[j, i] = Mathf.Clamp01(Height(x, z) / TH);
            }
        td.SetHeights(0, 0, hs);
        td.terrainLayers = layers;
        td.alphamapResolution = 512;
        int ar = td.alphamapResolution;
        var al = new float[ar, ar, 3];
        for (int j = 0; j < ar; j++)
            for (int i = 0; i < ar; i++)
            {
                float x = TX0 + TW * (i + 0.5f) / ar, z = TZ0 + TL * (j + 0.5f) / ar;
                float rd = RouteDist(x, z);
                float pathW = (1f - Smooth(ROUTE_HALF - 4f, ROUTE_HALF + 3f, rd)) * (z > AZ0 - 10f && z < AZ1 + 40f ? 1f : 0f);
                float h = Height(x, z);
                float patches = Smooth(0.62f, 0.78f, Mathf.PerlinNoise(x * 0.018f + 5f, z * 0.018f + 50f));
                float rockW = Mathf.Max(Smooth(1.5f, 9f, h), patches * 0.55f) * (1f - pathW);
                float sandW = Mathf.Max(0f, 1f - pathW - rockW);
                float sum = sandW + pathW + rockW;
                al[j, i, 0] = sandW / sum; al[j, i, 1] = pathW / sum; al[j, i, 2] = rockW / sum;
            }
        td.SetAlphamaps(0, 0, al);
        if (newTd) AssetDatabase.CreateAsset(td, tdPath); else EditorUtility.SetDirty(td);

        var tgo = Terrain.CreateTerrainGameObject(td);
        tgo.name = "Terrain_Desert";
        tgo.transform.SetParent(root, false);
        tgo.transform.position = new Vector3(TX0, BASE_Y, TZ0);
        tgo.layer = 0;
        terrain = tgo.GetComponent<Terrain>();
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (rp != null && rp.defaultTerrainMaterial != null) terrain.materialTemplate = rp.defaultTerrainMaterial;
        terrain.heightmapPixelError = 12f;
        terrain.basemapDistance = 350f;
        terrain.drawInstanced = true;
        terrain.drawTreesAndFoliage = false;
        terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(tgo, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        var tc = tgo.GetComponent<TerrainCollider>();
        if (tc) tc.enabled = false;   // visual only; gameplay ground stays the original zone planes
    }

    float GroundY(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + BASE_Y;

    // ---------------------------------------------------------------- placement
    Transform G(string name)
    {
        if (!groups.TryGetValue(name, out var t)) { t = new GameObject(name).transform; t.SetParent(groups["_root"], false); groups[name] = t; }
        return t;
    }

    bool Blocked(float x, float z, float r)
    {
        foreach (var b in blockers) if ((new Vector2(x - b.x, z - b.y)).magnitude < r + b.z) return true;
        return false;
    }

    GameObject Put(string name, float x, float z, string group, float rotY, float scale, float sink = 0.08f, bool block = false, float cull = 0f)
    {
        var pf = prefabs[name];
        var mf = pf.GetComponentInChildren<MeshFilter>();
        var b = mf.sharedMesh.bounds;
        float r = Mathf.Max(b.extents.x, b.extents.z) * scale;
        float y = GroundY(x, z);
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI / 3f + rotY * Mathf.Deg2Rad;
            y = Mathf.Min(y, GroundY(x + Mathf.Cos(a) * r * 0.75f, z + Mathf.Sin(a) * r * 0.75f));
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, G(group));
        go.transform.position = new Vector3(x, y - sink * scale, z);
        go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        go.transform.localScale = Vector3.one * scale;
        if (block) blockers.Add(new Vector3(x, z, r * 0.8f));
        if (cull > 0f)
        {
            var lg = go.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(cull, go.GetComponentsInChildren<Renderer>()) });
            lg.RecalculateBounds();
        }
        placed++;
        return go;
    }

    static readonly string[] Boulders = { "BOULDER_01", "BOULDER_02", "BOULDER_03", "BOULDER_04", "BOULDER_05" };
    static readonly string[] Blocks = { "ROCK_BLOCK_01", "ROCK_BLOCK_02", "ROCK_BLOCK_03", "ROCK_BLOCK_04", "ROCK_BLOCK_05", "ROCK_BLOCK_06", "ROCK_BLOCK_07", "ROCK_BLOCK_08", "ROCK_BLOCK_09", "ROCK_BLOCK_10" };
    static readonly string[] Smalls = { "ROCK_S_01", "ROCK_S_02", "ROCK_S_03", "ROCK_S_04", "ROCK_S_05", "ROCK_S_06", "ROCK_S_07", "ROCK_S_08", "ROCK_S_11", "ROCK_S_12" };
    static readonly string[] Debris = { "ROCK_DEBRIS_01", "ROCK_DEBRIS_02", "ROCK_S_09", "ROCK_S_10", "ROCK_STACK_01" };
    static readonly string[] Grass = { "GRASS_BUSH_01", "GRASS_BUSH_02", "GRASS_BUSH_03" };
    static readonly string[] Cacti = { "CACTUS_TALL_01", "CACTUS_TALL_02", "CACTUS_ARM_01", "CACTUS_MED_01" };
    static readonly string[] Shrubs = { "DESERT_SHRUB_01", "DESERT_SHRUB_02", "DRY_BUSH_01", "FLOWER_PLANT_01" };

    // a scatter of pieces around a point
    void Scatter(string[] names, int n, float cx, float cz, float r0, float r1, string group, float s0, float s1, float cull, float sink = 0.04f, float avoidRoute = ROUTE_HALF + 1f)
    {
        for (int i = 0; i < n; i++)
        {
            float a = R(0f, 6.2832f), rr = R(r0, r1);
            float x = cx + Mathf.Cos(a) * rr, z = cz + Mathf.Sin(a) * rr;
            if (RouteDist(x, z) < avoidRoute || OutsideDist(x, z) > 60f) continue;
            Put(Pick(names), x, z, group, R(0f, 360f), R(s0, s1), sink, false, cull);
        }
    }

    // pick a free spot in the field at a route distance band
    bool Spot(float dmin, float dmax, float r, out float x, out float z, float z0 = AZ0 + 8f, float z1 = AZ1 - 8f)
    {
        for (int t = 0; t < 60; t++)
        {
            z = R(z0, z1);
            float side = rng.Next(2) == 0 ? -1f : 1f;
            x = PX(z) + side * R(dmin, dmax);
            if (x < AX0 + 6f || x > AX1 - 6f) continue;
            if (Blocked(x, z, r)) continue;
            return true;
        }
        x = z = 0f;
        return false;
    }

    // ---------------------------------------------------------------- world
    void CanyonWalls()
    {
        // long side walls: cliff modules (front +Z) turned to face into the arena, with a stepped tower layer behind
        foreach (int side in new[] { -1, 1 })
        {
            float z = AZ0 - 40f;
            float wallX = side < 0 ? AX0 - 4f : AX1 + 4f;
            while (z < AZ1 + 60f)
            {
                float s = R(9f, 12.5f);
                string n = Pick("CLIFF_STRAIGHT_01", "CLIFF_STRAIGHT_02", "CLIFF_STRAIGHT_01", "CLIFF_STRAIGHT_02", "CLIFF_END_01");
                float depth = 0.95f * s;
                float x = wallX + side * (depth + R(0f, 5f));
                Put(n, x, z, "CANYON_WALLS", side < 0 ? 90f + R(-6f, 6f) : -90f + R(-6f, 6f), s, 0.3f / s * 1.5f, true);
                if (rng.NextDouble() < 0.75)
                    Put(Pick("CLIFF_TOWER_01", "CLIFF_TOWER_02", "ROCK_FORMATION_01", "CLIFF_CORNER_02"), x + side * R(22f, 40f), z + R(-10f, 10f),
                        "CANYON_WALLS", R(0f, 360f), R(9f, 14f), 0.04f, true);
                // rubble at the cliff foot
                for (int k = 0; k < 2; k++)
                    Put(Pick(Boulders), wallX - side * R(1f, 9f), z + R(-18f, 18f), "CANYON_WALLS", R(0f, 360f), R(2.6f, 4.6f), 0.12f, true, 0.004f);
                Scatter(Smalls, 3, wallX - side * 6f, z, 0f, 10f, "CANYON_WALLS", 1.5f, 3f, 0.006f);
                z += 4f * s * R(0.82f, 0.95f);
            }
        }
        // start end (behind the camera at spawn) and far end (around the arch gap)
        foreach (int end in new[] { -1, 1 })
        {
            float wallZ = end < 0 ? AZ0 - 6f : AZ1 + 6f;
            float x = AX0 - 20f;
            while (x < AX1 + 20f)
            {
                float s = R(9f, 12.5f);
                bool archGap = end > 0 && Mathf.Abs(x - PX(AZ1)) < 55f;
                if (!archGap)
                {
                    Put(Pick("CLIFF_STRAIGHT_01", "CLIFF_STRAIGHT_02", "CLIFF_END_01"), x, wallZ + end * (0.95f * s + R(0f, 5f)), "CANYON_WALLS",
                        (end < 0 ? 0f : 180f) + R(-6f, 6f), s, 0.2f / s * 1.5f, true);
                    if (rng.NextDouble() < 0.7)
                        Put(Pick("CLIFF_TOWER_01", "CLIFF_TOWER_02", "ROCK_FORMATION_01"), x + R(-10f, 10f), wallZ + end * R(30f, 48f), "CANYON_WALLS", R(0f, 360f), R(9f, 14f), 0.04f, true);
                }
                x += 4f * s * R(0.82f, 0.95f);
            }
        }
        // corner towers
        foreach (var c in new[] { new Vector2(AX0 - 20f, AZ0 - 20f), new Vector2(AX1 + 20f, AZ0 - 20f), new Vector2(AX0 - 20f, AZ1 + 20f), new Vector2(AX1 + 20f, AZ1 + 20f) })
            Put(Pick("CLIFF_TOWER_02", "CLIFF_TOWER_01"), c.x, c.y, "CANYON_WALLS", R(0f, 360f), R(12f, 15f), 0.04f, true);
    }

    void Arch()
    {
        // the arch: destination landmark straddling the route where it leaves the arena
        float az = AZ1 + 34f;
        Put("NATURAL_ARCH_01", PX(AZ1), az, "LANDMARKS", 0f, 9.5f, 0.05f, true);
        Put("ROCK_PILLAR_02", PX(AZ1) - 58f, az + 10f, "LANDMARKS", 20f, 9f, 0.03f, true);
        Put("ROCK_PILLAR_01", PX(AZ1) + 60f, az + 6f, "LANDMARKS", 140f, 8f, 0.03f, true);
    }

    void Landmarks()
    {
        // tall pillar groups inside the field: vertical silhouettes along the route
        var spots = new[] { new Vector3(-75f, -150f, 1f), new Vector3(85f, -60f, 1f), new Vector3(-110f, 40f, 1f), new Vector3(70f, 140f, 1f),
                            new Vector3(-60f, 250f, 1f), new Vector3(120f, 300f, 1f), new Vector3(-150f, -230f, 1f), new Vector3(165f, -200f, 1f) };
        foreach (var sp in spots)
        {
            float x = PX(sp.y) + sp.x, z = sp.y;
            mounds.Add(new Vector4(x, z, 30f, R(2.5f, 5f)));
        }
    }

    void PlaceLandmarkGroups()
    {
        foreach (var m in mounds)
        {
            float x = m.x, z = m.y;
            Put(Pick("ROCK_PILLAR_01", "ROCK_PILLAR_02"), x, z, "LANDMARKS", R(0f, 360f), R(4.5f, 6.5f), 0.04f, true);
            Put(Pick("ROCK_FORMATION_01", "CLIFF_TOWER_01", "CLIFF_TOWER_02"), x + R(-14f, 14f), z + R(9f, 16f), "LANDMARKS", R(0f, 360f), R(3.2f, 4.6f), 0.04f, true);
            if (rng.NextDouble() < 0.6)
                Put(Pick("ROCK_PILLAR_02", "ROCK_PILLAR_01"), x + R(-18f, 18f), z - R(8f, 14f), "LANDMARKS", R(0f, 360f), R(2.6f, 3.8f), 0.04f, true);
            Put("ROCK_WALL_01", x + R(-12f, 12f), z + R(-16f, -8f), "LANDMARKS", R(0f, 360f), R(3f, 4f), 0.06f, true);
            for (int k = 0; k < 5; k++)
                Put(k < 2 ? Pick(Blocks) : Pick(Boulders), x + R(-24f, 24f), z + R(-24f, 24f), "LANDMARKS", R(0f, 360f), R(1.6f, 3.2f), 0.1f, true, 0.005f);
            Scatter(Debris.Concat(Smalls).ToArray(), 10, x, z, 6f, 28f, "LANDMARKS", 1.2f, 2.4f, 0.006f);
            // life at the foot of the formation
            Scatter(Cacti, 2, x, z, 14f, 26f, "VEGETATION", 1.1f, 1.6f, 0.006f, 0.02f);
            Scatter(Grass.Concat(Shrubs).ToArray(), 6, x, z, 12f, 28f, "VEGETATION", 1.4f, 2.2f, 0.008f, 0.02f);
            if (rng.NextDouble() < 0.5)
                Put("DEAD_TREE_01", x + R(-20f, 20f), z + R(-22f, -14f), "PROPS", R(0f, 360f), R(1.6f, 2.2f), 0.03f, false, 0.004f);
        }
    }

    void Background()
    {
        // huge distant silhouettes beyond the canyon rim (fog softens them into the haze)
        for (int k = 0; k < 34; k++)
        {
            float x, z;
            int zone = rng.Next(3);
            if (zone == 0) { x = R(AX0 - 220f, AX0 - 90f); z = R(AZ0 - 120f, AZ1 + 200f); }
            else if (zone == 1) { x = R(AX1 + 90f, AX1 + 220f); z = R(AZ0 - 120f, AZ1 + 200f); }
            else { x = R(AX0 - 150f, AX1 + 150f); z = R(AZ1 + 110f, AZ1 + 220f); }
            if (x < TX0 + 20f || x > TX0 + TW - 20f || z > TZ0 + TL - 20f || z < TZ0 + 20f) continue;
            Put(Pick("CLIFF_TOWER_01", "CLIFF_TOWER_02", "ROCK_PILLAR_01", "ROCK_PILLAR_02", "ROCK_FORMATION_01"), x, z, "BACKGROUND", R(0f, 360f), R(13f, 22f), 0.03f, true);
        }
    }

    void FieldClusters()
    {
        // mid-ground boulder clusters (boulder + blocks + debris + dry bush)
        for (int k = 0; k < 46; k++)
        {
            if (!Spot(22f, 200f, 10f, out float x, out float z)) continue;
            Put(Pick(Boulders), x, z, "ROCKS", R(0f, 360f), R(2.2f, 4.2f), 0.12f, true);
            int extra = rng.Next(1, 4);
            for (int i = 0; i < extra; i++)
                Put(Pick(Blocks.Concat(Boulders).ToArray()), x + R(-7f, 7f), z + R(-7f, 7f), "ROCKS", R(0f, 360f), R(1.2f, 2.6f), 0.1f, false, 0.006f);
            Scatter(Debris.Concat(Smalls).ToArray(), 6, x, z, 3f, 11f, "ROCKS", 0.9f, 1.8f, 0.007f);
            if (rng.NextDouble() < 0.6) Scatter(new[] { "DRY_BUSH_01", "GRASS_BUSH_03", "DESERT_SHRUB_02" }, 2, x, z, 4f, 9f, "VEGETATION", 1.4f, 2f, 0.008f, 0.02f);
            if (rng.NextDouble() < 0.25) Scatter(new[] { "BONE_PILE_01", "BONE_RIB_03" }, 1, x, z, 5f, 9f, "PROPS", 1.2f, 1.6f, 0.006f, 0.05f);
        }
        // vegetation pockets: cactus families, agave/yucca beds, grass meadows
        for (int k = 0; k < 80; k++)
        {
            if (!Spot(15f, 210f, 5f, out float x, out float z)) continue;
            int kind = rng.Next(3);
            if (kind == 0)
            {
                Put(Pick(Cacti), x, z, "VEGETATION", R(0f, 360f), R(1.2f, 1.7f), 0.02f, true, 0.005f);
                Scatter(new[] { "CACTUS_SMALL_01", "CACTUS_MED_01" }, 2, x, z, 1.5f, 4f, "VEGETATION", 1f, 1.4f, 0.008f, 0.02f);
                Scatter(Grass, 4, x, z, 1f, 5f, "VEGETATION", 1.5f, 2.3f, 0.01f, 0.02f);
                Scatter(Smalls, 3, x, z, 1f, 5f, "ROCKS", 0.8f, 1.4f, 0.01f);
            }
            else if (kind == 1)
            {
                Put(Pick("AGAVE_PLANT_01", "YUCCA_PLANT_01"), x, z, "VEGETATION", R(0f, 360f), R(1.6f, 2.4f), 0.02f, true, 0.008f);
                Scatter(new[] { "AGAVE_PLANT_01", "YUCCA_PLANT_01", "DESERT_SHRUB_01", "FLOWER_PLANT_01", "GRASS_BUSH_02" }, 5, x, z, 1.5f, 6f, "VEGETATION", 1.4f, 2.2f, 0.01f, 0.02f);
                Scatter(Debris, 1, x, z, 2f, 6f, "ROCKS", 1f, 1.6f, 0.01f);
            }
            else
            {
                Scatter(Grass, 9, x, z, 0f, 7f, "VEGETATION", 1.6f, 2.6f, 0.01f, 0.02f);
                Scatter(new[] { "FLOWER_PLANT_01", "DESERT_SHRUB_01", "DRY_BUSH_01" }, 3, x, z, 1f, 7f, "VEGETATION", 1.4f, 2f, 0.01f, 0.02f);
                blockers.Add(new Vector3(x, z, 6f));
            }
        }
        for (int k = 0; k < 320; k++)
        {
            float z = R(AZ0 + 4f, AZ1 - 4f);
            float d = ROUTE_HALF + 2f + Mathf.Pow((float)rng.NextDouble(), 1.6f) * 45f;
            float x = PX(z) + (rng.Next(2) == 0 ? -d : d);
            if (x < AX0 + 4f || x > AX1 - 4f || Blocked(x, z, 1f)) continue;
            if (rng.NextDouble() < 0.5) Put(Pick(Smalls.Concat(Debris).ToArray()), x, z, "ROCKS", R(0f, 360f), R(0.9f, 1.7f), 0.05f, false, 0.012f);
            else Put(Pick("GRASS_BUSH_01", "GRASS_BUSH_02", "GRASS_BUSH_03", "DRY_BUSH_01", "CACTUS_SMALL_01", "FLOWER_PLANT_01"), x, z, "VEGETATION", R(0f, 360f), R(1.4f, 2.3f), 0.02f, false, 0.014f);
        }
        // sparse singles across the open sand (keeps contrast: lots of empty sand between pockets)
        for (int k = 0; k < 220; k++)
        {
            float z = R(AZ0 + 4f, AZ1 - 4f), x = R(AX0 + 4f, AX1 - 4f);
            if (RouteDist(x, z) < ROUTE_HALF + 1.5f) continue;
            if (Mathf.PerlinNoise(x * 0.02f + 70f, z * 0.02f) < 0.45f) continue;   // clump into pockets
            if (rng.NextDouble() < 0.55) Put(Pick(Smalls.Concat(Debris).ToArray()), x, z, "ROCKS", R(0f, 360f), R(0.9f, 1.8f), 0.05f, false, 0.01f);
            else Put(Pick("GRASS_BUSH_01", "GRASS_BUSH_03", "DRY_BUSH_01", "CACTUS_SMALL_01"), x, z, "VEGETATION", R(0f, 360f), R(1.4f, 2.2f), 0.02f, false, 0.012f);
        }
        // rocky ground patches using the kit's ground tiles, sunk so only the rocky top shows
        // (kit ground tiles tried here: they read as flat square slabs on the terrain, so the terrain splat paints the rocky patches instead)
    }

    void Storytelling()
    {
        // giant ribcage half-buried beside the route, early in the run (first big "wow" prop)
        float rz = -195f, rx = PX(rz) + 26f;
        for (int k = 0; k < 6; k++)
            Put(k % 2 == 0 ? "BONE_RIB_01" : "BONE_RIB_02", rx, rz + k * 4.2f, "PROPS", 90f + k * 2f, 4.2f, 0.18f, false);
        Put("BONE_PILE_01", rx - 9f, rz + 26f, "PROPS", 30f, 2.4f, 0.05f);
        Put("BONE_RIB_03", rx + 8f, rz - 6f, "PROPS", 200f, 2.6f, 0.1f);
        blockers.Add(new Vector3(rx, rz + 10f, 18f));
        Scatter(Grass, 6, rx, rz + 10f, 8f, 16f, "VEGETATION", 1.5f, 2.2f, 0.01f, 0.02f);
        // abandoned supply stop next to the start
        float sx = PX(-232f) - 20f, sz = -232f;
        Put("SIGN_POST_01", PX(-238f) - 14.5f, -238f, "PROPS", 120f, 1.8f, 0.03f);
        Put("CRATE_01", sx, sz, "PROPS", 15f, 1.9f, 0.02f);
        Put("CRATE_01", sx - 1.8f, sz + 1.2f, "PROPS", -25f, 1.6f, 0.02f);
        Put("CRATE_01", sx - 0.9f, sz + 0.6f, "PROPS", 40f, 1.3f, -0.62f);
        Put("BARREL_01", sx + 1.9f, sz + 1.5f, "PROPS", 0f, 1.7f, 0.02f);
        Put("BARREL_01", sx + 2.9f, sz - 0.4f, "PROPS", 70f, 1.7f, 0.02f);
        Put("WOOD_FENCE_02", sx - 4.5f, sz + 4f, "PROPS", 75f, 1.6f, 0.08f);
        Put("WOOD_FENCE_01", sx - 5.2f, sz - 0.4f, "PROPS", 95f, 1.6f, 0.08f);
        blockers.Add(new Vector3(sx, sz, 9f));
        // broken fence lines guiding the route (both sides, in runs with gaps)
        for (int run = 0; run < 14; run++)
        {
            float z0 = R(AZ0 + 30f, AZ1 - 40f);
            float side = rng.Next(2) == 0 ? -1f : 1f;
            int n = rng.Next(4, 9);
            for (int i = 0; i < n; i++)
            {
                float z = z0 + i * 4.3f;
                if (rng.NextDouble() < 0.15) continue;
                float dx = PX(z + 1f) - PX(z - 1f);
                float yaw = Mathf.Atan2(dx, 2f) * Mathf.Rad2Deg + 90f;
                Put(i % 4 == 3 ? "WOOD_FENCE_02" : "WOOD_FENCE_01", PX(z) + side * (ROUTE_HALF + 3.5f), z, "PROPS", yaw + R(-4f, 4f), 1.8f, 0.08f, false, 0.008f);
            }
        }
        // lone dead trees and bones in the open
        for (int k = 0; k < 10; k++)
            if (Spot(25f, 180f, 4f, out float x, out float z))
            {
                Put("DEAD_TREE_01", x, z, "PROPS", R(0f, 360f), R(1.8f, 2.6f), 0.03f, true, 0.004f);
                Scatter(new[] { "BONE_PILE_01", "ROCK_DEBRIS_01", "DRY_BUSH_01" }, 2, x, z, 2f, 5f, "PROPS", 1.2f, 1.8f, 0.008f, 0.03f);
            }
        for (int k = 0; k < 10; k++)
            if (Spot(16f, 150f, 3f, out float x, out float z))
                Put(Pick("BONE_RIB_01", "BONE_RIB_03", "BONE_PILE_01"), x, z, "PROPS", R(0f, 360f), R(1.6f, 2.6f), 0.12f, true, 0.006f);
    }

    // route edges: alternating small set-pieces every ~16 m so the band the camera always sees is never empty
    void RouteEdges()
    {
        float z = AZ0 + 50f;
        int k = 0;
        while (z < AZ1 - 20f)
        {
            float side = (k++ % 2 == 0) ? -1f : 1f;
            float x = PX(z) + side * R(15f, 30f);
            if (!Blocked(x, z, 4f) && x > AX0 + 6f && x < AX1 - 6f)
            {
                int kind = rng.Next(4);
                if (kind == 0)
                {
                    Put(Pick(Boulders), x, z, "ROCKS", R(0f, 360f), R(1.8f, 3.2f), 0.12f, true, 0.005f);
                    Scatter(Smalls.Concat(Debris).ToArray(), 5, x, z, 2f, 7f, "ROCKS", 0.9f, 1.6f, 0.01f);
                    Scatter(Grass, 3, x, z, 2f, 6f, "VEGETATION", 1.5f, 2.2f, 0.012f, 0.02f);
                }
                else if (kind == 1)
                {
                    Put(Pick(Cacti), x, z, "VEGETATION", R(0f, 360f), R(1.2f, 1.6f), 0.02f, true, 0.006f);
                    Scatter(new[] { "CACTUS_SMALL_01", "GRASS_BUSH_01", "GRASS_BUSH_02", "ROCK_S_04" }, 4, x, z, 1.2f, 4.5f, "VEGETATION", 1.1f, 2f, 0.012f, 0.02f);
                }
                else if (kind == 2)
                {
                    Put(Pick(Blocks), x, z, "ROCKS", R(0f, 360f), R(1.6f, 2.8f), 0.1f, true, 0.005f);
                    Scatter(new[] { "DRY_BUSH_01", "BONE_PILE_01", "ROCK_DEBRIS_02" }, 3, x, z, 2f, 6f, "PROPS", 1.2f, 1.8f, 0.01f, 0.03f);
                }
                else
                {
                    Scatter(new[] { "AGAVE_PLANT_01", "YUCCA_PLANT_01", "DESERT_SHRUB_01", "FLOWER_PLANT_01", "GRASS_BUSH_03" }, 6, x, z, 0f, 5f, "VEGETATION", 1.4f, 2.2f, 0.012f, 0.02f);
                    blockers.Add(new Vector3(x, z, 5f));
                }
            }
            if (k % 2 == 0) z += R(7f, 12f);
        }
    }

    void Foreground()
    {
        // strong framing around the spawn (the first thing the gameplay camera sees)
        float z0 = -272f;
        var picks = new (float dx, float dz, string n, float s)[] {
            (-16f, -6f, "BOULDER_02", 4.2f), (-22f, 8f, "ROCK_BLOCK_05", 3.6f), (17f, -2f, "BOULDER_01", 3.8f), (24f, 14f, "ROCK_BLOCK_09", 3.4f),
            (-30f, 30f, "BOULDER_03", 4.5f), (33f, 36f, "BOULDER_05", 4f), (-19f, 46f, "ROCK_BLOCK_08", 2.6f), (20f, 58f, "ROCK_BLOCK_02", 3f) };
        foreach (var p in picks)
        {
            float x = PX(z0 + p.dz) + p.dx, z = z0 + p.dz;
            Put(p.n, x, z, "FOREGROUND", R(0f, 360f), p.s, 0.12f, true);
            Scatter(Smalls.Concat(Debris).ToArray(), 4, x, z, 3f, 8f, "FOREGROUND", 1f, 1.8f, 0.01f);
        }
        Put("CACTUS_TALL_02", PX(z0 + 4f) - 14.5f, z0 + 4f, "FOREGROUND", 30f, 1.6f, 0.02f);
        Put("CACTUS_ARM_01", PX(z0 + 22f) + 15.5f, z0 + 22f, "FOREGROUND", 200f, 1.5f, 0.02f);
        Scatter(Grass, 8, PX(z0) - 15f, z0 + 10f, 0f, 6f, "FOREGROUND", 1.7f, 2.4f, 0.01f, 0.02f);
        Scatter(Grass, 8, PX(z0) + 16f, z0 + 12f, 0f, 6f, "FOREGROUND", 1.7f, 2.4f, 0.01f, 0.02f);
    }

    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Lev1.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        rng = new System.Random(20261002);
        prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { Kit }).Select(AssetDatabase.GUIDToAssetPath)
            .ToDictionary(p => Path.GetFileNameWithoutExtension(p), p => AssetDatabase.LoadAssetAtPath<GameObject>(p));

        var old = GameObject.Find("DESERT_ENVIRONMENT");
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject("DESERT_ENVIRONMENT").transform;
        groups["_root"] = root;

        // hide the prototype floors / boundary boxes (colliders untouched)
        foreach (var n in new[] { "FirstZone", "Zone 2", "Zone 2 (1)", "Plane (1)" })
        {
            var g = GameObject.Find(n);
            if (g) g.GetComponent<MeshRenderer>().enabled = false;
        }
        var walls = GameObject.Find("walls");
        if (walls) foreach (var r in walls.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;

        // keep the route and the spawn clear
        blockers.Add(new Vector3(0f, -272f, 12f));

        Landmarks();                  // registers mounds (terrain needs them)
        BuildTerrain(root);
        Arch();
        CanyonWalls();
        var archAndPillars = 0;
        PlaceLandmarkGroups();
        Background();
        Foreground();
        Storytelling();
        RouteEdges();
        FieldClusters();

        // count triangles
        long tris = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>()) tris += mf.sharedMesh.triangles.Length / 3;
        foreach (var t in groups) if (t.Key != "_root") log.AppendLine(t.Key + ": " + t.Value.childCount);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        result.Log("placed " + placed + " instances, tris " + tris + " mounds " + mounds.Count + archAndPillars + "\n" + log);
    }
}
