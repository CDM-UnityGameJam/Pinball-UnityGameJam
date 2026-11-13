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
        [Tooltip("Card illustration (ElementDefinition.cardArt). When it has a sprite, the text is hidden: the art shows element and number.")]
        public Image art;
        [Tooltip("Shown while the card is selected (\"click again to cast\").")]
        public GameObject hint;
        [Tooltip("How far (canvas units) the selected card rises.")]
        public float selectedRaise = 40f;

        HandView hand;
        int slot;
        Vector2 rest;

        internal void Bind(HandView owner, int index)
        {
            hand = owner;
            slot = index;
            rest = ((RectTransform)transform).anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData) => hand?.Press(slot, eventData.position);
        public void OnDrag(PointerEventData eventData) => hand?.Move(eventData.position);
        public void OnPointerUp(PointerEventData eventData) => hand?.Release(eventData.position);

        internal void Show(CardSpec? card, int stack, bool lucky, bool reserved, bool selected, UiTheme theme, int minNumber)
        {
            ((RectTransform)transform).anchoredPosition = rest + (selected ? Vector2.up * selectedRaise : Vector2.zero);
            if (hint) hint.SetActive(selected);
            edge.color = reserved ? theme.blue : lucky ? theme.gold : theme.panelEdge;
            title.color = reserved ? theme.blue : theme.text;
            var sprite = card.HasValue ? card.Value.element.CardArt(card.Value.number, minNumber) : null;
            if (art)
            {
                art.sprite = sprite;
                art.enabled = sprite;
            }
            // The art shows element and number; the stack caption stays (it sits above the card) and the
            // full description is in the drag ghost.
            title.enabled = description.enabled = !(art && sprite);
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
            caption.color = lucky ? theme.gold : theme.text; // sits on the lilac board, so dim text would not read
        }

        public static string CardText(CardSpec card) => $"{card.number} · {card.element.displayName}";

        public static string Describe(CardSpec card)
        {
            string text = (card.element.passiveDescription ?? "").Replace("{number}", card.number.ToString());
            return card.element.passive != null ? card.element.passive.FillTokens(text) : text;
        }
    }
}
