using Pinball.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // One hand slot. Press to pick the card up, release over the playfield to play it (HandView decides).
    public sealed class CardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Tooltip("Coloured edge: gold when the card matches the lucky number, blue while dragged.")]
        public Image edge;
        public TMP_Text caption, title, description;

        HandView hand;
        int slot;

        internal void Bind(HandView owner, int index)
        {
            hand = owner;
            slot = index;
        }

        public void OnPointerDown(PointerEventData eventData) => hand?.Press(slot, eventData.position);
        public void OnDrag(PointerEventData eventData) => hand?.Move(eventData.position);
        public void OnPointerUp(PointerEventData eventData) => hand?.Release(eventData.position);

        internal void Show(CardSpec? card, int stack, bool lucky, bool reserved, UiTheme theme)
        {
            edge.color = reserved ? theme.blue : lucky ? theme.gold : theme.panelEdge;
            title.color = reserved ? theme.blue : theme.text;
            if (!card.HasValue)
            {
                title.text = "Empty";
                description.text = "Awaiting refill";
                caption.text = "";
                return;
            }
            var c = card.Value;
            title.text = CardText(c);
            description.text = Describe(c);
            caption.text = $"#{c.number} stack: {stack}";
            caption.color = lucky ? theme.gold : theme.dim;
        }

        public static string CardText(CardSpec card) => $"{card.number} · {card.element.displayName}";

        public static string Describe(CardSpec card)
        {
            string text = (card.element.passiveDescription ?? "").Replace("{number}", card.number.ToString());
            return card.element.passive != null ? card.element.passive.FillTokens(text) : text;
        }
    }
}
