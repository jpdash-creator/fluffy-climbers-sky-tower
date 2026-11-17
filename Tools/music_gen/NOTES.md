# music_gen notes

- `compose_menu_themes.py`: round 1 (Sunny Skip, the original menu loop). `pitch_down_keep_tempo.py` renders the lowered version; originals are kept in `originals/`.
- `compose_menu_themes_v2.py`: round 2, five themes, output `candidates_2026-11-17/<n>_<name>.wav` (+ `_preview.wav`, not shipped).
- Shipped 2026-11-17: all six are the menu pool. Ship name = the candidate name without the number (`1_arcade_sprint.wav` -> `Assets/Resources/Audio/music/arcade_sprint.wav`), `loop.wav` -> `sunny_skip.wav`. To add a track: copy the wav into `Assets/Resources/Audio/music/`, copy an existing `.wav.meta` (new guid), and add its id and display name to `MusicCatalog.Ids` / `Names` (`Assets/_Game/Gameplay/MusicCatalog.cs`).
- Behaviour (AudioManager): default Random shuffle (never the same track twice in a row, also not at the next app start, 0.8 s crossfade at the end of a track), or one locked track that loops; every change of music bed or jingle cuts or ducks the old sound on the same call (`MusicSelector.Request(id, hardCut)`, `MusicDuck.Duck`).
