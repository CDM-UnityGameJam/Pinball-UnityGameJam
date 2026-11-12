using System.Collections.Generic;

public sealed class CardSelection
{
    readonly List<Card> cards=new List<Card>();
    public IList<Card> Cards {get{return cards.AsReadOnly();}}
    public int Count {get{return cards.Count;}}
    public int? Number {get{return cards.Count==0?(int?)null:cards[0].number;}}
    public bool Contains(Card card) {return cards.Contains(card);}
    public bool Toggle(Card card)
    {
        if(card==null)return false;
        if(cards.Remove(card))return true;
        if(cards.Count>=5 || (Number.HasValue && card.number!=Number.Value))return false;
        cards.Add(card);return true;
    }
    public void KeepOnly(IList<Card> hand) {cards.RemoveAll(c=>!hand.Contains(c));}
    public void Clear() {cards.Clear();}
}
