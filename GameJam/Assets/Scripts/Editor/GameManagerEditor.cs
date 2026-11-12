using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Presentation only: hidden properties remain serialized, preserving existing scene tuning.
[CustomEditor(typeof(GameManager)), CanEditMultipleObjects]
public class GameManagerEditor : Editor
{
    static readonly HashSet<string> InternalFields = new HashSet<string> {
        "hud", "physicsStep", "board", "ball", "specialEnemy", "bumperEnemy", "handSize"
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

    int debugCardNumber=6;
    string debugDice="6, 2";
    Card.Equipment debugEnemyElement;
    Card.Equipment debugEquipment;
    Vector3 debugEnemyPosition = new Vector3(0, 0, 5);

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
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Luck Number v0.5 · Developer Tools", EditorStyles.boldLabel);
        var gm = (GameManager)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying || gm.IsGameOver)) {
            debugDice=EditorGUILayout.TextField("Next dice (complete sequence)",debugDice);
            if(GUILayout.Button("Set next dice sequence")) {
                var values=new System.Collections.Generic.List<int>();bool valid=true;
                foreach(var part in debugDice.Split(',')) {if(int.TryParse(part.Trim(),out int value))values.Add(value);else valid=false;}
                if(!valid || !gm.DebugSetDice(values.ToArray()))Debug.LogWarning("Enter 1-5 dice, each 1-6. The count must equal the submitted card count.");
            }
            EditorGUILayout.LabelField("Pending debug dice",gm.FixedDice);
            if(GUILayout.Button("Clear fixed dice"))gm.DebugClearDice();
            debugCardNumber = EditorGUILayout.IntSlider("Card number", debugCardNumber, 1, 6);
            debugEquipment = (Card.Equipment)EditorGUILayout.EnumPopup("Equipment type", debugEquipment);
            using (new EditorGUI.DisabledScope(gm.IsHandFull))
                if (GUILayout.Button("Add specified card")) gm.DebugAddCard(debugCardNumber, debugEquipment);
            EditorGUILayout.LabelField("Run statistics",gm.Metrics.Summary(),EditorStyles.wordWrappedLabel);
            if(GUILayout.Button("Log run statistics")) Debug.Log(gm.Metrics.Summary());
            EditorGUILayout.LabelField("Recharge measurements",gm.Metrics.RechargeSummary(),EditorStyles.wordWrappedLabel);
            debugEnemyElement=(Card.Equipment)EditorGUILayout.EnumPopup("Enemy element",debugEnemyElement);
            debugEnemyPosition = EditorGUILayout.Vector3Field("Board-local position", debugEnemyPosition);
            if (GUILayout.Button("Spawn specified enemy")) gm.SpawnEnemy(debugEnemyElement,debugEnemyPosition);
        }
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
        if (group == "spawning") return !SpawnInternals.Contains(field) && !field.StartsWith("special") && !field.StartsWith("bumper") && field != "normalTargets";
        if (EnemyInternals.Contains(field)) return false;
        if (group == "normalEnemy") return field != "lifetime" && field != "kickSpeed" && field != "hitEnergy" && field != "pulseDamage" && field != "hitDamage" && field != "revealDuration";
        // Exact matches instantly kill specials, so HP and ordinary damage cannot change the outcome.
        if (group == "specialEnemy") return field != "health" && field != "hitDamage" &&
            field != "revealDuration" && field != "kickSpeed";
        // Bumpers cannot take damage, reveal numbers, grant energy or emit match pulses.
        if (group == "bumperEnemy") return field != "health" && field != "hitDamage" &&
            field != "hitEnergy" && field != "pulseDamage" && field != "revealDuration" && field != "lifetime";
        return true;
    }
}
