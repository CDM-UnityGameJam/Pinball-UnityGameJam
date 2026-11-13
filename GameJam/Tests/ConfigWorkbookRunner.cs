using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

public sealed class Card {public enum Equipment {Arc,Spark,Shockwave}}
class ConfigWorkbookRunner
{
    static readonly XNamespace N="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static int checkedCases;
    static void Check(bool v,string s){if(!v)throw new Exception(s);}
    static void Near(double a,double b,string s){Check(Math.Abs(a-b)<.00001,s+" "+a+" / "+b);}
    static ConfigWorkbookData Change(byte[] bytes,int sheet,string cell,string value,bool formula=false)
    {
        using(var m=new MemoryStream()){
            m.Write(bytes,0,bytes.Length);m.Position=0;
            using(var zip=new ZipArchive(m,ZipArchiveMode.Update,true)){
                string name="xl/worksheets/sheet"+sheet+".xml";var entry=zip.GetEntry(name);XDocument xml;using(var s=entry.Open())xml=XDocument.Load(s);
                var c=xml.Descendants(N+"c").FirstOrDefault(x=>(string)x.Attribute("r")==cell);
                if(c==null){int r=int.Parse(Regex.Match(cell,@"\d+").Value);var row=xml.Descendants(N+"row").FirstOrDefault(x=>(string)x.Attribute("r")==r.ToString());if(row==null){row=new XElement(N+"row",new XAttribute("r",r));xml.Root.Element(N+"sheetData").Add(row);}c=new XElement(N+"c",new XAttribute("r",cell));row.Add(c);}
                c.RemoveNodes();c.Attribute("t")?.Remove();
                if(value!=null){if(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out _))c.Add(new XElement(N+"v",value));else{c.SetAttributeValue("t","inlineStr");c.Add(new XElement(N+"is",new XElement(N+"t",value)));}if(formula)c.Add(new XElement(N+"f","4+5"));}
                entry.Delete();using(var s=zip.CreateEntry(name).Open())xml.Save(s);
            }
            m.Position=0;return ConfigWorkbookReader.Read(m);
        }
    }
    static void Reject(byte[] b,int sheet,string cell,string value,bool formula=false)
    {bool rejected=false;try{Change(b,sheet,cell,value,formula);}catch(FormatException){rejected=true;}Check(rejected,"Invalid fixture accepted: sheet "+sheet+" "+cell);checkedCases++;}
    static double Source(string text,string key,int spaces=4)=>double.Parse(Regex.Match(text,"(?m)^"+new string(' ',spaces)+key+": ([^\\r\\n]+)").Groups[1].Value,CultureInfo.InvariantCulture);
    static int Main(string[] args)
    {
        try{
            var bytes=File.ReadAllBytes(args[0]);ConfigWorkbookData data;using(var m=new MemoryStream(bytes))data=ConfigWorkbookReader.Read(m);
            if(!args.Contains("--workbook-only")){
            string card=File.ReadAllText("Assets/Settings/DefaultCardBalance.asset"),difficulty=File.ReadAllText("Assets/Settings/DefaultDifficulty.asset");
            Near(data.baseSpawnInterval,Source(difficulty,"baseSpawnInterval",2),"current difficulty interval");Near(data.baseDescentTime,Source(difficulty,"baseDescentTime",2),"descent");Near(data.spawnSpeedMultiplier,Source(difficulty,"spawnSpeedMultiplier",2),"global spawn");Near(data.moveSpeedMultiplier,Source(difficulty,"moveSpeedMultiplier",2),"global move");
            Near(data.baseEnemyHealth,Source(difficulty,"baseEnemyHealth",2),"base enemy HP");
            foreach(var field in typeof(LuckTuning).GetFields(BindingFlags.Public|BindingFlags.Instance))if(field.FieldType==typeof(float)||field.FieldType==typeof(int))Near(Convert.ToDouble(field.GetValue(data.cards)),Source(card,field.Name),field.Name);
            var stages=Regex.Matches(difficulty,@"  - time: ([^\r\n]+)\r?\n    spawnMultiplier: ([^\r\n]+)\r?\n    moveMultiplier: ([^\r\n]+)");Check(stages.Count==data.stages.Length,"stage count");for(int i=0;i<stages.Count;i++){Near(data.stages[i].time,double.Parse(stages[i].Groups[1].Value,CultureInfo.InvariantCulture),"stage time");Near(data.stages[i].spawnMultiplier,double.Parse(stages[i].Groups[2].Value,CultureInfo.InvariantCulture),"stage spawn");Near(data.stages[i].moveMultiplier,double.Parse(stages[i].Groups[3].Value,CultureInfo.InvariantCulture),"stage move");}
            foreach(var name in new[]{"arcTargetCounts","sparkTargetCounts","shockRadii"}){
                string hex=Regex.Match(card,"(?m)^    "+name+": ([0-9a-f]+)").Groups[1].Value;var raw=Enumerable.Range(0,hex.Length/2).Select(i=>Convert.ToByte(hex.Substring(i*2,2),16)).ToArray();var values=(Array)typeof(LuckTuning).GetField(name).GetValue(data.cards);Check(values.Length==5,"five-row table");for(int i=0;i<5;i++)Near(Convert.ToDouble(values.GetValue(i)),name=="shockRadii"?BitConverter.ToSingle(raw,i*4):BitConverter.ToInt32(raw,i*4),name);
            }
            int elementIndex=0;foreach(string name in new[]{"arc","spark","shock"}){
                string block=card.Split(new[]{"    "+name+"Tiers:"},StringSplitOptions.None)[1];block=Regex.Split(block,@"\r?\n    [a-zA-Z]")[0];var rows=Regex.Matches(block,@"- targetMode: (\d+)\r?\n      targetCount: (\d+)\r?\n      waves: (\d+)\r?\n      burnDuration: ([^\r\n]+)\r?\n      stunDuration: ([^\r\n]+)");Check(rows.Count==4,"four tier rows");for(int i=0;i<4;i++){var t=data.cards.Tiers((Card.Equipment)elementIndex)[i];Near((int)t.targetMode,double.Parse(rows[i].Groups[1].Value,CultureInfo.InvariantCulture),"mode");Near(t.targetCount,double.Parse(rows[i].Groups[2].Value,CultureInfo.InvariantCulture),"targets");Near(t.waves,double.Parse(rows[i].Groups[3].Value,CultureInfo.InvariantCulture),"waves");Near(t.burnDuration,double.Parse(rows[i].Groups[4].Value,CultureInfo.InvariantCulture),"burn");Near(t.stunDuration,double.Parse(rows[i].Groups[5].Value,CultureInfo.InvariantCulture),"stun");}elementIndex++;
            }
            }
            for(int tier=0;tier<4;tier++)for(int n=1;n<=5;n++)Check(data.cards.Plan(Card.Equipment.Shockwave,(CastTier)tier,n).waves==1,"Shockwave is fixed at one wave");
            Near(data.cards.scoring.timeInterval,10,"time score interval");Check(data.cards.scoring.timePoints==1&&data.cards.scoring.Reward(50)==10,"score rewards imported");Near(Change(bytes,8,"D9","5").cards.scoring.comboTimeout,5,"combo timeout edit imported");Check(Change(bytes,8,"C15","30").cards.scoring.Reward(50)==30,"combo reward edit imported");Reject(bytes,8,"D7","0");Reject(bytes,8,"D8","1.5");Reject(bytes,8,"B15","10");Reject(bytes,8,"C14","-1");Reject(bytes,8,"C14","2",true);
            for(int n=1;n<=5;n++)Near(data.cards.HeldSparkDps(n),data.cards.passiveBurnDps*data.cards.sparkDpsMultipliers[n-1],"configured fire multiplier import");
            var fireScaling=Change(bytes,3,"F10","7.5");Near(fireScaling.cards.HeldSparkDps(3),data.cards.passiveBurnDps*7.5,"custom fire multiplier import");Near(fireScaling.cards.HeldSparkDps(2),data.cards.HeldSparkDps(2),"other fire row unchanged");
            Reject(bytes,3,"F8","-1");Reject(bytes,3,"F8",null);Reject(bytes,3,"F8","2",true);
            Near(Change(bytes,1,"C7","2.5").baseSpawnInterval,2.5,"difficulty edit imported");Check(Change(bytes,2,"D16","9").cards.passiveArcDamage==9,"card edit imported");var independent=Change(bytes,4,"F11","2");Check(independent.cards.sparkTiers[0].targetCount==2&&independent.cards.arcTiers[0].targetCount==data.cards.arcTiers[0].targetCount,"independent fire scope");
            Reject(bytes,1,"B17","0");Reject(bytes,2,"D16",null);Reject(bytes,2,"A17","passiveArcDamage");Reject(bytes,2,"D16","NaN");Reject(bytes,2,"D16","9",true);Reject(bytes,3,"B12",null);Reject(bytes,4,"A18",null);Reject(bytes,4,"A7","Arc.0");Reject(bytes,4,"G11","3");Reject(bytes,1,"B22","600");Reject(bytes,7,"A1",null);Reject(bytes,2,"D13","-1");
            Reject(bytes,4,"G17","2");
            Reject(bytes,1,"C11","0");Reject(bytes,1,"C11","1.5");Reject(bytes,1,"H16","0");Reject(bytes,1,"H16",null);Reject(bytes,1,"H17","NaN");
            var healthEdit=Change(bytes,1,"H16","2");Check(DifficultyRules.Evaluate(0,healthEdit.baseSpawnInterval,healthEdit.baseDescentTime,healthEdit.spawnSpeedMultiplier,healthEdit.moveSpeedMultiplier,healthEdit.stages,14.5,baseHealth:healthEdit.baseEnemyHealth).spawnHealth==healthEdit.baseEnemyHealth*2,"stage HP edit imported");Check(Change(bytes,1,"C11","60").baseEnemyHealth==60,"base HP edit imported");
            Check(File.ReadAllBytes(args[0]).SequenceEqual(bytes),"Reader modified the workbook");
            Console.WriteLine("EXCEL IMPORT PASSED: "+(args.Contains("--workbook-only")?"workbook validation":"current assets round-trip")+", fixed single Shockwave, valid edits, independent scopes, shared/inline strings, "+checkedCases+" rejection cases and read-only parsing.");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}

