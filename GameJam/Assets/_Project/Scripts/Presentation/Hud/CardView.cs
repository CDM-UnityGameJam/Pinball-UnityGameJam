using Pinball.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // One hand slot. Pressing it picks up the card's whole group, dragging moves it (HandView decides). With card art the slot shows
    // the element's card face for the number; the text (title, held-effect description) is the fallback
    // for elements without art and for empty slots. Hover and selection drive CardMotion (lift, 3D tilt,
    // pop) and the rim / sheen shaders. Lucky-number cards get a gold rim and a LUCKY tag.
    public sealed class CardView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [Tooltip("Animated rim (CardRim shader): gold when selected or lucky, the element's colour otherwise.")]
        public Image edge;
        public TMP_Text caption, title, description;
        [Tooltip("Card face image (ElementDefinition.cardFaces).")]
        public Image art;
        [Tooltip("Foil sheen over the face (CardSheen shader); uses the face sprite as its mask.")]
        public Image sheen;
        [Tooltip("Plain backing shown for empty slots and elements without art.")]
        public Image fill;
        public CardMotion motion;
        [Tooltip("Shown over the card's printed number while its group is picked up or selected; pulses.")]
        public RectTransform numberHighlight;
        [Min(0)] public float highlightPulse = 0.12f;
        [Min(0)] public float highlightSpeed = 6f;

        HandView hand;
        int slot;
        bool wasSelected;

        internal void Bind(HandView owner, int index)
        {
            hand = owner;
            slot = index;
        }

        public void OnPointerDown(PointerEventData eventData) => hand?.Press(slot, eventData.position);
        public void OnDrag(PointerEventData eventData) => hand?.Move(eventData.position);
        public void OnPointerUp(PointerEventData eventData) => hand?.Release(eventData.position);

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

        // held / luckyHeld: cards of this element in hand, and of those with the lucky number.
        // dragOffset: canvas offset while the group is dragged; gather / gatherRoll: pull toward the group's centre.
        internal void Show(HandCard card, int held, int luckyHeld, bool lucky, bool selected, Vector2 dragOffset,
            Vector2 gather, float gatherRoll, UiTheme theme)
        {
            if (numberHighlight)
            {
                bool show = selected && card != null;
                if (numberHighlight.gameObject.activeSelf != show) numberHighlight.gameObject.SetActive(show);
                if (show)
                    numberHighlight.localScale = Vector3.one * (1f + highlightPulse * Mathf.Sin(Time.unscaledTime * highlightSpeed));
            }
            var face = card != null && card.Element ? card.Element.CardFace(card.Number) : null;
            if (art)
            {
                art.enabled = face;
                art.sprite = face;
                art.color = Color.white;
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
                motion.Blocked = false;
                motion.DragOffset = dragOffset;
                motion.Gather = gather;
                motion.GatherRoll = gatherRoll;
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
            edge.color = selected || lucky ? theme.gold : color;
            title.color = color;
            title.text = CardText(card.spec);
            description.text = Describe(card.spec, held, luckyHeld);
            description.color = theme.text;
            caption.text = (lucky ? "LUCKY · " : "") + (element ? element.symbol : "");
            caption.color = selected || lucky ? theme.gold : theme.text;
        }

        static void SetActive(Component c, bool active)
        {
            if (c && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }

        public static string CardText(CardSpec card) => $"{card.number} · {card.element.displayName}";

        // The element's card text with its module's numbers for `held` cards of the element (`lucky` of them lucky).
        public static string Describe(CardSpec card, int held, int lucky = 0)
        {
            string text = card.element.passiveDescription ?? "";
            return card.element.passive != null ? card.element.passive.FillTokens(text, held, lucky) : text;
        }
    }
}
