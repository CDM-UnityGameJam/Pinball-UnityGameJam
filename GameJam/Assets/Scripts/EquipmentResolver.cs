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
    public static List<EnemySnapshot> Snapshot(GameManager gm)
    {var result=new List<EnemySnapshot>();foreach(var e in Targets(gm))result.Add(new EnemySnapshot(e));return result;}
    public static List<Enemy> Nearby(GameManager gm, Vector3 point, float radius, Card.Equipment element, Enemy except = null)
    {
        var targets=Targets(gm);
        targets.RemoveAll(e=>!e.CanReceive(element) || e==except || Vector3.ProjectOnPlane(e.transform.position-point,gm.board.up).sqrMagnitude > radius*radius + .00001f);
        targets.Sort((a,b)=> {
            float da=Vector3.ProjectOnPlane(a.transform.position-point,gm.board.up).sqrMagnitude;
            float db=Vector3.ProjectOnPlane(b.transform.position-point,gm.board.up).sqrMagnitude;
            int order=da.CompareTo(db);
            return order!=0?order:a.SpawnOrder.CompareTo(b.SpawnOrder);
        });
        return targets;
    }
    // Only the accepted physical-collision path calls this. Damage/status APIs never call back here.
    public static void BallHit(GameManager gm, Enemy direct, Vector3 point, AttackRecord source)
    {
        int arcs=gm.HeldCount(Card.Equipment.Arc);
        int arcDamage=gm.luck.HeldArcDamage(gm.LuckyHeldCount(Card.Equipment.Arc));
        if(arcs>0) {
            // The direct hit receives Arc damage separately and does not consume a nearby-target slot.
            if(direct && direct.CanReceive(Card.Equipment.Arc)){
                if(gm.Vfx)gm.Vfx.Lightning(point,direct.transform.position,false);
                direct.TakeElementDamage(arcDamage,Card.Equipment.Arc,source);
            }
            var targets=Nearby(gm,point,gm.luck.passiveArcRadius,Card.Equipment.Arc,direct);
            for(int i=0;i<Mathf.Min(gm.luck.ArcTargets(arcs),targets.Count);i++) {
                var e=targets[i];if(gm.Vfx)gm.Vfx.Lightning(point,e.transform.position,false);
                e.TakeElementDamage(arcDamage,Card.Equipment.Arc,source);
            }
        }
        int sparks=gm.HeldCount(Card.Equipment.Spark);
        if(sparks>0) {
            float dps=gm.luck.HeldFireDps(sparks,gm.LuckyHeldCount(Card.Equipment.Spark));
            if(direct && direct.CanReceive(Card.Equipment.Spark))direct.Ignite(false,gm.luck.passiveBurnDuration,source,dps,gm.luck.burnTickInterval);
            var targets=Nearby(gm,point,gm.luck.passiveSparkRadius,Card.Equipment.Spark,direct);
            for(int i=0;i<Mathf.Min(gm.luck.ExtraSparkTargets(sparks),targets.Count);i++)targets[i].Ignite(false,gm.luck.passiveBurnDuration,source,dps,gm.luck.burnTickInterval);
        }
        int shocks=gm.HeldCount(Card.Equipment.Shockwave);
        if(shocks>0) {
            float radius=gm.luck.ShockRadius(shocks);bool affected=false;
            foreach(var e in Nearby(gm,point,radius,Card.Equipment.Shockwave))affected|=e.Stun(gm.luck.HeldStun(gm.LuckyHeldCount(Card.Equipment.Shockwave)),false);
            if(affected && gm.Vfx)gm.Vfx.Ring(point,radius,false);
        }
    }
}
