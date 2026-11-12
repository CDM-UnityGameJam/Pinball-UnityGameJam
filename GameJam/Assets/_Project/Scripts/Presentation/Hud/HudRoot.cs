using Pinball.Gameplay;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Presentation
{
    // Root of Hud.prefab (on the Canvas). Hands the GameContext to every presenter below it and converts
    // between world, screen and canvas space. Canvas space here = scaled pixels from the bottom-left corner.
    [RequireComponent(typeof(Canvas))]
    public sealed class HudRoot : MonoBehaviour
    {
        [Required, SceneObjectsOnly] public GameContext context;
        [Required, AssetsOnly] public UiTheme theme;
        [Tooltip("Camera that renders the board; defaults to the main camera.")]
        public Camera worldCamera;

        Canvas canvas;
        RectTransform rect;

        public Camera WorldCamera => worldCamera ? worldCamera : worldCamera = Camera.main;
        public Vector2 CanvasSize => rect.rect.size;
        float Scale => canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            rect = (RectTransform)transform;
            foreach (var presenter in GetComponentsInChildren<HudPresenter>(true)) presenter.Bind(this);
        }

        public Vector2 WorldToCanvas(Vector3 world) => (Vector2)WorldCamera.WorldToScreenPoint(world) / Scale;
        public Vector2 ScreenToCanvas(Vector2 screen) => screen / Scale;
        public Vector2 CanvasToScreen(Vector2 canvasPoint) => canvasPoint * Scale;

        public static string FormatTime(double seconds) =>
            Mathf.FloorToInt((float)(seconds / 60.0)).ToString("00") + ":" + Mathf.FloorToInt((float)(seconds % 60.0)).ToString("00");
    }

    // Base for HUD presenters: they read game state and raise commands; they never change rules state directly.
    public abstract class HudPresenter : MonoBehaviour
    {
        protected HudRoot Hud { get; private set; }
        protected GameContext Context => Hud.context;
        protected UiTheme Theme => Hud.theme;
        protected bool Ready => Hud != null && Context != null && Context.IsInitialized;

        internal void Bind(HudRoot hud)
        {
            Hud = hud;
            OnBind();
        }

        protected virtual void OnBind() { }
    }
}
