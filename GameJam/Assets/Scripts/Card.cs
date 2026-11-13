using UnityEngine;

// Equipment is aggregated by type; cards no longer own attack timers.
public sealed class Card
{
    public enum Equipment { Arc, Spark, Shockwave }
    public readonly int number;
    public readonly Equipment equipment;
    public Card(int number, Equipment equipment)
    {
        if (number < 1 || number > 6) throw new System.ArgumentOutOfRangeException(nameof(number));
        if ((int)equipment < 0 || (int)equipment > 2) throw new System.ArgumentOutOfRangeException(nameof(equipment));
        this.number = number; this.equipment = equipment;
    }
    public static Card Random() => Random(GameManager.Instance?GameManager.Instance.luck:new LuckTuning());
    public static Card Random(LuckTuning tuning)=>new Card(1+HandRules.Weighted(tuning.draws.numberWeights,UnityEngine.Random.value),(Equipment)HandRules.Weighted(tuning.draws.elementWeights,UnityEngine.Random.value));
    public string Name => equipment == Equipment.Arc ? "Arc" : equipment == Equipment.Spark ? "Spark" : "Shockwave";
    public static Color ColorFor(Equipment type) => type==Equipment.Arc?new Color(.12f,.55f,1):type==Equipment.Spark?new Color(1,.24f,.15f):new Color(1f,.85f,.12f);
    public static string IconFor(Equipment type) => type==Equipment.Arc?"ϟ":type==Equipment.Spark?"▲":"◎";
    public string Description(LuckTuning t,int heldCount=1,int luckyCount=0) => equipment==Equipment.Arc?$"Arc: {t.HeldArcDamage(luckyCount)} dmg · Hit + nearby\n{t.passiveArcRadius:0.##}m · Lightning foes immune"
        :equipment==Equipment.Spark?$"Burn: {t.HeldFireDps(heldCount,luckyCount):0.###} DPS · {t.passiveBurnDuration:0.###}s\nDamage appears at each whole point"
        :$"Area stun: {t.HeldStun(luckyCount):0.###}s\nShockwave foes immune";
    public string SkillName => equipment == Equipment.Arc ? "Thunderstorm" : equipment == Equipment.Spark ? "Inferno" : "Impact";
    public override string ToString() => number + " · " + Name;
}
