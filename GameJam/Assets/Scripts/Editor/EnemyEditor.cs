using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Enemy))]
public class EnemyEditor : Editor
{
    int number = 1;
    void OnEnable() { number = ((Enemy)target).Number; }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var enemy = (Enemy)target;
        EditorGUILayout.LabelField("Resonance number", enemy.Number.ToString());
        number = EditorGUILayout.IntSlider("Debug: number", number, 1, 6);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !enemy.Alive))
            if (GUILayout.Button("Set enemy number")) enemy.DebugSetNumber(number);
    }
}
