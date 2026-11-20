using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Leaderboard;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>Closed-testing release AAB (owner 2026-11-17): leaderboard ON only in the release patch, no admin/dev/test tools, real purchases only.</summary>
    public class ReleaseBuildTests
    {
        static string ProjectPath(string rel) { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", rel)); }

        static string Manifest { get { return File.ReadAllText(ProjectPath("Assets/Resources/Content/content_manifest.json")); } }

        [Test]
        public void ShippedManifestKeepsTheLeaderboardOffForDevelopmentBuilds()
        {
            string url;
            Assert.IsFalse(ReleaseManifestPatch.IsLeaderboardEnabled(Manifest, out url), "the committed manifest must keep leaderboard OFF; only the release build patches it temporarily");
            Assert.IsFalse(LeaderboardConfig.From(ContentRepository.LoadFromResources().RemoteConfig).Enabled);
        }

        [Test]
        public void ReleasePatchSwitchesTheLeaderboardOnAndTouchesNothingElse()
        {
            string patched = ReleaseManifestPatch.EnableLeaderboard(Manifest, LeaderboardConfig.DefaultBaseUrl + "/");
            string url;
            Assert.IsTrue(ReleaseManifestPatch.IsLeaderboardEnabled(patched, out url));
            Assert.AreEqual(LeaderboardConfig.DefaultBaseUrl, url);

            JObject before = JObject.Parse(Manifest), after = JObject.Parse(patched);
            ((JObject)after["remoteConfig"]).Remove("leaderboard");
            Assert.IsTrue(JToken.DeepEquals(before, after), "the patch must change only remoteConfig.leaderboard");

            ContentRepository repo = ContentRepository.LoadFromJson(patched);
            LeaderboardConfig cfg = LeaderboardConfig.From(repo.RemoteConfig);
            Assert.IsTrue(cfg.Enabled);
            Assert.AreEqual(LeaderboardConfig.DefaultBaseUrl, cfg.BaseUrl);
            Assert.AreEqual(patched, ReleaseManifestPatch.EnableLeaderboard(patched, LeaderboardConfig.DefaultBaseUrl), "patching twice is stable");
        }

        [Test]
        public void ReleasePatchRefusesAnEmptyUrlOrABrokenManifest()
        {
            Assert.Throws<ContentException>(() => ReleaseManifestPatch.EnableLeaderboard(Manifest, " "));
            Assert.Throws<ContentException>(() => ReleaseManifestPatch.EnableLeaderboard("{ \"version\": 1 }", "https://x.test"));
            Assert.Throws<ContentException>(() => ReleaseManifestPatch.EnableLeaderboard("not json", "https://x.test"));
        }

        [Test]
        public void ReleaseStateHasNoToolsAndNoTestPurchases()
        {
            Assert.IsNull(ReleaseBuildGuard.StateProblem(false, false, false, false));
            Assert.IsNotNull(ReleaseBuildGuard.StateProblem(true, false, false, false));
            Assert.IsNotNull(ReleaseBuildGuard.StateProblem(false, true, false, false));
            Assert.IsNotNull(ReleaseBuildGuard.StateProblem(false, false, true, false));
            Assert.IsNotNull(ReleaseBuildGuard.StateProblem(false, false, false, true));
        }

        [Test]
        public void ReleaseLockdownSourceTextIsIntact()
        {
            string problem = ReleaseBuildGuard.SourceProblem(
                File.ReadAllText(ProjectPath("Assets/_Game/Gameplay/AdminAccess.cs")),
                File.ReadAllText(ProjectPath("Assets/_Game/Gameplay/GameSettings.cs")),
                File.ReadAllText(ProjectPath("Assets/_Game/Gameplay/ShopScreen.cs")));
            Assert.IsNull(problem, problem);
        }

        [Test]
        public void ReleaseGuardCatchesAnOpenedLockdown()
        {
            string admin = File.ReadAllText(ProjectPath("Assets/_Game/Gameplay/AdminAccess.cs"));
            string settings = File.ReadAllText(ProjectPath("Assets/_Game/Gameplay/GameSettings.cs"));
            string shop = File.ReadAllText(ProjectPath("Assets/_Game/Gameplay/ShopScreen.cs"));
            Assert.IsNotNull(ReleaseBuildGuard.SourceProblem(admin.Replace("public const bool ToolsCompiledIn = false;", "public const bool ToolsCompiledIn = true;"), settings, shop));
            Assert.IsNotNull(ReleaseBuildGuard.SourceProblem(admin, settings.Replace("return false;   // release player", "return true;   // release player"), shop));
            Assert.IsNotNull(ReleaseBuildGuard.SourceProblem(admin, settings, shop.Replace("GameSettings.ShopTest", "true")));
            Assert.IsNotNull(ReleaseBuildGuard.SourceProblem(null, settings, shop));
        }

        [Test]
        public void ReleaseUsesTheRealGooglePlayStore()
        {
            Assert.IsNull(ReleaseBuildGuard.BillingProblem(File.ReadAllText(ProjectPath("Assets/Resources/BillingMode.json"))));
            Assert.IsNotNull(ReleaseBuildGuard.BillingProblem("{\"androidStore\":\"fake\"}"));
            Assert.IsNotNull(ReleaseBuildGuard.BillingProblem(""));
        }
    }
}
