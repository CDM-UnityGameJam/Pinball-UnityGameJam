using System;
using System.Collections.Generic;

public static class DiceRuleChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Dice rules: "+message);}
    static void Enumerate(int[] faces,int index,int number,int[] counts)
    {
        if(index==faces.Length){counts[(int)DiceRules.Tier(number,faces)]++;return;}
        for(int face=1;face<=6;face++){faces[index]=face;Enumerate(faces,index+1,number,counts);}
    }
    public static string Run()
    {
        for(int n=1;n<=5;n++) {
            var counts=new int[4];Enumerate(new int[n],0,6,counts);
            int total=(int)Math.Pow(6,n),miss=(int)Math.Pow(5,n);
            Check(counts[0]==miss,"base probability");Check(Math.Abs((total-counts[0])/(double)total-DiceRules.HitChance(n))<1e-12,"group hit probability");
            if(n==1)Check(counts[1]==1 && counts[2]==0 && counts[3]==0,"one die can never jackpot or full-field");
            if(n==2)Check(counts[0]==25 && counts[2]==10 && counts[3]==1,"two dice: 25 base / 10 full field / 1 jackpot");
        }
        var a=new Card(6,Card.Equipment.Arc);var b=new Card(6,Card.Equipment.Spark);var c=new Card(2,Card.Equipment.Shockwave);
        var hand=new Card[]{c,a,b};var selected=new CardSelection();
        Check(selected.Toggle(a) && selected.Number==6,"first card locks number");
        Check(!selected.Toggle(c) && selected.Count==1,"different number disabled");
        Check(selected.Toggle(b) && selected.Count==2,"different elements combine");
        Check(selected.Toggle(a) && selected.Number==6,"remaining card preserves lock");
        Check(selected.Toggle(b) && selected.Count==0 && !selected.Number.HasValue,"last deselection unlocks");
        Check(selected.Toggle(c) && selected.Number==2,"another number selectable after unlock");
        Check(DiceRules.ValidSelection(new Card[]{a,b},hand,x=>x.number),"valid same-number batch");
        Check(!DiceRules.ValidSelection(new Card[]{a,c},hand,x=>x.number),"mixed numbers rejected");
        Check(!DiceRules.ValidSelection(new Card[]{a,a},hand,x=>x.number),"duplicate instance rejected");
        Check(!DiceRules.ValidSelection(new Card[]{new Card(6,Card.Equipment.Arc)},hand,x=>x.number),"same-looking foreign instance rejected");
        Check(!DiceRules.ValidSelection(new Card[]{},hand,x=>x.number),"empty batch rejected");
        Check(!DiceRules.ValidSelection(new Card[]{null},hand,x=>x.number),"null rejected");
        Check(!DiceRules.ValidFaces(new int[]{6,0},2) && !DiceRules.ValidFaces(new int[]{6,7},2),"invalid dice rejected");
        var meter=new RechargeAccount();meter.PlayBatch(true,3,100,50);Check(meter.Required==250,"batch cost is applied once by actual card count");
        meter.EarnNatural(7);meter.PlayBatch(false,2,100,50);Check(meter.Required==350 && meter.Progress==7,"another batch preserves progress and adds 100");
        return "Dice rules PASSED: exhaustive 1-5 dice outcomes, 25/10/1 two-dice tiers, instance validation, selection locking and batch costs.";
    }
}
