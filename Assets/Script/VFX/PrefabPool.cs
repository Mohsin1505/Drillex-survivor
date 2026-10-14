using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Minimal per-prefab object pool for high-frequency spawns (machine-gun bullets, impacts, muzzle sparks).
// Pooled instances live in the active scene and the pool forgets them when a scene is unloaded.
public static class PrefabPool
{
    static readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
    static bool hooked;

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        Hook();
        if (!pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            pools[prefab] = stack;
        }
        while (stack.Count > 0)
        {
            var go = stack.Pop();
            if (go == null) continue;
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            return go;
        }
        var inst = Object.Instantiate(prefab, position, rotation);
        var tag = inst.GetComponent<PooledObject>();
        if (tag == null) tag = inst.AddComponent<PooledObject>();
        tag.prefab = prefab;
        return inst;
    }

    // Returns the object to its pool, or destroys it if it was never pooled.
    public static void Release(GameObject go)
    {
        if (go == null) return;
        var tag = go.GetComponent<PooledObject>();
        if (tag == null || tag.prefab == null) { Object.Destroy(go); return; }
        if (!go.activeSelf) return;
        go.SetActive(false);
        if (!pools.TryGetValue(tag.prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            pools[tag.prefab] = stack;
        }
        stack.Push(go);
    }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        SceneManager.sceneUnloaded += _ => pools.Clear();
    }
}

public class PooledObject : MonoBehaviour
{
    [HideInInspector] public GameObject prefab;
}
