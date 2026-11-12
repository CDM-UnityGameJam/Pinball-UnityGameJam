using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RechargeBatchCheck
{
    static double started;
    static bool density;
    public static void RunDensity() { density=true;Run(); }
    public static void Run()
    {
        started=EditorApplication.timeSinceStartup;
        EditorSceneManager.OpenScene("Assets/Scenes/Pinball.unity");
        EditorApplication.update+=WaitForPlay;
        EditorApplication.isPlaying=true;
    }
    static void WaitForPlay()
    {
        if(EditorApplication.timeSinceStartup-started>120) {Finish(false,"Play Mode startup timed out");return;}
        if(!Application.isPlaying || !GameManager.Instance || !Hud.Instance || GameManager.Instance.Lives==0) return;
        EditorApplication.update-=WaitForPlay;
        try {
            if(density) { RechargePhysicsMeasurement.StartComparison(()=>Finish(true,"Real-physics density measurements completed."));return; }
            LuckPrototypeCheck.CheckCards();
            LuckPrototypeCheck.Run();
            var gm=GameManager.Instance;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(GameManager).GetMethod("Launch",flags).Invoke(gm,null);
            gm.ball.isKinematic=true;gm.GetComponent<Spawner>().enabled=false;
            gm.DebugSetDice(new int[]{1});gm.UseCard(0);gm.TickGameplay(7.4f);
            typeof(Hud).GetMethod("RefreshBars",flags).Invoke(Hud.Instance,null);
            var label=(UnityEngine.UI.Text)typeof(Hud).GetField("energyText",flags).GetValue(Hud.Instance);
            if(!label.text.StartsWith("Recharge:") || Math.Abs(gm.RechargeProgress-74)>.001)throw new Exception("English recharge HUD / natural recovery mismatch: "+label.text);
            Debug.Log("RECHARGE HUD: "+label.text);
            Finish(true,"Compilation, gameplay regressions, charge accounting, collision ordering, state gates and English HUD passed.");
        }catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void Finish(bool passed,string detail)
    {
        EditorApplication.update-=WaitForPlay;
        Directory.CreateDirectory("tmp");
        File.WriteAllText("tmp/v05-functional-result.txt",(passed?"PASS":"FAIL")+"\n"+detail);
        Debug.Log("V05 FUNCTIONAL "+(passed?"PASS ":"FAIL ")+detail);
        EditorApplication.Exit(passed?0:1);
    }
}
