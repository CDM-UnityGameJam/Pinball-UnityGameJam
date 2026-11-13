using System.Collections.Generic;
using Pinball.Core;
using Pinball.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // One card element (Arc, Spark, Shockwave...). A new element is a new asset that mixes existing
    // effect modules; a new kind of behaviour is one new IPassiveEffect / IActiveSkill class.
    [CreateAssetMenu(fileName = "Element", menuName = "Pinball/Element", order = 10)]
    public sealed class ElementDefinition : ScriptableObject
    {
        [Title("Identity")]
        public string displayName;
        [Tooltip("Name of the active skill a played card casts.")]
        public string skillName;
        public Color color = Color.white;
        [PreviewField(48)] public Sprite icon;
        [Tooltip("Card illustration per number: index 0 is Luck Config's minNumber. Missing entries fall back to the text card.")]
        [PreviewField(48)] public Sprite[] cardArt = new Sprite[0];

        public Sprite CardArt(int number, int minNumber)
        {
            int i = number - minNumber;
            return cardArt != null && i >= 0 && i < cardArt.Length ? cardArt[i] : null;
        }

        [Title("Text")]
        [Multiline(3), Tooltip("Card text for the held (passive) effect. Tokens such as {number} are filled in by the UI.")]
        public string passiveDescription;
        [Multiline(2)] public string activeDescription;

        [Title("Held in hand: ball enchantment")]
        [SerializeReference, Required, HideLabel, BoxGroup("Passive")]
        public IPassiveEffect passive;

        [Title("Played: active skill")]
        [SerializeReference, Required, HideLabel, BoxGroup("Active")]
        public IActiveSkill active;

        [InfoBox("One entry per Luck Config band that can cast (Q > 0). Each range is interpolated by where Q sits in the band.")]
        [ListDrawerSettings(ShowFoldout = false)]
        public List<BandScaling> bandScaling = new List<BandScaling>();

        [Title("Feedback")]
        [AssetsOnly, Tooltip("Shown on the ball while this element's enchantment is active.")]
        public GameObject ballAuraPrefab;
        public VfxCue passiveHitVfx, activeHitVfx;
        public SfxCue passiveHitSfx, activeHitSfx, castSfx;

        public SkillPlan Plan(LuckConfig luck, int q) => SkillPlanner.Plan(luck.bands, bandScaling, q);
    }
}
