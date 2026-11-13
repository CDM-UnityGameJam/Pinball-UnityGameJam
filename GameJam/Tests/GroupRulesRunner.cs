using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
public sealed class Card
{
    public enum Equipment {Arc,Spark,Shockwave}
    public int number;public Equipment equipment;
    public Card(int number,Equipment equipment){this.number=number;this.equipment=equipment;}
}
class GroupRulesRunner
{
    static void Check(bool v,string s){if(!v)throw new Exception(s);}
    static void Near(double a,double b,string s){Check(Math.Abs(a-b)<1e-5,s+" "+a+" / "+b);}
    static void Main(string[] args)
    {
        var t=new LuckTuning();Check(t.Valid(),"safe defaults valid");Near(t.draws.LuckyChance(1),.2,"single draw chance");Near(t.draws.LuckyChance(5),.67232,"five draw chance");
        Check(typeof(LuckTuning).GetField("empoweredCollisionDamage")==null&&typeof(LuckTuning).GetField("empoweredHitsPerCard")==null,"removed collision boost configuration has no runtime fields");
        var rand=new Random(123456);int[] numbers={3,2,2};t.draws.luckyProbability=1;int[] counts=new int[4];for(int i=0;i<90000;i++)counts[t.draws.PickLucky(numbers,rand.NextDouble)]++;Check(Math.Abs(counts[3]/90000.0-1.0/3)<.01,"duplicate-number candidates are not deduplicated");
        t.draws.luckyProbability=0;Check(t.draws.PickLucky(numbers,()=>0)==0,"all failed clears lucky number");t.draws.luckyProbability=.2f;int success=0;for(int i=0;i<100000;i++)if(t.draws.PickLucky(new[]{1,1,2,3,3},rand.NextDouble)>0)success++;Near(Math.Round(success/100000.0,2),.67,"independent per-new-card probability");
        Check(HandRules.Weighted(new float[]{0,1,0},0)==1&&HandRules.Weighted(new float[]{0,1,0},1)==1,"zero weight exclusion incl endpoint");
        var a=new Card(1,Card.Equipment.Arc);var b=new Card(1,Card.Equipment.Spark);var c=new Card(2,Card.Equipment.Shockwave);var hand=new[]{a,b,c};
        Check(!DiceRules.ValidGroup(new[]{a},hand,x=>x.number),"subset rejected");Check(DiceRules.ValidGroup(new[]{a,b},hand,x=>x.number),"whole group accepted");Check(!DiceRules.ValidGroup(new[]{a,c},hand,x=>x.number),"mixed numbers rejected");Check(!DiceRules.ValidGroup(new[]{a,a},hand,x=>x.number),"duplicate instances rejected");
        var selection=new CardSelection();selection.ToggleGroup(a,hand);Check(selection.Count==2,"click selects whole group");selection.ToggleGroup(c,hand);Check(selection.Count==1&&selection.Number==2,"click other number switches");selection.ToggleGroup(c,hand);Check(selection.Count==0,"same number cancels");selection.ToggleGroup(a,hand);selection.KeepOnly(new[]{a,b,new Card(1,Card.Equipment.Arc),c});Check(selection.Count==3,"new draw refreshes selected group");
        var recharge=new RechargeAccount();recharge.SetRequirement(t.draws.RefillCost(2));recharge.EarnNatural(80);recharge.SetRequirement(t.draws.RefillCost(3));Near(recharge.Progress,80,"more casts retain charge");Near(recharge.Required,200,"cost replaced by empty slots");recharge.EarnCombat(0,500);Check(recharge.Ready,"overshoot ready");recharge.FullHand();Near(recharge.Progress,0,"full refill discards overshoot");
        Near(t.HeldArcDamage(2),8,"lucky Arc bonus");Near(t.HeldFireDps(3,2),14,"fire multiplier then lucky bonus");Near(t.HeldStun(2),.85,"lucky stun bonus");
        var plan=t.Plan(Card.Equipment.Spark,CastTier.Lucky,2);Near(plan.burnDuration,8,"same-element fire stacking");t.luckySkills[1].burnDps=99;Near(plan.burnDps,6,"submitted plan snapshot");
        for(int n=1;n<=5;n++)Check(t.Plan(Card.Equipment.Shockwave,CastTier.Normal,n).waves==1&&t.Plan(Card.Equipment.Shockwave,CastTier.Lucky,n).waves==1,"Shockwave fixed at one impact");
        t.normalSkills[1].targetCount=2;Check(t.Plan(Card.Equipment.Spark,CastTier.Normal,1).targetCount==2&&t.Plan(Card.Equipment.Arc,CastTier.Normal,1).targetCount==4,"independent scopes");
        t.draws.numberWeights=new float[]{0,0,0};Check(!t.Valid(),"zero weight sum invalid");t=new LuckTuning();t.draws.refillCosts[0]=float.NaN;Check(!t.Valid(),"invalid cost rejected");t=new LuckTuning();t.draws.handSize=6;Check(!t.Valid(),"hand size >5 invalid");
        Console.WriteLine("GROUP RULES PASSED: weighted generation, candidate probability / duplicate weighting, complete-group validation / selection refresh, refill progress / overflow, lucky enchantments, immediate tiers / snapshots / stacking and validation.");
        if(args.Length>0){var bytes=File.ReadAllBytes(args[0]);var data=ConfigWorkbookReader.Read(args[0]);Check(data.cards.Valid(),"migrated workbook valid");Check(data.cards.draws.handSize==5,"workbook hand size");Check(data.cards.Plan(Card.Equipment.Arc,CastTier.Normal,1).targetCount==6,"normal migrated from old Hit");Check(data.cards.Plan(Card.Equipment.Arc,CastTier.Lucky,1).allTargets,"lucky migrated from old FullField");
            Reject(bytes,2,"D35","6");Reject(bytes,2,"D36","1.1");Reject(bytes,2,"H8","-1");Reject(bytes,2,"H19","NaN");Reject(bytes,4,"J7","2.5");Reject(bytes,4,"F7",null);Reject(bytes,4,"G11","2");Reject(bytes,4,"G7","0");Reject(bytes,4,"J9","1",true);
            var changed=Change(bytes,4,"J9","0.25");Near(changed.cards.Plan(Card.Equipment.Spark,CastTier.Normal,1).burnDps,.25,"independent fire DPS import");Near(changed.cards.Plan(Card.Equipment.Arc,CastTier.Normal,1).damage,data.cards.normalSkills[0].damage,"changing fire leaves Arc unchanged");
            Near(Change(bytes,2,"H16","175").cards.draws.RefillCost(2),175,"independent missing-slot cost import");
            Check(File.ReadAllBytes(args[0]).SequenceEqual(bytes),"validation never modifies workbook");Console.WriteLine("NEW EXCEL IMPORT PASSED: six independent skill rows, draw settings, weights, refill costs, valid edits and nine invalid-input rejection cases.");if(args.Contains("--migrate"))WriteAsset(data.cards);}
    }
    static ConfigWorkbookData Change(byte[] bytes,int sheet,string address,string value,bool formula=false)
    {
        XNamespace n="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using(var memory=new MemoryStream()){
            memory.Write(bytes,0,bytes.Length);memory.Position=0;
            using(var zip=new ZipArchive(memory,ZipArchiveMode.Update,true)){
                string name="xl/worksheets/sheet"+sheet+".xml";var entry=zip.GetEntry(name);XDocument xml;using(var stream=entry.Open())xml=XDocument.Load(stream);
                var cell=xml.Descendants(n+"c").First(x=>(string)x.Attribute("r")==address);cell.RemoveNodes();cell.Attribute("t")?.Remove();
                if(value!=null){if(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out _))cell.Add(new XElement(n+"v",value));else{cell.SetAttributeValue("t","inlineStr");cell.Add(new XElement(n+"is",new XElement(n+"t",value)));}if(formula)cell.Add(new XElement(n+"f","1"));}
                entry.Delete();using(var stream=zip.CreateEntry(name).Open())xml.Save(stream);
            }
            memory.Position=0;return ConfigWorkbookReader.Read(memory);
        }
    }
    static void Reject(byte[] bytes,int sheet,string address,string value,bool formula=false){bool rejected=false;try{Change(bytes,sheet,address,value,formula);}catch(FormatException){rejected=true;}Check(rejected,"invalid input accepted "+address);}
    static void WriteAsset(LuckTuning tuning)
    {
        const string path="Assets/Settings/DefaultCardBalance.asset";string old=File.ReadAllText(path);int offset=old.IndexOf("  settings:",StringComparison.Ordinal);Check(offset>0,"asset settings anchor");
        var retired=new HashSet<string>{"rechargeBaseCost","rechargeCastCost","activeArcDamage","activeShockDamage","activeBurnDps","diceRevealDelay","arcTiers","sparkTiers","shockTiers"};
        var lines=new List<string>();foreach(var f in typeof(LuckTuning).GetFields(BindingFlags.Public|BindingFlags.Instance))if(!retired.Contains(f.Name))Emit(lines,f.Name,f.GetValue(tuning),4);
        File.WriteAllText(path,old.Substring(0,offset)+"  settings:\n"+string.Join("\n",lines)+"\n");Console.WriteLine("DefaultCardBalance migrated from original workbook values.");
    }
    static void Emit(List<string> lines,string name,object value,int indent)
    {
        string pad=new string(' ',indent);Type type=value.GetType();
        if(type.IsEnum||type.IsPrimitive){lines.Add(pad+name+": "+(type.IsEnum?Convert.ToInt32(value).ToString():Convert.ToString(value,CultureInfo.InvariantCulture)));return;}
        var arr=value as Array;
        if(value is int[]){var bytes=((int[])value).SelectMany(BitConverter.GetBytes).ToArray();lines.Add(pad+name+": "+BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant()+"i");return;}
        lines.Add(pad+name+":");
        if(arr!=null){foreach(var entry in arr){if(entry.GetType().IsPrimitive)lines.Add(pad+"- "+Convert.ToString(entry,CultureInfo.InvariantCulture));else{bool first=true;foreach(var f in entry.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance)){var sub=new List<string>();Emit(sub,f.Name,f.GetValue(entry),indent+2);if(first){sub[0]=pad+"- "+sub[0].TrimStart();first=false;}lines.AddRange(sub);}}}return;}
        foreach(var f in type.GetFields(BindingFlags.Public|BindingFlags.Instance))Emit(lines,f.Name,f.GetValue(value),indent+2);
    }
}
