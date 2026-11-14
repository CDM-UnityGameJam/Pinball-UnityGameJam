using Pinball.Core;
using Pinball.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

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

        [InfoBox("A played group casts the Normal row, or the Lucky row when its number is the lucky number.")]
        [HideLabel, BoxGroup("Normal cast")] public ActiveTier normal = new ActiveTier(true, 4, 1, 12, 0f, 6f, 0f);
        [HideLabel, BoxGroup("Lucky cast")] public ActiveTier lucky = new ActiveTier(true, 4, 1, 12, 0f, 6f, 0f);
        [MinValue(0), Tooltip("Extra waves for every extra card of this element in the same group.")]
        public int extraWavesPerCopy;
        [MinValue(0), SuffixLabel("s"), Tooltip("Extra burn seconds for every extra card of this element in the same group.")]
        public float extraBurnSecondsPerCopy;

        [Title("Feedback")]
        [AssetsOnly, Tooltip("Shown on the ball while this element's enchantment is active.")]
        public GameObject ballAuraPrefab;
        public VfxCue passiveHitVfx;
        [Tooltip("Active skill hit on each target of a Normal cast.")]
        public VfxCue activeHitVfx;
        [FormerlySerializedAs("matchedHitVfx"), Tooltip("Active skill hit on each target of a Lucky cast. Falls back to Active Hit Vfx.")]
        public VfxCue luckyHitVfx;
        public SfxCue passiveHitSfx, activeHitSfx, castSfx;

        public Sprite CardFace(int number) =>
            cardFaces != null && number >= 1 && number <= cardFaces.Length ? cardFaces[number - 1] : null;

        public SkillPlan Plan(CastTier tier, int copies) =>
            SkillPlanner.Plan(tier == CastTier.Lucky ? lucky : normal, copies, extraWavesPerCopy, extraBurnSecondsPerCopy);
    }
}
