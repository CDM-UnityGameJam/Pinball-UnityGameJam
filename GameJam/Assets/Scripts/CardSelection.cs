using System.Collections.Generic;

public sealed class CardSelection
{
    readonly List<Card> cards=new List<Card>();
    public IList<Card> Cards {get{return cards.AsReadOnly();}}
    public int Count {get{return cards.Count;}}
    public int? Number {get{return cards.Count==0?(int?)null:cards[0].number;}}
    public bool Contains(Card card) {return cards.Contains(card);}
    public bool SelectNumber(int number,IList<Card> hand)
    {
        cards.Clear();foreach(var c in hand)if(c!=null&&c.number==number)cards.Add(c);return cards.Count>0;
    }
    public bool Toggle(Card card)
    {
        if(card==null)return false;
        if(cards.Remove(card))return true;
        if(cards.Count>=5 || (Number.HasValue && card.number!=Number.Value))return false;
        cards.Add(card);return true;
    }
    public bool ToggleGroup(Card card,IList<Card> hand)
    {
        if(card==null)return false;bool clear=Number==card.number;cards.Clear();
        if(!clear)foreach(var c in hand)if(c!=null&&c.number==card.number)cards.Add(c);
        return true;
    }
    public void KeepOnly(IList<Card> hand) {int? number=Number;cards.Clear();if(number.HasValue)foreach(var c in hand)if(c!=null&&c.number==number.Value)cards.Add(c);}
    public void Clear() {cards.Clear();}
}
