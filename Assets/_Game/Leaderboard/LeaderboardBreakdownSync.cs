using System;
using System.Collections.Generic;
using FluffyClimbers.Economy;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>The answer of POST /sync-breakdown: the cloud copy of this week's crown calculation after the merge.</summary>
    public sealed class BreakdownReply
    {
        public string WeekKey = "";
        /// <summary>The service clock (UTC Unix seconds) when it answered.</summary>
        public long ReceivedAt;
        public List<WeekSlotInfo> Slots = new List<WeekSlotInfo>();
    }

    /// <summary>Wire format of the crown calculation (owner 2026-11-17): slot objects <c>{ id, crown, floors, score, combo, chains[], at, ra, verified, hero }</c>.</summary>
    public static class LbBreakdownJson
    {
        /// <summary>The upload body: <c>{ "weekKey": ..., "slots": [ { id, crown, floors, score, combo, chains, at } ] }</c>. The service recomputes the crown from the score and clamps the time; they are sent for display only.</summary>
        public static string BuildBody(string weekKey, List<WeekSlotInfo> slots)
        {
            var arr = new JArray();
            if (slots != null)
                foreach (WeekSlotInfo s in slots)
                {
                    if (s == null || s.Score <= 0) continue;   // a slot without its score cannot be shown by the cloud
                    var o = new JObject { ["id"] = s.SlotId, ["crown"] = s.Crown, ["floors"] = s.Floors, ["score"] = s.Score, ["combo"] = s.Combo, ["at"] = s.At };
                    if (s.Chains != null && s.Chains.Length > 0) o["chains"] = new JArray(s.Chains);
                    arr.Add(o);
                }
            return new JObject { ["weekKey"] = weekKey ?? "", ["slots"] = arr }.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>The slot list of a breakdown answer; entries without an id are skipped. Never null.</summary>
        public static List<WeekSlotInfo> ParseSlots(JArray slots)
        {
            var list = new List<WeekSlotInfo>();
            if (slots == null) return list;
            foreach (JToken t in slots)
            {
                string id = LbJson.Str(t["id"]);
                if (id.Length == 0) continue;
                int[] chains = null;
                JArray ch = t["chains"] as JArray;
                if (ch != null && ch.Count > 0) { chains = new int[ch.Count]; for (int i = 0; i < chains.Length; i++) chains[i] = (int)LbJson.Long(ch[i]); }
                list.Add(new WeekSlotInfo
                {
                    SlotId = id, Crown = LbJson.Long(t["crown"]), Floors = (int)LbJson.Long(t["floors"]), Mode = (int)LbJson.Long(t["mode"]), HeroId = LbJson.Str(t["hero"]),
                    Score = (int)LbJson.Long(t["score"]), Combo = (int)LbJson.Long(t["combo"]), Chains = chains, At = LbJson.Long(t["at"]), ReceivedAt = LbJson.Long(t["ra"]), Verified = LbJson.Bool(t["verified"])
                });
            }
            return list;
        }

        /// <summary>The answer of POST /sync-breakdown; null when it is not one (no week key).</summary>
        public static BreakdownReply ParseReply(string json)
        {
            JObject o = LbJson.Parse(json);
            if (o == null || o["weekKey"] == null || !(o["slots"] is JArray)) return null;
            return new BreakdownReply { WeekKey = LbJson.Str(o["weekKey"]), ReceivedAt = LbJson.Long(o["receivedAt"]), Slots = ParseSlots(o["slots"] as JArray) };
        }
    }
}
