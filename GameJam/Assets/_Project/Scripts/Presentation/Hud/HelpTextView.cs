using TMPro;

namespace Pinball.Presentation
{
    // Help lines that quote numbers from the configs, so they never go stale. Static lines are plain text
    // in the prefab.
    public sealed class HelpTextView : HudPresenter
    {
        public TMP_Text ballDamage, stacking, chargeCost, chargeSources;

        protected override void OnBind()
        {
            var config = Context.config;
            var deck = config.deck;
            if (ballDamage) ballDamage.text = $"Ball hit: {config.ballHitDamage} damage";
            if (stacking)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var e in config.elements)
                {
                    if (!e) continue;
                    if (e.extraWavesPerCopy > 0) parts.Add($"{e.displayName} +{e.extraWavesPerCopy}w");
                    else if (e.extraBurnSecondsPerCopy > 0f) parts.Add($"{e.displayName} +{e.extraBurnSecondsPerCopy:0.##}s");
                    else parts.Add($"{e.displayName} fixed");
                }
                stacking.text = "Extra card: " + string.Join(" · ", parts);
            }
            if (chargeCost) chargeCost.text = $"Refill by empty slots: {string.Join(" / ", System.Array.ConvertAll(deck.refillCosts, c => c.ToString("0.##")))}";
            if (chargeSources) chargeSources.text = $"Natural {deck.naturalPerSecond:0.##}/s · Ball contact +{deck.perContact:0.##}";
        }
    }
}
