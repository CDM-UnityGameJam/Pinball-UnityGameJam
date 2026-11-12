public sealed class Card
{
    public enum Equipment { Arc,Spark,Shockwave }
}
class CardBalanceRulesRunner
{
    static int Main(){try{System.Console.WriteLine(CardBalanceRuleChecks.Run());return 0;}catch(System.Exception e){System.Console.WriteLine(e);return 1;}}
}
