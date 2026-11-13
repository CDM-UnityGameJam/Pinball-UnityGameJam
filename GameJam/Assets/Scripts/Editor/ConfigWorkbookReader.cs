using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

// Reads the tagged XLSX template without Excel, COM, macros, or third-party packages.
public sealed class ConfigWorkbookData
{
    public float baseSpawnInterval,baseDescentTime,spawnSpeedMultiplier,moveSpeedMultiplier;
    public int baseEnemyHealth;
    public DifficultyStage[] stages;
    public LuckTuning cards;
}

public static class ConfigWorkbookReader
{
    const string Prefix="luck-number.";
    static readonly XNamespace Main="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    sealed class Cell {public string text;public bool formula;}
    sealed class Sheet
    {
        public string name;
        public readonly Dictionary<string,Cell> cells=new Dictionary<string,Cell>();
        public int lastRow;
        public string Text(string address){return cells.TryGetValue(address,out var c)?c.text:"";}
        public string Input(string address){if(cells.TryGetValue(address,out var c)&&c.formula)throw new FormatException(name+"!"+address+": input formulas are not supported. Enter a numeric value.");return Text(address).Trim();}
        public double Number(string address){string text=Input(address);if(text.Length==0||!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)||!DifficultyRules.Finite(n))throw new FormatException(name+"!"+address+": a finite number is required.");return n;}
        public float Float(string address){double n=Number(address);if(n>float.MaxValue||n< -float.MaxValue)throw new FormatException(name+"!"+address+": number is outside the supported range.");return (float)n;}
        public int Int(string address){double n=Number(address);if(n!=Math.Truncate(n)||n<int.MinValue||n>int.MaxValue)throw new FormatException(name+"!"+address+": a whole number is required.");return (int)n;}
        public void Blank(string address){if(Input(address).Length!=0)throw new FormatException(name+"!"+address+": this field is unused; leave it blank.");}
    }
    public static ConfigWorkbookData Read(string path)
    {
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))return Read(file);
    }
    public static ConfigWorkbookData Read(Stream stream)
    {
        var sheets=new Dictionary<string,Sheet>();
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true)){
            if(zip.Entries.Count>2000||zip.Entries.Sum(e=>e.Length)>50000000)throw new FormatException("Workbook is too large.");
            var shared=new List<string>();var sharedEntry=zip.GetEntry("xl/sharedStrings.xml");
            if(sharedEntry!=null)foreach(var si in Xml(sharedEntry).Root.Elements(Main+"si"))shared.Add(string.Concat(si.Descendants(Main+"t").Select(t=>t.Value)));
            XNamespace rel="http://schemas.openxmlformats.org/package/2006/relationships";
            var relationships=Xml(Entry(zip,"xl/_rels/workbook.xml.rels")).Root.Elements(rel+"Relationship").ToDictionary(e=>(string)e.Attribute("Id"));
            XNamespace docRel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            foreach(var info in Xml(Entry(zip,"xl/workbook.xml")).Descendants(Main+"sheet")){
                string id=(string)info.Attribute(docRel+"id");if(id==null||!relationships.TryGetValue(id,out var link))throw new FormatException("Worksheet relationship missing.");
                if((string)link.Attribute("TargetMode")=="External")throw new FormatException("External worksheet relationships are not supported.");
                string target=(string)link.Attribute("Target");if(string.IsNullOrEmpty(target))throw new FormatException("Worksheet target missing.");
                string entryName=target.StartsWith("/")?target.TrimStart('/'):"xl/"+target;
                if(entryName.Contains("..")||!entryName.StartsWith("xl/worksheets/",StringComparison.Ordinal))continue;
                var sheet=new Sheet{name=(string)info.Attribute("name")};
                foreach(var cell in Xml(Entry(zip,entryName)).Descendants(Main+"c")){
                    string address=(string)cell.Attribute("r");if(string.IsNullOrEmpty(address))throw new FormatException("Cell address missing.");
                    int start=0;while(start<address.Length&&char.IsLetter(address[start]))start++;
                    if(!int.TryParse(address.Substring(start),out var row)||row>10000)throw new FormatException("Unsupported worksheet row.");sheet.lastRow=Math.Max(sheet.lastRow,row);
                    string type=(string)cell.Attribute("t"),value=(string)cell.Element(Main+"v")??"";
                    if(type=="s"){if(!int.TryParse(value,out var index)||index<0||index>=shared.Count)throw new FormatException("Invalid shared string.");value=shared[index];}
                    else if(type=="inlineStr")value=string.Concat(cell.Descendants(Main+"t").Select(t=>t.Value));
                    sheet.cells.Add(address,new Cell{text=value,formula=cell.Element(Main+"f")!=null});
                }
                string tag=sheet.Text("A1");if(!tag.StartsWith(Prefix,StringComparison.Ordinal))continue;sheet.Input("A1");
                if(sheets.ContainsKey(tag))throw new FormatException("Duplicate worksheet tag: "+tag);sheet.name=tag;sheets.Add(tag,sheet);
            }
        }
        Sheet Find(string suffix,int version=1){string tag=Prefix+suffix+".v"+version;if(!sheets.TryGetValue(tag,out var sheet))throw new FormatException("Required worksheet missing: "+tag+". Use the updated workbook template.");return sheet;}
        var difficulty=Find("difficulty",2);var scalar=Find("card-scalars",2);var held=Find("held");var active=Find("active",2);var labels=Find("labels");
        var result=new ConfigWorkbookData{cards=new LuckTuning()};
        var dFields=new HashSet<string>();
        for(int row=7;row<=11;row++){
            string key=difficulty.Input("A"+row);if(!dFields.Add(key))throw new FormatException("Duplicate difficulty key: "+key);float value=difficulty.Float("C"+row);
            switch(key){case "baseSpawnInterval":result.baseSpawnInterval=value;break;case "baseDescentTime":result.baseDescentTime=value;break;case "spawnSpeedMultiplier":result.spawnSpeedMultiplier=value;break;case "moveSpeedMultiplier":result.moveSpeedMultiplier=value;break;case "baseEnemyHealth":result.baseEnemyHealth=difficulty.Int("C"+row);break;default:throw new FormatException("Unknown difficulty key: "+key);}
        }
        var stages=new List<DifficultyStage>();
        for(int row=16;row<=difficulty.lastRow;row++){
            if(difficulty.Input("B"+row).Length==0&&difficulty.Input("C"+row).Length==0&&difficulty.Input("D"+row).Length==0&&difficulty.Input("H"+row).Length==0)continue;
            stages.Add(new DifficultyStage(difficulty.Float("B"+row),difficulty.Float("C"+row),difficulty.Float("D"+row),difficulty.Float("H"+row)));
        }
        result.stages=stages.ToArray();
        var errors=DifficultyRules.Errors(result.baseSpawnInterval,result.baseDescentTime,result.spawnSpeedMultiplier,result.moveSpeedMultiplier,result.stages,result.baseEnemyHealth);
        if(errors.Count>0)throw new FormatException("Difficulty: "+string.Join(" ",errors));
        var retired=new HashSet<string>{"rechargeBaseCost","rechargeCastCost","activeArcDamage","activeShockDamage","activeBurnDps","diceRevealDelay","rechargeCombatBudget","rechargeCombatMinimum","empoweredCollisionDamage","empoweredHitsPerCard"};
        var fields=typeof(LuckTuning).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>(f.FieldType==typeof(float)||f.FieldType==typeof(int))&&!retired.Contains(f.Name)).ToDictionary(f=>f.Name);
        var drawFields=typeof(HandRules).GetFields().Where(f=>f.FieldType==typeof(float)||f.FieldType==typeof(int)).ToDictionary(f=>"draws."+f.Name);
        var seen=new HashSet<string>();
        for(int row=7;row<=scalar.lastRow;row++){
            string key=scalar.Input("A"+row);if(key.Length==0)continue;
            // Retired fields in earlier templates cannot re-enable recharge caps.
            if(retired.Contains(key))continue;
            if(drawFields.TryGetValue(key,out var drawField)){
                if(!seen.Add(key))throw new FormatException("Duplicate draw setting: "+key);
                if(drawField.FieldType==typeof(int))drawField.SetValue(result.cards.draws,scalar.Int("D"+row));else drawField.SetValue(result.cards.draws,scalar.Float("D"+row));continue;
            }
            if(!fields.TryGetValue(key,out var field)||!seen.Add(key))throw new FormatException("Unknown or duplicate card setting: "+key);
            if(field.FieldType==typeof(int))field.SetValue(result.cards,scalar.Int("D"+row));else field.SetValue(result.cards,scalar.Float("D"+row));
        }
        foreach(string key in fields.Keys)if(!seen.Contains(key))throw new FormatException("Card setting missing: "+key);
        foreach(string key in drawFields.Keys)if(!seen.Contains(key))throw new FormatException("Draw setting missing: "+key);
        for(int i=0;i<3;i++){result.cards.draws.numberWeights[i]=scalar.Float("H"+(8+i));result.cards.draws.elementWeights[i]=scalar.Float("I"+(8+i));}
        for(int i=0;i<5;i++)result.cards.draws.refillCosts[i]=scalar.Float("H"+(15+i));
        var counts=new HashSet<int>();
        for(int row=8;row<=held.lastRow;row++){
            if(held.Input("B"+row).Length==0&&held.Input("C"+row).Length==0&&held.Input("D"+row).Length==0&&held.Input("E"+row).Length==0)continue;
            int n=held.Int("B"+row);if(n<1||n>5||!counts.Add(n))throw new FormatException("Held-count table requires unique rows for 1–5 cards.");
            result.cards.arcTargetCounts[n-1]=held.Int("C"+row);result.cards.sparkTargetCounts[n-1]=held.Int("D"+row);result.cards.shockRadii[n-1]=held.Float("E"+row);
            // Older workbooks without this column retain the default linear scaling.
            if(held.Input("F7").Length>0||held.Input("F"+row).Length>0)result.cards.sparkDpsMultipliers[n-1]=held.Float("F"+row);
        }
        if(counts.Count!=5)throw new FormatException("Held-count table must cover 1–5 cards.");
        string Label(string key){for(int row=7;row<=labels.lastRow;row++)if(labels.Input("A"+row)==key)return labels.Input("B"+row);throw new FormatException("Label missing: "+key);}
        string limited=Label("Limited"),all=Label("AllEligible");if(limited.Length==0||all.Length==0||limited==all)throw new FormatException("Target-mode labels are invalid.");
        var tierRows=new HashSet<string>();
        for(int row=7;row<=active.lastRow;row++){
            string key=active.Input("A"+row);if(key.Length==0)continue;
            string[] parts=key.Split('.');if(parts.Length!=2||!Enum.TryParse(parts[0],out Card.Equipment element)||!Enum.IsDefined(typeof(Card.Equipment),element)||!Enum.TryParse(parts[1],out CastTier tier)||!Enum.IsDefined(typeof(CastTier),tier)||key!=element+"."+tier||!tierRows.Add(key))throw new FormatException("Unknown or duplicate active tier: "+key);
            if(tier!=CastTier.Normal&&tier!=CastTier.Lucky)throw new FormatException("Active rows must use Normal or Lucky.");
            var target=(tier==CastTier.Lucky?result.cards.luckySkills:result.cards.normalSkills)[(int)element];string mode=active.Input("E"+row);
            if(mode==limited){target.targetMode=SkillTargetMode.Limited;target.targetCount=active.Int("F"+row);}else if(mode==all){target.targetMode=SkillTargetMode.AllEligible;active.Blank("F"+row);}else throw new FormatException(key+": unknown target mode label.");
            if(element==Card.Equipment.Spark){active.Blank("G"+row);target.burnDuration=active.Float("H"+row);active.Blank("I"+row);target.burnDps=active.Float("J"+row);}
            else{target.damage=active.Int("J"+row);target.waves=active.Int("G"+row);active.Blank("H"+row);if(element==Card.Equipment.Shockwave){if(target.waves!=1)throw new FormatException(key+": Shockwave is fixed at one wave; enter 1.");target.stunDuration=active.Float("I"+row);}else active.Blank("I"+row);}
        }
        if(tierRows.Count!=6)throw new FormatException("Active skills require all six Normal/Lucky element rows.");
        if(sheets.TryGetValue(Prefix+"scoring.v1",out var scoring)){
            result.cards.scoring.timeInterval=scoring.Float("D7");result.cards.scoring.timePoints=scoring.Int("D8");result.cards.scoring.comboTimeout=scoring.Float("D9");
            var rewards=new List<ComboRewardTier>();
            for(int row=14;row<=scoring.lastRow;row++){
                if(scoring.Input("B"+row).Length==0&&scoring.Input("C"+row).Length==0)break;
                rewards.Add(new ComboRewardTier(scoring.Int("B"+row),scoring.Int("C"+row)));
            }
            result.cards.scoring.comboTiers=rewards.ToArray();
        }
        if(!result.cards.Valid())throw new FormatException("Card balance: "+string.Join(" ",result.cards.Errors()));
        return result;
    }
    static ZipArchiveEntry Entry(ZipArchive zip,string path)=>zip.GetEntry(path)??throw new FormatException("Workbook part missing: "+path);
    static XDocument Xml(ZipArchiveEntry entry)
    {
        if(entry.Length>10000000)throw new FormatException("Workbook part too large: "+entry.FullName);
        using(var stream=entry.Open())using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=10000000}))return XDocument.Load(reader);
    }
}
