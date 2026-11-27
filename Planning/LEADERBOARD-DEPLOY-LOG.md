# Leaderboard service deploy log (2026-11-12)

Owner approved: deploy now, public Cloud Run endpoint (service checks Firebase ID tokens itself), max 5 instances. The app feature flag stays OFF. Account jpdash.creator@gmail.com, project fluffy-climbers-tower-jump (763742589585), region asia-south1. Every gcloud call used `--project=fluffy-climbers-tower-jump --account=jpdash.creator@gmail.com` (shown as $A); no `gcloud config set`.

1. Local checks: `dotnet build Tools/leaderboard_service --artifacts-path <scratch>`; `FLUFFY_ROOT=E:\Code\fluffy-climbers-sky-tower dotnet test Tests/LeaderboardService --artifacts-path <scratch>`: 30 passed, 0 failed.
2. Build context staged in a scratch folder: `Tools/leaderboard_service` (no bin/obj), the `*.cs` of `Assets/_Game/{Simulation,Content,Economy}` (77 files, same relative paths), `Assets/Resources/Content/content_manifest.json`, and this Dockerfile (no secrets, keystores or google-services.json):
```
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Tools/leaderboard_service/LeaderboardService.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet","FluffyClimbers.LeaderboardService.dll"]
```
3. `gcloud services enable secretmanager.googleapis.com $A` (only new API; Cloud Run source deploy also created the Artifact Registry repo `cloud-run-source-deploy`).
4. Secret: 32 random bytes from python `secrets.token_urlsafe(32)` written to a temp file, then `gcloud secrets create lb-pid-salt --replication-policy=user-managed --locations=asia-south1 --data-file=<temp> $A`; temp file deleted. Value never printed.
5. Runtime identity:
```
gcloud iam service-accounts create fluffy-leaderboard-run --display-name="Fluffy leaderboard Cloud Run runtime" $A
gcloud projects add-iam-policy-binding fluffy-climbers-tower-jump --member=serviceAccount:fluffy-leaderboard-run@fluffy-climbers-tower-jump.iam.gserviceaccount.com --role=roles/datastore.user --condition=None $A
gcloud secrets add-iam-policy-binding lb-pid-salt --member=serviceAccount:fluffy-leaderboard-run@fluffy-climbers-tower-jump.iam.gserviceaccount.com --role=roles/secretmanager.secretAccessor $A
```
6. Deploy (from the staged folder):
```
gcloud run deploy fluffy-leaderboard --source . --region=asia-south1 --max-instances=5 --min-instances=0 --cpu=1 --memory=512Mi --timeout=60 --concurrency=40 --allow-unauthenticated --service-account=fluffy-leaderboard-run@fluffy-climbers-tower-jump.iam.gserviceaccount.com --set-env-vars=FIREBASE_PROJECT=fluffy-climbers-tower-jump --set-secrets=LB_PID_SALT=lb-pid-salt:latest $A --quiet
```
Result: revision `fluffy-leaderboard-00001-zhn`, 100 percent traffic, URL https://fluffy-leaderboard-763742589585.asia-south1.run.app
7. Smoke (curl, no Firestore writes): `GET /healthz/` 200 `{"ok":true,...}` (plain `/healthz` returns Google's 404 page: the run.app front end reserves it, use the trailing slash); `POST /start-run` no token 401; with a fake Bearer 401; `GET /my-week` 401; `POST /admin/tick` 401 (LB_ADMIN_KEY unset).

Not done: Cloud Scheduler tick (needs LB_ADMIN_KEY secret), `config/leaderboard` seeding (defaults come from config.default.json), real-token test, Firebase Auth Play Games provider, client work.
Redeploy after ANY simulation, generator, hero table or manifest change (same commit as the release).

## Pending owner steps (2026-11-22): Final Change 1. Nothing was deployed for it.
The release that carries Final Change 1 needs all of these, in this order, before the leaderboard is used with it (the closed-testing release AAB has the leaderboard ON):
1. **Redeploy** one new Cloud Run revision of `fluffy-leaderboard` from the release commit (same command as step 6). It carries: the new speed rule (steps every 30 s), the crown bonus 0 / 5 / 10 / 15 / 25 percent, the special levels (regenerated level files, golden hashes and content manifest), the top 3 rule for Endless and Time Attack slots (`SlotDoc.runs`, `SlotMath`), the new answers of `/submit-run` and `/my-week`, and rules version 2. It also carries the two pending revisions of 2026-11-17 below. No new env, secret, composite index or rule.
2. **Firestore `config/leaderboard`:** set `simVersions` to `[2]` (the defaults in `LeaderboardRules.cs` and `config.default.json` already say 2). Version 1 apps then get HTTP 426 (update the app).
3. **Old weekly data cannot be converted** (the crown formula inputs changed): delete the open week's `weeks/*` documents, or switch at a Monday 00:00 UTC.
4. **Re-measure the three entry limits** (Overall 250,000, Levels 100,000, Endless 150,000; ranks 1 to 3 need 1.5 x) with the new rules and write them to the code defaults, `config.default.json` and `config/leaderboard` before `leaderboard.enabled` is switched on for players.
5. Run `dotnet test Tests/LeaderboardService` on the release commit first (it includes `TopRunsTests`).
Details: `Planning/LEADERBOARD-DESIGN.md`, last section.

## Pending redeploy (2026-11-17): POST /delete-my-entries
New endpoint for Settings > Reset progress (see LEADERBOARD-DESIGN.md, last section). Needs a new Cloud Run revision of `fluffy-leaderboard` (same command as step 6). No Firestore rules, indexes, env or secret change. Until deployed the app keeps the wipe queued (`leaderboardWipePending`) and retries.

## Pending redeploy (2026-11-17): POST /sync-breakdown and breakdown deletion
New endpoint `POST /sync-breakdown`, `breakdown` field in `GET /my-week?slots=1`, and `/delete-my-entries` now also removes `weeks/*/breakdowns/{uid}`: one new Cloud Run revision of `fluffy-leaderboard` (same command as step 6; the shared `BreakdownMerge.cs` and `LeaderboardSave.cs` are linked from Assets/_Game/Economy). ALSO deploy the Firestore TTL policy: `firebase deploy --only firestore:indexes` (new fieldOverride `breakdowns.expireAt` ttl). No rules change, no composite index, no env or secret change. Same revision also carries the earlier `/delete-my-entries` change.

## Pending owner steps (2026-11-23): Final Change 2. Nothing was deployed for it.
Applies on top of the Final Change 1 steps above (do them once, from the release commit that carries both):
1. **Redeploy** the Cloud Run service from the release commit: the hero tables (`HeroTable.cs`, `HeroVariants.cs`, `HeroRolls.cs`), per-copy powers and the region penalty are compiled into it, and a replay needs the SAME tables the phone used (a variant index means another stat set after the hero rebuild).
2. **Firestore `config/leaderboard`:** set `simVersions` to `[3]` (code default, `config.default.json` and `LeaderboardConfig.SimVersion` already say 3). Version 2 apps get HTTP 426 and their queued runs are dropped by the phone (final answer).
3. Read `Planning/LEADERBOARD-REVIEW-2026-11-23.md` (findings and the entry-limit recommendation) before `leaderboard.enabled` is switched on.

## Rules version 4 (Final Changes 5 and 6, 2026-11-25), NOT deployed
`LeaderboardConfig.SimVersion`, `Limits.SimVersions` and `config.default.json` say 4 (19 new platforms, nine special level types, Moving platform fix, new level layouts and Rival Race ghost paths). Owner steps: redeploy `fluffy-leaderboard` from the release commit, set `simVersions` to `[4]` in Firestore `config/leaderboard`, re-measure the prize entry limits. Version 3 apps then get HTTP 426.

## Deployed 2026-11-27 (owner approved in chat): rules version 4, build v0.71.138
Revision `fluffy-leaderboard-00010-qbg`, 100 percent traffic (previous: `fluffy-leaderboard-00009-j8d`). Source commit e34e8bd, same staging and command as step 6. `dotnet test Tests/LeaderboardService`: 113 passed. Smoke: `/healthz/` content hash equals the shipped manifest (f8530c45...), unauthenticated calls answer 401, no error log lines. Covers the pending items above: rules version 4, `/delete-my-entries`, `/sync-breakdown`, profile upsert on submit, `/v1/save`.
No Firestore `config/leaderboard` document exists, so `simVersions` comes from the deployed defaults. Still open: `firebase deploy --only firestore:indexes` (TTL for breakdowns and saveBackups), publish the POLICY.md/TERMS.md wording, re-measure prize entry limits.

## Rules version 5 (Region 3 funnel removed), NOT deployed
Owner Final Bugs 2 (2026-11-27): the Undertow Pad is a plain platform (no water funnel, no slowing column). Platform layouts are unchanged; the replay physics differs and the Rival Race ghost of `lvl_003_13` was re-baked (level file and its golden hash changed; rebuild the content manifest, which the build does before every player build, before deploying so the service and the app hold the same content). Sky Hoops ring positions follow the physics: the shipped base layouts are identical, a few attempt-seed layouts of `lvl_003_08` / `_17` place rings differently. `LeaderboardConfig.SimVersion`, `Limits.SimVersions` and `config.default.json` say 5. Redeploy `fluffy-leaderboard` from the release commit (same command as step 6); there is no Firestore `config/leaderboard` document, so the deployed defaults apply. Version 4 apps (v0.71.138) then get HTTP 426.

## Deployed 2026-11-27 (owner approved in chat): rules 4 and 5
Revision `fluffy-leaderboard-00011-kx6`, 100 percent traffic (previous: `00010-qbg`). Working tree of Final Bugs 2 (not yet committed), content hash e8cda644..., 113 service tests passed. `simVersions` [4, 5]: phones on v0.71.138 keep counting (no APK was built; drop 4 with the next release). A v0.71.138 Region 3 run that used the old funnel, or level 3-13, may be refused as a mismatch.

## Deployed 2026-11-27 (owner approved, Final Bugs 2 fixes): commit 7a6413a
Revision `fluffy-leaderboard-00012-d4m`, 100 percent traffic (previous: `00011-kx6`). Content hash 650ca4fb... (lvl_001_15 new seed), `simVersions` [4, 5] kept, `/healthz/` ok. v0.71.138 runs on lvl_001_15 or the changed Region 3 physics may be refused as a mismatch.
