# The Past and the Furious

An arcade racing game built in Unity by **Team Celery** as a vertical-slice school project. Race against 7 AI opponents across three themed maps, drift through corners, pop turbo on straights, and try to finish on the podium.

## Status

Alpha / vertical slice. Three maps are playable end-to-end with AI opponents, a finish/podium flow, lap counting, pause menu, music player, and a garage scene for car selection.

## Controls

| Action | Key / Button |
|---|---|
| Steer | `A` / `D`, Left Stick |
| Accelerate | `W`, Right Trigger |
| Reverse / Brake | `S`, Left Trigger |
| Drift / Powerslide | `Left Shift`, Right Trigger |
| Turbo | `Space`, `A` button |
| Pause | `Esc`, `Start` |

## Running the project

1. Install **Unity 6000.2.14f1** (preferred) or **6000.4.0f1**. Mixing 6000.4 with 6000.2 will bump `Packages/manifest.json` and `ProjectVersion.txt`; coordinate with the team before committing those.
2. Clone the repo and open the `THE-PAST-AND-THE-FURIOUS/` subfolder as a Unity project.
3. Open `Assets/Scenes/MainMenu.unity` and press Play.

## Scene flow

`MainMenu` → `Garage` (pick car/driver) → `Map1` / `Map2` / `Map3` (race) → `WinScene` (podium) or game-over panel (4th+).

## Project layout

- `Assets/Scenes/` — playable scenes (`MainMenu`, `Garage`, `Map1`–`Map3`, `WinScene`).
- `Assets/Scripts/` — gameplay scripts: `CarController`, `AICarController`, `RaceManager`, `MusicManager`, etc. Editor tools live under `Assets/Scripts/Editor/` (waypoint painter, AI race setup window).
- `Assets/Prefabs/` — `Player_BMW_NEW.prefab` (player car), `RaceManager.prefab`, `PausePanel.prefab`, `MusicManager.prefab`, environment props.
- `Assets/Resources/` — runtime-loaded assets including `MusicPlaylistWidget.prefab`.

## Team

| Role | Owner |
|---|---|
| Programming / design | Kyle |
| Car modeling + textures | Adriana |
| Aztec sign / asphalt / racer textures | Rachel |
| I-4 sign / Neo Tokyo buildings | Nathan |
| UI / poster | Anna Grace |
| Post-processing / lighting / VFX / power-ups | Arash |
| Set dressing | Jose |
| Marketing (trailer, screenshots) | Greg |
| Modeling / engine support | Ryan |
