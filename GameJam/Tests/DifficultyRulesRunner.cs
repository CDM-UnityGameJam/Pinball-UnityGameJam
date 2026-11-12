using System;
class DifficultyRulesRunner
{
    static int Main(){try{Console.WriteLine(DifficultyRuleChecks.Run());return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
