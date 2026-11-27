using System;
using System.Collections.Generic;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>The result of tapping a Weekly Prize card.</summary>
    public enum ClaimStatus { Granted, AlreadyClaimed, Busy, Offline, NotEligible, Failed }

    public sealed class ClaimOutcome
    {
        public ClaimStatus Status;
        /// <summary>What was granted (coins, power-ups, hero cards) when <see cref="Status"/> is Granted; null otherwise.</summary>
        public RewardClaimOutcome Reward;
        /// <summary>Server error code of a refusal ("week_open", "not_eligible" ...), "" otherwise.</summary>
        public string Code = "";
    }

    /// <summary>
    /// The one place that knows the weekly leaderboard state of the app (static, like AccountSync): the switch, the sign-in, the run tickets, the offline upload queue, the own standing, the boards and
    /// the prize claims. Everything is asynchronous and silent on failure: a missing network, a signed-out player, a switched-off feature or a refused call only means "not on the boards", never an
    /// error for play. All state is plain fields; the only periodic work is <see cref="Tick"/> (every 2 s, driven by <see cref="LeaderboardRunner"/>). Tests inject the parts with <see cref="Setup"/>.
    /// </summary>
    public static partial class LeaderboardHub
    {
        /// <summary>A ticket is not used when it expires within this many seconds.</summary>
        public const int TicketMarginSeconds = 60;
        /// <summary>The boards and the own standing are re-read at most this often while a screen is open.</summary>
        public const int CacheSeconds = 20;
        const int MaxSignInTries = 5;

        static ProgressService progress;
        static LeaderboardConfig cfg = LeaderboardConfig.Off;
        static ILeaderboardAuth auth;
        static ILeaderboardTransport net;
        static LeaderboardClient client;
        static bool slowTickHooked;

        /// <summary>The clock (UTC). Tests replace it.</summary>
        public static Func<DateTime> Clock = () => DateTime.UtcNow;
        /// <summary>Raised when the standing, a board, the sign-in or the prizes changed. Screens subscribe while they are on screen and unsubscribe when they are destroyed.</summary>
        public static event Action Changed;

        static readonly Dictionary<string, StartTicket> tickets = new Dictionary<string, StartTicket>();
        static readonly HashSet<string> ticketsInFlight = new HashSet<string>();
        static readonly Dictionary<string, BoardData> boards = new Dictionary<string, BoardData>();
        static readonly Dictionary<string, long> boardsAt = new Dictionary<string, long>();
        static readonly HashSet<string> boardsInFlight = new HashSet<string>();
        static MyWeekData myWeek;
        static long myWeekAt;
        static bool myWeekInFlight, sending, claiming, signingIn;
        static string profileKey = "";
        static int signInTries;
        static long nextSignInTry;

        // ---------------- setup ----------------

        /// <summary>Real setup, called once when the menu starts: reads the remote config switch and builds the Firebase auth (or the stub) and the web transport. Safe to call again.</summary>
        public static void Init(ProgressService p, RemoteConfigDef remote, bool forceOn = false)
        {
            LeaderboardConfig c = LeaderboardConfig.From(remote, forceOn);
            bool same = client != null && ReferenceEquals(progress, p) && cfg.Enabled == c.Enabled && cfg.BaseUrl == c.BaseUrl;
            if (!same)
            {
                ILeaderboardAuth a = auth != null && ReferenceEquals(progress, p) && !(c.IsLive && auth is UnavailableLeaderboardAuth) ? auth : (c.IsLive ? LeaderboardServices.CreateAuth() : new UnavailableLeaderboardAuth());
                Setup(p, c, a, net ?? new UnityWebTransport());
            }
            if (!slowTickHooked && cfg.IsLive && UnityEngine.Application.isPlaying)
            {
                slowTickHooked = true;
                LeaderboardRunner.Get().OnTick = Tick;
            }
        }

        /// <summary>Injects the parts (tests and Init). Clears every cache.</summary>
        public static void Setup(ProgressService p, LeaderboardConfig config, ILeaderboardAuth authService, ILeaderboardTransport transport)
        {
            progress = p;
            cfg = config ?? LeaderboardConfig.Off;
            auth = authService ?? new UnavailableLeaderboardAuth();
            net = transport;
            client = net != null ? new LeaderboardClient(auth, net, cfg) : null;
            ClearCaches();
        }

        /// <summary>Tests: forget everything.</summary>
        public static void ResetForTests()
        {
            progress = null; cfg = LeaderboardConfig.Off; auth = null; net = null; client = null;
            fakeOn = false; fakeWeek = null; fakeBoards.Clear();
            Clock = () => DateTime.UtcNow;
            ClearCaches();
        }

        static void ClearCaches()
        {
            tickets.Clear(); ticketsInFlight.Clear(); boards.Clear(); boardsAt.Clear(); boardsInFlight.Clear();
            myWeek = null; myWeekAt = 0; myWeekInFlight = sending = claiming = signingIn = false;
            profileKey = ""; signInTries = 0; nextSignInTry = 0;
            ResetBreakdownState();
        }

        // ---------------- local test boards (Admin "Test 1000 users", development builds) ----------------

        static bool fakeOn;
        static MyWeekData fakeWeek;
        static readonly Dictionary<string, BoardData> fakeBoards = new Dictionary<string, BoardData>();

        /// <summary>Local fake boards are on screen instead of the service boards (nothing is read from or written to Firebase for them; the feature switch is not needed).</summary>
        public static bool FakeActive { get { return fakeOn; } }

        /// <summary>
        /// Turns the local fake boards on: <paramref name="count"/> players on each of the three boards, the player's own local crowns placed at their real rank. Open screens are told to redraw.
        /// </summary>
        public static void InstallFakeBoards(int count = 1000)
        {
            long[] own = new long[3];
            MyWeekData local = LocalSlotsData();
            for (int b = 0; b < 3; b++) own[b] = LeaderboardBreakdown.Build(local.Slots, LbBoards.All[b], null).Total;
            FakeBoardSet set = LeaderboardFake.Build(CurrentWeekKey, own, count);
            fakeBoards.Clear();
            for (int b = 0; b < 3; b++) fakeBoards[LbBoards.All[b]] = set.Boards[b];
            fakeWeek = set.Week;
            fakeOn = true;
            Raise();
        }

        /// <summary>Removes the local fake boards (the real state shows again).</summary>
        public static void ClearFakeBoards()
        {
            fakeOn = false; fakeWeek = null; fakeBoards.Clear();
            Raise();
        }

        /// <summary>Set by the game: warms a run ticket for the run the player is most likely to start next (the selected Endless region or level). Called after a reset, a wipe and every successful profile sync.</summary>
        public static Action WarmTickets;
        static void Warm() { if (CanUpload && WarmTickets != null) WarmTickets(); }

        /// <summary>A /start-run request for exactly this run is still open (a prefetch counts): the run start keeps waiting for it.</summary>
        public static bool TicketInFlight(string kind, string target, int mode) { return ticketsInFlight.Contains(TicketKey(kind, target, mode)); }

        static long Now { get { return LeaderboardTime.ToUnix(Clock()); } }
        static void Raise() { if (Changed != null) Changed(); }

        // ---------------- state ----------------

        /// <summary>The feature is switched on in the remote config, has a real service URL and this build can sign in to it.</summary>
        public static bool Enabled { get { return cfg.IsLive && auth != null && auth.IsAvailable; } }
        public static LeaderboardConfig Config { get { return cfg; } }
        /// <summary>A Firebase user exists: boards can be read and runs uploaded (consent permitting).</summary>
        public static bool IsSignedIn { get { return Enabled && auth.IsReady; } }
        /// <summary>Set by the game: true when the player chose the Google account mode (a guest never takes part, even when Play Games happens to be signed in on the phone). Null = no extra condition.</summary>
        public static Func<bool> AccountModeIsGoogle;
        /// <summary>The Google Play Games account is signed in (the first step of the sign-in) and the player plays with it, not as a guest.</summary>
        public static bool IsGoogleSignedIn { get { return Services.Account != null && Services.Account.IsSignedIn && (AccountModeIsGoogle == null || AccountModeIsGoogle()); } }
        public static bool HasConsent { get { return progress != null && progress.HasLeaderboardConsent; } }
        /// <summary>The player's runs are sent: switched on, signed in, consent given, and playing with the Google account (Unlink stops uploads at once, although the Firebase user stays until the app restarts).</summary>
        public static bool CanUpload { get { return IsSignedIn && HasConsent && client != null && (AccountModeIsGoogle == null || AccountModeIsGoogle()); } }
        public static LbProfile Profile { get { return auth != null ? auth.Profile : new LbProfile(); } }
        public static string Uid { get { return auth != null ? auth.Uid : ""; } }
        public static MyWeekData MyWeek { get { return fakeOn ? fakeWeek : myWeek; } }
        public static bool QueueBusy { get { return sending; } }
        public static int QueuedRuns { get { return progress != null ? progress.LeaderboardQueue.Count : 0; } }

        /// <summary>Time left in the current week (Monday 00:00 UTC).</summary>
        public static TimeSpan WeekRemaining { get { return LeaderboardTime.Remaining(Clock()); } }

        /// <summary>The current week key from the device clock (the service decides with its own clock when a run is received).</summary>
        public static string CurrentWeekKey { get { return LeaderboardTime.WeekKey(Clock()); } }

        // ---------------- sign-in and consent ----------------

        /// <summary>
        /// Signs in to Firebase (Play Games must be signed in already). Silent when called at start, interactive when the player pressed the button; either way done(true) only when a Firebase user exists.
        /// </summary>
        public static void SignIn(Action<bool> done)
        {
            if (!Enabled) { if (done != null) done(false); return; }
            if (auth.IsReady) { AfterSignIn(); if (done != null) done(true); return; }
            if (signingIn) { if (done != null) done(false); return; }
            signingIn = true;
            auth.SignIn((outcome, error) =>
            {
                signingIn = false;
                bool ok = outcome == LbSignInOutcome.Success && auth.IsReady;
                if (ok) { if (!HasConsent && IsGoogleSignedIn && !fakeOn && progress != null) progress.GrantLeaderboardConsent(Now); AfterSignIn(); }
                Raise();
                if (done != null) done(ok);
            });
        }

        static void AfterSignIn()
        {
            RunPendingWipe();   // a Reset progress made offline or signed out: the player's scores are deleted before anything new is sent
            if (!CanUpload) return;
            SyncProfile();
            Pump();
            RefreshMyWeek(true, null);
            SyncBreakdown();   // the phone's crown calculation goes up and the cloud's comes down (a new device or a reinstall gets it here)
        }

        /// <summary>Called when the menu starts: a returning player who accepted the consent is signed in to Firebase silently (retried a few times while Play Games is still signing in).</summary>
        public static void OnMenuStart()
        {
            AutoJoin();
            if (!Enabled || !HasConsent) return;
            TrySilentSignIn();
        }

        static void TrySilentSignIn()
        {
            if (auth.IsReady || signingIn || signInTries >= MaxSignInTries || Now < nextSignInTry) return;
            if (!IsGoogleSignedIn) return;
            signInTries++;
            nextSignInTry = Now + 20;
            SignIn(null);
        }

        /// <summary>
        /// Owner Final Change 4 (2026-11-24): signing in with Google Play Games IS the opt-in. A signed-in player (Google mode, feature on) gets the consent stored silently, so no Join card or popup is
        /// needed and the profile row (0 crowns) is written at once. A guest never joins. Safe to call often; does nothing when consent exists. Returns true when consent was just granted.
        /// </summary>
        public static bool AutoJoin()
        {
            if (fakeOn || progress == null || HasConsent || !Enabled || !IsGoogleSignedIn) return false;
            AcceptConsent();
            return true;
        }

        /// <summary>Stores the consent, writes the profile, sends what is queued (called by <see cref="AutoJoin"/>; the old Join button path is gone).</summary>
        public static void AcceptConsent()
        {
            if (progress == null) return;
            progress.GrantLeaderboardConsent(Now);
            if (auth != null && auth.IsReady) AfterSignIn();
            else if (Enabled) { signInTries = 0; nextSignInTry = 0; TrySilentSignIn(); }
            Raise();
        }

        /// <summary>"Not now": no consent is stored; the popup comes back tomorrow at the earliest.</summary>
        public static void DeclineConsent()
        {
            if (progress != null) progress.DeclineLeaderboardConsent(LeaderboardTime.DayKey(Clock()));
            Raise();
        }

        /// <summary>The consent popup should be shown now (consent missing and not declined today).</summary>
        public static bool ShouldAskConsent { get { return progress != null && progress.ShouldAskLeaderboardConsent(LeaderboardTime.DayKey(Clock())); } }

        static bool wiping;

        /// <summary>
        /// Settings > Reset progress: drops every cached standing, board and ticket and asks the service to delete the player's scores (POST /delete-my-entries). When offline or signed out the request stays in the save
        /// (<c>leaderboardWipePending</c>) and is sent at the next sign-in or slow tick; until it is confirmed no run is uploaded.
        /// </summary>
        public static void OnProgressReset()
        {
            tickets.Clear(); ticketsInFlight.Clear(); boards.Clear(); boardsAt.Clear(); myWeek = null; myWeekAt = 0; profileKey = "";
            Raise();
            RunPendingWipe();
        }

        /// <summary>Sends the queued "delete my entries" request when a wipe is pending and the player is signed in to the service. One request at a time; a failure leaves it pending.</summary>
        public static void RunPendingWipe()
        {
            if (progress == null || !progress.LeaderboardWipePending || wiping || client == null || !IsSignedIn) return;
            wiping = true;
            client.DeleteMyEntries(ok =>
            {
                wiping = false;
                if (!ok) return;
                if (progress != null) progress.ClearLeaderboardWipePending();
                profileKey = ""; SyncProfile(); Warm();   // the wipe may have removed the weekly row: the player is on the boards again (0 crowns) right away, same name and uid
                MarkStale();
                Raise();
                Pump();   // runs played while the wipe was pending go up now
            });
        }

        /// <summary>"Leave leaderboards" (Settings): consent and queue are cleared, the cached standing and tickets are dropped. Play Games itself stays signed in.</summary>
        public static void LeaveLeaderboards()
        {
            if (progress != null) progress.RevokeLeaderboardConsent();
            tickets.Clear(); boards.Clear(); boardsAt.Clear(); myWeek = null; myWeekAt = 0; profileKey = "";
            Raise();
        }

        /// <summary>The name shown to other players: the player's own display name when set (Settings), else the Google Play Games name.</summary>
        public static string ShownName
        {
            get { return LeaderboardNameRules.Resolve(progress != null ? progress.LeaderboardDisplayName : "", Profile.DisplayName); }
        }

        /// <summary>
        /// Sets (or, with an empty text, clears) the player's own display name. Validates with <see cref="LeaderboardNameRules"/>; nothing is stored unless the verdict is Ok (or the text is empty).
        /// Then the public profile is written again (<see cref="SyncProfile"/>: only when switched on, signed in and consent given; otherwise nothing is sent and the name goes out with the next sign-in).
        /// </summary>
        public static LeaderboardNameCheck SetDisplayName(string raw)
        {
            if (progress == null) return LeaderboardNameCheck.TooShort;
            string n = LeaderboardNameRules.Normalize(raw);
            if (n.Length > 0)
            {
                LeaderboardNameCheck v = LeaderboardNameRules.Validate(n);
                if (v != LeaderboardNameCheck.Ok) return v;
            }
            if (!progress.SetLeaderboardDisplayName(n)) return LeaderboardNameCheck.TooLong;
            SyncProfile();
            Raise();
            return LeaderboardNameCheck.Ok;
        }

        /// <summary>Writes the shown name (own display name, else the Play Games name) and the picture URL to players/{uid} once per sign-in (and again when either changes).</summary>
        public static void SyncProfile()
        {
            if (!CanUpload) return;
            LbProfile p = new LbProfile { DisplayName = ShownName, AvatarUrl = auth.Profile.AvatarUrl };
            string key = auth.Uid + "|" + LbJson.SafeName(p.DisplayName) + "|" + LbJson.SafeAvatar(p.AvatarUrl);
            if (key == profileKey) return;
            profileKey = key;   // set first: a failure below clears it so the next tick tries again
            client.WriteProfile(auth.Uid, p, ok =>
            {
                if (!ok) { profileKey = ""; return; }
                // Final Change 4: the profile exists now, so the service lists the player on all three boards (0 crowns until the first score); a failure retries with the next profile sync
                client.RegisterMe(done => { if (!done) profileKey = ""; else { MarkStale(); Raise(); Warm(); } });   // joined again: a ticket is ready before the first run
            });
        }

        // ---------------- run tickets ----------------

        public static string TicketKey(string kind, string target, int mode) { return kind + "|" + target + "|" + mode; }

        /// <summary>True when a run of this kind should try to get a ticket (signed in, consent given, switched on).</summary>
        public static bool WantsTicket { get { return CanUpload; } }

        /// <summary>True when a usable ticket for exactly this run is already cached (it is not removed).</summary>
        public static bool HasTicket(string kind, string target, int mode)
        {
            StartTicket t;
            return tickets.TryGetValue(TicketKey(kind, target, mode), out t) && t.ExpiresAtUnix - TicketMarginSeconds > Now;
        }

        /// <summary>A ready ticket for exactly this run, removed from the cache (a ticket is single use); null when there is none or it is about to expire.</summary>
        public static StartTicket TryTakeTicket(string kind, string target, int mode)
        {
            string key = TicketKey(kind, target, mode);
            StartTicket t;
            if (!tickets.TryGetValue(key, out t)) return null;
            tickets.Remove(key);
            return t.ExpiresAtUnix - TicketMarginSeconds > Now ? t : null;
        }

        /// <summary>
        /// Asks the service for a ticket and keeps it in the cache for <see cref="TryTakeTicket"/>; <paramref name="done"/> (may be null) gets it too. A late answer is still cached, so a slow call
        /// never blocks a run: the run starts with a local seed and the next attempt finds the ticket.
        /// </summary>
        public static void RequestTicket(string kind, string target, int mode, Action<StartTicket> done)
        {
            if (!CanUpload || string.IsNullOrEmpty(target)) { if (done != null) done(null); return; }
            string key = TicketKey(kind, target, mode);
            StartTicket have;
            if (tickets.TryGetValue(key, out have) && have.ExpiresAtUnix - TicketMarginSeconds > Now) { if (done != null) done(have); return; }
            if (!ticketsInFlight.Add(key)) { if (done != null) done(null); return; }
            client.StartRun(kind, kind == "level" ? target : null, kind == "level" ? null : target, mode, t =>
            {
                ticketsInFlight.Remove(key);
                if (t != null) { if (t.ExpiresAtUnix <= 0) t.ExpiresAtUnix = Now + 100 * 60; tickets[key] = t; }   // a ticket without a readable expiry is trusted for 100 minutes (the service lives 120)
                if (done != null) done(t);
            });
        }

        /// <summary>Fires RequestTicket without caring for the answer (the next attempt or the next level).</summary>
        public static void Prefetch(string kind, string target, int mode) { RequestTicket(kind, target, mode, null); }

        // ---------------- uploads ----------------

        /// <summary>
        /// Queues a finished run for upload and starts sending. Never blocks: a run without consent, sign-in or ticket is simply not uploaded. A run too large for the persisted queue is sent once, directly.
        /// </summary>
        public static void Submit(QueuedRun run)
        {
            if (run == null || !CanUpload) return;
            run.QueuedAtUnix = Now;
            if (!progress.EnqueueLeaderboardRun(run))
            {
                if (run.InputsB64 != null && run.InputsB64.Length > ProgressService.LeaderboardMaxRunChars && !sending)
                {
                    sending = true;
                    client.SubmitRun(run, r =>
                    {
                        sending = false;
                        if (r.Ok) { Accepted(); MarkStale(); }
                        else if (LeaderboardRetryPolicy.Classify(r, 0) == SubmitVerdict.Drop) Refused(r);   // too big to wait in the queue: it is not sent again
                        Pump();
                    }, new LbProfile { DisplayName = ShownName, AvatarUrl = auth.Profile.AvatarUrl });
                }
                return;
            }
            Pump();
        }

        /// <summary>Sends the first due run of the queue (one at a time). Called after a new run, after sign-in and every 2 s while runs wait.</summary>
        public static void Pump()
        {
            if (progress != null && progress.LeaderboardWipePending) { RunPendingWipe(); return; }   // nothing new goes up before the old scores are gone
            if (sending || !CanUpload) return;
            QueuedRun run = progress.NextDueLeaderboardRun(Now);
            if (run == null) return;
            sending = true;
            string id = run.RunId;
            client.SubmitRun(run, r =>
            {
                sending = false;
                switch (LeaderboardRetryPolicy.Classify(r, run.Tries))
                {
                    case SubmitVerdict.Done: progress.RemoveLeaderboardRun(id); Accepted(); MarkStale(); Pump(); break;
                    case SubmitVerdict.Retry: progress.LeaderboardRunFailed(id, Now); break;   // backoff: 30 s, 1 min, 2 min ... at most 30 min, dropped after 12 tries
                    case SubmitVerdict.RejoinThenRetry:
                        // the service no longer knows the player (profile or weekly row removed): write the profile and register again, keep the run and send it again soon
                        profileKey = "";
                        SyncProfile();
                        progress.LeaderboardRunFailed(id, Now);
                        break;
                    default: progress.RemoveLeaderboardRun(id); Refused(r); break;   // the service refused THIS run for good (mismatch, ticket used or expired, update the app): never retried, the reason is kept for the popup
                }
            }, new LbProfile { DisplayName = ShownName, AvatarUrl = auth.Profile.AvatarUrl });
        }

        /// <summary>Keeps why a run was refused (saved, shown until the week ends) and tells the open screens.</summary>
        static void Refused(HttpReply r)
        {
            if (progress == null) return;
            progress.SetLeaderboardRefusal(CurrentWeekKey, LeaderboardRetryPolicy.IsOldApp(r != null ? r.Status : 0, r != null ? r.ErrorCode : ""));
            Raise();
        }

        /// <summary>An accepted run proves the app is new enough again: the "update the game" note goes.</summary>
        static void Accepted() { if (progress != null) progress.ClearLeaderboardOldAppRefusal(); }

        /// <summary>The short reason a run of this week was refused by the service, "" when none was.</summary>
        public static string RefusalText
        {
            get
            {
                if (progress == null || progress.LeaderboardRefusalWeek != CurrentWeekKey) return "";
                return LeaderboardRetryPolicy.RefusalText(progress.LeaderboardRefusedOldApp);
            }
        }

        /// <summary>The slow tick: retry the queue, retry the silent sign-in, notice a new week.</summary>
        public static void Tick()
        {
            if (!Enabled) return;
            AutoJoin();
            if (HasConsent && !auth.IsReady) TrySilentSignIn();
            RunPendingWipe();   // retried until the service confirms
            if (!CanUpload) return;
            if (profileKey.Length == 0) SyncProfile();   // a failed profile write is retried
            if (QueuedRuns > 0) Pump();
            TickBreakdown();
            if (myWeek != null && myWeek.WeekKey != CurrentWeekKey && Now - myWeekAt > 5) RefreshMyWeek(true, null);
        }

        static void MarkStale() { myWeekAt = 0; foreach (string k in new List<string>(boardsAt.Keys)) boardsAt[k] = 0; }

        // ---------------- own standing and boards ----------------

        /// <summary>Reads GET /my-week (cached for <see cref="CacheSeconds"/> unless <paramref name="force"/>). done(null) when it could not be read; the previous standing stays in <see cref="MyWeek"/>.</summary>
        public static void RefreshMyWeek(bool withPrizes, Action<MyWeekData> done, bool force = false)
        {
            if (fakeOn) { if (done != null) done(fakeWeek); return; }
            if (!CanUpload) { if (done != null) done(myWeek); return; }
            if (!force && myWeek != null && Now - myWeekAt < CacheSeconds && (!withPrizes || myWeek.PrizesKnown)) { if (done != null) done(myWeek); return; }
            if (myWeekInFlight) { if (done != null) done(myWeek); return; }
            myWeekInFlight = true;
            client.MyWeek(withPrizes, d =>
            {
                myWeekInFlight = false;
                if (d != null)
                {
                    if (!withPrizes && myWeek != null) { d.Pending = myWeek.Pending; d.PrizesKnown = myWeek.PrizesKnown; }   // keep the prizes already known
                    myWeek = d; myWeekAt = Now;
                    Raise();
                }
                if (done != null) done(d);
            });
        }

        /// <summary>
        /// Reads GET /my-week?slots=1 for the own-row calculation popup (never cached here: the screen keeps the answer for its own life). done(null) when the player cannot be on the boards or the call failed.
        /// It does not replace <see cref="MyWeek"/>, so the prizes and ranks of the screens stay as they are.
        /// </summary>
        public static void LoadSlots(Action<MyWeekData> done)
        {
            if (fakeOn) { MyWeekData k = LocalSlotsData(); k.SlotsKnown = true; if (done != null) done(k); return; }
            if (!CanUpload) { if (done != null) done(null); return; }
            client.MySlots(d =>
            {
                if (d != null && d.SlotsKnown) { StoreSlotsCache(d); ResolveSlots(d); }
                if (done != null) done(d);
            });
        }

        /// <summary>Keeps the service's slot list in the save so the calculation popup also works offline (owner Bug List 5, 2026-11-17).</summary>
        static void StoreSlotsCache(MyWeekData d)
        {
            if (progress == null || d == null) return;
            var list = new List<WeekSlotInfo>(d.Slots.Count);
            foreach (LbSlot s in d.Slots) list.Add(ToVerifiedSlot(s));
            progress.StoreLeaderboardSlots(string.IsNullOrEmpty(d.WeekKey) ? CurrentWeekKey : d.WeekKey, list, d.Totals, d.TotalsKnown);
        }

        /// <summary>A slot of the service's replay list as a week slot; an Endless / Time Attack slot keeps its counted runs as parts (crown = their sum).</summary>
        static WeekSlotInfo ToVerifiedSlot(LbSlot s)
        {
            var v = new WeekSlotInfo { SlotId = s.SlotId, Crown = s.Crown, Floors = s.Floors, Mode = s.Mode, HeroId = s.HeroId, Score = s.Score, At = s.At, Verified = true };
            if (s.Runs != null && s.Runs.Count > 0)
            {
                v.Parts = new List<WeekSlotPart>(s.Runs.Count);
                foreach (LbSlotRun r in s.Runs) v.Parts.Add(new WeekSlotPart { Crown = r.Crown, Floors = r.Floors, Score = r.Score, At = r.At });
            }
            return v;
        }

        /// <summary>The slots of this week built from the phone alone (the local weekly crown store, written after every run). Never null; <c>SlotsKnown</c> is true when at least one slot exists.</summary>
        public static MyWeekData LocalSlotsData()
        {
            var d = new MyWeekData { WeekKey = CurrentWeekKey };
            if (progress == null) return d;
            foreach (WeekSlotInfo s in progress.WeekSlots()) d.Slots.Add(ToLbSlot(s));
            d.SlotsKnown = d.Slots.Count > 0;
            return d;
        }

        static LbSlot ToLbSlot(WeekSlotInfo s)
        {
            var slot = new LbSlot { SlotId = s.SlotId, Crown = s.Crown, Floors = s.Floors, Mode = s.Mode, HeroId = s.HeroId ?? "", Score = s.Score, At = s.At, Verified = s.Verified };
            if (s.Parts != null && s.Parts.Count > 0)
            {
                // Endless / Time Attack: the counted runs behind the sum (the calculation popup lists them under the line)
                slot.Runs = new List<LbSlotRun>(s.Parts.Count);
                foreach (WeekSlotPart p in s.Parts) if (p != null) slot.Runs.Add(new LbSlotRun { Crown = p.Crown, Floors = p.Floors, Score = p.Score, At = p.At });
            }
            return slot;
        }

        /// <summary>
        /// The service answered GET /my-week?slots=1: its copy of the player's calculation (the uploaded display list with the replay results laid over it) is merged into the phone (the newer version per slot wins), then the
        /// slot list of the answer becomes what the phone now holds with the service's replay numbers on top (the service wins for every slot it replayed; a slot it has not replayed yet is still shown).
        /// </summary>
        static void ResolveSlots(MyWeekData d)
        {
            if (progress == null || d == null || !d.SlotsKnown) return;
            string wk = string.IsNullOrEmpty(d.WeekKey) ? CurrentWeekKey : d.WeekKey;
            if (d.Display != null && wk == CurrentWeekKey) progress.MergeCloudBreakdown(wk, d.Display);
            var verified = new List<WeekSlotInfo>(d.Slots.Count);
            foreach (LbSlot s in d.Slots) verified.Add(ToVerifiedSlot(s));
            List<WeekSlotInfo> shown = BreakdownMerge.ApplyVerified(wk == CurrentWeekKey ? progress.LocalBreakdown() : null, verified);
            d.Slots.Clear();
            foreach (WeekSlotInfo s in shown) d.Slots.Add(ToLbSlot(s));
        }

        /// <summary>
        /// The best calculation the phone has without asking the service: the slot list the service last answered for this week (with its totals) when no run waits for upload, otherwise the local weekly store.
        /// <c>SlotsKnown</c> is false when there is nothing at all (nothing played this week, nothing cached).
        /// </summary>
        public static MyWeekData KnownSlots()
        {
            LeaderboardSlotsCache c = progress != null ? progress.LeaderboardSlots : null;
            if (c != null && c.WeekKey == CurrentWeekKey && c.Slots != null && c.Slots.Count > 0 && QueuedRuns == 0)
            {
                // the service's replay numbers win; a slot only the phone knows (replay not available yet) stays, whichever version is newest
                var d = new MyWeekData { WeekKey = c.WeekKey, SlotsKnown = true, TotalsKnown = c.TotalsKnown };
                var verified = new List<WeekSlotInfo>(c.Slots.Count);
                foreach (WeekSlotInfo s in c.Slots) { WeekSlotInfo v = s.Copy(); v.Verified = true; verified.Add(v); }
                foreach (WeekSlotInfo s in BreakdownMerge.ApplyVerified(progress.LocalBreakdown(), verified)) d.Slots.Add(ToLbSlot(s));
                if (c.Totals != null) for (int i = 0; i < 3 && i < c.Totals.Length; i++) d.Totals[i] = c.Totals[i];
                return d;
            }
            return LocalSlotsData();
        }

        /// <summary>The cached board (or null) of the current week.</summary>
        public static BoardData CachedBoard(string board)
        {
            BoardData b;
            if (fakeOn) return fakeBoards.TryGetValue(board, out b) ? b : null;
            return boards.TryGetValue(CurrentWeekKey + "|" + board, out b) ? b : null;
        }

        /// <summary>Reads the top 100 of a board (Firestore REST, one document; cached for <see cref="CacheSeconds"/>). done(null) on failure.</summary>
        public static void LoadBoard(string board, Action<BoardData> done, bool force = false)
        {
            if (fakeOn) { if (done != null) done(CachedBoard(board)); return; }
            if (!IsSignedIn || client == null) { if (done != null) done(null); return; }
            string wk = CurrentWeekKey;
            string key = wk + "|" + board;
            long at;
            BoardData have;
            if (!force && boards.TryGetValue(key, out have) && boardsAt.TryGetValue(key, out at) && Now - at < CacheSeconds) { if (done != null) done(have); return; }
            if (!boardsInFlight.Add(key)) { if (done != null) done(CachedBoard(board)); return; }
            client.FetchBoard(wk, board, b =>
            {
                boardsInFlight.Remove(key);
                if (b != null) { boards[key] = b; boardsAt[key] = Now; Raise(); }
                if (done != null) done(b);
            });
        }

        // ---------------- prizes ----------------

        /// <summary>The prizes the service says can be claimed now (closed weeks, paid rank, not granted yet).</summary>
        public static List<PendingPrize> ClaimablePrizes()
        {
            var list = new List<PendingPrize>();
            if (myWeek == null || myWeek.Pending == null) return list;
            foreach (PendingPrize p in myWeek.Pending) if (p.Claimable) list.Add(p);
            return list;
        }

        /// <summary>
        /// Claims one prize exactly once: POST /claim-prize (the service creates the claim in a transaction), then the items are granted through ProgressService and saved at once, then the service
        /// is told (ack) that the grant is saved. A repeated tap, a second device or a crash between the steps never grants twice: the device remembers the claim id, the service remembers the state.
        /// </summary>
        public static void ClaimPrize(PendingPrize prize, Action<ClaimOutcome> done)
        {
            if (prize == null || !CanUpload) { done(new ClaimOutcome { Status = ClaimStatus.Offline }); return; }
            if (claiming) { done(new ClaimOutcome { Status = ClaimStatus.Busy }); return; }
            claiming = true;
            client.ClaimPrize(prize.WeekKey, prize.Board, false, (r, c) =>
            {
                if (c == null)
                {
                    claiming = false;
                    string code = r.ErrorCode;
                    ClaimStatus st = r.NetworkError || r.NoToken || r.Retryable ? ClaimStatus.Offline : (code == "not_eligible" || code == "not_ranked" ? ClaimStatus.NotEligible : ClaimStatus.Failed);
                    done(new ClaimOutcome { Status = st, Code = code });
                    return;
                }
                if (progress.LeaderboardPrizeGranted(c.ClaimId) || !c.Grant)
                {
                    // already in this save (an earlier tap whose ack was lost) or already granted elsewhere: only confirm, grant nothing
                    client.ClaimPrize(prize.WeekKey, prize.Board, true, (r2, c2) => { });
                    prize.State = "granted";
                    claiming = false;
                    Raise();
                    done(new ClaimOutcome { Status = ClaimStatus.AlreadyClaimed });
                    return;
                }
                RewardClaimOutcome o = progress.GrantLeaderboardPrize(c.ClaimId, c.Prize.Bundles, c.Prize.GoldChests, c.Prize.IronChests, c.Prize.PowerUps, c.Prize.Coins);
                if (o.Result == RewardClaimResult.Ok)
                {
                    prize.State = "granted";
                    client.ClaimPrize(prize.WeekKey, prize.Board, true, (r2, c2) => { });   // best effort: a lost ack only repeats the (harmless) confirm later
                    claiming = false;
                    Raise();
                    done(new ClaimOutcome { Status = ClaimStatus.Granted, Reward = o });
                }
                else
                {
                    claiming = false;
                    done(new ClaimOutcome { Status = o.Result == RewardClaimResult.AlreadyClaimed ? ClaimStatus.AlreadyClaimed : ClaimStatus.Failed });
                }
            });
        }

        // ---------------- avatars ----------------

        /// <summary>Downloads a picture through the transport (used by the avatar cache); null transport (disabled) answers null.</summary>
        public static void GetTexture(string url, Action<UnityEngine.Texture2D> done)
        {
            if (net == null || string.IsNullOrEmpty(url) || LbJson.SafeAvatar(url).Length == 0) { done(null); return; }
            net.GetTexture(url, done);
        }
    }
}
