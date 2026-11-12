using Pinball.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // One enemy's screen label: number above, health bar and "hp/max" below, status ("Burning", "Stunned").
    public sealed class EnemyLabelView : MonoBehaviour
    {
        public TMP_Text number, health, status;
        public RectTransform healthBar;
        public RectTransform healthFill;
        public Image healthFillImage;
        [Tooltip("Status text sits this far above the enemy's far edge.")]
        public Vector2 statusOffset = new Vector2(0f, 12f);
        [Tooltip("HP text sits this far below the bar.")]
        public Vector2 healthTextOffset = new Vector2(0f, -12f);

        float fullWidth = -1f;

        internal void Show(Vector2 numberAt, Vector2 barAt, Vector2 statusAt, Enemy enemy, bool previewed, UiTheme theme)
        {
            if (fullWidth < 0f) fullWidth = healthFill.sizeDelta.x;
            number.rectTransform.anchoredPosition = numberAt;
            healthBar.anchoredPosition = barAt;
            health.rectTransform.anchoredPosition = barAt + healthTextOffset;
            status.rectTransform.anchoredPosition = statusAt + statusOffset;

            var hp = enemy.Health;
            number.text = enemy.Number.ToString();
            number.color = previewed ? theme.gold : theme.text;
            healthFill.sizeDelta = new Vector2(fullWidth * Mathf.Clamp01(hp.Current / (float)Mathf.Max(1, hp.Max)), healthFill.sizeDelta.y);
            healthFillImage.color = previewed ? theme.gold : theme.green;
            health.text = $"{hp.Current}/{hp.Max}";
            var s = enemy.Status;
            status.text = (s.IsBurning ? "Burning " : "") + (s.IsStunned ? "Stunned" : s.PassiveImmunityRemaining > 0f ? "Stun immune" : "");
        }
    }
}
