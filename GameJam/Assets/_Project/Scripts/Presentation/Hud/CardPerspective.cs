using UnityEngine;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // Fake 3D tilt for a card on the overlay canvas: toward the card's top edge, pulls this Graphic in toward a
    // shared vanishing line (the centre of the card's parent, i.e. the hand), so the row reads as lying back on a
    // table and outer cards lean more. Put it on every Image of a card (TMP text is not affected).
    [RequireComponent(typeof(Graphic))]
    public sealed class CardPerspective : BaseMeshEffect
    {
        [Tooltip("Card whose rect defines the tilt; defaults to the nearest CardView.")]
        public RectTransform card;
        [Range(0.5f, 1f), Tooltip("At the card's top edge, distances to the vanishing line shrink to this fraction.")]
        public float topWidth = 0.85f;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var frame = card ? card : GetComponentInParent<CardView>()?.transform as RectTransform;
            if (!frame) return;
            var rect = frame.rect;
            float vanishX = frame.parent ? frame.InverseTransformPoint(frame.parent.position).x : rect.center.x;
            var vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                var p = frame.InverseTransformPoint(transform.TransformPoint(vertex.position));
                float t = Mathf.InverseLerp(rect.yMin, rect.yMax, p.y);
                p.x = vanishX + (p.x - vanishX) * Mathf.Lerp(1f, topWidth, t);
                vertex.position = transform.InverseTransformPoint(frame.TransformPoint(p));
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
