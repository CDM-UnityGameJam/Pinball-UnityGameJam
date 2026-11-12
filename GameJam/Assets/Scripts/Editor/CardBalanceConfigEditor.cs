using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardBalanceConfig))]
public sealed class CardBalanceConfigEditor : Editor
{
    Card.Equipment element;
    CastTier tier;
    int held=1,copies=1;
    SerializedProperty data;
    void Fields(string title,params string[] names){EditorGUILayout.Space();EditorGUILayout.LabelField(title,EditorStyles.boldLabel);foreach(var name in names)EditorGUILayout.PropertyField(data.FindPropertyRelative(name));}
    public override void OnInspectorGUI()
    {
        var asset=(CardBalanceConfig)target;
        if(asset.settings==null){EditorGUILayout.HelpBox("Settings missing. Safe defaults active.",MessageType.Error);if(GUILayout.Button("Create default settings")){Undo.RecordObject(asset,"Reset card settings");asset.settings=new LuckTuning();EditorUtility.SetDirty(asset);}return;}
        serializedObject.Update();data=serializedObject.FindProperty("settings");
        Fields("Recharge & charge","rechargeBaseCost","rechargeCastCost","rechargePerSecond","rechargeHit");
        EditorGUILayout.HelpBox("Natural recovery is retained. Each qualifying physical ball contact grants the full hit amount, with no rate cap. Burning, skills and kills grant no charge. Full hands stop charging.",MessageType.Info);
        Fields("Ball damage & empowered hits","collisionDamage","empoweredCollisionDamage","empoweredHitsPerCard");
        EditorGUILayout.HelpBox("Empowered collision damage is TOTAL damage, not bonus damage. All balls share the stock.",MessageType.None);
        Fields("Held Arc","passiveArcDamage","passiveArcRadius");HeldTable("arcTargetCounts","Nearby targets (direct hit receives Arc separately)");
        Fields("Held Spark","passiveBurnDps","passiveBurnDuration","passiveSparkRadius");HeldTable("sparkTargetCounts","Nearby targets (direct hit separate)");
        Fields("Held Shockwave (stun only)","passiveStunDuration","passiveStunImmunity");HeldTable("shockRadii","Radius (m)");
        Fields("Shared flame timing","burnTickInterval");
        Fields("Active Arc","activeArcDamage","arcWaveInterval");Tiers("arcTiers",Card.Equipment.Arc);
        Fields("Active Spark","activeBurnDps");Tiers("sparkTiers",Card.Equipment.Spark);
        Fields("Active Shockwave (one impact only)","activeShockDamage");Tiers("shockTiers",Card.Equipment.Shockwave);
        Fields("Same-element stacking (Arc / Spark only)","extraArcWaves","extraBurnPerCard");
        Fields("Dice presentation & reveal","diceRevealDelay");EditorGUILayout.HelpBox("One fair six-sided die per card. Same-number cards only. Reveal delay is captured at submission.",MessageType.None);
        Fields("Existing enemy queue spacing","enemyQueueGap");
        serializedObject.ApplyModifiedProperties();
        var config=(CardBalanceConfig)target;bool valid=config.settings!=null&&config.settings.Valid();
        if(config.settings==null)EditorGUILayout.HelpBox("Settings missing. Safe defaults active.",MessageType.Error);
        else foreach(var error in config.settings.Errors())EditorGUILayout.HelpBox(error+" Safe defaults active until fixed.",MessageType.Error);
        var t=valid?config.settings:new LuckTuning();
        EditorGUILayout.Space();EditorGUILayout.LabelField("Actual calculation preview",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Charge per physical contact",t.rechargeHit.ToString("0.###")+" (no rate cap)");
        EditorGUILayout.LabelField("Held fire damage per tick",LuckTuning.BurnDamage(t.passiveBurnDps,t.burnTickInterval).ToString());
        EditorGUILayout.LabelField("Active fire damage per tick",LuckTuning.BurnDamage(t.activeBurnDps,t.burnTickInterval).ToString());
        EditorGUILayout.HelpBox("Tick damage = floor(DPS × tick interval + 0.5). Battle and preview use this same rule. A small positive DPS can round to zero.",MessageType.Info);
        element=(Card.Equipment)EditorGUILayout.EnumPopup("Element",element);held=EditorGUILayout.IntSlider("Held cards of this element",held,1,5);copies=EditorGUILayout.IntSlider("Cast cards of this element",copies,1,5);tier=(CastTier)EditorGUILayout.EnumPopup("Tier",tier);
        EditorGUILayout.HelpBox(t.HeldSummary(element,held),MessageType.None);
        var p=t.Plan(element,tier,copies);EditorGUILayout.HelpBox(p.Summary(element),MessageType.None);
        EditorGUILayout.LabelField("Total effect duration",p.Duration(element).ToString("0.###")+" s");
        if(element!=Card.Equipment.Spark)EditorGUILayout.LabelField("First-to-last wave span",((p.waves-1)*(double)p.waveInterval).ToString("0.###")+" s");
        EditorGUILayout.LabelField("Damage per eligible target",p.TheoreticalDamage(element).ToString("0.###"));
        EditorGUILayout.LabelField("Additional charge required",(copies*(double)t.rechargeCastCost).ToString("0.###"));
        EditorGUILayout.LabelField("Empowered hits granted",((long)copies*t.empoweredHitsPerCard).ToString());
        EditorGUILayout.HelpBox("Preview assumes a batch containing only the chosen element, a surviving non-immune target, and a fresh burn without overlap. Shock duration includes its stun; wave span is shown separately.",MessageType.Info);
    }
    void HeldTable(string name,string label)
    {
        var p=data.FindPropertyRelative(name);EditorGUILayout.LabelField("Held cards → "+label);
        if(p.arraySize!=5){EditorGUILayout.PropertyField(p,true);EditorGUILayout.HelpBox("Exactly five rows required (1–5 held cards).",MessageType.Error);return;}
        for(int i=0;i<5;i++)EditorGUILayout.PropertyField(p.GetArrayElementAtIndex(i),new GUIContent((i+1)+" card(s)"));
    }
    void Tiers(string name,Card.Equipment e)
    {
        var p=data.FindPropertyRelative(name);
        if(p.arraySize!=4){EditorGUILayout.PropertyField(p,true);EditorGUILayout.HelpBox("Exactly four tiers required.",MessageType.Error);return;}
        string[] conditions={"Base · No matching dice","Hit · One card and one match","Full Field · Multiple cards, exactly one match","Jackpot · At least two matches"};
        for(int i=0;i<4;i++){
            var r=p.GetArrayElementAtIndex(i);EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.LabelField(conditions[i],EditorStyles.boldLabel);
            var mode=r.FindPropertyRelative("targetMode");EditorGUILayout.PropertyField(mode);
            if(mode.enumValueIndex==(int)SkillTargetMode.Limited)EditorGUILayout.PropertyField(r.FindPropertyRelative("targetCount"));
            if(e==Card.Equipment.Shockwave)EditorGUILayout.LabelField("Waves","1 (fixed, regardless of card count)");
            else EditorGUILayout.PropertyField(r.FindPropertyRelative(e==Card.Equipment.Spark?"burnDuration":"waves"));
            if(e==Card.Equipment.Shockwave)EditorGUILayout.PropertyField(r.FindPropertyRelative("stunDuration"),new GUIContent("First-wave stun"));
            EditorGUILayout.EndVertical();
        }
    }
}
