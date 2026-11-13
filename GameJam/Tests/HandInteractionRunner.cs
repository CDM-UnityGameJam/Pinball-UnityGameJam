using System;
public sealed class Card
{
    public readonly int number;public Card(int n){number=n;}
}
class HandInteractionRunner
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main()
    {
        var keys=new GroupKeyInput();
        Check(keys.Press(1,null,0)==GroupKeyAction.Select,"first press selects, never casts");
        Check(keys.Advance(10,1)==GroupKeyAction.None,"selection has no timeout until another tap");
        Check(keys.Press(1,1,10)==GroupKeyAction.AwaitSecondTap,"one selected-number tap waits for double-tap");
        Check(keys.Advance(10.29,1)==GroupKeyAction.None,"single cancel waits through double-tap window");
        Check(keys.Advance(10.31,1)==GroupKeyAction.Cancel,"single tap cancels");
        Check(keys.Advance(11,1)==GroupKeyAction.None,"cancel happens once");
        Check(keys.Press(2,null,12)==GroupKeyAction.Select,"select another number");
        Check(keys.Press(2,2,13)==GroupKeyAction.AwaitSecondTap,"double-tap starts after selection");
        Check(keys.Press(2,2,13.2)==GroupKeyAction.Cast,"second rapid tap casts once");
        Check(keys.Advance(14,2)==GroupKeyAction.None,"cast clears pending cancellation");
        keys.Press(1,1,15);Check(keys.Press(3,1,15.1)==GroupKeyAction.Select,"different key switches group without casting");Check(keys.Advance(16,3)==GroupKeyAction.None,"switch clears old cancellation");
        keys.Press(3,3,17);keys.Reset();Check(keys.Advance(18,3)==GroupKeyAction.None,"pause/focus/drag reset pending tap");
        Check(keys.Press(0,3,19)==GroupKeyAction.None&&keys.Press(4,3,19)==GroupKeyAction.None,"only 1–3 keys valid");
        var a=new Card(1);var b=new Card(1);var c=new Card(2);var d=new Card(3);var hand=new[]{d,b,null,c,a};
        HandOrder.Sort(hand,x=>x.number);Check(ReferenceEquals(hand[0],b)&&ReferenceEquals(hand[1],a)&&ReferenceEquals(hand[2],c)&&ReferenceEquals(hand[3],d)&&hand[4]==null,"sort keeps number order, stable identities and empty slots at end");
        var selected=new CardSelection();Check(selected.SelectNumber(1,hand)&&selected.Count==2,"drag selects whole group");selected.KeepOnly(new[]{a,b,new Card(1),c});Check(selected.Count==3,"new same-number refill joins dragged/selected group");Check(!selected.SelectNumber(3,new[]{a,b,c}),"missing group cannot be selected");
        Console.WriteLine("HAND INTERACTION PASSED: first-key selection, deferred single cancel, double-tap cast, switching, resets, stable numeric sorting and whole-group refresh.");
    }
}
