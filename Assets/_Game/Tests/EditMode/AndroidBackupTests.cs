using System;
using System.Reflection;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Owner 2026-11-16: development builds have Android backup off (clean debug installs), release builds keep it. The build hook lives in the editor assembly, so it is called by reflection.</summary>
    public class AndroidBackupTests
    {
        const string Manifest = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"x\">\n  <application android:label=\"x\" />\n</manifest>\n";

        static MethodInfo Patch()
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType("FluffyClimbers.Editor.Build.AndroidNoBackup", false);
                if (t != null) return t.GetMethod("PatchManifest", BindingFlags.Public | BindingFlags.Static);
            }
            return null;
        }

        [Test]
        public void DevelopmentManifestDisablesBackup()
        {
            MethodInfo m = Patch();
            Assert.IsNotNull(m, "AndroidNoBackup.PatchManifest exists");
            var text = (string)m.Invoke(null, new object[] { Manifest, true });
            StringAssert.Contains("android:allowBackup=\"false\"", text);
            StringAssert.Contains("android:dataExtractionRules=\"@xml/fluffy_no_backup_rules\"", text);
        }

        [Test]
        public void ReleaseManifestKeepsBackup()
        {
            MethodInfo m = Patch();
            Assert.IsNotNull(m);
            var text = (string)m.Invoke(null, new object[] { Manifest, false });
            Assert.AreEqual(Manifest, text);
            StringAssert.DoesNotContain("allowBackup", text);
        }
    }
}
