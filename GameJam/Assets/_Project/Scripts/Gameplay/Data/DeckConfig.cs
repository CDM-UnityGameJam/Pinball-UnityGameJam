using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // The hand: its size, the cards a run starts with, how replacements are drawn and how fast they arrive.
    [CreateAssetMenu(fileName = "DeckConfig", menuName = "Pinball/Config/Deck Config", order = 2)]
    public sealed class DeckConfig : ScriptableObject
    {
        [Title("Hand")]
        [MinValue(1)] public int handSize = 5;
        [ValidateInput(nameof(FitsHand), "The starting hand has more cards than the hand size.")]
        public List<CardSpec> startingHand = new List<CardSpec>();

        [Title("Refill")]
        [MinValue(0.01), SuffixLabel("s"), Tooltip("Time between refills while the hand isn't full.")]
        public float refillInterval = 8f;
        [MinValue(0), SuffixLabel("s"), Tooltip("Every card played (success or failure) pushes the next refill back by this much.")]
        public float playRefillDelay = 4f;

        [Title("Draw table")]
        [InfoBox("Refills pick an element and a number independently, by weight.")]
        public List<ElementWeight> elementWeights = new List<ElementWeight>();
        public List<NumberWeight> numberWeights = new List<NumberWeight>();

        bool FitsHand(List<CardSpec> value) => value == null || value.Count <= handSize;
    }

    [Serializable]
    public struct CardSpec
    {
        [HorizontalGroup, HideLabel, Required] public ElementDefinition element;
        [HorizontalGroup(60), LabelWidth(20), LabelText("#")] public int number;

        public CardSpec(ElementDefinition element, int number) { this.element = element; this.number = number; }
    }

    [Serializable]
    public struct ElementWeight
    {
        [HorizontalGroup, HideLabel, Required] public ElementDefinition element;
        [HorizontalGroup(90), MinValue(0), LabelWidth(45)] public float weight;
    }

    [Serializable]
    public struct NumberWeight
    {
        [HorizontalGroup, LabelWidth(50)] public int number;
        [HorizontalGroup, MinValue(0), LabelWidth(45)] public float weight;
    }
}
