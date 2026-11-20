# Closed testing: owner checklist (2026-11-17)

Game: **Fluffy Climbers: Tower Jump**, package `com.jdlabs.fluffyclimbers`. Details: docs 33, 35, 47, 48, 49 (folder `Planning/Game Ideas/fluffy-climbers-sky-tower/docs`), `Planning/RELEASE-AAB-CHECKLIST.md`.

What the release AAB is (built by `AndroidReleaseBuild`): leaderboard ON (turned on only for the build, the repo file stays OFF), no admin, dev or test tools, real Google Play purchases only, R8 mapping and symbols. The normal development APK keeps the leaderboard OFF. The build log prints `leaderboard enabled in AAB`.

## A. Product IDs and prices (from doc 48, do not change)

One-time products (Monetize > Products > In-app products, Active, consumable). INR is set under country prices.

| # | Product ID | USD | INR |
|---|---|---|---|
| 1 | `com.jdlabs.fluffyclimbers.gems.pack_50` | 0.79 | 75 |
| 2 | `com.jdlabs.fluffyclimbers.gems.pack_100` | 1.49 | 142 |
| 3 | `com.jdlabs.fluffyclimbers.gems.pack_200` | 2.49 | 237 |
| 4 | `com.jdlabs.fluffyclimbers.gems.pack_500` | 5.99 | 569 |
| 5 | `com.jdlabs.fluffyclimbers.gems.pack_1000` | 10.99 | 1,044 |
| 6 | `com.jdlabs.fluffyclimbers.gems.pack_2000` | 20.99 | 1,994 |
| 7 | `com.jdlabs.fluffyclimbers.gems.pack_5000` | 49.99 | 4,749 |
| 8 | `com.jdlabs.fluffyclimbers.gems.pack_10000` | 89.99 | 8,549 |
| 9 | `com.jdlabs.fluffyclimbers.gems.pack_15000` | 119.99 | 11,399 |

Subscriptions (Monetize > Products > Subscriptions, one auto-renewing base plan each, then Activate).

| # | Subscription ID | Base plan ID (suggested) | Period | USD | INR |
|---|---|---|---|---|---|
| 10 | `com.jdlabs.fluffyclimbers.sub.weekly` | `weekly-autorenew` | 1 week | 1.49 | 149 |
| 11 | `com.jdlabs.fluffyclimbers.sub.weekly_plus` | `weekly-plus-autorenew` | 1 week | 2.99 | 299 |
| 12 | `com.jdlabs.fluffyclimbers.sub.monthly` | `monthly-autorenew` | 1 month | 4.99 | 449 |
| 13 | `com.jdlabs.fluffyclimbers.sub.monthly_premium` | `monthly-premium-autorenew` | 1 month | 9.99 | 899 |

Do NOT create `...bundle.starter`. Names, descriptions and benefit lines to paste: doc 48 sections 1 and 2. IDs are permanent and lowercase.

## B. Play Console, in this order

1. Create the app `com.jdlabs.fluffyclimbers` (developer registration paid, app name, free app with in-app purchases and ads).
2. Set up the **payments profile** (merchant account, bank, tax). Nothing can be sold without it.
3. Test and release > Testing > **Closed testing** > create track > create release > upload `E:\Unity\builds\fluffyclimbers_v<ver>.aab` (versioned file). Upload the `_mapping.txt` as the deobfuscation file and the symbols zip if the build log says it was not embedded.
4. Turn on **Play App Signing** when asked (default). Products can be created only after a build with the BILLING permission is uploaded.
5. Create the 9 in-app products and 4 subscriptions from section A, each **Active**.
6. Setup > **License testing**: add the tester Gmail accounts.
7. Closed testing track > Testers: create the tester list (emails or a Google Group), save, copy the opt-in link and send it. Testers must install from that link (sideloaded APKs cannot buy).
8. App content: privacy policy URL `https://jpdash-creator.github.io/fluffy-climbers-sky-tower/policy.html`, Ads: Yes, Advertising ID: Yes, target age 13+, Data safety (section E). Roll out the release to the closed track.

## C. Keys and sign-in

- **Play licence key:** check `Assets/Resources/Services/iap_config.json` `playLicenseKey` is the real base64 key from Monetize > Monetization setup > Licensing (just confirm it, the release preflight refuses a placeholder).
- **Play App Signing SHA-1:** after the first upload copy it from Test and release > Setup > App signing > "App signing key certificate". Register it in BOTH places:
  1. Play Games Services > Configuration > Credentials: add an Android credential for the package with this SHA-1 and save (doc 35). Also keep the upload key SHA-1 `E3:0D:AE:F1:AF:2A:29:EC:78:D5:40:1E:F4:6D:A9:66:81:11:5F:6A` there.
  2. Firebase console, project `fluffy-climbers-tower-jump` > Project settings > the Android app > Add fingerprint (App Signing SHA-1, plus the upload SHA-1). Without it leaderboard sign-in fails for Play-installed builds. Re-download `google-services.json` only if the console asks (the file stays git-ignored).
- Confirm the Play Games project id `794061115424` belongs to this game.

## D. AdMob

- Publish the app, then AdMob > Apps > this app > **app-ads.txt**: host the line AdMob shows at the root of the developer website listed on the store listing (doc 33 step 5) and check the status in AdMob.
- Add your tester devices (AdMob > Settings > Test devices) and the UMP consent test device id if you test consent. Currently `testDeviceIds` in `ads_config.json` is empty and `testMode` is false.
- Three rewarded unit ids (`double_reward`, `daily_powerup`, `shop_free_gems`) are still placeholders; they fall back to `default` until you create the units. To give each its own unit: AdMob > Apps > this app > **Ad units > Add ad unit > Rewarded**, name it (for example `double_reward`), then paste its id (`ca-app-pub-...~/...`) into `Assets/Resources/Services/ads_config.json` under `rewardedAdUnitIds` for that key.
- **Interstitial unit (new, restart ad):** AdMob > Apps > this app > Ad units > Add ad unit > **Interstitial**, name it `restart_interstitial`, and paste its id into `ads_config.json` under `interstitialAdUnitIds` (`restart`, and the same id as `default`). Until you do, a RELEASE build shows no interstitial (it logs one warning); development builds use Google's sample interstitial. Rebuild after editing the file.
- **Remove Ads product (new):** Play Console > Monetize > Products > In-app products > Create product, id `com.jdlabs.fluffyclimbers.ads.remove`, name Remove Ads, price 2.99 USD (set India in INR), status Active. It is a one-time product: do not mark it consumable anywhere (doc 48, product 10).

## E. Data safety form (doc 49, section "Leaderboard and Firebase")

Tick in addition to the ad and purchase answers: User IDs (Play Games id, Firebase uid), Name (leaderboard display name, public), Photos (profile picture link, public), App interactions (weekly scores, run inputs, hero, power-ups, prize claims), Device or other IDs (Firebase installation id), Approximate location (IP in server logs). All collected; shared with a service provider (Google/Firebase); optional except where doc 49 says required; encrypted in transit Yes; deletion by e-mail jpdash.creator@gmail.com Yes; data sold No; account creation question stays No. Firebase Analytics is OFF (manifest flags), so no new Analytics purpose. Then export the CSV to `Planning/play-data-safety.csv`.
