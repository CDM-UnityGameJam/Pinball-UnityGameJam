using System;
using System.Collections.Generic;

public enum CastTier { Base, Standard, FullField, Jackpot }

// Pure rules shared by the battle entry point, selection UI and standalone checks.
public static class DiceRules
{
    public static double HitChance(int count) { return count<1 || count>5?0:1-Math.Pow(5.0/6,count); }
    public static bool ValidFaces(IList<int> faces,int count)
    { if(faces==null || faces.Count!=count || count<1 || count>5)return false;foreach(int face in faces)if(face<1 || face>6)return false;return true; }
    public static int Matches(int number,IList<int> faces) { int count=0;foreach(int face in faces)if(face==number)count++;return count; }
    public static CastTier Tier(int number,IList<int> faces)
    {
        if(number<1 || number>6 || faces==null || !ValidFaces(faces,faces.Count))throw new ArgumentException("Invalid dice batch");
        int matches=Matches(number,faces);
        return matches==0?CastTier.Base:faces.Count==1?CastTier.Standard:matches==1?CastTier.FullField:CastTier.Jackpot;
    }
    public static bool ValidSelection<T>(IList<T> cards,IList<T> hand,Func<T,int> number) where T:class
    {
        if(cards==null || hand==null || cards.Count<1 || cards.Count>5)return false;
        int locked=0;
        for(int i=0;i<cards.Count;i++) {
            T card=cards[i];if(card==null)return false;
            int n=number(card);if(n<1 || n>6 || (i>0 && n!=locked))return false;locked=n;
            for(int j=0;j<i;j++)if(ReferenceEquals(card,cards[j]))return false;
            bool present=false;foreach(T held in hand)if(ReferenceEquals(card,held)){present=true;break;}
            if(!present)return false;
        }
        return true;
    }
    public static string Label(CastTier tier) { return tier==CastTier.Base?"Base":tier==CastTier.Standard?"Hit":tier==CastTier.FullField?"Full Field":"Jackpot"; }
}

