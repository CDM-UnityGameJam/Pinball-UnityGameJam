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
    public static Card Random() => new Card(UnityEngine.Random.Range(1, 7), (Equipment)UnityEngine.Random.Range(0, 3));
    public string Name => equipment == Equipment.Arc ? "Arc" : equipment == Equipment.Spark ? "Spark" : "Shockwave";
    public static Color ColorFor(Equipment type) => type==Equipment.Arc?new Color(.12f,.55f,1):type==Equipment.Spark?new Color(1,.24f,.15f):new Color(.73f,.4f,1);
    public static string IconFor(Equipment type) => type==Equipment.Arc?"ϟ":type==Equipment.Spark?"▲":"◎";
    public string Description(LuckTuning t,int heldCount=1) => equipment==Equipment.Arc?$"Arc: {t.passiveArcDamage} dmg · Hit + nearby\n{t.passiveArcRadius:0.##}m · Lightning foes immune"
        :equipment==Equipment.Spark?$"Burn: {t.HeldSparkDps(heldCount):0.###} DPS · {t.passiveBurnDuration:0.###}s\nDamage appears at each whole point"
        :$"Area stun: {t.passiveStunDuration:0.###}s\nShockwave foes immune";
    public string SkillName => equipment == Equipment.Arc ? "Thunderstorm" : equipment == Equipment.Spark ? "Inferno" : "Impact";
    public override string ToString() => number + " · " + Name;
}
