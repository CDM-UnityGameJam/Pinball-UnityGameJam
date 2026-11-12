using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySnapshot
{
    public readonly Enemy Enemy;
    public readonly Card.Equipment Element;
    public readonly Vector3 Position;
    public readonly long Order;
    public EnemySnapshot(Enemy enemy) {Enemy=enemy;Element=enemy.Element;Position=enemy.LocalPosition;Order=enemy.SpawnOrder;}
}

public sealed class DiceBatch : AttackRecord
{
    readonly Card[] cards;
    readonly int[] dice;
    readonly List<SkillCast> skills=new List<SkillCast>();
    readonly HashSet<Enemy> covered=new HashSet<Enemy>();
    public IList<Card> Cards => Array.AsReadOnly(cards);
    public IList<int> Dice => Array.AsReadOnly(dice);
    public IList<SkillCast> Skills => skills.AsReadOnly();
    public readonly int Number,Matches;
    public readonly CastTier Tier;
    public readonly double RevealAt;
    public bool Revealed { get; internal set; }
    public int CoverageCount => covered.Count;
    public DiceBatch(Card[] cards,int[] dice,double revealAt,IList<EnemySnapshot> snapshot,LuckTuning tuning) : base("Batch #"+cards[0].number)
    {
        this.cards=(Card[])cards.Clone();this.dice=(int[])dice.Clone();Number=cards[0].number;
        Tier=DiceRules.Tier(Number,dice);Matches=DiceRules.Matches(Number,dice);RevealAt=revealAt;
        for(int type=0;type<3;type++) {
            int count=0;foreach(var c in cards)if((int)c.equipment==type)count++;
            if(count==0)continue;
            var element=(Card.Equipment)type;var plan=tuning.Plan(element,Tier,count);var targets=new List<Enemy>();
            foreach(var target in snapshot) {
                if(target.Element==element)continue;
                if(!plan.allTargets && targets.Count>=plan.targetCount)break;
                targets.Add(target.Enemy);
            }
            skills.Add(new SkillCast(this,element,count,plan,targets));
        }
    }
    internal void Cover(Enemy enemy) => covered.Add(enemy);
}
