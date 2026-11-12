using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Enemy))]
public class EnemyEditor:Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var enemy=(Enemy)target;
        EditorGUILayout.LabelField("Element / immunity",enemy.Element.ToString());
        EditorGUILayout.HelpBox("Element is fixed at spawn. Its own damage and status are immune; base ball damage is neutral.",MessageType.Info);
    }
}
