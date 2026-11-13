using Pinball.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // One hand slot. Click to select or unselect the card (HandView decides). With card art the slot shows
    // the element's card face for the number; the text (title, held-effect description) is the fallback
    // for elements without art and for empty slots. Hover and selection drive CardMotion (lift, 3D tilt,
    // pop) and the rim / sheen shaders.
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [Tooltip("Animated rim (CardRim shader): gold when selected, the element's colour otherwise, dim when blocked.")]
        public Image edge;
        public TMP_Text caption, title, description;
        [Tooltip("Card face image (ElementDefinition.cardFaces).")]
        public Image art;
        [Tooltip("Foil sheen over the face (CardSheen shader); uses the face sprite as its mask.")]
        public Image sheen;
        [Tooltip("Plain backing shown for empty slots and elements without art.")]
        public Image fill;
        public CardMotion motion;
        [Tooltip("Card face tint while another number is selected.")]
        public Color blockedTint = new Color(0.35f, 0.35f, 0.4f, 1f);

        HandView hand;
        int slot;
        bool wasSelected;

        internal void Bind(HandView owner, int index)
        {
            hand = owner;
            slot = index;
        }

        public void OnPointerClick(PointerEventData eventData) => hand?.Toggle(slot);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!motion) return;
            motion.Hovered = true;
            UpdatePointer(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (motion) motion.Hovered = false;
        }

        public void OnPointerMove(PointerEventData eventData) => UpdatePointer(eventData);

        void UpdatePointer(PointerEventData eventData)
        {
            if (!motion) return;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local)) return;
            var half = rect.rect.size * 0.5f;
            motion.Pointer = new Vector2(Mathf.Clamp(local.x / half.x, -1f, 1f), Mathf.Clamp(local.y / half.y, -1f, 1f));
        }

        // held: cards of this element in hand. pair: another card with the same number is held.
        internal void Show(HandCard card, int held, bool pair, bool selected, bool blocked, UiTheme theme)
        {
            var face = card != null && card.Element ? card.Element.CardFace(card.Number) : null;
            if (art)
            {
                art.enabled = face;
                art.sprite = face;
                art.color = blocked ? blockedTint : Color.white;
            }
            if (sheen)
            {
                sheen.enabled = face;
                sheen.sprite = face;
            }
            if (fill) fill.enabled = !face;
            SetActive(title, !face);
            SetActive(description, !face);
            if (motion)
            {
                motion.HasCard = card != null;
                motion.Selected = selected;
                motion.Blocked = blocked;
                if (selected != wasSelected) motion.Pop();
            }
            wasSelected = selected;
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

        static void SetActive(Component c, bool active)
        {
            if (c && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
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
