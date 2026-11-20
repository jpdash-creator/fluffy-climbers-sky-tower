using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// "Daily free power-up" (owner 2026-11-17; docs/33): one card at the top of the Daily tab, under the scene header. Once per local day (the same day as the Daily rewards) the player can
    /// watch a rewarded ad (placement <see cref="AdPlacements.DailyPowerUp"/>) and gets ONE unit of a seeded-random implemented power-up (ProgressService.ClaimDailyAdPowerUp, saved at once).
    /// The button shows the Ads icon (accessibility label "Watch an ad"); it is dimmed while no ad is loaded (a tap says so) and reads "Claimed" once today's unit was taken (nothing to tap until tomorrow).
    /// </summary>
    public sealed partial class RewardsScreen
    {
        public const string AdPowerUpCardName = "DailyAdPowerUpCard";
        public const string AdPowerUpButtonName = "DailyAdPowerUpButton";

        Text adPowerUpBody;
        bool adPowerUpBusy;

        /// <summary>The card exists on the Daily tab only (tests look it up by <see cref="AdPowerUpCardName"/>).</summary>
        void BuildDailyAdPowerUpCard(RectTransform content)
        {
            bool claimed = progress.DailyAdPowerUpClaimed;
            bool adReady = !claimed && AdGate.IsRewardedReady(AdPlacements.DailyPowerUp);   // IsReady also starts loading an ad
            float line = LineH(BodyPx);
            float btnW = 300f;
            float btnH = Mathf.Max(ClaimButtonH, UiKit.MinTouchRef);
            float textH = CardPadY * 2f + line * 2f + 6f + line * 2f + 6f + 40f;   // title (up to two lines) and the hint (up to two lines) at 130 percent text, plus slack (tall glyph rows measured 18 px over)
            float h = Mathf.Max(textH, btnH + CardPadY * 2f + 8f);
            RectTransform card = UiKit.Rect(content, AdPowerUpCardName);
            FixedHeight(card, h);
            Image plate = Plate(card, "Plate", "plate_cream", StateTint(claimed ? CardState.Done : CardState.Ready), CardCorner);
            UiKit.Stretch(plate.rectTransform);

            Text title = UiKit.AddText(card, "Title", "Free power-up", BodyPx, UiKit.PanelInk, TextAnchor.UpperLeft, true);
            UiKit.OnPanel(title);
            BestFit(title, false);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(CardPadX, -(CardPadY + line * 2f)); trt.offsetMax = new Vector2(-(btnW + CardPadX * 2f), -CardPadY);

            string hint = claimed ? "Claimed today. A new one tomorrow." : "Watch an ad for 1 random power-up.";
            adPowerUpBody = UiKit.AddText(card, "Hint", hint, BodyPx, UiKit.PanelInk, TextAnchor.UpperLeft);
            UiKit.OnPanel(adPowerUpBody);
            BestFit(adPowerUpBody, true);
            var brt = adPowerUpBody.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(CardPadX, -(CardPadY + line * 2f + 6f + line * 2f)); brt.offsetMax = new Vector2(-(btnW + CardPadX * 2f), -(CardPadY + line * 2f + 6f));

            string label = claimed ? "Claimed" : "";   // the Ads icon replaces the "Watch Ad" text (owner 2026-11-17)
            string a11y = claimed ? "Free power-up, claimed today" : (adReady ? "Watch an ad for a free power-up" : "Watch an ad for a free power-up, not ready yet");
            Button b = UiKit.AddButton(card, AdPowerUpButtonName, label, ToyKit.Button(1), Color.white, new Vector2(btnW, btnH), ClaimDailyAdPowerUp, 44);
            b.GetComponent<A11yLabel>().Text = a11y;
            var crt = (RectTransform)b.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-CardPadX, 0f);
            crt.sizeDelta = new Vector2(btnW, btnH);
            Text t = b.GetComponentInChildren<Text>();
            if (t != null)
            {
                var tr = (RectTransform)t.transform;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
                t.resizeTextMaxSize = 48;
            }
            if (!claimed)
            {
                Image ic = UiKit.AddImage(b.transform, "Icon", ToyKit.AdsIcon, Color.white);   // the Ads icon: the button is never blank
                ic.raycastTarget = false;
                RectTransform r = ic.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(btnH * 0.7f, btnH * 0.7f);
            }
            if (claimed || !adReady)
            {
                // Claimed: nothing to tap. No ad loaded: dimmed but still tappable (the tap explains why and asks the ad service to load one).
                Image bi = b.GetComponent<Image>();
                if (bi != null) bi.color = new Color(0.62f, 0.62f, 0.62f, 0.9f);
                if (t != null) t.color = new Color(0.10f, 0.06f, 0.14f, 0.85f);
                if (claimed) b.interactable = false;
            }
        }

        /// <summary>Taps "Watch Ad": plays the rewarded ad, then grants the power-up once (also callable from tests).</summary>
        public void ClaimDailyAdPowerUp()
        {
            if (adPowerUpBusy || progress.DailyAdPowerUpClaimed) return;
            if (!AdGate.IsRewardedReady(AdPlacements.DailyPowerUp))
            {
                if (adPowerUpBody != null) adPowerUpBody.text = RunHud.AdNotReadyText;
                return;
            }
            adPowerUpBusy = true;
            AdGate.ShowRewarded(AdPlacements.DailyPowerUp, granted =>
            {
                adPowerUpBusy = false;
                if (root == null) return;   // left the screen while the ad was up
                if (!granted)
                {
                    if (adPowerUpBody != null) adPowerUpBody.text = "No reward. Watch the whole ad to get it.";
                    return;
                }
                string pid = progress.ClaimDailyAdPowerUp();
                if (pid == null) { Render(true); return; }
                var outcome = new RewardClaimOutcome { Result = RewardClaimResult.Ok };
                outcome.PowerUpIds.Add(pid);
                AfterClaim(outcome);
            });
        }
    }
}
