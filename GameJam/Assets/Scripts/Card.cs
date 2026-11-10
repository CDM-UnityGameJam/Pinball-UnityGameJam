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
    public string Description(LuckTuning t) => equipment == Equipment.Arc ? $"#{number} foes in {t.passiveArcRadius:0.#}m\n{t.passiveArcDamage} dmg; excludes hit foe"
        : equipment == Equipment.Spark ? $"#{number} burn: {t.passiveBurnDps} DPS / {t.passiveBurnDuration:0.#}s\nSame-number spread"
        : $"#{number} stun: {t.passiveStunDuration:0.##}s\nMore #{number} cards: wider area";
    public string SkillName => equipment == Equipment.Arc ? "Thunderstorm" : equipment == Equipment.Spark ? "Inferno" : "Impact";
    public override string ToString() => number + " · " + Name;
}
