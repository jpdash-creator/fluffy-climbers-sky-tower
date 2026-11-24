using System;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Leaderboard;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// The crown calculation on BOTH phone and cloud, synced both ways (owner 2026-11-17): the phone stamps every slot version with a monotonic UTC time, uploads its breakdown (display only) and merges the cloud
    /// copy back, the newer version per slot wins (the higher crown when a time is unknown), the service's replay numbers win over display-only numbers, a Reset progress that is still waiting is never overwritten by an
    /// old cloud copy, and everything works offline.
    /// </summary>
    public class LeaderboardBreakdownSyncTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);   // Wednesday, week 2026-11-09

        ContentRepository repo;
        MemorySaveStore store;
        ProgressService progress;
        FakeLbTransport net;
        StubLeaderboardAuth auth;
        DateTime now;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            progress = new ProgressService(repo, store, 5);
            now = T0;
            progress.WeekClock = () => now;
            net = new FakeLbTransport();
            auth = new StubLeaderboardAuth();
            LeaderboardHub.ResetForTests();
            LeaderboardHub.Clock = () => now;
        }

        [TearDown]
        public void TearDown()
        {
            LeaderboardHub.ResetForTests();
            LeaderboardHub.AccountModeIsGoogle = null;
        }

        void GoLive()
        {
            LeaderboardHub.Setup(progress, new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" }, auth, net);
            progress.GrantLeaderboardConsent(LeaderboardTime.ToUnix(now));
            auth.SignIn(null);
        }

        static long Unix(DateTime t) { return LeaderboardTime.ToUnix(t); }

        static RunOutcome Run(int score, int mode, int floors = 40, int combo = 3, int[] chains = null) { return new RunOutcome { Completed = true, StarMask = 1, Score = score, Mode = mode, Floors = floors, BestCombo = combo, Chains = chains }; }

        static WeekSlotInfo Cloud(string id, int score, int floors, long at, int[] chains = null)
        {
            int mode = id[id.Length - 1] - '0';
            return new WeekSlotInfo { SlotId = id, Score = score, Floors = floors, At = at, Chains = chains, Crown = CrownScore.FromScore(score, mode), Mode = mode };
        }

        WeekSlotInfo Local(string id)
        {
            foreach (WeekSlotInfo s in progress.LocalBreakdown()) if (s.SlotId == id) return s;
            return null;
        }

        static HttpReply Json(string body, int status = 200) { return new HttpReply { Status = status, Body = body }; }

        // ---------------- phone: monotonic version times ----------------

        [Test]
        public void ARecordedCrownCarriesItsRunNumbersAndAMonotonicUtcStamp()
        {
            progress.ApplyRun("lvl_001_01", Run(100, 2, 40, 3, new[] { 3, 4, 5 }));   // squares 9 + 16 + 25 = 50, floors 40 would not match: chains are stored anyway, the cloud checks them
            WeekSlotInfo s = Local("c.lvl_001_01.2");
            Assert.IsNotNull(s);
            Assert.AreEqual(100, s.Score);
            Assert.AreEqual(40, s.Floors);
            Assert.AreEqual(3, s.Combo);
            Assert.AreEqual(Unix(T0), s.At);
            Assert.AreEqual(CrownScore.FromScore(100, 2), s.Crown);
            int rev = progress.BreakdownRevision;
            Assert.Greater(rev, 0, "a run changed the calculation");

            now = T0.AddHours(-3);   // the phone clock jumps back
            progress.ApplyRun("lvl_001_01", Run(300, 2, 60, 5));
            Assert.Greater(Local("c.lvl_001_01.2").At, Unix(T0), "a newer run never looks older than the one before it");
            Assert.Greater(progress.BreakdownRevision, rev);
        }

        [Test]
        public void TheStampSurvivesTheSaveAndAnOldSaveWithoutItLoads()
        {
            progress.ApplyRun("lvl_001_01", Run(100, 2));
            var again = new ProgressService(repo, store, 5);
            again.WeekClock = () => now;
            Assert.AreEqual(Unix(T0), again.LocalBreakdown()[0].At);
            var old = Newtonsoft.Json.JsonConvert.DeserializeObject<CrownRun>("{\"f\":10,\"c\":2,\"s\":50}");
            Assert.AreEqual(0L, old.At, "a crown from before the sync has an unknown time");
        }

        // ---------------- merge by time ----------------

        [Test]
        public void TheNewerVersionOfASlotWinsOnThePhoneAndAnOlderOneDoesNot()
        {
            progress.ApplyRun("lvl_001_01", Run(100, 2, 40, 3));
            long mine = Local("c.lvl_001_01.2").At;

            // an OLDER cloud version, even with a higher score, is ignored
            Assert.IsFalse(progress.MergeCloudBreakdown("2026-11-09", new[] { Cloud("c.lvl_001_01.2", 900, 80, mine - 100) }));
            Assert.AreEqual(100, Local("c.lvl_001_01.2").Score);

            // a NEWER cloud version (played on another device) replaces it, with its floors and chains, and is saved
            var chains = new[] { 5, 6 };   // 25 + 36 = 61 = 101 - 40
            Assert.IsTrue(progress.MergeCloudBreakdown("2026-11-09", new[] { Cloud("c.lvl_001_01.2", 101, 40, mine + 100, chains) }));
            WeekSlotInfo after = Local("c.lvl_001_01.2");
            Assert.AreEqual(101, after.Score);
            Assert.AreEqual(mine + 100, after.At);
            CollectionAssert.AreEqual(chains, after.Chains);
            Assert.AreEqual(Unix(T0) + 100, new ProgressService(repo, store, 5) { WeekClock = () => now }.LocalBreakdown()[0].At, "saved");

            // the very same version again changes nothing
            Assert.IsFalse(progress.MergeCloudBreakdown("2026-11-09", new[] { Cloud("c.lvl_001_01.2", 101, 40, mine + 100, chains) }));
        }

        [Test]
        public void WhenATimeIsUnknownTheHigherCrownWins()
        {
            progress.Data.WeekCrowns = new WeekCrownData { WeekKey = progress.CurrentWeekKey };
            progress.Data.WeekCrowns.Levels["lvl_001_01"] = new[] { null, null, new CrownRun { Floors = 40, BestCombo = 3, BaseScore = 500, At = 0 }, null, null };
            Assert.IsFalse(progress.MergeCloudBreakdown("2026-11-09", new[] { Cloud("c.lvl_001_01.2", 100, 20, Unix(T0)) }), "a lower crown never replaces a higher one of unknown age");
            Assert.AreEqual(500, Local("c.lvl_001_01.2").Score);
            Assert.IsTrue(progress.MergeCloudBreakdown("2026-11-09", new[] { Cloud("c.lvl_001_01.2", 700, 50, Unix(T0)) }));
            Assert.AreEqual(700, Local("c.lvl_001_01.2").Score);
        }

        [Test]
        public void ANewDeviceOrReinstallGetsEveryCloudSlotAndTheCrownIsRecomputedFromTheScore()
        {
            Assert.AreEqual(0, progress.LocalBreakdown().Count);
            var cloud = new List<WeekSlotInfo>
            {
                Cloud("c.lvl_001_06.1", 600, 50, Unix(T0)),
                Cloud("e.region_001_meadow_springs.4", 2000, 90, Unix(T0)),
                Cloud("t.region_002_mushroom_grove.0", 700, 30, Unix(T0)),
                new WeekSlotInfo { SlotId = "c.lvl_001_07.1", Score = 0, Crown = 999 },        // no score: skipped
                new WeekSlotInfo { SlotId = "junk", Score = 10 }                                // unreadable id: skipped
            };
            cloud[0].Crown = 999999;   // a claimed crown is not trusted
            Assert.IsTrue(progress.MergeCloudBreakdown("2026-11-09", cloud));
            List<WeekSlotInfo> local = progress.LocalBreakdown();
            Assert.AreEqual(3, local.Count);
            Assert.AreEqual(CrownScore.FromScore(600, 1), Local("c.lvl_001_06.1").Crown);
            Assert.IsNotNull(Local("e.region_001_meadow_springs.4"));
            Assert.IsNotNull(Local("t.region_002_mushroom_grove.0"));
            Assert.AreEqual(0, progress.BreakdownRevision, "what came down is not an upload for the next tick");
        }

        [Test]
        public void ADifferentWeekOrAPendingResetIsNeverMerged()
        {
            Assert.IsFalse(progress.MergeCloudBreakdown("2026-11-02", new[] { Cloud("c.lvl_001_01.1", 100, 10, Unix(T0)) }), "last week's copy");
            Assert.AreEqual(0, progress.LocalBreakdown().Count);
            progress.GrantLeaderboardConsent(1);
            progress.ResetAll();
            Assert.IsTrue(progress.LeaderboardWipePending);
            Assert.IsFalse(progress.MergeCloudBreakdown("2026-11-09", new[] { Cloud("c.lvl_001_01.1", 100, 10, Unix(T0)) }), "the cloud copy is old until the delete is confirmed");
            Assert.AreEqual(0, progress.LocalBreakdown().Count);
        }

        // ---------------- hub and client: both ways, offline, reset ----------------

        [Test]
        public void SigningInUploadsThePhoneBreakdownAndMergesTheCloudAnswerBack()
        {
            progress.ApplyRun("lvl_001_01", Run(100, 2));
            long mine = Local("c.lvl_001_01.2").At;
            GoLive();
            net.Handler = (m, u, b) =>
                u.EndsWith("/sync-breakdown")
                    ? Json("{\"weekKey\":\"2026-11-09\",\"receivedAt\":" + Unix(T0) + ",\"count\":2,\"slots\":[" +
                           "{\"id\":\"c.lvl_001_01.2\",\"crown\":1,\"floors\":40,\"score\":100,\"at\":" + mine + ",\"ra\":" + Unix(T0) + "}," +
                           "{\"id\":\"e.region_001_meadow_springs.4\",\"crown\":1,\"floors\":90,\"score\":2000,\"combo\":9,\"at\":" + (mine + 5) + ",\"ra\":" + Unix(T0) + "}]}")
                    : Json("{}");
            LeaderboardHub.SignIn(null);
            Assert.AreEqual(1, net.CountOf("POST https://lb.test/sync-breakdown"));
            int at = net.Calls.FindIndex(c => c.Contains("/sync-breakdown"));
            StringAssert.Contains("\"weekKey\":\"2026-11-09\"", net.Bodies[at]);
            StringAssert.Contains("\"id\":\"c.lvl_001_01.2\"", net.Bodies[at]);
            StringAssert.Contains("\"score\":100", net.Bodies[at]);
            Assert.AreEqual("stub-token", net.Bearers[at], "the Firebase ID token carries the uid; the body has none");
            StringAssert.DoesNotContain("uid", net.Bodies[at]);
            Assert.IsNotNull(Local("e.region_001_meadow_springs.4"), "the slot only the cloud knew came down");
            Assert.AreEqual(2, progress.LocalBreakdown().Count);
            Assert.IsFalse(LeaderboardHub.BreakdownNeedsSync);
            LeaderboardHub.Tick();
            Assert.AreEqual(1, net.CountOf("/sync-breakdown"), "nothing changed: the tick does not upload again");
        }

        [Test]
        public void ANewRunGoesUpOnTheNextTickAndAnOfflinePhoneKeepsItsCalculationAndRetriesLater()
        {
            GoLive();
            net.Handler = (m, u, b) => Json("{\"weekKey\":\"2026-11-09\",\"slots\":[]}");
            LeaderboardHub.SignIn(null);
            int first = net.CountOf("/sync-breakdown");
            Assert.AreEqual(1, first);

            progress.ApplyRun("lvl_001_01", Run(100, 2));
            Assert.IsTrue(LeaderboardHub.BreakdownNeedsSync);
            net.Handler = null;   // offline
            LeaderboardHub.Tick();
            Assert.AreEqual(first + 1, net.CountOf("/sync-breakdown"), "tried once");
            Assert.AreEqual(1, progress.LocalBreakdown().Count, "the phone keeps its own calculation");
            Assert.IsTrue(LeaderboardHub.BreakdownNeedsSync);
            LeaderboardHub.Tick();
            Assert.AreEqual(first + 1, net.CountOf("/sync-breakdown"), "inside the 30 s wait nothing is sent");

            now = now.AddSeconds(31);
            net.Handler = (m, u, b) => Json("{\"weekKey\":\"2026-11-09\",\"slots\":[]}");
            LeaderboardHub.Tick();
            Assert.AreEqual(first + 2, net.CountOf("/sync-breakdown"));
            Assert.IsFalse(LeaderboardHub.BreakdownNeedsSync);
        }

        [Test]
        public void NoConsentSignedOutOrASwitchedOffFeatureSendsNothing()
        {
            progress.ApplyRun("lvl_001_01", Run(100, 2));
            LeaderboardHub.Setup(progress, new LeaderboardConfig { Enabled = true, BaseUrl = "https://lb.test" }, auth, net);
            net.Handler = (m, u, b) => Json("{}");
            LeaderboardHub.SyncBreakdown();   // signed out and no consent
            auth.SignIn(null);
            LeaderboardHub.SyncBreakdown();   // signed in, still no consent
            Assert.AreEqual(0, net.CountOf("/sync-breakdown"));
            LeaderboardHub.Setup(progress, LeaderboardConfig.Off, auth, net);
            LeaderboardHub.SyncBreakdown();
            Assert.AreEqual(0, net.Calls.Count);
            Assert.AreEqual(1, progress.LocalBreakdown().Count, "the popup still has the phone's own calculation");
        }

        [Test]
        public void ResetProgressNeverSyncsWhileTheCloudDeleteIsWaitingAndThenStartsEmpty()
        {
            progress.ApplyRun("lvl_001_01", Run(100, 2));
            GoLive();
            net.Handler = (m, u, b) => Json("{}");
            progress.ResetAll();
            Assert.IsTrue(progress.LeaderboardWipePending);
            net.Handler = null;   // the delete cannot be sent yet
            LeaderboardHub.OnProgressReset();
            LeaderboardHub.SyncBreakdown();
            Assert.AreEqual(0, net.CountOf("/sync-breakdown"), "no upload before the old data is gone");

            net.Handler = (m, u, b) => u.EndsWith("/sync-breakdown") ? Json("{\"weekKey\":\"2026-11-09\",\"slots\":[]}") : Json("{\"deleted\":true}");
            LeaderboardHub.Tick();
            Assert.IsFalse(progress.LeaderboardWipePending);
            Assert.AreEqual(0, progress.LocalBreakdown().Count);
            Assert.GreaterOrEqual(net.CountOf("/delete-my-entries"), 1);
        }

        // ---------------- what the popup shows ----------------

        [Test]
        public void ThePopupListIsTheNewestOfPhoneAndCloudWithTheServiceReplayNumbersOnTop()
        {
            GoLive();
            progress.ApplyRun("lvl_001_01", Run(100, 2));   // only on the phone (replay not available yet)
            progress.StoreLeaderboardSlots(progress.CurrentWeekKey, new List<WeekSlotInfo>
            {
                new WeekSlotInfo { SlotId = "c.lvl_001_02.1", Crown = 777, Floors = 30, Mode = 1, Score = 140 },   // only in the service's replay list
                new WeekSlotInfo { SlotId = "c.lvl_001_01.2", Crown = 5, Floors = 4, Mode = 2, Score = 4 }          // the service replayed this slot: its number wins
            }, new long[] { 782, 782, 0 }, true);
            MyWeekData d = LeaderboardHub.KnownSlots();
            Assert.IsTrue(d.SlotsKnown);
            Assert.AreEqual(2, d.Slots.Count);
            LbSlot a = d.Slots.Find(s => s.SlotId == "c.lvl_001_01.2"), b = d.Slots.Find(s => s.SlotId == "c.lvl_001_02.1");
            Assert.AreEqual(5L, a.Crown, "server replay wins for the numbers");
            Assert.IsTrue(a.Verified);
            Assert.AreEqual(777L, b.Crown);
            Assert.IsTrue(d.TotalsKnown);
        }

        [Test]
        public void TheServiceAnswerCarriesTheCloudCalculationWhichIsMergedIntoThePhoneAndShown()
        {
            GoLive();
            net.Handler = (m, u, b) => Json("{\"weekKey\":\"2026-11-09\",\"totals\":{\"overall\":10,\"levels\":10,\"endless\":0},\"slots\":[{\"slotId\":\"c.lvl_001_01.2\",\"crown\":10,\"floors\":9,\"mode\":2,\"score\":9,\"at\":" + Unix(T0) + "}]," +
                "\"breakdown\":{\"weekKey\":\"2026-11-09\",\"receivedAt\":1,\"slots\":[" +
                "{\"id\":\"c.lvl_001_01.2\",\"crown\":10,\"floors\":9,\"score\":9,\"at\":" + Unix(T0) + ",\"verified\":true}," +
                "{\"id\":\"e.region_001_meadow_springs.4\",\"crown\":1,\"floors\":90,\"score\":2000,\"at\":" + Unix(T0) + "}]}}");
            MyWeekData got = null;
            LeaderboardHub.LoadSlots(x => got = x);
            Assert.IsNotNull(got);
            Assert.AreEqual(2, got.Slots.Count, "the verified slot and the cloud-only one");
            Assert.IsNotNull(Local("e.region_001_meadow_springs.4"), "the newer cloud version is now on the phone too");
            Assert.IsNotNull(got.Display);
        }

        // ---------------- wire format ----------------

        [Test]
        public void TheUploadBodyHasNoUidAndSkipsSlotsWithoutAScore()
        {
            string body = LbBreakdownJson.BuildBody("2026-11-09", new List<WeekSlotInfo>
            {
                new WeekSlotInfo { SlotId = "c.lvl_001_01.2", Crown = 10, Floors = 9, Score = 9, Combo = 2, Chains = new[] { 2 }, At = 123 },
                new WeekSlotInfo { SlotId = "c.lvl_001_02.2", Crown = 10, Floors = 9, Score = 0 }
            });
            StringAssert.Contains("\"weekKey\":\"2026-11-09\"", body);
            StringAssert.Contains("\"chains\":[2]", body);
            StringAssert.DoesNotContain("lvl_001_02", body);
            StringAssert.DoesNotContain("uid", body);
            BreakdownReply r = LbBreakdownJson.ParseReply("{\"weekKey\":\"w\",\"receivedAt\":5,\"slots\":[{\"id\":\"c.a.1\",\"crown\":3,\"chains\":[2,3],\"at\":7,\"ra\":8,\"verified\":true},{\"crown\":1}]}");
            Assert.AreEqual("w", r.WeekKey);
            Assert.AreEqual(1, r.Slots.Count, "a line without an id is skipped");
            Assert.AreEqual(7L, r.Slots[0].At);
            Assert.AreEqual(8L, r.Slots[0].ReceivedAt);
            Assert.IsTrue(r.Slots[0].Verified);
            CollectionAssert.AreEqual(new[] { 2, 3 }, r.Slots[0].Chains);
            Assert.IsNull(LbBreakdownJson.ParseReply("{\"deleted\":true}"));
        }

        [Test]
        public void TheOwnRowPopupKeepsAnOtherLineForAnyGapAgainstTheBoardNumber()
        {
            var slots = new List<LbSlot> { new LbSlot { SlotId = "c.lvl_001_01.2", Crown = 400, Mode = 2 } };
            BreakdownResult r = LeaderboardBreakdown.Build(slots, LbBoards.Overall, repo, 1000L);
            Assert.AreEqual(1000L, r.Total);
            Assert.AreEqual(600L, r.Difference);
            Assert.AreEqual("Other (not itemised)", r.Groups[r.Groups.Count - 1].Lines[0].Label);
            Assert.AreEqual(1000L, r.LineSum());
        }

        // ---------------- Endless and Time Attack: the three best runs of the week travel as parts (owner 2026-11-22) ----------------

        void PlayEndless(int score, int seed, RunKind kind = RunKind.Endless, int mode = 3)
        {
            RecordResult rr;
            progress.FinishEndlessRun(new RunRecord { Kind = kind, RegionId = "region_001_meadow_springs", Floors = score / 2, Ticks = 3000 + seed, LayoutSeed = seed, Mode = mode, Score = score, BestCombo = 3 }, "20261111", out rr);
        }

        [Test]
        public void TheUploadCarriesTheCountedRunsOfAnEndlessSlotAndTheAnswerComesBackWithThem()
        {
            foreach (int score in new[] { 400, 900, 100, 600 }) PlayEndless(score, score);
            progress.ApplyRun("lvl_001_01", Run(100, 2));
            WeekSlotInfo mine = Local("e.region_001_meadow_springs.3");
            long sum = (long)CrownScore.FromScore(900, 3) + CrownScore.FromScore(600, 3) + CrownScore.FromScore(400, 3);
            Assert.AreEqual(sum, mine.Crown);
            Assert.AreEqual(3, mine.Parts.Count);

            string body = LbBreakdownJson.BuildBody("2026-11-09", progress.LocalBreakdown());
            Newtonsoft.Json.Linq.JObject sent = Newtonsoft.Json.Linq.JObject.Parse(body);
            Newtonsoft.Json.Linq.JToken slot = null, level = null;
            foreach (Newtonsoft.Json.Linq.JToken s in (Newtonsoft.Json.Linq.JArray)sent["slots"])
            {
                if ((string)s["id"] == "e.region_001_meadow_springs.3") slot = s;
                if ((string)s["id"] == "c.lvl_001_01.2") level = s;
            }
            Assert.IsNotNull(slot);
            Assert.AreEqual(sum, (long)slot["crown"]);
            Newtonsoft.Json.Linq.JArray parts = (Newtonsoft.Json.Linq.JArray)slot["parts"];
            Assert.AreEqual(3, parts.Count);
            Assert.AreEqual(900, (int)parts[0]["score"]);
            Assert.AreEqual(600, (int)parts[1]["score"]);
            Assert.AreEqual(400, (int)parts[2]["score"]);
            Assert.IsNull(level["parts"], "a level slot has no parts");

            // the answer of the service (the same slot with its parts) parses back into the same shape
            BreakdownReply reply = LbBreakdownJson.ParseReply("{\"weekKey\":\"2026-11-09\",\"receivedAt\":5,\"slots\":[{\"id\":\"e.region_001_meadow_springs.3\",\"crown\":" + sum + ",\"floors\":450,\"score\":900,\"at\":9,"
                + "\"parts\":[{\"crown\":1,\"floors\":450,\"score\":900,\"chains\":[2],\"at\":7},{\"crown\":1,\"floors\":300,\"score\":600,\"at\":9},{\"crown\":1,\"floors\":200,\"score\":400,\"at\":8}]}]}");
            Assert.AreEqual(3, reply.Slots[0].Parts.Count);
            Assert.AreEqual(600, reply.Slots[0].Parts[1].Score);
            CollectionAssert.AreEqual(new[] { 2 }, reply.Slots[0].Parts[0].Chains);
            Assert.IsNull(LbBreakdownJson.ParseReply("{\"weekKey\":\"w\",\"slots\":[{\"id\":\"c.a.1\",\"crown\":3}]}").Slots[0].Parts);
        }

        [Test]
        public void ANewerCloudVersionOfAnEndlessSlotReplacesItsRunListAndAnOlderOneDoesNot()
        {
            PlayEndless(500, 1);
            PlayEndless(300, 2);
            string id = "e.region_001_meadow_springs.3";
            long mine = Local(id).At;
            int rev = progress.BreakdownRevision;

            var older = new WeekSlotInfo { SlotId = id, Parts = new List<WeekSlotPart> { new WeekSlotPart { Score = 9000, Floors = 100, At = mine - 50 }, new WeekSlotPart { Score = 8000, Floors = 100, At = mine - 60 } } };
            Assert.IsFalse(progress.MergeCloudBreakdown("2026-11-09", new[] { older }), "an older version never replaces the phone's, however high");
            Assert.AreEqual(2, progress.EndlessWeekRuns("region_001_meadow_springs", 3).Count);
            Assert.AreEqual(500, progress.EndlessCrownRun("region_001_meadow_springs", 3).BaseScore);

            // a newer version (three runs played on another device): the whole list is taken, the crown recomputed from the scores, a claimed crown ignored
            var newer = new WeekSlotInfo
            {
                SlotId = id, Crown = 999999999,
                Parts = new List<WeekSlotPart> { new WeekSlotPart { Score = 700, Floors = 60, At = mine + 10, Crown = 5 }, new WeekSlotPart { Score = 800, Floors = 70, At = mine + 30 }, new WeekSlotPart { Score = 200, Floors = 20, At = mine + 20 }, new WeekSlotPart { Score = 100, Floors = 10, At = mine + 5 } }
            };
            Assert.IsTrue(progress.MergeCloudBreakdown("2026-11-09", new[] { newer }));
            var runs = progress.EndlessWeekRuns("region_001_meadow_springs", 3);
            Assert.AreEqual(3, runs.Count, "the best three of the cloud's list");
            Assert.AreEqual(800, runs[0].BaseScore);
            Assert.AreEqual(700, runs[1].BaseScore);
            Assert.AreEqual(200, runs[2].BaseScore);
            Assert.AreEqual(CrownScore.FromScore(800, 3) + CrownScore.FromScore(700, 3) + CrownScore.FromScore(200, 3), progress.EndlessWeekCrown("region_001_meadow_springs", 3));
            Assert.AreEqual(CrownScore.FromScore(800, 3), progress.EndlessBestCrown("region_001_meadow_springs", 3), "the Endless pop-up shows the best of them");
            Assert.AreEqual(mine + 30, Local(id).At, "the version time is the newest counted run");
            Assert.AreEqual(rev, progress.BreakdownRevision, "what came down is not an upload for the next tick");

            // the very same version again changes nothing; it is in the save
            Assert.IsFalse(progress.MergeCloudBreakdown("2026-11-09", new[] { Local(id) }));
            Assert.AreEqual(3, new ProgressService(repo, store, 5) { WeekClock = () => now }.EndlessWeekRuns("region_001_meadow_springs", 3).Count, "saved");

            // a cloud slot from before the rule (no parts) reads as one run
            Assert.IsTrue(progress.MergeCloudBreakdown("2026-11-09", new[] { Cloud("t.region_001_meadow_springs.1", 450, 40, mine + 40) }));
            Assert.AreEqual(1, progress.TimeAttackWeekRuns("region_001_meadow_springs", 1).Count);
            Assert.AreEqual(CrownScore.FromScore(450, 1), progress.TimeAttackWeekCrown("region_001_meadow_springs", 1));
        }

        [Test]
        public void TheServiceRunListWinsInThePopupAndItsRunsAreShown()
        {
            GoLive();
            PlayEndless(500, 1);
            net.Handler = (m, u, b) => Json("{\"weekKey\":\"2026-11-09\",\"totals\":{\"overall\":7000,\"levels\":0,\"endless\":7000},"
                + "\"slots\":[{\"slotId\":\"e.region_001_meadow_springs.3\",\"crown\":7000,\"floors\":90,\"mode\":3,\"score\":800,\"at\":" + Unix(T0) + ",\"runs\":[{\"crown\":4000,\"floors\":90,\"score\":800},{\"crown\":3000,\"floors\":70,\"score\":600}]}]}");
            MyWeekData got = null;
            LeaderboardHub.LoadSlots(x => got = x);
            Assert.IsNotNull(got);
            LbSlot s = got.Slots.Find(x => x.SlotId == "e.region_001_meadow_springs.3");
            Assert.AreEqual(7000L, s.Crown, "the service's replay sum wins over the phone's single run");
            Assert.IsTrue(s.Verified);
            Assert.AreEqual(2, s.Runs.Count);
            Assert.AreEqual(4000L, s.Runs[0].Crown);
            BreakdownResult r = LeaderboardBreakdown.Build(got.Slots, LbBoards.Endless, repo, got.Totals[2]);
            Assert.AreEqual(7000L, r.Total);
            Assert.AreEqual(r.Total, r.LineSum());
            CollectionAssert.AreEqual(new long[] { 4000, 3000 }, r.Groups[0].Lines[0].Runs);

            // the answer is kept for the offline popup, with its run list
            MyWeekData known = LeaderboardHub.KnownSlots();
            Assert.AreEqual(2, known.Slots.Find(x => x.SlotId == "e.region_001_meadow_springs.3").Runs.Count);
            Assert.AreEqual(7000L, known.Slots.Find(x => x.SlotId == "e.region_001_meadow_springs.3").Crown);
        }

        // ---------------- the shared merge rules ----------------

        [Test]
        public void SanitizeKeepsTheCloudHonest()
        {
            long start = WeekKeys.StartOfWeekUnix(Unix(T0));
            var raw = new List<WeekSlotInfo>
            {
                new WeekSlotInfo { SlotId = "c.lvl_001_01.2", Score = 100, Floors = 40, Crown = long.MaxValue, At = Unix(T0) + 99999, Chains = new[] { 3, 3 } },
                new WeekSlotInfo { SlotId = "c.lvl_001_02.9", Score = 100 },
                new WeekSlotInfo { SlotId = "c.lvl_001_03.1", Score = 100, At = start - 5 }
            };
            List<WeekSlotInfo> clean = BreakdownMerge.Sanitize(raw, Unix(T0), start, true);
            Assert.AreEqual(2, clean.Count);
            Assert.AreEqual(CrownScore.FromScore(100, 2), clean[0].Crown);
            Assert.AreEqual(Unix(T0), clean[0].At, "a time in the future is clamped to the receipt");
            Assert.IsNull(clean[0].Chains, "chains that do not add up to the score are dropped");
            Assert.AreEqual(start, clean[1].At);
            Assert.AreEqual(Unix(T0), clean[0].ReceivedAt);
        }
    }
}
