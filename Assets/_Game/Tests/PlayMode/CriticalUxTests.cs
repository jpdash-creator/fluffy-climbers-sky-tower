using System.Collections;
using System.Collections.Generic;
using System.Text;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using FluffyClimbers.Monetization;
using FluffyClimbers.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Enforces the critical UX rules (docs/11, CUX-3/4/5): no overlapping text or buttons, nothing outside the safe area,
    /// touch targets of at least 48 dp and text of at least 16 sp on every supported aspect ratio. Category CriticalUX.
    /// The canvas follows the camera's target texture, so each aspect is simulated by rendering to a texture of that size.
    /// </summary>
    [Category("CriticalUX")]
    public partial class CriticalUxLayoutTests
    {
        struct Aspect
        {
            public string Name; public int W, H;
            public Aspect(string n, int w, int h) { Name = n; W = w; H = h; }
        }

        static readonly Aspect[] Aspects =
        {
            new Aspect("9x16", 540, 960),
            new Aspect("9x19.5", 450, 975),
            new Aspect("9x21", 450, 1050),
            new Aspect("3x4", 720, 960),
        };

        sealed class Item { public RectTransform Rt; public Rect Box; public string Name; public bool IsButton; public bool IsImage; }

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
            GoogleConnectPrompt.ResetForTests();
            GoogleConnectPrompt.AutoShow = false;   // Bug List 1 B1.c: the Google connect pop-up is tested on its own, not over every Home screen
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            foreach (var mc in Object.FindObjectsByType<MetaController>(FindObjectsSortMode.None)) mc.enabled = false;
            UiKit.SafeAreaNormalized = () => new Rect(0f, 0f, 1f, 1f);
            RunView.AspectOverride = 0f;
            RunController.HeroOverride = null;
            PlayerPrefs.DeleteAll();
            GameSettings.ApplyTextScale();
            GameSettings.ApplyFlags();    // the admin audits turn System Test switches on: back to production behaviour
            GameSettings.ShowDevRowsForTests = false;
            Services.Account = null;      // account scenarios use stub services: back to the default
            AccountSync.ResetForTests();
            GameSession.Reset();
        }

        static IEnumerator Load(string scene)
        {
            var op = SceneManager.LoadSceneAsync(scene);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
        }

        static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null && p.GetComponent<Canvas>() == null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        static bool IsDescendant(Transform child, Transform ancestor)
        {
            for (Transform t = child; t != null; t = t.parent) if (t == ancestor) return true;
            return false;
        }

        /// <summary>Collects the visible text and button boxes in canvas units (reference pixels) and returns the violations found.</summary>
        static List<string> Audit(string screen, Aspect a, Rect safe)
        {
            var problems = new List<string>();
            var items = new List<Item>();
            Camera cam = Camera.main;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;
                float scale = canvas.scaleFactor;

                foreach (Text t in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (string.IsNullOrEmpty(t.text) || !t.gameObject.activeInHierarchy) continue;
                    if (t.color.a < 0.05f) continue;
                    Rect box = ToCanvasUnits(t.rectTransform, cam, scale);
                    // Use the size the text really needs, placed according to its alignment.
                    float pw = Mathf.Min(t.preferredWidth, box.width);
                    float ph = t.preferredHeight;
                    float x = box.x, y = box.y;
                    switch (t.alignment)
                    {
                        case TextAnchor.UpperCenter: case TextAnchor.MiddleCenter: case TextAnchor.LowerCenter: x += (box.width - pw) * 0.5f; break;
                        case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: x += box.width - pw; break;
                    }
                    switch (t.alignment)
                    {
                        case TextAnchor.UpperLeft: case TextAnchor.UpperCenter: case TextAnchor.UpperRight: y += box.height - ph; break;
                        case TextAnchor.MiddleLeft: case TextAnchor.MiddleCenter: case TextAnchor.MiddleRight: y += (box.height - ph) * 0.5f; break;
                    }
                    items.Add(new Item { Rt = t.rectTransform, Box = new Rect(x, y, pw, ph), Name = Path(t.transform) + " \"" + Trim(t.text) + "\"" });
                    if (t.fontSize < UiKit.MinBodyTextPx - 1f)
                        problems.Add(screen + ": text below 16 sp (" + t.fontSize + "): " + Path(t.transform));
                }

                // Decorative images (icons, stars, badges): text must not run over them unless the image is the text's own backing.
                foreach (Image img in canvas.GetComponentsInChildren<Image>(false))
                {
                    if (img.sprite == null || img.color.a < 0.3f || !img.gameObject.activeInHierarchy || Theme.IsSurfaceSprite(img.sprite)) continue;
                    if (img.GetComponent<Button>() != null) continue;          // buttons are audited as buttons
                    string n = img.name;
                    if (n == "Bg" || n == "Fill" || n == "Panel" || n == "Ribbon" || n == "WalletPanel" || n == "StarPill" || n == "RowsBacking" || n == "TopBacking" || n == "SeamSky" || n == "NameBacking" || n == "Ground" || n == "GroundEdge" || n == "DirtOval" || n == "Backdrop") continue;   // GearPic: number badges sit on their own backing over its corner by design; the others are scenery
                    Rect ib = ToCanvasUnits(img.rectTransform, cam, scale);
                    if (ib.width * ib.height > 0.35f * (a.W / scale) * (a.H / scale)) continue;   // full-screen art
                    items.Add(new Item { Rt = img.rectTransform, Box = ib, Name = Path(img.transform) + " [image]", IsImage = true });
                }

                foreach (Button b in canvas.GetComponentsInChildren<Button>(false))
                {
                    if (!b.gameObject.activeInHierarchy) continue;
                    var rt = (RectTransform)b.transform;
                    Rect box = ToCanvasUnits(rt, cam, scale);
                    items.Add(new Item { Rt = rt, Box = box, Name = Path(rt) + " [button]", IsButton = true });
                    if (box.width < UiKit.MinTouchPx - 1f || box.height < UiKit.MinTouchPx - 1f)
                        problems.Add(screen + ": touch target below 48 dp (" + Mathf.RoundToInt(box.width) + "x" + Mathf.RoundToInt(box.height) + "): " + Path(rt));
                }

                // Content inside a scroll view (the world map) is only audited where the viewport shows it; the mask clips the rest.
                for (int n = items.Count - 1; n >= 0; n--)
                {
                    ScrollRect sr = items[n].Rt.GetComponentInParent<ScrollRect>();
                    if (sr == null || sr.viewport == null || sr.GetComponentInParent<Canvas>() != canvas) continue;
                    Rect vp = ToCanvasUnits(sr.viewport, cam, scale);
                    Transform topBar = null;
                    foreach (Transform tr in sr.transform.parent.GetComponentsInChildren<Transform>(false)) if (tr.name == "TopBacking") { topBar = tr; break; }
                    if (topBar != null) vp.yMax = Mathf.Min(vp.yMax, ToCanvasUnits((RectTransform)topBar, cam, scale).yMin);   // the map shows through the floating top bar; only the part below it is audited
                    Rect b = items[n].Box;
                    float x0 = Mathf.Max(b.xMin, vp.xMin), x1 = Mathf.Min(b.xMax, vp.xMax);
                    float y0 = Mathf.Max(b.yMin, vp.yMin), y1 = Mathf.Min(b.yMax, vp.yMax);
                    if (x1 <= x0 || y1 <= y0) { items.RemoveAt(n); continue; }
                    items[n].Box = new Rect(x0, y0, x1 - x0, y1 - y0);
                }

                // Content under a Mask (a hero face: the doll is much bigger than the round window it shows through) is only visible inside the mask: audit just that part.
                for (int n = items.Count - 1; n >= 0; n--)
                {
                    Transform host = items[n].Rt.parent;
                    Mask mk = host != null ? host.GetComponentInParent<Mask>() : null;
                    if (mk == null || !mk.isActiveAndEnabled || mk.GetComponentInParent<Canvas>() != canvas) continue;
                    Rect mb = ToCanvasUnits(mk.rectTransform, cam, scale);
                    Rect ib = items[n].Box;
                    float x0 = Mathf.Max(ib.xMin, mb.xMin), x1 = Mathf.Min(ib.xMax, mb.xMax);
                    float y0 = Mathf.Max(ib.yMin, mb.yMin), y1 = Mathf.Min(ib.yMax, mb.yMax);
                    if (x1 <= x0 || y1 <= y0) { items.RemoveAt(n); continue; }
                    items[n].Box = new Rect(x0, y0, x1 - x0, y1 - y0);
                }

                // A modal popup or overlay hides everything behind it: only the modal is audited.
                if (items.Exists(i => i.Name.StartsWith("Popup/") || i.Name.StartsWith("Overlay/")))
                    items.RemoveAll(i => !(i.Name.StartsWith("Popup/") || i.Name.StartsWith("Overlay/")));

                // Inside the safe area (normalised to the canvas size in canvas units).
                float cw = a.W / scale, ch = a.H / scale;
                var sa = new Rect(safe.x * cw, safe.y * ch, safe.width * cw, safe.height * ch);
                foreach (Item it in items)
                {
                    if (it.IsImage) continue;
                    const float tol = 2f;
                    if (it.Box.xMin < sa.xMin - tol || it.Box.xMax > sa.xMax + tol || it.Box.yMin < sa.yMin - tol || it.Box.yMax > sa.yMax + tol)
                        problems.Add(screen + ": outside the safe area: " + it.Name + " " + it.Box + " safe " + sa);
                }
            }

            // Overlap of text or buttons (shared ancestors and button labels are not overlaps).
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    Item p = items[i], q = items[j];
                    if (IsDescendant(p.Rt, q.Rt) || IsDescendant(q.Rt, p.Rt)) continue;
                    if (p.IsImage != q.IsImage)
                    {
                        // image vs text only; an image next to a button is fine
                        Item other = p.IsImage ? q : p;
                        if (other.IsButton) continue;
                    }
                    else if (p.IsImage && q.IsImage) continue;
                    if (!p.Box.Overlaps(q.Box)) continue;
                    float ox = Mathf.Min(p.Box.xMax, q.Box.xMax) - Mathf.Max(p.Box.xMin, q.Box.xMin);
                    float oy = Mathf.Min(p.Box.yMax, q.Box.yMax) - Mathf.Max(p.Box.yMin, q.Box.yMin);
                    if (ox < 6f || oy < 6f) continue;
                    problems.Add(screen + ": overlap " + ox.ToString("F0") + "x" + oy.ToString("F0") + ": " + p.Name + "  <>  " + q.Name);
                }
            }
            return problems;
        }

        static string Trim(string s) { s = s.Replace("\n", " "); return s.Length > 24 ? s.Substring(0, 24) : s; }

        static Rect ToCanvasUnits(RectTransform rt, Camera cam, float scale)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector3 a = cam.WorldToScreenPoint(c[0]);
            Vector3 b = cam.WorldToScreenPoint(c[2]);
            return new Rect(a.x / scale, a.y / scale, (b.x - a.x) / scale, (b.y - a.y) / scale);
        }

        /// <summary>Points the canvases at a render texture of the given size so the CanvasScaler lays out for that aspect.</summary>
        static IEnumerator ApplyAspect(Aspect a, RenderTexture[] holder)
        {
            Camera cam = Camera.main;
            if (holder[0] != null) holder[0].Release();
            holder[0] = new RenderTexture(a.W, a.H, 24);
            cam.targetTexture = holder[0];
            cam.aspect = (float)a.W / a.H;
            RunView.AspectOverride = (float)a.W / a.H;
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 1f;
                }
            }
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        static void Restore(RenderTexture[] holder)
        {
            Camera cam = Camera.main;
            if (cam != null) { cam.targetTexture = null; cam.ResetAspect(); }
            RunView.AspectOverride = 0f;
            if (holder[0] != null) holder[0].Release();
        }

        static readonly Rect FullSafe = new Rect(0f, 0f, 1f, 1f);
        static readonly Rect NotchSafe = new Rect(0.03f, 0.02f, 0.94f, 0.93f);   // a rounded, notched phone

        static IEnumerator CheckScreen(string screen, System.Action show, List<string> all)
        {
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                foreach (Rect safe in new[] { FullSafe, NotchSafe })
                {
                    Rect s = safe;
                    UiKit.SafeAreaNormalized = () => s;
                    show();
                    yield return ApplyAspect(a, holder);
                    yield return null;
                    all.AddRange(Audit(screen + "@" + a.Name + (safe == NotchSafe ? "+notch" : ""), a, safe));
                    if (safe == FullSafe) Shot(screen, a, holder[0]);
                }
            }
            Restore(holder);
        }

        /// <summary>Visual validation: with BU_SHOTS=dir set, every audited screen is saved as a PNG (9x16 and 9x21, normal text, right-handed) for review.</summary>
        static void Shot(string screen, Aspect a, RenderTexture rt)
        {
            string dir = System.Environment.GetEnvironmentVariable("BU_SHOTS");
            if (string.IsNullOrEmpty(dir) || rt == null) return;
            if (a.Name != "9x16" && a.Name != "9x21") return;
            if (GameSettings.TextPercent != 100 || GameSettings.LeftHanded) return;
            System.IO.Directory.CreateDirectory(dir);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, screen + "_" + a.Name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static void Report(List<string> problems)
        {
            // De-duplicate while keeping order, so one real defect is not repeated per aspect.
            var seen = new HashSet<string>();
            var sb = new StringBuilder();
            foreach (string p in problems)
            {
                string key = p.Substring(p.IndexOf(':') + 1);
                if (seen.Add(key)) sb.AppendLine(p);
            }
            Assert.IsEmpty(problems, "Critical UX violations (" + seen.Count + " distinct):\n" + sb);
        }

        [UnityTest]
        public IEnumerator MetaScreensHaveNoOverlapAndFitEveryAspect() { return MetaAudit(100); }

        /// <summary>CUX-5: every screen must survive the 130 percent text size.</summary>
        [UnityTest]
        public IEnumerator MetaScreensSurvive130PercentText() { return MetaAudit(130); }

        IEnumerator MetaAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            for (int i = 1; i <= 6; i++)
                GameSession.Progress.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = i % 2 == 0 ? 7 : 3, Score = 100 * i, CoinsCollected = 30 });

            yield return CheckScreen("menu", () => meta.ShowMenu(), problems);
            // Account (docs/35 section 8): Home has no account button any more (B1.c); the pop-up code stays for Settings, so it is audited in the three states, plus the Settings account block signed in and in a build without sign-in.
            yield return CheckScreen("google-prompt", () => { AccountScenario("guest"); meta.ShowMenu(); meta.ShowGooglePrompt(); }, problems);
            yield return CheckScreen("menu-signed-in", () => { AccountScenario("signed-in"); meta.ShowMenu(); }, problems);
            yield return CheckScreen("account-popup-guest", () => { AccountScenario("guest"); meta.ShowMenu(); meta.ShowAccount(); }, problems);
            yield return CheckScreen("account-popup-signed-in", () => { AccountScenario("signed-in"); meta.ShowMenu(); meta.ShowAccount(); }, problems);
            yield return CheckScreen("account-popup-unavailable", () => { AccountScenario("unavailable"); meta.ShowMenu(); meta.ShowAccount(); }, problems);
            // Settings (Bug List 1 B2): the compact popup as guest, signed in (Unlink Google Account), in a build without sign-in, with the Admin icon, and with the Save Progress result on its button.
            yield return CheckScreen("settings-signed-in", () => { AccountScenario("signed-in"); meta.ShowSettings(); }, problems);
            yield return CheckScreen("settings-unavailable", () => { AccountScenario("unavailable"); meta.ShowSettings(); }, problems);
            yield return CheckScreen("settings-guest-admin-icon", () => { AccountScenario("guest"); GameSettings.ShowDevRowsForTests = true; meta.ShowSettings(); GameSettings.ShowDevRowsForTests = false; }, problems);
            yield return CheckScreen("settings-guest-saved", () => { AccountScenario("guest"); meta.ShowSettings(); FindRt("SaveProgress").GetComponent<Button>().onClick.Invoke(); }, problems);
            AccountScenario("guest");
            yield return CheckScreen("map", () => meta.ShowMap(), problems);
            yield return CheckScreen("levels", () => meta.ShowLevels(r1), problems);
            yield return CheckScreen("settings-guest", () => meta.ShowSettings(), problems);
            var levels = GameSession.Repo.LevelsOf(r1.Id);
            yield return CheckScreen("level-popup", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); }, problems);
            yield return CheckScreen("locked-popup", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[12]); }, problems);
            // Power-ups: no loadout any more (2026-10-31); the level and endless popups with and without owned power-ups look the same.
            yield return CheckScreen("endless-popup-empty", () => { meta.ShowLevels(r1); meta.OnEndlessTapped(r1); }, problems);
            GrantAllPowerUps(3);
            yield return CheckScreen("level-popup-with-powerups", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); }, problems);
            yield return CheckScreen("endless-popup-with-powerups", () => { meta.ShowLevels(r1); meta.OnEndlessTapped(r1); }, problems);
            yield return CheckScreen("region-locked-popup", () => { meta.ShowMap(); meta.OnRegionTapped(GameSession.Repo.GetRegionByIndex(2)); }, problems);
            yield return CheckScreen("coming-soon-popup", () => { meta.ShowMap(); meta.OnRegionTapped(GameSession.Repo.GetRegionByIndex(6)); }, problems);
            yield return CheckScreen("reset-popup", () => { meta.ShowSettings(); meta.ConfirmReset(); }, problems);
            // Settings extras (docs/38): the Admin popup (both pages), the reset confirm (signed-in text is the longest), the restore result and the hidden code box.
            // Admin (Bug List 1 B3): System Test OFF (PROD READY, the test rows dimmed), ON with the three switches off, ON with every switch on (the tallest), and the hero page.
            yield return CheckScreen("admin-system-off", () => { AdminScenario(false); meta.ShowSettings(); meta.ShowAdmin(); }, problems);
            yield return CheckScreen("admin-system-on", () => { AdminScenario(true, false); meta.ShowSettings(); meta.ShowAdmin(); }, problems);
            yield return CheckScreen("admin-system-on-all-switches", () => { AdminScenario(true, true); meta.ShowSettings(); meta.ShowAdmin(); }, problems);
            yield return CheckScreen("admin-main-rewards-on", () =>
            {
                AdminScenario(true, true);
                GameSession.Progress.AdminSetMainRewardsReady(true);   // E3: the button reads "Main rewards ON"
                meta.ShowSettings();
                meta.ShowAdmin();
                Assert.IsNotNull(FindRt("Main_rewards_ONButton"), "the Main rewards test button shows with System Test on");
            }, problems);
            GameSession.Progress.AdminSetMainRewardsReady(false);
            yield return CheckScreen("admin-heroes", () => { AdminScenario(true, true); meta.ShowSettings(); meta.ShowAdmin(1); }, problems);
            AdminCleanup();
            yield return CheckScreen("settings-reset-confirm", () => { meta.ShowSettings(); GameSession.Progress.SetAccount("google", "test"); meta.ConfirmReset(); }, problems);
            yield return CheckScreen("settings-restored", () => { meta.ShowSettings(); meta.ShowPurchasesRestored(2); }, problems);
            yield return CheckScreen("settings-nothing-to-restore", () => { meta.ShowSettings(); meta.ShowPurchasesRestored(0); }, problems);
            yield return CheckScreen("settings-code-box", () => { meta.ShowSettings(); meta.ShowAdminCodeBox(); }, problems);
            yield return CheckScreen("menu-music", () => { meta.ShowSettings(); meta.ShowMenuMusic(); }, problems);
            yield return CheckScreen("menu-music-locked", () => { MusicCatalog.Choice = "sky_circuit"; meta.ShowSettings(); meta.ShowMenuMusic(); }, problems);
            MusicCatalog.Choice = MusicCatalog.Random;
            GameSession.Progress.SetAccount("guest", "");

            // Characters: the chosen character on the land stage, the row of 3 icon tabs (All Heroes, Merge, Powers; no bottom bar) and the list of OWNED hero cards (one card per copy, no counts)
            yield return CheckScreen("characters", () => meta.ShowCharacters(), problems);
            ProgressService.TestAllCharacters = true;   // Character Test: only the five Common heroes are owned (Uncommon and above stay locked)
            yield return CheckScreen("characters-test", () => meta.ShowCharacters(), problems);
            foreach (CharacterDef hero in new[] { CharacterCatalog.ForestGirl, CharacterCatalog.BeachOtter, CharacterCatalog.CandyGirl, CharacterCatalog.Monk })
            {
                CharacterCatalog.Selected = hero;
                yield return CheckScreen("characters-" + hero.Id, () => meta.ShowCharacters(), problems);
                yield return CheckScreen("hero-reveal-" + hero.Id, () => { meta.ShowMenu(); meta.ShowHeroReveal(hero); }, problems);
            }
            CharacterCatalog.Selected = CharacterCatalog.Momo;
            ProgressService.TestAllCharacters = false;

            // Owned cards (Bug List 5 F2.d): duplicates are more cards (no number anywhere), the tick sits on the first card of the chosen type, and a long list scrolls.
            var savedCopies = new Dictionary<string, int>(GameSession.Progress.Data.Copies);
            var savedRevealed = new List<string>(GameSession.Progress.Data.HeroesRevealed);
            GameSession.Progress.HeroInventory.AddCopies("momo", 1);       // 2 Common Momo
            GameSession.Progress.HeroInventory.AddCopies("momo_u", 2);     // a new card type with 2 copies
            yield return CheckScreen("characters-duplicates", () => meta.ShowCharacters(), problems);
            meta.ShowCharacters();
            yield return null;
            var cardNames = new List<string>();
            int ticks = 0;
            foreach (Image im in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
            {
                if (im.name.StartsWith("Char_")) cardNames.Add(im.name);
                if (im.name == CharactersScreen.ChosenName) { ticks++; Assert.AreEqual("Char_momo", im.transform.parent.name, "the tick is on the first card of the chosen type"); }
            }
            CollectionAssert.AreEquivalent(new[] { "Char_momo", "Char_momo_2", "Char_momo_u", "Char_momo_u_2" }, cardNames, "one card per copy");
            Assert.AreEqual(1, ticks, "exactly one tick");
            foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                for (Transform up = t.transform.parent; up != null; up = up.parent)
                    Assert.IsFalse(up.name.StartsWith("Char_"), "no count or text on a hero card");
            GameSession.Progress.HeroInventory.AddCopies("momo", 10);      // 12 Momo
            foreach (string extra in new[] { "forestgirl", "candygirl" }) { GameSession.Progress.HeroInventory.AddCopies(extra, 4); GameSession.Progress.MarkHeroRevealed(extra); }
            yield return CheckScreen("characters-many", () => meta.ShowCharacters(), problems);
            GameSession.Progress.Data.Copies.Clear();
            foreach (KeyValuePair<string, int> kv in savedCopies) GameSession.Progress.Data.Copies[kv.Key] = kv.Value;
            GameSession.Progress.Data.HeroesRevealed.Clear();
            GameSession.Progress.Data.HeroesRevealed.AddRange(savedRevealed);

            // Hero sheet (docs/21): Common Momo on both tabs, plus fake heroes of every higher rarity (long name, long region, maximum stats, powers) so the banner colours and the longest texts are audited.
            yield return CheckScreen("hero-sheet-common", () => meta.ShowHeroSheet(CharacterCatalog.Momo), problems);
            yield return CheckScreen("hero-sheet-common-details", () => { meta.ShowHeroSheet(CharacterCatalog.Momo); meta.CurrentHeroSheet.SelectTab(1); }, problems);
            var maxStats = new CharacterStats { JumpLaunch = 23, MaxSpeed = 12, Control = 45, WallBoostHundredths = 100, CoinFind = 50, GemFind = 5 };
            foreach (CharacterRarity rar in new[] { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary })
            {
                CharacterRarity rr = rar;
                int powers = CharacterDefs.PowerCount[(int)rr];
                var list = new[] { PowerUp.Flight, PowerUp.DoubleJump, PowerUp.SlowTime, PowerUp.Glide };   // up to 4 powers (Legendary)
                var pw = new PowerUp[powers];
                for (int i = 0; i < powers; i++) pw[i] = list[i];
                string label = "hero-sheet-" + rr.ToString().ToLowerInvariant();
                HeroSheetData fake = HeroSheetData.From("Starlight Dragon", rr, "Crystal Caverns of Dawn", maxStats, pw);
                yield return CheckScreen(label, () => meta.ShowHeroSheet(CharacterCatalog.Momo, null, fake), problems);
                yield return CheckScreen(label + "-details", () => { meta.ShowHeroSheet(CharacterCatalog.Momo, null, fake); meta.CurrentHeroSheet.SelectTab(1); }, problems);
            }
            // entry points (owner 2026-11-02): the HOME screen has no (i) button and no hero tap area; the HEROES screen has the (i) button right of the shown hero (at least 48 dp)
            meta.ShowMenu();
            yield return null;
            foreach (Button bt in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                Assert.IsFalse(bt.name == "HeroInfo" || bt.name == "HeroTap", bt.name + " must not exist on the home screen");
            meta.ShowCharacters();
            yield return null;
            // Bug List 6 item 3a/3b: three icon tabs on top, no bottom bar (no Shop / Play / Character buttons, no tab bar behind them)
            foreach (string tab in HeroTabs.ButtonNames) Assert.IsNotNull(FindRt(tab), tab + " is missing on the Heroes screen");
            foreach (string gone in new[] { "TabBar", "TabShop", "TabPlay", "TabCharacters", "TabPower-ups", "AllHeroesButton", "MergeButton" })
                Assert.IsNull(FindRt(gone), gone + " must not exist on the Heroes screen any more");
            Button found = null;
            foreach (Button bt in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) if (bt.name == "HeroInfo") found = bt;
            Assert.IsNotNull(found, "HeroInfo is missing on the Heroes screen");
            Assert.GreaterOrEqual(((RectTransform)found.transform).rect.width, UiKit.MinTouchPx - 1f, "HeroInfo is smaller than 48 dp");
            found.onClick.Invoke();
            Assert.AreEqual("Hero", meta.CurrentScreen, "HeroInfo must open the hero sheet");
            // Owner 2026-11-25: the red X on the hero sheet returns to the Heroes screen it was opened from, not to the main screen.
            Button sheetClose = null;
            foreach (Button bt in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) if (bt.name == "Close") sheetClose = bt;
            Assert.IsNotNull(sheetClose, "the hero sheet has no red X");
            sheetClose.onClick.Invoke();
            Assert.AreEqual("Characters", meta.CurrentScreen, "the X on the hero sheet must return to the Heroes screen");
            yield return null;

            // The one-time "New hero joins you!" pop-up (docs/21): card, hero picture, name, rarity and the single OK button on every aspect (and at 130 percent text in the second run).
            yield return CheckScreen("hero-reveal", () => { meta.ShowMenu(); meta.ShowHeroReveal(CharacterCatalog.ForestGirl); }, problems);
            AccountCleanup();
            Report(problems);
        }

        // ---- emulator report 2 regressions (2026-11-02): settings reset button, level popup, rewards region names, list ends ----

        static Rect CanvasBox(RectTransform rt)
        {
            Canvas c = rt.GetComponentInParent<Canvas>().rootCanvas;
            return ToCanvasUnits(rt, Camera.main, c.scaleFactor);
        }

        static RectTransform FindRt(string name, Transform under = null)
        {
            foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            {
                if (rt.name != name || !rt.gameObject.activeInHierarchy) continue;
                if (under != null && !IsDescendant(rt, under)) continue;
                return rt;
            }
            return null;
        }

        /// <summary>The touch-target rule is in dp on the real density: 720 x 1280 at 320 dpi needs 144 reference px (the old fixed 126 was 42 dp).</summary>
        [Test]
        public void TouchMinimumFollowsRealDensity()
        {
            Assert.GreaterOrEqual(UiKit.MinTouchRefFor(320f, 720, 1280), 143.9f, "720x1280 @320 dpi");
            Assert.GreaterOrEqual(UiKit.MinTouchRefFor(320f, 1080, 1920), UiKit.MinTouchPx - 0.1f, "1080x1920 @320 dpi never drops below the reference minimum");
            Assert.AreEqual(UiKit.MinTouchPx, UiKit.MinTouchRefFor(420f, 1080, 1920), 0.5f, "1080x1920 @420 dpi stays at the 411 dp reference");
            Assert.AreEqual(UiKit.MinTouchPx, UiKit.MinTouchRefFor(0f, 1080, 1920), 0.01f, "unknown dpi falls back");
        }

        /// <summary>
        /// Settings (Bug List 1 B2): one compact popup. All five switch cells, the two value buttons, the Google button, Save Progress, Reset, Restore, the Admin icon and
        /// the version tap sit inside the panel (the content scrolls only when the panel is at its height cap), every target is at least 48 dp and at least 8 dp from its
        /// neighbours, and the popup is much shorter than the old screen. Also checks the Google button label per state and that Save Progress writes locally / to Google.
        /// </summary>
        [UnityTest]
        public IEnumerator SettingsPopupIsCompactAndEverythingStaysInside()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            GameSettings.ShowDevRowsForTests = true;
            AccountScenario("guest");
            var holder = new RenderTexture[1];
            string[] names = { "SoundToggle", "MusicToggle", "HapticsToggle", "Reduced motionToggle", "Left-handedToggle", "Pace ghostToggle", "TextsizeButton", "Google", "SaveProgress", "Reset", "RestorePurchases", "PrivacyPolicy", "TermsOfUse", "Admin" };
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowSettings();
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform panel = FindRt("Panel");
                RectTransform content = FindRt("Content");
                if (panel == null || content == null) { problems.Add(a.Name + ": settings popup (Panel/Content) missing"); continue; }
                Rect pb = CanvasBox(panel);
                if (pb.height > 1500f) problems.Add(a.Name + ": the settings popup is " + Mathf.RoundToInt(pb.height) + " tall, it must stay compact (under 1500)");
                ScrollRect sr = content.GetComponentInParent<ScrollRect>();
                if (sr != null) problems.Add(a.Name + ": Settings must not scroll (Bug List 6 item 7) but has a ScrollRect");
                if (FindRt("DifficultyButton", panel) != null) problems.Add(a.Name + ": the Difficulty row moved to the level screen (Bug List 6 item 6)");
                bool scrolls = false;   // never: the screen has no scroll view, so every item must sit inside the panel
                var boxes = new List<KeyValuePair<string, Rect>>();
                foreach (string n in names)
                {
                    RectTransform rt = FindRt(n, panel);
                    if (rt == null) { problems.Add(a.Name + ": " + n + " missing"); continue; }
                    Rect bb = CanvasBox(rt);
                    boxes.Add(new KeyValuePair<string, Rect>(n, bb));
                    if (bb.width < UiKit.MinTouchPx - 1f || bb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": " + n + " below 48 dp");
                    if (!scrolls && (bb.xMin < pb.xMin || bb.xMax > pb.xMax || bb.yMin < pb.yMin || bb.yMax > pb.yMax)) problems.Add(a.Name + ": " + n + " leaves the popup " + pb);
                }
                for (int i = 0; i < boxes.Count; i++)
                    for (int j = i + 1; j < boxes.Count; j++)
                    {
                        Rect p = boxes[i].Value, q = boxes[j].Value;
                        float gap = Mathf.Max(Mathf.Max(p.xMin - q.xMax, q.xMin - p.xMax), Mathf.Max(p.yMin - q.yMax, q.yMin - p.yMax));
                        if (gap < UiKit.MinSpacingPx - 1f) problems.Add(a.Name + ": " + boxes[i].Key + " and " + boxes[j].Key + " are only " + Mathf.RoundToInt(gap) + " apart (needs 8 dp)");
                    }
                if (FindRt("Back") != null) problems.Add(a.Name + ": Settings has no Back button any more (the X closes it)");
                if (FindRt("RestorePurchases", panel) != null && FindRt("Reset", panel) != null && !(CanvasBox(FindRt("Reset", panel)).center.x < CanvasBox(FindRt("RestorePurchases", panel)).center.x))
                    problems.Add(a.Name + ": Reset and Restore are one row: Reset left, Restore right");
                RectTransform privacy = FindRt("PrivacyPolicy", panel), terms = FindRt("TermsOfUse", panel), restoreRt = FindRt("RestorePurchases", panel);
                if (privacy != null && terms != null && restoreRt != null)
                {
                    // owner 2026-11-09: the legal links are the last row, Privacy left and Terms right, directly under Reset / Restore
                    if (!(CanvasBox(privacy).center.x < CanvasBox(terms).center.x)) problems.Add(a.Name + ": Privacy and Terms are one row: Privacy left, Terms right");
                    if (!(CanvasBox(privacy).yMax <= CanvasBox(restoreRt).yMin + 1f)) problems.Add(a.Name + ": the Privacy / Terms row sits under Reset / Restore");
                    if (Mathf.Abs(CanvasBox(privacy).center.y - CanvasBox(terms).center.y) > 1f) problems.Add(a.Name + ": Privacy and Terms are not aligned");
                }
            }
            GameSettings.ShowDevRowsForTests = false;
            Restore(holder);
            Report(problems);
            AccountCleanup();
        }

        /// <summary>
        /// Owner 2026-11-09 (docs/47): the Privacy and Terms buttons open the hosted legal pages (https only) through LegalLinks.Opener, show the short words and
        /// tell screen readers that the browser opens. The cap rule stays pure: the fixed 1470 on a 3:4 canvas, the safe height minus the margins on a tall phone.
        /// </summary>
        [UnityTest]
        public IEnumerator SettingsPrivacyAndTermsButtonsOpenTheLegalPages()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var opened = new List<string>();
            LegalLinks.Opener = url => opened.Add(url);
            try
            {
                AccountScenario("guest");
                meta.ShowSettings();
                yield return null;
                RectTransform privacy = FindRt("PrivacyPolicy"), terms = FindRt("TermsOfUse");
                Assert.IsNotNull(privacy, "Privacy button");
                Assert.IsNotNull(terms, "Terms button");
                Assert.AreEqual(LegalLinks.PrivacyA11y, privacy.GetComponent<A11yLabel>().Text);
                Assert.AreEqual(LegalLinks.TermsA11y, terms.GetComponent<A11yLabel>().Text);
                Assert.AreEqual(LegalLinks.PrivacyLabel, privacy.GetComponentInChildren<Text>().text);
                Assert.AreEqual(LegalLinks.TermsLabel, terms.GetComponentInChildren<Text>().text);
                privacy.GetComponent<Button>().onClick.Invoke();
                terms.GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(2, opened.Count, "each tap opens exactly one page");
                Assert.AreEqual(LegalLinks.PrivacyUrl, opened[0]);
                Assert.AreEqual(LegalLinks.TermsUrl, opened[1]);
                Assert.IsTrue(LegalLinks.IsWebUrl(opened[0]) && LegalLinks.IsWebUrl(opened[1]), "https pages only");

                Assert.AreEqual(1470f, MetaController.SettingsCapFor(1660f), 0.01f, "3:4 canvas keeps the fixed cap");
                Assert.AreEqual(1471f, MetaController.SettingsCapFor(1663f), 0.01f, "the real 3:4 safe height is one unit above the fixed cap: still safe");
                Assert.AreEqual(1470f, MetaController.SettingsCapFor(1246f), 0.01f, "a short editor window keeps the fixed cap");
                Assert.AreEqual(1728f, MetaController.SettingsCapFor(1920f), 0.01f, "9:16 phone: safe height minus 96 above and below");
                Assert.AreEqual(1800f, MetaController.SettingsCapFor(2400f), 0.01f, "never above 1800");
            }
            finally
            {
                LegalLinks.ResetForTests();
                AccountCleanup();
            }
        }

        /// <summary>Google button: Sign in with Google (guest) or Unlink Google Account (signed in) with the existing sign-in logic; Save Progress: guest saves on the phone, signed in saves to Google.</summary>
        [UnityTest]
        public IEnumerator SettingsGoogleButtonAndSaveProgress()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            AccountScenario("guest");
            meta.ShowSettings();
            yield return null;
            Assert.AreEqual(AccountPopup.SignInLabel, FindRt("Google").GetComponent<A11yLabel>().Text, "guest: Sign in with Google");
            FindRt("SaveProgress").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(AccountPopup.SavedLocalText, meta.SaveButtonTextForTests, "guest: saved on this phone");
            Assert.IsFalse(AccountSync.IsGoogleMode);
            FindRt("Google").GetComponent<Button>().onClick.Invoke();   // the stub signs in at once and Settings rebuilds
            yield return null;
            yield return null;
            Assert.IsTrue(AccountSync.IsSignedIn, "signed in after the tap");
            Assert.AreEqual(AccountPopup.UnlinkLabel, FindRt("Google").GetComponent<A11yLabel>().Text, "signed in: Unlink Google Account");
            FindRt("SaveProgress").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(AccountPopup.SavedGoogleText, meta.SaveButtonTextForTests, "signed in: saved to Google");
            FindRt("Google").GetComponent<Button>().onClick.Invoke();   // unlink
            yield return null;
            yield return null;
            Assert.IsFalse(AccountSync.IsGoogleMode, "guest again after Unlink");
            Assert.AreEqual(AccountPopup.SignInLabel, FindRt("Google").GetComponent<A11yLabel>().Text);
            AccountCleanup();
        }

        /// <summary>The Admin popup: System Test master switch first, a PROD READY / TEST MODE pill, test rows disabled while it is off, and OFF clears every test switch.</summary>
        [UnityTest]
        public IEnumerator AdminPopupSystemTestMasterSwitch()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            AdminScenario(false);
            meta.ShowSettings();
            meta.ShowAdmin();
            yield return null;
            Assert.AreEqual(AdminAccess.PillText(false), FindRt("StatusPill").GetComponentInChildren<Text>().text, "System Test off: PROD READY");
            RectTransform master = FindRt("System TestToggle"), game = FindRt("Game TestToggle");
            Assert.IsNotNull(master);
            Assert.IsNotNull(game);
            Assert.Greater(CanvasBox(game).yMax, 0f);
            Assert.Greater(CanvasBox(master).yMin, CanvasBox(game).yMax - 1f, "System Test sits above Game Test");
            Assert.IsFalse(game.GetComponentInParent<CanvasGroup>().interactable, "Game Test is disabled while System Test is off");
            Assert.IsNull(FindRt("Big_walletButton"), "no test tools while System Test is off");
            Assert.IsNull(FindRt("Main_rewards_OFFButton"), "no Main rewards test button while System Test is off");
            master.GetComponent<Button>().onClick.Invoke();   // System Test on
            yield return null;
            yield return null;
            Assert.IsTrue(GameSettings.SystemTest);
            Assert.AreEqual(AdminAccess.PillText(true), FindRt("StatusPill").GetComponentInChildren<Text>().text, "System Test on: TEST MODE");
            FindRt("Shop TestToggle").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsTrue(GameSettings.ShopTest);
            Assert.IsNotNull(FindRt("Big_walletButton"), "Shop Test on: its tools show");
            Assert.IsNotNull(FindRt("Main_rewards_OFFButton"), "System Test on: the Main rewards test button shows (Game Test group)");
            FindRt("System TestToggle").GetComponent<Button>().onClick.Invoke();   // System Test off again: everything off
            yield return null;
            yield return null;
            Assert.IsFalse(GameSettings.SystemTest);
            Assert.IsFalse(GameSettings.ShopTest);
            Assert.IsFalse(GameSettings.GameJumpTest);
            Assert.IsFalse(GameSettings.CharacterTest);
            Assert.IsNull(FindRt("Big_walletButton"));
            AdminCleanup();
        }

        /// <summary>Tests: an install where the admin code was accepted (so the test switches are real), with System Test on or off and optionally every sub switch on.</summary>
        static void AdminScenario(bool systemTest, bool subSwitches = true)
        {
            PlayerPrefs.SetInt("settings.adminunlocked", 1);
            GameSettings.SystemTest = systemTest;
            if (!systemTest) return;
            GameSettings.GameJumpTest = subSwitches;
            GameSettings.ShopTest = subSwitches;
            GameSettings.CharacterTest = subSwitches;
        }

        static void AdminCleanup()
        {
            foreach (string k in new[] { "settings.adminunlocked", "settings.systemtest", "settings.gamejumptest", "settings.shoptest", "settings.charactertest" }) PlayerPrefs.DeleteKey(k);
            GameSettings.ApplyFlags();
        }
        /// <summary>Puts the account layer in one of three states: "guest" (stub available), "signed-in" (stub signed in as Test Player, save linked) or "unavailable" (release build without the plugin).</summary>
        static void AccountScenario(string which)
        {
            ProgressService p = GameSession.Progress;
            AccountSync.Hook(p);
            if (which == "unavailable") { Services.Account = new UnavailableAccountService(); p.SetAccount("guest", ""); return; }
            var stub = new StubAccountService { UseMemoryOnly = true };
            Services.Account = stub;
            if (which == "signed-in") { stub.SignIn(null); p.SetAccount("google", stub.PlayerId); }
            else p.SetAccount("guest", "");
        }

        static void AccountCleanup()
        {
            GameSession.Progress.SetAccount("guest", "");
            Services.Account = null;   // back to the default (stub in the editor)
            AccountSync.ResetForTests();
        }

        /// <summary>
        /// Bug List 1 B1.a to B1.e: Home has no tab bar, no Heroes/Rewards/Shop buttons, no round Account button and no wallet; ONE Play button (icon only, no text)
        /// runs end to end (safe width minus the 16 unit side margins) at the bottom; the blue Settings button sits just above it; Momo and the version label stay.
        /// </summary>
        [UnityTest]
        public IEnumerator HomeHasOneBigPlayButtonAndSettingsAboveIt()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowMenu();
                yield return ApplyAspect(a, holder);
                yield return null;
                foreach (string gone in new[] { "TabBar", "TabShop", "TabPlay", "TabCharacters", "TabVIP", "Rewards", "Account", "CurrencyBar", "CoinsPill", "GemsPill", "StarsPill", "WalletPanel" })
                    if (FindRt(gone) != null) problems.Add(a.Name + ": " + gone + " must not be on Home");
                RectTransform play = FindRt("Play"), settings = FindRt("Settings"), safe = FindRt("Safe");
                if (play == null || settings == null) { problems.Add(a.Name + ": Home needs Play and Settings"); continue; }
                Rect pb = CanvasBox(play), sb = CanvasBox(settings);
                Rect screenBox = safe != null ? CanvasBox(safe) : new Rect(0f, 0f, UiKit.CanvasSizeRef().x, UiKit.CanvasSizeRef().y);
                if (Mathf.Abs(pb.xMin - screenBox.xMin - 16f) > 1.5f || Mathf.Abs(screenBox.xMax - pb.xMax - 16f) > 1.5f) problems.Add(a.Name + ": Play does not run end to end (" + pb + " in " + screenBox + ")");
                if (pb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": Play is below 48 dp");
                if (pb.yMin - screenBox.yMin > 40f) problems.Add(a.Name + ": Play must sit at the bottom");
                if (play.Find("Label") != null || play.GetComponentInChildren<Text>() != null) problems.Add(a.Name + ": Play must have no text");
                Transform icon = play.Find("Icon");
                if (icon == null) problems.Add(a.Name + ": Play needs its icon");
                else
                {
                    Rect ib = CanvasBox((RectTransform)icon);
                    if (Mathf.Abs(ib.center.x - pb.center.x) > 1.5f || Mathf.Abs(ib.center.y - pb.center.y) > 1.5f) problems.Add(a.Name + ": the Play icon is not centred");
                }
                if (play.GetComponent<A11yLabel>() == null || play.GetComponent<A11yLabel>().Text != "Play") problems.Add(a.Name + ": Play needs the label Play");
                if (sb.width < UiKit.MinTouchPx - 1f || sb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": Settings is below 48 dp");
                if (sb.yMin < pb.yMax) problems.Add(a.Name + ": Settings must sit above Play");
                if (sb.Overlaps(pb)) problems.Add(a.Name + ": Settings overlaps Play");
                if (settings.Find("Icon") == null) problems.Add(a.Name + ": Settings needs its icon image named Icon");
                if (settings.GetComponent<A11yLabel>() == null || settings.GetComponent<A11yLabel>().Text != "Settings") problems.Add(a.Name + ": Settings needs the label Settings");
                if (FindRt("Logo") == null) problems.Add(a.Name + ": Home keeps the logo");
                if (FindRt("Momo") != null || FindRt("Hero") != null) problems.Add(a.Name + ": Home has no big hero any more (owner 2026-11-12)");
                if (FindRt("Version") != null) problems.Add(a.Name + ": Home has no version label any more (owner 2026-11-12)");
                if (FindRt("Logo") != null && CanvasBox(FindRt("Logo")).Overlaps(sb)) problems.Add(a.Name + ": the logo overlaps Settings");

                // the heroes bar has no crown any more (Bug List 1, B5.d)
                meta.ShowCharacters();
                yield return null;
                if (FindRt("TabVIP") != null) problems.Add(a.Name + ": the crown is in the Shop now, not on the heroes bar");
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>Owner 2026-11-12: the title banner sits at exactly the same rect on Home and on the login screen (centre or just above), neither screen has a big hero, Home has no version label.</summary>
        [UnityTest]
        public IEnumerator HomeAndLoginShareTheBannerAndHaveNoHeroOrVersion()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                MetaUiActive(true);
                DropLoginCanvas();
                meta.ShowMenu();
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform homeLogo = FindRt("Logo");
                if (homeLogo == null) { problems.Add(a.Name + ": Home needs the logo"); continue; }
                Rect homeBox = CanvasBox(homeLogo);
                if (FindRt("Momo") != null || FindRt("Hero") != null) problems.Add(a.Name + ": Home must not show a big hero");
                if (FindRt("Version") != null) problems.Add(a.Name + ": Home must not show the version label");

                ShowLoginTest(false, true);   // hides the Meta UI, so only the login's Logo is active
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform loginLogo = FindRt("Logo");
                if (loginLogo == null) problems.Add(a.Name + ": Login needs the logo");
                else
                {
                    Rect loginBox = CanvasBox(loginLogo);
                    if (Mathf.Abs(loginBox.x - homeBox.x) > 1f || Mathf.Abs(loginBox.y - homeBox.y) > 1f || Mathf.Abs(loginBox.width - homeBox.width) > 1f || Mathf.Abs(loginBox.height - homeBox.height) > 1f)
                        problems.Add(a.Name + ": the banner differs between Home " + homeBox + " and Login " + loginBox);
                }
                if (FindRt("Hero") != null || FindRt("Momo") != null) problems.Add(a.Name + ": Login must not show a hero");
                if (FindRt("Version") != null) problems.Add(a.Name + ": Login must not show the version label");
                if (FindRt("Bg") == null) problems.Add(a.Name + ": Login needs the same meadow background as Home");
                DropLoginCanvas();
                MetaUiActive(true);
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>The Google connect pop-up: a G icon, one line and two icon buttons (Open Settings, Later); the gear opens Settings, Later closes it.</summary>
        [UnityTest]
        public IEnumerator GooglePromptHasIconButtonsThatOpenSettingsOrClose()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            meta.ShowMenu();
            meta.ShowGooglePrompt();
            yield return null;
            Assert.IsNotNull(FindRt("GoogleIcon"), "the G icon");
            Assert.IsNotNull(FindRt("Line"), "the one line");
            RectTransform open = FindRt("OpenSettings"), later = FindRt("Later");
            Assert.IsNotNull(open); Assert.IsNotNull(later);
            Assert.AreEqual("Open Settings", open.GetComponent<A11yLabel>().Text);
            Assert.AreEqual("Later", later.GetComponent<A11yLabel>().Text);
            Assert.IsTrue(CanvasBox(open).width >= UiKit.MinTouchPx - 1f && CanvasBox(later).width >= UiKit.MinTouchPx - 1f, "48 dp targets");
            later.GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsNull(FindRt("OpenSettings"), "Later closes the pop-up");
            Assert.AreEqual("Menu", meta.CurrentScreen);
            meta.ShowGooglePrompt();
            yield return null;
            FindRt("OpenSettings").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual("Settings", meta.CurrentScreen, "the gear opens Settings");
            Assert.IsNull(FindRt("OpenSettings"));
        }

        /// <summary>The automatic prompt on Home: after a completed level, as a guest, once; never for a fresh player.</summary>
        [UnityTest]
        public IEnumerator GooglePromptShowsOnHomeOnlyAfterAFirstCompletedLevelAndOncePerSession()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            AccountScenario("guest");
            GoogleConnectPrompt.ResetForTests();
            meta.ShowMenu();
            yield return null;
            Assert.IsNull(FindRt("OpenSettings"), "nothing completed yet: no prompt");
            GameSession.Progress.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 7, Score = 100, CoinsCollected = 5 });
            meta.ShowMenu();
            yield return null;
            Assert.IsNotNull(FindRt("OpenSettings"), "guest after a completed level: the prompt shows");
            Assert.IsTrue(GoogleConnectPrompt.ShownThisSession);
            Assert.IsNotEmpty(GameSession.Progress.Data.LastGooglePromptUtc, "the day is stored");
            FindRt("Later").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            meta.ShowMenu();
            yield return null;
            Assert.IsNull(FindRt("OpenSettings"), "once per session");
            AccountCleanup();
            GoogleConnectPrompt.ResetForTests();
            GoogleConnectPrompt.AutoShow = false;
        }

        /// <summary>The Account pop-up in each state: the right buttons, the right texts, taps do what they say and the pop-up rebuilds into the new state.</summary>
        [UnityTest]
        public IEnumerator AccountPopupStatesAndActions()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            AccountScenario("guest");
            meta.ShowMenu();
            meta.ShowAccount();
            yield return null;
            Assert.IsNotNull(FindRt("Google"), "guest: Sign in with Google Play");
            Assert.IsNull(FindRt("SyncNow"), "guest: no Sync now");
            Assert.IsNotNull(FindRt("CloseX"), "a Close X");
            FindRt("Google").GetComponent<Button>().onClick.Invoke();   // the stub signs in at once
            yield return null;
            yield return null;
            Assert.IsTrue(AccountSync.IsSignedIn, "signed in after the tap");
            Assert.IsNotNull(FindRt("SyncNow"), "signed in: Sync now");
            Assert.IsNotNull(FindRt("SignOut"), "signed in: Sign out");
            Assert.IsNull(FindRt("Google"), "signed in: no sign-in button");
            FindRt("SignOut").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsFalse(AccountSync.IsGoogleMode, "guest again after Sign out");
            Assert.IsNotNull(FindRt("Google"), "guest again: Sign in with Google Play");
            AccountScenario("unavailable");
            meta.ShowMenu();
            meta.ShowAccount();
            yield return null;
            Assert.IsNull(FindRt("Google"), "unavailable: no sign-in button");
            Assert.IsNull(FindRt("SyncNow"), "unavailable: no Sync now");
            Assert.IsNotNull(FindRt("CloseX"), "unavailable: only close");
            AccountCleanup();
        }

        /// <summary>Level popup: exactly one close control, PLAY and the close X at least 48 dp, and no big empty middle between the pills and PLAY.</summary>
        [UnityTest]
        public IEnumerator LevelPopupHasOneCloseAndNoEmptyMiddle()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            var levels = GameSession.Repo.LevelsOf(r1.Id);
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowLevels(r1);
                meta.OnLevelTapped(levels[0]);
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform popup = FindRt("Popup");
                RectTransform panel = popup != null ? popup.Find("Panel") as RectTransform : null;
                if (panel == null) { problems.Add(a.Name + ": level popup missing"); continue; }
                int closes = 0;
                Rect playBox = default(Rect);
                foreach (Button b in panel.GetComponentsInChildren<Button>(false))
                {
                    Rect bb = CanvasBox((RectTransform)b.transform);
                    if (b.name == "Close" || b.name == "CloseX") closes++;
                    if (b.name == "PLAY") playBox = bb;
                    if (bb.width < UiKit.MinTouchPx - 1f || bb.height < UiKit.MinTouchPx - 1f) problems.Add(a.Name + ": popup button " + b.name + " below 48 dp");
                }
                if (closes != 1) problems.Add(a.Name + ": level popup has " + closes + " close buttons, expected 1");
                if (playBox.height <= 0f) { problems.Add(a.Name + ": PLAY missing"); continue; }
                // the lowest content row (pills) to PLAY: a gap, not a void
                float lowestContent = float.MaxValue;
                foreach (Transform t in panel)
                {
                    if (t.name == CrownInfoUi.RowName || t.name == "PaysLine" || t.name.StartsWith("Star")) lowestContent = Mathf.Min(lowestContent, CanvasBox((RectTransform)t).yMin);
                }
                float gapToPlay = lowestContent - playBox.yMax;
                if (gapToPlay > 160f) problems.Add(a.Name + ": empty middle of " + gapToPlay.ToString("F0") + " between the content and PLAY");
                if (gapToPlay < 16f) problems.Add(a.Name + ": content touches PLAY (" + gapToPlay.ToString("F0") + ")");
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>Settings (Bug List 6 item 7): no ScrollRect anywhere, every row inside the popup, at 100 and 130 percent text on every aspect and with a notch.</summary>
        [UnityTest]
        public IEnumerator SettingsHasNoScrollViewAndFitsAt130Percent()
        {
            foreach (int percent in new[] { 100, 130 })
            {
                GameSettings.TextPercent = percent;
                yield return Load("Meta");
                var meta = Object.FindFirstObjectByType<MetaController>();
                var problems = new List<string>();
                GameSettings.ShowDevRowsForTests = true;
                AccountScenario("guest");
                yield return CheckScreen("settings-" + percent, () => meta.ShowSettings(), problems);
                meta.ShowSettings();
                yield return null;
                RectTransform panel = FindRt("Panel");
                Assert.IsNotNull(panel);
                Assert.AreEqual(0, panel.GetComponentsInChildren<ScrollRect>(true).Length, "Settings has no ScrollRect at " + percent + " percent");
                Assert.IsNull(panel.GetComponentInParent<ScrollRect>(), "Settings sits in no scroll view");
                Assert.IsNull(FindRt("DifficultyButton", panel), "no Difficulty row in Settings");
                GameSettings.ShowDevRowsForTests = false;
                AccountCleanup();
                Report(problems);
            }
        }

        /// <summary>
        /// Level screen (owner Final Change 1, 2026-11-22). From the top: the wallet and progress pills, THEN the five Gameplay settings (Relaxed, Easy, Normal, Hard, Insane),
        /// THEN the region name row with the Endless button, then the level path. The settings have NO button plates: each slot (icon plus name) is the button, at least 48 dp
        /// with 8 dp between neighbours. Exactly one is selected and it is told apart three ways (never colour alone): a white ring around its icon, a larger icon and a bold
        /// white name; the other four are smaller and slightly faded. Every name has an outline over the art, sits under its icon and never touches its neighbour or leaves the
        /// safe area. The accessibility text names the setting, its speed rule and its reward bonus. The Endless button is wider than the old 136 square and carries the
        /// infinity and the stopwatch glyph side by side (one button: it opens the Endless popup with START and Time Attack). Checked at 100 and 130 percent text, on every
        /// aspect, with and without a notch. Picking a setting stores it and moves the mark.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelScreenHasGameplayRowAboveRegionName()
        {
            foreach (int percent in new[] { 100, 130 })
            {
                GameSettings.TextPercent = percent;
                yield return Load("Meta");
                var meta = Object.FindFirstObjectByType<MetaController>();
                RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
                var problems = new List<string>();
                var holder = new RenderTexture[1];
                string[] names = GameSettings.DifficultyNames;
                bool popupChecked = false;
                foreach (Aspect a in Aspects)
                {
                    foreach (Rect safe in new[] { FullSafe, NotchSafe })
                    {
                        Rect safeNow = safe;
                        UiKit.SafeAreaNormalized = () => safeNow;
                        GameSettings.Difficulty = 0;
                        meta.ShowLevels(r1);
                        yield return ApplyAspect(a, holder);
                        yield return null;
                        string tag = a.Name + (safe == NotchSafe ? "+notch" : "") + "@" + percent + ": ";
                        RectTransform row = FindRt(MetaController.GameplayRowName), header = FindRt("Header"), titleRow = FindRt("TitleRow"), path = FindRt("PathViewport");
                        RectTransform pills = FindRt("RegionBar"), endless = FindRt("Endless");
                        if (row == null || header == null || titleRow == null || path == null || pills == null || endless == null) { problems.Add(tag + "Gameplay row, region name row, Endless button, progress pills or path missing"); continue; }
                        Rect rb = CanvasBox(row), hb = CanvasBox(header), tb = CanvasBox(titleRow), pb = CanvasBox(path), qb = CanvasBox(pills), eb = CanvasBox(endless);
                        float canvasScale = row.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                        float safeLeft = safe.xMin * a.W / canvasScale, safeRight = safe.xMax * a.W / canvasScale;

                        // order from the top: pills, Gameplay row, region name row, path (canvas y grows upward)
                        if (rb.yMax > qb.yMin + 1f) problems.Add(tag + "the Gameplay row must be below the progress pills");
                        if (qb.yMin - rb.yMax > 40f) problems.Add(tag + "the Gameplay row is not directly below the progress pills (" + Mathf.RoundToInt(qb.yMin - rb.yMax) + " gap)");
                        if (tb.yMax > rb.yMin + 1f) problems.Add(tag + "the region name row must be below the Gameplay row");
                        if (rb.yMin - tb.yMax > 40f) problems.Add(tag + "the region name row is not directly below the Gameplay row (" + Mathf.RoundToInt(rb.yMin - tb.yMax) + " gap)");
                        if (pb.yMax > tb.yMin + 1f) problems.Add(tag + "the level path must start below the region name row");
                        if (rb.xMin < safeLeft - 2f || rb.xMax > safeRight + 2f) problems.Add(tag + "the Gameplay row leaves the safe area");

                        // region name row: the name (even on two lines) stays inside its row and left of the Endless button
                        Text headerText = header.GetComponent<Text>();
                        if (headerText.preferredHeight > tb.height + 1f) problems.Add(tag + "the region name (" + Mathf.RoundToInt(headerText.preferredHeight) + " high) does not fit its row (" + Mathf.RoundToInt(tb.height) + ")");
                        if (hb.xMax > eb.xMin - 4f) problems.Add(tag + "the region name runs under the Endless button");
                        if (hb.center.x >= eb.center.x) problems.Add(tag + "the region name must be on the left, the Endless button on the right");
                        if (eb.yMin < tb.yMin - 1f || eb.yMax > tb.yMax + 1f || eb.xMax > tb.xMax + 1f) problems.Add(tag + "the Endless button leaves the region name row");

                        // Endless button: wider than the old 136 x 136, two glyphs side by side, one accessibility label
                        if (eb.height < UiKit.MinTouchPx - 1f) problems.Add(tag + "the Endless button is below 48 dp");
                        if (eb.width < 137f || eb.width < eb.height + 20f) problems.Add(tag + "the Endless button must be wider than the old 136 square (" + Mathf.RoundToInt(eb.width) + "x" + Mathf.RoundToInt(eb.height) + ")");
                        RectTransform infinity = FindRt(MetaController.EndlessGlyphName, endless), stopwatch = FindRt(MetaController.TimeAttackGlyphName, endless);
                        if (infinity == null || stopwatch == null || infinity.GetComponent<Image>().sprite != Glyphs.Infinity || stopwatch.GetComponent<Image>().sprite != TimedIcons.Stopwatch)
                            problems.Add(tag + "the Endless button must carry the infinity glyph and the stopwatch glyph");
                        else
                        {
                            Rect ib2 = CanvasBox(infinity), sb2 = CanvasBox(stopwatch);
                            if (ib2.Overlaps(sb2)) problems.Add(tag + "the two glyphs of the Endless button overlap");
                            if (Mathf.Abs(ib2.center.y - sb2.center.y) > 1f || ib2.center.x >= sb2.center.x) problems.Add(tag + "the glyphs must sit side by side, infinity left, stopwatch right");
                            if (ib2.xMin < eb.xMin - 0.5f || sb2.xMax > eb.xMax + 0.5f || ib2.yMin < eb.yMin - 0.5f || ib2.yMax > eb.yMax + 0.5f) problems.Add(tag + "a glyph leaves the Endless button");
                            if (Mathf.Abs(ib2.width - ib2.height) > 0.5f || Mathf.Abs(sb2.width - sb2.height) > 0.5f || !infinity.GetComponent<Image>().preserveAspect || !stopwatch.GetComponent<Image>().preserveAspect)
                                problems.Add(tag + "the glyphs of the Endless button must not be stretched");
                        }
                        A11yLabel endlessLabel = endless.GetComponent<A11yLabel>();
                        if (endlessLabel == null || endlessLabel.Text != "Endless and Time Attack") problems.Add(tag + "the Endless button needs the accessibility label \"Endless and Time Attack\"");

                        // the five settings
                        var boxes = new List<Rect>();
                        var nameBoxes = new List<Rect>();
                        float left = float.MaxValue, right = float.MinValue, selectedIcon = 0f, biggestOther = 0f;
                        for (int i = 0; i < names.Length; i++)
                        {
                            RectTransform b = FindRt("Gameplay" + names[i], row);
                            if (b == null) { problems.Add(tag + "button " + names[i] + " missing"); continue; }
                            Rect bb = CanvasBox(b);
                            boxes.Add(bb);
                            left = Mathf.Min(left, bb.xMin);
                            right = Mathf.Max(right, bb.xMax);
                            if (b.GetComponent<Button>() == null) problems.Add(tag + names[i] + " slot is not a button");
                            if (bb.width < UiKit.MinTouchPx - 1f || bb.height < UiKit.MinTouchPx - 1f) problems.Add(tag + names[i] + " below 48 dp");
                            if (bb.xMin < rb.xMin - 1f || bb.xMax > rb.xMax + 1f || bb.yMin < rb.yMin - 1f || bb.yMax > rb.yMax + 1f) problems.Add(tag + names[i] + " leaves the row");
                            Image plate = b.GetComponent<Image>();
                            if (plate != null && (plate.sprite != null || plate.color.a > 0.05f)) problems.Add(tag + names[i] + " must have no button plate (no green or yellow plate any more)");
                            Transform iconT = b.Find(MetaController.GameplayIconName), labelT = b.Find(MetaController.GameplayLabelName);
                            if (iconT == null || labelT == null) { problems.Add(tag + names[i] + " needs an icon and a name"); continue; }
                            if (b.Find(MetaController.GameplayRingName) != null) problems.Add(tag + names[i] + " must have no white ring any more (Final Change 3)");
                            Image icon = iconT.GetComponent<Image>();
                            Text label = labelT.GetComponent<Text>();
                            bool on = i == GameSettings.Difficulty;   // the selected one is the bigger icon with the bold white name (no ring since Final Change 3)
                            if (b.Find("Check") != null) problems.Add(tag + names[i] + " must not have a tick mark (Bug List 8 item 2a)");

                            // icon: present, never stretched, clearly larger than the old 88 px; the selected one is full strength, the others slightly faded
                            if (icon.sprite == null) problems.Add(tag + names[i] + " has no icon (fallback glyph missing)");
                            Rect ib = CanvasBox(icon.rectTransform);
                            if (Mathf.Abs(ib.width - ib.height) > 0.5f || !icon.preserveAspect) problems.Add(tag + names[i] + " icon must not be stretched");
                            if (ib.width < 100f) problems.Add(tag + names[i] + " icon must be clearly larger than the old 88 px (" + Mathf.RoundToInt(ib.width) + ")");
                            if (ib.xMin < bb.xMin - UiKit.MinSpacingPx || ib.xMax > bb.xMax + UiKit.MinSpacingPx || ib.yMax > bb.yMax + 0.5f) problems.Add(tag + names[i] + " icon leaves its slot");
                            if (on)
                            {
                                selectedIcon = ib.width;
                                if (icon.color.a < 0.99f) problems.Add(tag + names[i] + " is selected: its icon must not be faded");
                                if (Mathf.Abs(ib.width - MetaController.GameplaySelectedIconPx) > 1f) problems.Add(tag + names[i] + " the selected icon must fill the old ring box (" + Mathf.RoundToInt(ib.width) + " vs " + MetaController.GameplaySelectedIconPx + ")");
                                if (label.fontStyle != FontStyle.Bold) problems.Add(tag + names[i] + " is selected: its name must be bold");
                                if (label.color.r < 0.99f || label.color.g < 0.99f || label.color.b < 0.99f || label.color.a < 0.99f) problems.Add(tag + names[i] + " is selected: its name must be white");
                            }
                            else
                            {
                                biggestOther = Mathf.Max(biggestOther, ib.width);
                                if (icon.color.a > 0.9f || icon.color.a < 0.5f) problems.Add(tag + names[i] + " is not selected: its icon must be slightly faded (alpha " + icon.color.a.ToString("F2") + ")");
                                if (label.fontStyle == FontStyle.Bold) problems.Add(tag + names[i] + " is not selected: its name must not be bold");
                            }

                            // name: 16 sp, outlined (it sits on the art without a plate), under the icon and the ring
                            if (label.text != names[i]) problems.Add(tag + names[i] + " name text is \"" + label.text + "\"");
                            if (label.fontSize < UiKit.MinBodyTextPx - 1f) problems.Add(tag + names[i] + " name below 16 sp");
                            var outline = label.GetComponent<Outline>();
                            if (outline == null || !outline.enabled || outline.effectColor.a < 0.5f) problems.Add(tag + names[i] + " name needs an outline over the art (CUX-5)");
                            Rect lb = CanvasBox(label.rectTransform);
                            if (lb.yMax > ib.yMin + 1f) problems.Add(tag + names[i] + " name must sit under its icon");
                            if (label.preferredHeight > lb.height + 6f) problems.Add(tag + names[i] + " name is taller than its line");
                            if (lb.yMin < bb.yMin - 0.5f) problems.Add(tag + names[i] + " name leaves the row");
                            float half = label.preferredWidth * 0.5f;   // the glyphs are centred on the slot and may be a little wider than it (no plate to stay inside)
                            nameBoxes.Add(new Rect(lb.center.x - half, lb.yMin, 2f * half, lb.height));

                            // accessibility text: name, speed rule, reward bonus, "selected"
                            A11yLabel al = b.GetComponent<A11yLabel>();
                            string said = al != null ? al.Text : "";
                            if (!said.StartsWith(names[i])) problems.Add(tag + names[i] + " accessibility text must start with the name: \"" + said + "\"");
                            if (!said.Contains("speed plus " + SimConfig.DifficultyStepPercent[i] + " percent every 30 seconds, up to plus " + SimConfig.TopSpeedUpPercent(i) + " percent")) problems.Add(tag + names[i] + " accessibility text lacks the speed rule: \"" + said + "\"");
                            if (!said.Contains("crowns and coins plus " + SimConfig.RewardBonusPercent(i) + " percent")) problems.Add(tag + names[i] + " accessibility text lacks the reward bonus: \"" + said + "\"");
                            if (said.Contains("selected") != on) problems.Add(tag + names[i] + " accessibility text must say \"selected\" exactly when it is: \"" + said + "\"");
                            if (said != MetaController.GameplayA11yText(i, on)) problems.Add(tag + names[i] + " accessibility text differs from GameplayA11yText");
                        }
                        if (boxes.Count == names.Length)
                        {
                            for (int i = 1; i < boxes.Count; i++)
                            {
                                if (boxes[i].xMin < boxes[i - 1].xMax + UiKit.MinSpacingPx - 1f) problems.Add(tag + names[i - 1] + " and " + names[i] + " are closer than 8 dp");
                                if (boxes[i].center.x <= boxes[i - 1].center.x) problems.Add(tag + "buttons are not in the order Relaxed to Insane");
                            }
                            float rowCentre = rb.center.x, used = (left + right) * 0.5f;
                            if (Mathf.Abs(rowCentre - used) > 4f) problems.Add(tag + "the buttons are not centred in the row");
                        }
                        if (nameBoxes.Count == names.Length)
                        {
                            for (int i = 1; i < nameBoxes.Count; i++)
                                if (nameBoxes[i].xMin < nameBoxes[i - 1].xMax + 6f) problems.Add(tag + "the names " + names[i - 1] + " and " + names[i] + " touch or overlap");
                            if (nameBoxes[0].xMin < safeLeft - 2f || nameBoxes[nameBoxes.Count - 1].xMax > safeRight + 2f) problems.Add(tag + "a name leaves the safe area");
                        }
                        if (selectedIcon < biggestOther + 12f) problems.Add(tag + "the selected icon (" + Mathf.RoundToInt(selectedIcon) + ") must be clearly larger than the others (" + Mathf.RoundToInt(biggestOther) + ")");

                        // picking a setting stores it and moves ring, size, fade and bold name
                        RectTransform hard = FindRt("GameplayHard", row), relaxed = FindRt("GameplayRelaxed", row);
                        if (hard == null || relaxed == null) continue;
                        hard.GetComponent<Button>().onClick.Invoke();
                        if (GameSettings.Difficulty != 3) problems.Add(tag + "tapping Hard did not store the setting");
                        RectTransform hardIcon = hard.Find(MetaController.GameplayIconName) as RectTransform, relaxedIcon = relaxed.Find(MetaController.GameplayIconName) as RectTransform;
                        if (hardIcon != null && relaxedIcon != null && hardIcon.rect.width < relaxedIcon.rect.width + 12f) problems.Add(tag + "the larger icon did not move to Hard");
                        if (hard.Find(MetaController.GameplayLabelName).GetComponent<Text>().fontStyle != FontStyle.Bold || relaxed.Find(MetaController.GameplayLabelName).GetComponent<Text>().fontStyle == FontStyle.Bold) problems.Add(tag + "the bold name did not move to Hard");
                        if (!hard.GetComponent<A11yLabel>().Text.Contains("selected") || relaxed.GetComponent<A11yLabel>().Text.Contains("selected")) problems.Add(tag + "the accessibility text did not move \"selected\" to Hard");
                        if (GameSession.Cfg.ScrollStepPercent != SimConfig.DifficultyStepPercent[3]) problems.Add(tag + "Hard must set a speed-up step of " + SimConfig.DifficultyStepPercent[3] + " percent, got " + GameSession.Cfg.ScrollStepPercent);
                        float hardSlotW = CanvasBox(hard).width;
                        if (boxes.Count == names.Length && Mathf.Abs(hardSlotW - boxes[3].width) > 0.5f) problems.Add(tag + "choosing a setting must not change the slot widths");

                        // the one Endless button keeps its action: the Endless popup with START and Time Attack
                        if (!popupChecked)
                        {
                            popupChecked = true;
                            endless.GetComponent<Button>().onClick.Invoke();
                            yield return null;
                            if (FindRt("START") == null || FindRt(MetaController.TimeAttackButtonName) == null) problems.Add(tag + "the Endless button must open the Endless popup with START and Time Attack");
                        }
                    }
                }
                GameSettings.Difficulty = 0;
                GameSession.ApplyDifficulty();
                Restore(holder);
                Report(problems);
            }
        }

        /// <summary>The number of filled stars under a level node (S0..S2 at full strength), or -1 when the node shows no stars (locked).</summary>
        static int NodeStars(Transform node)
        {
            if (node.Find("S0") == null) return -1;
            int n = 0;
            for (int s = 0; s < 3; s++)
            {
                Transform t = node.Find("S" + s);
                if (t != null && t.GetComponent<Image>().color.a > 0.9f) n++;
            }
            return n;
        }

        static string PillValue(string pillName)
        {
            RectTransform pill = FindRt(pillName);
            Transform value = pill != null ? pill.Find("Value") : null;
            return value != null ? value.GetComponent<Text>().text : null;
        }

        /// <summary>
        /// Final Change 1 (owner 2026-11-22): stars, locks and the current level are kept PER Gameplay setting, so every setting has its own path through a region. Tapping a
        /// setting on the level screen redraws the WHOLE path in place: the stars under every tile, locked / open / current, both progress pills (levels of 25, stars of 75) and
        /// the scroll position; the wallet star pill keeps the sum over all settings; the gem badge reads "collected" once the level is cleared in ANY setting. The level popup
        /// shows the stars of the selected setting, "25 percent" only when THIS setting has 3 stars, and a locked level names the setting. The region lock popup names the rule.
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingTheGameplaySettingRedrawsStarsLocksAndPills()
        {
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1), r2 = GameSession.Repo.GetRegionByIndex(2);
            var levels = GameSession.Repo.LevelsOf(r1.Id);
            // Relaxed: levels 1..6 cleared (2, 3, 2, 3, 2, 3 stars = 15), level 7 is the current one. Hard: levels 1..4 cleared with 1 star each (4), level 5 is the current one.
            for (int i = 1; i <= 6; i++)
                GameSession.Progress.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = i % 2 == 0 ? 7 : 3, Score = 100 * i, CoinsCollected = 30, Mode = 0 });
            for (int i = 1; i <= 4; i++)
                GameSession.Progress.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = 1, Score = 100 * i, CoinsCollected = 30, Mode = 3 });
            int total = r1.Levels.Count;
            bool starArt = GameSession.Art != null && GameSession.Art.StarFilled != null && GameSession.Art.StarEmpty != null;
            bool gemArt = GameSession.Art != null && GameSession.Art.GemIcon != null;

            GameSettings.Difficulty = 0;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var holder = new RenderTexture[1];
            try
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowLevels(r1);
                yield return ApplyAspect(Aspects[0], holder);
                meta.ShowLevels(r1);   // again on the 9:16 canvas, so the path is laid out and scrolled for the size that is measured
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();

                // ---- Relaxed ----
                Assert.AreEqual("6/" + total, PillValue("LevelsPill"), "flag pill: levels cleared on Relaxed");
                Assert.AreEqual("15/" + total * 3, PillValue("RegionStarsPill"), "star pill: stars on Relaxed");
                Assert.AreEqual("19", PillValue("StarsPill"), "wallet star pill: all settings added up (15 Relaxed + 4 Hard)");
                RectTransform n2 = FindRt("Level2"), n5 = FindRt("Level5"), n7 = FindRt("Level7"), n8 = FindRt("Level8");
                Assert.IsNotNull(n2); Assert.IsNotNull(n5); Assert.IsNotNull(n7); Assert.IsNotNull(n8);
                if (starArt) Assert.AreEqual(3, NodeStars(n2), "Relaxed: level 2 shows its 3 Relaxed stars");
                StringAssert.Contains("3 of 3 stars on Relaxed", n2.GetComponent<A11yLabel>().Text);
                Assert.IsNotNull(n7.Find("Num"), "Relaxed: level 7 is open");
                Assert.AreEqual(1.15f, n7.localScale.x, 0.01f, "Relaxed: level 7 is the current level (bigger node)");
                StringAssert.Contains("current level", n7.GetComponent<A11yLabel>().Text);
                Assert.IsNull(n8.Find("Num"), "Relaxed: level 8 is locked");
                Assert.AreEqual(-1, NodeStars(n8), "a locked level shows no stars");
                StringAssert.Contains("locked on Relaxed", n8.GetComponent<A11yLabel>().Text);
                Rect vpBox = CanvasBox(FindRt("PathViewport"));
                Assert.IsTrue(vpBox.Contains(CanvasBox(n7).center), "Relaxed: the path is scrolled to its current level 7");

                // ---- tap Hard: the whole path follows, in place ----
                FindRt("GameplayHard").GetComponent<Button>().onClick.Invoke();
                yield return null;
                yield return null;
                Assert.AreEqual(3, GameSettings.Difficulty);
                Assert.AreEqual("Levels", meta.CurrentScreen, "the level screen stays open");
                Assert.AreEqual("4/" + total, PillValue("LevelsPill"), "flag pill: levels cleared on Hard");
                Assert.AreEqual("4/" + total * 3, PillValue("RegionStarsPill"), "star pill: stars on Hard");
                Assert.AreEqual("19", PillValue("StarsPill"), "the wallet star pill does not depend on the selected setting");
                int nodes = 0;
                foreach (Button b in FindRt("PathContent").GetComponentsInChildren<Button>(false)) if (b.name.StartsWith("Level")) nodes++;
                Assert.AreEqual(total, nodes, "one visible node per level after the redraw (the old nodes are gone)");
                n2 = FindRt("Level2"); n5 = FindRt("Level5"); n7 = FindRt("Level7");
                RectTransform n6 = FindRt("Level6");
                if (starArt) Assert.AreEqual(1, NodeStars(n2), "Hard: level 2 shows its 1 Hard star, not the 3 of Relaxed");
                StringAssert.Contains("1 of 3 stars on Hard", n2.GetComponent<A11yLabel>().Text);
                Assert.IsNotNull(n5.Find("Num"), "Hard: level 5 is open (level 4 is cleared on Hard)");
                Assert.AreEqual(1.15f, n5.localScale.x, 0.01f, "Hard: level 5 is the current level");
                if (starArt) Assert.AreEqual(0, NodeStars(n5), "Hard: level 5 has no Hard star yet");
                Assert.IsNull(n6.Find("Num"), "Hard: level 6 is locked although it is cleared on Relaxed");
                Assert.IsNull(n7.Find("Num"), "Hard: level 7 is locked");
                Assert.AreEqual(1f, n7.localScale.x, 0.01f, "Hard: level 7 is no longer the current level");
                if (gemArt)
                {
                    Transform gem = n5.Find(MetaController.GemBadgeName);
                    Assert.IsNotNull(gem, "level 5 is a gem level: the badge shows on the open tile");
                    StringAssert.Contains("collected", gem.GetComponent<A11yLabel>().Text, "gems are paid once per level: collected because level 5 is cleared on Relaxed");
                }
                vpBox = CanvasBox(FindRt("PathViewport"));
                Assert.IsTrue(vpBox.Contains(CanvasBox(n5).center), "Hard: the path is scrolled to its current level 5");
                Assert.AreEqual(1f, FindRt("PathContent").GetComponent<CanvasGroup>().alpha, 0.001f, "the path stays visible while it is redrawn");

                // ---- level popup follows the setting ----
                meta.OnLevelTapped(levels[1]);   // level 2: 1 star on Hard, 3 on Relaxed
                yield return null;
                RectTransform panel = FindRt("Panel");
                Assert.IsNotNull(panel, "level popup on Hard");
                if (starArt)
                {
                    int filled = 0;
                    for (int s = 0; s < 3; s++) if (panel.Find("Star" + s).GetComponent<Image>().color.a > 0.9f) filled++;
                    Assert.AreEqual(1, filled, "the popup shows the stars of the selected setting (Hard: 1)");
                }
                Assert.AreEqual("100%", FindRt(CrownInfoUi.SecondValueName, panel).GetComponent<Text>().text, "Hard has 1 star: a clear still pays in full");
                meta.OnLevelTapped(levels[6]);   // level 7 is locked on Hard
                yield return null;
                Assert.AreEqual("Clear level 6 on Hard first.", FindRt("Body").GetComponent<Text>().text, "the lock message names the setting");

                // ---- back to Relaxed ----
                meta.ShowLevels(r1);
                yield return null;
                yield return null;
                FindRt("GameplayRelaxed").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.AreEqual("6/" + total, PillValue("LevelsPill"), "back on Relaxed");
                Assert.AreEqual("15/" + total * 3, PillValue("RegionStarsPill"), "back on Relaxed");
                Assert.IsNotNull(FindRt("Level7").Find("Num"), "Relaxed again: level 7 is open");
                meta.OnLevelTapped(levels[1]);   // level 2 has 3 stars on Relaxed: a replay pays 25 percent
                yield return null;
                Assert.AreEqual(CoinPayoutRules.ReplayPercent + "%", FindRt(CrownInfoUi.SecondValueName, FindRt("Panel")).GetComponent<Text>().text, "3 stars in THIS setting: the replay percent");

                // ---- region lock popup: all levels on ONE setting ----
                meta.ShowMap();
                yield return null;
                yield return null;
                meta.OnRegionTapped(r2);
                yield return null;
                StringAssert.StartsWith("Clear all " + total + " levels of " + r1.Name + " on one gameplay setting to open " + r2.Name + ".", FindRt("Body").GetComponent<Text>().text);
            }
            finally
            {
                GameSettings.Difficulty = 0;
                GameSession.ApplyDifficulty();
                Restore(holder);
            }
        }

        /// <summary>Rewards: a region name never breaks inside a word, and the list ends above the tab row with room to scroll the last card fully into view.</summary>
        [UnityTest]
        public IEnumerator RewardsRegionNamesAndListEndAreSafe()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                meta.ShowRewards(RewardTab.Main);
                yield return ApplyAspect(a, holder);
                yield return null;
                foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                {
                    if (t.name != "Name" || t.transform.parent == null || !t.transform.parent.name.StartsWith("Slot") || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                    if (t.horizontalOverflow != HorizontalWrapMode.Overflow || t.text.IndexOf(' ') >= 0)
                        problems.Add(a.Name + ": region name \"" + t.text + "\" may wrap inside a word");
                }
                RectTransform tabs = FindRt("Tabs");
                foreach (ScrollRect sr in Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
                {
                    if (sr.viewport == null || tabs == null || !sr.gameObject.activeInHierarchy) continue;
                    if (CanvasBox(sr.viewport).yMin < CanvasBox(tabs).yMax - 0.5f) problems.Add(a.Name + ": the rewards list runs under the tab row");
                    var vl = sr.content.GetComponent<VerticalLayoutGroup>();
                    if (vl != null && vl.padding.bottom < 48) problems.Add(a.Name + ": rewards list has no end padding for the last card");
                }
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>Shop lists: the scroll content is longer than the cards by the end padding, so the last card can be pulled fully above the tab bar.</summary>
        [Test]
        public void ShopListsHaveEndPadding()
        {
            Assert.GreaterOrEqual(ShopChrome.ScrollEndPad, 24f);
        }

        /// <summary>The run draws the character from its parts: every part picture sits on the same canvas bounds.</summary>
        static void AssertPartsFitPlayer(RunView view, List<string> problems)
        {
            SpriteRenderer[] parts = view.PartRenderers;
            if (parts.Length < 6) problems.Add("run: only " + parts.Length + " part pictures were built");
            if (parts.Length == 0) return;
            Bounds a = parts[0].bounds;
            foreach (SpriteRenderer w in parts)
            {
                Bounds b = w.bounds;
                if (Vector3.Distance(a.size, b.size) > 0.02f * a.size.magnitude || Vector3.Distance(a.center, b.center) > 0.02f * a.size.magnitude)
                    problems.Add("run: " + w.name + " bounds " + b + " differ from the character canvas " + a);
            }
        }

        /// <summary>CUX-9 addendum (owner, 2026-10-23): no platform image is stretched. Every platform sprite of a region that has painted width pieces is drawn
        /// Simple with the same X and Y scale (one uniform scale); a stretched slice or a non-uniform scale fails the test.</summary>
        [UnityTest]
        [Category("CriticalUX")]
        public IEnumerator NoPlatformImageIsStretched()
        {
            var problems = new List<string>();
            int checkedPlatforms = 0;
            foreach (string id in new[] { "lvl_001_01", "lvl_001_05", "lvl_001_10", "lvl_002_03", "lvl_002_09", "lvl_003_04", "lvl_003_12", "lvl_004_06", "lvl_004_14", "lvl_005_02", "lvl_005_11" })
            {
                GameSession.CurrentLevelId = id;
                yield return Load("Run");
                var rc = Object.FindFirstObjectByType<RunController>();
                Assert.IsNotNull(rc, id);
                yield return null;
                foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    if (!sr.gameObject.name.StartsWith("plat") || !sr.gameObject.activeInHierarchy) continue;
                    checkedPlatforms++;
                    Vector3 ls = sr.transform.lossyScale;
                    if (Mathf.Abs(ls.x - ls.y) > 0.001f * Mathf.Max(ls.x, ls.y)) problems.Add(id + " " + sr.name + " non-uniform scale " + ls);
                    if (sr.drawMode != SpriteDrawMode.Simple) problems.Add(id + " " + sr.name + " draw mode " + sr.drawMode + " (stretches a slice)");
                }
            }
            Assert.Greater(checkedPlatforms, 60, "platforms were inspected");
            Report(problems);
        }

        // ---- emulator report 2 (2026-11-02), CUX-3 in the run: the HUD and the pads are in reserved strips, the camera shows the world between them ----

        /// <summary>The strips leave a play field of at least MinPlayUnits (the player can be 10 units above the danger line) on every aspect, and are consistent between HUD (ref px) and camera (world).</summary>
        [Test]
        public void RunStripsLeavePlayFieldOnEveryAspect()
        {
            UiKit.SafeAreaNormalized = () => FullSafe;
            foreach (float aspect in new[] { 9f / 16f, 9f / 19.5f, 9f / 21f, 3f / 4f })
            {
                float top, bottom;
                RunLayout.StripsWorld(aspect, 10f, out top, out bottom);
                Assert.Greater(top, 0f, "top strip at " + aspect);
                Assert.AreEqual(0f, bottom, 0.0001f, "no bottom reserve: the screen bottom is the danger edge (owner 2026-11-02) at " + aspect);
                Assert.GreaterOrEqual(10f / aspect - top - bottom, RunLayout.MinPlayUnits - 0.01f, "play height at " + aspect);
                float tr, br;
                RunLayout.StripsRef(aspect, 10f, out tr, out br);
                Assert.AreEqual(0f, br, 0.0001f, "HUD ref bottom reserve is 0 at " + aspect);
                Assert.AreEqual(top, tr * 10f / RunLayout.CanvasW(aspect), 0.001f, "HUD and camera top reserve agree at " + aspect);
                if (aspect < 0.6f)
                {
                    Assert.GreaterOrEqual(tr, 16f + RunLayout.RowH + 12f + RunLayout.Row2H - 0.1f, "top strip holds both HUD rows at " + aspect);
                }
            }
        }

        /// <summary>Real run scene: every top HUD element lies inside the top strip, every pad inside the bottom strip, and the camera puts the danger line on the strip edge (no platform under a pill or a pad).</summary>
        [UnityTest]
        public IEnumerator RunHudAndPadsStayInTheirStripsAndCameraAgrees()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                if (a.Name == "3x4") continue;   // wide tablets: the strips shrink to keep a play field (see RunLayout.MinPlayUnits), documented in docs/11
                UiKit.SafeAreaNormalized = () => FullSafe;
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform topStrip = FindRt("TopStrip");
                if (topStrip == null) { problems.Add(a.Name + ": top strip missing"); continue; }
                if (FindRt("BottomStrip") != null) problems.Add(a.Name + ": a bottom strip exists (owner 2026-11-02: none)");
                if (FindRt("DangerLine") != null) problems.Add(a.Name + ": a red danger line exists (owner 2026-11-02: hidden)");
                Rect tb = CanvasBox(topStrip);
                foreach (string n in new[] { "FloorPill", "GoalPill", "GemPill", "Gauge", "Pause", "Star0", "Star1", "Star2" })
                {
                    RectTransform rt = FindRt(n);
                    if (rt == null) continue;
                    Rect r = CanvasBox(rt);
                    if (r.yMin < tb.yMin - 1.5f) problems.Add(a.Name + ": " + n + " reaches into the play field (" + r.yMin.ToString("F0") + " < " + tb.yMin.ToString("F0") + ")");
                }
                foreach (string n in new[] { "PadLeft", "PadRight", "PadJump" })
                    if (FindRt(n) == null) problems.Add(a.Name + ": " + n + " missing");   // pads float over the field; their overlap rules are in the audits
                // the kill line of the simulation is the physical bottom edge of the screen
                Camera cam = Camera.main;
                float lineY = cam.WorldToScreenPoint(new Vector3(0f, rc.Session.State.KillY.F(), 0f)).y;
                if (Mathf.Abs(lineY) > 3f) problems.Add(a.Name + ": kill line at screen y " + lineY.ToString("F1") + ", expected the screen bottom (0)");
            }
            Restore(holder);
            Report(problems);
        }

        /// <summary>CUX-3 (owner 2026-11-02): the combo and speed-up banners are in the CENTRE of the screen (horizontally centred, around the vertical middle), below the top HUD strip and above the pads, never at the bottom.</summary>
        [UnityTest]
        public IEnumerator CentreBannersStayInTheScreenCentre()
        {
            GameSession.CurrentLevelId = "lvl_001_01";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                UiKit.SafeAreaNormalized = () => FullSafe;
                yield return ApplyAspect(a, holder);
                yield return null;
                RectTransform top = FindRt("TopStrip");
                foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (rt.name != "ComboBanner" && rt.name != "SpeedBanner") continue;
                    bool was = rt.gameObject.activeSelf;
                    rt.gameObject.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    Rect r = CanvasBox(rt);
                    Rect screen = CanvasBox(rt.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>());
                    if (Mathf.Abs(r.center.x - screen.center.x) > 2f) problems.Add(a.Name + ": " + rt.name + " not horizontally centred");
                    if (r.center.y < screen.yMin + screen.height * 0.3f || r.center.y > screen.yMin + screen.height * 0.7f) problems.Add(a.Name + ": " + rt.name + " is not in the vertical centre (y " + r.center.y.ToString("F0") + ")");
                    if (top != null && r.yMax > CanvasBox(top).yMin + 1.5f) problems.Add(a.Name + ": " + rt.name + " overlaps the top HUD strip");
                    rt.gameObject.SetActive(was);
                }
            }
            Restore(holder);
            Report(problems);
        }

        [UnityTest]
        public IEnumerator RunScreensHaveNoOverlapAndFitEveryAspect() { return RunAudit(100, false); }

        [UnityTest]
        public IEnumerator RunScreensSurvive130PercentTextAndLeftHandedLayout() { return RunAudit(130, true); }

        IEnumerator RunAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;   // the second scenario also exercises reduced motion
            for (int i = 1; i <= 2; i++)
                GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10 });
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            var problems = new List<string>();
            AssertPartsFitPlayer(rc.View, problems);
            yield return CheckScreen("run-intro", () => { }, problems);
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 120; frame++) yield return null;
            yield return CheckScreen("run-hud", () => { }, problems);
            yield return CheckScreen("run-pause", () => rc.Hud.ShowPause(), problems);
            rc.Hud.CloseOverlay();
            yield return CheckScreen("run-failed", () => rc.Hud.ShowFailed(rc.Session, 10, true), problems);
            rc.Hud.CloseOverlay();
            yield return CheckScreen("run-failed-ad-dimmed", () => rc.Hud.ShowFailed(rc.Session, 10, false), problems);   // Watch Ad dimmed, never hidden
            rc.Hud.CloseOverlay();
            yield return CheckScreen("run-failed-ad-not-ready-message", () => { rc.Hud.ShowFailed(rc.Session, 0, false); rc.Hud.ShowAdNotReady(); }, problems);   // message visible: no overlap
            rc.Hud.CloseOverlay();
            var outcome = rc.Session.BuildOutcome();
            var reward = GameSession.Progress.ApplyRun(GameSession.CurrentLevelId, outcome);
            yield return CheckScreen("run-results", () => rc.Hud.ShowResults(rc.Session, reward, true), problems);
            rc.Hud.CloseOverlay();
            yield return CheckScreen("run-results-double-coins", () => rc.Hud.ShowResults(rc.Session, reward, true, true), problems);   // "Double the coins" (owner 2026-11-17): the x2 ad button row
            rc.Hud.CloseOverlay();
            yield return CheckScreen("run-results-double-coins-done", () => { rc.Hud.ShowResults(rc.Session, reward, true, true); rc.Hud.MarkDoubleCoinsDone(Mathf.Max(1, reward.PayoutCoins)); }, problems);   // done state: extra coins shown, disabled
            rc.Hud.CloseOverlay();

            // Endless mode (owner decision 2026-10-20): its HUD (Floor / Best) and the game over overlay obey the same rules.
            rc.StartEndlessRun(GameSession.Repo.GetRegionByIndex(1).Id);
            rc.Hud.SkipIntro();
            yield return CheckScreen("run-endless-hud", () => { }, problems);
            var endlessReward = new EndlessReward { Floors = 142, BestBefore = 120, NewBest = true, FullRateCoins = 121, Coins = 61, OverCap = true, RunsToday = 4 };
            yield return CheckScreen("run-endless-failed", () => rc.Hud.ShowEndlessFailed(rc.Session, endlessReward), problems);
            Report(problems);
        }

        // ---- timed gameplay (doc 46): level popup with medals and records, the HUD timer row with delta and Pace Ghost, result screens, Time Attack ----

        [UnityTest]
        public IEnumerator TimedGameplayScreensFitEveryAspect() { return TimedAudit(100, false); }

        [UnityTest]
        public IEnumerator TimedGameplayScreensSurvive130PercentTextAndLeftHandedLayout() { return TimedAudit(130, true); }

        /// <summary>
        /// Bug List 8: a NEW BEST ribbon must be a wide banner (at least 600 x 130 ref px, stretched over its whole rect, not aspect-locked into a small graphic) and its image rect must fully contain
        /// the label text rect with a margin on every side; the label is one line and may shrink to 20 px at the least.
        /// </summary>
        static void AssertRibbonCoversLabel(string badgeName)
        {
            GameObject badge = GameObject.Find(badgeName);
            Assert.IsNotNull(badge, badgeName);
            var img = badge.GetComponent<UnityEngine.UI.Image>();
            Assert.IsNotNull(img, badgeName + " has the ribbon image");
            Assert.IsFalse(img.preserveAspect, badgeName + ": the ribbon graphic fills its rect (no aspect lock shrinking it below the label)");
            var rt = (RectTransform)badge.transform;
            Assert.GreaterOrEqual(rt.rect.width, 600f, badgeName + " ribbon width (ref px)");
            Assert.GreaterOrEqual(rt.rect.height, 130f, badgeName + " ribbon height (ref px)");
            var label = badge.GetComponentInChildren<UnityEngine.UI.Text>();
            Assert.IsNotNull(label, badgeName + " label");
            Assert.IsTrue(label.resizeTextForBestFit && label.resizeTextMinSize <= 20, badgeName + " label shrinks to fit down to 20 (one line)");
            var lrt = (RectTransform)label.transform;
            var rc = new Vector3[4];
            var lc = new Vector3[4];
            rt.GetWorldCorners(rc);
            lrt.GetWorldCorners(lc);
            float mx = 0.04f * Mathf.Abs(rc[2].x - rc[0].x);   // at least 4 percent of the ribbon width left and right
            float my = 0.08f * Mathf.Abs(rc[2].y - rc[0].y);   // and 8 percent of its height above and below
            Assert.Greater(lc[0].x - rc[0].x, mx, badgeName + " left margin");
            Assert.Greater(rc[2].x - lc[2].x, mx, badgeName + " right margin");
            Assert.Greater(lc[0].y - rc[0].y, my, badgeName + " bottom margin");
            Assert.Greater(rc[2].y - lc[2].y, my, badgeName + " top margin");
        }

        static bool IsCentred(TextAnchor a) { return a == TextAnchor.UpperCenter || a == TextAnchor.MiddleCenter || a == TextAnchor.LowerCenter; }

        /// <summary>
        /// Bug List 8 items 2b and 5: the level popup and the endless popup are centred. The title sits on the middle of the panel, every text in them is centre aligned, the medal strip
        /// has its caption, the words Bronze / Silver / Gold under the icons and a "plat/s" target under each word, and START and Time Attack are two green buttons on one row.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelAndEndlessPopupsAreCentredWithMedalWords()
        {
            IMedalTargetSource savedTargets = MedalTargets.Source;
            var targets = new DictionaryMedalTargets();
            targets.Set("lvl_001_01", 0, 900);
            MedalTargets.Source = targets;
            var problems = new List<string>();
            try
            {
                foreach (int percent in new[] { 100, 130 })
                {
                    GameSettings.TextPercent = percent;
                    GameSettings.Difficulty = 0;
                    yield return Load("Meta");
                    var meta = Object.FindFirstObjectByType<MetaController>();
                    RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
                    var levels = GameSession.Repo.LevelsOf(r1.Id);
                    var holder = new RenderTexture[1];
                    foreach (Aspect a in Aspects)
                    {
                        UiKit.SafeAreaNormalized = () => FullSafe;
                        for (int popup = 0; popup < 2; popup++)
                        {
                            meta.ShowLevels(r1);
                            if (popup == 0) meta.OnLevelTapped(levels[0]); else meta.OnEndlessTapped(r1);
                            yield return ApplyAspect(a, holder);
                            string tag = a.Name + "@" + percent + (popup == 0 ? " level popup: " : " endless popup: ");
                            RectTransform panel = FindRt("Panel"), title = panel != null ? FindRt("Title", panel) : null;
                            if (panel == null || title == null) { problems.Add(tag + "panel or title missing"); continue; }
                            Rect pb = CanvasBox(panel);
                            if (Mathf.Abs(CanvasBox(title).center.x - pb.center.x) > 3f) problems.Add(tag + "the title is not centred on the panel");
                            foreach (Text t in panel.GetComponentsInChildren<Text>())
                            {
                                if (!t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                                if (!IsCentred(t.alignment)) problems.Add(tag + t.name + " is not centre aligned");
                                Rect tb = CanvasBox(t.rectTransform);
                                if (tb.xMin < pb.xMin - 1f || tb.xMax > pb.xMax + 1f) problems.Add(tag + t.name + " leaves the panel");
                            }
                            if (popup == 0)
                            {
                                RectTransform strip = FindRt(MedalStrip.RowName, panel);
                                if (strip == null) { problems.Add(tag + "medal strip missing"); continue; }
                                RectTransform cap = FindRt(MedalStrip.CaptionName, strip);
                                if (cap == null || cap.GetComponent<Text>().text != MedalStrip.CaptionText) problems.Add(tag + "the medal strip needs its caption");
                                else if (Mathf.Abs(CanvasBox(cap).center.x - pb.center.x) > 3f) problems.Add(tag + "the medal caption is not centred");
                                foreach (string tier in new[] { "Bronze", "Silver", "Gold" })
                                {
                                    RectTransform icon = FindRt(tier + "Medal", strip), word = FindRt(tier + "Name", strip), pace = FindRt(tier + "Caption", strip);
                                    if (icon == null || word == null || pace == null) { problems.Add(tag + tier + " icon, word or pace missing"); continue; }
                                    if (word.GetComponent<Text>().text != tier) problems.Add(tag + tier + " word is wrong");
                                    if (!pace.GetComponent<Text>().text.EndsWith("plat/s")) problems.Add(tag + tier + " target must read like 0.90 plat/s");
                                    Rect ib = CanvasBox(icon), wb = CanvasBox(word), cb = CanvasBox(pace);
                                    if (Mathf.Abs(wb.center.x - ib.center.x) > 3f || Mathf.Abs(cb.center.x - ib.center.x) > 3f) problems.Add(tag + tier + " word and target are not centred under the icon");
                                    if (wb.yMax > ib.yMin + 1f || cb.yMax > wb.yMin + 1f) problems.Add(tag + tier + " icon, word and target overlap or are out of order");
                                }
                            }
                            else
                            {
                                RectTransform start = FindRt("START", panel), ta = FindRt(MetaController.TimeAttackButtonName, panel);
                                if (start == null || ta == null) { problems.Add(tag + "START or Time Attack button missing"); continue; }
                                Rect sb = CanvasBox(start), tb = CanvasBox(ta);
                                if (Mathf.Abs(sb.center.y - tb.center.y) > 2f) problems.Add(tag + "START and Time Attack must be on the same row");
                                if (sb.Overlaps(tb)) problems.Add(tag + "START and Time Attack overlap");
                                if (sb.width < UiKit.MinTouchPx - 1f || tb.width < UiKit.MinTouchPx - 1f) problems.Add(tag + "action buttons below 48 dp");
                                if (start.GetComponent<Image>().sprite != ta.GetComponent<Image>().sprite) problems.Add(tag + "Time Attack must use the same green plate as START");
                                bool stopwatch = false;
                                foreach (Image im in ta.GetComponentsInChildren<Image>()) if (im.sprite == TimedIcons.Stopwatch) stopwatch = true;
                                if (!stopwatch) problems.Add(tag + "Time Attack must carry the stopwatch icon");
                                if (FindRt(MetaController.TimeAttackCaptionName, panel) == null || FindRt(MetaController.EndlessCaptionName, panel) == null) problems.Add(tag + "captions under the two buttons missing");
                            }
                        }
                    }
                    Restore(holder);
                }
            }
            finally { MedalTargets.Source = savedTargets; GameSettings.TextPercent = 100; }
            Report(problems);
        }

        /// <summary>Bug List 8 item 2c: the intro popup of Time Attack / Endless is centred vertically (and horizontally) in the safe area at every aspect.</summary>
        [UnityTest]
        public IEnumerator RunIntroPopupIsCentredInSafeArea()
        {
            var problems = new List<string>();
            foreach (bool timeAttack in new[] { true, false })
            {
                yield return Load("Meta");
                RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
                if (timeAttack) GameSession.SelectTimeAttack(r1.Id); else GameSession.SelectEndless(r1.Id);
                yield return Load("Run");
                var holder = new RenderTexture[1];
                foreach (Aspect a in Aspects)
                {
                    UiKit.SafeAreaNormalized = () => FullSafe;
                    yield return ApplyAspect(a, holder);
                    string tag = a.Name + (timeAttack ? " time attack: " : " endless: ");
                    RectTransform intro = FindRt("Intro");
                    if (intro == null) { problems.Add(tag + "intro popup missing"); continue; }
                    Rect ib = CanvasBox(intro), sb = CanvasBox((RectTransform)intro.parent);
                    if (Mathf.Abs(ib.center.y - sb.center.y) > 3f) problems.Add(tag + "the intro popup is not centred vertically in the safe area");
                    if (Mathf.Abs(ib.center.x - sb.center.x) > 3f) problems.Add(tag + "the intro popup is not centred horizontally");
                    if (ib.yMin < sb.yMin || ib.yMax > sb.yMax || ib.xMin < sb.xMin || ib.xMax > sb.xMax) problems.Add(tag + "the intro popup leaves the safe area");
                }
                Restore(holder);
            }
            Report(problems);
        }

        static RunRecord TimedRun(string levelId, string regionId, int floors, int ticks, bool revived = false)
        {
            return new RunRecord { Kind = RunKind.Level, LevelId = levelId, RegionId = regionId, Floors = floors, Ticks = ticks, Mode = 0, HeroId = "momo", GameVersion = "test", Revived = revived, Curve = new[] { 4, 9, 14, 19, 24, 29 } };
        }

        IEnumerator TimedAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.PaceGhost = true;
            IMedalTargetSource savedTargets = MedalTargets.Source;
            var targets = new DictionaryMedalTargets();
            targets.Set("lvl_001_03", 0, 900);
            MedalTargets.Source = targets;
            var problems = new List<string>();
            try
            {
                RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
                for (int i = 1; i <= 2; i++)
                    GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10 });
                GameSession.Progress.RecordRun(TimedRun("lvl_001_03", r1.Id, 30, 1800));   // 1.00 plat/s: the best record the screens compare with
                GameSession.Progress.RecordRun(new RunRecord { Kind = RunKind.Endless, RegionId = r1.Id, Floors = 142, Ticks = 4200, Mode = 0, HeroId = "momo", GameVersion = "test", Curve = new[] { 3, 6, 9, 12 }, Splits = new[] { 2400 } });

                yield return Load("Meta");
                var meta = Object.FindFirstObjectByType<MetaController>();
                var levels = GameSession.Repo.LevelsOf(r1.Id);
                yield return CheckScreen("level-popup-timed", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[2]); }, problems);
                yield return CheckScreen("level-popup-no-record", () => { meta.ShowLevels(r1); meta.OnLevelTapped(levels[1]); }, problems);
                yield return CheckScreen("endless-popup-timed", () => { meta.ShowLevels(r1); meta.OnEndlessTapped(r1); }, problems);
                // audit gap 2: the Time Attack line carries the best pace (and the time of a run that ended early) as a second line; it must not overlap at any aspect or at 130 percent
                GameSession.Progress.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 96, Ticks = 9100, Mode = 0, HeroId = "momo", GameVersion = "test", Curve = new[] { 3, 6, 9 } });
                Assert.IsNotNull(GameSession.Progress.TimeAttackBest(r1.Id, 0), "a Time Attack record exists for the popup");
                yield return CheckScreen("endless-popup-timeattack-pace", () => { meta.ShowLevels(r1); meta.OnEndlessTapped(r1); }, problems);
                Assert.IsNotNull(GameObject.Find("TimeAttackBest"), "the Time Attack record line is shown");

                GameSession.CurrentLevelId = "lvl_001_03";
                yield return Load("Run");
                var rc = Object.FindFirstObjectByType<RunController>();
                Assert.IsTrue(rc.Hud.GhostActive, "the ghost is on (Settings) and a best curve exists");
                rc.Hud.SkipIntro();
                int frame = 0;
                rc.Input.ScriptedMove = () => SimInput.Right;
                rc.Input.ScriptedJump = () => frame % 45 == 0;
                for (frame = 1; frame <= 150; frame++) yield return null;
                yield return CheckScreen("run-hud-timed", () => { }, problems);
                var reward = GameSession.Progress.ApplyRun(GameSession.CurrentLevelId, rc.Session.BuildOutcome());
                RecordResult better = GameSession.Progress.RecordRun(TimedRun("lvl_001_03", r1.Id, 30, 1500));
                GameSession.LastResult = new RunResult { LevelId = "lvl_001_03", Completed = true, Floors = 30, Ticks = 1500, Record = better };
                yield return CheckScreen("run-results-new-best", () => rc.Hud.ShowResults(rc.Session, reward, true), problems);
                if (GameObject.Find("NewBestTimeBadge") != null) AssertRibbonCoversLabel("NewBestTimeBadge");
                if (GameObject.Find("NewBestBadge") != null) AssertRibbonCoversLabel("NewBestBadge");
                rc.Hud.CloseOverlay();
                // audit gap 6: a run with a new best Time AND a new best Pace shows two separate ribbons (NEW BEST TIME and NEW BEST PACE), stacked without overlap
                var both = new RecordResult { Kind = RunKind.Level, Floors = 30, Ticks = 1400, Counted = true, NewBestTime = true, NewBestPace = true, HasPrevTime = true, HasPrevPace = true, DeltaTicks = -100, DeltaPaceHundredths = 7 };
                GameSession.LastResult = new RunResult { LevelId = "lvl_001_03", Completed = true, Floors = 30, Ticks = 1400, Record = both };
                yield return CheckScreen("run-results-new-best-time-and-pace", () => rc.Hud.ShowResults(rc.Session, reward, true), problems);
                Assert.IsNotNull(GameObject.Find("NewBestTimeBadge"), "NEW BEST TIME ribbon");
                Assert.IsNotNull(GameObject.Find("NewBestPaceBadge"), "NEW BEST PACE ribbon");
                Assert.IsNull(GameObject.Find("NewBestBadge"), "no generic ribbon when Time and Pace are named");
                AssertRibbonCoversLabel("NewBestTimeBadge");
                AssertRibbonCoversLabel("NewBestPaceBadge");
                var tr = (RectTransform)GameObject.Find("NewBestTimeBadge").transform;
                var pr = (RectTransform)GameObject.Find("NewBestPaceBadge").transform;
                Assert.GreaterOrEqual(tr.position.y - pr.position.y, tr.rect.height * tr.lossyScale.y - 0.5f, "stacked ribbons do not overlap");
                rc.Hud.CloseOverlay();
                RecordResult worse = GameSession.Progress.RecordRun(TimedRun("lvl_001_03", r1.Id, 30, 2100));
                GameSession.LastResult = new RunResult { LevelId = "lvl_001_03", Completed = true, Floors = 30, Ticks = 2100, Record = worse };
                yield return CheckScreen("run-results-slower", () => rc.Hud.ShowResults(rc.Session, reward, true), problems);
                rc.Hud.CloseOverlay();
                RecordResult revived = GameSession.Progress.RecordRun(TimedRun("lvl_001_03", r1.Id, 30, 1000, true));
                GameSession.LastResult = new RunResult { LevelId = "lvl_001_03", Completed = true, Floors = 30, Ticks = 1000, Revived = true, Record = revived };
                yield return CheckScreen("run-results-revived", () => rc.Hud.ShowResults(rc.Session, reward, true), problems);
                rc.Hud.CloseOverlay();

                // endless and Time Attack: the HUD (ghost pill, countdown) and the end screen
                rc.StartTimeAttackRun(r1.Id);
                rc.Hud.SkipIntro();
                yield return CheckScreen("run-timeattack-hud", () => { }, problems);
                var er = new EndlessReward { Floors = 142, BestBefore = 120, NewBest = true, FullRateCoins = 121, Coins = 61, RunsToday = 1 };
                GameSession.LastResult = new RunResult { Endless = true, TimeAttack = true, RegionId = r1.Id, Floors = 142, Ticks = 10800, Record = new RecordResult { Kind = RunKind.TimeAttack, Floors = 142, Ticks = 10800, Counted = true, NewBestFloors = true } };
                yield return CheckScreen("run-timeattack-end", () => rc.Hud.ShowEndlessFailed(rc.Session, er), problems);
                AssertRibbonCoversLabel("NewBestBadge");   // (NewBestFloors only, so the generic ribbon) the endless / Time Attack end screens use the same ribbon sizes
                rc.Hud.CloseOverlay();
                rc.StartEndlessRun(r1.Id);
                rc.Hud.SkipIntro();
                yield return CheckScreen("run-endless-hud-timed", () => { }, problems);
                var rec2 = GameSession.Progress.RecordRun(new RunRecord { Kind = RunKind.Endless, RegionId = r1.Id, Floors = 160, Ticks = 4000, Mode = 0, HeroId = "momo", GameVersion = "test", Curve = new[] { 3, 6, 9 } });
                GameSession.LastResult = new RunResult { Endless = true, RegionId = r1.Id, Floors = 160, Ticks = 4000, Record = rec2 };
                yield return CheckScreen("run-endless-end-timed", () => rc.Hud.ShowEndlessFailed(rc.Session, er), problems);
            }
            finally
            {
                MedalTargets.Source = savedTargets;
            }
            Report(problems);
        }

        /// <summary>
        /// Audit gap 5 (doc 46): the medal badge on a level node is the medal of the CURRENT Gameplay mode, not the best over all five, and it follows a mode change in place
        /// (a tap on a setting of the level screen, no new screen). Since Final Change 1 every setting has its own path, so levels 1 and 2 are cleared on Relaxed AND on Hard:
        /// level 3 is open in both, the medal run exists on Relaxed only.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelNodeMedalFollowsTheGameplayMode()
        {
            IMedalTargetSource savedTargets = MedalTargets.Source;
            var targets = new DictionaryMedalTargets();
            targets.Set("lvl_001_03", 0, 900);
            targets.Set("lvl_001_03", 3, 900);
            MedalTargets.Source = targets;
            try
            {
                RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
                foreach (int mode in new[] { 0, 3 })
                    for (int i = 1; i <= 2; i++)
                        GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10, Mode = mode });
                GameSession.Progress.RecordRun(TimedRun("lvl_001_03", r1.Id, 30, 1800));   // Relaxed (mode 0) only: 1.00 plat/s against a 0.90 target
                Assert.AreNotEqual(MedalLevel.None, GameSession.Progress.Medal("lvl_001_03", 0), "the Relaxed run earned a medal");
                Assert.AreEqual(MedalLevel.None, GameSession.Progress.Medal("lvl_001_03", 3), "nothing in Hard yet");
                GameSettings.Difficulty = 0;
                yield return Load("Meta");
                var meta = Object.FindFirstObjectByType<MetaController>();
                meta.ShowLevels(r1);
                yield return null;
                yield return null;
                Transform node = FindRt("Level3");
                Assert.IsNotNull(node, "level 3 node");
                Assert.IsNotNull(node.Find("Num"), "level 3 is open on Relaxed");
                Assert.IsNotNull(node.Find(MetaController.MedalBadgeName), "Relaxed shows the Relaxed medal");
                FindRt("GameplayHard").GetComponent<Button>().onClick.Invoke();   // in place: the level screen redraws its nodes for Hard
                yield return null;
                Assert.AreEqual(3, GameSettings.Difficulty, "the tap selected Hard");
                node = FindRt("Level3");
                Assert.IsNotNull(node, "level 3 node after the change");
                Assert.IsNotNull(node.Find("Num"), "level 3 is open on Hard too (levels 1 and 2 are cleared on Hard)");
                Assert.IsNull(node.Find(MetaController.MedalBadgeName), "Hard has no medal: the best over all modes is NOT shown");
                FindRt("GameplayRelaxed").GetComponent<Button>().onClick.Invoke();
                yield return null;
                node = FindRt("Level3");
                Assert.IsNotNull(node.Find(MetaController.MedalBadgeName), "back on Relaxed the Relaxed medal shows again");
            }
            finally
            {
                MedalTargets.Source = savedTargets;
                GameSettings.Difficulty = 0;
                GameSession.ApplyDifficulty();
            }
        }

        /// <summary>The Pace Ghost is OFF by default; Settings carries its switch without a scroll view.</summary>
        [UnityTest]
        public IEnumerator PaceGhostIsOffByDefaultAndTheSettingsSwitchWorks()
        {
            Assert.IsFalse(GameSettings.PaceGhost, "default OFF (owner 2026-11-09)");
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            meta.ShowSettings();
            yield return null;
            RectTransform toggle = FindRt("Pace ghostToggle");
            Assert.IsNotNull(toggle, "the Settings screen has the Pace ghost switch");
            Assert.IsNull(FindRt("Content").GetComponentInParent<ScrollRect>(), "Settings stays non-scrolling");
            toggle.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(GameSettings.PaceGhost, "the switch turns it on");
            toggle.GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(GameSettings.PaceGhost, "and off again");
        }

        // ---- power-ups (docs/15): the in-run buttons on every aspect, at 130 percent text, right- and left-handed ----

        static void GrantAllPowerUps(int each)
        {
            foreach (PowerUpDef d in GameSession.Progress.PowerUpDefs.Implemented) GameSession.Progress.GrantPowerUp(d.Id, each);
        }

        [UnityTest]
        public IEnumerator PowerUpButtonsFitEveryAspect() { return PowerUpRunAudit(100, false); }

        [UnityTest]
        public IEnumerator PowerUpButtonsSurvive130PercentTextAndLeftHandedLayout() { return PowerUpRunAudit(130, true); }

        IEnumerator PowerUpRunAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;
            for (int i = 1; i <= 2; i++)
                GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10 });
            GrantAllPowerUps(3);
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            var problems = new List<string>();
            Assert.AreEqual(9, rc.Hud.Powers.ButtonCount, "one button per owned type, no loadout limit");
            yield return CheckScreen("run-powerups-intro", () => { }, problems);   // the buttons wait until the intro text is gone
            Assert.IsFalse(rc.Hud.Powers.Visible, "hidden during the intro");
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 60; frame++) yield return null;
            Assert.IsTrue(rc.Hud.Powers.Visible);

            // thumb side: the buttons sit on the jump side, are well over 48 dp, keep at least 8 dp between ALL pairs and never touch the pad
            var rects = new List<Rect>();
            for (int i = 0; i < 7; i++)
            {
                RectTransform b = rc.Hud.Powers.ButtonRect(i);
                Assert.IsNotNull(b, "button " + i);
                Assert.GreaterOrEqual(b.rect.width, UiKit.MinTouchPx - 1f);
                Assert.GreaterOrEqual(b.rect.height, UiKit.MinTouchPx - 1f);
                Assert.IsNotNull(b.GetComponent<A11yLabel>());
                StringAssert.Contains("left", b.GetComponent<A11yLabel>().Text, "spoken label names the units left");
                rects.Add(LayoutRect(b));
            }
            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.GreaterOrEqual(RectDistance(rects[i], rects[j]), UiKit.MinSpacingPx, "power-up buttons " + i + " and " + j + " are closer than 8 dp");
                foreach (HoldButton pad in new[] { rc.Hud.Pad.Left, rc.Hud.Pad.Right, rc.Hud.Pad.Jump })
                    Assert.GreaterOrEqual(RectDistance(rects[i], LayoutRect((RectTransform)pad.transform)), UiKit.MinSpacingPx, "power-up button " + i + " is too close to the pad");
            }
            yield return CheckScreen("run-powerups-ready", () => { }, problems);

            // activate every type through the input path: rings drain, counts stay, seconds show, nothing overlaps; Rocket is refused while Flight runs
            foreach (PowerUp t in PowerUps.Implemented)
                rc.Input.LatchPower(t);
            for (frame = 61; frame <= 90; frame++) yield return null;
            Assert.AreEqual(8, rc.Session.State.PwUsed, "eight buttons reached the simulation, the Rocket waits for the Flight");
            yield return CheckScreen("run-powerups-active", () => { }, problems);
            // stacking: another tap on the Glide Cape adds the full 20 s onto the remaining time
            int glideBefore = rc.Session.State.CapeTicks;
            rc.Input.LatchPower(PowerUp.Glide);
            for (frame = 91; frame <= 100; frame++) yield return null;
            Assert.Greater(rc.Session.State.CapeTicks, glideBefore + 1000, "a second tap adds the base time");
            yield return CheckScreen("run-powerups-stacked", () => { }, problems);
            rc.Session.SettlePowerUps(GameSession.Progress);
            Assert.AreEqual(2, GameSession.Progress.PowerUpCount("pw_flight"), "a used unit is gone after the run settles");
            Assert.AreEqual(1, GameSession.Progress.PowerUpCount("pw_glide"), "two taps used two units");
            Assert.AreEqual(3, GameSession.Progress.PowerUpCount("pw_rocket"), "a refused tap costs nothing");
            Report(problems);
        }

        // ---- hero powers (owner Bug List 6, 2026-11-09): NOT a separate bar any more. A free hero power is merged into the power button of its own type, marked FREE;
        // the buttons sit in two side groups by type: left = Flight, Rocket, Double Jump (max 3), right = Magnet, Shield, Slow Time, Wall Stick (max 4). ----

        [UnityTest]
        public IEnumerator HeroPowerButtonsFitEveryAspectNextToSevenBoughtPowerUps() { return HeroPowerRunAudit(100, false); }

        [UnityTest]
        public IEnumerator HeroPowerButtonsSurvive130PercentTextAndLeftHandedLayout() { return HeroPowerRunAudit(130, true); }

        /// <summary>Hero with powers and NO bought power-up: the free powers alone make the buttons, in their type groups, marked FREE.</summary>
        [UnityTest]
        public IEnumerator FreeHeroPowersAloneFormTheTypeGroups() { return HeroOnlyAudit(100, false); }

        [UnityTest]
        public IEnumerator FreeHeroPowersAloneSurvive130PercentTextAndLeftHandedLayout() { return HeroOnlyAudit(130, true); }

        /// <summary>A Rare or Elite hero has ONE free power: one button on its own, still laid out cleanly.</summary>
        [UnityTest]
        public IEnumerator ASingleFreeHeroPowerFormsOneButton() { return HeroOnlyAudit(100, false, RareTestHero()); }

        [UnityTest]
        public IEnumerator ASingleFreeHeroPowerSurvives130PercentTextAndLeftHandedLayout() { return HeroOnlyAudit(130, true, RareTestHero()); }

        static CharacterSpec LegendaryTestHero()
        {
            // a Legendary hero with the 2 powers its tier rule gives since owner Final Change 2 (default combination: Safety Net + Double Jump; built from the rule, not a row)
            return new CharacterSpec { Id = "test_legendary", Name = "Test Legendary", Rarity = CharacterRarity.Legendary, Powers = TierRules.PowersFor("momo", CharacterRarity.Legendary) };
        }

        static CharacterSpec RareTestHero()
        {
            // a Rare hero with the ONE power of its tier (default combination: Wide Platforms)
            return new CharacterSpec { Id = "test_rare", Name = "Test Rare", Rarity = CharacterRarity.Rare, Powers = TierRules.PowersFor("momo", CharacterRarity.Rare) };
        }

        /// <summary>The merged run buttons: at least 48 dp, labelled, same alpha, at least 8 dp from each other and from the pads, inside the safe area, on the side of their type
        /// group (mirrored by the left-handed option) and clear of the centred banners (CUX-3/4).</summary>
        static void AssertPowerGroupsLayout(RunController rc, bool leftHanded)
        {
            PowerUpBar bar = rc.Hud.Powers;
            Assert.LessOrEqual(bar.LeftCount, PowerGroups.MaxLeft);
            Assert.LessOrEqual(bar.RightCount, PowerGroups.MaxRight);
            Assert.AreEqual(bar.ButtonCount, bar.LeftCount + bar.RightCount);
            var rects = new List<Rect>();
            for (int i = 0; i < bar.ButtonCount; i++)
            {
                RectTransform b = bar.ButtonRect(i);
                Assert.IsNotNull(b, "button " + i);
                Assert.GreaterOrEqual(b.rect.width, UiKit.MinTouchPx - 1f);
                Assert.GreaterOrEqual(b.rect.height, UiKit.MinTouchPx - 1f);
                Assert.IsNotNull(b.GetComponent<A11yLabel>());
                Assert.AreEqual(ControlPad.ButtonAlpha, b.GetComponent<CanvasGroup>().alpha, 0.001f);
                Assert.AreEqual(PowerGroups.IsLeft(bar.ButtonType(i)), bar.ButtonIsLeft(i), "group of " + bar.ButtonType(i));
                Rect r = LayoutRect(b);
                Vector2 parent = ((RectTransform)b.parent).rect.size;
                Assert.GreaterOrEqual(r.xMin, 0f, "button " + i + " inside the safe area (left)");
                Assert.LessOrEqual(r.xMax, parent.x, "button " + i + " inside the safe area (right)");
                Assert.GreaterOrEqual(r.yMin, 0f, "button " + i + " inside the safe area (bottom)");
                Assert.LessOrEqual(r.yMax, parent.y, "button " + i + " inside the safe area (top)");
                bool onLeftEdge = r.center.x < parent.x * 0.5f;
                Assert.AreEqual(bar.ButtonIsLeft(i) != leftHanded, onLeftEdge, "the left group sits on the left edge (right edge when left-handed): " + bar.ButtonType(i));
                float mid = parent.x * 0.5f, halfBanner = RunHud.BannerW * 0.5f + UiKit.MinSpacingPx;
                if (onLeftEdge) Assert.LessOrEqual(r.xMax, mid - halfBanner, "button " + i + " clear of the centred banners");
                else Assert.GreaterOrEqual(r.xMin, mid + halfBanner, "button " + i + " clear of the centred banners");
                rects.Add(r);
            }
            var pads = new List<Rect>();
            foreach (HoldButton pad in new[] { rc.Hud.Pad.Left, rc.Hud.Pad.Right, rc.Hud.Pad.Jump }) pads.Add(LayoutRect((RectTransform)pad.transform));
            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.GreaterOrEqual(RectDistance(rects[i], rects[j]), UiKit.MinSpacingPx, "buttons " + i + " and " + j + " are closer than 8 dp");
                for (int j = 0; j < pads.Count; j++)
                    Assert.GreaterOrEqual(RectDistance(rects[i], pads[j]), UiKit.MinSpacingPx, "button " + i + " is too close to pad " + j);
            }
        }

        IEnumerator HeroPowerRunAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;
            for (int i = 1; i <= 2; i++)
                GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10 });
            GrantAllPowerUps(3);
            RunController.HeroOverride = LegendaryTestHero();
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            var problems = new List<string>();
            PowerUpBar bar = rc.Hud.Powers;
            Assert.AreEqual(9, bar.ButtonCount, "all nine types are bought: one merged button each, no separate hero buttons");
            Assert.AreEqual(4, bar.LeftCount, "left group: Double Jump, Rocket, Flight, Glide Cape");
            Assert.AreEqual(5, bar.RightCount, "right group: Shield, Wall Stick, Slow Time, Wide Platforms, Safety Net");
            Assert.AreEqual(2, rc.Session.Hero.Count, "a Legendary hero has two free hero powers (owner Final Change 2)");
            int free = 0;
            for (int i = 0; i < bar.ButtonCount; i++)
            {
                Assert.IsTrue(bar.ButtonHasBought(i), "bought count shown on " + bar.ButtonType(i));
                Assert.AreEqual(rc.Session.Hero.IndexOf(bar.ButtonType(i)) >= 0, bar.ButtonHasFree(i), "FREE only on the hero's own types");
                if (bar.ButtonHasFree(i)) free++;
            }
            Assert.AreEqual(2, free, "the two hero powers are merged into two of the nine buttons");
            Assert.IsFalse(bar.Visible, "hidden during the intro");
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 60; frame++) yield return null;
            Assert.IsTrue(bar.Visible);
            AssertPowerGroupsLayout(rc, leftHanded);
            for (int i = 0; i < bar.ButtonCount; i++)
            {
                if (bar.ButtonHasFree(i)) Assert.AreEqual("FREE", bar.ButtonTopText(i), "ready free hero power carries the FREE word (not colour only): " + bar.ButtonType(i));
                else Assert.AreEqual("", bar.ButtonTopText(i), "no FREE tag without a hero power: " + bar.ButtonType(i));
                StringAssert.Contains(bar.ButtonHasFree(i) ? "free hero power" : "left", bar.ButtonRect(i).GetComponent<A11yLabel>().Text);
            }
            yield return CheckScreen("run-heropowers", () => { }, problems);   // every aspect (9:16, 9:19.5, 9:21, 3:4), full and notched safe area

            // free first, then bought: the first tap on a type that has a free hero power takes no unit and starts its cooldown; the second tap takes one bought unit
            int idx = -1;
            for (int i = 0; i < bar.ButtonCount; i++) if (bar.ButtonHasFree(i) && bar.ButtonType(i) != PowerUp.SafetyNet) { idx = i; break; }   // a timed power: an armed Safety Net refuses the second tap
            PowerUp first = bar.ButtonType(idx);
            int heroIdx = rc.Session.Hero.IndexOf(first);
            Assert.GreaterOrEqual(heroIdx, 0);
            int stockBefore = rc.Session.PowerStock(first);
            rc.Input.LatchPower(first);
            for (frame = 61; frame <= 75; frame++) yield return null;
            Assert.IsTrue(PowerUps.IsActive(in rc.Session.State, first));
            Assert.AreEqual(stockBefore, rc.Session.PowerStock(first), "the free hero power takes no inventory unit");
            Assert.IsFalse(rc.Session.Hero.FreeAvailable(heroIdx), "its one free use of the run is spent");
            for (int k = 0; k < rc.Session.Hero.Count; k++) if (k != heroIdx) Assert.IsFalse(rc.Session.Hero.FreeAvailable(k), "one free use per run in total: no other hero power is free any more");
            rc.Input.LatchPower(first);
            for (frame = 76; frame <= 90; frame++) yield return null;
            Assert.AreEqual(stockBefore - 1, rc.Session.PowerStock(first), "the second tap uses a bought unit");
            yield return CheckScreen("run-heropowers-cooldown", () => { }, problems);
            Report(problems);
        }

        IEnumerator HeroOnlyAudit(int textPercent, bool leftHanded, CharacterSpec hero = null)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;
            for (int i = 1; i <= 2; i++)
                GameSession.Progress.ApplyRun("lvl_001_0" + i, new RunOutcome { Completed = true, StarMask = 1, Score = 100, CoinsCollected = 10 });
            hero = hero ?? LegendaryTestHero();
            RunController.HeroOverride = hero;
            GameSession.CurrentLevelId = "lvl_001_03";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            var problems = new List<string>();
            PowerUpBar bar = rc.Hud.Powers;
            int want = 0;
            foreach (PowerUp t in PowerUps.Implemented)
                if (rc.Session.Hero.IndexOf(t) >= 0 || rc.Session.OwnedAtStart(t)) want++;
            Assert.GreaterOrEqual(want, hero.Powers.Length, "every hero power has a button");
            Assert.AreEqual(hero.Powers.Length, rc.Session.Hero.Count, "Rare and Elite have one free power, a Legendary two");
            Assert.AreEqual(want, bar.ButtonCount, "one button per type: bought or free");
            for (int i = 0; i < bar.ButtonCount; i++)
                Assert.AreEqual(rc.Session.OwnedAtStart(bar.ButtonType(i)), bar.ButtonHasBought(i), "the bought count only on bought types: " + bar.ButtonType(i));
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 60; frame++) yield return null;
            AssertPowerGroupsLayout(rc, leftHanded);
            yield return CheckScreen("run-heropowers-only", () => { }, problems);
            Report(problems);
        }

        /// <summary>
        /// Layout rectangle in canvas reference pixels from anchor, anchored position, pivot and size of a child of the HUD root, so it does not
        /// depend on the canvas scale or on the aspect (power-up buttons and pad buttons are siblings).
        /// </summary>
        static Rect LayoutRect(RectTransform rt)
        {
            Vector2 size = rt.rect.size;
            Vector2 parent = ((RectTransform)rt.parent).rect.size;
            float x = rt.anchorMin.x * parent.x + rt.anchoredPosition.x - rt.pivot.x * size.x;
            float y = rt.anchorMin.y * parent.y + rt.anchoredPosition.y - rt.pivot.y * size.y;
            return new Rect(x, y, size.x, size.y);
        }

        /// <summary>Empty space between two rectangles (0 when they touch or overlap; the larger axis gap when separated on both).</summary>
        static float RectDistance(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // ---- shop (docs/14): all four tabs, both sections, promo on and off, and the shop popups ----

        [UnityTest]
        public IEnumerator ShopScreenHasNoOverlapAndFitsEveryAspect() { return ShopAudit(100); }

        /// <summary>CUX-5: the shop survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator ShopScreenSurvives130PercentText() { return ShopAudit(130); }

        /// <summary>Bug List 2 C2.a: the first yellow banner scrolls with the list. Every tab, every aspect: at the top the banner is the first item under the Momo art;
        /// scrolled down it has left the viewport, the Momo art is still fully visible (the viewport starts below it) and nothing overlaps.</summary>
        [UnityTest]
        public IEnumerator ShopFirstBannerScrollsAwayAndMomoArtStaysVisible() { return ShopScrollAudit(100); }

        [UnityTest]
        public IEnumerator ShopFirstBannerScrollsAwayAt130PercentText() { return ShopScrollAudit(130); }

        IEnumerator ShopScrollAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            GameSession.Progress.AddGems(300);
            GameSession.Progress.AddCoins(12345);
            int[] tabs = { ShopScreen.TabBundles, ShopScreen.TabVip, ShopScreen.TabCharacter, ShopScreen.TabGems };
            string[] firstBanner = { "Bundles", "Subscription", "Heroes", "Gems" };
            for (int t = 0; t < tabs.Length; t++)
            {
                int tab = tabs[t];
                string banner = "Banner_" + firstBanner[t];
                yield return CheckShopScroll("shop-scroll-top-" + firstBanner[t], () => meta.ShowShop(tab), banner, 0f, problems);
                yield return CheckShopScroll("shop-scroll-down-" + firstBanner[t], () => meta.ShowShop(tab), banner, 100000f, problems);   // clamped to the end of the list
                yield return CheckShopScroll("shop-scroll-mid-" + firstBanner[t], () => meta.ShowShop(tab), banner, ShopChrome.BannerBlockH + 300f, problems);
            }
            Report(problems);
        }

        /// <summary>Bug List 3 D2.a/b: ShowShop() with no arguments selects Gems / Coins at scroll 0; the Character page opens at its top (Heroes banner), not at Power-ups.</summary>
        [UnityTest]
        public IEnumerator ShopDefaultEntryIsGemsCoinsAtTopAndCharacterStartsAtTop()
        {
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            meta.ShowShop();
            yield return null;
            yield return null;
            if (meta.CurrentShop.Tab != ShopScreen.TabGems) problems.Add("default entry opened tab " + meta.CurrentShop.Tab + ", expected Gems / Coins");
            if (meta.CurrentShop.Section != 0) problems.Add("default entry section " + meta.CurrentShop.Section + ", expected the top");
            RectTransform list = FindRt("ShopList");
            if (list == null || list.anchoredPosition.y > 0.5f) problems.Add("default entry: Gems list not at scroll 0");
            meta.ShowShop(ShopScreen.TabCharacter);
            yield return null;
            yield return null;
            list = FindRt("ShopList");
            if (list == null || list.anchoredPosition.y > 0.5f) problems.Add("Character page not at scroll 0 (Heroes banner hidden)");
            if (meta.CurrentShop.Section != 0) problems.Add("Character page section " + meta.CurrentShop.Section + ", expected 0");
            Report(problems);
        }

        /// <summary>Shows a shop tab at every aspect, scrolls its list by <paramref name="scrollPx"/> (reference px, clamped) and audits the result.</summary>
        static IEnumerator CheckShopScroll(string screen, System.Action show, string bannerName, float scrollPx, List<string> all)
        {
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                foreach (Rect safe in new[] { FullSafe, NotchSafe })
                {
                    Rect s = safe;
                    UiKit.SafeAreaNormalized = () => s;
                    show();
                    yield return ApplyAspect(a, holder);
                    yield return null;
                    string tag = screen + "@" + a.Name + (safe == NotchSafe ? "+notch" : "");
                    RectTransform list = FindRt("ShopList");
                    RectTransform vp = list != null ? list.parent as RectTransform : null;
                    if (list == null || vp == null) { all.Add(tag + ": no ShopList/ShopViewport"); continue; }
                    LayoutRebuilder.ForceRebuildLayoutImmediate(list);
                    float maxY = Mathf.Max(0f, list.rect.height - vp.rect.height);
                    list.anchoredPosition = new Vector2(0f, Mathf.Clamp(scrollPx, 0f, maxY));
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Rect vpBox = CanvasBox(vp);
                    RectTransform scene = FindRt("Scene");
                    RectTransform bannerRt = FindRt(bannerName, list);
                    if (scene == null) all.Add(tag + ": Momo scene missing");
                    else if (CanvasBox(scene).yMin < vpBox.yMax - 1.5f) all.Add(tag + ": the list viewport covers the Momo art (art bottom " + CanvasBox(scene).yMin + ", viewport top " + vpBox.yMax + ")");
                    RectTransform shopBody = FindRt("ShopBody");
                    if (shopBody != null && shopBody.Find("Ribbon") != null) all.Add(tag + ": a fixed banner is still in the page header");
                    if (bannerRt == null) all.Add(tag + ": first banner " + bannerName + " is not inside the scrolling list");
                    else
                    {
                        Rect bb = CanvasBox(bannerRt);
                        float pos = list.anchoredPosition.y;
                        if (pos <= 0.5f)
                        {
                            if (bb.yMax > vpBox.yMax + 1.5f || bb.yMin < vpBox.yMin) all.Add(tag + ": at the top the first banner is not the first visible item (" + bb + " viewport " + vpBox + ")");
                        }
                        else if (pos >= ShopChrome.BannerBlockH + 8f && bb.yMin < vpBox.yMax - 1.5f)
                            all.Add(tag + ": scrolled down by " + Mathf.RoundToInt(pos) + " but the first banner is still in view (" + bb + " viewport " + vpBox + ")");
                    }
                    all.AddRange(Audit(tag, a, safe));
                }
            }
            Restore(holder);
        }

        IEnumerator ShopAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            GameSession.Progress.AddGems(300);
            GameSession.Progress.AddCoins(12345);

            // Release layout: the gem buttons say "Not available yet".
            yield return CheckScreen("shop-tab-gems", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-tab-gems-coins-section", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection), problems);
            // Remove Ads section between the gems and the coins (owner 2026-11-17): for sale, then owned ("Ads removed", disabled).
            yield return CheckScreen("shop-tab-gems-remove-ads", () => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection); RectTransform adsCard = FindRt(ShopScreen.RemoveAdsCardName); Assert.IsNotNull(adsCard, "the Remove Ads card is on the Gems page"); Assert.IsNull(adsCard.Find("Price"), "the price is on the button, not a loose text above it (owner 2026-11-20)"); Transform adsBuy = adsCard.Find(ShopScreen.RemoveAdsBuyName); Assert.IsNotNull(adsBuy, "the buy button"); Text adsBuyText = adsBuy.GetComponentInChildren<Text>(); Assert.IsNotNull(adsBuyText); StringAssert.Contains("$", adsBuyText.text, "the price reads on the green button"); }, problems);
            yield return CheckScreen("shop-tab-gems-remove-ads-owned", () => { GameSession.Progress.ApplyStorePurchase(ProductCatalog.AdsRemove, "ux-test", System.DateTime.UtcNow); meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection); }, problems);
            GameSession.Progress.Data.AdsRemoved = false;
            yield return CheckScreen("shop-tab-character-powerups", () => meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection), problems);
            yield return CheckScreen("shop-tab-character-powerups-bought", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection); meta.CurrentShop.TapBuyPowerUp(GameSession.Progress.PowerUpDefs.Get("pw_flight"), PowerUpCurrency.Coins); }, problems);
            // Shop 2a: Heroes and Power-ups are coins only: no gem buy button anywhere on the Character page (CriticalUX)
            meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection);
            yield return null;
            foreach (UnityEngine.UI.Button b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.name == "BuyGems") problems.Add("shop-2a: a gem buy button is still on the Character page");
            // Treasure Chest screen (docs/22 point 3): rows, the (i) texts, the confirmation, the single and the ten reveal
            yield return CheckScreen("treasure-chests", () => meta.ShowTreasureChests(), problems);
            // VIP tab (owner issue 35): one scrollable page, Subscription section first, Chests under it; the old 'Chests' tab is gone
            yield return CheckScreen("shop-tab-vip", () => meta.ShowShop(ShopScreen.TabVip), problems);
            yield return CheckScreen("shop-tab-vip-chests-section", () => meta.ShowShop(ShopScreen.TabVip, ShopScreen.VipChestsSection), problems);
            yield return CheckScreen("shop-tab-character-heroes-section", () => meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection), problems);   // Heroes banner + the 2 x 2 hero cards (Bug List 4 E2)
            // Heroes section popups (E2.c, E2.e): the (i) of each tier, "No Rare heroes yet" (nothing charged), the confirm popup and the hero reveal (gems topped up first, so later screens see the same wallet)
            foreach (CharacterRarity tier in HeroPurchase.Tiers)
            {
                CharacterRarity t = tier;
                yield return CheckScreen("shop-heroes-info-" + HeroPurchase.RarityName(t).ToLowerInvariant(), () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroInfo(t); }, problems);
            }
            yield return CheckScreen("shop-heroes-none-popup", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroBuy(CharacterRarity.Rare); }, problems);
            yield return CheckScreen("shop-heroes-none-popup-legendary", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroBuy(CharacterRarity.Legendary); }, problems);
            yield return CheckScreen("shop-heroes-confirm-popup", () => { GameSession.Progress.AddCoins(HeroPurchase.CoinPrice(CharacterRarity.Uncommon)); meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroBuy(CharacterRarity.Uncommon); }, problems);   // coins for the card first (24,000 since Final Change 1), else the not-enough-coins popup shows
            yield return CheckScreen("shop-heroes-reveal", () =>
            {
                GameSession.Progress.AddCoins(HeroPurchase.CoinPrice(CharacterRarity.Uncommon));
                meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection);
                meta.CurrentShop.TapHeroBuy(CharacterRarity.Uncommon);
                ClickByName("Buy for");
            }, problems);
            yield return CheckScreen("treasure-confirm", () => { GameSession.Progress.AddGems(ChestRolls.Price(ChestKind.Golden, 10)); meta.ShowTreasureChests(); meta.CurrentTreasure.TapBuy(ChestKind.Golden, 10); }, problems);
            yield return CheckScreen("treasure-reveal-single", () => { GameSession.Progress.AddGems(ChestRolls.Price(ChestKind.Iron, 1)); meta.ShowTreasureChests(); meta.CurrentTreasure.OpenNow(ChestKind.Iron, 1); meta.CurrentTreasure.Reveal.Skip(); }, problems);
            yield return CheckScreen("treasure-reveal-ten", () => { GameSession.Progress.AddGems(ChestRolls.Price(ChestKind.Golden, 10)); meta.ShowTreasureChests(); meta.CurrentTreasure.OpenNow(ChestKind.Golden, 10); meta.CurrentTreasure.Reveal.Skip(); }, problems);
            // Bug List 5 F3: the chests hold a hero card too. The "i" popup of each chest, the Open 1 reveal with a hero row, the Open 10 reveal with a list of hero rows
            yield return CheckScreen("treasure-info-iron", () => { meta.ShowShop(ShopScreen.TabVip, ShopScreen.VipChestsSection); meta.CurrentTreasure.ShowInfo(ChestKind.Iron); }, problems);
            yield return CheckScreen("treasure-info-gold", () => { meta.ShowShop(ShopScreen.TabVip, ShopScreen.VipChestsSection); meta.CurrentTreasure.ShowInfo(ChestKind.Golden); }, problems);
            yield return CheckScreen("treasure-reveal-iron-ten", () => { GameSession.Progress.AddGems(ChestRolls.Price(ChestKind.Iron, 10)); meta.ShowTreasureChests(); meta.CurrentTreasure.OpenNow(ChestKind.Iron, 10); meta.CurrentTreasure.Reveal.Skip(); }, problems);
            yield return CheckScreen("shop-tab-bundles", () => meta.ShowShop(ShopScreen.TabBundles), problems);
            yield return CheckScreen("shop-gems-notbuyable-popup", () => { meta.ShowShop(); meta.CurrentShop.TapGemPack(3); }, problems);

            // Development layout: "Buy (test)" and the test purchase popups.
            ShopScreen.TestPurchasesOverride = true;
            yield return CheckScreen("shop-gems-test", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-test-buy-popup", () => { meta.ShowShop(); meta.CurrentShop.TapGemPack(6); }, problems);
            yield return CheckScreen("shop-tab-character-powerups-test", () => meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection), problems);
            ShopScreen.TestPurchasesOverride = null;

            // Coin popups: confirm above 50 gems, not enough gems, done.
            yield return CheckScreen("shop-coin-confirm-popup", () => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection); meta.CurrentShop.TapCoinPack(4); }, problems);
            yield return CheckScreen("shop-coin-short-goes-to-gems",() => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection); meta.CurrentShop.TapCoinPack(6); }, problems);
            yield return CheckScreen("shop-coins-done-popup", () => { meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection); meta.CurrentShop.TapCoinPack(0); }, problems);

            // Promo off: list prices only, no crossed-out price and no Launch offer badge.
            GameSettings.LaunchPromo = false;
            yield return CheckScreen("shop-gems-list-prices", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.GemsSection), problems);
            yield return CheckScreen("shop-coins-list-prices", () => meta.ShowShop(ShopScreen.TabGems, ShopScreen.CoinsSection), problems);
            GameSettings.LaunchPromo = true;

            // Power-up shop: the not-enough-coins popup (with an empty wallet) points to the coins section.
            GameSession.Progress.TrySpendCoins(GameSession.Progress.Coins);
            yield return CheckScreen("treasure-need-gems", () => { GameSession.Progress.TrySpendGems(GameSession.Progress.Gems); meta.ShowTreasureChests(); meta.CurrentTreasure.TapBuy(ChestKind.Iron, 1); }, problems);
            yield return CheckScreen("shop-tab2-not-enough-popup", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection); meta.CurrentShop.TapBuyPowerUp(GameSession.Progress.PowerUpDefs.Get("pw_rocket"), PowerUpCurrency.Coins); }, problems);

            yield return CheckScreen("shop-heroes-not-enough-popup", () => { meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.HeroesSection); meta.CurrentShop.TapHeroBuy(CharacterRarity.Uncommon); }, problems);   // empty wallet

            // Rewards (docs/26 section 4): the three tabs with something claimable (stars, points, play time), and the "Reward!" card
            for (int lvl = 1; lvl <= 9; lvl++) GameSession.Progress.ApplyRun("lvl_001_0" + lvl, new RunOutcome { Completed = true, StarMask = 7, Score = 100, CoinsCollected = 10 });
            GameSession.Progress.RecordLogin();
            GameSession.Progress.AddPlaySeconds(600);
            yield return CheckScreen("rewards-main", () => meta.ShowRewards(RewardTab.Main), problems);
            yield return CheckScreen("rewards-daily", () => meta.ShowRewards(RewardTab.Daily), problems);
            yield return CheckScreen("rewards-weekly", () => meta.ShowRewards(RewardTab.Weekly), problems);
            yield return CheckScreen("rewards-main-region3", () => { meta.ShowRewards(RewardTab.Main); meta.CurrentRewards.SelectRegion(3); }, problems);   // a locked region: lock on the circle, the locked note on top, goals at 0
            yield return CheckScreen("rewards-main-comingsoon", () => { meta.ShowRewards(RewardTab.Main); meta.CurrentRewards.SelectRegion(8); }, problems);   // Coming Soon: one centred locked card, no goals
            yield return CheckScreen("rewards-reward-card", () =>
            {
                // the small icon card: gem, coin, two power-up types and a hero face per hero copy (two rows of chips, the widest case)
                meta.ShowRewards(RewardTab.Main);
                var sample = new RewardClaimOutcome { Result = RewardClaimResult.Ok, Gems = 40, Coins = 2000 };
                sample.PowerUpIds.Add("pw_flight"); sample.PowerUpIds.Add("pw_flight"); sample.PowerUpIds.Add("pw_shield");
                sample.HeroIds.Add("forestgirl"); sample.HeroIds.Add("forestgirl");
                meta.CurrentRewards.ShowClaimed(sample);
            }, problems);
            // ready states of the redesign: yellow region-goal cards with the green Claim button, Claim All, and a claimed (green, check, open chest) look
            for (int lvl = 10; lvl <= 15; lvl++) GameSession.Progress.ApplyRun("lvl_001_" + lvl.ToString("00"), new RunOutcome { Completed = true, StarMask = 7, Score = 100, CoinsCollected = 10 });
            GameSession.Progress.AddPlaySeconds(120 * 60);
            yield return CheckScreen("rewards-main-ready", () => { meta.ShowRewards(RewardTab.Main); meta.CurrentRewards.SelectRegion(1); }, problems);
            yield return CheckScreen("rewards-weekly-ready", () => meta.ShowRewards(RewardTab.Weekly), problems);
            yield return CheckScreen("rewards-daily-ready", () => meta.ShowRewards(RewardTab.Daily), problems);
            yield return CheckScreen("rewards-daily-claimed", () => { meta.ShowRewards(RewardTab.Daily); GameSession.Progress.TryClaimReward(RewardTab.Daily, "dp2_100"); meta.ShowRewards(RewardTab.Daily); }, problems);
            // Daily free power-up card (owner 2026-11-17): ad ready, ad not ready (dimmed), then claimed today.
            yield return CheckScreen("rewards-daily-ad-powerup-ready", () => { Services.Ads = new StubAdService { Immediate = true }; meta.ShowRewards(RewardTab.Daily); Assert.IsNotNull(FindRt(RewardsScreen.AdPowerUpCardName), "the card is on the Daily tab"); }, problems);
            yield return CheckScreen("rewards-daily-ad-powerup-not-ready", () => { Services.Ads = new StubAdService { Immediate = true, Ready = false }; meta.ShowRewards(RewardTab.Daily); }, problems);
            Services.Ads = new StubAdService { Immediate = true };
            yield return CheckScreen("rewards-daily-ad-powerup-claimed", () => { meta.ShowRewards(RewardTab.Daily); meta.CurrentRewards.ClaimDailyAdPowerUp(); meta.ShowRewards(RewardTab.Daily); }, problems);
            Services.Reset();
            yield return CheckScreen("rewards-main-claimed", () =>
            {
                // region 1: the first goals of two groups claimed (green check cards), the rest still ready or open; the circle keeps its dot while something is left
                GameSession.Progress.TryClaimRegionGoal("g2_1_clear_5");
                GameSession.Progress.TryClaimRegionGoal("g2_1_stars_25");
                meta.ShowRewards(RewardTab.Main);
                meta.CurrentRewards.SelectRegion(1);
            }, problems);
            yield return CheckScreen("rewards-weekly-claimed", () => { GameSession.Progress.TryClaimReward(RewardTab.Weekly, "w2_play120"); meta.ShowRewards(RewardTab.Weekly); }, problems);   // compact cards: claimed look
            Report(problems);
        }

        // ---- Merge screen (docs/34): ready, not enough copies, top tier, a full grid, and the "New hero" result card ----

        [UnityTest]
        public IEnumerator MergeScreenHasNoOverlapAndFitsEveryAspect() { return MergeAudit(100); }

        /// <summary>CUX-5: the Merge screen survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator MergeScreenSurvives130PercentText() { return MergeAudit(130); }

        /// <summary>A clean save: Momo with 3 copies (mergeable), and with <paramref name="full"/> also 4 Capi, 3 Bonbon and 2 Otto (only types with 3 or more copies are listed) so the list has two rows.</summary>
        static void MergeState(bool full)
        {
            GameSession.Progress.ResetAll();
            GameSession.Progress.AddCopies("momo", 2);
            if (!full) return;
            foreach (KeyValuePair<string, int> kv in new Dictionary<string, int> { { "forestgirl", 4 }, { "candygirl", 3 }, { "beachotter", 2 }, { "momo", 1 } })
            {
                GameSession.Progress.AddCopies(kv.Key, kv.Value);
                GameSession.Progress.MarkHeroRevealed(kv.Key);
            }
        }

        /// <summary>Owner 2026-11-09: all 25 ACTIVE heroes owned (3 copies of each type below Legendary, 1 Legendary): the biggest hero lists the game can have (20 mergeable types x 3 cards = 60 merge cards, 25 hero types in All Heroes).</summary>
        static void Region1Full()
        {
            GameSession.Progress.ResetAll();
            foreach (string line in new[] { "momo", "forestgirl", "beachotter", "candygirl", "monk" })
                foreach (string suffix in new[] { "", "_u", "_r", "_e", "_l" })
                {
                    GameSession.Progress.AddCopies(line + suffix, suffix == "_l" ? 1 : 3);
                    GameSession.Progress.MarkHeroRevealed(line + suffix);
                }
        }

        IEnumerator MergeAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            // the list: only hero types with 3 or more copies, one card per copy, no numbers
            yield return CheckScreen("merge-list", () => { MergeState(false); meta.ShowMerge(); }, problems);
            yield return CheckScreen("merge-full-grid", () => { MergeState(true); meta.ShowMerge(); }, problems);
            yield return CheckScreen("merge-not-enough", () => { GameSession.Progress.ResetAll(); meta.ShowMerge(); }, problems);   // Momo with his one starting copy: an empty list
            // All 25 active heroes owned: 20 mergeable types, 60 cards (a long scrolling grid), top and scrolled, plus a selected Rare type
            yield return CheckScreen("merge-region1-all", () => { Region1Full(); meta.ShowMerge(); }, problems);
            yield return CheckScreen("merge-region1-selected-rare", () => { Region1Full(); meta.ShowMerge(); meta.CurrentMerge.SelectType("forestgirl_r"); }, problems);
            // selection: three of one type (green ticks, three filled slots, Merge enabled), a wrong mix (Merge disabled with the hint)
            yield return CheckScreen("merge-selected-3", () => { MergeState(true); meta.ShowMerge(); meta.CurrentMerge.SelectType("momo"); }, problems);
            yield return CheckScreen("merge-selected-capi", () => { MergeState(true); meta.ShowMerge(); meta.CurrentMerge.SelectType("forestgirl"); }, problems);
            yield return CheckScreen("merge-wrong-selection", () => { MergeState(true); meta.ShowMerge(); var m = meta.CurrentMerge; m.TapCard(0); m.TapCard(1); m.TapCard(m.Cards.Count - 1); }, problems);
            yield return CheckScreen("merge-one-selected", () => { MergeState(true); meta.ShowMerge(); meta.CurrentMerge.TapCard(0); }, problems);
            // after a merge: the three consumed cards are gone, the type vanished (1 Momo left), the result card is on top
            yield return CheckScreen("merge-after", () => { MergeState(false); meta.ShowMerge(); meta.CurrentMerge.SelectType("momo"); meta.CurrentMerge.TapMerge(); }, problems);
            Assert.AreEqual(1, GameSession.Progress.Copies("momo_u"), "the audit's merge produced the Uncommon hero");
            Assert.AreEqual(0, meta.CurrentMerge.Cards.Count, "the merged type left the list");
            Report(problems);
        }

        // ---- All Heroes (Bug List 5 F2.c): top of the list, scrolled, the details popup of a locked and of an owned hero ----

        [UnityTest]
        public IEnumerator AllHeroesScreenHasNoOverlapAndFitsEveryAspect() { return AllHeroesAudit(100); }

        /// <summary>CUX-5: the All Heroes screen survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator AllHeroesScreenSurvives130PercentText() { return AllHeroesAudit(130); }

        IEnumerator AllHeroesAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            yield return CheckScreen("all-heroes", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); }, problems);
            yield return CheckScreen("all-heroes-scrolled", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.ScrollTo(0.5f); }, problems);
            yield return CheckScreen("all-heroes-bottom", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.ScrollTo(1f); }, problems);
            yield return CheckScreen("all-heroes-owned", () => { MergeState(true); GameSession.Progress.AddCopies("momo_u", 1); meta.ShowAllHeroes(); }, problems);
            // All 25 active heroes owned (full colour cards), top, a quarter and the bottom, and the details of a new hero
            yield return CheckScreen("all-heroes-region1-owned", () => { Region1Full(); meta.ShowAllHeroes(); }, problems);
            yield return CheckScreen("all-heroes-region1-scrolled", () => { Region1Full(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.ScrollTo(0.2f); }, problems);
            yield return CheckScreen("all-heroes-region1-info", () => { Region1Full(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.OpenInfo("forestgirl_l"); }, problems);
            // details popups: a real locked hero, a not-yet-made tier, and an owned hero
            yield return CheckScreen("all-heroes-info", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.OpenInfo("momo_u"); }, problems);
            yield return CheckScreen("all-heroes-info-legendary", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.OpenInfo("monk_legendary"); }, problems);
            yield return CheckScreen("all-heroes-info-owned", () => { GameSession.Progress.ResetAll(); meta.ShowAllHeroes(); meta.CurrentAllHeroes.OpenInfo("momo"); }, problems);
            // Back returns to the Heroes screen
            meta.ShowAllHeroes();
            yield return null;
            Button back = null;
            foreach (Button bt in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) if (bt.name == "Back") back = bt;
            Assert.IsNotNull(back, "All Heroes has a Back button");
            back.onClick.Invoke();
            Assert.AreEqual("Characters", meta.CurrentScreen, "Back returns to the Hero screen");
            Report(problems);
        }

        // ---- Heroes screen, Bug List 6 item 3: the three tabs (All Heroes, Merge, Powers), no bottom bar, one background ----

        [UnityTest]
        public IEnumerator PowersScreenHasNoOverlapAndFitsEveryAspect() { return PowersAudit(100); }

        /// <summary>CUX-5: the Powers screen survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator PowersScreenSurvives130PercentText() { return PowersAudit(130); }

        static void GivePowers()
        {
            GameSession.Progress.ResetAll();
            GameSession.Progress.GrantPowerUp("pw_flight", 3);
            GameSession.Progress.GrantPowerUp("pw_glide", 12);
            GameSession.Progress.GrantPowerUp("pw_wallstick", 99);
        }

        IEnumerator PowersAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();
            yield return CheckScreen("powers", () => { GameSession.Progress.ResetAll(); meta.ShowPowers(); }, problems);
            yield return CheckScreen("powers-owned", () => { GivePowers(); meta.ShowPowers(); }, problems);
            yield return CheckScreen("powers-scrolled", () => { GivePowers(); meta.ShowPowers(); meta.CurrentPowers.ScrollTo(1f); }, problems);
            yield return CheckScreen("powers-info", () => { GivePowers(); meta.ShowPowers(); meta.CurrentPowers.OpenInfo("pw_rocket"); }, problems);   // the longest description
            Report(problems);
        }

        static Button FindButtonNamed(string name)
        {
            foreach (Button bt in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (bt.name == name && bt.gameObject.activeInHierarchy) return bt;
            return null;
        }

        /// <summary>Bug List 6 item 3a: the Powers tab lists all 7 power-ups, each with the number owned at the BOTTOM RIGHT of its card; no buy button; a tap shows only name and description.</summary>
        [UnityTest]
        public IEnumerator PowersTabListsSevenPowerUpsWithOwnedCountAtBottomRight()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            GivePowers();
            meta.ShowPowers();
            yield return null;
            yield return null;
            Assert.AreEqual("Powers", meta.CurrentScreen);
            Assert.AreEqual(9, meta.CurrentPowers.Cards.Count, "all 9 power-ups (Bug List 4: Magnet replaced by Safety Net, Glide Cape, Wide Platforms)");
            var expected = new Dictionary<string, int> { { "pw_flight", 3 }, { "pw_rocket", 0 }, { "pw_doublejump", 0 }, { "pw_glide", 12 }, { "pw_shield", 0 }, { "pw_slowtime", 0 }, { "pw_wallstick", 99 }, { "pw_safety_net", 0 }, { "pw_wide_platforms", 0 } };
            Assert.AreEqual(9, expected.Count);
            foreach (KeyValuePair<string, int> kv in expected)
            {
                RectTransform card = FindRt("Power_" + kv.Key);
                Assert.IsNotNull(card, "card of " + kv.Key);
                Text owned = null;
                foreach (Text t in card.GetComponentsInChildren<Text>()) if (t.name == PowersScreen.OwnedName) owned = t;
                Assert.IsNotNull(owned, "owned number on " + kv.Key);
                Assert.AreEqual(kv.Value.ToString(), owned.text, "owned count of " + kv.Key);
                var cc = new Vector3[4];
                card.GetWorldCorners(cc);
                Vector3 mid = (cc[0] + cc[2]) * 0.5f, at = owned.rectTransform.position;
                Assert.AreEqual(mid.x, at.x, 0.05f * (cc[2].x - cc[0].x), kv.Key + ": the number is centred horizontally on the card (Bug List 8 item 1d)");
                var oc = new Vector3[4];
                owned.rectTransform.GetWorldCorners(oc);
                Assert.Greater(oc[2].x - oc[0].x, 0.6f * (cc[2].x - cc[0].x), kv.Key + ": the number box is wide (most of the card width)");
                Assert.Less(at.y, mid.y, kv.Key + ": the number is on the bottom half of the card");
            }
            foreach (Button bt in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                Assert.IsFalse(bt.name.StartsWith("Buy"), bt.name + ": the Powers tab has no buy button");
            // a tap on a card opens only the name and the description
            Assert.IsTrue(meta.CurrentPowers.OpenInfo("pw_shield"));
            yield return null;
            Assert.AreEqual("pw_shield", meta.CurrentPowers.OpenInfoFor.Id);
            RectTransform pop = FindRt(PowersScreen.InfoPopupName);
            Assert.IsNotNull(pop, "the details popup");
            var shown = new List<string>();
            foreach (Text t in pop.GetComponentsInChildren<Text>()) shown.Add(t.text);
            CollectionAssert.AreEquivalent(new[] { "Shield", GameSession.Progress.PowerUpDefs.Get("pw_shield").Description }, shown, "only the name and the description");
            meta.CurrentPowers.CloseInfo();
            // a unit gained elsewhere (the shop) shows here
            GameSession.Progress.GrantPowerUp("pw_flight", 1);
            meta.ShowPowers();
            yield return null;
            Assert.AreEqual(4, GameSession.Progress.PowerUpCount("pw_flight"));
            foreach (Text t in FindRt("Power_pw_flight").GetComponentsInChildren<Text>()) if (t.name == PowersScreen.OwnedName) Assert.AreEqual("4", t.text);
        }

        /// <summary>Bug List 8 items 1c/1e: Back of the Heroes and Rewards screens returns to where they were opened (World map, Level screen, home); the "i" of All Heroes opens the hero stat sheet and Back returns to All Heroes.</summary>
        [UnityTest]
        public IEnumerator BackReturnsToTheScreenThatOpenedHeroesAndRewards()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            // Heroes from the World map
            meta.ShowMap();
            yield return null;
            meta.ShowCharacters();
            yield return null;
            FindButtonNamed("Back").onClick.Invoke();
            yield return null;
            Assert.AreEqual("Map", meta.CurrentScreen, "Heroes opened from the World map goes back to it");
            // Heroes from the Level screen, through All Heroes and a hero sheet
            meta.ShowLevels(r1);
            yield return null;
            meta.ShowCharacters();
            yield return null;
            FindButtonNamed(HeroTabs.ButtonNames[HeroTabs.AllHeroes]).onClick.Invoke();
            yield return null;
            meta.CurrentAllHeroes.OpenInfo("momo");
            yield return null;
            Assert.AreEqual(HeroSheetScreen.ScreenName, meta.CurrentScreen, "the i opens the existing hero stat sheet");
            FindButtonNamed("Back").onClick.Invoke();
            yield return null;
            Assert.AreEqual(AllHeroesScreen.ScreenName, meta.CurrentScreen, "Back from the sheet returns to All Heroes");
            FindButtonNamed("Back").onClick.Invoke();
            yield return null;
            Assert.AreEqual("Characters", meta.CurrentScreen);
            FindButtonNamed("Back").onClick.Invoke();
            yield return null;
            Assert.AreEqual("Levels", meta.CurrentScreen, "Heroes opened from the Level screen goes back to it");
            // Rewards from the World map and from the Level screen
            meta.ShowMap();
            yield return null;
            meta.ShowRewards();
            yield return null;
            FindButtonNamed("Back").onClick.Invoke();
            yield return null;
            Assert.AreEqual("Map", meta.CurrentScreen, "Rewards opened from the World map goes back to it");
            meta.ShowLevels(r1);
            yield return null;
            meta.ShowRewards();
            yield return null;
            FindButtonNamed("Back").onClick.Invoke();
            yield return null;
            Assert.AreEqual("Levels", meta.CurrentScreen, "Rewards opened from the Level screen goes back to it");
        }

        /// <summary>Bug List 6 item 3a: the three tabs switch between All Heroes, Merge and Powers, the tab of the page showing is selected, Back returns to the Heroes screen, and none of the pages has the old yellow button or a bottom bar.</summary>
        [UnityTest]
        public IEnumerator HeroTabsSwitchPagesAndShowTheSelectedTab()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            meta.ShowCharacters();
            yield return null;
            Assert.AreEqual("Characters", meta.CurrentScreen);
            for (int i = 0; i < 3; i++) StringAssert.DoesNotContain("selected", FindButtonNamed(HeroTabs.ButtonNames[i]).GetComponent<A11yLabel>().Text, "no tab is selected on the Heroes landing screen");
            string[] screens = { AllHeroesScreen.ScreenName, MergeScreen.ScreenName, PowersScreen.ScreenName };
            for (int target = 0; target < 3; target++)
            {
                meta.ShowCharacters();
                yield return null;
                FindButtonNamed(HeroTabs.ButtonNames[target]).onClick.Invoke();   // from the landing screen
                yield return null;
                Assert.AreEqual(screens[target], meta.CurrentScreen, "tab " + target + " opens its page");
                for (int i = 0; i < 3; i++)
                {
                    Button tab = FindButtonNamed(HeroTabs.ButtonNames[i]);
                    Assert.IsNotNull(tab, HeroTabs.ButtonNames[i] + " on the " + screens[target] + " page");
                    Assert.AreEqual(i == target, tab.GetComponent<A11yLabel>().Text.Contains("selected"), "selected state of tab " + i + " on page " + target);
                }
                Assert.IsNull(FindRt("TabBar"), "no bottom bar on the " + screens[target] + " page");
                Assert.IsNull(FindRt("AllHeroesButton"), "no old yellow All Heroes button");
                // from this page to the next one
                int next = (target + 1) % 3;
                FindButtonNamed(HeroTabs.ButtonNames[next]).onClick.Invoke();
                yield return null;
                Assert.AreEqual(screens[next], meta.CurrentScreen, "tab " + next + " from page " + target);
                FindButtonNamed("Back").onClick.Invoke();
                yield return null;
                Assert.AreEqual("Characters", meta.CurrentScreen, "Back returns to the Heroes screen");
            }
        }

        /// <summary>Bug List 6 item 3a: the All Heroes and Powers pages take the Merge screen's background (same painted slot or gradient, no yellow).</summary>
        [UnityTest]
        public IEnumerator HeroPagesShareTheMergeBackground()
        {
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            meta.ShowMerge();
            yield return null;
            Sprite mergeBg = BackgroundSprite("MergeBackground");
            Assert.IsNotNull(mergeBg, "the Merge background");
            meta.ShowAllHeroes();
            yield return null;
            Assert.AreSame(mergeBg, BackgroundSprite("AllHeroesBackground"), "All Heroes uses the Merge background");
            meta.ShowPowers();
            yield return null;
            Assert.AreSame(mergeBg, BackgroundSprite("PowersBackground"), "Powers uses the Merge background");
        }

        static Sprite BackgroundSprite(string root)
        {
            RectTransform bg = FindRt(root);
            if (bg == null) return null;
            foreach (Image im in bg.GetComponentsInChildren<Image>()) if (im.name == "Bg") return im.sprite;
            return null;
        }

        /// <summary>Bug List 6 item 8: a shop power-up card ALWAYS says "x1" (what one purchase gives) and never changes after a purchase; the owned count is only in Heroes > Powers.</summary>
        [UnityTest]
        public IEnumerator ShopPowerUpCardsAlwaysSayX1AndNeverChangeAfterAPurchase()
        {
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            GameSession.Progress.ResetAll();
            GameSession.Progress.AddCoins(50000);
            GameSession.Progress.GrantPowerUp("pw_glide", 5);   // owning some must not change the card either
            meta.ShowShop(ShopScreen.TabCharacter, ShopScreen.PowerupsSection);
            yield return null;
            yield return null;
            AssertShopCardsSayX1(9);
            int before = GameSession.Progress.PowerUpCount("pw_flight");
            meta.CurrentShop.TapBuyPowerUp(GameSession.Progress.PowerUpDefs.Get("pw_flight"), PowerUpCurrency.Coins);
            yield return null;
            yield return null;
            Assert.AreEqual(before + 1, GameSession.Progress.PowerUpCount("pw_flight"), "the purchase went through");
            AssertShopCardsSayX1(9);
        }

        static void AssertShopCardsSayX1(int cards)
        {
            int n = 0;
            foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (t.name != "Owned" || !t.gameObject.activeInHierarchy) continue;
                n++;
                Assert.AreEqual(ShopScreen.PerPurchaseText, t.text, "a shop power-up card always says x1");
            }
            Assert.AreEqual("x1", ShopScreen.PerPurchaseText);
            Assert.AreEqual(cards, n, "one x1 line per power-up card");
        }

        // ---- VIP page (docs/26 section 2, renamed from Subscription): release and test layouts, an active plan with Claim, the claimed state with countdown, the popups ----

        [UnityTest]
        public IEnumerator VipScreenHasNoOverlapAndFitsEveryAspect() { return SubscriptionAudit(100); }

        /// <summary>CUX-5: the VIP page survives the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator VipScreenSurvives130PercentText() { return SubscriptionAudit(130); }

        /// <summary>Clean wallet, optionally with a running plan; the show action may run once per aspect, so every state is rebuilt from scratch.</summary>
        static void SubscriptionState(string plan, bool claimedToday)
        {
            GameSession.Progress.ResetAll();
            GameSession.Progress.SubscriptionClock = () => System.DateTime.UtcNow;
            GameSession.Progress.SubscriptionLocalOffset = null;
            if (plan == null) return;
            GameSession.Progress.GrantTestSubscriptionPurchase(plan, true);
            if (claimedToday) GameSession.Progress.ClaimDaily();
        }

        IEnumerator SubscriptionAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            ShopScreen.TestPurchasesOverride = null;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();

            // Release layout: price buttons only, the buy tap says "Not available yet".
            yield return CheckScreen("vip-release", () => { SubscriptionState(null, false); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-release-buy-popup", () => { SubscriptionState(null, false); meta.ShowSubscription(); meta.CurrentSubscription.TapBuy("weekly"); }, problems);
            yield return CheckScreen("vip-plan-info-weekly", () => { SubscriptionState(null, false); meta.ShowSubscription(); meta.CurrentSubscription.ShowInfo("weekly"); }, problems);
            yield return CheckScreen("vip-plan-info-weekly-plus", () => { SubscriptionState(null, false); meta.ShowSubscription(); meta.CurrentSubscription.ShowInfo("weekly_plus"); }, problems);
            yield return CheckScreen("vip-plan-info-monthly", () => { SubscriptionState(null, false); meta.ShowSubscription(); meta.CurrentSubscription.ShowInfo("monthly"); }, problems);
            yield return CheckScreen("vip-plan-info-premium", () => { SubscriptionState(null, false); meta.ShowSubscription(); meta.CurrentSubscription.ShowInfo("monthly_premium"); }, problems);
            yield return CheckScreen("vip-opened-from-shop", () => { SubscriptionState(null, false); meta.ShowShop(); meta.ShowSubscription(); }, problems);

            // Development layout: the test note, the confirm popup, then an active plan.
            ShopScreen.TestPurchasesOverride = true;
            yield return CheckScreen("vip-test", () => { SubscriptionState(null, false); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-test-confirm-popup", () => { SubscriptionState(null, false); meta.ShowSubscription(); meta.CurrentSubscription.TapBuy("monthly"); }, problems);
            yield return CheckScreen("vip-weekly-active-claim", () => { SubscriptionState("weekly", false); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-weekly-claimed-popup", () => { SubscriptionState("weekly", false); meta.ShowSubscription(); meta.CurrentSubscription.TapClaim(); }, problems);
            yield return CheckScreen("vip-weekly-come-back-tomorrow", () => { SubscriptionState("weekly", true); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-monthly-active-claim", () => { SubscriptionState("monthly", false); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-monthly-come-back-tomorrow", () => { SubscriptionState("monthly", true); meta.ShowSubscription(); }, problems);
            yield return CheckScreen("vip-other-plan-popup", () => { SubscriptionState("monthly", false); meta.ShowSubscription(); meta.CurrentSubscription.TapBuy("weekly"); }, problems);
            ShopScreen.TestPurchasesOverride = null;
            Report(problems);
        }

        // ---- login screen and the choose-your-save popup (docs/35) ----

        [UnityTest]
        public IEnumerator LoginScreensHaveNoOverlapAndFitEveryAspect() { return LoginAudit(100); }

        /// <summary>CUX-5: the login screen and the conflict popup survive the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator LoginScreensSurvive130PercentText() { return LoginAudit(130); }

        static GameObject loginTestCanvas;

        static void DropLoginCanvas()
        {
            if (loginTestCanvas != null) Object.DestroyImmediate(loginTestCanvas);
            loginTestCanvas = null;
        }

        static void ShowLoginTest(bool conflict, bool googleAvailable)
        {
            DropLoginCanvas();
            MetaUiActive(false);
            Canvas canvas = UiKit.CreateCanvas("LoginTestUi", 20);
            loginTestCanvas = canvas.gameObject;
            if (conflict)
            {
                // the biggest numbers the cards must hold
                var device = new SaveSummary { LevelsCleared = 75, Gems = 1234567, Coins = 12345678, HeroCopies = 120, LastPlayedUtc = "2026-11-03T10:00:00Z" };
                var google = new SaveSummary { LevelsCleared = 1, Gems = 0, Coins = 0, HeroCopies = 1, LastPlayedUtc = "2026-10-01T10:00:00Z" };
                LoginScreen.BuildConflict(canvas.transform, device, google, "A Very Long Player Display Name", () => { }, () => { });
            }
            else LoginScreen.BuildLogin(canvas.transform, GameSession.Art, googleAvailable, () => { }, () => { });
        }

        IEnumerator LoginAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var problems = new List<string>();
            yield return CheckScreen("login", () => ShowLoginTest(false, true), problems);
            yield return CheckScreen("login-guest-only", () => ShowLoginTest(false, false), problems);
            yield return CheckScreen("login-conflict", () => ShowLoginTest(true, true), problems);
            DropLoginCanvas();
            MetaUiActive(true);
            Report(problems);
        }

        // ---- bundles page and timer offer (docs/26 section 3) ----

        [UnityTest]
        public IEnumerator BundlesPageAndTimerOfferHaveNoOverlapAndFitEveryAspect() { return BundlesAudit(100); }

        /// <summary>CUX-5: the bundles page, its popups and the timer offer survive the 130 percent text size too.</summary>
        [UnityTest]
        public IEnumerator BundlesPageAndTimerOfferSurvive130PercentText() { return BundlesAudit(130); }

        static GameObject bundlesTestCanvas;

        static void MetaUiActive(bool on)
        {
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c.name == "MetaUi") c.gameObject.SetActive(on);
        }

        static void DropBundlesCanvas()
        {
            if (bundlesTestCanvas != null) Object.DestroyImmediate(bundlesTestCanvas);
            bundlesTestCanvas = null;
        }

        /// <summary>A popup host for the page when it is drawn on its own canvas: the same modal "Popup" panel the Meta scene uses.</summary>
        static ShopScreen.Host BundlesTestHost(Canvas canvas, ArtCatalog art)
        {
            RectTransform popup = null;
            var host = new ShopScreen.Host();
            host.ClosePopup = () => { if (popup != null) Object.DestroyImmediate(popup.gameObject); popup = null; };
            host.CloseOverlay = host.ClosePopup;
            host.NewOverlay = () =>
            {
                host.ClosePopup();
                popup = UiKit.Rect(canvas.transform, "Popup");
                UiKit.Stretch(popup);
                popup.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
                return popup;
            };
            host.OpenPopup = (title, body, height) =>
            {
                RectTransform panel = UiKit.BuildPopupFrame(host.NewOverlay(), title, art, new Vector2(860f, height), host.ClosePopup);
                Text b = UiKit.AddText(panel, "Body", body, 44, Color.white);
                UiKit.OnPanel(b);
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(740f, Mathf.Min(520f, height - 214f - 200f)));
                b.alignment = TextAnchor.MiddleCenter;
            };
            host.PopupButton = (label, primary, y, action) => ToyKit.PopupAction(popup.Find("Panel"), label, ToyKit.Button(primary ? 1 : 0), Color.white, y, action);
            return host;
        }

        /// <summary>Draws the Bundles page the way the shop does (scene, ribbon, body, tab bar) on its own canvas, with the Meta canvas hidden so only the page is audited.</summary>
        static ShopScreen.Host ShowBundlesPage(int gems, int coins)
        {
            DropBundlesCanvas();
            MetaUiActive(false);
            GameSession.Progress.ResetAll();
            GameSession.Progress.AddGems(gems);
            GameSession.Progress.AddCoins(coins);
            Canvas canvas = UiKit.CreateCanvas("BundlesTestUi", 20);
            bundlesTestCanvas = canvas.gameObject;
            RectTransform screen = UiKit.SafeArea(canvas.transform);
            Text c, g, s;
            ShopChrome.BuildScene(screen, GameSession.Art, GameSession.Progress, out c, out g, out s);
            RectTransform body = ShopChrome.BuildBody(screen, "Bundles", ShopChrome.RibbonColor.Yellow, false);   // no fixed banner: it is the first scroll item (C2.a)
            ShopChrome.SetScrollBanner(body, "Bundles");
            ShopScreen.Host host = BundlesTestHost(canvas, GameSession.Art);
            BundlesScreen.Build(body, GameSession.Progress, GameSession.Art, host, null);
            ShopChrome.BuildTabBar(screen, ShopChrome.TabBundles, null, null);
            return host;
        }

        static void ClickByName(string name)
        {
            foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && b.name.StartsWith(name)) { b.onClick.Invoke(); return; }
        }

        /// <summary>Clean wallet with a running timer offer (the play-time counter is ticked past the longest gap).</summary>
        static void TimerOfferState()
        {
            DropBundlesCanvas();
            MetaUiActive(true);
            GameSession.Progress.ResetAll();
            for (int i = 0; i < 70 && GameSession.Progress.CurrentTimerOffer(System.DateTime.UtcNow) == null; i++) GameSession.Progress.TickPlayTime(60);
        }

        [UnityTest]
        public IEnumerator BundleNameSharesTheRowWithTheInfoButton() { return BundleNameRowAudit(100); }

        [UnityTest]
        public IEnumerator BundleNameSharesTheRowWithTheInfoButtonAt130PercentText() { return BundleNameRowAudit(130); }

        /// <summary>Bug List 3 D3: on every bundle card the name is right of the "i", on the same centre line, and ends before the Worth text; the name font stays at least 16 sp.</summary>
        IEnumerator BundleNameRowAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                foreach (Rect safe in new[] { FullSafe, NotchSafe })
                {
                    Rect s = safe;
                    UiKit.SafeAreaNormalized = () => s;
                    ShowBundlesPage(5000, 0);
                    yield return ApplyAspect(a, holder);
                    yield return null;
                    string tag = "bundle-name-row@" + a.Name + (safe == NotchSafe ? "+notch" : "") + "/" + textPercent;
                    int cards = 0;
                    foreach (var card in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                    {
                        if (!card.gameObject.activeInHierarchy || !card.name.StartsWith("Bundle_")) continue;
                        RectTransform info = card.Find("BundleInfo") as RectTransform, name = card.Find("Name") as RectTransform, worth = card.Find("Worth") as RectTransform;
                        if (info == null || name == null || worth == null) { problems.Add(tag + ": " + card.name + " lacks BundleInfo/Name/Worth"); continue; }
                        cards++;
                        Rect ib = CanvasBox(info), nb = CanvasBox(name), wb = CanvasBox(worth);
                        if (nb.xMin < ib.xMax - 0.5f) problems.Add(tag + ": " + card.name + " name starts left of the end of the i button");
                        if (nb.xMax > wb.xMin + 0.5f) problems.Add(tag + ": " + card.name + " name runs into the Worth text");
                        if (Mathf.Abs(nb.center.y - ib.center.y) > 2f) problems.Add(tag + ": " + card.name + " name is not on the i button's centre line (" + nb.center.y + " vs " + ib.center.y + ")");
                        var txt = name.GetComponent<UnityEngine.UI.Text>();
                        if (txt != null && txt.resizeTextMinSize < Mathf.RoundToInt(UiKit.MinBodyTextPx)) problems.Add(tag + ": name min font below 16 sp");
                        if (txt != null)
                        {
                            // the wrapped name must fit its row (so it cannot reach the chips below), in canvas pixels like the layout audit.
                            // Text.preferredHeight is already in rect units (it divides by pixelsPerUnit itself), so only the rect-to-canvas ratio is applied.
                            float needed = txt.preferredHeight * (nb.height / Mathf.Max(0.01f, name.rect.height));
                            if (needed > nb.height * 1.15f + 1f) problems.Add(tag + ": " + card.name + " name text (" + needed + " px) is taller than its row (" + nb.height + " px)");   // 15 percent tolerance: the real overlap audit is the authority, this is a sanity check
                        }
                    }
                    if (cards == 0) problems.Add(tag + ": no bundle cards found");
                }
            }
            Report(problems);
        }

        [UnityTest]
        public IEnumerator BundleBuyButtonsSitInABrownFrame() { return BundleBuyFrameAudit(100); }

        [UnityTest]
        public IEnumerator BundleBuyButtonsSitInABrownFrameAt130PercentText() { return BundleBuyFrameAudit(130); }

        /// <summary>Bug List 4 E1: on every bundle card the green Buy button is a child of the brown BuyFrame plate (same brown as the Coins strip), at least 48 dp, with at least 8 dp of frame around it, inside the card and clear of the "i", name, Worth text and chips. The Refresh buttons have no frame.</summary>
        IEnumerator BundleBuyFrameAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var problems = new List<string>();
            var holder = new RenderTexture[1];
            foreach (Aspect a in Aspects)
            {
                foreach (Rect safe in new[] { FullSafe, NotchSafe })
                {
                    Rect s = safe;
                    UiKit.SafeAreaNormalized = () => s;
                    ShowBundlesPage(5000, 0);
                    yield return ApplyAspect(a, holder);
                    yield return null;
                    string tag = "bundle-buy-frame@" + a.Name + (safe == NotchSafe ? "+notch" : "") + "/" + textPercent;
                    int cards = 0;
                    float minPx = UiKit.MinTouchPx;
                    foreach (var card in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                    {
                        if (!card.gameObject.activeInHierarchy || !card.name.StartsWith("Bundle_")) continue;
                        RectTransform frame = card.Find("BuyFrame") as RectTransform;
                        RectTransform buy = frame != null ? frame.Find("Buy") as RectTransform : null;
                        if (frame == null || buy == null) { problems.Add(tag + ": " + card.name + " has no BuyFrame/Buy"); continue; }
                        cards++;
                        Rect cb = CanvasBox(card), fb = CanvasBox(frame), bb = CanvasBox(buy);
                        var fi = frame.GetComponent<UnityEngine.UI.Image>();
                        if (fi == null || (Vector4)fi.color != (Vector4)BundlesScreen.BuyFrameColor) problems.Add(tag + ": " + card.name + " frame is not the Coins brown");
                        if (bb.width < minPx - 0.5f || bb.height < minPx - 0.5f) problems.Add(tag + ": " + card.name + " buy button below 48 dp");
                        if (bb.xMin - fb.xMin < 8f - 0.5f || fb.xMax - bb.xMax < 8f - 0.5f || bb.yMin - fb.yMin < 8f - 0.5f || fb.yMax - bb.yMax < 8f - 0.5f)
                            problems.Add(tag + ": " + card.name + " frame padding below 8 dp");
                        if (fb.xMin < cb.xMin - 0.5f || fb.xMax > cb.xMax + 0.5f || fb.yMin < cb.yMin - 0.5f || fb.yMax > cb.yMax + 0.5f) problems.Add(tag + ": " + card.name + " frame leaves the card");
                        foreach (string other in new[] { "BundleInfo", "Name", "Worth" })
                        {
                            RectTransform o = card.Find(other) as RectTransform;
                            if (o != null && CanvasBox(o).Overlaps(fb)) problems.Add(tag + ": " + card.name + " frame overlaps " + other);
                        }
                        foreach (Transform ch in card)
                            if (ch.name == "Chip" && ch.gameObject.activeInHierarchy && CanvasBox((RectTransform)ch).Overlaps(fb)) problems.Add(tag + ": " + card.name + " frame overlaps a chip");
                    }
                    if (cards == 0) problems.Add(tag + ": no bundle cards found");
                    foreach (string refresh in new[] { "RefreshGems", "RefreshCoins" })
                    {
                        RectTransform rb = FindRt(refresh);
                        if (rb != null && rb.parent.name == "BuyFrame") problems.Add(tag + ": " + refresh + " must not have the brown frame");
                    }
                }
            }
            DropBundlesCanvas();
            Report(problems);
        }

        IEnumerator BundlesAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            var problems = new List<string>();

            yield return CheckScreen("bundles-page-empty-wallet", () => ShowBundlesPage(0, 0), problems);
            yield return CheckScreen("bundles-page-rich", () => ShowBundlesPage(5000, 500), problems);
            yield return CheckScreen("bundles-info", () => { ShowBundlesPage(5000, 0); ClickByName("BundleInfo"); }, problems);   // the "i" popup of one bundle: icons, hero chance, x worth
            yield return CheckScreen("bundles-not-enough-gems-popup", () => { ShowBundlesPage(0, 0); ClickByName("Buy"); }, problems);
            yield return CheckScreen("bundles-confirm-popup", () => { ShowBundlesPage(5000, 0); ClickByName("Buy"); }, problems);
            yield return CheckScreen("bundles-card-reveal", () => { ShowBundlesPage(5000, 0); ClickByName("Buy"); ClickByName("Buy for"); }, problems);   // "You got: name (rarity)" with the hero portrait
            yield return CheckScreen("bundles-card-reveal-long-name", () =>
            {
                ShopScreen.Host h = ShowBundlesPage(5000, 0);
                BundleDef four = null;
                foreach (BundleDef d in GameSession.Progress.Bundles.All) if (d.PowerUps.Count == 4) { four = d; break; }   // the longest "also in" line
                BundlesScreen.ShowReveal(h, four ?? GameSession.Progress.Bundles.All[0], CharacterDefs.Get("candygirl_u"));
            }, problems);
            yield return CheckScreen("bundles-card-reveal-no-hero-fallback", () => { ShopScreen.Host h = ShowBundlesPage(5000, 0); BundlesScreen.ShowReveal(h, GameSession.Progress.Bundles.All[0], null); }, problems);
            yield return CheckScreen("bundles-refreshed-with-coins", () => { ShowBundlesPage(5000, 500); ClickByName("RefreshCoins"); }, problems);
            yield return CheckScreen("bundles-refresh-toast-broke", () => { ShowBundlesPage(0, 0); ClickByName("RefreshGems"); }, problems);
            DropBundlesCanvas();
            MetaUiActive(true);

            // Timer offer: the corner button on the home and map screens, and its popup.
            yield return CheckScreen("timer-offer-home", () => { TimerOfferState(); meta.ShowMenu(); }, problems);
            yield return CheckScreen("timer-offer-map", () => { TimerOfferState(); meta.ShowMap(); }, problems);
            yield return CheckScreen("timer-offer-popup", () => { TimerOfferState(); meta.ShowMenu(); ClickByName("BundleOffer"); }, problems);
            yield return CheckScreen("timer-offer-info", () => { TimerOfferState(); meta.ShowMenu(); ClickByName("BundleOffer"); ClickByName("BundleInfo"); }, problems);
            Report(problems);
        }

        // ---- special level types (owner Final Change 1, 2026-11-22): tile badge, level popup, run intro, One Life (no revive), No Power-ups (no buttons), Keep Moving (ring + cue) ----
        // Region 1 placement: L6 No Wall Touch, L9 One Life, L14 No Power-ups, L16 Keep Moving, L21 Chase, L24 Combo Climb.

        [UnityTest]
        public IEnumerator SpecialLevelTilesAndPopupsFitEveryAspect() { return SpecialLevelMetaAudit(100); }

        [UnityTest]
        public IEnumerator SpecialLevelTilesAndPopupsSurvive130PercentText() { return SpecialLevelMetaAudit(130); }

        IEnumerator SpecialLevelMetaAudit(int textPercent)
        {
            GameSettings.TextPercent = textPercent;
            int mode = GameSettings.Difficulty;
            for (int i = 1; i <= 23; i++)   // levels 1 to 24 are open on the selected setting (24 is the last special level; 25 stays locked so no region unlock pops up)
                GameSession.Progress.ApplyRun("lvl_001_" + i.ToString("D2"), new RunOutcome { Completed = true, StarMask = 7, Score = 100, CoinsCollected = 10, Mode = mode });
            // A medal on two two-digit tiles (16 carries the special badge, 20 the gem badge): the medal is the top left corner pip and must stay clear of the number at 130 percent text.
            string[] medalLevels = { "lvl_001_16", "lvl_001_20" };
            foreach (string id in medalLevels)
            {
                RunRecord fast = TimedRun(id, GameSession.Repo.GetRegionByIndex(1).Id, 120, 3600);   // 2.0 plat/s: above the bronze line of every setting
                fast.Mode = mode;
                GameSession.Progress.RecordRun(fast);
                Assert.AreNotEqual(MedalLevel.None, GameSession.Progress.Medal(id, mode), id + ": the fast run earned a medal (medal targets are loaded)");
            }
            yield return Load("Meta");
            var meta = Object.FindFirstObjectByType<MetaController>();
            RegionDef r1 = GameSession.Repo.GetRegionByIndex(1);
            var levels = GameSession.Repo.LevelsOf(r1.Id);
            var problems = new List<string>();
            yield return CheckScreen("levels-special-badges", () => meta.ShowLevels(r1), problems);

            // Tiles: a badge on exactly the six special levels, never next to a gem badge, labelled, a corner pip to the right of the level number and clear of the medal and
            // the stars (CUX-3, CUX-6). The gem badge of the gem levels follows the same corner rule.
            meta.ShowLevels(r1);
            yield return null;
            yield return null;   // the path is laid out one frame after the screen opens
            Canvas.ForceUpdateCanvases();
            Rect pathView = CanvasBox(FindRt("PathViewport"));
            int badges = 0, gemBadges = 0, medalBadges = 0;
            foreach (LevelDef level in levels)
            {
                if (level.Index > 24) continue;
                RectTransform node = FindRt("Level" + level.Index);
                Assert.IsNotNull(node, "level node " + level.Index);
                Transform badge = node.Find(MetaController.SpecialBadgeName);
                SpecialLevelType type = level.SpecialType;
                Transform medalPip = node.Find(MetaController.MedalBadgeName);
                if (medalPip != null)
                {
                    // the mirrored corner pip (MetaController.CornerPipPositionLeft): left of the number's own box, inside the height of the tile, inside the scroll view
                    medalBadges++;
                    Rect tile = CanvasBox(node), mb = CanvasBox((RectTransform)medalPip);
                    Text num = node.Find("Num") != null ? node.Find("Num").GetComponent<Text>() : null;
                    if (num != null)
                    {
                        float numberLeft = tile.center.x - num.preferredWidth * node.localScale.x * 0.5f;
                        Assert.LessOrEqual(mb.xMax, numberLeft + 1f, "level " + level.Index + ": the medal ends left of the level number (number starts " + numberLeft.ToString("F0") + ", medal ends " + mb.xMax.ToString("F0") + ")");
                    }
                    Assert.IsTrue(mb.yMax <= tile.yMax + 1f && mb.yMin >= tile.yMin - 1f, "level " + level.Index + ": the medal stays inside the height of the tile (the stars of the next row are above it)");
                    Assert.IsTrue(mb.xMin >= pathView.xMin - 1f && mb.xMax <= pathView.xMax + 1f, "level " + level.Index + ": the medal is inside the path view");
                }
                Transform pip = badge != null ? badge : node.Find(MetaController.GemBadgeName);
                if (pip != null)
                {
                    // the corner pip rule (MetaController.CornerPipPosition): right of the number's own box at this text size, inside the height of the tile, inside the scroll view
                    Rect tile = CanvasBox(node), pb = CanvasBox((RectTransform)pip);
                    Text num = node.Find("Num") != null ? node.Find("Num").GetComponent<Text>() : null;
                    if (num != null)
                    {
                        float numberRight = tile.center.x + num.preferredWidth * node.localScale.x * 0.5f;
                        Assert.GreaterOrEqual(pb.xMin, numberRight - 1f, "level " + level.Index + ": the " + pip.name + " starts right of the level number (number ends " + numberRight.ToString("F0") + ", badge starts " + pb.xMin.ToString("F0") + ")");
                    }
                    Assert.IsTrue(pb.yMax <= tile.yMax + 1f && pb.yMin >= tile.yMin - 1f, "level " + level.Index + ": the " + pip.name + " stays inside the height of the tile (the stars of the next row are above it)");
                    Assert.IsTrue(pb.xMin >= pathView.xMin - 1f && pb.xMax <= pathView.xMax + 1f, "level " + level.Index + ": the " + pip.name + " is inside the path view");
                    if (badge == null) gemBadges++;
                }
                if (type == SpecialLevelType.None) { Assert.IsNull(badge, "level " + level.Index + " is a Regular Run: no special badge"); continue; }
                badges++;
                Assert.IsNotNull(badge, "level " + level.Index + " is a " + SpecialLevels.Name(type) + " level: badge on the tile");
                Assert.IsNull(node.Find(MetaController.GemBadgeName), "a special level is never a gem level");
                string spoken = badge.GetComponent<A11yLabel>().Text;
                StringAssert.Contains(SpecialLevels.Name(type), spoken);
                StringAssert.Contains(SpecialLevels.RuleText(type), spoken);
                StringAssert.Contains(SpecialLevels.Name(type), node.GetComponent<A11yLabel>().Text, "the tile itself names the type too");
                Assert.AreEqual(SpecialLevels.Glyph(type), badge.Find("Glyph").GetComponent<Text>().text, "the type's own glyph (never colour alone)");
                Rect bb = CanvasBox((RectTransform)badge);
                Transform medal = node.Find(MetaController.MedalBadgeName);
                if (medal != null) Assert.IsFalse(CanvasBox((RectTransform)medal).Overlaps(bb), "level " + level.Index + ": badge over the medal");
                for (int s = 0; s < 3; s++)
                {
                    Transform star = node.Find("S" + s);
                    if (star != null) Assert.IsFalse(CanvasBox((RectTransform)star).Overlaps(bb), "level " + level.Index + ": badge over star " + s);
                }
            }
            Assert.AreEqual(SpecialLevels.Count, badges, "one tile per special type in the region");
            Assert.AreEqual(medalLevels.Length, medalBadges, "the two-digit tiles 16 and 20 show their medal");
            if (GameSession.Art != null && GameSession.Art.GemIcon != null) Assert.AreEqual(4, gemBadges, "the open gem levels 5, 10, 15 and 20 carry the gem badge");

            // Popups: the type name is the second line of the title, its one-sentence rule sits between the goal and the stars; nothing overlaps on any aspect.
            foreach (LevelDef level in levels)
            {
                SpecialLevelType type = level.SpecialType;
                if (type == SpecialLevelType.None) continue;
                LevelDef l = level;
                yield return CheckScreen("level-popup-special-" + type, () => { meta.ShowLevels(r1); meta.OnLevelTapped(l); }, problems);
                RectTransform panel = FindRt("Popup").Find("Panel") as RectTransform;
                Text titleText = panel.Find("Title").GetComponent<Text>(), ruleText = panel.Find("Rule").GetComponent<Text>();
                Assert.AreEqual("Level " + l.Index + "\n" + SpecialLevels.Name(type), titleText.text, "the title names the level and its special type");
                Assert.AreEqual(MetaController.SpecialPopupTitleFont, titleText.fontSize, "the two-line title keeps one fixed size at every text scale");
                Assert.GreaterOrEqual(titleText.fontSize, UiKit.MinBodyTextPx - 1f);
                LevelSpec spec = LevelSpecFactory.Build(l, r1);
                Assert.AreEqual(RunSession.GoalRule(spec), ruleText.text);
                if (type != SpecialLevelType.ComboClimb) Assert.AreEqual(SpecialLevels.RuleText(type), ruleText.text);
                Assert.AreEqual(RunSession.DescribeGoal(spec), panel.Find("Goal").GetComponent<Text>().text);
                Canvas.ForceUpdateCanvases();
                // stacked top to bottom with real gaps: the boxes of title, goal, rule and stars do not intersect, and each text fits the height of its own box
                Rect titleBox = CanvasBox(titleText.rectTransform), goalBox = CanvasBox((RectTransform)panel.Find("Goal")), ruleBox = CanvasBox(ruleText.rectTransform), starBox = CanvasBox((RectTransform)panel.Find("Star0"));
                Assert.IsTrue(titleBox.yMin >= goalBox.yMax - 1f, type + ": the goal line starts below the title box");
                Assert.IsTrue(goalBox.yMin >= ruleBox.yMax + 1f, type + ": the rule starts below the goal line");
                Assert.IsTrue(ruleBox.yMin >= starBox.yMax + 1f, type + ": the stars start below the rule");
                Assert.LessOrEqual(titleText.preferredHeight, titleBox.height + 1f, type + ": the two title lines fit the title box");
                Assert.LessOrEqual(panel.Find("Goal").GetComponent<Text>().preferredHeight, goalBox.height + 1f, type + ": the goal fits its line (one line at this text size)");
                Assert.LessOrEqual(ruleText.preferredHeight, ruleBox.height + 6f, type + ": the rule fits its two-line box");
            }
            meta.ShowLevels(r1);
            meta.OnLevelTapped(levels[0]);
            yield return null;
            RectTransform plain = FindRt("Popup").Find("Panel") as RectTransform;
            Assert.AreEqual("Level " + levels[0].Index, plain.Find("Title").GetComponent<Text>().text, "a Regular Run keeps the plain title");
            Assert.IsNull(plain.Find("Rule"), "a Regular Run has no rule line");
            Report(problems);
        }

        /// <summary>
        /// Puts the hero of a running session onto a plain platform a few rows up, as if he had just landed there after his first jump (test helper for the Keep Moving timer:
        /// a plain platform cannot throw, drop or carry him, and the danger line is a full kill distance below, so only the timer can end the run in the next 3 seconds).
        /// </summary>
        static int StandOnAPlainPlatform(RunSession s)
        {
            Platform[] ps = s.Data.Platforms;
            for (int i = 1; i < ps.Length; i++)
            {
                if (ps[i].Kind != PlatformKind.Normal || ps[i].Row < 3) continue;
                s.State.X = ps[i].X; s.State.Y = ps[i].Y; s.State.Vx = Fx.Zero; s.State.Vy = Fx.Zero;
                s.State.Grounded = true; s.State.GroundId = i; s.State.BestGroundId = i; s.State.Row = ps[i].Row;
                s.State.MaxY = ps[i].Y; s.State.KillY = ps[i].Y - s.Cfg.KillDistance;
                s.State.StartTick = s.State.Tick; s.State.StandTicks = 0;
                return i;
            }
            return -1;
        }

        static bool HasReviveButton(RectTransform overlay)
        {
            foreach (Button b in overlay.GetComponentsInChildren<Button>(false))
                if (b.name.IndexOf("revive", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static IEnumerator FallUntilFailedPopup(RunController rc)
        {
            rc.Session.State.Grounded = false;
            rc.Session.State.Y = Fx.FromInt(-200);   // far below the danger line
            for (int f = 0; f < 120 && !rc.Hud.OverlayOpen; f++) yield return null;
            Assert.IsTrue(rc.Hud.OverlayOpen, "the failed popup opens");
        }

        [UnityTest]
        public IEnumerator OneLifeLevelOffersNoReviveAndKeepsTheOneTapRetry() { return OneLifeAudit(100, false); }

        [UnityTest]
        public IEnumerator OneLifeLevelSurvives130PercentTextAndLeftHandedLayout() { return OneLifeAudit(130, true); }

        IEnumerator OneLifeAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;
            GameSession.Progress.AddGems(500);   // gems are not the reason: the level has no revive at all
            GrantAllPowerUps(2);                 // the player owns every power-up, the Safety Net included
            GameSession.CurrentLevelId = "lvl_001_09";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            Assert.AreEqual(SpecialLevelType.OneLife, rc.Session.SpecialType);
            var problems = new List<string>();
            yield return CheckScreen("run-intro-one-life", () => { }, problems);
            RectTransform intro = FindRt("Intro");
            Assert.AreEqual("One Life", intro.Find("Special").GetComponent<Text>().text, "the intro names the level type");
            Assert.AreEqual("No revive, no Safety Net: one fall ends the run.", intro.Find("Rule").GetComponent<Text>().text);
            Assert.AreEqual(SpecialLevels.RuleText(SpecialLevelType.OneLife), intro.Find("Rule").GetComponent<Text>().text);
            // Owner 2026-11-22: the Safety Net is off on a One Life level: no button for it (a button that does nothing is never shown), every other owned power-up keeps its button.
            Assert.AreEqual(PowerUps.Implemented.Length - 1, rc.Hud.Powers.ButtonCount, "every owned type but the Safety Net");
            foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.AreNotEqual("Power" + PowerUp.SafetyNet, rt.name, "no Safety Net button on a One Life level");
            rc.Hud.SkipIntro();
            yield return null;
            rc.Input.LatchPower(PowerUp.SafetyNet);   // a stray activation reaches nothing
            rc.Input.LatchPower(PowerUp.Shield);      // the other power-ups work
            for (int f = 0; f < 10; f++) yield return null;
            Assert.IsFalse(rc.Session.State.SafetyNetArmed, "the Safety Net cannot be armed");
            Assert.Greater(rc.Session.State.ShieldTicks, 0, "the Shield works on a One Life level");
            Assert.AreEqual(2, rc.Session.PowerStock(PowerUp.SafetyNet), "no Safety Net unit was taken");
            yield return CheckScreen("run-one-life-hud", () => { }, problems);
            int gemsBefore = GameSession.Progress.Gems;
            yield return FallUntilFailedPopup(rc);
            RectTransform overlay = FindRt("Overlay");
            Assert.IsFalse(rc.Session.CanRevive);
            Assert.IsFalse(HasReviveButton(overlay), "One Life: no ad revive and no gem revive button");
            Assert.IsNotNull(FindRt("Try again", overlay), "Retry stays one tap away (CUX-7)");
            Assert.AreEqual("One Life: no revive and no Safety Net.", FindRt("Reason", overlay).GetComponent<Text>().text);
            Assert.AreEqual(0, rc.Session.State.SafetyNetCatches, "one fall ended the run: no rescue");
            Assert.AreEqual(gemsBefore, GameSession.Progress.Gems);
            yield return CheckScreen("run-one-life-failed", () => rc.Hud.ShowFailed(rc.Session, 500, true), problems);
            Assert.IsFalse(HasReviveButton(FindRt("Overlay")), "also with an ad ready");
            rc.Hud.CloseOverlay();

            // a plain level right after it does offer both revives again
            GameSession.CurrentLevelId = "lvl_001_08";
            yield return Load("Run");
            rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            yield return null;
            yield return FallUntilFailedPopup(rc);
            Assert.IsTrue(HasReviveButton(FindRt("Overlay")), "a Regular Run keeps its revive");
            Assert.IsNull(FindRt("Reason", FindRt("Overlay")), "no reason line on a plain fall");
            Report(problems);
        }

        [UnityTest]
        public IEnumerator NoPowerUpsLevelShowsNoPowerButtonsAndUsesNoUnit() { return NoPowerUpsAudit(100, false); }

        [UnityTest]
        public IEnumerator NoPowerUpsLevelSurvives130PercentTextAndLeftHandedLayout() { return NoPowerUpsAudit(130, true); }

        IEnumerator NoPowerUpsAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;
            GrantAllPowerUps(3);
            RunController.HeroOverride = LegendaryTestHero();   // a hero WITH free powers: they are off here too
            GameSession.CurrentLevelId = "lvl_001_14";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            Assert.AreEqual(SpecialLevelType.NoPowerUps, rc.Session.SpecialType);
            Assert.IsTrue(rc.Session.PowersOff);
            Assert.IsNull(rc.Session.Hero, "the hero's own power is off on this level");
            Assert.AreEqual(0, rc.Hud.Powers.ButtonCount, "no power button at all: neither bought nor hero");
            var problems = new List<string>();
            yield return CheckScreen("run-intro-no-powerups", () => { }, problems);
            RectTransform intro = FindRt("Intro");
            Assert.AreEqual("No Power-ups", intro.Find("Special").GetComponent<Text>().text);
            Assert.AreEqual(SpecialLevels.RuleText(SpecialLevelType.NoPowerUps), intro.Find("Rule").GetComponent<Text>().text);
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => SimInput.Right;
            rc.Input.ScriptedJump = () => frame % 45 == 0;
            for (frame = 1; frame <= 40; frame++) yield return null;
            // even a latched activation (a stray key, an automation script) reaches nothing
            foreach (PowerUp t in PowerUps.Implemented) rc.Input.LatchPower(t);
            for (frame = 41; frame <= 80; frame++) yield return null;
            Assert.AreEqual(0, rc.Session.State.PwUsed, "no activation reaches the simulation");
            // a power-up bought in the shop during the run does not bring a button either
            GameSession.Progress.GrantPowerUp("pw_flight", 2);
            for (frame = 81; frame <= 100; frame++) yield return null;
            Assert.AreEqual(0, rc.Hud.Powers.ButtonCount);
            foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.IsFalse(rt.name.StartsWith("Power") && rt.GetComponent<Button>() != null, "a power button exists: " + rt.name);
            yield return CheckScreen("run-no-powerups-hud", () => { }, problems);
            rc.Session.SettlePowerUps(GameSession.Progress);
            Assert.AreEqual(5, GameSession.Progress.PowerUpCount("pw_flight"), "no unit was used (3 granted + 2 bought)");
            Assert.AreEqual(3, GameSession.Progress.PowerUpCount("pw_shield"));
            Report(problems);
        }

        [UnityTest]
        public IEnumerator KeepMovingLevelShowsTheRingAndTheMoveCueForTheLastTwoSeconds() { return KeepMovingAudit(100, false); }

        [UnityTest]
        public IEnumerator KeepMovingLevelSurvives130PercentTextLeftHandedAndReducedMotion() { return KeepMovingAudit(130, true); }

        IEnumerator KeepMovingAudit(int textPercent, bool leftHanded)
        {
            GameSettings.TextPercent = textPercent;
            GameSettings.LeftHanded = leftHanded;
            GameSettings.ReducedMotion = leftHanded;
            GameSession.Progress.AddGems(500);
            GameSession.Progress.GrantPowerUp("pw_safety_net", 1);
            GameSession.CurrentLevelId = "lvl_001_16";
            yield return Load("Run");
            var rc = Object.FindFirstObjectByType<RunController>();
            Assert.AreEqual(SpecialLevelType.KeepMoving, rc.Session.SpecialType);
            var problems = new List<string>();
            yield return CheckScreen("run-intro-keep-moving", () => { }, problems);
            Assert.AreEqual("Keep Moving", FindRt("Intro").Find("Special").GetComponent<Text>().text);
            Assert.AreEqual("Over 3 seconds on one platform ends the run.", FindRt("Intro").Find("Rule").GetComponent<Text>().text);
            rc.Hud.SkipIntro();
            // Owner 2026-11-22: the Safety Net stays available here (it rescues real falls) but never rescues the timeout.
            Assert.AreEqual(1, rc.Hud.Powers.ButtonCount, "the owned Safety Net has its button on a Keep Moving level");
            yield return null;
            rc.Input.LatchPower(PowerUp.SafetyNet);
            for (int f = 0; f < 5; f++) yield return null;
            Assert.IsTrue(rc.Session.State.SafetyNetArmed, "the net is armed");
            RectTransform banner = null;
            foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (rt.name == RunHud.KeepMovingBannerName) banner = rt;
            Assert.IsNotNull(banner, "the cue banner is built once");
            Assert.IsFalse(banner.gameObject.activeSelf, "hidden until the warning");

            // waiting on the start platform never starts the timer
            for (int f = 0; f < 240; f++) yield return null;
            Assert.IsFalse(rc.Session.Over, "no timer before the first jump");
            Assert.IsFalse(rc.View.KeepMovingRingShown);
            Assert.IsFalse(banner.gameObject.activeSelf);

            // standing on a platform after the first jump: the ring and the cue appear for the last 2 seconds only
            int frame = 0;
            Assert.Greater(StandOnAPlainPlatform(rc.Session), 0);
            int warnFrames = 0, firstLeft = -1;
            float firstDiameter = 0f, lastDiameter = 0f;
            for (frame = 1; frame <= 400 && !rc.Session.Over; frame++)
            {
                yield return null;
                SimState st = rc.Session.State;
                bool warn = rc.Session.Sim.KeepMovingWarning(in st) && !rc.Session.Over;
                if (!rc.Session.Over) Assert.AreEqual(warn, rc.View.KeepMovingRingShown, "ring follows the warning at frame " + frame);
                if (!warn) continue;
                if (warnFrames == 0) { firstLeft = rc.Session.Sim.KeepMovingTicksLeft(in st); firstDiameter = rc.View.KeepMovingRingDiameter; }
                warnFrames++;
                lastDiameter = rc.View.KeepMovingRingDiameter;
                Assert.IsTrue(banner.gameObject.activeSelf, "the text cue shows with the ring (never the ring alone)");
                StringAssert.StartsWith("MOVE!", banner.GetComponentInChildren<Text>().text);
                if (warnFrames == 30)
                {
                    rc.enabled = false;   // freeze the run for the layout audit
                    yield return CheckScreen("run-keep-moving-warning", () => { }, problems);
                    rc.enabled = true;
                }
            }
            Assert.IsTrue(firstLeft > 100 && firstLeft <= rc.Session.Cfg.KeepMovingWarnTicks, "the warning starts 2 seconds before the end (ticks left " + firstLeft + ")");
            Assert.Greater(warnFrames, 60);
            if (GameSettings.ReducedMotion)
            {
                Assert.AreEqual(RunView.KeepMovingRingStill, firstDiameter, 0.01f, "reduced motion: a still ring");
                Assert.AreEqual(RunView.KeepMovingRingStill, lastDiameter, 0.01f);
            }
            else
            {
                Assert.Greater(firstDiameter, lastDiameter + 0.5f, "the ring closes in on the hero as the time runs out");
                Assert.LessOrEqual(firstDiameter, RunView.KeepMovingRingMax + 0.01f);
                Assert.GreaterOrEqual(lastDiameter, RunView.KeepMovingRingMin - 0.01f);
            }
            Assert.IsTrue(rc.Session.State.Dead, "more than 3 seconds on one platform ends the run, also with an armed Safety Net");
            Assert.IsTrue(rc.Session.KeepMovingTimedOut);
            Assert.AreEqual(0, rc.Session.State.SafetyNetCatches, "the Safety Net does not rescue the timeout");
            for (int f = 0; f < 60 && !rc.Hud.OverlayOpen; f++) yield return null;
            RectTransform overlay = FindRt("Overlay");
            Assert.IsNotNull(overlay);
            Assert.IsTrue(HasReviveButton(overlay), "Keep Moving allows a revive");
            Assert.AreEqual(rc.Session.FailReason, FindRt("Reason", overlay).GetComponent<Text>().text);
            Assert.IsFalse(rc.View.KeepMovingRingShown, "no ring once the run is over");
            yield return CheckScreen("run-keep-moving-failed", () => rc.Hud.ShowFailed(rc.Session, 500, true), problems);
            Report(problems);
        }
    }

    /// <summary>CUX-1: the run loop stays inside its frame budget and does not allocate per frame, on the hardest content.</summary>
    [Category("CriticalUX")]
    public class CriticalUxPerformanceTests
    {
        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            GameSession.Reset();
            GameSession.InitForTests(new MemorySaveStore());
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            foreach (var rc in Object.FindObjectsByType<RunController>(FindObjectsSortMode.None)) rc.enabled = false;
            GameSession.Reset();
        }

        [UnityTest]
        public IEnumerator HardestLevelStaysInTheFrameBudgetWithoutAllocations()
        {
            GameSession.CurrentLevelId = "lvl_005_15";
            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            yield return null;
            var rc = Object.FindFirstObjectByType<RunController>();
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => (frame / 70) % 2 == 0 ? SimInput.Right : SimInput.Left;
            rc.Input.ScriptedJump = () => frame % 30 == 0;
            for (frame = 1; frame <= 120; frame++) yield return null;     // warm-up (JIT, first-use caches)

            // The editor runs on a shared PC: a single OS hitch can add a few ms to one frame. A real regression is slow in EVERY window, so the
            // best of up to three 600-frame windows is judged (average and allocations are judged on that same window).
            double bestMax = double.MaxValue, bestAvg = 0.0, bestAlloc = 0.0;
            const int measured = 600;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                rc.Stats.Reset();
                System.GC.Collect();
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int k = 0; k < measured; k++) { frame++; yield return null; }
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
                Debug.Log("[CUX-PERF] window " + attempt + " avg " + rc.Stats.AverageMs.ToString("F3") + " ms, max " + rc.Stats.MaxMs.ToString("F3") + " ms, alloc " + (allocated / (double)measured).ToString("F1") + " B/frame");
                if (rc.Stats.MaxMs < bestMax) { bestMax = rc.Stats.MaxMs; bestAvg = rc.Stats.AverageMs; bestAlloc = allocated / (double)measured; }
                if (bestMax <= 6.0) break;
            }
            Assert.LessOrEqual(bestAvg, 2.0, "average script time per frame (CUX-1)");
            Assert.LessOrEqual(bestMax, 6.0, "worst script time per frame (CUX-1), best of 3 windows");
            Assert.LessOrEqual(bestAlloc, AllocBudget, "managed allocation per frame (CUX-1)");
            Assert.IsFalse(rc.Paused, "the run was not interrupted");
        }

        /// <summary>CUX-1: the timer, live delta and Pace Ghost of the timed HUD do not allocate per frame (strings are cached, they change only with the shown tenth or floor).</summary>
        [UnityTest]
        public IEnumerator TimedHudWithGhostAndDeltaDoesNotAllocate()
        {
            GameSettings.PaceGhost = true;
            var region = GameSession.Repo.GetRegionByIndex(1);
            GameSession.Progress.RecordRun(new RunRecord { Kind = RunKind.Level, LevelId = "lvl_001_01", RegionId = region.Id, Floors = 30, Ticks = 1800, Mode = 0, HeroId = "momo", GameVersion = "test", Curve = new[] { 4, 9, 14, 19, 24, 29 } });
            GameSession.CurrentLevelId = "lvl_001_01";
            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            yield return null;
            var rc = Object.FindFirstObjectByType<RunController>();
            Assert.IsTrue(rc.Hud.GhostActive);
            rc.Hud.SkipIntro();
            int frame = 0;
            rc.Input.ScriptedMove = () => (frame / 70) % 2 == 0 ? SimInput.Right : SimInput.Left;
            rc.Input.ScriptedJump = () => frame % 30 == 0;
            for (frame = 1; frame <= 120; frame++) yield return null;
            string first = rc.Hud.TimerText.text;
            System.GC.Collect();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            const int measured = 400;
            for (int k = 0; k < measured; k++) { frame++; yield return null; }
            double perFrame = (System.GC.GetAllocatedBytesForCurrentThread() - before) / (double)measured;
            Assert.AreNotEqual(first, rc.Hud.TimerText.text, "the timer ran");
            Assert.LessOrEqual(perFrame, AllocBudget, "managed allocation per frame with the timed HUD (CUX-1)");
            PlayerPrefs.DeleteAll();
        }

        /// <summary>
        /// CUX-1 (owner Final Change 1, 2026-11-22): the Keep Moving countdown (ring on the hero, "MOVE!" cue) costs no per-frame allocation while it counts down. The hero stands on a
        /// plain platform as after a landing; the 100 measured frames lie inside the 2 second warning (the cue text changes once in them, from a cached string).
        /// </summary>
        [UnityTest]
        public IEnumerator KeepMovingCountdownDoesNotAllocate()
        {
            GameSession.CurrentLevelId = "lvl_001_16";
            var op = SceneManager.LoadSceneAsync("Run");
            while (!op.isDone) yield return null;
            yield return null;
            var rc = Object.FindFirstObjectByType<RunController>();
            Assert.IsTrue(rc.Session.Sim.KeepMoving);
            rc.Hud.SkipIntro();
            for (int f = 0; f < 60; f++) yield return null;
            RunSession s = rc.Session;
            Platform[] ps = s.Data.Platforms;
            int on = -1;
            for (int i = 1; i < ps.Length && on < 0; i++) if (ps[i].Kind == PlatformKind.Normal && ps[i].Row >= 3) on = i;
            Assert.Greater(on, 0);
            s.State.X = ps[on].X; s.State.Y = ps[on].Y; s.State.Vx = Fx.Zero; s.State.Vy = Fx.Zero;
            s.State.Grounded = true; s.State.GroundId = on; s.State.BestGroundId = on; s.State.Row = ps[on].Row;
            s.State.MaxY = ps[on].Y; s.State.KillY = ps[on].Y - s.Cfg.KillDistance;
            s.State.StartTick = s.State.Tick; s.State.StandTicks = 0;
            for (int f = 0; f < 70; f++) yield return null;   // into the warning: the ring and the cue are on screen
            Assert.IsTrue(rc.View.KeepMovingRingShown, "the countdown ring is drawn");
            System.GC.Collect();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            const int measured = 100;
            for (int k = 0; k < measured; k++) yield return null;
            double perFrame = (System.GC.GetAllocatedBytesForCurrentThread() - before) / (double)measured;
            Assert.IsFalse(s.Over, "still inside the 3 seconds");
            Assert.IsTrue(rc.View.KeepMovingRingShown);
            Assert.LessOrEqual(perFrame, AllocBudget, "managed allocation per frame with the Keep Moving countdown (CUX-1)");
        }

        const double AllocBudget = 1024.0;
    }
}
