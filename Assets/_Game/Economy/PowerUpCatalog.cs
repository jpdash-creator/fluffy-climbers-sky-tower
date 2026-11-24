using System.Collections.Generic;
using FluffyClimbers.Simulation;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.Economy
{
    /// <summary>One sellable power-up (docs/15 section 3.2, Content/economy/powerups.json). Effect numbers live in SimConfig.</summary>
    public sealed class PowerUpDef
    {
        public string Id;
        public PowerUp Type;
        public int Order;
        public string Name;
        public string Description;
        /// <summary>"button" or "auto".</summary>
        public string Trigger;
        public int DurationSec;
        public int Uses;
        public int CoinPrice;
        public int GemPrice;
        public int UnlockRegion;
        /// <summary>False for ids that are reserved by the data format but have no simulation feature yet; they are hidden everywhere.</summary>
        public bool Implemented;
        public string Icon;
    }

    public enum PowerUpCurrency
    {
        Coins,
        Gems
    }

    public enum PowerUpBuyResult
    {
        Ok,
        NotEnoughCoins,
        NotEnoughGems,
        /// <summary>The region that unlocks this power-up has not been reached.</summary>
        Locked,
        Invalid
    }

    /// <summary>
    /// The power-up table. The runtime uses the built-in copy below (no manifest change needed); a unit test keeps it identical to
    /// Content/economy/powerups.json, which is the editable source (and is parsed by FromJson for tools and remote overrides).
    /// </summary>
    public sealed class PowerUpCatalog
    {
        readonly List<PowerUpDef> all = new List<PowerUpDef>();
        readonly Dictionary<string, PowerUpDef> byId = new Dictionary<string, PowerUpDef>();
        readonly List<PowerUpDef> implemented = new List<PowerUpDef>();

        public IReadOnlyList<PowerUpDef> All { get { return all; } }
        /// <summary>Only the power-ups that exist in the simulation, in shop order.</summary>
        public IReadOnlyList<PowerUpDef> Implemented { get { return implemented; } }

        public PowerUpCatalog(IEnumerable<PowerUpDef> defs)
        {
            foreach (PowerUpDef d in defs)
            {
                all.Add(d);
                byId[d.Id] = d;
            }
            all.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (PowerUpDef d in all) if (d.Implemented) implemented.Add(d);
        }

        public PowerUpDef Get(string id)
        {
            PowerUpDef d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        public PowerUpDef Get(PowerUp type)
        {
            return Get(PowerUps.IdOf(type));
        }

        // type, display name, description, trigger, duration s per tap, uses per tap, coins, gems (gem price = coins / 20), implemented.
        // Prices (owner Final Change 1, 2026-11-22): Wall Stick, Shield, Wide Platforms 2,000 coins / 100 gems; Safety Net, Double Jump, Glide Cape, Slow Time 3,000 / 150; Flight, Rocket 4,000 / 200
        // (before: 1,000 / 50 and 1,500 / 75). The three unimplemented rows keep their old numbers.
        // Rework 2026-10-31: a tap while active adds the full duration onto the remaining time; no stock cap, no loadout, no carry cap.
        static readonly object[][] Rows =
        {
            new object[] { PowerUp.Flight, "Flight", "Fly upward for 10 seconds and steer left and right. Gongs cannot hurt you. Tap again to add 10 more seconds.", "button", 10, 1, 4000, 200, true },
            new object[] { PowerUp.Rocket, "Rocket", "Auto-climb 20 floors in about 4 seconds. Gongs cannot hurt you. Tap again to add 20 more floors. Stops before the last floors of a level.", "button", 4, 1, 4000, 200, true },
            new object[] { PowerUp.DoubleJump, "Double Jump", "For 20 seconds you can jump once more in mid-air. Tap again to add 20 seconds.", "button", 20, 1, 3000, 150, true },
            new object[] { PowerUp.WallStick, "Wall Stick", "For 20 seconds you stick to the side walls for up to 1.5 seconds. Jump to leap off. Tap again to add 20 seconds.", "button", 20, 1, 2000, 100, true },
            new object[] { PowerUp.Shield, "Shield", "Gongs cannot hurt you for 20 seconds. Tap again to add 20 seconds.", "button", 20, 1, 2000, 100, true },
            new object[] { PowerUp.SafetyNet, "Safety Net", "Arm it any time before a fall. The next time you fall, it jumps you up 3 floors. Arm the next one after a rescue. Not timed.", "button", 0, 1, 3000, 150, true },
            new object[] { PowerUp.Glide, "Glide Cape", "For 20 seconds the whole game slows to half speed after every jump until you land, so you can steer to the next platform. Tap again to add 20 seconds.", "button", 20, 1, 3000, 150, true },
            new object[] { PowerUp.WidePlatforms, "Wide Platforms", "For 20 seconds every platform is 20 percent wider to land on. Tap again to add 20 seconds.", "button", 20, 1, 2000, 100, true },
            new object[] { PowerUp.SlowTime, "Slow Time", "The rising danger zone moves 50 percent slower for 30 seconds. Tap again to add 30 seconds.", "button", 30, 1, 3000, 150, true },
            new object[] { PowerUp.SpringBoots, "Spring Boots", "The next 3 jumps are 20 percent higher.", "button", 0, 3, 1000, 0, false },
            new object[] { PowerUp.CoinDoubler, "Coin Doubler", "Doubles the coins paid at the end of this level.", "button", 0, 1, 120, 0, false },
            new object[] { PowerUp.StarSeeker, "Star Seeker", "Arrows at the screen edge point to stars you have not collected yet.", "button", 12, 1, 800, 0, false },
        };

        public static PowerUpCatalog BuiltIn()
        {
            var list = new List<PowerUpDef>(Rows.Length);
            int order = 1;
            foreach (object[] r in Rows)
            {
                var type = (PowerUp)r[0];
                list.Add(new PowerUpDef
                {
                    Id = PowerUps.IdOf(type), Type = type, Order = order++, Name = (string)r[1], Description = (string)r[2],
                    Trigger = (string)r[3], DurationSec = (int)r[4], Uses = (int)r[5],
                    CoinPrice = (int)r[6], GemPrice = (int)r[7], UnlockRegion = 1, Implemented = (bool)r[8], Icon = PowerUps.IdOf(type)
                });
            }
            return new PowerUpCatalog(list);
        }

        /// <summary>Parses Content/economy/powerups.json (an array). Entries with an unknown id are skipped.</summary>
        public static PowerUpCatalog FromJson(string json)
        {
            var list = new List<PowerUpDef>();
            JArray arr = JArray.Parse(json);
            foreach (JToken t in arr)
            {
                string id = (string)t["id"];
                PowerUp type = PowerUps.FromId(id);
                if (type == PowerUp.None) continue;
                JToken price = t["price"];
                list.Add(new PowerUpDef
                {
                    Id = id, Type = type, Order = (int?)t["order"] ?? 0, Name = (string)t["name"], Description = (string)t["description"],
                    Trigger = (string)t["trigger"] ?? "button", DurationSec = (int?)t["durationSec"] ?? 0, Uses = (int?)t["uses"] ?? 1,
                    CoinPrice = price != null ? ((int?)price["coins"] ?? 0) : 0, GemPrice = price != null ? ((int?)price["gems"] ?? 0) : 0,
                    UnlockRegion = (int?)t["unlockRegion"] ?? 1, Implemented = (bool?)t["implemented"] ?? false, Icon = (string)t["icon"]
                });
            }
            return new PowerUpCatalog(list);
        }

        /// <summary>
        /// Price of <paramref name="quantity"/> units: unit price x quantity, with the bundle discounts of docs/15 (5 units -10 percent,
        /// 10 or more units -20 percent), rounded half up. 0 when the currency is not offered for this power-up.
        /// </summary>
        public static int PriceFor(PowerUpDef def, PowerUpCurrency currency, int quantity)
        {
            if (def == null || quantity <= 0) return 0;
            long unit = currency == PowerUpCurrency.Coins ? def.CoinPrice : def.GemPrice;
            if (unit <= 0) return 0;
            int percent = quantity >= 10 ? 80 : (quantity >= 5 ? 90 : 100);
            long total = unit * quantity * percent;
            long price = (total + 50) / 100;
            return price > int.MaxValue ? int.MaxValue : (int)price;
        }
    }
}
