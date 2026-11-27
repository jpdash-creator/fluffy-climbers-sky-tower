namespace FluffyClimbers.Leaderboard
{
    /// <summary>What to do with a queued run after the service answered its upload.</summary>
    public enum SubmitVerdict
    {
        /// <summary>Accepted (2xx): remove from the queue.</summary>
        Done,
        /// <summary>No answer, no token, 401, 408, 429, 5xx: keep it, back off, try again.</summary>
        Retry,
        /// <summary>The service does not know the player (profile / weekly row removed by a reset or a clean-up): write the profile again, register, keep the run and retry it.</summary>
        RejoinThenRetry,
        /// <summary>A final refusal of THIS run (replay mismatch, ticket used or expired, old rules version ...): drop it, retrying can never succeed.</summary>
        Drop
    }

    /// <summary>
    /// Pure decision logic of the upload queue (Final Bugs 1 item 11): a verified local run is never dropped for a reason that is the player's account state, only for a refusal of the run itself.
    /// Unknown 4xx answers keep the run (capped by the queue's own 12 tries) instead of losing it silently.
    /// </summary>
    public static class LeaderboardRetryPolicy
    {
        /// <summary>Rejoin attempts per queued run (after that the normal backoff and its 12-try cap apply).</summary>
        public const int MaxRejoinsPerRun = 2;

        /// <summary>Shown when the service refused a run because this app is too old (Final Bugs 2 #8).</summary>
        public const string OldAppText = "Update the game to count runs";
        /// <summary>Shown for every other final refusal of a run.</summary>
        public const string NotVerifiedText = "Could not verify this run";

        /// <summary>The refusal means "this app version is too old" (HTTP 426 / unsupported_version).</summary>
        public static bool IsOldApp(int status, string code) { return status == 426 || code == "unsupported_version"; }

        public static string RefusalText(bool oldApp) { return oldApp ? OldAppText : NotVerifiedText; }

        public static SubmitVerdict Classify(HttpReply r, int tries)
        {
            if (r == null) return SubmitVerdict.Retry;
            if (r.Ok) return SubmitVerdict.Done;
            if (r.Retryable) return SubmitVerdict.Retry;
            return Classify(r.Status, r.ErrorCode, tries);
        }

        public static SubmitVerdict Classify(int status, string code, int tries)
        {
            code = code ?? "";
            switch (code)
            {
                case "no_profile":
                case "not_joined":
                case "unknown_user":
                case "user_not_found":
                    return tries < MaxRejoinsPerRun ? SubmitVerdict.RejoinThenRetry : SubmitVerdict.Retry;
                case "mismatch":
                case "ticket_used":
                case "ticket_expired":
                case "ticket_mismatch":
                case "ticket_invalid":
                case "unsupported_version":
                case "implausible":
                case "bad_request":
                case "bad_variant":
                    return SubmitVerdict.Drop;
            }
            if (status == 404) return tries < MaxRejoinsPerRun ? SubmitVerdict.RejoinThenRetry : SubmitVerdict.Retry;   // an unknown route or document: the account state is the likely cause
            if (status == 403 || status == 410 || status == 426 || status == 422 || status == 400 || status == 409) return SubmitVerdict.Drop;   // 403 is a final refusal (the service recreates a missing profile on submit, so a plain 403 is not an account-state problem)
            return SubmitVerdict.Retry;   // other 4xx with an unknown code: keep the run, the try cap ends it
        }
    }
}
