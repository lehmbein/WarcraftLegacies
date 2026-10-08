# Warcraft Legacies – Bot AI

Computer-player bots for Warcraft Legacies (4.8.2 base, Old Gods kept). The whole AI lives in
`src/WarcraftLegacies.Source/Ai/`; the only change to WL's own code is one line in `GameSetup.cs`:

```csharp
WarcraftLegacies.Source.Ai.AiSetup.Setup();   // directly after Artifacts.Setup();
```

| File | Contents |
|---|---|
| `AiSystem.cs` | All logic: target picking on the control-point graph, defense, production, tech, waves, quests, Legion summon, portals |
| `AiConfig.cs` | Data: CP graph (`AiGraph`), portals, quest rules, kill targets, area clears, coordinates (`AiWorld`) |
| `AiUpgrades.cs` | Data: tech pools per faction, building upgrades, event researches |
| `AiDebug.cs` | Chat commands, `-dump` file writer, on-screen readouts |

The chat banner at game start shows the version (`WL AI 0.34.3`). If it doesn't, the map is stale.

## Chat commands

All commands work for every human player. They only read state or change bots — but some (marked ⚠)
change the game for everyone, so don't use them in real multiplayer matches. To ship a build without any
of them, set `Enabled = false` at the top of `AiDebug.cs`.

### Diagnostics

| Command | What it does |
|---|---|
| `-help` | One line: build number and the most used commands |
| `-dump` | Writes a full snapshot to `Documents\Warcraft III\CustomMapData\WLAI_dump_<time>.txt` |
| `-autodump` | Toggle: writes a dump every 5 minutes (`WLAI_dump_05m00s.txt`, `…10m00s.txt`, …) |
| `-why` | One line per bot: state, target, reason, frontier counts |
| `-why <pid>` | Detailed trace for one bot (e.g. `-why 22`) |
| `-portals` | Labels and pings every AI portal (ship + Argus) for 30 s; says whether the graphs are merged |
| `-edges` | Toggle: draws every graph edge as a coloured line (only you see it) |
| `-debug1` | Toggle repeating readout: per-bot army / landmass / target |
| `-debug2` | Toggle repeating readout: load (units touched per tick) |
| `-debug3` | Toggle repeating readout: what each bot is doing |
| `-debug4` | Toggle repeating readout: gold and food per bot |
| `-xy` | Position and landmass of your selected unit |
| `-id` | Type id (rawcode + number), name, owner and position of your selected unit |
| `-ids` | Shows every control point's id above it for 30 s |
| `-fac` | Each bot's faction and how many unit/building types it has per category |

`-edges` colours: green = held, yellow = neutral frontier, orange = uncapturable, red = contested,
blue = neutral↔neutral (faded), magenta = crosses landmass, grey = closed (time/gate).

### Bot control ⚠

| Command | What it does |
|---|---|
| `-botbreak` | Toggle: pause all bot logic |
| `-prod` | Toggle: bot production on/off |
| `-atk` | Toggle: bot attacking on/off |
| `-gold0` `-gold50` `-gold100` `-gold150` `-gold200` | Gold every bot gets per production turn (default 25 ≈ 100 gold/min per bot; Scourge/Legion get 150 %) |

### Events and tests ⚠

| Command | What it does |
|---|---|
| `-legion` | Forces the Legion summon (Argus hub opens, graphs merge, betrayal) |
| `-ogwave` | Forces the Old Gods invasion wave |
| `-ship` | Ship-portal asset test at (0,0) plus a walkable pair beside your selected unit; repeat = next ship model |

## The dump file

`-dump` / `-autodump` write to `CustomMapData` (on Linux/Proton: inside the Steam prefix,
`…/pfx/drive_c/users/steamuser/Documents/Warcraft III/CustomMapData`). Each line is wrapped as
`call Preload( "…" )` — send the file as it is. Sections:

- **PINNED** – one-off events that are never pushed out (portals created, summon, repairs, quest
  completions, `TICK ERROR in …` lines)
- **PORTALS** – every AI portal pair: name, position, alive, destination
- **LOAD** – units touched per tick (the multiplayer lag indicator)
- **BOTS** – per bot: target and why, frontier counts, army by landmass, heroes
- **WAVES**, **PERMADEAD**, **EVENT LOG** (last 300 changes), **NODES**, **EDGES**

`wl.sh dumps` (Ubuntu) moves all dumps into one dated archive, so two games never mix.

## Build and deploy

- **Windows (Winboat):** start `tools\wl-build-watch.ps1` once per session.
- **Ubuntu:** `wl.sh deploy` takes the newest `Ai*.cs` from Downloads, checks each file contains its
  own class, installs them, asks Winboat to build + publish, and copies the map into `Maps/Own`.
  `wl.sh build` rebuilds without new files. Set `SHARED` at the top of `wl.sh` once.

Developer checks (Python, `pip install tree-sitter tree-sitter-c-sharp`):
`tools/check_refs.py <Ai folder>` (wrong-class / private references – these only fail at the map
transpile step, not at `dotnet build`) and `tools/map_audit.py <map.w3x> <Ai folder>` (every graph edge
checked against the map's real pathing).

## Fixed in this fork: `%` in quest/power text

`SYSTEM ERROR: Kul'tiras failed to execute OnQuestProgressChanged: … invalid use of '%' in replacement string`
(also Stormwind). WL's `Loc.Format` used `string.Replace`, which in the Lua build treats `%` in the replacement as
a pattern character, so any power text with a percentage (e.g. "35%") crashed the quest reward before it handed
over units. History upstream: #4061 introduced a safe replacement, #4083 removed it again (WL 4.8.2 ships that
version), #4189 (5.0 beta, a 253-file commit) brought it back. That commit can't be cherry-picked on its own, so
this fork carries the same small change in `src/MacroTools/Localization/Loc.cs` as its own commit (0.34.3). The
AI's Kul Tiras repair stays as a harmless fallback.

## Versions

`0.MINOR.PATCH` -- a feature build raises MINOR, a fix-only build raises PATCH. The same name is used for the
in-game banner ("WL AI 0.34.3"), the `-dump` header and the git tag.

## Translator limit to remember (CSharp.lua)

A class may have only about 55 static fields with a non-literal initializer (`= new()`, a method call ...). Fields
beyond that are silently `nil` at runtime -- no build error. `tools/check_refs.py` reports every class over 45;
create new collections in an Init method instead (see `SimpleBot.InitState`).
