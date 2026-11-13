using Pinball.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // One hand slot. Click to select or unselect the card (HandView decides).
    public sealed class CardView : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Coloured edge: gold when selected, the element's colour otherwise, dim when blocked.")]
        public Image edge;
        public TMP_Text caption, title, description;

        HandView hand;
        int slot;

        internal void Bind(HandView owner, int index)
        {
            hand = owner;
            slot = index;
        }

        public void OnPointerClick(PointerEventData eventData) => hand?.Toggle(slot);

        // held: cards of this element in hand. pair: another card with the same number is held.
        internal void Show(HandCard card, int held, bool pair, bool selected, bool blocked, UiTheme theme)
        {
            if (card == null)
            {
                edge.color = theme.panelEdge;
                title.color = theme.dim;
                title.text = "Empty";
                description.text = "Awaiting charge";
                description.color = theme.dim;
                caption.text = "";
                return;
            }
            var element = card.Element;
            var color = element ? element.color : theme.text;
            edge.color = selected ? theme.gold : blocked ? theme.panelEdge : color;
            title.color = blocked ? theme.dim : color;
            title.text = CardText(card.spec);
            description.text = Describe(card.spec, held);
            description.color = blocked ? theme.dim : theme.text;
            caption.text = (selected ? "SELECTED · " : "") + (element ? element.symbol : "") + (pair ? "  PAIR" : "");
            caption.color = selected ? theme.gold : blocked ? theme.dim : theme.text;
        }

        public static string CardText(CardSpec card) => $"{card.number} · {card.element.displayName}";

        // The element's card text with its module's numbers for `held` cards of the element.
        public static string Describe(CardSpec card, int held)
        {
            string text = card.element.passiveDescription ?? "";
            return card.element.passive != null ? card.element.passive.FillTokens(text, held) : text;
        }
    }
}
