using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardBalanceConfig))]
public sealed class CardBalanceConfigEditor : Editor
{
    Card.Equipment element;
    CastTier tier=CastTier.Normal;
    int held=1,copies=1,luckyHeld,drawCount=5,missing=1;
    SerializedProperty data;
    void Fields(string title,params string[] names){EditorGUILayout.Space();EditorGUILayout.LabelField(title,EditorStyles.boldLabel);foreach(var name in names)EditorGUILayout.PropertyField(data.FindPropertyRelative(name));}
    public override void OnInspectorGUI()
    {
        var asset=(CardBalanceConfig)target;
        if(asset.settings==null){EditorGUILayout.HelpBox("Settings missing. Safe defaults active.",MessageType.Error);if(GUILayout.Button("Create default settings")){Undo.RecordObject(asset,"Reset card settings");asset.settings=new LuckTuning();EditorUtility.SetDirty(asset);}return;}
        serializedObject.Update();data=serializedObject.FindProperty("settings");
        Fields("Recharge sources","rechargePerSecond","rechargeHit");
        EditorGUILayout.PropertyField(data.FindPropertyRelative("draws"),new GUIContent("Draws, lucky bonuses & missing-slot costs"),true);
        EditorGUILayout.Space();EditorGUILayout.LabelField("Time score & physical-hit combo",EditorStyles.boldLabel);EditorGUILayout.PropertyField(data.FindPropertyRelative("scoring"),true);
        EditorGUILayout.HelpBox("Natural recovery is retained. Each qualifying physical ball contact grants the full hit amount, with no rate cap. Burning, skills and kills grant no charge. Full hands stop charging.",MessageType.Info);
        Fields("Ball collision damage","collisionDamage");
        Fields("Held Arc","passiveArcDamage","passiveArcRadius");HeldTable("arcTargetCounts","Nearby targets (direct hit receives Arc separately)");
        Fields("Held Spark","passiveBurnDps","passiveBurnDuration","passiveSparkRadius");HeldTable("sparkTargetCounts","Nearby targets (direct hit separate)");HeldTable("sparkDpsMultipliers","Burn DPS multiplier");
        Fields("Held Shockwave (stun only)","passiveStunDuration","passiveStunImmunity");HeldTable("shockRadii","Radius (m)");
        Fields("Shared flame timing","burnTickInterval");
        Fields("Active Arc timing","arcWaveInterval");
        Skills("normalSkills","Normal");Skills("luckySkills","Lucky");
        Fields("Same-element stacking (Arc / Spark only)","extraArcWaves","extraBurnPerCard");
        EditorGUILayout.HelpBox("Cast the entire same-number group instantly. Lucky status is sampled at submission; all subsequent waves keep captured parameters.",MessageType.Info);
        Fields("Existing enemy queue spacing","enemyQueueGap");
        serializedObject.ApplyModifiedProperties();
        var config=(CardBalanceConfig)target;bool valid=config.settings!=null&&config.settings.Valid();
        if(config.settings==null)EditorGUILayout.HelpBox("Settings missing. Safe defaults active.",MessageType.Error);
        else foreach(var error in config.settings.Errors())EditorGUILayout.HelpBox(error+" Safe defaults active until fixed.",MessageType.Error);
        var t=valid?config.settings:new LuckTuning();
        EditorGUILayout.Space();EditorGUILayout.LabelField("Actual calculation preview",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Charge per physical contact",t.rechargeHit.ToString("0.###")+" (no rate cap)");
        EditorGUILayout.LabelField("Held fire accumulation per tick",LuckTuning.BurnDamage(t.HeldSparkDps(held),t.burnTickInterval).ToString("0.###"));
        EditorGUILayout.HelpBox("Each tick adds DPS × tick interval to the enemy's burn remainder. Only whole points reduce HP and show damage numbers. Refreshing or switching burn sources preserves the remainder; fully extinguishing then reigniting starts a new accumulator.",MessageType.Info);
        element=(Card.Equipment)EditorGUILayout.EnumPopup("Element",element);held=EditorGUILayout.IntSlider("Held cards of this element",held,1,5);luckyHeld=EditorGUILayout.IntSlider("Lucky cards of this element",luckyHeld,0,held);copies=EditorGUILayout.IntSlider("Cast cards of this element",copies,1,5);tier=EditorGUILayout.Toggle("Lucky cast",tier==CastTier.Lucky)?CastTier.Lucky:CastTier.Normal;
        EditorGUILayout.HelpBox(t.HeldSummary(element,held,luckyHeld),MessageType.None);
        drawCount=EditorGUILayout.IntSlider("New cards in draw batch",drawCount,1,5);EditorGUILayout.LabelField("Chance of a lucky number",t.draws.LuckyChance(drawCount).ToString("P2"));
        missing=EditorGUILayout.IntSlider("Empty slots",missing,1,5);EditorGUILayout.LabelField("Charge to refill all slots",t.draws.RefillCost(missing).ToString("0.###"));
        var p=t.Plan(element,tier,copies);EditorGUILayout.HelpBox(p.Summary(element),MessageType.None);
        EditorGUILayout.LabelField("Total effect duration",p.Duration(element).ToString("0.###")+" s");
        if(element!=Card.Equipment.Spark)EditorGUILayout.LabelField("First-to-last wave span",((p.waves-1)*(double)p.waveInterval).ToString("0.###")+" s");
        EditorGUILayout.LabelField("Damage per eligible target",p.TheoreticalDamage(element).ToString("0.###"));
        EditorGUILayout.HelpBox("Preview assumes a batch containing only the chosen element, a surviving non-immune target, and a fresh burn without overlap. Shock duration includes its stun; wave span is shown separately.",MessageType.Info);
    }
    void Skills(string name,string label)
    {
        var rows=data.FindPropertyRelative(name);EditorGUILayout.LabelField(label+" active skills",EditorStyles.boldLabel);
        if(rows.arraySize!=3){EditorGUILayout.PropertyField(rows,true);return;}
        for(int i=0;i<3;i++){
            var row=rows.GetArrayElementAtIndex(i);EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.LabelField(((Card.Equipment)i).ToString());
            var mode=row.FindPropertyRelative("targetMode");EditorGUILayout.PropertyField(mode);if(mode.enumValueIndex==0)EditorGUILayout.PropertyField(row.FindPropertyRelative("targetCount"));
            if(i==1){EditorGUILayout.PropertyField(row.FindPropertyRelative("burnDps"));EditorGUILayout.PropertyField(row.FindPropertyRelative("burnDuration"));}
            else{EditorGUILayout.PropertyField(row.FindPropertyRelative("damage"));if(i==0)EditorGUILayout.PropertyField(row.FindPropertyRelative("waves"));else{EditorGUILayout.LabelField("Impacts","1 (fixed)");EditorGUILayout.PropertyField(row.FindPropertyRelative("stunDuration"));}}
            EditorGUILayout.EndVertical();
        }
    }
    void HeldTable(string name,string label)
    {
        var p=data.FindPropertyRelative(name);EditorGUILayout.LabelField("Held cards → "+label);
        if(p.arraySize!=5){EditorGUILayout.PropertyField(p,true);EditorGUILayout.HelpBox("Exactly five rows required (1–5 held cards).",MessageType.Error);return;}
        for(int i=0;i<5;i++)EditorGUILayout.PropertyField(p.GetArrayElementAtIndex(i),new GUIContent((i+1)+" card(s)"));
    }
}
