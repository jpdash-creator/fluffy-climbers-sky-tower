using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FluffyClimbers.Content
{
    /// <summary>
    /// Pure text patch of the shipped content manifest (Assets/Resources/Content/content_manifest.json, the file the app reads its remote config from).
    /// The closed-testing release AAB build (AndroidReleaseBuild) switches the weekly leaderboard ON by writing remoteConfig.leaderboard = { enabled: true, baseUrl }
    /// into the manifest for the duration of the build and restoring the original bytes afterwards. Normal development builds never call this, so they keep the
    /// leaderboard OFF. Plain C# so an EditMode test covers it.
    /// </summary>
    public static class ReleaseManifestPatch
    {
        /// <summary>Returns the manifest text with remoteConfig.leaderboard set to enabled + baseUrl (same layout as ContentManifestMerger: indent 2, LF, one trailing newline).</summary>
        public static string EnableLeaderboard(string manifestJson, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ContentException("remoteConfig", "leaderboard baseUrl is empty");
            JObject root = Load(manifestJson);
            var rc = root["remoteConfig"] as JObject;
            if (rc == null) throw new ContentException("remoteConfig", "missing 'remoteConfig' object");
            rc["leaderboard"] = new JObject { ["enabled"] = true, ["baseUrl"] = baseUrl.Trim().TrimEnd('/') };

            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            {
                sw.NewLine = "\n";
                using (var jw = new JsonTextWriter(sw) { Formatting = Formatting.Indented, Indentation = 2, IndentChar = ' ' })
                    root.WriteTo(jw);
            }
            return sb.ToString().Replace("\r\n", "\n") + "\n";
        }

        /// <summary>True when the manifest carries remoteConfig.leaderboard.enabled = true; baseUrl is the configured URL (null when absent).</summary>
        public static bool IsLeaderboardEnabled(string manifestJson, out string baseUrl)
        {
            baseUrl = null;
            JObject root = Load(manifestJson);
            var lb = root["remoteConfig"]?["leaderboard"] as JObject;
            if (lb == null) return false;
            baseUrl = (string)lb["baseUrl"];
            JToken en = lb["enabled"];
            return en != null && en.Type == JTokenType.Boolean && (bool)en;
        }

        static JObject Load(string text)
        {
            try
            {
                using (var r = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
                    return JObject.Load(r);
            }
            catch (JsonException e) { throw new ContentException("manifest", "invalid JSON: " + e.Message, e); }
        }
    }
}
