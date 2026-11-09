using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Presentation only: hidden properties remain serialized, preserving existing scene tuning.
[CustomEditor(typeof(GameManager)), CanEditMultipleObjects]
public class GameManagerEditor : Editor
{
    static readonly HashSet<string> InternalFields = new HashSet<string> {
        "hud", "physicsStep", "board", "ball"
    };

    static readonly HashSet<string> SpawnInternals = new HashSet<string> {
        "normalAttempts", "bumperAttempts", "specialAttempts",
        "boardHalfWidth", "arcCenterZ", "arcRadius", "ballRadius",
        "dangerLineScale", "dangerLineOffset", "dangerLineColor"
    };

    static readonly HashSet<string> EnemyInternals = new HashSet<string> {
        "halfHeight", "centerHeight", "capPosition", "capScale", "flashDuration",
        "ringColor", "fillColor", "matchColor", "previewColor", "flashColor",
        "hitPopupColor", "killPopupColor"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var property = serializedObject.GetIterator();
        bool first = true;
        while (property.NextVisible(first))
        {
            first = false;
            if (property.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property);
                continue;
            }
            if (InternalFields.Contains(property.name)) continue;
            if (property.name == "spawning" || property.name == "normalEnemy" ||
                property.name == "specialEnemy" || property.name == "bumperEnemy")
                DrawTuningGroup(property);
            else EditorGUILayout.PropertyField(property, true);
        }
        serializedObject.ApplyModifiedProperties();
    }

    static void DrawTuningGroup(SerializedProperty group)
    {
        group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, group.displayName, true);
        if (!group.isExpanded) return;
        using (new EditorGUI.IndentLevelScope())
        {
            var child = group.Copy();
            var end = group.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                if (IsGameplayField(group.name, child.name)) EditorGUILayout.PropertyField(child, true);
            }
        }
    }

    public static bool IsGameplayField(string group, string field)
    {
        if (group == "spawning") return !SpawnInternals.Contains(field);
        if (EnemyInternals.Contains(field)) return false;
        if (group == "normalEnemy") return field != "lifetime" && field != "kickSpeed";
        // Exact matches instantly kill specials, so HP and ordinary damage cannot change the outcome.
        if (group == "specialEnemy") return field != "health" && field != "hitDamage" &&
            field != "revealDuration" && field != "kickSpeed";
        // Bumpers cannot take damage, reveal numbers, grant energy or emit match pulses.
        if (group == "bumperEnemy") return field != "health" && field != "hitDamage" &&
            field != "hitEnergy" && field != "pulseDamage" && field != "revealDuration" && field != "lifetime";
        return true;
    }
}
