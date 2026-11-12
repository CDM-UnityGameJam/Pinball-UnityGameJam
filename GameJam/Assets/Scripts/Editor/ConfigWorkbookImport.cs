using System;
using UnityEditor;
using UnityEngine;

public sealed class ConfigWorkbookImport : EditorWindow
{
    string path="",error="",status="";
    DifficultyConfig difficulty;
    CardBalanceConfig cards;
    ConfigWorkbookData loaded;
    DateTime validatedTime;
    Vector2 scroll;
    [MenuItem("LuckyPinball/Configuration/Import Excel Workbook...")]
    public static void Open(){GetWindow<ConfigWorkbookImport>("Excel Configuration");}
    void OnEnable()
    {
        minSize=new Vector2(560,410);var gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();
        difficulty=gm?gm.difficultyConfig:AssetDatabase.LoadAssetAtPath<DifficultyConfig>("Assets/Settings/DefaultDifficulty.asset");
        cards=gm?gm.cardBalanceConfig:AssetDatabase.LoadAssetAtPath<CardBalanceConfig>("Assets/Settings/DefaultCardBalance.asset");
    }
    void OnGUI()
    {
        EditorGUILayout.LabelField("Excel Configuration Import",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Edit the Chinese workbook, save it, then validate and apply here. Input cells must contain values, not formulas. Both configurations are validated before either is changed.",MessageType.Info);
        EditorGUILayout.BeginHorizontal();EditorGUI.BeginChangeCheck();path=EditorGUILayout.TextField("Workbook",path);if(EditorGUI.EndChangeCheck())loaded=null;
        if(GUILayout.Button("Browse...",GUILayout.Width(85))){string selected=EditorUtility.OpenFilePanel("Select configuration workbook","","xlsx");if(selected.Length>0){path=selected;loaded=null;error=status="";}}EditorGUILayout.EndHorizontal();
        difficulty=(DifficultyConfig)EditorGUILayout.ObjectField("Difficulty target",difficulty,typeof(DifficultyConfig),false);
        cards=(CardBalanceConfig)EditorGUILayout.ObjectField("Card balance target",cards,typeof(CardBalanceConfig),false);
        if(GUILayout.Button("Validate workbook")){try{var before=System.IO.File.GetLastWriteTimeUtc(path);loaded=ConfigWorkbookReader.Read(path);validatedTime=System.IO.File.GetLastWriteTimeUtc(path);if(before!=validatedTime)throw new InvalidOperationException("Workbook changed during validation. Save it and validate again.");error="";status="Validated. Review the target assets, then apply.";}catch(Exception e){loaded=null;error=e.Message;status="";}}
        if(error.Length>0)EditorGUILayout.HelpBox(error,MessageType.Error);
        if(status.Length>0)EditorGUILayout.HelpBox(status,MessageType.Info);
        if(loaded!=null){
            scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUILayout.LabelField("Difficulty",EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Base spawn interval: {loaded.baseSpawnInterval}s · Descent: {loaded.baseDescentTime}s");
            EditorGUILayout.LabelField($"Global spawn: {loaded.spawnSpeedMultiplier}x · Move: {loaded.moveSpeedMultiplier}x · Stages: {loaded.stages.Length}");
            EditorGUILayout.LabelField($"Base enemy HP: {loaded.baseEnemyHealth} · Applies only to new spawns");
            EditorGUILayout.LabelField("Card balance",EditorStyles.boldLabel);EditorGUILayout.LabelField($"Recharge: {loaded.cards.rechargeBaseCost} + {loaded.cards.rechargeCastCost}/card · Natural: {loaded.cards.rechargePerSecond}/s");
            foreach(Card.Equipment e in Enum.GetValues(typeof(Card.Equipment)))EditorGUILayout.LabelField(e+" Base: "+loaded.cards.Plan(e,CastTier.Base,1).Summary(e));
            EditorGUILayout.EndScrollView();
            bool targets=difficulty&&cards&&AssetDatabase.Contains(difficulty)&&AssetDatabase.Contains(cards);
            using(new EditorGUI.DisabledScope(!targets))if(GUILayout.Button("Apply both configurations (Undo supported)")){
                try{if(System.IO.File.GetLastWriteTimeUtc(path)!=validatedTime){loaded=null;throw new InvalidOperationException("Workbook changed after validation. Validate it again.");}Apply(loaded,difficulty,cards);status="Applied and saved both assets. Existing batches and status snapshots remain intact.";error="";}
                catch(Exception e){error=e.Message;status="";}
            }
            if(!targets)EditorGUILayout.HelpBox("Select two persistent project assets before applying.",MessageType.Warning);
        }
    }
    public static void Apply(ConfigWorkbookData data,DifficultyConfig difficulty,CardBalanceConfig cards)
    {
        if(data==null||!difficulty||!cards)throw new ArgumentException("Workbook data and both target assets are required.");
        var errors=DifficultyRules.Errors(data.baseSpawnInterval,data.baseDescentTime,data.spawnSpeedMultiplier,data.moveSpeedMultiplier,data.stages,data.baseEnemyHealth);
        if(errors.Count>0||data.cards==null||!data.cards.Valid())throw new ArgumentException("The workbook data is invalid; neither asset was modified.");
        string oldDifficulty=JsonUtility.ToJson(difficulty),oldCards=JsonUtility.ToJson(cards);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Import Excel configurations");Undo.RecordObjects(new UnityEngine.Object[]{difficulty,cards},"Import Excel configurations");
        try{
            difficulty.baseSpawnInterval=data.baseSpawnInterval;difficulty.baseDescentTime=data.baseDescentTime;difficulty.spawnSpeedMultiplier=data.spawnSpeedMultiplier;difficulty.moveSpeedMultiplier=data.moveSpeedMultiplier;difficulty.stages=(DifficultyStage[])data.stages.Clone();
            difficulty.baseEnemyHealth=data.baseEnemyHealth;
            cards.settings=JsonUtility.FromJson<LuckTuning>(JsonUtility.ToJson(data.cards));EditorUtility.SetDirty(difficulty);EditorUtility.SetDirty(cards);AssetDatabase.SaveAssetIfDirty(difficulty);AssetDatabase.SaveAssetIfDirty(cards);Undo.CollapseUndoOperations(group);
        }catch{JsonUtility.FromJsonOverwrite(oldDifficulty,difficulty);JsonUtility.FromJsonOverwrite(oldCards,cards);EditorUtility.SetDirty(difficulty);EditorUtility.SetDirty(cards);AssetDatabase.SaveAssetIfDirty(difficulty);AssetDatabase.SaveAssetIfDirty(cards);throw;}
    }
}
