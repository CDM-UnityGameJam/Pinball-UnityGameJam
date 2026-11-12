using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(DifficultyConfig))]
public sealed class DifficultyConfigEditor : Editor
{
    ReorderableList rows;
    double previewTime;
    float distance=14.5f;
    void OnEnable()
    {
        rows=new ReorderableList(serializedObject,serializedObject.FindProperty("stages"),true,true,true,true);
        rows.drawHeaderCallback=r=>{float w=r.width/4;string[] labels={"Time (s)","Spawn","Move","Health"};for(int i=0;i<4;i++)EditorGUI.LabelField(new Rect(r.x+i*w,r.y,w,r.height),labels[i]);};
        rows.elementHeight=EditorGUIUtility.singleLineHeight+5;
        rows.drawElementCallback=(r,i,a,f)=>{
            var p=rows.serializedProperty.GetArrayElementAtIndex(i);r.height=EditorGUIUtility.singleLineHeight;float w=(r.width-18)/4;
            EditorGUI.PropertyField(new Rect(r.x,r.y+2,w,r.height),p.FindPropertyRelative("time"),GUIContent.none);
            EditorGUI.PropertyField(new Rect(r.x+w+6,r.y+2,w,r.height),p.FindPropertyRelative("spawnMultiplier"),GUIContent.none);
            EditorGUI.PropertyField(new Rect(r.x+2*w+12,r.y+2,w,r.height),p.FindPropertyRelative("moveMultiplier"),GUIContent.none);
            EditorGUI.PropertyField(new Rect(r.x+3*w+18,r.y+2,w,r.height),p.FindPropertyRelative("healthMultiplier"),GUIContent.none);
        };
        rows.onAddCallback=l=>{
            var p=l.serializedProperty;int i=p.arraySize;p.InsertArrayElementAtIndex(i);var row=p.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("time").floatValue=i==0?0:p.GetArrayElementAtIndex(i-1).FindPropertyRelative("time").floatValue+30;
            row.FindPropertyRelative("spawnMultiplier").floatValue=i==0?1:p.GetArrayElementAtIndex(i-1).FindPropertyRelative("spawnMultiplier").floatValue;
            row.FindPropertyRelative("moveMultiplier").floatValue=i==0?1:p.GetArrayElementAtIndex(i-1).FindPropertyRelative("moveMultiplier").floatValue;
            row.FindPropertyRelative("healthMultiplier").floatValue=i==0?1:p.GetArrayElementAtIndex(i-1).FindPropertyRelative("healthMultiplier").floatValue;l.index=i;
        };
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        foreach(string name in new[]{"baseSpawnInterval","baseDescentTime","spawnSpeedMultiplier","moveSpeedMultiplier","baseEnemyHealth"})EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        EditorGUILayout.HelpBox("Health is sampled only when a new enemy spawns. Living enemies keep both their current and maximum HP.",MessageType.Info);
        rows.DoLayoutList();
        using(new EditorGUI.DisabledScope(rows.index<0||rows.index>=rows.count))if(GUILayout.Button("Duplicate selected stage")){
            var p=rows.serializedProperty;int i=rows.index;p.InsertArrayElementAtIndex(i);rows.index=i+1;
            // Preserve copied multipliers; place the new row between its neighbours.
            float a=p.GetArrayElementAtIndex(i).FindPropertyRelative("time").floatValue;
            float b=i+2<p.arraySize?p.GetArrayElementAtIndex(i+2).FindPropertyRelative("time").floatValue:a+60;
            p.GetArrayElementAtIndex(i+1).FindPropertyRelative("time").floatValue=(a+b)*.5f;
        }
        serializedObject.ApplyModifiedProperties();
        var config=(DifficultyConfig)target;ShowErrors(config);
        EditorGUILayout.Space();EditorGUILayout.LabelField("Difficulty preview",EditorStyles.boldLabel);
        previewTime=EditorGUILayout.DoubleField("Sample time (seconds)",previewTime);
        distance=EditorGUILayout.FloatField("Spawn-to-danger distance (m)",distance);
        Preview(config,previewTime,distance);
        double end=600;if(config.stages!=null&&config.stages.Length>0&&DifficultyRules.Finite(config.stages[config.stages.Length-1].time))end=System.Math.Max(600,config.stages[config.stages.Length-1].time);
        Curve(config,end,0);Curve(config,end,1);Curve(config,end,2);
    }
    internal static void ShowErrors(DifficultyConfig c){if(!c){EditorGUILayout.HelpBox("Missing configuration. Runtime uses safe defaults.",MessageType.Warning);return;}foreach(var e in c.Errors())EditorGUILayout.HelpBox(e+" Runtime uses the complete default configuration until fixed.",MessageType.Error);}
    internal static void Preview(DifficultyConfig c,double time,double distance)
    {
        var s=c?c.Sample(time,distance):DifficultyRules.Evaluate(time,2,40,1,1,DifficultyRules.Defaults(),distance);
        EditorGUILayout.LabelField("Spawn interval",s.interval.ToString("F3")+" s");
        EditorGUILayout.LabelField("Theoretical attempts / minute",(60/s.interval).ToString("F2"));
        EditorGUILayout.LabelField("Move speed multiplier",s.moveMultiplier.ToString("F3")+"x");
        EditorGUILayout.LabelField("Movement speed",s.speed.ToString("F3")+" m/s");
        EditorGUILayout.LabelField("Fixed-speed traversal time",s.traversalSeconds.ToString("F2")+" s");
        EditorGUILayout.LabelField("New enemy health",s.spawnHealth+" HP · "+s.healthMultiplier.ToString("F3")+"x");
    }
    static void Curve(DifficultyConfig c,double end,int kind)
    {
        EditorGUILayout.LabelField(kind==0?"Spawn speed multiplier":kind==1?"Move speed multiplier":"New enemy health multiplier",EditorStyles.boldLabel);
        var r=GUILayoutUtility.GetRect(80,100);EditorGUI.DrawRect(r,new Color(.12f,.12f,.12f));
        var values=new double[121];double max=1;
        for(int i=0;i<values.Length;i++){var d=c.Sample(end*i/120,1);values[i]=kind==0?d.spawnMultiplier:kind==1?d.moveMultiplier:d.healthMultiplier;max=System.Math.Max(max,values[i]);}
        var points=new Vector3[values.Length];for(int i=0;i<points.Length;i++)points[i]=new Vector3(r.x+r.width*i/120,r.yMax-12-(float)(values[i]/max)*(r.height-25));
        Handles.BeginGUI();Handles.color=kind==0?Color.cyan:kind==1?Color.green:new Color(1,.55f,.4f);Handles.DrawAAPolyLine(2,points);Handles.EndGUI();
        GUI.Label(new Rect(r.x+4,r.y,r.width,20),"0–"+end.ToString("F0")+"s     0–"+max.ToString("F2")+"x");
    }
}

