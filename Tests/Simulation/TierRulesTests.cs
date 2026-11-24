using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>
    /// Merge tier rules (docs/34 section 2b, docs/44): the rule tables (run bonus, powers), the validators, and the hero table (one absolute physics row per hero,
    /// 25 lines x 5 tiers) held to <see cref="HeroIndex"/>. The old percent chain over the tier below is retired.
    /// </summary>
    public class TierRulesTests
    {
        static readonly CharacterRarity[] Tiers = { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary };

        [Fact]
        public void RuleTablesMatchTheOwnersNumbers()
        {
            // run coins min, max, run gems min, max, powers
            int[][] want =
            {
                new[] { 50, 100, 1, 5, 0 },      // owner Final Change 2 (2026-11-23): powers Uncommon 0, Rare 1, Elite 1, Legendary 2
                new[] { 200, 500, 5, 10, 1 },
                new[] { 500, 1000, 5, 10, 1 },
                new[] { 1000, 2000, 5, 10, 2 },
            };
            for (int i = 0; i < Tiers.Length; i++)
            {
                TierRule r = TierRules.RuleFor(Tiers[i]);
                Assert.NotNull(r);
                Assert.Equal(want[i], new[] { r.RunCoinsMin, r.RunCoinsMax, r.RunGemsMin, r.RunGemsMax, r.PowerCount });
                Assert.Equal(CharacterDefs.PowerCount[(int)Tiers[i]], r.PowerCount);
                Assert.Equal(CharacterDefs.CooldownSeconds[(int)Tiers[i]], r.CooldownSeconds);
            }
            Assert.Null(TierRules.RuleFor(CharacterRarity.Common));
            Assert.Equal(new[] { 0, 0, 1, 1, 2 }, CharacterDefs.PowerCount);
            Assert.Equal("Elite", CharacterDefs.RarityNames[(int)CharacterRarity.Epic]);
            for (int i = 1; i < 4; i++) Assert.True(CharacterDefs.CooldownSeconds[i + 1] < CharacterDefs.CooldownSeconds[i], "cooldown shortens with rarity");
        }

        [Fact]
        public void PowerChoicesAreTheOwnersListsAndUseOnlyImplementedPowers()
        {
            var implemented = new HashSet<PowerUp> { PowerUp.Flight, PowerUp.Rocket, PowerUp.DoubleJump, PowerUp.WallStick, PowerUp.Shield, PowerUp.SafetyNet, PowerUp.Glide, PowerUp.WidePlatforms, PowerUp.SlowTime };
            // owner Final Change 2 (2026-11-23): Rare ONE of Wide Platforms, Shield, Safety Net, Wall Stick, Glide Cape, Slow Time; Elite ONE of Safety Net, Double Jump, Glide Cape, Slow Time, Flight, Rocket; Legendary TWO of the Elite list
            Assert.Equal(new[] { PowerUp.WidePlatforms, PowerUp.Shield, PowerUp.SafetyNet, PowerUp.WallStick, PowerUp.Glide, PowerUp.SlowTime }, TierRules.PowerChoices[(int)CharacterRarity.Rare]);
            Assert.Equal(new[] { PowerUp.SafetyNet, PowerUp.DoubleJump, PowerUp.Glide, PowerUp.SlowTime, PowerUp.Flight, PowerUp.Rocket }, TierRules.PowerChoices[(int)CharacterRarity.Epic]);
            Assert.Equal(TierRules.PowerChoices[(int)CharacterRarity.Epic], TierRules.PowerChoices[(int)CharacterRarity.Legendary]);
            Assert.Empty(TierRules.PowerChoices[(int)CharacterRarity.Common]);
            Assert.Empty(TierRules.PowerChoices[(int)CharacterRarity.Uncommon]);
            foreach (CharacterRarity r in Tiers)
            {
                PowerUp[][] combos = TierRules.PowerCombos[(int)r];
                Assert.Equal(r == CharacterRarity.Uncommon ? 1 : (r == CharacterRarity.Legendary ? 15 : 6), combos.Length);
                Assert.Equal(combos.Length, combos.Select(c => string.Join("+", c)).Distinct().Count());
                foreach (PowerUp[] combo in combos)
                {
                    Assert.Equal(CharacterDefs.PowerCount[(int)r], combo.Length);
                    Assert.Equal(combo.Length, new HashSet<PowerUp>(combo).Count);
                    foreach (PowerUp p in combo) { Assert.Contains(p, implemented); Assert.Contains(p, TierRules.PowerChoices[(int)r]); }
                    Assert.Null(TierRules.ValidatePowers("momo", r, combo));
                }
                Assert.Equal(CharacterDefs.PowerCount[(int)r], TierRules.PowersFor("momo", r).Length);   // the default combination
                Assert.Equal(combos[0], TierRules.PowersFor("momo", r));
            }
            Assert.Empty(TierRules.PowersFor("momo", CharacterRarity.Common));
            Assert.NotNull(TierRules.ValidatePowers("momo", CharacterRarity.Rare, new[] { PowerUp.DoubleJump }));        // Double Jump is not on the Rare list
            Assert.NotNull(TierRules.ValidatePowers("momo", CharacterRarity.Uncommon, new[] { PowerUp.SafetyNet }));     // Uncommon has no power
            Assert.NotNull(TierRules.ValidatePowers("momo", CharacterRarity.Legendary, new[] { PowerUp.Flight }));       // Legendary has two
            Assert.NotNull(TierRules.ValidatePowers("momo", CharacterRarity.Legendary, new[] { PowerUp.Flight, PowerUp.Flight }));
        }

        [Fact]
        public void EveryDefinedTierRowObeysItsRunBonusRuleAndHasItsPowers()
        {
            Assert.Equal(5 + 19 * 5, MergeTierDefs.Rows.Length);   // 5 Uncommon heroes + Regions 1 to 5 (Momo/Capi/Otto/Bonbon/Bao Rare/Elite/Legendary, 4 new heroes x 4 tiers each per region)
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                Assert.Null(TierRules.Validate(row));
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                Assert.NotNull(spec);
                Assert.Null(TierRules.ValidatePowers(row.LineId, row.Rarity, spec.Powers));
                Assert.Equal(CharacterDefs.PowerCount[(int)row.Rarity], spec.Powers.Length);
            }
        }

        [Fact]
        public void TheRunBonusValidatorRejectsRowsOutsideTheRule()
        {
            Assert.Null(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 50, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 55, RunGemsAdd = 1 }));    // not a step of 10
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 110, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 40, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Uncommon, RunCoinsAdd = 50, RunGemsAdd = 6 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "t", Rarity = CharacterRarity.Common, RunCoinsAdd = 50, RunGemsAdd = 1 }));
            Assert.NotNull(TierRules.Validate(null));
            Assert.Null(TierRules.Validate(new TierRow { Id = "l", Rarity = CharacterRarity.Legendary, RunCoinsAdd = 2000, RunGemsAdd = 10 }));
            Assert.NotNull(TierRules.Validate(new TierRow { Id = "l", Rarity = CharacterRarity.Legendary, RunCoinsAdd = 2010, RunGemsAdd = 10 }));
        }

        // ---- the hero table ----

        [Fact]
        public void TheTableHasEveryHeroExactlyOnce_25LinesTimesFiveTiers()
        {
            Assert.Equal(25, TierRules.Lines.Length);
            Assert.Equal(25 * 5, HeroTable.Rows.Length);
            Assert.Equal(HeroTable.Rows.Length, HeroTable.Rows.Select(r => r.Id).Distinct().Count());
            foreach (string line in TierRules.Lines)
                for (int t = 0; t < HeroIndex.Tiers; t++)
                {
                    string id = line + HeroTable.TierSuffix[t];
                    Assert.Single(HeroTable.Rows, r => r.Id == id);
                    HeroRow row = HeroTable.Of(line, t);
                    Assert.NotNull(row);
                    Assert.Equal(id, row.Id);
                    Assert.Equal(line, row.Line);
                    Assert.Equal(t, row.Tier);
                }
        }

        [Fact]
        public void EveryCommonAndTierIdExistsExactlyOnceInTheCharacterList()
        {
            // the data accessor still holds all 125 heroes; the runtime list holds the 25 ACTIVE ones only (owner 2026-11-09)
            Assert.Equal(125, CharacterDefs.AllIncludingInactive.Length);
            Assert.Equal(125, CharacterDefs.AllIncludingInactive.Select(c => c.Id).Distinct().Count());
            Assert.Equal(25, CharacterDefs.All.Length);
            Assert.Equal(25, CharacterDefs.All.Select(c => c.Id).Distinct().Count());
            foreach (HeroRow row in HeroTable.Rows)
            {
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                Assert.NotNull(spec);
                Assert.Equal(row.Tier, (int)spec.Rarity);
                Assert.Equal(row.Line, spec.LineId);
                Assert.Equal(row.Tier > 0, spec.IsMergeTier);
            }
        }

        [Fact]
        public void EveryActiveTableRowObeysTheBandsOverItsLinesCommon()
        {
            // owner Final Change 2: the 25 heroes of the five active lines are bands over the line's Common (Commons over Momo Common); the 100 rows of the inactive lines are data only
            foreach (HeroRow row in HeroTable.Rows)
                if (row.Active) Assert.Null(TierRules.ValidatePhysics(row));
            Assert.Empty(HeroTable.Legacy);
        }

        [Fact]
        public void EveryTableRowStaysInsideTheLegalLimitsEvenWhileItIsLegacy()
        {
            foreach (HeroRow row in HeroTable.Rows)
            {
                Assert.InRange(row.LaunchTenths, CharacterStats.JumpLaunchMinTenths, HeroIndex.LaunchMaxTenths);
                Assert.InRange(row.Speed, HeroIndex.SpeedMin, HeroIndex.SpeedMax);
                Assert.InRange(row.Control, HeroIndex.ControlMin, HeroIndex.ControlMax);
                Assert.InRange(row.Wall, HeroIndex.WallMin, HeroIndex.WallMax);
            }
        }

        [Fact]
        public void TheIndexValidatorRejectsRowsOutsideTheBandsAndLimits()
        {
            HeroPhysics m = HeroIndex.MomoCommon;
            Assert.Null(TierRules.ValidatePhysics(new HeroRow("x", "momo", 0, m.LaunchTenths, m.Speed, m.Control, m.Wall)));
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "momo", 0, m.LaunchTenths + 20, m.Speed, m.Control, m.Wall)));   // Common is -3 to +3 percent of Momo
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "momo", 1, m.LaunchTenths, m.Speed, m.Control, m.Wall)));        // Uncommon is +3 to +7 percent on every stat
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "momo", 4, 228, 13, 30, 100)));
            Assert.NotNull(TierRules.ValidatePhysics(new HeroRow("x", "momo", 5, 228, 11, 41, 100)));   // no such tier
            Assert.NotNull(TierRules.ValidatePhysics(null));
            Assert.NotNull(HeroIndex.ValidateLimits(new HeroPhysics(HeroIndex.LaunchMinTenths - 1, 9, 29, 77)));
            Assert.Null(HeroIndex.ValidateLimits(new HeroPhysics(HeroIndex.LaunchMinTenths, 9, 29, 77)));
            Assert.Equal(184, HeroIndex.LaunchMinTenths);
        }

        [Fact]
        public void MomoCommonIsTheAnchorAndTheTableAndTheGameAgree()
        {
            HeroRow row = HeroTable.Of("momo", 0);
            HeroPhysics want = HeroIndex.MomoCommon;
            Assert.Equal(want.LaunchTenths, row.LaunchTenths);
            Assert.Equal(want.Speed, row.Speed);
            Assert.Equal(want.Control, row.Control);
            Assert.Equal(want.Wall, row.Wall);
            CharacterStats s = CharacterDefs.Momo.Stats;
            Assert.Equal(want.LaunchTenths, s.LaunchTenths);
            Assert.Equal(want.Speed, s.MaxSpeed);
            Assert.Equal(want.Control, s.Control);
            Assert.Equal(want.Wall, s.WallBoostHundredths);
            for (int t = 1; t < HeroIndex.Tiers; t++) Assert.Null(TierRules.ValidatePhysics(HeroTable.Of("momo", t)));   // Momo's tiers are generated like every other line's
        }

        [Fact]
        public void EveryHeroPlaysWithItsTableRow()
        {
            foreach (HeroRow row in HeroTable.Rows)
            {
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                CharacterStats s = spec.Stats;
                Assert.Equal(row.LaunchTenths, s.LaunchTenths);
                Assert.Equal(row.Speed, s.MaxSpeed);
                Assert.Equal(row.Control, s.Control);
                Assert.Equal(row.Wall, s.WallBoostHundredths);
                Assert.Null(spec.IsMergeTier ? s.ValidateTier() : s.Validate());
            }
        }

        [Fact]
        public void ATierHeroKeepsItsCommonHeroFindsAndAddsItsRunBonus()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterStats e = CharacterDefs.GetIncludingInactive(row.Id).Stats, c = CharacterDefs.GetIncludingInactive(row.LineId).Stats;
                Assert.Equal(c.CoinFind, e.CoinFind);
                Assert.Equal(c.GemFind, e.GemFind);
                Assert.Equal(c.RunBonusCoins + row.RunCoinsAdd, e.RunBonusCoins);
                Assert.Equal(c.RunBonusGems + row.RunGemsAdd, e.RunBonusGems);
                Assert.Null(e.ValidateTier());
            }
        }

        [Fact]
        public void TierHeroesShowTheirAbsoluteStatsWithoutAChain()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                ShownStats want = ShownStats.From(spec.Stats, new SimConfig());
                ShownStats got = spec.ShownValues();
                Assert.Equal(want.Jump, got.Jump);
                Assert.Equal(want.Speed, got.Speed);
                Assert.Equal(want.Control, got.Control);
                Assert.Equal(want.WallBounce, got.WallBounce);
                Assert.Equal(want.RunBonusCoins, got.RunBonusCoins);
                Assert.Equal(want.RunBonusGems, got.RunBonusGems);
            }
        }

        static double[] Vals(ShownStats s) { return new[] { s.Jump, s.Speed, s.Control, s.WallBounce }; }

        [Fact]
        public void GainLinesShowOnlyThePositiveChangesBetweenTwoShownBlocks()
        {
            string[] names = { "Jump", "Speed", "Control", "Wall Bounce" };
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                ShownStats common = CharacterDefs.GetIncludingInactive(row.LineId).ShownValues();
                ShownStats shown = CharacterDefs.GetIncludingInactive(row.Id).ShownValues();
                double[] c = Vals(common), u = Vals(shown);
                var expect = new List<string>();
                for (int s = 0; s < 4; s++)
                {
                    int pct = ShownStats.Percent(c[s], u[s]);
                    if (pct > 0) expect.Add(names[s] + " +" + pct + "%");
                }
                expect.Add("Run coins +" + row.RunCoinsAdd);
                expect.Add("Run gems +" + row.RunGemsAdd);
                Assert.Equal(expect, TierRules.GainLines(common, shown));
                Assert.Equal(expect, MergeRules.ShownGainLines(common, shown));
            }
            // a stat that did not rise (or fell) prints nothing
            var a = new ShownStats { Jump = 5.0, Speed = 9.0, Control = 30.0, WallBounce = 7.0 };
            var b = new ShownStats { Jump = 5.0, Speed = 8.0, Control = 33.0, WallBounce = 7.0 };
            Assert.Equal(new List<string> { "Control +10%" }, TierRules.GainLines(a, b));
            Assert.Empty(TierRules.GainLines(null, b));
        }

        [Fact]
        public void TheJumpBandTextReadsTheIndexBands()
        {
            Assert.Equal("-3 to +3% of Momo", TierRules.JumpBandText(CharacterRarity.Common));
            Assert.Equal("+3 to +7% on every stat", TierRules.JumpBandText(CharacterRarity.Uncommon));
            Assert.Equal("+6 to +10% on every stat", TierRules.JumpBandText(CharacterRarity.Rare));
            Assert.Equal("+10 to +14% on every stat", TierRules.JumpBandText(CharacterRarity.Epic));
            Assert.Equal("+14 to +18% on every stat", TierRules.JumpBandText(CharacterRarity.Legendary));
        }

        [Fact]
        public void EffectiveStatsOfEveryTierAreInsideTheLimitsAndJumpRisesWithRarity()
        {
            foreach (TierRow row in MergeTierDefs.Rows)
            {
                CharacterSpec spec = CharacterDefs.GetIncludingInactive(row.Id);
                CharacterSpec common = CharacterDefs.GetIncludingInactive(row.LineId);
                Assert.Null(spec.Stats.ValidateTier());
                Assert.Null(spec.Stats.ValidatePhysics());
                CharacterStats e = spec.Stats, c = common.Stats;
                // the bible's rule: Jump rises with the rarity (the bands do not overlap upwards); Speed and Control are independent draws (the FI gate balances them), so they may be lower than the Common hero's
                Assert.True(e.LaunchTenths >= c.LaunchTenths, row.Id);
                SimConfig cfg = new SimConfig();
                Assert.True(e.Sheet(cfg).FullSpeedJump > cfg.NormalGapMax.Raw / 65536.0, row.Id);
            }
        }

        [Fact]
        public void TheCapKeepsTenthsAndClampsToTheLimits()
        {
            var raw = new CharacterStats { JumpLaunchTenths = 300, MaxSpeed = 20, Control = 90, WallBoostHundredths = 120, RunBonusCoins = 70, RunBonusGems = 3 };
            CharacterStats eff = MergeTierDefs.Cap(raw);
            Assert.Equal(HeroIndex.LaunchMaxTenths, eff.LaunchTenths);
            Assert.Equal(HeroIndex.SpeedMax, eff.MaxSpeed);
            Assert.Equal(HeroIndex.ControlMax, eff.Control);
            Assert.Equal(HeroIndex.WallMax, eff.WallBoostHundredths);
            Assert.Equal(70, eff.RunBonusCoins);
            Assert.Equal(216, MergeTierDefs.Cap(new CharacterStats { JumpLaunchTenths = 216 }).LaunchTenths);
        }

        [Fact]
        public void ShownBaseOfACommonHeroRoundTripsToItsOwnPhysics()
        {
            foreach (string line in TierRules.Lines)
            {
                CharacterStats s = CharacterDefs.GetIncludingInactive(line).Stats;
                CharacterStats back = ShownStats.From(s, new SimConfig()).ToPhysics(new SimConfig());
                Assert.InRange(back.LaunchTenths, s.LaunchTenths - 2, s.LaunchTenths + 2);   // Jump is shown with one decimal, which is about 1 tenth of launch speed
                Assert.Equal(s.MaxSpeed, back.MaxSpeed);
                Assert.Equal(s.Control, back.Control);
                Assert.InRange(back.WallBoostHundredths, s.WallBoostHundredths - 1, s.WallBoostHundredths + 1);   // 1-decimal Wall Bounce over Speed: Otto 5.8 / 8 = 72.5
            }
        }

        [Fact]
        public void BotProofsAndGoldenHashesStayOnTheBaselineConfig()
        {
            // Levels and bot proofs use new SimConfig(); the BARE default stats (CharacterStats.Default, 19.0 / 9 / 30 / 77) reproduce it bit for bit. Momo Common (the Forgiveness Index anchor, owner Final Change 2: 19.2 / 9 / 29 / 77) is a hero row of the table and differs from it.
            SimConfig baseline = new SimConfig();
            SimConfig viaMomo = baseline.Clone();
            viaMomo.JumpBase = Fx.Zero; viaMomo.MaxSpeed = Fx.Zero; viaMomo.Accel = Fx.Zero; viaMomo.WallBoost = Fx.Zero;
            CharacterStats.Default.ApplyTo(viaMomo);
            Assert.Equal(baseline.JumpBase.Raw, viaMomo.JumpBase.Raw);
            Assert.Equal(baseline.MaxSpeed.Raw, viaMomo.MaxSpeed.Raw);
            Assert.Equal(baseline.Accel.Raw, viaMomo.Accel.Raw);
            Assert.Equal(baseline.WallBoost.Raw, viaMomo.WallBoost.Raw);
            Assert.Equal(190, CharacterStats.Default.LaunchTenths);
            Assert.Equal(HeroIndex.MomoCommon.LaunchTenths, CharacterDefs.Momo.Stats.LaunchTenths);
            Assert.Equal(9, CharacterStats.Default.MaxSpeed);
            Assert.Equal(30, CharacterStats.Default.Control);
            Assert.Equal(77, CharacterStats.Default.WallBoostHundredths);
            foreach (TierRow row in MergeTierDefs.Rows) Assert.NotSame(CharacterStats.Default, CharacterDefs.GetIncludingInactive(row.Id).Stats);
        }
    }
}
