using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // Fakes a 3D card on an overlay canvas: subdivides a simple quad into a grid and projects every vertex
    // through the card's tilt with real perspective (a plain rotation would look flat, overlay canvases are
    // orthographic). Also writes the card's shader drive into UV1: (tilt x, tilt y, glow, sheen), so the
    // rim and sheen shaders animate per card without material instances.
    // Put it on each Graphic under CardMotion.visual. Text is not warped (TMP builds its own mesh).
    [RequireComponent(typeof(Graphic))]
    public sealed class CardTilt : BaseMeshEffect
    {
        [Tooltip("Grid cells per side for a simple quad; more cells = smoother perspective.")]
        [Range(1, 12)] public int subdivisions = 6;
        [Tooltip("Distance from the eye to the card plane, in canvas units. Smaller = stronger perspective.")]
        public float eyeDistance = 520f;

        CardMotion motion;
        Vector4 lastDrive;

        static readonly List<UIVertex> Corners = new List<UIVertex>(4);

        protected override void OnEnable()
        {
            base.OnEnable();
            motion = GetComponentInParent<CardMotion>();
            var canvas = graphic ? graphic.canvas : null;
            if (canvas) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        Vector4 Drive => motion ? new Vector4(motion.Tilt.x, motion.Tilt.y, motion.Glow, motion.Sheen) : Vector4.zero;

        // Called by CardMotion every frame; rebuilds the mesh only when the drive changed.
        public void Refresh()
        {
            var drive = Drive;
            if ((drive - lastDrive).sqrMagnitude < 1e-6f || !graphic) return;
            lastDrive = drive;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var drive = Drive;
            lastDrive = drive;
            var rotation = Quaternion.Euler(drive.x, drive.y, 0f);
            // Vertices are in this graphic's space; tilt about the card visual's pivot.
            var offset = motion && motion.visual && transform.parent == motion.visual ? (Vector2)transform.localPosition : Vector2.zero;
            var uv1 = new Vector4(drive.x / 30f, drive.y / 30f, drive.z, drive.w);

            if (vh.currentVertCount == 4 && subdivisions > 1)
            {
                Corners.Clear();
                for (int i = 0; i < 4; i++) { var v = new UIVertex(); vh.PopulateUIVertex(ref v, i); Corners.Add(v); }
                vh.Clear();
                int n = subdivisions;
                for (int y = 0; y <= n; y++)
                for (int x = 0; x <= n; x++)
                {
                    float u = x / (float)n, w = y / (float)n;
                    // Corner order of a simple Image quad: bottom-left, top-left, top-right, bottom-right.
                    var v = Lerp(Lerp(Corners[0], Corners[3], u), Lerp(Corners[1], Corners[2], u), w);
                    v.position = Project(v.position, rotation, offset);
                    v.uv1 = uv1;
                    vh.AddVert(v);
                }
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * (n + 1) + x;
                    vh.AddTriangle(i, i + n + 1, i + n + 2);
                    vh.AddTriangle(i + n + 2, i + 1, i);
                }
                return;
            }

            var vert = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                vert.position = Project(vert.position, rotation, offset);
                vert.uv1 = uv1;
                vh.SetUIVertex(vert, i);
            }
        }

        Vector3 Project(Vector3 position, Quaternion rotation, Vector2 offset)
        {
            var p = rotation * new Vector3(position.x + offset.x, position.y + offset.y, 0f);
            float s = eyeDistance / Mathf.Max(1f, eyeDistance + p.z);
            return new Vector3(p.x * s - offset.x, p.y * s - offset.y, 0f);
        }

        static UIVertex Lerp(UIVertex a, UIVertex b, float t) => new UIVertex
        {
            position = Vector3.Lerp(a.position, b.position, t),
            normal = a.normal,
            tangent = a.tangent,
            color = Color32.Lerp(a.color, b.color, t),
            uv0 = Vector4.Lerp(a.uv0, b.uv0, t),
            uv1 = a.uv1,
        };
    }
}
