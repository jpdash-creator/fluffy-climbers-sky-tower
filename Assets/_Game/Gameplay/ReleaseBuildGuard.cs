using System;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Pure release-build checks (owner decision 2026-11-17: the closed-testing / store AAB has NO admin, dev or test tools and uses real purchases only).
    /// AndroidReleaseBuild runs <see cref="SourceProblem"/> and <see cref="BillingProblem"/> before the build number is used up; the EditMode tests run the same
    /// functions, so a refactor that opens the tools in a release player fails a test. <see cref="StateProblem"/> takes the compile-time and runtime flags of a
    /// PLAYER as arguments (in the Editor they are always "tools on", so the Editor cannot answer it for the player; the source check is what covers the player).
    /// </summary>
    public static class ReleaseBuildGuard
    {
        /// <summary>
        /// The flags a release player must show: AdminAccess.ToolsCompiledIn false, GameSettings.DevBuild false, no admin button, test purchases (Test buy rows,
        /// ShopScreen.TestPurchasesEnabled) off. Returns an error text or null.
        /// </summary>
        public static string StateProblem(bool toolsCompiledIn, bool devBuild, bool adminAvailable, bool testPurchasesEnabled)
        {
            if (toolsCompiledIn) return "AdminAccess.ToolsCompiledIn is true: the admin tools would be reachable in the release.";
            if (devBuild) return "GameSettings.DevBuild is true: the dev and test flags would be active in the release.";
            if (adminAvailable) return "The Admin button is available in the release.";
            if (testPurchasesEnabled) return "Test purchases are enabled in the release (the shop would show Test buy rows).";
            return null;
        }

        /// <summary>
        /// Source guard: AdminAccess.ToolsCompiledIn and GameSettings.DevBuild must stay behind "#if UNITY_EDITOR || DEVELOPMENT_BUILD" with the else branch false, and the
        /// shop test flag must stay derived from GameSettings.ShopTest (which needs DevBuild). Returns an error text or null.
        /// </summary>
        public static string SourceProblem(string adminAccessSource, string gameSettingsSource, string shopScreenSource)
        {
            string p = Require(adminAccessSource, "AdminAccess.cs", "#if UNITY_EDITOR || DEVELOPMENT_BUILD", "public const bool ToolsCompiledIn = true;", "#else", "public const bool ToolsCompiledIn = false;");
            if (p != null) return p;
            p = Require(adminAccessSource, "AdminAccess.cs", "get { return ToolsCompiledIn && PlayerPrefs.GetInt(UnlockedKey, 0) == 1; }", "public static bool Available { get { return ToolsCompiledIn && ");
            if (p != null) return p;
            p = Require(gameSettingsSource, "GameSettings.cs", "#if UNITY_EDITOR || DEVELOPMENT_BUILD", "return (Debug.isDebugBuild && !Application.isEditor) || AdminAccess.Unlocked;", "#else", "return false;");
            if (p != null) return p;
            p = Require(gameSettingsSource, "GameSettings.cs", "get { return DevBuild && PlayerPrefs.GetInt(SystemTestKey, 0) == 1; }", "get { return SystemTest && PlayerPrefs.GetInt(ShopTestKey, 0) == 1; }");
            if (p != null) return p;
            return Require(shopScreenSource, "ShopScreen.cs", "TestPurchasesOverride ?? GameSettings.ShopTest");
        }

        /// <summary>Real purchases only: Unity IAP's BillingMode.json must name the Google Play store. Returns an error text or null.</summary>
        public static string BillingProblem(string billingModeJson)
        {
            if (string.IsNullOrWhiteSpace(billingModeJson)) return "Assets/Resources/BillingMode.json is missing or empty (Unity IAP must target Google Play).";
            if (billingModeJson.IndexOf("\"androidStore\"", StringComparison.Ordinal) < 0 || billingModeJson.IndexOf("GooglePlay", StringComparison.Ordinal) < 0)
                return "Assets/Resources/BillingMode.json does not set androidStore to GooglePlay. A release uses the real store only.";
            return null;
        }

        static string Require(string source, string file, params string[] needles)
        {
            if (string.IsNullOrEmpty(source)) return file + " could not be read for the release guard.";
            int from = 0;
            foreach (string n in needles)
            {
                int i = source.IndexOf(n, from, StringComparison.Ordinal);
                if (i < 0) return file + " no longer contains the release lockdown text '" + n + "' (in order). The admin and test tools may be reachable in a release: review it before building.";
                from = i;
            }
            return null;
        }
    }
}
