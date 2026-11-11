using System;
public static class RechargeAccountRunner {
    public static int Main() {try{Console.WriteLine(RechargeAccountChecks.Run());return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
