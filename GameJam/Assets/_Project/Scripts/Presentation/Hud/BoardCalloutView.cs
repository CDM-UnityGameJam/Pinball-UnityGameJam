using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // A label pinned to a point on the board (e.g. "ENEMY SPAWN AREA", "DANGER LINE").
    public sealed class BoardCalloutView : HudPresenter
    {
        [Tooltip("Board anchor to follow.")] public Transform target;
        public Vector3 worldOffset;
        RectTransform rect;

        void LateUpdate()
        {
            if (!Ready || !target) return;
            rect ??= (RectTransform)transform;
            rect.anchoredPosition = Hud.WorldToCanvas(target.position + worldOffset);
        }
    }
}
