using System.Collections.Generic;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>One line of a help page: an existing game icon and a short text.</summary>
    public sealed class HelpRow
    {
        /// <summary>Icon spec, see <see cref="HelpIcons.Get"/>: a plain name is Resources/Art/Ui/&lt;name&gt;; "rw:" Art/Rewards, "pw:" Art/PowerUps, "ch:" Art/Chests, "g:" a code glyph, "gp:" a Gameplay setting icon.</summary>
        public readonly string Icon;
        public readonly string Text;
        public HelpRow(string icon, string text) { Icon = icon; Text = text; }
    }

    /// <summary>One carousel page: a title and 2 to 5 icon rows.</summary>
    public sealed class HelpPage
    {
        public readonly string Title;
        public readonly HelpRow[] Rows;
        public HelpPage(string title, params HelpRow[] rows) { Title = title; Rows = rows; }
    }

    /// <summary>The help of ONE screen (or one tab of a screen): an id kept in the save (SaveData.HelpSeen) and its pages.</summary>
    public sealed class HelpTopic
    {
        public readonly string Id;
        public readonly HelpPage[] Pages;
        public HelpTopic(string id, params HelpPage[] pages) { Id = id; Pages = pages; }
    }

    /// <summary>
    /// First-time help (owner 2026-11-25): THE one static table of all help text. Edit the text here only. Topic ids are permanent (they are stored in the save as "seen").
    /// <see cref="IdsFor"/> maps a screen (and its tab) to the topic ids shown when it is entered; a tab that holds two sections (Shop VIP = Subscription + Chests, Shop Character = Heroes + Powers)
    /// gives two ids, shown together as one carousel. Home and the pop-ups are not topics: Home is two buttons, and the level, Endless, crown and hero pop-ups are explained on the Level screen and Hero sheet topics.
    /// </summary>
    public static class HelpTable
    {
        public const string Map = "map", Levels = "levels", Heroes = "heroes", Merge = "merge", Powers = "powers", AllHeroes = "all_heroes", HeroSheet = "hero_sheet",
            ShopBundles = "shop_bundles", ShopSubscription = "shop_subscription", ShopChests = "shop_chests", ShopHeroes = "shop_heroes", ShopPowers = "shop_powers", ShopGems = "shop_gems",
            Gameplay = "gameplay", Leaderboard = "leaderboard", RewardsMain = "rewards_main", RewardsDaily = "rewards_daily", RewardsWeekly = "rewards_weekly", Settings = "settings";

        static HelpRow R(string icon, string text) { return new HelpRow(icon, text); }
        static HelpPage P(string title, params HelpRow[] rows) { return new HelpPage(title, rows); }

        public static readonly HelpTopic[] All =
        {
            new HelpTopic(Map,
                P("World map",
                    R("icon_flag", "Tap a region to open its levels."),
                    R("icon_lock", "A padlock: clear every level of the region before it."),
                    R("icon_star", "Stars won in a region, out of all it holds."),
                    R("icon_tower", "Coming soon: arrives in a later update.")),
                P("Bottom bar",
                    R("icon_shop", "Shop: gems, coins, chests, heroes, power-ups."),
                    R("icon_crown", "Leaderboard: the weekly crown ranking."),
                    R("icon_heroes", "Heroes: pick, merge and see your power-ups."),
                    R("icon_daily", "Rewards: a red dot means one is ready."))),

            new HelpTopic(Levels,
                P("Level screen",
                    R("gp:2", "Gameplay setting: harder scrolls faster and pays more."),
                    R("icon_star", "Pills: levels cleared and stars won in this setting."),
                    R("icon_flag", "Tap a level to open it. Clear the one before first."),
                    R("lb_icon_infinity", "Endless: a tower with no top, and 3:00 Time Attack.")),
                P("Level pop-up",
                    R("icon_crown", "Crown: your score. Tap it to see how it is counted."),
                    R("icon_coin", "Coins for a clear. A 3-star level pays less."),
                    R("icon_gem", "Levels 5, 10, 15, 20, 25 pay gems on the first clear."),
                    R("icon_penalty", "Away from home, your hero is a little weaker.")),
                P("Special levels",
                    R("icon_flame", "Six special levels per region, each with its own rule."),
                    R("icon_star", "Every level holds 3 stars to collect."),
                    R("medal_gold", "Fast climbs win Bronze, Silver and Gold medals."),
                    R("icon_play", "The rule is written in the pop-up. PLAY starts."))),

            new HelpTopic(Heroes,
                P("Your heroes",
                    R("icon_heroes", "The big hero is yours to play. Tap a card to choose."),
                    R("lb_icon_info", "The (i) button opens the hero sheet."),
                    R("icon_lock", "A grey card is a hero you do not own yet."),
                    R("icon_chest", "Find heroes in chests, bundles and the shop.")),
                P("Hero tabs",
                    R("MergeIcon", "Merge: 3 copies make 1 hero of the next tier."),
                    R("icon_bolt", "Powers: the power-ups you own."),
                    R("icon_heroes", "All Heroes: every hero, by region."))),

            new HelpTopic(Merge,
                P("Merge",
                    R("MergeIcon", "Merge 3 copies of a hero into 1 of the next tier."),
                    R("icon_star", "Higher tiers are stronger and gain powers."),
                    R("icon_restart", "The 3 copies are used up. The new hero gets fresh stats."),
                    R("lb_icon_info", "Legendary is the top tier, so it cannot merge."))),

            new HelpTopic(Powers,
                P("Your power-ups",
                    R("icon_bolt", "The number on a card is how many you own."),
                    R("lb_icon_info", "Tap a card to read what it does."),
                    R("icon_shop", "Buy more in the Shop."),
                    R("icon_play", "In a run, owned ones show as round buttons.")),
                P("What they do",
                    R("pw:pw_flight", "Flight: fly up for 10 seconds and steer."),
                    R("pw:pw_rocket", "Rocket: climb 20 floors in a flash."),
                    R("pw:pw_doublejump", "Double Jump: one more jump in mid-air."),
                    R("pw:pw_wallstick", "Wall Stick: cling to the walls a moment.")),
                P("More power-ups",
                    R("pw:pw_shield", "Shield: gongs cannot hurt you."),
                    R("pw:pw_safety_net", "Safety Net: the next fall bounces you up."),
                    R("pw:pw_glide", "Glide Cape: slows the game after each jump."),
                    R("pw:pw_slowtime", "Slow Time: the danger zone rises slower."))),

            new HelpTopic(AllHeroes,
                P("All Heroes",
                    R("icon_heroes", "Every hero of the game, grouped by region."),
                    R("icon_lock", "A padlock and a grey card: not yours yet."),
                    R("lb_icon_info", "Tap a hero to open its sheet."),
                    R("MergeIcon", "The Merge button opens the merge screen."))),

            new HelpTopic(HeroSheet,
                P("Hero sheet",
                    R("icon_tower", "Jump: how high the hero launches."),
                    R("icon_speed", "Speed: how fast it moves sideways."),
                    R("icon_dial", "Control: how quickly it turns and stops."),
                    R("rw:icon_wall", "Wall Bounce: how hard walls push it back.")),
                P("Using a hero",
                    R("icon_star", "The ribbon shows the tier. Higher is stronger."),
                    R("icon_penalty", "Away from home region, a hero is a little weaker."),
                    R("icon_play", "Use this hero makes it your chosen hero."),
                    R("g:left", "The green arrows step to the next hero you own."))),

            new HelpTopic(ShopBundles,
                P("Bundles",
                    R("icon_bundle", "A bundle holds coins, power-ups and a hidden hero card."),
                    R("icon_gem", "Tap one to see what is inside, then buy with gems."),
                    R("lb_icon_clock", "A bundle with a timer is a limited offer."))),

            new HelpTopic(ShopSubscription,
                P("Subscription",
                    R("icon_crown", "VIP plans run for a week or a month."),
                    R("icon_gem", "Gems at once, then gems and coins every day."),
                    R("icon_bolt", "Plans add power-ups. Bigger ones add a chest."),
                    R("icon_daily", "Tap the daily claim button each day."))),

            new HelpTopic(ShopChests,
                P("Chests",
                    R("ch:chest_iron", "Iron chest: a power-up, coins and a hero card."),
                    R("ch:chest_golden", "Gold chest: more of all, and a better hero card."),
                    R("icon_gem", "Chests cost gems. Open 1 or 10 at a time."),
                    R("lb_icon_info", "The (i) button shows the odds."))),

            new HelpTopic(ShopHeroes,
                P("Heroes",
                    R("icon_heroes", "Buy hero cards with coins. Rarer costs more."),
                    R("icon_lock", "Only heroes of regions you have opened."),
                    R("MergeIcon", "A second copy is a new card. Three merge up."))),

            new HelpTopic(ShopPowers,
                P("Power-ups",
                    R("icon_bolt", "Buy power-ups with coins. The number is how many you own."),
                    R("lb_icon_info", "Tap the (i) on a card to read what it does."),
                    R("pw:pw_flight", "Use them in a run with the round buttons."))),

            new HelpTopic(ShopGems,
                P("Gems and Coins",
                    R("icon_gem", "Gems are premium money. Gem packs cost real money."),
                    R("icon_coin", "Coins buy heroes and power-ups. 1 gem is 20 coins."),
                    R("icon_no_ads", "Remove Ads: no ad breaks, every ad reward paid at once."),
                    R("icon_ads", "Or watch an ad for a free revive or double coins."))),

            new HelpTopic(Gameplay,
                P("Move and jump",
                    R("g:left", "Hold the left and right arrows to steer."),
                    R("g:up", "Tap the big button to jump. Swap sides in Settings."),
                    R("icon_tower", "Each platform row is a floor. Reach the top to win."),
                    R("icon_speed", "The screen scrolls up. Fall to the bottom and the run ends.")),
                P("On the screen",
                    R("g:pause", "Pause. The game also pauses if you leave the app."),
                    R("icon_star", "Collect up to 3 stars in every level."),
                    R("icon_flame", "Chain big jumps for combos and many crown points."),
                    R("rw:icon_wall", "The side walls bounce you back.")),
                P("After a fall",
                    R("icon_gem", "Revive with gems or an ad, up to twice."),
                    R("g:restart", "Restart with one tap."),
                    R("pw:pw_flight", "Tap a round button to use a power-up."))),

            new HelpTopic(Leaderboard,
                P("Leaderboard",
                    R("lb_icon_trophy", "Three boards: Overall, Levels and Endless."),
                    R("lb_icon_crown", "Your crowns decide your rank."),
                    R("lb_icon_clock", "A new week starts every Monday."),
                    R("lb_icon_gift", "Top ranks win prizes. Claim them in Rewards.")),
                P("Joining",
                    R("lb_icon_info", "Sign in with Google Play to join."),
                    R("lb_icon_map", "Level, Endless and Time Attack runs of the week all count."))),

            new HelpTopic(RewardsMain,
                P("Main goals",
                    R("rw:task_flag", "Each region has its own list of goals."),
                    R("icon_coin", "Finish a goal, then tap Claim for coins, gems or power-ups."),
                    R("icon_star", "Each goal can be claimed once."))),

            new HelpTopic(RewardsDaily,
                P("Daily goals",
                    R("rw:icon_calendar", "New daily goals every day."),
                    R("icon_star", "Each finished goal adds points to the bar."),
                    R("icon_coin", "Reach the marks on the bar to claim coins."),
                    R("icon_ads", "Watch an ad for a free power-up."))),

            new HelpTopic(RewardsWeekly,
                P("Weekly goals",
                    R("rw:icon_trophy", "A longer list of goals for the whole week."),
                    R("icon_coin", "Claim coins, gems and power-ups for each."),
                    R("lb_icon_clock", "The list starts again every week."))),

            new HelpTopic(Settings,
                P("Sound and touch",
                    R("icon_sound", "Sound effects on or off."),
                    R("icon_music", "Music on or off."),
                    R("icon_vibrate", "Haptics: vibration on touch."),
                    R("icon_motion", "Reduced motion: less shaking and flashing.")),
                P("Play",
                    R("icon_hand", "Left-handed: swaps the move and jump pads."),
                    R("icon_size", "Text size: make all text bigger."),
                    R("icon_speed", "Pace ghost: shows your best pace as you climb.")),
                P("More",
                    R("lb_icon_info", "Help: show each screen's help once."),
                    R("icon_restart", "Show help again starts over."),
                    R("icon_shop", "Restore purchases brings back what you paid for."))),
        };

        static readonly Dictionary<string, HelpTopic> byId = Index();

        static Dictionary<string, HelpTopic> Index()
        {
            var d = new Dictionary<string, HelpTopic>();
            foreach (HelpTopic t in All) d[t.Id] = t;
            return d;
        }

        /// <summary>The topic of an id, or null.</summary>
        public static HelpTopic Get(string id)
        {
            HelpTopic t;
            return id != null && byId.TryGetValue(id, out t) ? t : null;
        }

        /// <summary>Every topic id, in table order (the "all seen" check of the Help switch).</summary>
        public static IEnumerable<string> AllIds()
        {
            for (int i = 0; i < All.Length; i++) yield return All[i].Id;
        }

        static readonly string[] none = new string[0];
        static readonly string[] shopVip = { ShopSubscription, ShopChests };
        static readonly string[] shopCharacter = { ShopHeroes, ShopPowers };

        /// <summary>
        /// The topic ids to show when a screen is entered. <paramref name="screen"/> is MetaController.CurrentScreen; <paramref name="sub"/> is the tab (Shop: 0 Bundles, 1 VIP, 2 Character, 3 Gems;
        /// Rewards: 0 Main, 1 Daily, 2 Weekly), 0 for screens without tabs. Home and unknown screens give none (no allocation).
        /// </summary>
        public static string[] IdsFor(string screen, int sub)
        {
            switch (screen)
            {
                case "Map": return new[] { Map };
                case "Levels": return new[] { Levels };
                case "Characters": return new[] { Heroes };
                case "Merge": return new[] { Merge };
                case "Powers": return new[] { Powers };
                case "AllHeroes": return new[] { AllHeroes };
                case "Hero": return new[] { HeroSheet };
                case "Leaderboard": return new[] { Leaderboard };
                case "Settings": return new[] { Settings };
                case "Shop":
                    switch (sub)
                    {
                        case 0: return new[] { ShopBundles };
                        case 1: return shopVip;
                        case 2: return shopCharacter;
                        default: return new[] { ShopGems };
                    }
                case "Rewards":
                    return new[] { sub == 1 ? RewardsDaily : (sub == 2 ? RewardsWeekly : RewardsMain) };
                default: return none;
            }
        }
    }
}
