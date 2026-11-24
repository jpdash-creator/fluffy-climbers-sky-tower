# FluffyClimbers.Content (T2: runtime content loading)

Assembly `FluffyClimbers.Content` (references FluffyClimbers.Simulation + Unity.Nuget.Newtonsoft-Json, package `com.unity.nuget.newtonsoft-json` 3.2.2). Namespace `FluffyClimbers.Content`.

## Loading
- Offline fallback: `ContentRepository.LoadFromResources()` reads `Assets/Resources/Content/content_manifest.json` (TextAsset, path `Content/content_manifest`).
- Remote/other text: `ContentRepository.LoadFromJson(string manifestJson)` (same format, validated identically).
- Regenerate the manifest after any change under `Content/`: menu `Tools/Fluffy Climbers: Tower Jump/Build Content Manifest`, or headless
  `Unity.exe -batchmode -nographics -projectPath <repo> -executeMethod FluffyClimbers.Editor.ContentSync.ContentManifestBuilder.BuildFromCommandLine -logFile <log>` (no `-quit`; exits 0/1).
  It also runs automatically before every player build (`ContentManifestBuildStep`, IPreprocessBuildWithReport).
- The EditMode test `ResourcesManifestIsInSyncWithSourceContent` fails when the manifest is stale.

## Manifest format
`{"version":1,"contentHash":"<sha256 of all inputs>","regions":[..],"levels":[..],"items":[..],"gemPacks":[..],"coinPacks":[..],"modules":[..],"remoteConfig":{..}}`.
Indent 2, LF, no timestamps. Regions ordered by index, levels by id (ordinal); items, gemPacks, modules keep their source-file order. The merge logic is `ContentManifestMerger.Build(contentRoot)` (plain C#, shared by the Editor tool and tests).

## Public API
- `ContentRepository`: `Regions` (by index), `LiveRegions`, `Modules`, `GemPacks`, `CoinPacks` (optional key, docs/14), `Items`, `RemoteConfig`, `ContentHash`, `LevelCount`; `GetRegion(id)`, `GetRegionByIndex(n)`, `GetLevel(id)`, `LevelsOf(regionId)` (ordered by index, new list per call).
- Static helpers: `ContentRepository.EffectiveDifficulty(LevelDef, RegionDef)` returns `Fx` (base d rounded to 1/1000, region offset to 1/10000, then `LevelGenerator.EffectiveDifficulty`). (`CoinPayoutFor` and the level fields `stars` and `rewards.coinPayout`, the region field `platformWeights` and the config key `regionCoinScalePercent` were removed 2026-11-10; the payout is `CoinPayoutRules` in Simulation.)
- Models (`Models.cs`): RegionDef, UnlockDef, ThemeDef, RegionRulesDef, LevelDef (`LevelClass` = JSON `class`; constants `ClassSimple/ClassComplex/ClassHighlyComplex`), GoalDef, StarDef, LevelOverridesDef, RewardsDef, GemRewardDef, ModuleDef, GemPackDef, ItemDef, RemoteConfigDef (+ Ramp/Generator/Score/Economy sections). Content layer uses `double`; convert to `Fx` only at the Simulation boundary.
- Errors: `ContentException` (`ContentId`, message prefixed `[id]`).

## Special level types (owner Final Change 1, 2026-11-22)
- Level JSON: optional `special` (`noPowerUps`, `oneLife`, `keepMoving`; `LevelDef.Special`) and `overrides.rows` (`LevelOverridesDef.Rows`, the floor count of a Combo Climb level). Schema: `Content/schemas/level.schema.json`. `LevelSpecFactory.Build` maps them to `LevelSpec.Special` and `LevelSpec.Rows`; an unknown `special` id throws `ContentException`.
- `LevelDef.SpecialType` (not serialised) = `SpecialLevels.TypeOf(goal type, special)`: the one lookup for level tiles, popups and rewards (None = Regular Run). Names, rule sentences and tile glyphs: `FluffyClimbers.Simulation.SpecialLevels`.
- Source of truth of the level files of regions 1 to 5: `python Tools/content_cli/migrate_levels_25.py` (placement table `SPECIAL_PLACEMENT`); `python Tools/content_cli/content.py validate` checks the shipped JSON against it. Rebuild the manifest afterwards (menu or headless builder above; the merger is plain C#, so any .NET host that calls `ContentManifestMerger.Build` writes the same text).

## Validation on load (no silent defaults)
Manifest version == 1; contentHash present; every required field present (Newtonsoft `Required.Always`); unknown JSON members rejected (`MissingMemberHandling.Error`); exactly 30 regions with indexes 1..30; 25 levels in regions 1 to 5 and 15 in regions 6 to 30 (`ContentRepository.ExpectedLevelsFor`); every referenced level exists, belongs to its region and its `index` equals its position; no duplicate ids; no unlisted (orphan) levels; `unlock.requires` references an existing region; status is live or teaser.

## Assumptions
- Nullable by design (not required): `unlock.requires/gemSkip` (null for region 1), `theme.music`, `availableFrom/To`, level `overrides`, `rewards.gems` and its optional fields, goal `value/count/timeSec`, star `value`, item `tags`.
- If the JSON schemas later add fields, the models must be extended (strict unknown-member check will throw with the offending id).
- Mapping LevelDef -> Simulation LevelSpec is intentionally not done here.
- `IsRegionPlayable(region, progress)` is not implemented yet.
- `LoadFromResources` uses `UnityEngine.Resources`; everything else is engine-free.
