using System.Collections.Generic;
using UnityEngine;

public static class EquipmentResolver
{
    public static List<Enemy> Targets(GameManager gm)
    {
        var targets = new List<Enemy>();
        foreach (var e in Object.FindObjectsByType<Enemy>())
            if (e.transform.parent == gm.board && e.Alive) targets.Add(e);
        targets.Sort((a,b) => {
            int order = Mathf.Abs(a.LocalPosition.z-gm.spawning.dangerZ).CompareTo(Mathf.Abs(b.LocalPosition.z-gm.spawning.dangerZ));
            return order != 0 ? order : a.SpawnOrder.CompareTo(b.SpawnOrder);
        });
        return targets;
    }
    public static List<Enemy> ActiveTargets(GameManager gm, SkillPlan plan)
    {
        var all=Targets(gm); var result=new List<Enemy>();
        for(int i=0;i<all.Count;i++) if(plan.allTargets || i<plan.targetCount) result.Add(all[i]);
        return result;
    }
    public static List<Enemy> Nearby(GameManager gm, Vector3 point, float radius, int number, Enemy except = null)
    {
        var targets=Targets(gm);
        targets.RemoveAll(e=>e.Number!=number || e==except || Vector3.ProjectOnPlane(e.transform.position-point,gm.board.up).sqrMagnitude > radius*radius + .00001f);
        targets.Sort((a,b)=> {
            float da=Vector3.ProjectOnPlane(a.transform.position-point,gm.board.up).sqrMagnitude;
            float db=Vector3.ProjectOnPlane(b.transform.position-point,gm.board.up).sqrMagnitude;
            int order=da.CompareTo(db);
            return order!=0?order:a.SpawnOrder.CompareTo(b.SpawnOrder);
        });
        return targets;
    }
    // Only the accepted physical-collision path calls this. Damage/status APIs never call back here.
    public static void BallHit(GameManager gm, Enemy direct, Vector3 point)
    {
        var source=new AttackRecord("Ball enchantments");
        // Each element-number pair is independent. Slot order never changes these groups.
        for(int number=1;number<=6;number++) {
            int arcs=gm.EnabledCount(Card.Equipment.Arc,number);
            if(arcs<=0) continue;
            var targets=Nearby(gm,point,gm.luck.passiveArcRadius,number,direct);
            for(int i=0;i<Mathf.Min(gm.luck.ArcTargets(arcs),targets.Count);i++) {
                var e=targets[i];
                if(gm.Vfx) gm.Vfx.Lightning(point,e.transform.position,false);
                e.TakeDamage(gm.luck.passiveArcDamage,source);
            }
        }
        for(int number=1;number<=6;number++) {
            int sparks=gm.EnabledCount(Card.Equipment.Spark,number);
            if(sparks<=0) continue;
            if(direct && direct.Alive && direct.Number==number) direct.Ignite(false,gm.luck.passiveBurnDuration,source);
            var targets=Nearby(gm,point,gm.luck.passiveSparkRadius,number,direct);
            for(int i=0;i<Mathf.Min(gm.luck.ExtraSparkTargets(sparks),targets.Count);i++) targets[i].Ignite(false,gm.luck.passiveBurnDuration,source);
        }
        for(int number=1;number<=6;number++) {
            int shocks=gm.EnabledCount(Card.Equipment.Shockwave,number);
            if(shocks<=0) continue;
            float radius=gm.luck.ShockRadius(shocks);
            var targets=Nearby(gm,point,radius,number);
            bool affected=false;
            foreach(var e in targets) affected|=e.Stun(gm.luck.passiveStunDuration,false);
            if(affected && gm.Vfx) gm.Vfx.Ring(point,radius,false);
        }
    }
}
