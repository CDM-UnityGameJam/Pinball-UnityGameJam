using System;

public enum GroupKeyAction { None, Select, AwaitSecondTap, Cast, Cancel }

// Selection is owned by the HUD. A single tap on an already selected number
// must wait briefly so a second tap can turn it into a double-tap cast.
public sealed class GroupKeyInput
{
    public readonly double DoubleTapSeconds;
    int pending;
    double deadline;
    public GroupKeyInput(double doubleTapSeconds=.3){DoubleTapSeconds=doubleTapSeconds;}
    public void Reset(){pending=0;deadline=0;}
    public GroupKeyAction Advance(double now,int? selected)
    {
        if(pending==0)return GroupKeyAction.None;
        if(selected!=pending){Reset();return GroupKeyAction.None;}
        if(now<=deadline)return GroupKeyAction.None;
        Reset();return GroupKeyAction.Cancel;
    }
    public GroupKeyAction Press(int number,int? selected,double now)
    {
        if(number<1||number>3)return GroupKeyAction.None;
        if(selected!=number){Reset();return GroupKeyAction.Select;}
        if(pending==number&&now<=deadline){Reset();return GroupKeyAction.Cast;}
        pending=number;deadline=now+DoubleTapSeconds;return GroupKeyAction.AwaitSecondTap;
    }
}

public static class HandOrder
{
    // Stable order within one number; card identities, elements and effects are untouched.
    public static void Sort<T>(T[] hand,Func<T,int> number) where T:class
    {
        for(int i=1;i<hand.Length;i++){
            var item=hand[i];int value=item==null?int.MaxValue:number(item),j=i-1;
            while(j>=0&&(hand[j]==null?int.MaxValue:number(hand[j]))>value){hand[j+1]=hand[j];j--;}
            hand[j+1]=item;
        }
    }
}
