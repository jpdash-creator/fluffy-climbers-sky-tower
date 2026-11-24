using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Keeps a Rewards card sized to its real text (CUX-3, CUX-5). The card is first built with an estimate of the list width (the canvas is not laid out yet when the
    /// cards are made); this component re-measures the title at the REAL text-column width as soon as the card has its width (and whenever it changes), then sets the
    /// row height, the title rectangle, the bar and the reward plate. A wrapped title therefore never slides under the progress bar.
    /// </summary>
    public sealed class RewardCardFit : MonoBehaviour
    {
        public enum Kind { Card, Completed, ButtonCard }
        public Kind Mode;
        public RectTransform Card;
        public LayoutElement Row;
        public Text Title;
        public RectTransform Bar, Plate;
        public float MidL, MidR, PlateW;
        public bool Ready;
        // ButtonCard (ad power-up card, weekly prize cards): title + body over the text column, a button of BtnW at the right
        public Text Body;
        public float BtnW, BodyMinLines, TailExtra, MinH;
        public string[] Alts;   // messages a tap can swap into the body: the box holds the longest
        float lastW = -1f;

        /// <summary>Re-measures at <paramref name="width"/> (the card's real width); does nothing when the width did not change.</summary>
        public void Apply(float width)
        {
            if (width < 50f || Mathf.Abs(width - lastW) < 0.4f) return;
            lastW = width;
            if (Mode == Kind.Card) RewardsScreen.FitCard(this, width);
            else if (Mode == Kind.Completed) RewardsScreen.FitCompleted(this, width);
            else RewardsScreen.FitButtonCard(this, width);
            var rt = transform.parent as RectTransform;
            if (Mode == Kind.ButtonCard) { if (rt != null && isActiveAndEnabled) LayoutRebuilder.MarkLayoutForRebuild(rt); }
            else if (rt != null && rt.parent != null && isActiveAndEnabled) LayoutRebuilder.MarkLayoutForRebuild(rt.parent as RectTransform);
        }

        void LateUpdate()
        {
            if (Card != null) Apply(Card.rect.width);   // safety net when the dimension callback was missed; one float compare, no allocation
        }

        void OnRectTransformDimensionsChange()
        {
            if (Card != null) Apply(Card.rect.width);
        }
    }
}
