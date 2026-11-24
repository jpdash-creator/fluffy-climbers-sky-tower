# Player data deletion runbook (Final Change 3, 2026-11-23)

Who this is for: the owner, when a player asks by e-mail to have their data deleted, and the in-app path the player can use alone. Keep it in step with POLICY.md sections 3, 4 and 4a.

## 1. What data exists and where

| Data | Where it lives | Who can delete it |
|---|---|---|
| Game save (progress, wallet, heroes, power-ups, settings, chosen hero, purchase ledger, leaderboard queue) | The device, in the app private folder (save file plus `.bak` and `.tmp`) | The player: Reset progress, or uninstall. An Android device backup (a Google service) may hold a copy; the player controls it in Android settings. |
| Cloud save | Google Play Games Saved Games, file `fluffy_save`, in the player's own Google account (hidden Drive app data). We have no server copy and no access. | The player only (see section 4). |
| Leaderboard data (only players who confirmed the weekly leaderboards) | Firebase project `fluffy-climbers-tower-jump`, Firestore Mumbai (asia-south1): `players/{uid}` (display name, picture link, public id, hero variants), `weeks/{weekKey}/slots/{uid}_{slot}`, `weeks/{weekKey}/totals/{uid}`, `weeks/{weekKey}/breakdowns/{uid}`, `weeks/{weekKey}/boards/{board}` (rebuilt boards that name the player), `prizeClaims/{id}`, `tickets/{ticketId}` (one-time run tickets with the uid, short lived) | The app (Reset progress calls the service) for slots, totals and breakdowns; the owner for the rest. |
| Firebase Authentication user | Firebase console > Authentication, one user per signed-in player (the uid used above) | The owner. |
| Cloud Run request logs | Google Cloud Logging (IP address, uid) | They expire by the log retention setting; the owner can delete entries in Logs Explorer. |

## 2. What the player can do in the app

1. **Settings > Reset progress > confirm.** The game wipes the device save except the shop purchase ledger and the purchase records, sets `cloudResetPending`, and (when signed in) uploads the empty save to Play Games Saved Games at once. If that upload fails the flag stays, and the next sign-in uploads the reset save BEFORE any cloud download is applied (`AccountSync.Resolve`), so an old cloud copy can never bring the data back. The chosen hero goes back to Momo copy 0; the player's settings are kept on purpose (they are preferences, not progress).
2. The same reset sets `leaderboardWipePending` (when the player ever confirmed the leaderboards). `LeaderboardHub` then calls `POST /delete-my-entries` (`LeaderboardClient.DeleteMyEntries`) now, at the next sign-in and on every slow tick until the service answers 2xx; nothing new is uploaded while it is pending. The service removes slots, weekly totals and breakdowns of every week and rebuilds the open boards without the player. It keeps the profile row, the prize claims and boards of weeks that are already final (they expire by TTL).
3. **Unlink Google account** (Settings) stops uploads; the cloud copy stays until deleted as in section 4.
4. Uninstalling removes the device files only.

## 3. What the owner does on an e-mail request (jpdash.creator@gmail.com)

Answer within one month (POLICY section 4a). Ask for the Play Games player name (the e-mail address is only used to reply). Then:

1. **Find the uid.** Firebase console > Firestore > `players`: search the name field for the Play Games name; the document id is the uid. Note it.
2. **Delete the leaderboard rows.** Firestore > `weeks` > for every week: delete the `slots` documents whose field `uid` equals the uid, the `totals/{uid}` document and the `breakdowns/{uid}` document. Then the `prizeClaims` documents with `uid` equal to the uid and the `tickets` documents with `uid` equal to the uid. Finally the `players/{uid}` document. (The player can do the slots, totals and breakdowns part alone with Reset progress; `players`, `prizeClaims` and final weeks still need the owner.)
3. **Boards.** The rebuilt `weeks/{weekKey}/boards/{board}` documents of open weeks keep the name until the next rebuild (the scheduled admin tick or the next score write). To remove it at once, open the document and delete the row with the uid.
4. **Delete the Authentication user.** Firebase console > Authentication > Users > the uid > Delete account.
5. **Logs.** Google Cloud console > Logging > Logs Explorer, query the uid. Delete entries only if the retention would exceed one month; otherwise the default retention applies.
6. **Reply** to the player with the date and what was deleted and what cannot be deleted by us (section 4).

This runbook needs no deploy and no code change.

## 4. What the owner cannot delete

- **The Play Games cloud save (`fluffy_save`).** It lives in the player's Google account and we have no access. Tell the player: use Reset progress while signed in (it replaces the file with an empty save that keeps only the purchase records), or delete it in Google Play Games settings > Delete Play Games data for this game, or in Google Drive > Settings > Manage apps > hidden app data.
- Android device backups, the player's Google account data, store purchase records at Google and AdMob data held by Google: the player uses Google's own tools.
- The purchase records the reset keeps on purpose (POLICY section 3). If the player wants them gone too, tell them that purchases can then not be restored.

## 5. When the save gains a new field

Update POLICY.md sections 3 and 4, this table and `docs/35-login-and-cloud-save.md` in the same change.