[CustomEditor(typeof(GameManager))]
public sealed class DifficultyGameManagerEditor : Editor
{
    double time=300;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var gm=(GameManager)target;
        if(!gm.cardBalanceConfig)EditorGUILayout.HelpBox("CardBalanceConfig missing. Safe card defaults are active.",MessageType.Warning);
        else if(gm.cardBalanceConfig.settings==null)EditorGUILayout.HelpBox("Card balance settings missing. Safe defaults are active.",MessageType.Error);
        else if(!gm.cardBalanceConfig.settings.Valid())foreach(var error in gm.cardBalanceConfig.settings.Errors())EditorGUILayout.HelpBox(error+" Safe card defaults active.",MessageType.Error);
        EditorGUILayout.Space();EditorGUILayout.LabelField("Difficulty test",EditorStyles.boldLabel);DifficultyConfigEditor.ShowErrors(gm.difficultyConfig);
        time=EditorGUILayout.DoubleField("Preview / test time (s)",time);
        DifficultyConfigEditor.Preview(gm.difficultyConfig,time,gm.spawning.normalSpawnZ-gm.spawning.dangerZ);
        using(new EditorGUI.DisabledScope(!DifficultyRules.Finite(time)||time<0))if(GUILayout.Button(Application.isPlaying?"Use preview time in this run":"Play once at preview time")){
            if(Application.isPlaying)gm.DifficultyDebug.Specify(time);
            else {SessionState.SetFloat("Luck.NextDifficultyTime",(float)time);EditorApplication.isPlaying=true;}
        }
        if(Application.isPlaying){EditorGUILayout.LabelField("Current difficulty sample",gm.DifficultyTime.ToString("F1")+"s · "+gm.DifficultyDebug.Mode);if(GUILayout.Button("Restore automatic difficulty"))gm.DifficultyDebug.Automatic();Repaint();}
    }
}

[InitializeOnLoad]
static class DifficultyPlayPreview
{
    static DifficultyPlayPreview(){EditorApplication.playModeStateChanged+=Changed;}
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode)return;
        float time=SessionState.GetFloat("Luck.NextDifficultyTime",-1);SessionState.EraseFloat("Luck.NextDifficultyTime");
        if(time>=0&&GameManager.Instance)GameManager.Instance.DifficultyDebug.Specify(time);
    }
}
