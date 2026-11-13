using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DifficultyDebugPanel : MonoBehaviour
{
    GameManager gm;
    bool visible;
    public bool Visible=>visible;
    Rect window=new Rect(20,20,420,480);
    string spawn="1",move="1",time="300",error="";
    int page,cardElement;
    string cardNumber="1",dice="0";
    Vector2 cardScroll;
    void Awake(){gm=GetComponent<GameManager>();enabled=Application.isEditor||Debug.isDebugBuild;}
    void Update(){if(Keyboard.current!=null&&Keyboard.current.f8Key.wasPressedThisFrame)visible=!visible;}
    void OnGUI(){if(enabled&&visible&&gm)window=GUI.Window(760051,window,Draw,"Difficulty debug · F8 to close");}
    bool Read(string text,out double value)=>double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&DifficultyRules.Finite(value);
    void Draw(int id)
    {
        page=GUILayout.Toolbar(page,new[]{"Difficulty","Cards"});
        if(page==1){DrawCards();GUI.DragWindow(new Rect(0,0,420,22));return;}
        var d=gm.CurrentDifficulty;var stats=gm.DifficultyStats;var mean=stats.MeanRefill(gm.CombatTime);
        GUILayout.Label("Combat time: "+gm.CombatTime.ToString("F1")+"s");
        GUILayout.Label("Difficulty time: "+gm.DifficultyTime.ToString("F1")+"s · "+gm.DifficultyDebug.Mode);
        GUILayout.Label("Spawn interval: "+d.interval.ToString("F3")+"s");
        GUILayout.Label("Move multiplier: "+d.moveMultiplier.ToString("F3")+"x · Speed: "+d.speed.ToString("F3")+"m/s");
        GUILayout.Label("New enemy HP: "+d.spawnHealth+" · Health multiplier: "+d.healthMultiplier.ToString("F3")+"x");
        GUILayout.Label("Living enemies: "+EquipmentResolver.Targets(gm).Count);
        GUILayout.Label("Last 30s · Spawned: "+stats.Spawns(gm.CombatTime)+" · Killed: "+stats.Kills(gm.CombatTime));
        GUILayout.Label("Mean completed refill interval (30s): "+(mean.HasValue?mean.Value.ToString("F2")+"s":"—"));
        GUILayout.Space(8);
        GUILayout.Label("Temporary total multiplier overrides (this run only)");
        GUILayout.BeginHorizontal();GUILayout.Label("Spawn",GUILayout.Width(55));spawn=GUILayout.TextField(spawn);GUILayout.Label("Move",GUILayout.Width(45));move=GUILayout.TextField(move);GUILayout.EndHorizontal();
        if(GUILayout.Button("Apply multipliers")){
            if(Read(spawn,out var s)&&s>0&&Read(move,out var m)&&m>0){gm.DifficultyDebug.SetMultipliers(s,m);error="";}else error="Multipliers must be finite and positive.";
        }
        GUILayout.BeginHorizontal();GUILayout.Label("Sample time (s)",GUILayout.Width(120));time=GUILayout.TextField(time);GUILayout.EndHorizontal();
        if(GUILayout.Button("Use specified time")){if(Read(time,out var t)&&t>=0){gm.DifficultyDebug.Specify(t);error="";}else error="Time must be finite and non-negative.";}
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Freeze difficulty"))gm.DifficultyDebug.Freeze(gm.CombatTime);
        if(GUILayout.Button("Restore automatic"))gm.DifficultyDebug.Automatic();
        GUILayout.EndHorizontal();
        if(GUILayout.Button("Reset temporary multipliers")){gm.DifficultyDebug.ClearMultipliers();spawn=move="1";}
        if(error.Length>0)GUILayout.Label(error);
        GUILayout.Label("Configuration asset is never changed by this panel.");
        GUI.DragWindow(new Rect(0,0,420,22));
    }
    void DrawCards()
    {
        cardScroll=GUILayout.BeginScrollView(cardScroll);
        GUILayout.Label("Config: "+(gm.cardBalanceConfig?gm.cardBalanceConfig.name:"MISSING · safe defaults"));
        if(gm.cardBalanceConfig&&(gm.cardBalanceConfig.settings==null||!gm.cardBalanceConfig.settings.Valid()))GUILayout.Label("INVALID CONFIG · safe defaults active");
        foreach(var c in gm.HandSnapshot)if(c!=null)GUILayout.Label(c.ToString());
        GUILayout.Label("Arc "+gm.HeldCount(Card.Equipment.Arc)+" · Spark "+gm.HeldCount(Card.Equipment.Spark)+" · Shock "+gm.HeldCount(Card.Equipment.Shockwave));
        GUILayout.Label($"Recharge: {gm.RechargeProgress:0.##} / {gm.RechargeRequired:0.##}");
        GUILayout.BeginHorizontal();GUILayout.Label("Card number",GUILayout.Width(100));cardNumber=GUILayout.TextField(cardNumber);GUILayout.EndHorizontal();
        cardElement=GUILayout.Toolbar(cardElement,new[]{"Arc","Spark","Shockwave"});
        if(GUILayout.Button("Add specified card to empty slot")){
            error=int.TryParse(cardNumber,out var n)&&gm.DebugAddCard(n,(Card.Equipment)cardElement)?"Card added.":"Cannot add: use number 1–3, an empty slot, and an active run.";
        }
        GUILayout.Label("Lucky number: "+(gm.LuckyNumber==0?"None":gm.LuckyNumber.ToString()));
        GUILayout.Label("Set lucky number (0 = none, 1–3)");dice=GUILayout.TextField(dice);
        if(GUILayout.Button("Set lucky number for this run"))error=int.TryParse(dice,out var lucky)&&gm.DebugSetLucky(lucky)?"Lucky number updated.":"Use 0–3.";
        if(GUILayout.Button("Fill current recharge through normal refill"))error=gm.DebugFillRecharge()?"Recharge completed.":"Requires normal play and an empty slot.";
        var batch=gm.LastBatch;
        if(batch!=null){GUILayout.Space(6);GUILayout.Label("Last batch: "+DiceRules.Label(batch.Tier));foreach(var c in batch.Cards)GUILayout.Label(c.ToString());foreach(var skill in batch.Skills)GUILayout.Label(skill.Element+": "+skill.Plan.Summary(skill.Element));}
        if(error.Length>0)GUILayout.Label(error);
        GUILayout.EndScrollView();
    }
}
