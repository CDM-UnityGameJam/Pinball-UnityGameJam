using System.Collections.Generic;
using UnityEngine;

public static class EnemyMovement
{
    public struct Move { public Enemy enemy; public Vector3 local; }
    // Front-to-back planning, using already planned positions of all earlier rows.
    public static List<Move> Plan(GameManager gm,float delta)
    {
        var result=new List<Move>();
        if(!gm.IsPlaying) return result;
        foreach(var e in EquipmentResolver.Targets(gm)) {
            var current=e.LocalPosition;
            var next=current;
            if(!e.IsStunned) next.z-=(gm.spawning.normalSpawnZ-gm.spawning.dangerZ)/Mathf.Max(.01f,gm.enemyDescendTime)*delta;
            foreach(var front in result) {
                float dx=Mathf.Abs(current.x-front.local.x);
                float spacing=e.Radius+front.enemy.Radius+gm.luck.enemyQueueGap;
                if(dx>=spacing) continue;
                float safeZ=front.local.z+Mathf.Sqrt(Mathf.Max(0,spacing*spacing-dx*dx));
                // Never push an enemy backwards to fix pre-existing editor overlaps.
                next.z=Mathf.Min(current.z,Mathf.Max(next.z,safeZ));
            }
            result.Add(new Move { enemy=e,local=next });
        }
        return result;
    }
    public static void Step(GameManager gm,float delta)
    {
        foreach(var move in Plan(gm,delta)) {
            if(!gm.IsPlaying) break;
            if(move.local.z<=gm.spawning.dangerZ) move.enemy.Breach();
            else move.enemy.MoveToLocal(move.local);
        }
    }
}
