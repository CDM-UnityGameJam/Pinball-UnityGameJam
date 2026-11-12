using UnityEditor;
using UnityEngine;
public static class RechargeCheck
{
    [MenuItem("LuckyPinball/Luck Number/Check Recharge Account")]
    public static void CheckAccount()=>Debug.Log(RechargeAccountChecks.Run());
    public static void RunIntegration()=>LuckPrototypeCheck.CheckRecharge();
}
