using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// First-time help in the Meta scene (owner 2026-11-25; text in <see cref="HelpTable"/>, rules in <see cref="HelpState"/>, popup in <see cref="HelpPopup"/>). While the Help switch is on, every screen
    /// (and every tab of the Shop and the Rewards) shows its help ONCE the next time it is entered. The host polls four times a second (no per-frame work): it notices that the screen or tab changed, waits
    /// a moment until it stays, and shows the help only when nothing else is open (no pop-up, no overlay, no login or consent card) and the topic was not shown before. The Meta scene never runs a
    /// game, so help can never interrupt a run; the Gameplay topic is shown before the first run starts (<see cref="LoadRunScene"/>). Marks are written when a help is shown, so a kill never repeats it.
    /// </summary>
    public sealed partial class MetaController
    {
        HelpPopup helpPopup;
        string helpScreenSeen;
        int helpSubSeen = -1;
        bool helpArmed;
        float helpArmedAt, helpNextPoll;
        const float HelpPollSeconds = 0.25f, HelpSettleSeconds = 0.4f;

        /// <summary>The help popup while it is showing (null otherwise). Tests drive it through this.</summary>
        public HelpPopup CurrentHelp { get { return helpPopup != null && !helpPopup.IsClosed ? helpPopup : null; } }

        /// <summary>The tab of the current screen as the help table counts it: Shop 0 Bundles, 1 VIP, 2 Character, 3 Gems; Rewards 0 Main, 1 Daily, 2 Weekly; 0 elsewhere.</summary>
        int HelpSub()
        {
            if (CurrentScreen == "Shop") return shop != null ? shop.Tab : 0;
            if (CurrentScreen == "Rewards") return rewards != null ? (int)rewards.Tab : 0;
            return 0;
        }

        /// <summary>True while something else sits on the screen: a pop-up, an overlay, the login or consent card.</summary>
        bool HelpBlocked()
        {
            if (popup != null) return true;
            Transform ct = canvas.transform;
            for (int i = 0; i < ct.childCount; i++)
            {
                Transform c = ct.GetChild(i);
                if ((screenRoot != null && c == screenRoot) || (helpPopup != null && helpPopup.Root != null && c == helpPopup.Root)) continue;
                if (c.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        /// <summary>Called from Update: notices a screen or tab change and shows its help when the rules allow.</summary>
        void UpdateHelp()
        {
            if (Time.unscaledTime < helpNextPoll) return;
            helpNextPoll = Time.unscaledTime + HelpPollSeconds;
            string scr = CurrentScreen;
            int sub = HelpSub();
            if (!GameSettings.Help)
            {
                helpScreenSeen = scr;   // the screen you are on when you switch it on does not count as "entered"
                helpSubSeen = sub;
                helpArmed = false;
                return;
            }
            if (CurrentHelp != null) return;
            if (scr != helpScreenSeen || sub != helpSubSeen)
            {
                helpScreenSeen = scr;
                helpSubSeen = sub;
                helpArmed = true;
                helpArmedAt = Time.unscaledTime;
            }
            if (!helpArmed || Time.unscaledTime - helpArmedAt < HelpSettleSeconds) return;
            List<string> unseen = HelpState.Unseen(progress, HelpTable.IdsFor(scr, sub));
            if (unseen.Count == 0) { helpArmed = false; return; }
            if (HelpBlocked()) return;   // try again when the pop-up is gone (as long as we are still on this screen)
            helpArmed = false;
            ShowHelpFor(unseen, true, null);
        }

        /// <summary>Shows the help of the given topics as one carousel. <paramref name="markSeen"/> stores the marks (and may turn the switch off); tests pass false.</summary>
        HelpPopup ShowHelpFor(IList<string> ids, bool markSeen, Action after)
        {
            CloseHelp();
            List<HelpPage> pages = HelpState.PagesOf(ids);
            if (pages.Count == 0) { if (after != null) after(); return null; }
            if (markSeen) HelpState.MarkSeen(progress, ids);
            helpPopup = HelpPopup.Show(canvas.transform, art, pages, () =>
            {
                helpPopup = null;
                if (after != null) after();
            });
            return helpPopup;
        }

        /// <summary>Tests: shows the help of these topic ids now, without touching the seen marks.</summary>
        public HelpPopup ShowHelpForTests(params string[] ids) { return ShowHelpFor(ids, false, null); }

        /// <summary>Removes the help popup without a callback (a screen change).</summary>
        void CloseHelp()
        {
            if (helpPopup == null) return;
            HelpPopup h = helpPopup;
            helpPopup = null;
            if (h != null && h.Root != null) Destroy(h.Root.gameObject);
        }

        /// <summary>Starts the Run scene. When the Help switch is on and the Gameplay help was not shown yet, it is shown first and the run starts when the player taps Got it (or the X).</summary>
        void LoadRunScene()
        {
            if (HelpState.ShouldShow(progress, HelpTable.Gameplay))
            {
                ClosePopup();
                ShowHelpFor(new[] { HelpTable.Gameplay }, true, () => SceneManager.LoadScene("Run"));
                return;
            }
            SceneManager.LoadScene("Run");
        }

        /// <summary>Settings: the Help switch, a compact cell (small icon, narrower switch) so it fits a third of the row.</summary>
        void AddSettingsHelpSwitch(RectTransform c, float x, float y, float w, float rowH)
        {
            SettingsSwitchAt(c, "Help", "lb_icon_info", x, y, w, rowH, () => GameSettings.Help, v => GameSettings.Help = v, 56f, 170f, 8f);
        }

        /// <summary>Settings: the "Show help again" button (a third of the row).</summary>
        void AddSettingsHelpAgain(RectTransform c, float x, float y, float w, float rowH)
        {
            SettingsWordButton(c, "ShowHelpAgain", "Help again", 0, "Show help again", x, y, w, rowH, () =>
            {
                HelpState.ShowAgain(progress);
                ShowSettings();   // rebuild so the Help switch shows on
            });
        }
    }
}
