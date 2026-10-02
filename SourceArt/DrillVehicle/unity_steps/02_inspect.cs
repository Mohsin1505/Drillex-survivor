using UnityEngine;
using UnityEditor;
using System.Text;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        var rock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/RockZ1.prefab");
        foreach (var c in rock.GetComponentsInChildren<Collider>(true))
            sb.AppendLine("rock collider " + c.GetType().Name + " trigger=" + c.isTrigger + " on " + c.name);
        var train = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/Train.prefab");
        foreach (var mb in train.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            sb.Append(mb.name + "/" + mb.GetType().Name + ": ");
            while (it.NextVisible(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                    sb.Append(it.propertyPath + "=" + (it.objectReferenceValue ? it.objectReferenceValue.name : "null") + "; ");
            }
            sb.AppendLine();
        }
        var lt = train.GetComponentInChildren<Light>(true);
        sb.AppendLine("light " + lt.type + " range " + lt.range + " angle " + lt.spotAngle + " intensity " + lt.intensity + " rot " + lt.transform.localEulerAngles);
        result.Log(sb.ToString());
    }
}
