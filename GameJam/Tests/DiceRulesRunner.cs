#if DICE_STANDALONE
public sealed class Card
{
    public enum Equipment {Arc,Spark,Shockwave}
    public readonly int number;public readonly Equipment equipment;
    public Card(int n,Equipment e){number=n;equipment=e;}
}
public static class DiceRulesRunner
{
    public static int Main(){try{System.Console.WriteLine(DiceRuleChecks.Run());System.Console.WriteLine(RechargeAccountChecks.Run());return 0;}catch(System.Exception e){System.Console.WriteLine(e);return 1;}}
}
#endif
