using System;
using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The "Remove Ads" section of the Gems and Coins page (owner 2026-11-17), BETWEEN the gem packs and the Coins banner: a "No Ads" banner and ONE centred card, a little wider than a gem
    /// card: the No Ads icon, "Remove Ads", and a green button that carries the price ($2.99, the store's localised price once it is loaded), like the gem pack cards. After the purchase (or a restore) the same card reads "Ads removed"
    /// with a disabled "Owned" button: nothing to buy. The product is a one-time non-consumable (ProductCatalog.AdsRemove); the entitlement is ProgressService.AdsRemoved and AdGate does the rest.
    /// </summary>
    public sealed partial class ShopScreen
    {
        public const string RemoveAdsSectionTitle = "No Ads";
        public const string RemoveAdsCardName = "RemoveAdsCard";
        public const string RemoveAdsBuyName = "BuyRemoveAds";
        const float AdsCardWidthFraction = 0.4f;   // a gem card is one third of the row

        /// <summary>The store price text of Remove Ads ("$2.99" or the localised one).</summary>
        public string RemoveAdsPriceText()
        {
            string store = StoreLink.StorePrice(ProductCatalog.AdsRemove);
            ProductDef def = ProductCatalog.Get(ProductCatalog.AdsRemove);
            return store ?? ShopChrome.PriceText(def != null ? def.PriceUsd : 2.99);
        }

        void BuildRemoveAdsSection(RectTransform pageList)
        {
            ShopChrome.SectionBanner(pageList, RemoveAdsSectionTitle);
            bool owned = progress.AdsRemoved;
            string price = RemoveAdsPriceText();
            float titleH = Mathf.Ceil(UiKit.MinBodyTextPx * UiKit.TextScale * 1.3f) * 2f + 6f;   // the title may take two lines at 130 percent text
            const float iconSize = 150f;
            float cardH = PileTop + iconSize + 4f + titleH + 12f + StripH + 9f;

            RectTransform row = UiKit.Rect(pageList, "AdsGrid");
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = cardH + CardGap;

            RectTransform crt = ShopChrome.Card(row, RemoveAdsCardName, ShopChrome.FrameColor.Gold);
            crt.anchorMin = new Vector2(0.5f - AdsCardWidthFraction * 0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f + AdsCardWidthFraction * 0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0f, cardH);

            Image strip = ToyKit.Sliced(crt, "Fill", Theme.RoundSprite, 26f);   // the dark price strip, like the pack cards
            strip.color = new Color(0.66f, 0.36f, 0.08f, 1f);
            strip.rectTransform.anchorMin = new Vector2(0f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.pivot = new Vector2(0.5f, 0f);
            strip.rectTransform.offsetMin = new Vector2(9f, 9f);
            strip.rectTransform.offsetMax = new Vector2(-9f, 9f + StripH);

            Image icon = UiKit.AddImage(crt, "NoAdsIcon", ToyKit.NoAdsIcon, Color.white);   // decorative: the title says it
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -PileTop), new Vector2(iconSize, iconSize));

            Text title = UiKit.AddText(crt, "Title", owned ? "Ads removed" : "Remove Ads", 46, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(title);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = Mathf.RoundToInt(UiKit.MinBodyTextPx);
            title.resizeTextMaxSize = 46;
            RectTransform trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            float top = PileTop + iconSize + 4f;
            trt.offsetMin = new Vector2(8f, -(top + titleH)); trt.offsetMax = new Vector2(-8f, -top);

            string a11y = owned ? "Remove Ads, owned, ads are removed" : "Buy Remove Ads for " + price;
            Button buy = BottomButton(crt, RemoveAdsBuyName, 1, null, owned ? "Owned" : price, a11y, TapRemoveAds, 9f + 11f, 22f);
            if (owned)
            {
                buy.interactable = false;
                Image bi = buy.GetComponent<Image>();
                if (bi != null) bi.color = new Color(0.62f, 0.62f, 0.62f, 0.9f);
            }
        }

        /// <summary>Taps BUY on the Remove Ads card: the real store purchase (Services.Payments; the stub in the editor and development builds). Granted once per transaction.</summary>
        public void TapRemoveAds()
        {
            if (progress.AdsRemoved || storeBusy) return;
            ProductDef def = ProductCatalog.Get(ProductCatalog.AdsRemove);
            if (def == null || !def.Live) { Info("Not available", "This cannot be bought right now."); return; }
            storeBusy = true;
            IPaymentService pay = Services.Payments;
            pay.Initialize();
            pay.Purchase(def.Id, r =>
            {
                storeBusy = false;
                string message, title = "Purchase";
                if (r.Status == PurchaseStatus.Success)
                {
                    StorePurchaseOutcome o = progress.ApplyStorePurchase(r.ProductId, r.TransactionId, DateTime.UtcNow);
                    if (o == StorePurchaseOutcome.Granted || o == StorePurchaseOutcome.Duplicate) { title = "Ads removed"; message = "Thank you! No more ads, and every ad reward is now given at once."; }
                    else { r.Reject = true; title = "Could not remove ads"; message = "Your purchase could not be added. It will be refunded by Google Play if this keeps happening."; }
                }
                else if (r.Status == PurchaseStatus.Cancelled) return;
                else if (r.Status == PurchaseStatus.Pending) { title = "Purchase pending"; message = "Your payment is waiting for approval. Ads are removed when it is approved."; }
                else { title = "Purchase failed"; message = r.Status == PurchaseStatus.Unavailable ? "The store is not available right now. Check your connection and try again." : "The purchase did not go through. You were not charged."; }
                if (screen == null) return;
                RebuildKeepScroll();
                Info(title, message);
            });
        }
    }
}
