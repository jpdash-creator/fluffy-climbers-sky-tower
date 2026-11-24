using System;
using System.Collections.Generic;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Per-player hero stats AND powers (owner 2026-11-12, docs/44 section 11; powers added by owner Final Change 2, 2026-11-23, docs/44 section 14). Every tier hero (Uncommon, Rare, Elite, Legendary of EVERY active line, Momo's four upper tiers included)
    /// has a POOL of variants. A variant is ONE integer: <c>variant = statIndex + StatPoolSize x comboIndex</c>. The STAT part indexes the hero's stat rows (entry 0 is the hero's row in <see cref="HeroTable"/>; entries 1.. are the generated
    /// <see cref="HeroVariants"/>, every one measured inside the tier's Forgiveness Index window over all shipped levels, Tests/Simulation/HeroVariantGeneratorTests.cs, checked again by HeroVariantTests). The POWER part indexes the tier's power
    /// combinations (<see cref="TierRules.PowerCombos"/>: Rare one of six, Elite one of six, Legendary two of six = 15 pairs, Uncommon none). Powers are not in the Forgiveness Index, so every combination is legal with every stat row.
    /// Variant 0 is the table row with the default combination (what a static spec carries). Two players holding the same hero see slightly different numbers and different powers, never a stronger or weaker hero.
    /// The Common hero of a line (and Momo Common, the 19.0 reference) is NOT rolled: it has one fixed row and no power.
    /// The variant is dealt for EVERY COPY (owner 2026-11-17: one roll per copy), when that copy is obtained, by <see cref="Pick"/>: a pure integer function of (account seed, hero id, roll counter), so a server recomputes it from the
    /// per-account seed. Pure C#, no engine types, no clock, no floats: the .NET test project and a future server compile this file as it is.
    /// </summary>
    public static class HeroRolls
    {
        static readonly Dictionary<string, HeroRow[]> pools = BuildPools();
        static readonly Dictionary<string, Built> built = new Dictionary<string, Built>();
        static readonly Dictionary<string, CharacterSpec> specs = new Dictionary<string, CharacterSpec>();
        static readonly PowerUp[] NoPowers = new PowerUp[0];

        sealed class Built { public CharacterStats Stats; public ShownStats Shown; }

        static Dictionary<string, HeroRow[]> BuildPools()
        {
            var variants = new Dictionary<string, List<HeroRow>>();
            foreach (HeroRow r in HeroVariants.Rows)
            {
                List<HeroRow> l;
                if (!variants.TryGetValue(r.Id, out l)) { l = new List<HeroRow>(32); variants[r.Id] = l; }
                l.Add(r);
            }
            var d = new Dictionary<string, HeroRow[]>();
            foreach (HeroRow r in HeroTable.Rows)
            {
                if (r.Tier < 1 || !r.Active) continue;   // Common stays fixed; inactive lines never exist at runtime
                var list = new List<HeroRow>(1 + 32) { r };
                List<HeroRow> v;
                if (variants.TryGetValue(r.Id, out v)) list.AddRange(v);
                d[r.Id] = list.ToArray();
            }
            return d;
        }

        /// <summary>True for a hero whose stats are rolled per player (an active tier hero, Uncommon or above).</summary>
        public static bool IsRollable(string heroId) { return heroId != null && pools.ContainsKey(heroId); }

        /// <summary>How many STAT rows the hero has (table row included): 0 for a hero that is not rolled.</summary>
        public static int StatPoolSize(string heroId)
        {
            HeroRow[] p;
            return heroId != null && pools.TryGetValue(heroId, out p) ? p.Length : 0;
        }

        /// <summary>How many power combinations the hero's tier rolls (1 for Uncommon: none; 6 for Rare and Elite; 15 for Legendary); 1 for a hero that is not rolled.</summary>
        public static int ComboCount(string heroId)
        {
            HeroRow[] p;
            return heroId != null && pools.TryGetValue(heroId, out p) ? TierRules.ComboCount(p[0].Tier) : 1;
        }

        /// <summary>How many variants the hero has: stat rows x power combinations. 0 for a hero that is not rolled.</summary>
        public static int PoolSize(string heroId) { return StatPoolSize(heroId) * ComboCount(heroId); }

        /// <summary>The stat row index (0 = the table row) of a variant. 0 for an index outside the pool.</summary>
        public static int StatIndex(string heroId, int variant)
        {
            int m = StatPoolSize(heroId);
            return m > 0 && IsValidVariant(heroId, variant) ? variant % m : 0;
        }

        /// <summary>The power combination index (0 = the default combination) of a variant. 0 for an index outside the pool.</summary>
        public static int ComboIndex(string heroId, int variant)
        {
            int m = StatPoolSize(heroId);
            return m > 0 && IsValidVariant(heroId, variant) ? variant / m : 0;
        }

        /// <summary>The variant index that has the given stat row and power combination (the inverse of <see cref="StatIndex"/> and <see cref="ComboIndex"/>). -1 when out of range.</summary>
        public static int Compose(string heroId, int statIndex, int comboIndex)
        {
            int m = StatPoolSize(heroId);
            if (m == 0 || statIndex < 0 || statIndex >= m || comboIndex < 0 || comboIndex >= ComboCount(heroId)) return -1;
            return statIndex + m * comboIndex;
        }

        /// <summary>The physics row of a variant (0 = the table row). An index outside the pool gives the table row; null for a hero that is not rolled.</summary>
        public static HeroRow Row(string heroId, int variant)
        {
            HeroRow[] p;
            if (heroId == null || !pools.TryGetValue(heroId, out p)) return null;
            return p[StatIndex(heroId, variant)];
        }

        /// <summary>True when the index is a real entry of the hero's pool.</summary>
        public static bool IsValidVariant(string heroId, int variant) { return variant >= 0 && variant < PoolSize(heroId); }

        /// <summary>
        /// The hero powers of a variant, in list order: none for a hero that is not rolled or for Uncommon, one for Rare and Elite, two for Legendary. The array is shared: callers must not change it.
        /// An index outside the pool gives the default combination.
        /// </summary>
        public static PowerUp[] PowersFor(string heroId, int variant)
        {
            HeroRow[] p;
            if (heroId == null || !pools.TryGetValue(heroId, out p)) return NoPowers;
            return TierRules.PowerCombos[p[0].Tier][ComboIndex(heroId, variant)];
        }

        static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>
        /// THE roll: the variant index of a hero for an account. Pure and repeatable: variant = f(accountSeed, heroId, counter), splitmix64 over the seed, the FNV hash of the hero id and the counter.
        /// Uniform over the pool (0 .. <see cref="PoolSize"/> - 1), so the stats and the power(s) are drawn together and independently. 0 for a hero that is not rolled.
        /// </summary>
        public static int Pick(int accountSeed, string heroId, int counter)
        {
            int size = PoolSize(heroId);
            if (size <= 1) return 0;
            unchecked
            {
                ulong x = Mix(((ulong)(uint)accountSeed << 32) ^ (ulong)(uint)HeroIndex.Hash(heroId) ^ 0x48524F4C4C53UL);
                x = Mix(x ^ ((ulong)(uint)counter * 0xD6E8FEB86659FD93UL));
                return (int)(x % (ulong)size);
            }
        }

        /// <summary>
        /// The effective stats of a hero for a variant: the hero's own spec stats (flat finds, run bonus) with the four physics numbers of the variant's stat row, limited by the safety cap
        /// (<see cref="MergeTierDefs.Cap"/>). A variant on stat row 0 and any hero that is not rolled return the spec's own stats object. Cached, so a call in a game loop allocates nothing after the first.
        /// </summary>
        public static CharacterStats StatsFor(CharacterSpec spec, int variant)
        {
            if (spec == null) return CharacterStats.Default;
            int s = StatIndex(spec.Id, variant);
            if (s <= 0) return spec.Stats ?? CharacterStats.Default;
            return Resolve(spec, s).Stats;
        }

        /// <summary>The numbers the hero sheet prints for the variant (same rules as <see cref="StatsFor"/>); the spec's own <c>Shown</c> for stat row 0. May be null for a spec without it.</summary>
        public static ShownStats ShownFor(CharacterSpec spec, int variant)
        {
            if (spec == null) return null;
            int s = StatIndex(spec.Id, variant);
            if (s <= 0) return spec.Shown;
            return Resolve(spec, s).Shown;
        }

        /// <summary>
        /// The hero as ONE copy plays it: the spec with that variant's stats, shown stats and powers. The spec itself when the variant is 0 or the hero is not rolled;
        /// otherwise a cached copy, so the run, the hero sheet and the server replay all read <c>Powers</c> from the same kind of object. Allocation-free after the first call per (hero, variant).
        /// </summary>
        public static CharacterSpec Rolled(CharacterSpec spec, int variant)
        {
            if (spec == null || variant == 0 || !IsRollable(spec.Id) || !IsValidVariant(spec.Id, variant)) return spec;
            string key = spec.Id + "#" + variant;
            lock (specs)
            {
                CharacterSpec c;
                if (specs.TryGetValue(key, out c)) return c;
                c = new CharacterSpec
                {
                    Id = spec.Id, Name = spec.Name, Rarity = spec.Rarity, Region = spec.Region, RegionStarter = spec.RegionStarter, GemPrice = spec.GemPrice,
                    Powers = PowersFor(spec.Id, variant), Stats = StatsFor(spec, variant), Shown = ShownFor(spec, variant),
                    Line = spec.Line, ArtId = spec.ArtId, FallbackArtId = spec.FallbackArtId, IsMergeTier = spec.IsMergeTier,
                };
                specs[key] = c;
                return c;
            }
        }

        static Built Resolve(CharacterSpec spec, int statIndex)
        {
            string key = spec.Id + "#" + statIndex;
            lock (built)
            {
                Built b;
                if (built.TryGetValue(key, out b)) return b;
                HeroRow r = pools[spec.Id][statIndex];
                CharacterStats own = spec.Stats ?? CharacterStats.Default;
                CharacterStats stats = MergeTierDefs.Cap(new CharacterStats
                {
                    JumpLaunchTenths = r.LaunchTenths,
                    MaxSpeed = r.Speed,
                    Control = r.Control,
                    WallBoostHundredths = r.Wall,
                    CoinFind = own.CoinFind,
                    GemFind = own.GemFind,
                    RunBonusCoins = own.RunBonusCoins,
                    RunBonusGems = own.RunBonusGems,
                });
                b = new Built { Stats = stats, Shown = ShownStats.From(stats, new SimConfig()) };
                built[key] = b;
                return b;
            }
        }
    }
}
