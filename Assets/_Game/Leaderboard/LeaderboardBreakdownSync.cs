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

    /// <summary>
    /// Wire format of the crown calculation (owner 2026-11-17): slot objects <c>{ id, crown, floors, score, combo, chains[], at, ra, verified, hero }</c>. An Endless / Time Attack slot (owner 2026-11-22: the
    /// three best runs of the week count) also carries <c>parts: [ { crown, floors, score, combo, chains[], at } ]</c>, its counted runs best first; its crown is their sum.
    /// </summary>
    public static class LbBreakdownJson
    {
        /// <summary>
        /// The upload body: <c>{ "weekKey": ..., "slots": [ { id, crown, floors, score, combo, chains, at, parts? } ] }</c>. The service recomputes every crown from the score and clamps the times; they are sent for
        /// display only.
        /// </summary>
        public static string BuildBody(string weekKey, List<WeekSlotInfo> slots)
        {
            var arr = new JArray();
            if (slots != null)
                foreach (WeekSlotInfo s in slots)
                {
                    if (s == null || s.Score <= 0) continue;   // a slot without its score cannot be shown by the cloud
                    var o = new JObject { ["id"] = s.SlotId, ["crown"] = s.Crown, ["floors"] = s.Floors, ["score"] = s.Score, ["combo"] = s.Combo, ["at"] = s.At };
                    if (s.Chains != null && s.Chains.Length > 0) o["chains"] = new JArray(s.Chains);
                    if (s.Parts != null && s.Parts.Count > 0)
                    {
                        var parts = new JArray();
                        foreach (WeekSlotPart p in s.Parts)
                        {
                            if (p == null || p.Score <= 0) continue;
                            var po = new JObject { ["crown"] = p.Crown, ["floors"] = p.Floors, ["score"] = p.Score, ["combo"] = p.Combo, ["at"] = p.At };
                            if (p.Chains != null && p.Chains.Length > 0) po["chains"] = new JArray(p.Chains);
                            parts.Add(po);
                        }
                        if (parts.Count > 0) o["parts"] = parts;
                    }
                    arr.Add(o);
                }
            return new JObject { ["weekKey"] = weekKey ?? "", ["slots"] = arr }.ToString(Newtonsoft.Json.Formatting.None);
        }

        static int[] ParseChains(JToken t)
        {
            JArray ch = t as JArray;
            if (ch == null || ch.Count == 0) return null;
            var chains = new int[ch.Count];
            for (int i = 0; i < chains.Length; i++) chains[i] = (int)LbJson.Long(ch[i]);
            return chains;
        }

        /// <summary>The counted runs of an Endless / Time Attack slot of an answer (at most a few are read); null when the slot has none.</summary>
        static List<WeekSlotPart> ParseParts(JToken t)
        {
            JArray arr = t as JArray;
            if (arr == null || arr.Count == 0) return null;
            var parts = new List<WeekSlotPart>(BreakdownMerge.TopRuns);
            foreach (JToken p in arr)
            {
                if (parts.Count >= BreakdownMerge.MaxPartsRead) break;
                if (!(p is JObject)) continue;
                parts.Add(new WeekSlotPart
                {
                    Crown = LbJson.Long(p["crown"]), Floors = (int)LbJson.Long(p["floors"]), Score = (int)LbJson.Long(p["score"]), Combo = (int)LbJson.Long(p["combo"]), Chains = ParseChains(p["chains"]), At = LbJson.Long(p["at"])
                });
            }
            return parts.Count == 0 ? null : parts;
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
                list.Add(new WeekSlotInfo
                {
                    SlotId = id, Crown = LbJson.Long(t["crown"]), Floors = (int)LbJson.Long(t["floors"]), Mode = (int)LbJson.Long(t["mode"]), HeroId = LbJson.Str(t["hero"]),
                    Score = (int)LbJson.Long(t["score"]), Combo = (int)LbJson.Long(t["combo"]), Chains = ParseChains(t["chains"]), At = LbJson.Long(t["at"]), ReceivedAt = LbJson.Long(t["ra"]), Verified = LbJson.Bool(t["verified"]),
                    Parts = ParseParts(t["parts"])
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
