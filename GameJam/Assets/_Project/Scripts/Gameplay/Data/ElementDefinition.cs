using Pinball.Core;
using Pinball.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // One card element (Arc, Spark, Shockwave...). A new element is a new asset that mixes existing
    // effect modules; a new kind of behaviour is one new IPassiveEffect / IActiveSkill class.
    // Enemies of an element are immune to its damage and statuses.
    [CreateAssetMenu(fileName = "Element", menuName = "Pinball/Element", order = 10)]
    public sealed class ElementDefinition : ScriptableObject
    {
        [Title("Identity")]
        public string displayName;
        [Tooltip("Name of the active skill a played card casts.")]
        public string skillName;
        public Color color = Color.white;
        [Tooltip("Short glyph shown on cards and on enemies of this element.")]
        public string symbol;
        [PreviewField(48)] public Sprite icon;
        [Tooltip("Card art by number: entry 0 is card 1.")]
        [PreviewField(60), ListDrawerSettings(ShowFoldout = false)]
        public Sprite[] cardFaces = new Sprite[0];

        [Title("Text")]
        [Multiline(2), Tooltip("Card text for the held (passive) effect. Module tokens such as {radius} are filled in by the UI.")]
        public string passiveDescription;
        [Multiline(2)] public string activeDescription;

        [Title("Held in hand: ball enchantment")]
        [InfoBox("Held cards of this element combine, whatever their number: strength follows how many are held.")]
        [SerializeReference, Required, HideLabel, BoxGroup("Passive")]
        public IPassiveEffect passive;

        [Title("Played: active skill")]
        [SerializeReference, Required, HideLabel, BoxGroup("Active")]
        public IActiveSkill active;

        [InfoBox("One row per dice tier: Base, Hit, Full Field, Jackpot.")]
        [ValidateInput(nameof(FourTiers), "Exactly four tiers: Base, Hit, Full Field, Jackpot.")]
        public ActiveTier[] tiers = new ActiveTier[SkillPlanner.TierCount];
        [MinValue(0), Tooltip("Extra waves for every extra card of this element in the same batch.")]
        public int extraWavesPerCopy;
        [MinValue(0), SuffixLabel("s"), Tooltip("Extra burn seconds for every extra card of this element in the same batch.")]
        public float extraBurnSecondsPerCopy;

        [Title("Feedback")]
        [AssetsOnly, Tooltip("Shown on the ball while this element's enchantment is active.")]
        public GameObject ballAuraPrefab;
        public VfxCue passiveHitVfx;
        [Tooltip("Active skill hit on each target when no die matched (Base).")]
        public VfxCue activeHitVfx;
        [Tooltip("Active skill hit on each target when a die matched the card's number (Hit, Full Field, Jackpot). Falls back to Active Hit Vfx.")]
        public VfxCue matchedHitVfx;
        public SfxCue passiveHitSfx, activeHitSfx, castSfx;

        public Sprite CardFace(int number) =>
            cardFaces != null && number >= 1 && number <= cardFaces.Length ? cardFaces[number - 1] : null;

        public SkillPlan Plan(CastTier tier, int copies) =>
            SkillPlanner.Plan(tiers, tier, copies, extraWavesPerCopy, extraBurnSecondsPerCopy);

        bool FourTiers(ActiveTier[] value) => value != null && value.Length == SkillPlanner.TierCount;
    }
}
