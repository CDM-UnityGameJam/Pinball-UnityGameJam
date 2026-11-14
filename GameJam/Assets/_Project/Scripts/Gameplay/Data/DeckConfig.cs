using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // The hand: its size, how cards are drawn, the lucky draw, and what refills cost.
    [CreateAssetMenu(fileName = "DeckConfig", menuName = "Pinball/Config/Deck Config", order = 2)]
    public sealed class DeckConfig : ScriptableObject
    {
        [Title("Hand")]
        [InfoBox("A run starts with a full hand drawn for free and no lucky number.")]
        [MinValue(1), MaxValue(5)] public int handSize = 5;

        [Title("Charge (refill)")]
        [InfoBox("While the hand isn't full, charge builds up. The requirement is the row for how many slots are empty " +
                 "(row 1 = one empty slot). When it is reached, every empty slot is drawn at once and charge resets.")]
        [ValidateInput(nameof(HasCosts), "Needs one cost per possible empty slot (hand size).")]
        public float[] refillCosts = { 100f, 250f, 450f, 700f, 1000f };
        [MinValue(0), SuffixLabel("/s"), Tooltip("Charge per combat second while a card is missing.")]
        public float naturalPerSecond = 3f;
        [MinValue(0), Tooltip("Charge for every new ball contact with an enemy (even inside its damage cooldown).")]
        public float perContact = 10f;

        [Title("Lucky draw")]
        [Range(0, 1), Tooltip("On a refill, unless the hand still holds the lucky number, each new card becomes a lucky " +
                               "candidate with this chance. One candidate's number becomes the lucky number.")]
        public float luckyProbability = 0.2f;

        [Title("Draw table")]
        [InfoBox("Every drawn card picks a number and an element independently, by weight.")]
        public List<NumberWeight> numberWeights = new List<NumberWeight>();
        public List<ElementWeight> elementWeights = new List<ElementWeight>();

        bool HasCosts(float[] value) => value != null && value.Length >= handSize;
    }

    [Serializable]
    public struct CardSpec
    {
        [HorizontalGroup, HideLabel, Required] public ElementDefinition element;
        [HorizontalGroup(60), LabelWidth(20), LabelText("#")] public int number;

        public CardSpec(ElementDefinition element, int number) { this.element = element; this.number = number; }

        public override string ToString() => $"{number} · {(element ? element.displayName : "?")}";
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
