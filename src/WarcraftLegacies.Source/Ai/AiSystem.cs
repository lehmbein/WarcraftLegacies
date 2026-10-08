using System.Collections.Generic;
using MacroTools.Extensions;
using MacroTools.Factions;
using MacroTools.Legends;
using MacroTools.Quests;
using MacroTools.Shared;
using MacroTools.ControlPoints;
using MacroTools.GameTime;
using WarcraftLegacies.Source.Factions.Legion.Quests;

// Warcraft Legacies AI -- build 0.34.3 (hotfix). main / 4.8.x.
//  * 0.34.3: 18 static collections created in InitState -- CSharp.lua left static fields with initializers past
//    ~55 per class nil, which broke portals, area clears and every bot's attack logic (v34-v34.2). Legion exit
//    back beside Felwood; Legion's Argus buildings placed on searched open ground. Versions now 0.MINOR.PATCH.
// Warcraft Legacies AI -- build v34.2 (robustness: isolated tick/commands/dump; area clears; links).
//  * v34.2: every wave-tick subtask, summon step, chat command and dump section runs isolated (one error used
//    to stop the gate sweep, -dump and -portals); gates swept first. Area clears (Oculus, Nexus). Highbank <->
//    Southern Highlands, Nazmir <-> Kezan ship. Neutral bias 0.5. -help shortened (README.md has the list).
// Warcraft Legacies AI -- build v34.1 (portal + summon fixes, Kul Tiras repair, Balor).
//  * v34.1: portals owned by Neutral Extra (WL removes Neutral-Passive units from areas) + watchdog + per-pair
//    try/catch; summon handling runs whatever completed the quest (polled), once, merge guaranteed; Kul Tiras'
//    crashed Admiralty reward repaired (base rescued); Balor <-> W. Stranglethorn ship + Balor on the island
//    graph; pinned log + PORTALS section in -dump; -portals pings every AI portal.
// Warcraft Legacies AI -- build v34 (node system: islands, Kul Tiras, Argus hub, merge event; bug fixes).
//  * v34: ship portals (real waygates, named, landing beside the other end) for the Kalimdor islands and Kul
//    Tiras; Argus open from the start and linked to Northrend until the summon, then to Felwood and (WL's demon
//    portal) Quel'Danas; the summon MERGES the graphs, home graph first. Bot Legion starts in Argus (workers ->
//    buildings). The Sunwell capital now triggers the summon (once). Construction Sites upgraded (Stormwind
//    City). Fallback events (Theramore, Scarlet Crusade) no longer auto-ordered. Fallback target respects the
//    10-min lock. No naval techs. Illidan's Broken Isles grant removed.
// Warcraft Legacies AI -- build v33 (tech pools, Ragnaros summon+kill, ship-portal test).
//  * v33: two tech pools besides weapon/armor (unchanged): UNIT/ABILITY (casters + abilities, incl. stock
//    abilities the auto-list missed) = 1 + 1 per 5 min + 1 per 2 CPs, stepped by level; BUILDING/MISC (free
//    building upgrades, town halls first; then naval/flight) = 1 per 2 CPs. v32 caster schedule removed.
//    Ragnaros: pedestal captured -> summoned -> killed for free by the bot's level-12 hero standing by.
//    Legion exit back beside Felwood. -ship asset test for the Kul Tiras ship portals.
// Warcraft Legacies AI -- build v32 (early quests by turn, starter buildings, tech split, lore rules).
//  * v32: early quests force-completed for bots during turn N-1 of their expiry (WL turn counter), one per 3 s.
//    Starter barracks + altar for every bot (workers become buildings). Upgrades split: techs (5-min timer +1 per
//    2 CP captures), caster trainings (all, up to max level at 4/10/18 min), event researches (ordered when WC3
//    allows, free; Fireland Invasion not before 15:00; plague left to WL). Stromgarde rule fixed. Cenarius stays
//    dead for bots. Late-game quest rules (World-Shaman, Guardian, Line of Succession, Ascension). Ragnaros
//    pedestal hero trip. Murmur kill target. Legion's Lordaeron capital + northern CPs handed to the Scourge.
// Warcraft Legacies AI -- build v31 (target ranking, stuck detection, quest rules, Legion portal).
//  * v31: frontier ranking -- neutral-only before 10 min, then nearest-to-army with a neutral bias; per-faction
//    goal routing (Legion -> Nordrassil); stuck detection blacklists a target after 4 min without progress.
//    Kill targets by unit type (Scourge -> Sapphiron 'ubdr'). Quest rules with conditions (capital lost, CP
//    held by ally). WL's caster-less Demon Portal rebuilt off (0,0) + Antoran portal re-pointed; +3000 gold
//    for a bot Legion on summon. Per-faction gold subsidy. CP grants (Illidan's Broken Isles).
// Warcraft Legacies AI -- build v30 (neutral-CP fix, army anchor, bot-only waves, graph edits).
//  * v30: IsEnemyCp ignores neutral players (their slot state reads PLAYING -> every creep CP was "enemy").
//    Target selection anchors on the army centroid. Waves only run when a BOT is in the group (a human
//    Kul Tiras got the fleet messages). Timed quest auto-complete for bots (AiConfig.TimedQuests).
// Warcraft Legacies AI -- build v29 (defense rewrite).
//  * v29: Rule 0 defense only fires for a DAMAGED owned anchor with >5 ENEMY-PLAYER ground units near it.
//    Neutrals (creeps + ~1900 preplaced Neutral-Passive rescue units), allies and units on unwalkable terrain
//    (ships) are ignored. Undamaged anchors are not scanned at all (removes most of the combat-tick load).
//  * v28: graph walker skips invulnerable / Neutral-Passive CPs (quest-locked; can never be captured -> bots parked
//    on them forever). Clash window + staging REMOVED: enemy frontiers are attacked directly (the 30s release
//    window yo-yo'd armies back to the muster CP mid-march). Perma-death guard: heroes that died permanently
//    (LegendaryHero.Died, Permanent=true) or carry WL's 'LEgo' perma-dies marker are never re-trained/revived.
//    Diagnostics: per-bot BotDiag + per-wave WaveDiag + decision event log + per-subsystem op counters, read by
//    AiDebug (-dump writes them to a file). Logic only WRITES diagnostics; it never branches on them.
//  * v14: per-faction ordered attack march from AiConfig.Doctrine (coordinate waypoints via -xy, time-gated,
//    ownership-aware, advances when a step is clear+held). Falls back to the generic picker when unfilled.
//  * v13: bots get a flat gold subsidy each production service (they were gold-starved, not food-capped);
//    mass to MinArmy=20 before attacking (bigger pushes); free hero revival for bots every 3 min.
//  * v12: production now services ONE bot per tick (was all bots every 15s) -> the big load spike is gone.
//    Attack-move is re-issued only when the target changes or every ~20s -> no more stop-and-go stutter.
//  * v11: inspection moved to AiDebug.cs (own file, master const, per-player -debug1/2/3, default OFF).
//    Deterministic op counters live in the logic here (near-free, always on). Control commands
//    -botbreak/-prod/-atk stay here so they work even in a no-debug build.
//  * v8-main: dropped the dev-only H00B vertical-gate constant (absent on main -> would not compile).
//  * v9: area doctrine -- attack nearest reachable enemy/neutral CONTROL POINT first, then CAPITAL;
//    Rule 0 defense (recall if >5 enemies near an owned anchor); home landmass taken from an owned
//    capital/CP (fixes idle-at-portal, snaking, and the Druids l-1 hug). Replaces FindTarget's scan.
//  * v10: bots train/revive heroes at owned Altars using faction-correct ids from AiConfig.cs
//    (source-derived, no id spray). Gold-gated; WC3 hero-limit self-caps; faction absent -> skip.
//    NEW FILE: AiConfig.cs must sit beside this file in the Ai/ folder -- copy BOTH into the build.
//  * v8: one bot serviced per combat tick (round-robin); ~13x less enum/orders per second.
//  * v8: chat toggles -botbreak (stop), -debug (readout mute), -prod, -atk.  Diagnostics kept.
//  * QuestAutoComplete: at 2:00, completes every in-play faction's designated StartingQuest (humans + bots for
//    now; flip computerOnly=true later to exclude humans -- that is the split point).
//  * Capital-targeting for SCOURGE only: attack-moves toward the nearest reachable enemy/unowned CAPITAL
//    (clears the yard; capital becomes takeable), instead of snaking through nearest hostile. Others unchanged.
//  * everything from v6 (gate removal, gather-to-5, attack-move, hold-if-unknown-landmass, faction production, -xy, -fac).

namespace WarcraftLegacies.Source.Ai
{
  public static class Geography
  {
    private const int GW = 416;
    private const int GH = 448;
    private const float CellW = 128.0f;
    private const float MinX = -28672.0f;
    private const float MinY = -32256.0f;

    private const string Grid =
      "...........................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................C..................CCC...................................................................................................................................................................................................................................................................................................................................................................................................CCCC..C.C........CC.C.C.CCCCCCC...........................C.................................................................................................................................................................................................................................................................................................................................................................CCCCCCCC.C...CCC....CC..CCCCCCCCCCCCC........................C..................................................................................................................................................................................................................................................................................................................................................................CCCCCCCCC...C.....CCCCCCCC.CCCCCCCCCCC....................CCCCC....C..C....C.......................................................................................................................................................................................................................................................................................................................................................CCCCCC...C......CCCCCC.CCCCCCCCCCC................C.CCCCCCC.C..CCCCC..CCC..................................................................................................................................................................................................................................................................................................................................................CCCCCCCCCCCC..C.....CCCCCC..CCCCCCCCCCCC...............CCCCCCCCCCCCCCCCCCCCCCC.C.................................................................................................G.GGGGGG..GGGGGGG............GGGGG....GGGGG...........................................................................................................................................................................................C.C..C...CCCCCCCCCCCC..CCC.CCCCCCCCCCCCCCCCCCCCCC...........CCCCCCCCCCCCCCCCCCCCCCCCCCCCC......CCCCC.............SSSS.....................................................................GGGGGGGGGGGGGGGGGGGGG.GGG...GGGGG.G..GGG..G.........................................................................................................................................................................................CCCCCCCCCCCC.CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC...........CCCCCCC.CCCCCCCCCCCCCCCCCCCCC......CCCCCCC..........SSSSSSS....................................................................GGGGGGGGGGG.GGGGGGGG.GGGGGGGGGGG..GGGGGG.G.........................................................................................................................................................................................CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC...........CCCC..CCCCCCCCCCCCCCCCCCCCCCC.....CCCCCCC..........SSSSSSSS..................................................................GGGGGGGGG.GGGG.GGGGGGGGGGGGGGGGGGGGGGGGGG.........................................................................................................................................................................................CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC.CCCCCCCCCCCCCCCCCCC.........CCCC...CCCCCCCCCCCCCCCCCCCCCC.....CCCCCCCC........SSSSSSSSS..................................................................GGGGGGGGGG.G....GGGGGGGGGGGGGGGGGGGGGGGG...........................................................................................................................................................................................CCCCCCCCCCCCCCCC..C.CCCCCCCCCCC.....C.CCCCCCCCCCCCCCCCCC........CCCCCCCCCCCC.CCCCCCCCCCCCCCCC.....CCCCCCCCC.......SSSSSSSSS..................................................................GGGGGGGGG........GGGG...GGGGGGGGGGGGGGG...........................................................................................................................................................................................CCCCCCCCCCCCCCCC....CCCCCCCCCC......C.CCCCCCCCCCCC..CCCCC........CCCCCCCCCCCCCCCCCCCCCCCCCCCCC....CCCCCCCCCC.......SSSSSSSSSS.................................................................GGGGGGGG.G........G........GGGGGGGGGGGG.G..........................................................................................................................................................................................CCCCCCCCCCCCCCC....CCCC.CCCCC........CCCCCC.CCCCC...CCCC.........CCCCCCCCCCC..C.CCCCCCCCCCCCC......CCCCCCCCC......SSSSSSSSSS..................................................................GGGGGG.G.G.................GGGGGGGGGGGGG..........................................................................................................................................................................................CCCCCCCCCCCCC.CC...CCCCC..CCC.......CCCCC...CCCC.....CCC........CCCCCCCCCCCC....CCCCCCCCCCCCCCCCCCCCCCCCCCCC......SSSSSSSSSS.................................................................GGGGGGGG...GGGG.............GGGGGGGGGGGGG..........................................................................................................................................................................................CCCCCCCCCCCCCCCCC.C.CCC..CCCC......CCCCC....CCCCC...CCCCCC......CCC.CCCCCC.C...CCCCCCCCCCCCCCCCCCCCCCCCCCCCC......SSSSSSSSSS.................................................................GGGGGGGGGGGGG...............GGGGGGGGGGG.............................................................................JJJJJJJJJJJJJJJJJJ.............................................................................................CC.CCCCCCCCCCCCCCCCCCCCC..CC........CCCC.....CCC.CCCCCCCC....CCCCCC.CCCCC.CCC..C.CCCCCCCCCCCCCCCCCCCCCCCCCCC......SSSSSSSSSS.................................................................G.GGGGGGGGGGGG..............GGGGGGGGGGGG.......................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ..............................................MMMMMMMMMMMMMMM.................................CCCCCC...CCCC..CCCCCCCCCCCCC......CCCCCC....CCCCCC.CCCC.C.CCCCCCCCCCCCCCC..CCCCCCCCCCCCCCCCC.....CCCCCCCCCC......SSSSSSSSSS...................................................................GGGGGGGGG...................GGGGGGGGGGG......................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM................................CCCCCCC..........CCCCCCCCCCCC......CCCCC.CCCCCCC.C.CCC.CCCCCCCCCCCCCCCCCC....CCCCCCCCCCCCCCCCC....CCCCCCCCCC......SSSSSSSSSS.....................................................................GGG..G.G....G..........GG.GGGGGGGGGGG......................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM................................CCCCC....CC......CCCCCCC.CCC........CCCCC.CCCCCCCCCCCC...CCCCCCCCCCCCCCCCCC.CCCCCCCCCCCCCCCCC.....CCCCCCCCCC......SSSSSSSSS..........................................................................G.G....GGG...........GGGGGGGGGGGG......................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM................................CCCCC...CCCCCC...CCCCCC.CCC........CCCCCC.CCCCCCCCCCCCC.CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC.....CCCCCCCCCC......SSSSSSSSS..........................................................................G.....GGG.GG.....GG..GGGGGGGGGGG.......................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM................................CCCC.C..CCCCCCC..CCCCCCCCCC........C.CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC......CCCCCCCCC.......SSSSSSSSS...............................................................................GGGG........GGGGGGGGGGGGG.........................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM...............................CCCCC.C..CCCCCCCCCCCCCC.CC.......CCCCCCCC.CCCCCCCCC.CCCCCCCCCCC...CC..C..CCCCC..CCCCCCCCCCCC.......CCCCCCCC........SSSSSSSSS................................................................................GGGG...G....GGGGGG...GGG.........................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM...............................CCCC.....CCCCCCCCCCCC.CCC...CC..C.CCCCCCCCCCC.CCCCCC.CCCCCCCCCC............CCC...CCCCCCCCCCCC......CCCCCCC.........SSSSSSSSS................................................................................GGGGGGG..GGGGG...................................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM...............................CCCCCC.....CCCCC..CCCCCCC..C...CCCCCCCCCCCCCC..CCC..CCCCCCCCCCC.C..........CC.......CCCCCCC........CCCCCC..........SSSSSSSSS..............................................................................GGGGGGGGGG.GGGGG...................................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM................................CCCCC....CCCCCC...CCCCCC.C....CCCCCCCCCCCCCCCCCCCCCCCCCCCCC.CCCCCC.................CCCCCC.C.......................SSSSSSSSS................................................................................GGGGGGGGGGGGGGG..................................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM...............................CCCCCCC....CCC....CCCCCCCC.......CCCCCCCC.CCCCCCCCCCCCCCCCC.C..CCC..................CCCCCCCCC.......................SSSSSSS..............................................................................GGGGGGGGGGGGGGGGGGG.................................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM.................................CCCCCC...CCC..CCCCCCCCCC.......CCCCCCC.CCCCCCCCCCCCCCCCCCC...CCC....................CCCCCC........................................................................................................G..GGGGGGGGGGGGGGGGGG.G..................................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM..................................CCCCCCCCCCCCCCCCCCCCCCC.......CCCCCCCC.CCCCCCCCCCCCCC........C.....................CCC.C....................................................................................................G.G.GGGGGGGGGGGGGGGGGGGGGGG.GG................................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM....................................CCCCCCCCCCCCCCCCCCCCCC......CCCCCCCCCCCC.CCCCCCCCC...............................CCC.....................................................................................................GGGGGGGGGGGGGGGGGGGGGGGGGGG.........GGGG.......................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM..................................CCCCCCCCCCCCCCCCCCCCCCCC...CC..CCCCCCCCCC.CCCCCCCCCC.......................................................................................................................................GGGGGGGGGGGGGGGGGGGGGG...GGG.......GGGG.G......................................................RRRRRRRRRRRRRRR......JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO........MMMMMMMMMMMMMMM................................CCCCCCCCCCCCCCCCCCCC.CCCCC...C...CCCCCCCCCCCCCCCCC...........................................................................................................................................GGGGGGGGGGGGGGGGGGGG..............GGGGGGG.G.........................................................................JJJJJJJJJJJJJJJJJJ....NNNNNNNNNNNNNNN....OOOOOOOOOOOOOOO......................................................CCCCCC.CCCCCCCCCCCCC.CCCCCCCC.C........CCCCCCCCCCC.............................................................................................................................................GGGGGGGGGG.GG.GGGG..............GGGGGGGGGG.......................................................................................................................................................................................CCCCC...CCCCCC.C...CCCCCCCCCC...........CCCCCCCCC..........C...................................................................................................................................GGGG.GGG......GG...............GGGGGGGGG.G.......................................................................................................................................................................................CCCCCCC...CCCC..CCCCCCCCCCCCCC........C.CCCCCCCC.........CCCC.........C.........................................................................................................................GGGG...........................GGGGGGGGGG.............................................................................PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ......................................................CCCCCCCC..CCCCCCCCCCCC...CCCC..........CCCCCCCC...........CC.CCC.....C.CC......................................................................................................................GGGG.............................GGGGG...G.............................................................................PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ.......................................................CCCCCCC.CCCCCCCCC...C...CCCCC.....CC.CCCCCC.C.........CCCCC..CC.C.CCCCCCCCCCC................................................................................................................GGGGGG............................GGGGGG.....................................................IIIIIIIIIIIIIIIIIIIIIIIII..PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ...........................................................CCC..CCCCCCC.CC.C...CCCCCCC....CCCCCCCCC........CCCCCCCCC.CCCCCCCCCCCCCCCC.................................................................................................................GGGGGG....................GG.....GGGG......................................................IIIIIIIIIIIIIIIIIIIIIIIII..PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ...........................................................CCC..CCCCCCC.CCCC....CCCCCC....CCCCCCC.......CCCCCCCCCCCC..CCCC......CCCC.................................................................................................................GGGGGGG.GGG...GG.....GGG..GGGG...GGGGG......................................................IIIIIIIIIIIIIIIIIIIIIIIII..PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ...............................................................CCCCCCC....C.....CCCCCC...CCCCCCC......CCCCCCCCCCCCCCCCCCC..CCCC...CCCCC..............................................................................................................GGGGGGGGGGGGGGGGG...GGGGGGGGGGGGGGGGGGGG....................................................IIIIIIIIIIIIIIIIIIIIIIIII..PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ..............................................................CCCCCC.C.....C......CCCCCC.CCCCCC.....CCCCCCCCCCCCCCCCCCCC...CCCC....CCCC..............................................................................................................GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG....................................................IIIIIIIIIIIIIIIIIIIIIIIII..PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ..............................................................CCCCCC.............CCCCCCCCCCCCCCC...CCCCCCCCCCCCCCCCCCCC.CCCCCCC...CCCCC...............................................................................................................GGGGGGGGGG..GGGGGGGGGGGGGGGGGGGGGGGG...GG..................................................IIIIIIIIIIIIIIIIIIIIIIIII..PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ..............................................................CCCCC..............CCCCCCCCCCCCC.....CCCCCCCCCCCCCCCCCCCCC.CCCCCCC...CCCCC.............................................................................................................GGGGGGGGGGG...GG....GGGGGGGGGGGGGGGGGGGGG...................................................IIIIIIIIIIIIIII............PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ...............................................................CCCC...............CCCCCC.C.CCC.....CCCCCCCCCCCCCCCCCCCC.CCCCCCCCC..CCCCC............................................................................................................G.GGGGGGGG...........GGGGGGGGGGGG..GG.GGG....................................................IIIIIIIIIIIIIII............PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ...............................................................CCCC..........CC..CCCCCCCCCCCCC....C.CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC.C............................................................................................................GGGGGGG....GGGG...GGG.GGGGGG.G...GGG.G.G..................................................IIIIIIIIIIIIIII............PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ..............................................................CCCCC.......CCCCC..CCCCCCCC.CCCC....CCCCCCCCCCCCCCCCCCCCCCC.CCCCC..CCCCCCCCCCC.........................................................................................................GGGGGGG....GG.G.....GGGG....G...G...G...GG..................................................IIIIIIIIIII..II............PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ..............................................................CCCCCC.CCCCC.CCCCCCCCCCCC.C.CCCCC.CCCCCCCCCCCCCCCCCCCCCCCCC...CCC.CCCC.CCCCCCCCCC.........................................................................................................G.GG.GGGG.GG.............................................................................IIIIIIIIIII.III............PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ.............................................................CCCCCCCCCCCCCCCCCCCCCCCCC...CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC..CCCCCCCCCCCCCCCCCC.C.C........................................................................................................GGGGGGG..G.............................................................................IIIIIIIIIIIIIII............PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ...........................................................CC..CCCCCCC.CCCCCCCCCCCCCCC..CC.CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC...................................................................................................GGGGGGGGGGG................................................................................IIIIIIIIIIIIIII............PPPPPPPPPPPPPPP...LLLLLLLLLLLLLLL....QQQQQQQQQQQQQQQ.........................................................CC.CCCCCCCCCC..CCCCCCCCCCCCCCC....CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC.C..................................................................................................GGGGGGGGGG..GGG.............................................................................IIIIIIIIIIIIIII........................................................................................................................CCCCCCCCCC.CCCCCCCCCCCC.CCCCCCC.CC.CCCCCCCCCCCCCCCCCCCCCCCCCCC.CCCCCCCCCCCCCCCCCCCCCCCCC.CC...................................................................................................GGGGGGGG..GGGG.............................................................................IIIIIIIIIIIIIII........................................................................................................................CCCC.CCCCCCCCCCCCCCC....C.CCCCCCCCCC.CCCC....CCCCCCCCCCCCC.CC.CCCCCCCCCCCCCCCCCCCCCCCCCCCCC..................................................................................................GGGGGGG..GGGGGG.............................................................................IIIIIIIIIIIIIII.......................................................................................................................CCCCCCC.CC.CCCCCC.........CCCCCCC.CCCCCC.......CCCCCCC.CCC.CCC..CCC.CCCCCCCCCCCCCCCCCCCCCCCC..................................................................................................GGGGGGG.GGGGGGG.............................................................................IIIIIIIIIIIIIII.......................................................................................................................CCCC.C...CCCCCCC...........CCCCCCC.CCCCCCC.....CCCCCCC..CCCCCCCCCC...CCCCCCCCCCCCCCCCCCCCCC...................................................................................................GGGGGGGGGGGGGGG.............................................................................IIIIIIIIIIIIIII........................................................................................................................CCCC......CCCCCC.........CCCCCCCCCCCCCCC.......CCCCCC.CCCCCCCCCC...CCCCC.CCCCCCCCCCCCCCC.....................................................................................................GGG.GGGGGGGGGGG.............................................................................IIIIIIIIIIIIIII........................................................................................................................CCCC....CCCCCCC.CCCCCCCCCCCCCCCCCCCC..CCC.......CCCCC.C.CCCC.CCCCCCCCCC..CC.CCCCCCCC.CCCC....................................................................................................GGG.GGGGGGGGGGG.............................................................................IIIIIIIIIIIIIII........................................................................................................................CC.CC.CCCCCCCCCCCCCCCCCCCC.CCCCCCCCCC.CCCC......CCCCC.........CCCCCCC....CC..CCCCCCCCCCCC....................................................................................................GG.GGGGGGGGGGGG.....................................................................................................................................................................................................................CCCCC..CCC.CCCCCCCCCCCCCCCCC..CCC.C.CCCCCC.......CCC...........CCCCC..CC....CCCCCCCCCCCC......................................................................................................GGGGGGGGGG........................................................................................................................................................................................................................CC.CCCCCCCCCCCCCCCCCC.CCCCC...CCC....CCC...........C............CCC..........CCCCCCCCCC.....................................................................................................GGGGGGGGG...........................................................................................................................................................................................................................CCCCCCCCCCCCCCCCCCCCC..CCCC...CCC..C............................CCC..........CCCCCCCCCC..........................................................AAAA.......................................GGGGGGGG...GGGGG......................................................................................................................................................................................................................CCC.CCC.CCCC.CCCCCCC.....CC.CCC.C.C........................................C.CCCCCCC.........................................................AAAAAAAAA....................................GGGGGGGGGGGGGGGG......................................................................................................................................................................................................................C.CCC.C.CC.....CCCCC.C.CC..CCCC...C...........................................CCCC............................................AAAAAA........AAAAAAAAAAA...................................GGGGGG.GGGGGGGGG.......................................................................................................................................................................................................................C.C...........CCCC.C.C.C.C.CCCCCC..........................................................................................AAAAAAAAAAA....AAAAAAAAAAAAA..................................GGGGG..GGGGGGGGG..................................................................................................................................................................................................................................CCCCCCC.C..C..CCCCCCCCC.........................................................................................AAAAA.AAAAAAAAAAAAAAAAAAAAAA..................................GGGGG..GGGGGGGGG...............................................................................................................................................................................................................................CCC..CCCCC..C..CC.CCCCCCCCC.C......................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAA..................................GGGGG.GGGGGGGGGG.................................................................................................................................................................................................................................C..CCCC..CCCC.CCCCCCCCCCC..CC..................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..................................GGGGG.GGGGGGGGGG...............................................................................................................................................................................................................................CC..CCCCCCCCCCCCCCC.CCC.CCCCCC........................................................................................AAAAAAAAAAAAAAAAAAAAAAAAA..................................GGGGG.GGGGGGGGGG...............................................................................................................................................................................................................................CC..CCCCCCCCCCCCCCCCCCC.CCCCCCC...........................................................................................AAAAAAAAAAAAAAAAAAAAA..................................GGGGG.GGGGGGGGGG..............................................................................................................................................................................................................................CCC...CCCCCCCCCCCCC.CCCC.CCCCCC................................................................................AA..A......AAAAAA...AAAAAAAAAAAAA.........................................GGGGGGGG...............................................................................................................................................................................................................................CCC.CCCCCCCCCC.CCCC..CCC.CCCCCC.......................C.CC....................................................AAAAAAA.....A.AAAA....AAAAAAAAAAA.................................................................................................................................................................................................................................................................................CCCCCCCCC.CCCC.CCCCC.CCCCCCCC.C...................CCCCCCCCC................................................AAAAAAAAAAA....AAAAAAA....AAAAAAAAAA.................................................................................................................................................................................................................................................................................CCCCCCCCC.CCCCC.CCCC.CCCCCC.....................CCCCCCCCCCC...............................................AAAAAAAAAAAAA...A.AAAAA.....AAAAAAAA...................................................................................................................................................................................................................................................................................CCCCC....CCCCCCCCC..CCCCC..C...................CCCCCCCCCCCC..............................................AAAAAAAAAAAAA...AAAAAAAA....AAAAAA....................................................................................................................................................................................................................................................................................CCCC.....CCCCCCCCC...CCCCC.C........C.....C....CCCCCCCCCCCCC..............................................AAAA.AAAAAAAA....AAAAAAAAAAAAAAA......................................................................................................................................................................................................................................................................................CCCC..CCCCCCCCCCCC..CCCCC.CC.......CC.....CC.CCCCCCCCCCCCCCCC..............................................AAAAAAAAAAA.....AAAAAAAAAAAAAA.......................................................................................................................................................................................................................................................................................CCCCCCCCCCCCCCCCCC..CCCC..........CCCCCCCCCCCCCCCCCCCCCCCCCCC................................................AAAAAAAA....AAAAAAAAAAAAAAAA.......................................................................................................................................................................................................................................................................................CCCCCCCCCCCCCCCCCCC.CCCCCCC......CCCCCCCCCCCCCCCC..CCCC.CCC..................................................A....AAAAAAAAAAAAAAAAAAAAAAAAAAA...................................................................................................................................................................................................................................................................................CCCCCCCCCCCCCCCCCCC..CCCCCC......CCCCCCCCCCCCCC.....C...CCC.......................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA............................................................................................................................................................................................................................................................................CCCC....CCCCCC..CCCC.CCCCCC......CCCCCCCCCCCCC...........CC.....CCCC...........................................AAAAAAAAAAAAA..AAAAAAAAAAAAAAAAAAAAAAAAA.........................................................................................................................................................................................................................................................................CCCC...CCCCCCC...CC.CCCCCCC......CCCCCCCCCCCC............CC....CCCCC...........................................AAAAAAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAA.....A.A.A.....A....................................................................................................................................................................................................................................................CCCCC..CCCCCCC.C.CCCCCCCCCC.......CCCCCCCCCCC.........C.CCC...CCCCCCC.........................................AAAAAAAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAAAAAAA.AAA.....AA..............................................................................................................................................................................................................................................CCCCC...CCCCCCCCCCCCCCCCC.........CCCCCCCCC.....CCCCCCCCCCCCCCCCCCCC.......................................AAAAAAAAAAAAAA.......AAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA....AAA..............................................................................................................................................................................................................................................CCCC...CCCCCCCCCCCCCCCCC..........CCCCCCCC....CCCCCCCCCCCCCCCCCCCCC.......................................AAAAAAAAAAAAAA.....AAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAA.....................................................................................................................................................................................................................................................CCCCCCCC.....CCCCC........CCCCCCCCC...CCCCCCCCCCCC...CCCCCCC......................................AAAAAAAAAAAAAAA.....AAAAAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA......................................................................................................................................................................................................................................................CCCCCC......CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC......CCCCCC.................................AAAAAAAAAAAAAAAAAA.A....AAAAAAAAAAA.......AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..............................................................................................................................................................................................................................................CC....CCCCCCCCC.....CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC..CCC.......CCCC.................................AAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAA............AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AA........................................................................................................................................................................................................................................CCC.CCCCCCCCCCCCCCC..CCCCCCCCCCCCCCCCCCC..CCCCCCCC...CCCC..........................................AAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAA...........AAAAAAAAAAAAAAAAAAA...AAAAAAAAAAAAAAAAAAA.......................................................................................................................................................................................................................................CCCCCCCCCC.CCCCCCCC..CCCCC.......CCCCC.....CCCCCCC....CCC..........................................AAAAAAA....AAAAAAAAAAAAAAAAAAAAA..............AAAAAAAAAAAAAAA.........AAAAAAAAAAAAAAAAA......................................................................................................................................................................................................................................CCCCCCCC....CCCCCCC..CCCC..........CC.......CCC.C.....CCCC.........................................AAA.A...AAAAAAAAAAAAAAAAAAAAAA..................AAAAAAAAAAAA............AAAAAAAAAAAAAAA.....................................................................................................................................................................................................................................CC.CCCCCC.....CCCCC...CCCC...................CCCC.....CCCCCCC.......................................AAA....AAAAAAAAAAAAAAAAAAAAAA........AAA........AAAAAAAAAAA.............AAAAAAAAAAAAAAA........................................................................................................................................................................B............................................................CCCCCCCCC.....CCC.....CCCC...................CCC....CCCCCCCCC..............................................AAAAA..AAAAAAAAAAAAAA.......AAAAAA......AAAAAAAAAAAA....AAAA......AAAAAAAAAAAAAA.................................................................................................................................................................BBBBB.BBBBB..........................................................CCCCCCC......CCC....CCCC....................CCC....CCCCCCCCC..............................................AAAAA..AAAAAAAAAAAAAA......AAAAAAAA.....AAAAAAAAAAAA...AAAA.AA....A.AAAAAAAAAA..................................................................................................................................................................BBBBBBBBBBBB............................................................CCCCC.....CCCCCCCCCCCC.......C...........CCCC....CCCCCCCCC...............................................AAAA.AAAAAAAAAAAAAA.......AAAAAAAAA....AAAAAAAAAAAAAAAAAAAAAAA....AAAAAAAAAAAA...............................................................................................................................................................B.BBBBBBBBBBBB.......................................................................CCCCCCCCCCCC....CCCCC.......CCCCCC....CCCCCCCCC...................................................AAAAAAAAAAAAAAA......AAAAAAAAAA....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.............................................................................................................................................................BBB.BBBBBBBBBBB........................................................................CCCCCCCCCCCCCCCCCCCCCC.....CCCCCCC....CCCCCCCCC.................................................AAAAAAAAAAAAAAAAAA.....AAAAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAA...............................................................................................................................................BBBBBBBBB....BBBB.BBBBBBBBBBBBB......................................................................CCCCCCCCCCCCCCCCCCCCCCCCCCCC.CCCCC....CCCCCCCCC..................................................AAAAAAAA.AAAAAAAAA....AAAAAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAA.............................................................................................................................................BBBBBBBBBBBB.B..BBBB.B.BBBBBBBBBBB..............................................................................CCCCCCCCCCCCCCCCCCCCCCCCCC....CCCCCCC....................................................AAAAAAAAAAAAAAAAA.....AAAAAAAAAAAA......AAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....A............................................................................................................................................BBBBBBBBBBBBB...BBBB.B.BBBBBBBBBB.B.....................................................................................CCCCCCCCCCCCCCCCCC...CCCCC..................................................AAA..AAAAAAAAAAAAA.AA......AAAAAAAAAAAAA......AAAAAAAAAAAAAAAAAAAAAAAAAAAAA..A............................................................................................................................................BBBBBBBBBBBBBBB..BBBB.BBBBBBBBBBBBBBB......................................................................................CCCCC.....CCCCCCCCCCCCCC.................................................AAAAAAAAAAAAAAAAAAA..AA.....AAAAAAAAAAAAAAA.....AAAAA..AAAAAAAAAAAAAAAAAAAAAAAAA...........................................................................................................................................BBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBBBBBB.......................................................................................CCCC.....CCCCCCCCCCCCCCC................................................AAAA.AAAAAA.AAAAAAAAAAAA.....AAAAAAAAAAAAAAA.....AAAAA...AAAAAAAAAAAAAAAAAAAAAAAA...........................................................................................................................................BBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBBBB........................................................................................CC.......CCC.CCCCCCCCCC.................................................AAAAAAAAAAA.AAAAAAAAAAAA......AAAAAAAAAAAAAA.....AAAAA...AAAAAAAAAAAAAAAAAAAAAAAAA...........................................................................................................................................BBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBBBB..................................................................................................C.CCC.CCCCCCCC...............................................AAAAAAAAAAAA..AAAAAAAAAAA....AAAAAAAAAAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA...........................................................................................................................................BBBBBBBBBBBBBB...BBBB..BBBBBBBBBB......................................................................................................CCC.CCCCCCC.............................................AAAAAAAAAAAAAAA..AAAAAAAAAAA.....AAAAAAAAAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..............................................................................................................................................BBBBBBBBBBB......BBB.BBBBBBBBB...B..................................................................................................CC.C.CCCC.CCC...........................................AAAAAAAAAAAAAAAAA..AAAAAAAAAAA....AAAAAAAAAAAAAAAA....AAAAAAAAAAAAAAAAAAAAAAAA.A.AAAAA..............................................................................................................................................BBBBBBBBBB...BB.BBBB..BBBBB....BBB..................................................................................................CC.C.CCC................................................AAAAAAAAAAAAAAAA..AAAAAAAAAAA.....AAAAAAAAAAAAAAA....AAAAAAA...AAAAAAAAAAAAAAAA.A.AAAAAA...................................................................................................................BB.......BBBBB............BBBBBBBBBB.B...BBBBBBBBBBBBBBBBBB..........................................................................................................................................................AAAAAAAAAAAAAAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....AAAAAA.....AAAAAAAAAAAAA...AAAAAAA..................................................................................................................BBBB...BBBBBBBBBB...........BBBBBBBBB.BB.BBBBBBBBBBBBBBBBBBB..........................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....AAAAA...AAAAAAAAAAAAA..A..AAAAAAA..................................................................................................................BBBBBBBBBBBBBBBBB............BBBBBBBB...BBBBBBBBBBBBBBBBBBB...........................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA......AAAAAA..AAAAAAAAAAAA..A.AAAAAAAAAA.................................................................................................................BBBBBBBBBBBBBBBBB.............BBBBBBBBBBBBBBBBBBBBBBBBBBB.............................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAA..........AAAAAAAAAAAAAAAAAAAA..A.A...AAAAAAA.................................................................................................................BBBBBBBBBBBBBBBBBB............BBBBBBBBBBBBBBBBBBBBBBBBBBBB.B........................................................................................................................................................AAAAAAAAAAAAA..AAAAAAAAAAAAAAAAA...AAAAAAAAAA...........AAAAAAAAAAAA..AAAAA.....A.AA.AAAAAA.................................................................................................................BBBBBBBBBBBBBBBBBBB............BBBBBBBBBBBBBBBBBBBBBBBBBBBBB................BBBB......................................................................................................................................AAAAAAAAA.A...AAAAAAAAAAAAAAA....AAAAAAAAA..............AAAAAAAAA...AAAAA..AA..A...A..AAA.................................................................................................................BBBB.......BBBBBBBBBB...........BBBBBBBBBBBBB....BBBBBBBBBBBB..............BBBBBBBB.....................................................................................................................................AAAAAA........AAAAAAAAAAAA.....AAAAAAAA...............AAAAAAAAA..AAAAAAAAAAAA.A.A...AAAA.....................................................................................................................BBBBBBBBBBBBBBBBB..........BBBBBBBBBBBB.....BBBBBBBBBBBB............BBBBBBBBBBB.....................................................................................................................................A.A..........A.AAAAAAAAA......AAAAAAAA....AAAAAA.....AAAAAAAA...AAAAAAAAAAAAA.A....AAAA.................................................................................................................BBBBBBBBBBBBBBBBBBBBBB.........BBBBBBBBBB.BB.....BBBBBB.BBBBBBB...........BBBBBBBBB.........................................................................................................................A........................A..AAAAAAAA..AAA.AAAAAAA....AAAAAA.A....AAAAAAA.A..AAAAAAAAAAAAAA.A...AAAAA................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBBB.B......B.BBBBBBBBBBBBB...........BBBBBBBBB......................................................................................................................AAAAAAAA...................A..AAAAAA....AAAAAAAAAA......AAAAAAA.....AAA.AA...AAAAAAAAAAAAAAAA....AAAAA................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBBBBB...BBBBBBBBBBBBBBBB..............BBBBBBBBBB...................................................................................................................AAAAAAAAAAA..................AAAAAAAA....AAA...AAA.....A.AAAAAAA...........A..AAAAAAAAAAAAAAAA....AAAAA................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBBBBB.BB.BBBBBBBBBBBBBB................BBBBBBBB...................................HHHH...........................................................................AAAAA..AAAAAAA..........AA..AA.AAAAAAA..........AAA.....AAAAAAAAA...............AAAAAAAAAAAAAAA....AAAA.................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBBBBBBBBBBBBBBBBBBBBBB..................BBBBBBBBB.........................HH......HHHHHH..........................................................................AAAAA....AA..AA.A...A.AAAA.AAAAAAAAAAA.................AAAAAAAAAA...............AAAAAAAAAAAAAAA....AAAA.................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBB.BBBB.BB.BBBBBBBBB......................BBBBBBBBB.......................HHH.....HHHHHHH..........................................................................AAAAAA..AAA..AAAA..AAA.AAAAAAA.AAAAAAA.A...A...........AAAAAAAAAA................AAAAA...AAAAA.....AAAA.................................................................................................................BBBBBBBB.B.BBBBBBBBBBBBB.......BBBBB.BBBBBBBBBBBBBBBBBBBB........................BBBBBBBB.....................HHHH....HHHHHH.H.........................................................................AAAAAAAAAAAAA.AAAA..AAAAAAAAAAAAAAAAAAAAA.A.A...........AAAA...AAA................AAAAAA............AAAA.................................................................................................................B.BBBBB.BBB.BBBBBBBBBBBB......BBBBB....BBBBBBBBBBBBBBBBBBB........................BBBBBBB...................HHHHHHH..HHHHHHHHH.........................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAA.AAA.....AAAAA..AAAAA..AAAAAA.AAA..AAAAAAAA...........AAAA.................................................................................................................BBBBBBBBB..BBBBBBBBBBBB.......BBBBBBB...BBBBBBBBBBBBBBBB.BB............BB..........BBBBBB..................HHHHHHHHHHHHHHHHH..........................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAA....AAAAA....AAAA.AA.AA.AAAAAAAAAAAAAA.A..........AAAA..................................................................................................................BBBBBB.BB.BBBBBBBBBBBBB......BBBBBBBBB..BBBBBBBBBBBBBBB..B.BBBBBB..BBBBBB..........BBBBBB.................HHHHHHHHHHHHHHHHH.......................................................................A..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAAAAA....AAAAAAAAAAAAAAAAAAAAAAA.A...AA...A..AAAA.................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBBB.....BBBBBBBBBB..BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBB................HHHHHHHHHHHHHHHHHHHH.....................................................................A..A.AAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAAAAAA....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.................................................................................................................BBBBBBBBBBBBBBBBBBB.BBBB.........BBB..BBBBBBBBBBB..BBBBBBBBBBBBBBBBBBBBBBBBBBBBB......BBBBBB....................HHHHHHHHHHHHHHHH.H..................................................................A.AAA.AAAAAAAAAAAAAAA..AA.AAAAAA.AAAAA.AAAAAAAAAAAAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA................................................................................................................B.BBBBBBBBBBBBBBBBBBB..............BB...BBBBBB.B...BBBBBBBBB.BBBBBBBBBBBBBBBBBBBBB.....BBBBBB....................HHHHHHHHHHHHHHHHH....................................................................AA..AAAAAAAAAAAAAAAA..AA.AAAAAA.AAAA...AAAA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.................................................................................................................B.BBBBBBBBBBBBBBB..................BBBB.BBBBBBB.B..BBBB.BBBBBBBBBBBBBBBBBBBBBBBBBB......BBBBB.....................HHHHHHHHHHHHHHHHH.HHH...............................................................AAA.AAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAA....AAA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..................................................................................................................B.BBBBBBBBBBBBB...................BBBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBBBBBBBBBBBBB......BBBBB....................HHHHHHHHHHHHHHHHH...................................................................AAAAAAAAAAAAAAAAAAAA..A.AAAAAAAAAAA....AAAA....AAAAAAAAAAAAAAAAAAAAAAA.A..AAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AA.................................................................................................................BB..BBBBBBBBBBB..............BBB....BBBBBBBBBBBBBBBB.BB.BBB...BBBBBBBBBBB.BBBBBBBB......BBBBB...................HHHHHHHHHHHHHHHHH..HHH...............................................................AAAAAAAAAAAAAAAAAAAAA.AAAAAAAAA.AA.....AAAA.....AAAAAAAAAAAA.A....AA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.................................................................................................................B....BBBBBBBBBB...........BBBBBBB....BBBBBBBBBBBBBBBBB..BB...BBBBBBBBBBBB..BBBBBBBB.....BBBBB...................HHHHHHHHHHHHHHHHHHH..H.................................................................AAAAAAAAAAAAAAAAAAA.AA.AAAA.AAAA....AAAAA.....AAAAAAAAAAA..............A.AAAAAAAAAAAAAAAAAAAA.AAAAAAAAA.......................................................................................................................BBBBBBBBB........BBBBBBBBBBB....BBBBBBBBBBBBBBBBBBBBB..BBBBBBBBBBBBBB.BBBBBBBB.....BBBB....................HHHHHHHHHHHHHHHHH.HH.HH................................................................AAAAAAAAAAAAAAAAAAA..AAAAAAAAAAA.....A.A.....AAAAAAAAAAA................AA..AAAAAAAAAAAAAAAAA.AAAAAAAAA.......................................................................................................................BBBBBBBBB.......BBBBBBBBBBBBB....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBB...BBBBB...................HHHHHHHHHHHHHHHHHHHH...................................................................AAAAAAAAAAAAAAAAAAAA.....AAA..AAAA............AAAAAAAAAAA....................A..AAAAAAAAAAAAAAAAAAAA.AAA.......................................................................................................................BBBBBBBBB......BBBBBBBBBBBBBB...........BBB.BBBBBBBB.BBBBBBBBBBBBBBBBBB..BBBBBBB.BBBBBB...................HHHHHHHHHHHHHHHHHH......HH..............................................................AAAAAAAAAAAAAAAAAAAA......AAAAAAAA..........AAAAAAAAAAAA......................AAAAAAAAA.AAAAAAAAAAAAAAA.......................................................................................................................BBBBBBBBB.....BBBBBBBBBBBBBBBB.......B.B.B..BBBBB..BBBBB.BBBBBBBB..BBBBBBBBBBBBBBBBBBBB...................HHHHHHHHHHHHHHHHHHHHHHH.................................................................AAAAAAAAAAAAAAAAAAAAAAA........AAAA........AAAAAAAAAAA........................AAAAAAAAAAA.AAAAAAAAAAAAAA......................................................................................................................BBBBBBBB......BBBBBBBBBBBBBBBBB...B..B....BBBBBBBBBBBBBBB.BBBBBB...BBBBBBBBBBBBBBBBBBBB..................HHHHHHHHHHHHHHHHHHHHHHHHHHH..............................................................AAA.AAAAAAAAAAAAAAAAAAA.A.......AAAAAAAAAAAAAAA.AAAAAA..........A..............AAAAAAAAAAAAAAAAAAAAAAAA......................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..BBBBB.BBBBBBBBBB...BBBBBB..BBBBB.BBBBBBBBBBBBBBBBBBBBBB...................HHHHHH......HHHHHHHHHHHHH...H............................................................AAA.AAAAAAAAAA..AAAAAAA.A.........AAAAAAAAAAAA..AAAAA........AA..............AAAAAA.....AAAAAA.AAAAAAA.....................................................................................................................BB.BBBBBBBBBBBBBBBBBBB..BBBBBBB..BBBBBBBBBBBBBBBB.....BBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBBB.......................HH........H..HHHHHH.......H............................................................AAAAAAAAAAA.....AAAAAAA..AA.AA........AAAA......AAAAAA.....A.................AAAAA......A.....AAAAAAA.....................................................................................................................BBBBBBBBBB.BBBBBBBBBBBBBBBBBBB...BBBBBBBBBBBBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBB....BBBBBB....................................HHHHH....HHHHH...................................FF......FF..............AAAAAAAA........AAAAAA.A.A.AAAAAAA.A..AAAA.....AAAAAAAAAA...AAA.A.AAA........AAAAA............AAAAAAA.....................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBB.BBBBB..BBBBBBBBBBBBBBBB......BBBBBBBBBBBBBBBBBBBBBBBBB....BBBBBB.....................................HHHHHH..HHHHH...................................FFFF.FFFFF.................AAAA.........A.AAAAAAAAAAAAAAAAAA.AAAAAAA...A..AAAAAAAAAAAAAAAAAAAAA...AAAAAAA.............AAAAAAAA......................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBBBBB...BBBBBBBBBBBBBBBBB..........BBBBBBBBBBBBBBBBBBBB.....BBBBBB...................................HHHHHHHHHHHHHH...................................FFFFFFFFFFFFFF..............AAA..........AAAAAAAAAAAAAAA.AAAAAAAAAAA.AAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.............AAAAAAA......................................................................................BB.BBB........................B..BBBBBBBBBBBBBBBBBBBBBBBBBBB...BBBBBBBBBBBBBBBBB.......BB..BBBBBBBBBBBBBBBBBB......BBBBBB...................................HHHHHHHHHHHHHH....................................FFFFFFFFFFFFF..........................AAAAAAAAAAAAA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..........AAAAAAA.....................................................................................BBBBB...BB.....................BBBBBBBBBBBBBBB..BBBBBBBBBBBBBBB..BBBBBBBBBBBBBBBBB......BBBB..BBBBBBBBBBBBBBBBBB....BBBBBBB..................HHHHH........H...HHHHHHH...HHHHH..................................FFFFFFFFFFFFFF.........................A.A.AAAAAAAAAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.......AAAAAA......................................................................................BBBBBBBBBB.....................BBBBBBBBBBBBBBBB...BBBBBBBBBBBB..BBBBBBBB...BBBBB..B.....BBBBBBBBBBBBBBBBBBBBBBBB.....BBBBB..................HHHHHHHH.....HH..HHHHHHHH.HHHHHH.................................FFFFFFFFFFFFFFF...........................AAAAAAAA.AAAAAAAAAAA.AAAAAAAAAAAAAAAA.AA.A.AAAAA.AAAAAAAA....AAAAAAAAAA....AAAAAAAAA....................................................................................BBBBBBBBBBB.......................BBBBBB..BBBBBBBB...BBBBBBB.BB..BB..BBB.....BBBB........BBBBBBBBBBBBBBBBBBBBBB.BB.BBBBBBBB................HHHHHHHHHHH...HHHH.HHHHHHHH.HHHHHHH................................FFFFFFFFFFF..FF.FF........................A.AAAAA..AAAAAAAA.A.A..AAAAAAAAAAAAAA......AAAA......AAAA..AA....AAAAAAAAAAAAAAAAAAAA..................................................................................BBBBBBBBBBBBBB..................BBBBBBBB...BBBBBBBBB..BBBBBBBBBB..BB..BBB.....BBBBBBB.....BBBBBBBB.BBBBBBBBBBBBBBBB.B.BBBBBB................HHHHHHHHHHH...HHHHHHHHHHHHHHHHHHHHH................................FFFF.FFFFFF...FFFF.........................AAAAA....AAAAAAAAA....AAAAAA..AAAA........AAAA.....AAAA.....AAA..AAAAAAAAAAAAAAAAAAA..................................................................................BBBBBBBBBBBBBBB................BBBBBBBBB..BBBBBBBBBB..BBBB.BBBBB..BB.BBBB....BBBBBBB......BBBBBBBB..BBBBBBBBBBBBBBBBBBBBBBBB.................HHHHHHHHHH...HHHHHHHHHHHHHHHHHHHH.................................FFFFFFFFFFFF..FFFF.........................AAAAA...AAAAAAAAAAAAA.A...AA..AAA.......A.AAAAAAA...AAAAA....A..AAAAAAAAAAAAAAAAAAAAA.................................................................................BBBBBBBBBBBBBBB................BBBBBBBBB..BBBBBBBBBB.BBBBBBBBBBB..BBBBBBBB..B.BBBBBB......BBBBBBBBB..BBBBBBBBBBBBBBBBBBBBBB...................HHHHHHHH....HHHHHHHHHHHHHHHHHHHH................................FFFFFFFFFFFFFFFFFF..........................AAAA...AAAAAAAAAAAAAAA....AAA..A......AAAAAAAAA....AAAAAAAAA....AAAAAAAAAAAAAAAAAAA....................................................................................BBBBBBB.BBBBBB................BBBBBBBBB..BBBBBBBBBBBBBBBBBBBBBB..BBBBBBB.BBBBBBBBBB........BBBBBBBB.BBBBBBBBBBBBBBBB..BBB.....................HHHHHHHH...HHHHHHHHHHHHHHHHHHHH................................FFFFFFFFFFFFFF..............................A..A..AAAAAAAAAAAAAAAAAA..AAAA.........AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA....AAAAAAAAAAA....................................................................................BBBBB.BBBBBBB.................BBBBBBBBB..BBBBBBBBBBBBBBBB..BB....BBBBBBBBBBBBBBBBB........BBBBBBBB..BBBBBBBBBBBBBBBBBBB.......................HHHHHHH..HHHHHHHHHH.HHHHHHHHHH................................FFFFFFFFFFFFF..FFF.F........................A......AAAAAAAAAAAAAAAAA..A.AA.A.......AAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....AAAAAAAAAAA.....................................................................................BBBBB.BBBBBBB.................BBBBBBB...BB...BBBBBBBBBB........BBBBBBBBBBBBBBBBB......BBBBB.BBBBB..BBBBBB.B.BBBBB.BBB........................HHHHHHHHHHHHHHHHHHHHHHHHHHHHH.................................FFFFFFFFFFFFF..F.FFFFFFFFFF.......................AAAAAAAAAAAAAAAAAA....AAAA........AAAAAAAAAAAAAAAAAAAAAAAAAAA.A....AAAAAAAAAAA......................................................................................BBBB.BBBBBBB.................BBBBBBBB........BBBBBBBB.........BBBBBBBBBBBBBBBBB....BBBBBBBB.BBB...BBBBBBBB.BBBBB.B..........................HHHHHHHHHHHHHHHHHHHHHHHHHHH.........................................FFFFFF..FFFFFFFFFFFFFF......................AAAAAAAAAAA.AAAAAA..A..AA..........AAAAA.AAAAAAAAAA..AAAAAAA.......AAAAAAAAAA.......................................................................................BBBBBBBBBBBB.............B...BBBBBBBBB..BBBBBBBBBBBBBB........BBBBBBBBBBBBBBBBB....BBBBBBBBB......BBBBBBB........B.........................HHHHHHHHHHHHH...HHHHHHHHHHHHH........................................FFFFFF..F.FFFFFFFFFFFF.....................AAAAAAAAAA...AAAAAA.....A...............A.AAAAAAAAA.......AA.........AAAAAAAAAA......................................................................................BBBBBBBBBBBBB.......BBBBBBBBBBBBBBBBB...BBBBBBBBBBBBBBBBBB....BBB.BBBBBBBBBBBB....BBBBBBBBBB.......BBBBB..................................HHHHHHHHHHHHH.....HH..HHHHHHHHH....................................FF....FFFFFFFFFFFFFFFFFFFF......................AAAA.........AAAA........................AAAAA..A.........A.......AAAAAAAAAA........................................................................................BBBBBBBBBBB.B......BBBBBBBBBBBBBBBBB....BBBBBBBBBBBBBBBB........BBBBBBBBBBBB.....BBBBBBBBBB.....BBBBBBB...................................HHHHHHHHHHHH.........HHHHHHHHH....................................FF....FFFFFFFFFFFFFFFFFFFF......................AAAA.....................................AAAAAA...................AAAAAAAAAAA.......................................................................................BB.BBBBBBBBB......BBBBBBBBBBBBBBBBBBB..BBBBBBBBBBBBBBBBB........BBBBBBBBBBBB.....BBBBBBBBBB....B.BBBBBBBBBB................................HHHHHHHHHH.......HHHHHHHHHHHH....................................FFFFFFFFFFFFFFFFFFFFFFFFF.......................AAAA.....................................AAAAA...................AAAAAAAAAAAA...........................................................................................BBBBBBBB....B.BBBBBBBBBB...BBBBBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBB.......BBBBBBBBB......BBBBBBBBBBBB..............................HHHHHHHHHHH.........HHHHHHHHHH....................................FFFFFFFFFFFFFFFFFFFFFFFFF.....................AAAAAAAA...................................AAAAAA..................AAAAAA..AAAA...........................................................................................BBBBBBBB...BBBBBBBBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..BBBBBBBB.......................BBBBBBBBBBBBBB.....B......................HHHHHHHHHHH...........HHHHHHHH.H...................................FFFFFFFFFFFFFFFF.FFFFFFF.....................AAAAAAAAA................................AA.AAAAAA.A....AAA........A.AAAAAAAAAAAA..........................................................................................B.BBBBB.....BBBBBBBB.B......BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..........................BBBBBBBBBBBBB..B.B.....................HHHHHHHHHHHHH............HHHHHHHH.....................................FFFFFFFFFF.FFF.F..FFFF..............AA.A....AAAAAAAAA..AAA...........AAAAA..........AAAAAAAAAAAAAAAAAA.........AAAAAAAAAAAAAA........................................................................................BB.BBBBB.....BBBBBBB............BBBBBBBBBB..BBBBB.B.BBBBBBBBBBBBBBBB...........................BBBBBBBBBBBBBBBB.B......................HHHH.HHHHHH.............HHHHHH......................................FFFFFFF.F...FFFF...F................AAAAAAAA.AAAAAAAA..AAAAAA.......AAAAAAA..........AAAAAAAAAAAAAAAAAAA........AAAAAAAAAAAAAA........................................................................................BBB.BBBB....BBBBB..............BBBB...B......B......BBBBBBBBBBBBBBB...........................BBBBBBBBBBBBBBBBBB......................HHH...HHHH...............HH........................................FFFFFFFF..FFFFFFF...................AAAAAAAAA.AAAAAAAA..AAAAAA......AAAAAAAAA........AAAAAAAAAAAAAAAAAAA.........AAAAAAAAAAAAAA..................................................................................BBB.BBBB.BBBBBB..BBBBBB.............................B.......BBBBBBBBBBB...........................BBBBBBBBBBBBBBBBBBBB......................HHH....HHH..............HH........................................FFFFFFFFF...FFFF.F...................AAAAAAAAAAAAAAAAAAAAAAAAA......AAAAAAAAAAA...AAAAAAAAAAAA.AAAAAAAAAA.......AAAAAAAAAAAAAAAA...................................................................................BBBBBB.BBBBBBBBBBBBBBB....................................BBBBBBBBB.B..........................BBBBBBBBBBBBBBBBBBBBBB.........................................................................................FFFFFFFF....FFF......................AAAAAAAAAAAAAAAAAAAAAAAA.......AAAAAAAAAA...AAAAAAAAAAAAAAAAAAAAAAA.....AAAAAAAAAAA.AAAAAA.....................................................................................BBBBBBBBBBBBBBBBBBBB......................................BBBBBB...........................BBBBBBBBBBBBBBBBBBBBBB...........................................................................................FFFFFFFF............................AAAAAAAAAA.AAAAAAAA.AAAAA.......AAAAAAAAAA....AAAAAAAAAAAAAA.AAAAAA.....AAAAAAAAAAAAAAAAA........................................................................................BBBBBBBBBBBBBBBBB..........................................BB.....................B.BB.BBBBBBBBBBBBBBBBBBBBBBBB...........................................................................................FFFFFFFFFF...........................AAAAAAAAA..AAAAAAA..AAAAA........AAAAAAAAAA.AAAAAAAAA...AAA...AAAA.....AAAAAAAAAAAAAAAAA..........................................................................................BBBBBBBBBBBBBBB....................B.B.............................BB..........B.BBBB.BBBBBBBBBBBBBBBBBBBBBBBBB..........................................................................................FFFFFFF.FFFF.F..F....................AAAAAAAAA....AAA....AAAAA........AAAAAAAAAAAAAAAAAAAA..AAAA...A.A......AAAAAAAAAAA..AAAAAAA.........................................................................................BBBBBBBBBBBBBB.................BBBBBBB...........................BBBBB......BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB............................................................................................FFFFFFF.FFF..FF........................AAA.......AA......AAA.........AAAAAAAAAAAAAAAAAAAAAAAAAA...........AAAA.AAAAAAAAAAAAAA............................................................................................BBBBBBBB.BBBB..................BBBBBBB........................BBBBBBBBB..BBBBBBBBBBB.BBBBBBBBBBBBBBBBBBBBBBB...........................................................................................FFFFFFFFFFFFFFF....................AA..AA......AAAAAAA.AAAA..........AAAAAAAAAAAAAAAAAAAAAAAAAAAA.............AAAAAAAAAAAAAAA.............................................................................................BBBBBBBBBBBBBB................BBBBBBB.......B...............BBBBBBBBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.............................................................................................FFFFFFFFFFFFFFF...................AAAAAAAAAAAAAAAAAAAAAAAAA........AAAAAAAAA.......AAAAAAAAAAAA............AAAAAAAAAAAAAAAA..............................................................................................B.BBBBBBBBBBB.................BBBBBB.......B................BBBBBBBBBBBBBBBBB.BBBBBB.......B...B...BBBBBBB..............................................................................................FFFFFFFFFFFFFF....................AAAAAAAAAAAAAAAAAAAAAAAA........AAAAAAAAA.......AAAAAAAAAA.............AAAAAAAAAAAAAAA........................................................................................................BBBBB...............B.BBBBBBBBBB...B.................BBBBB.BBBBBBBBBBBBBB...............B......BB................................................................................................FFFFFFFFFFFFF....................AAAAAAAAA.AAAAAAAA.AAAAA........AAAAAAAAA........AAAAAAAAAA.......AAAAA.AAAAAA.AA.AAAAAA......................................................................................................BBBBB.......B..........BBBBBBBBBBB.BB.................B.BBBBBBBBBBB.BBBB.........................................................................................................................FFFF..FFFFFFF...................AAAAAAAAA..AAAAAAA..AAAAA.........AAAAAA...........AAAAAAAA......A..AAAAAAAAAA....AAAAAAAA.....................................................................................................BBBBBB......BBB........BBBBBBBB...BBBBBBBB..............BBBBBBB.BBBBBBB...................................BBBBB.................................................................FFFFF...................FFFFF..................AA.AAAAAAAA..AAAAAA...AAAAA.........AAAA.............AAAAAAA......AAAAAAAAAAAAA.......AAAAAAAAA.................................................................................................B.BBBBB......BBB.B.B...BBBBBBBBBBBBBBBBBBBB.............B.BBBBBBBBBBBBB..........B........................BBBBBBB................................................................FFFFFFFF.................FFFF..................AAA..AAAAAA..AAAAAA...AAAAA......A..AAAA.............AA...........AAAAAAAAAAAA.......AAAAAAAAAAA................................................................................................BBBBBBBB......BBB.B....BBBBBBBBBBBBBBBBBBBBB............BBBBBBBBBBB.BB..........BBBBBBBBB................BBBBBBBB...................................KK..........................FFFF.F.FFFF...............FFFFF.................AAAAAAAAAAA..AAAAAA...AAAAA......AAAAAAAAA..........A.....A.....A..AA.....AAAA.......AAAAAAAAAAA.............................................................................................B.BBBBBBBBB......BBB.......BBBBBBBBBBBBBB.BBBBBB..........BBBBBBBBBBBBB..........BBBBBBBB..BB............BB.BBBBBBB...................................K.KKKK..................FFFFFFFF.FFFFFFF...............FFFFF.................AAAAAAAAAA......................AAAAAAAAAA.............AAAA.....AAAAA.....AAAAAA......AAAAAAAAAA..............................................................................................B.BBBBBBBB.......BB.......BBBBBBBBBBBBB..BBBBBBB.........BBBBB.BBB.BBB..........B.BBBBBBBBBB...........BBBBBBBBBBB....................K.K.....KKKK....K.KKKK................FFFFFFFFFFFFFF.FF...............FFFFF.................AAAAAAAAAA......................AAAAAAAAA............AAAAAAAAA.AAAAAA.....AAAAAA.......AAAAAAAA.............................................................................................BBBBBBBBBBBBBB.....BBB.....BBBBBBBBBB.BBBBBBBBBBBB........BBBBBBBBBBBBB..........BBBBBBBBBBBBB...........BBBBBBBBBBB...................KKK......KKKK....KKKKKK................FFFFFFFFFFF.F.FF................FFFFF.................AAAAA...A.......................AAAAAAAA..........A...AAAAAAAAAAAAAAAA...AAAAAAA.......AAAAAA...............................................................................................B.BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB........BBBBBBBBBBBBB..........BBBBBBBBBBBBBBB.........BBBBBBBBBBBBBB.................KKKK...KKKKKK.....KKKK................FFFFFFFFFFFFF.F..................FFFFF.................AAAAA............................AAAAAAA.......AA.A....AAAAAAAAAAAAAAAAAAAAAAAAA........AAAA................................................................................................BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB...BBBBBBB.BBBB............BBBBBBBBBBBBBB...........BBBBBBBBBBBBBB..................KKK....KKKKKKK...KKKK................FFFF..FFFFFFF.............FFF..FFFFFFFF................AAA............A..A.........AAAAAAAAAAAA.......AAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAA............................................................................................................BBBBBBBBB..BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..BBBBBBBBBBBBBB...........BBBBBBBBBBBBBBBBB...........BBBB.BBBBB....................KKK...KKKKKKKK..KKKKK.................FFFFFFFFFFFFF.........F..F.FFFFFFFFFF..................A............AAAAAA........AAAAAAAAAAAA.....AAAAAAAAAAAAA..AAAAAAAAAAAAAAAAAAAA.............................................................................................................BBBBBB......BBBBBBBBBBBBBBBBBBBBBBBBBB.BB.BBBBBBBBBBBBBBBBBBBBBBBB..........BBBBBBBBBBBBBBBBBB..........BBBB..BBBB.....................KKKKKKKKKKKKKKKKKKKK..................FFFFF.FFFFFFFFF..FFFFFFFF..FFF.FFFFF...................A........AAAAAAAAAAAAA.....AAAAAAAAAAAA....AAAAAAAAAAAAAA..AAAAAAA.AAAAAAAAAAA.............................................................................................................BBBBBBBBBBB...B.....BBBBBBBBBBBBBBBBBBBBB...BBBBBBBBBBBBBBBBBBBBBBBB........BBBBBBBBBBBBB.BBBBBBB.......B.BBBB..BBB.....................KKKKKKKKKKKKKKKKKKK....................FFFFFFFFFFFFFFFFFFFFFFFFFFFF...FFFFF..................A.......AAAAAAA.A.AAAAAA...AAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAA....AAAAAAAAAA..............................................................................................................BBBBBBBBBBB..B......BBBBBBBB.B.BBBBBB.BB.....BBBBBBBBBBBBB...BBBBBB........BBBBBBBBBBBBBBBBBBBB.......BBBBBBBB.BBB.....................KKKKKKKKKKKKKKKKKKK.....................F.F.FFFFFFFFFFFFFFFFFFFFFFFF.FFFFF..........................AAAA.A.........AA....AA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....AAAAAAAAAA.............................................................................................................BBBBBBBBBBB.B.......BBBBBBBBBBBBBBBBBB.........BBBBBBBBBB....BBBBBBB.......BBBBBBBBBBBBB...BBBBB......BBBBBBBBBBBB....................K..KKKKKKKKKKKKKKKKK.KK......................FFFFFFFFFFFFFFFFFFFFFFFFFFFFF..........................AAA.............AA........AAAAAAAAAAAAAAAAAAAAAAA..AAAAAAAAA..AAAAAAAAAAA.............................................................................................................BBBBBBBBBBB........BBBBBBBBBB..BBBBBB..........B..BBBBBB....BBBBBBBB.......BBBBBBBBBBBBB....BBBB.......BBBBBBBBBBB.......................KKKKKKKKKKKKKKKKKKKKKKK...................FFFFFFF..FFFFFFF.FFFFFFFFFFFFFF.......................AAA...A.A..A...............AAAAAAAAAAAAAAAAAAAAAAA...AAAAAAAAAAAAAAA..AAAAA.............................................................................................................BBBB.BB..........BBBBBBBBB....BBBBBB.........B..B.BBBB.....BBBBBBBB.......BBBBBBBBBBBB.....BBBB.......BBBBBBBBBBB.....................K.KKKKKKKKKK.KKKK.KKKKKKKK................FFFFFFFF....FFFF..FFF..FFFFFFFFFFF......................AA.....AAAAAA.......A......AAAAAAAAA..AAAAAAAAAAAA....AAAAAAAAAAAAA...AAAAA..............................................................................................................BBB..............BBBBBBBB.....BBBB.............BBBB.B....BB.BBBBBBBB.B.BBBBBBBBBBBBBB.....BBBBBB.......BBBBBBB.......................KKKKKKK......KKKK.KKKKKKKK................FFFFFFFF..FFFFF..F......FFFFFFFFFFFF.................AAAAAA....AAAAAAAA.....AA................AAAAAAAAAAA....AAAAAAAAAAAAAA..AAAAAA............................................................................................................................BBBBBBBBBBB.....BBBB..............BB.......BBBBBBBBBBBBBBBBBBBBBBBBBBB......BBBBBBBB.....BBBBBBB.......................KKKKKK........KKKKKKKKKKKK................F..FFFF....FFF..........FFFFFFFFFFFF...............AAAAAAA.....AAAAAAAAA.....AAAAA.............AAAAAAAAAA.....AAAAAAAAAAAAAA.AAAAA............................................................................................................................BBBBB.BBBBBBBBB...BB......................BBBBBBBBBBBBBBBBBBBBBBBBBBBB........BBBBBBBB.......BBBBB.......................KKKKK........KKKKKKKKKK.....................FFF....F.FFF........FFFF.FFFFFFFF................AAAAAAAA....AA.AAAAA......AAAAA............AAA..AAAA.....AAAAA....AAAAAAAAA.................................AAAAAAAA.....................................................................................BBB...BBBBBB.B..............................B.BBBBBBBBBBBBBBBBBBBBBBBB..........BBBBBBB......BBBBBBB.......................K.K........KKKK.KKKK......................FFFF.....FFF..........F..FFFFFFFFF...............AAAAAAAAA.......AAAAAA.AA....AAAA.................AAAA.....AAA.....AAAAAAAAA.................AAAAAAAA.......AAAAAAAAAA...................................................................................BBBB.BBBBBBB.BB.B.............BBBBBB.........BBBBBBBBBB...BBBBBBBBB.BBB..........BBBBBBB......BBBBBBB...................................KK..K................................FFFFFF..........FFFFFFFFFFF.............AAAAAAAAAA......AAAAAAA..AA.AAA.AAAAAAA.AA.........AAAAA.............AAAAAAA....................AAAAAAAA......AAAAAAAAAAA..................................................................................BBB..BBBBBB...BB..............B..BBBBBB.B...BBBBBBBBBB....BBBBBBBB...B..........BBBB.BBBB.....BBBBBBBBBB.......BB........................KK....................................FFF.........FFFFFFFFFFFFF............AAA..AAAAA........AA.AAAAAAA..AAA...AAAAAAAA........AAAAA............AAAAA...............AAAA.A....AAAAAAAA....AAAAAAAAAAA...................................................................................BBBB..BBB....................BBBBBBBBBBBBBBBBBBBBBBBB....BBBBBBBB.............BBBBB..BBBB...BBBBB.BBBBBBBB...BBB............................................................FFFF..........FFFFFFFFFF.FF...........AA...................AAAAAAAAAAAAAAA....AA.AAA.......AAAAAA..........AAAAAA.............AAAAAAAAA...AA.AAAAA....AAAAAAAAAAA...................................................................................BBBBBBBB........B...........BBBBBBBBBBBB...BBBBBBBBBB....BBBBBBBBBB.........BBBBBBBBBBBB....B.BBBBB.BBBBBBBBBB.B......................KK.K...................................FFF.........FFFFFFFFFFFF.F...........AA..........A...A.AA.AAAAAAAAAAAAAAAAA.A...A..A.....AAAAAAA...........AAAAA.............AAAAAAAAAA....A.AAAAA..AAAAAAAAAAAA.................................................................................BBBBBBBBBB..BBBBBB..B.....B.BBBBBBBBBBBBBBBBBBBBBBBBBBB....BBBBBBBBBB.........BBBBBBBBBBBBBBB.BBBBB...BBBBBBBBBBBBB......................KK....................................FFF........FFFFFFFFFFFFFF...........AA.........AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA....A.A....AAAAAAAA.........AAAAAA.............AAAAAAAAAA..AA.A.AAAAAAAAAAAAAAAAAA.................................................................................B.BBB.BBBB.BBBBBBB..B.....BBBBBBBBBBBBBBBB...BBBBBBBBBB....BBBBBBBB..........BBBBBBBBBBBBBBBBBBBBB......BBBBBBBB..BBB..........................................................FFF........FFFFFFFFFFF..F...........AA....AA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..............AAAAAAA...A..AAAAAAAAA.AA.........AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA...................................................................................BBBBB..BBBBBBBBBBBBB.....BBBBBBB.BBBB.......BB.BBBBBBB......BBBBBBBBB.......BBBBBBBBBBBBBBBBBBBB........BBBBBBBBB..BB..........................................................FFF.......FFFFFFFFFFFFF............AA.....AAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA....A.A........AAAAAA.AAAAAAAAAAAAAAAAA........AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..................................................................................BBBBB....BBBBBBBBBBB.B...BBBBBBBB....B...B.....BBBBBBBB........BBBBBB.......BBBBBBBBBBBBBBBBBBBB.........BB.BBBBBBB.............................................................FFFF....FFFFFFFFFFFFFFF...........AAA...AAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAA..A.A..........AAAAAAAAAAAAAAAAAAAAAAAA.......AAAAAAAAAAAAAAAAAAAAAAAAAAAAA....................................................................................BBB.BBB...BBBBBBBBBB.B...B.B.BBBBBBB...BBBBBBB.BBBBBBBBBB........BBBBBBB....BBBBBBBBBBBBBBBBBBBBBBB..........B.BBBBBB.............................................................FFF.....FFFFF..FFF.F.FF...........AA...AAAAAA.AAA...AA.AAA..AAAAAA.....AAAAAAAAAAAAAAA.........AAAAAAAAAAAAAAAAAAAAAAAA.......AAAAAAAAAAAAAAAAA..AAAAAAAA.....................................................................................BBBBBBBBBBBBB.BBBB.BBB.B..BBBBBBBBBBBBBBBBBBBB.BBBBBBBBBB......BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB............BBB.B..............................................................FFF.....FFFFF......FFF...........AA....AAAAAAAAA....AA......AAAAA.....A.AAAAAAAAAAAAAAAA........AAAAAAAAAAAAA.AAAAAAAAA......AAAAAAAA..AAAAAAAA.AAAAAAAAA.....................................................................................BBBBBBBBBBBBBBBBBBBBBBB....BBB..BBBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBBBBBBBBBBBBBBBB......B.BBBBBBB..........BBB.....................................................F.FFFF.F..FFF....FFFFFFF...FF.FF...........AA...AAAAA.AAAAA..AA..AA....AAA...AAA....AAAAAAAAAAAAAAAAAAAAA.AAAAAAAAA.AA..AAAAAAAA.......AAAAAAA....AAAAAAAAAAAAAAAAA.....................................................................................BBBBBBBBBBBBBBBBBBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.......B.BBBBBBBBBBBBBBBBBBB.........BBBBBBBBB................................................................FFFFFFFFF.FFFF....FFFFFFF....FFF...........AA.....AA.AAAAAAAAAAA.AA.....AAA.....AA....AAAAAAAAAAAAAAAAAAAAAAAAAAAA....A..AAAAAAAA......AAAAAAA.....AAAAAAAAAAAAAAAAA.....................................................................................BB.BBBBBBBBBBBBBBBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB......BBBBBBBBBBBBBBBBBBB..........BBBBBBBBB...............................................................FFFFFFFFFFFFFFF....FFFFFFF...................AA....AAAAAAAAA.AA..AAA....AAAAAA....AAA.A.AAAAAAAAAAAAAAAAAAAAAAAAAAA......AAAAAAAA.......AAAAAAAA..AAAAAAAAAAAAAAAAAAAA....................................................................................BBBBBBBBBBBB.B.BBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBB.B...BBBBB.........BBBBBBBBB...............................................................FFFFFFFFFFFFFFF....FFFFFFF...................A......AAAA.AA.A.A..AA...AAAAAAAAA.....A...AAAAAA..AAA.....AAAAAAAAAAA....AAAAAAAAAA.......AAAAAAAAAAAAAAAA.AAAAAAAAAAAAAA...................................................................................B.BBBBBBBB..BBBBBBBBBB....B.BBBBBBBBBBBBBBBBBB.BBBBBBBBBBBBB.......BBBBBBB.....BBBBBBB.......BB.BBBBBBBBB.............................................................FFFFFFFFFFFFFFF....FFFFFFFF..................A.....AAAAA.AAA.A..AA...AAAAAAAAAA......A..AAAAA............AAAAAAAAAA..AAAAAAAAAAAA........AAAAAAAAAAAAAAAAAAAAAAA..AAAAA......................................................................................BBBBBBB..B.BBB.B.B......BBB..BBBBBBBBBBBBB....BBBBBBBBBBB........BBBBB......BBBBBBB.......BBBBBBBBBBBBBB...........................................................FFFFFFFFFFFFFF....FFFFFFF...................AA...AAAAAAAAAAA...AA...AAAAAAAAAAA......A...AAA.............AAAAAAAAAA..A.A....AAA..........AAAAAAAAAAAAAAAAAAAAAAA.AAAA.........................................................................................BBBBB.B...BB.B.B......BBBB..BBBBBBBB.BBB....BBBBBBBBBBBBBB......BBBB.......BBBBBB.......BBBBBBBBBBBBBBBB..........................................................FFFFFFFFFFFFFFF.F.FFF..F.....................A...AAAAAAAA.....AA...AAAAAAAAAAAAAA........AAA.............AAAAAAAAA...A.....AAAAA.........AAAAAAA.AAAAAAAAAAAAA.AA.AAA.........................................................................................BBBBB.........B.....BB.BBBB.BBBBBBB...B......BBBBBBBBBBBBBB.....BBBB.......BBB..........BBBBBBBBBBBBBBBB...........................................................FFFFFFFFFFFFFFFFFFF...F.....................AA...AAAAAAAA...AA....AAAAAAAAAAAAAAA.......AAAA............AAAAAAAAA........A..AAAA......AAAAAAA....AAAAAAAAAAA.AAAAAA..........................................................................................BBBB................BBBBBBB..BBBBB...BB.....BBBBBBBBBBBBBBB.....BBBB....B...............BBBBBBBBBBBBBBBB...........................................................FF..FFFFFFFFFFFFFFFF.......................AAA..AAAAAAAAA..AA....AAAAAAAAAAAAAAAAA...A.AAAAAAA..........AAAAAAA..........AAAAAAA....AAAAAAAA.....AAAAAAAAAAAAAAA.............................................................................................BBBB...............B...BBBB...BBBB...B.....B.BBBBBBBBBBBBBBBB...BBBBBBBBBB.........BBBB.BBBBBBBBBBBBB.BB................................................................FFFFFFFFFFFFFFFFF.....................AAAA..AAAAAAAA..AA....AAAAAAAAAAAAAAAAA...A...AAAAAAA.........AAAAAA..........AAAAAAAAA..AAAAAAA......AAAAAAAAAAAAAA.............................................................................................BBBBBB...BBB............BBBBB..BBBB..B.BB....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB........BBBBBBBBBBBBBBBBBB.................................................................FFFFFFFFFFFFFFFFFFF.......................AAA...AAAAAA...AA....AAAAAAAAAAAAAAAAA........AAAAAAA.........AAAAAAA........AAAAAAAAAAAAAAAAA.......AAAAAAAA.AA..........................................................................................BBBBBBBBBBBBB.BBBB..............BBBBBBBBBBBBBB......BBBBBBBBB.BBBBBBBBBBBBBBBBBBB.......BBBBBBBBBBBBBBBBBB....B......................EEE....................................FFFFFFFFFFFFFF..FFF......................AAAAA..AAAAA..AAAA....AAAAAAAAAAAAAAAAA...A..AAAA.AAAA........AAAAAA.A........AAAAA.AAAAAAAAAA.....AA.AAAAAAA.AA.A.........................................................................................BBBBBBBBBBBBBBBBBBBBB.B.......BBBBBBBBBBBBBBBB.......BBBBBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBBBBBBBBBBBB..................EE.EE....EEEE...................................FFFFFFFFFFFFF....FF......................AAAAA........AAAAA....AAAAAAAAAAAAAAAAA......AAAAA.AAAA.......AAAAAAA.........AAAA...AAAAAA.........AAAAAA.AAA.A........A.AA.............................................................................BBBBBBBBBBBBBBBBBBBBBB..B........BBBBBBBBBBBBBBBB........BBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBBBBBBBBBBB................E.E.EEEEE.EEEEEEE...................................FFFFFFFFFFFFF.....F.......................AAAAAA.....AAAAAA....AAAAAAAAAAAAAAAAA.........AAAAAAA.......AAAAAAA..........AA......AA..........AAAAAAAAAAA.........AAAAAA............................................................................BBBBBBBBBBBBBBBBBBBBBBB.B......BBBBBBBBBBBBBBBBBBB.......BBBBBBBBBBBBBB.....BBBBBBBB.....B.BBBBBBBBBBBBBBBBB.............EEEE.EEEE.EEEEEEEEE..................................FF....FFFFFFF............................AAAAAAAAAAAAAAAAAA....AAAAAAAAAAAAAAAAA.........AAAAAAA......AA.AAAA....................AA.........AAAAAAAAAAAAA......AAAAAAAA...........................................................................BBBBBBBBBBBBB..BBBBBBBBBBB........BBBBBBBBBBBBBBBBB.....BBBBBBBBBBBBB..B....BBBBBB.BB......BBBBBBBBBBBBBBBBBB..........EEEEEEEEEEEEEEEEEEEE.........................................FFFFFFF............................AAAAAAAAAAAAAAAAAA....AAAAAAAAAAAAAAAA............AAAAAA.....AAAAAAA.............................AAAAAAAAAAAAA.A....AAAAAAAAAA...........................................................................BBBBBBBBBBBB....BBBBBBBBBB....BBB..BBBBBBBBBBB.BBB......BBBBBBBBBBBBB..B...BBBBBBBBBB.......BBB.BBBBBBBBBBB...........EEEEEEEEEEEEEEEEEEEE..........................................FFFFFF.............................AAAAAAAAAAAAAAAAAA.....A.AAAAAAAAAAAA.............A.AAAAAAAAAAAAAAAAA.AA......................AA.AAAAAAAAAAAAAA.....AAAAAAAAA............................................................................B...BBBBBBBB...BBBBBBBBBBB.....BBBBBBBB.BB..BBB...B......BBBBBBBBBBBBB.BB.BBBBBBBBBBB..........BBBBBBBBBBBBB.........EEEE.EEEEEEEEEEEEE............................................FFFFFFF...............................AAAAAAAAAAAAAAA........AAAAAAAAAAA................AAAAAAAAAAAAAAAAAAAA.......................AAAAAAAAAAAAAAA.......AAAAAAAAA.................................................................................BBBBBBBBBBBBBBBBBBBB.....BBBBBBB.BBBB..BBBB........BBBBBBBBBBBBBBBBBBBBBBBBBBBB..........BBBBBBB..BBBB.............EEEEEEEEEEEEEEEEEEE......................................FFFFFFFF.................................AA..AA....AA...............A.......................AAAAAAAAAAAAAAA.AA..........A...........AAAAAAAAAAAA..........AAAAAAAAAA..................................................................................BBBBBBBBBBBBBBBBBBB....BBB.BBB.BBBB..BBBBB......BBBBBBB.BBBBBBBBBBBBBBBBBBBB........B.BBBBBBBB..BBBB........EEEEEEEEEE...EEEEEEEEEE......................................FFFFFFFFF...................................................................................A......A.AAAAAA...........A..AA...........AAAAAAAA.AAA.........AAAAAAAAA...................................................................................BBBBBBBBBBBBBBBB...........BBB.BBBB.BBBBBBB.....BBBBBB..BBBBBBBBBBBBBBBBBBB.........BBBBBBBBB...BB..........EEEEEEEEEEEE.EEEEEEEEEEE....................................FFFFFFFFF..............................................................................................AAAAA...A......AAAAAAAAA.......AAAAAAA..A.A.........AAAAAAAAA.................................................................................B...BBBBBBBBBBBBB....BB.....BB.BBB.BBB.BB..BBB....BBBBBBB..BBBBBBBBBB..BBBBBBB........BBBBBBBB................EEEEEEEEEEEEE.EEEEEEE...EEEE.................................FFFFFF.........................................................................AAAAAAA..................AAAAA.AAA.....A.AAAAAA........AAAAAA....A.........AAAAAAAAAAA................................................................................BBB.BBBBBBBBBBBBB....BB....BBB.BBBBBBBBBBB.BBB....BBBBBBBBBBBBBBBBBB...BBBBBB......BBBBBBBBBB................EEEEEEEEEEEEE....EEEEEEEEEEEEE...................................F.FFF........................................................................AAAAAAA.................AAAAAAAAA....AA.AAAAAA........AAAAA...............AAAAAAAAAA.................................................................................BBBBBBBBBBBBBBBBBB..BBB....BBBBBBBBBBBBBBB..B....B.BBBBBBBBBBBBBBBBB.....BBBB....BBBBBBBBBBB.................EEEEEEEEEEEE......EEEEEEEEEEEEE..............................................................................................................AAAAAAAA...............AAAAAAAAAA.....AAAAAAAAAAA.....AAAAAA.............AAAAAAAAAAAAA................................................................................BBBBBBBBBBBBBBBBBBBBBB......BBBBBB..BBBBBB.......BBBBBBBBBB..BBBBBBBB.B.........BBBBBBBBBBB...................EEEEEEEEEEEE....EEEEEEEEEEEEEEE............................................................................................................AAAAAAAA....AAAAAAAAAAAAAAAAAAAAAA.....AAAAAAAAAAAA...AAAAAAAA...........AAAAAAAAAAAAAA.A...............................................................................BBBBBBBBB...BBBBBBBBB.......BBBBBBB.BBBB.........BBBBBBB....BBBBBBBBBBB.........BBBBBBBBBB....................EEEEEEEEEEEE..EEEEEE.EEEEEEEEEE......................................................................AA..AA.............................AAAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAA....AAAAAAAAAAAA.....AAAAAA...........AAAAAAAAAAAAAAAAA..............................................................................BBB.BBBBB....BBBBBBBBBB......BBBBBBB.BB..........BBBBBB.......BBBBBBBBBBBB.B....BBBBBBBBBBB.BBB.................EEEEEEEEEEEEEEEE.....EEEEEEEEEE............................E.....................................AAAAAAAAAAAA.........................AAAAAAAAAAA....AAAAA.AAAAAAAAAAAAAAA.....AAAAAAAAAAAAAA..AAAAAAA...........AAAAAAAAAAAAAAAAA...............................................................................B..BBBBBB...BBBBBBBBB.B......BBBBB..B..........BBBBBB.......BBBBBBBBBBBBBBB.....BBBBBBBBBBBBBBB.................EEEEEEEEEEEEEE........EEEEEEEEE.......EEEEEEEEE..........EEEE................................AAAAAAAAAAAAAAAA.........................AAAAAAAAAAA..AAAAAAAAAA.AAAAAAAAAA.....AAAAAAAAAAAAAA.AAAAAAAAAAA.AA...AAAAAAAAAAAAAAAAAAA.................................................................................BBBBBBBBB..BBBBBBBBBBBB........BBB...........BBBBBBB........BBBBBBBBBBBBB........BBBBBBBBBBBBB..................EEEEEEEEEEEEEE.........EEEEEEEE...EEEEEEEEEEEEEEE......EEEEEE..............................AAAAAAAAAAAAAAAAA.........................AAAAAAA..AAAAAAAA..AAA.AAAAAAAAA.......AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.A..............................................................................BBBBBBBBBBBB.BBBBBBBBBBBB..........B........BBBBBBBBBBBB........BBBBB.BBBBBB..........BBBBBBBBBBB...................EEEEEEEEEEEE...........EEEEEEEE.EEEEEEEEEEEEEEEEE.....EEEEEE............................AAAAAAAAAAAAAAAAAAA.........................A...AA....AAAAAAA.....AAAAAAAAAA.......AAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAA.AA.AAAAA..A..........................................................................BBBBBBBB.BBBBBBBBBBBBBBBBBBB..........B.......B.BBBBBBBBBBB.B.......BBB...BBBBBBB.........BBBBBBBBBBB...................EEEEEEEEEEE.............EEEEEEEEEEEEEEEEEEEEEEEEEE....EEEEEE...................AAA.......AAAAAAAAAAAA..AAAA..................................AAAAAAAAAAAAAAAAAAAAAAAAA.....A.A..AAAA.AAAAAAAAAAA.AAAAAA..AAAAAAAAAAA...A...AA......................................................................BB...BBBBBB....B.BBBBBBBBBBBBBBBB.........B........BBBBBBBBBBBBBB.B......BBBBB.BBBBB.B.B..BB...BBBBBBBBBB...................EEEEEEEEEE................EEEEEEEEEEEEEEEEEEEEEEEEEE..EEEEEEE..................AAAA.......AAAAAA.AAAA...AAAAA.................................AAAAAAAAAAAAAAAAAAAAAAAAA.......A.......AAAAAAA..A.AAAAAAA.AAAAAAAAAAA.......A........................................................................BBBBBBBBBB........BBBBBBBBB.B.BB...................BBBBBBBBBBBBBBB...B.BBBBBBB.BBBBBBBBB.BBBB.BBBB...BBB....................EEEEEEEEE.................EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE.................AAA.AAA......AAAAAAAA.A.AAAAAAAA................................AAAAAAAAAAAAAAAAAAAAAAAAAA....AAA.......A.AAAA......AAAAAAAAAAAAAAAAAAAA..............................................................................BBBBBBBBBB...........BBBB........................BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB...BB.........................E.....................EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE...................AAA.A.......AAAAAAAAAAAAAAAAAAAAA............................AAA.AAAAAAAAAAAAAAAAAAAAAAAA...AAAAAAA.....AAAA......AAAAAAAAAAAAA.AAAAAA...............................................................................BBBBBBBB.BB..........BBBB................BBBB....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB...BBB..............................................EEEEEEEEEEEEE...EEEEEEEEEEEEEEEE....................AAAAAA.......AAAAAAAAAAAAA....AAAA.....AA..........TTTTT.......A.AAAAAAAAAAAAAAAAAAAAAAAAA......AAAA....AAAAA......AAA..AAAAAAAAAAAAAAAA.................................................................................BBBBB.............BBBB...............BBBBB...BBBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBB..........................................EEEEEEEEEEEE......EEEEEEEEEEEEE......................AAAAAA......AAAAAAAAAAAAA......AAAA...AAAA..........TTTT..........AAAAAAAAAAAAAAAAAAAAAAAA.............AAAAA.......AAA.AAAAA.AAAA..AAA....................................................................................BBB.............BBBBB...............BBBBBB.BBBBBB....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.........................................EEEEEEEEEE..........EEEEEEEEEEE......................AAAAAAAA....AAAAA....AAAAA......AAAA...AAAA........TTTTT...........AAAAAAAAAAAAAAAAA.AAAAAAA............AAAAAA......AAAAAAAAA.AA....A.......................................................................................BB............BBBBBBB..............BBBBBBBBBBBB.B...BBBBBBBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBBBBBBBBBBB..........................................EEEEEEEE...........EEEEEEEEEEEE................AA.....AAAAAAA....AAAA.AAA.AAAAA.......AAAA..AAAA.......TTTTTTTTT......AAA.AAAAAAAAAA.AAA.A.AAAAAA...........AAAAAA......AAAAAAAAAAAAA.A.........................................................................................BBB.............BBBBBB.........B...BBBBBBBBBBBBBB....BBBBBBBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBBBBBBBBBBB.........................................EEEEEEEE...........EEEEEEEEEEEEEE...............AAAA..AAAAAAAAA....AAA.AAA..AAAA........AA...AAA.........TTTTTTTT.......AAAAAAAAAAAAAAAA...AAAAAAAA..........AAAAA........AAA..AAAAAAAAA.........................................................................................BBBB.............BBBBB........BBBBBBBBBBBBBBBBBB....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBBBBBBB.B..B........................................EEEEEEEE..........EEEEEEEEEEEEEEE..............AAAAAAAAAAA.AAAA....AAA.AAA...AAAA.......AAAAAA...............TTT.........AAAA.AAAAAAAA......AAAAAAAA........AAAAA.........AA...AAAAAAAAAA........................................................................................BBBB.............BBBBBB......BBB..BBBBBBBBB.BBBBBBBBBBBBBBB...BBBBBBBBBBBBB.BBBBBB.BB......BBBBBB.............................................EEEEEEEEE.......EEEEEEE...EEEEEEEE..............AAAA..AAAA.AAAAA...AAAA.AAA....AAA.....AAAAAAA................TTT..........AAAAAAAAAAA......AAAAAAAAAA.AAAAAAAAAAA.........AAAAAAAAA..AAAAA.......................................................................................................B.BBBBBBB...BBB.BBBBBBBBBB.....BBBBBBBBBBB...B..B.BBBBBBB.....BBBB..........BBBBB.............................................EEEEEEEEE.....EEEEEEEEEE..EEEEEEEE..............AAA...AA..AAAAAAAAAAAAAAAAA....AA.....A.AAAAAAA...............TTT....TT.........AAAAA......AAAAAAAAAAAAAAAAAAAAAA...........AAAAAAAA...AAAA.........................................................................................................BBBBBBBBBB.B.BBBBBBBBBB....BBBBBBBBBBBBBBBB....BBBBBBB....BBBBBBB........BBBBBBBB......B..................................EEEEEEEEEE....EEEEEEEEE......EEEEEE....................A...AAAAAAAAAAAAAAAAAAA.........AAAAAAAAAAA...........T.TTTT....TT.........AAAAA....A.AAAAAAAAAAAAAAAAAAAAAAA.AA.......A.AAAAAAA....A.........................................................................................................BBBBBBBBBBBBBBBBBBBBBBBB....BBBBBBBBBBBBBBB......BBBBBBB..B.BBBBBB.......BBBBBBBB.BB..BBB.BBB..............................EEEEEEEEE.....EEEEEEEEE......EEEEEEE.....................AAAAAAAAAAAAAAAAAAA..............AAAAAAAA..........TTTTTTTTTT.TT........AAAAA....A.AAAAAAAAAAAAAAAAAAA.AAAAAAAA.........AAAAA...AA..........................................................................................................BBBBBBBBBBBBBBBBBBBBBBBBB.BB.BBBBBBBBBBBBBBB....BBBBBBBB.BBBBBBBBB....BBBBBBBBBBBBBB..BBBBBBBB.............................EEEEEEEEE....EEEEEEEEEE.....EEEEEEE.....................AAAAAA....AAAAAAAAAA............AAAAAAAAA............TTTT..TTTTTT........AAAA.....AAAAAAAAAAAAAAAA..AA....AAAAAAA........AAAAA..A............................................................................................................BBBB..BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBB.BBBBBBBBBBBBBBBBBB....BBBBBBBBBBBBBBBBBBBBBBBB.............................EEEEEEEEE......EEEEEEE...E..EEEEEEE...................AAAA.A........A..AAAA.............AAAAAA.............TTTTTTT..TTTTT.........AA........AAAAAAAAAAAA...........AAAAAA........AAAAA...A..........................................................................................................BBBBB..BBBBBBBBBBBB.BBBBBB.BBBBBBBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBBB....BBBBBBBBBBBBBBBBBBBBBBBB...........................EEEEEEEE............EEE...E.EEEEEEEEEE..................AAAA..............AAAAAAAAAAAAAAAAAAAAAAA...........TTTTTTTTT.TT..T..........A........AAAAAAAAAAAA...........AAAAA.....AAAAAAAAAA....A.......................................................................................................BBBBB....BBBBBBBBB....BBBBBBBBBBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBB.........................EEEEEEEEE.............EE..E.EEEEEEEEEEE..................AAAA...............AAAAAAAAAAAAAAAAAAAAAA...........TTTTTTTTTTTTT......................AAAAAAAAAA.............A.A......A.AAAAAAAA...........................................................................................................BBBBBB.....BBBBB.......BBBBBBBBBBBBBB.....B...BBBBBBBBBBBBBBB.BBBB.B.......BBBBBBBBBBBBBBBBBBBBBBBB........................EEEEEEEEEEE.................EEEEEEEEEEE..................AAA................AAAAAAAAAAAAAAAAAA.AAA...........TTTTTTTTTTTTTT......................AAAAAAAA........................A.AAAA.AAA...........................................................................................................BBBBB.....BBBB.B.....BB.BBBBBBBBBBB...........BBBBBBBBBBBB.BB....B........BBBBBBBBBBBBBBBBBBBBBBBB........................EEEEEEEEEEEE...............EEEEEEEEEEEE.................................AAA..AAAAAAA.AAAAAAA.AAAAAA..........TTTTTTTTTTTTTTT.....................AAAAAAA.........................AAAAAAAAA.............................................................................................................BBBBBB...BBBB.B.....BB.BBBBBBBBBB...............BBBBBBBBB.B..............BBBBBBBBBBBBBB.BBBBBB..........................EEEEEEEEEEEEEEE..............EEEEEEEEEEE................................AAAAA.AAAAAAA.AAAAAAA...AAAA............TT...TT.TTTTT....................A..AAAAA.......................A..A..AAAA.............................................................................................................BBBBBBBBBBBBB..B........BBBBBBBBBB...............BBBBBBBBB...............BBBBBBBBBBBBBBB..BBBBBBB........................EEE.EEEEEEEEEEEEEEEEE.......EEEEEEEEEEE................A..........A.....AAAAA.AAAAAAA.AAAAAAA....AAA.................TTTTTTTT.......................AAAAA............................A.A..............................................................................................................BBBBBBBBBBBBBBBB........B.BBBBBBBBB...............BBBBBBBBB................BBBBBBBBBBBBBB..BBBBBBB.......................EEEEE.EEEEEEEEEEEEEEEEEEEEE.E...EEEEEEEEE...............AA.....AA..A.....AAAAA.AAAAAA...AAAAAA...AAAA..................T.TTTT........................AAAAA............................................................................................................................................BBBB..BBBBBBBBBBB..........BB.BBBBBBB..............BBBBBBBB.................B.BBBBBBBBB....BBBBBBBB.......................EEEEE..EEEEEEEEEEEEEEEEEEEEEEE..EEEEEEEE.............A..AA....AAAAAAAA..AAAAAA.AAA.........AAA..A.AAA..................TTTTT.......AAAA.AAAAA........AAAAA............................................................................................................................................BBB......B.BBBBB..............BBBBBBBB.........B...BBBBBBBBBB..................BBBBBBBB...BBBBBBBBB......................EEEEE...EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE.............A..AAAA.AAAAAAAAAAAAAA.A...AAAAAAA..AAAAAA..AAAAA...................TTT.......AA.AA.AAAAAA.......AAAAA...........................................................................................................................................BBBB........BBBBBB.BB..........BBBBBBB.........BBBBBBBB.BBB.B..B.................BBBBBBBBBBBBBBBBBB.......................EEEEE....EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE.............A.AAAAAAAAAAAAAAAAAAAA.....AAAAAAA.AAAAAAA..AAAAAA.A..........................AAAAAAAAAAAA.A.....AAAAA..........................................................................................................................................BBBBBB.......BBBBBBBBB...........BBBBBB........BB.BBBBBBBBBBBBBB....................BBBBBBBBBBBBB..B.......................EEEE......EEEEEEEEEEEEEEEEEEEEEEEEEEEE...............AA.AAAAAAAAAAAAAAAAAA......AAAAAAA.AAAAAAA...AAAAAAAA.........................AAAAAA.AAAA..AAA...AAAAA...........................................................................................................................................BBBBB......BBBBBBBBB............BBBBBBBB..B..BBBBBBBBBBBBBBBBB......B...............BBBBBBBBBBBB.........................EEEE........EEEEEEEEEEEEEEEEEEEEEEEEEEEE..............AAAAAAAAAAAAAAAAAAAAAA.....AAAAAAA.AAAAAAAAAA.AAAAAAA.........................AAAAAAA.AA.A.AAA..AAAAAAA...........................................................................................................................................BBBB......BBBBBBBB.....B.BB.B..BBBBBBBBB..B..BBBBBBBBBBBBBBB.....BBBBBB.............BBBBBBBBBBB........................EEEEE.........EEEEEEEEEEEEEEEEEEEEEEE..................AAAAAAAAAAAAAAAAAAAAAAA....AAAAAAAAAAAAAAAAAAAAAAAAAA.........................AAAAAAAAAAAAAAAAAAAAAAAAAA...........................................................................................................................................BBBBBBBBBBBBBBBBB....BB.BBBBB.BBBBBBBB....BBBBBBBBBBBBBBBBB...BBBBBBBBB............BB.BBBBBB........................EE.EEE............EEEEEEEEEEEEEEEEEEEE...................AAAAAAAAAAAAAA.....AAAAA...AAAAAAAAAAAAAAAAAAAAAAAAAAA.........................AAAAAAAAAAAAAAAAA.AAAAAAAAAAA........................................................................................................................................BBBBBBBBBBBBBBB....BB.........BBBBBBBBB....BBBBBBBBBBBBBB.BBBBBBBBBBBB............BB..BBBB.......................EEEEEEEEEE..............EEEE..EEEEEEEEE.....................AAAAAAAAAAA........AAAA..AAAAAAAAAAAAAA..AAAAAAAAAA............................AAAAAAAAAAAAAAAAAAAAAAAAAA.........AA..............................................................................................................................BBBBBBBBBBBB.B........BBB.BBB..B.BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..............BBBB.......................EEEEEEEEEEE......................E..E......................AAAAAAAA.AA.........AAAAAAAAA............AAAAAAAAAAA.............................AAAAAAAAAAAAAAAAAAAAAAAAAA..A.A...AAA..............................................................................................................................BBBBBBBBBB.......BBB.BBBBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..............BBBB.......BBB.............EEEEEEEEEEEE...............................................AAAAAAAA.A...........AAAAAAAA..........AAAAAAAAAAA................A.........A.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....AAAA.............................................................................................................................BBBBBBB.........BB..BBBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB............B.BBBB.......BB............EEEEEEEEEEEEE...............................................AAAAAAAA.............AAAAAAAA..AA..AA...AAAAA.A.A......AAA.......A.A.....A..A.......AAAAAAAAAA.AAAAAAAAAAAAAAAAAAA.AAAAAA.............................................................................................................................BBBB..........BBBB..BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB............BBBBB....B.BBBBB..........EEEEEEEEEEEEE...............................................AAAAAA...............AAAAAAAAAAAAAAAA..AAAAAAA........AAA.....AAAA.....AAAAAAA.......AAAA....A.AAAAAAAAA.A.AAAAAAAAAAAAAAA.............................................................................................................................BB............BBBBBBBBBBBBBBBBBB...BBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBBBB............BBBBBB.BBBBBBBBB.........EEEEEEEEEEEEEEE................................................AAA...............AAAAAAAAAAAAAAAAA.AAAAAA.A......AAAAAAA..AAAAA....AAAAAAAA.......AAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..........................................................................................................................................BBBBBBBBBBBBBBBBBB...BBBBBBBBBBBBBBBBBBBBB......BBBBBBBBB......BB..BBBBBBBBBBBBBBBBBB........EEEEEEEEEEEEE.....................................................A............AAAAAAAAAAAAAAAAAAAA.AAAAAAA........AAAAAAA.AAAAAA...A.AAAAA.A......AAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA........................................................................................................................................BBBBBBBBBBBBBB.B.BBBBBBBBBBBBBBBBBBBBBB..B......BBBBBBBBBBB...BBBBBBBBBBBBBBBBBBBBBB..........EEEEEEEEEEEEEEE...............................................................AAAAAAAAAAAAAAAAAAA...AAA..A.........AAAAAAAAAAAAAA.....AAAAAA.A...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA........................................................................................................................................BBBBBBBBBBBBBBB.B.BBBBBBBBBBBBB.BBBBBB.......BBBBBBBBBBBBB..B.B.BBBBBBBBBBBBBBBBBBBBB.........EEEEEEEEEEEEEEE...............................................................AAAAAAAAAAA.AAAAA.A......A...........AAAAAAAAAAAAA...AAAAAAAAAAA.A.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAA......................................................................................................................................BBBBBBB.BBBBBBB..BBBBBBBBBBBBBBB.BBBBBB......BBBBBBBBBBBBB...BBBBBBBBBBBBBBBBBBB.BBBB...........EEEEEEEEEEEEE..........DDDDDDD...............................................AAAAAAAAAA.AAAAAAA..................AAAAAAAAAAAAAA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA....AAAAAAAAA......................................................................................................................................BBBBBB...BBB.BB.B.BBBBBBBBBBBB..BBBBBB.......BBBBBBBBBBBBBBB.BBBBBBBBBBBB.BBBBBB..BBB...........EEEEEEEEEEEEE..........DDDDDDDD..............................................AAAAAAAAA..AAAAAAA..............AA.AAAAAAAAAAAAAAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAA......AAAAAAA....................................................................................................................................BBBBBBBB.....B...B.BBBBBBBBBBBBB..BBBBBB........BBBBBBBBBBBBB.BBBBBBBBBBBBBBBBBB.BBBB.BB..........EEEEEEEEEEEE...........DDDDDDDDD...............................................AA...A...AAAAAAA................AAAAAAAAAAAAAAAA...AAAAAAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAA.......AAAAAAAA...............................................................................................................................B.BBBBBBBBBB.......BBBB.B.BBBBBBBBB.B..BBB..........BBBBBBBBBBBBBBBBBBBBBBBBBBBBB.......B............EEEEEEEEEEEE...........DDDDDDDDDD....D...D...............................................AAAAA...........A.....AAAAAAA..AAAAAAA...AAAAAAAAAAAAAAAAAAAAA.AAA.......AAAAA................AAAAAAA...........................................................................................................................BBBBBBBBBBBBBBB.BBBBBBB.B..BBBBBBBB.......BB...........BBBBBBBBBBB.BBBBBBBBBBBBBBBB.....................EEEEEEEEEE..........DDDDDDDDDDDDDDDDDDDDDDD.........................................AAAAAAAAAA..........AA.AAAAAAAAA...AAAAAAA..AAAAAAAAAAAAAAAAA.AAA.A...........AAAA...............AAAAAAAAA........................................................................................................................BBBBBBBBBBBBBBBBBBBBBBBB..BB.BBBBBBBBB.....................BBBBBBBBBBBBBBBBBBBB..BBBB.......................EEEEEEE..........DDDDDDDDDDDDDDDDDDDDDDDDD........................................AAAA.AAAAA......AAAAAA.AAAAAAAAAA.AAAAAAAAAAAAA.AAAAAAAAAAAA..A...............AAAA.........AA.....AAAAAAAA.........................................................................................................................BBBBBBBBBB...BBBBBBBBBBBBBBBBBBBBBBBBB....................BBBBBBBBBBBBBBBBBBBB..BBBB.........................EEE...........DDDDDD.DD..DDDDDDDDDDDDDDDDDD...............................A.....AAAAAAAAAAA...AAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAA..AAAAAAAAAAA....................AAAA.....AAAAAA....AAAAAAAA........................................................................................................................BBBBBBB.BBBBBBBBBBBBB..BBBBBBBBBBBBBBBBB....................BBBBBBB.BBBBBBBBBBB..BBBB......................................DDDDDDD.......DDDDDDDDDDDDDDDD..............................AAAAA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAAA...A.AAAAAAAAAAA.................AAAAAA....AAAAAA.....AAAAAAAA......................................................................................................................BBBBBBB..BBBBBBBBB......BBBBBBBBBBBBBBBBB...BBBBB..............BBB.B.BBBBBBBBBBB..BB.B......................................DDDDDD........DDDDDDDDDDDDDDDDDD............................AAAAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAA.....AAAAAAAAAAAA............AAA..AAAAAA....AAAAAA.....AAAAAAAAA.....................................................................................................................BBBB...B.BBBBBBBB........BBBBBBBBBB...BBB.B.BBBBBBB...............B..BBBBBBBB....BBB.......................................DDDDDDD........DDDDDDDD....DDDDDDDD.........................AAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAA...A.AAAAAAAAAAAAAAA.A......AAAAAAAAAAAAAA...AAAAAAAA...AAAAAAAAA....................................................................................................................BBBBB......BBBBBB..........BBBBBBBB.B.BBBBBBBBBBBBBB..................BBBBBBBBBBBBBBB...............BBBB....................DDDDDD.........DDD.DDD......DDDDDDDDD.......................AAAAAAAAAAAAA..AAAAAAAAA.AAAAAAAAAAAAAA.AAAAAAAAAAAA...AAAAAAAAAAAAAAAAAAAAA.A...AAAAAAAA.AA....AAAAAAAA...AAAAAAAAA...................................................................................................................BBBBB.......BBBB...........BBBBBBBBBB..BBBBBBBBBBBBBBBBBB.............BBBBBBBBBBBB.BBB.BB..........BBBBBBBB...............DDDDDDDDDD.........DDDDD........DDDDDDDDDD.....................AAAAAAAAAAAAA..AAA.........AA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAAAAAAAAAA.....AAAAAAA...AAAAAAAAA.................................................................................................................BBBB..B.......BBBB...........BBBBBBBBBBBBBBBB..BBBBBBBBBBBBB................BBBBBBBBBBBBBBBB.........BBBBBBBB..............DDDDDDDDDDDD.........DDDDD.......DDDDDDDDDDD....................AAAAAAAAAAAAA..AAA......A..AAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAA.A.AAA.AA....AAAAAAA...AAAAAAAAA................................................................................................................BBBB...........BBBBB.........BBBBBBBBBBBBBBBBB.BBBBBBBBBBBBBBBB..........B.BBBBBBBBBBBBBBBBBB.........BBBBBBB..............DDDDDDDDDDDDDDD.........DDD......DDDDDDDDDDDDD....................AAAAAAAAAAAAAAAAA......AA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA...AAAAA....AAAAAAAAAAAAAA..AAAAAA....AAAAAAA...AAAAAAAA.................................................................................................................BBB...........BBBBB.........BBBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBBBB.........BBBBBBBBB.BBBBBBBBBB....B...B..BBBB.............DDDDDDDDDDDDDDDDD........DDDDDDD.DDDDDDDDDDDDDD.......................AAAAAAAAAAAAAAA.....AAAAAAAAAAAAAAAA.AAAAAA..AAAAAAAAA...AA..........AAAA.AAAAAAAAAAAAAAA.A.AAAAAAA....AAAAAAAA.................................................................................................................BB.........BBBBBBBBBB........BBBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBBB.......BBBBBBBBBBB.BB...BBB.....BB....B...BB..BBB..........DDDD.DD.DD..D.D..........DDDDDDDDDDDDDDDDDDD....................AAAAAAAAAAAAAAAAAAA....AAAAAAAAAAAAAAAAAAAAAAA...AAAAAAAAA...A.....AAAAAAAA..AAAAAAAAAAAAAAAAAA.A.AAAA...AAAAAAAAA..................................................................................................................B........BBBBBBBBB.B.......BBBBBBBBBBBBB..BBBBBBBBBBB..BBBBBBBBBB.........BBBBBBBBBBBB...BBBBB.BBBBBB.BBBBBBBBBBBB..........DDDD....DDDDDD............DDDDDDDDDDDDDDDDDD...................AAAAA...AAAAAAAAAAA.....AAAAAAAAAAAAAAAAA.AAA......AAAAAAA.........AAAAAAAA....AAAAAAAAAAAAAAAAAAAAAAAA..AAAAAAAAA..................................................................................................................BBB....BBBBBBBBBBBBBB.......BBBBBBBBBB.....BBBBBBBBBBBB.BBBBBBBBBB..........BBBBBBBBBBBB.B.BBBBBBBBBBBBBBBBBBBBBBBB..........DDDDDDDDDDDDDDD...........DDDDDDDDDDDDDDDDDD...................AAAA....AAAAAAAAAA......AAAAAAAAAAAAAAAAAA.A.......AAAAAAAA........AAAAAAAA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..................................................................................................................BBBB.BBBBBBBBBBBBBB.........BBBBBBBBBB......BBBBBBBBBBBBBBBBBBBBBBB.........BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB...........DDDDDDDDDDDDDDDD............DDDDDDDDDDDDDDDDD..................AAA....AAAAAAA..AA......AAAAAAAAAAAAAAAAA.....A..A..AAAAAA..........AAAAAAA....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA...................................................................................................................BBBBBBBBBBBBBBBB.B.........BBBBBBBBBB......BBBBBBBBBBBBBBBBBBBBBBB.........BBBBBBBBBBBBBBBBBBBBBBBB....BBBBB.BBB..........DDDDDDDDDDDDDDDDDD.............DDDDDDDDDDDDDDDDD.......................AAAAAAA...........AAAAAAAAAAAAAAAAA.....AAA...AAAAAA...........AAAAAA..A.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA....................................................................................................................BBBBBBBBBBBBBBB..........B.BBBBBBBBBBB....BBBBBBBBBBBBBBBBBBBBBBB.......BB.BBBB..BBBBBBBBB..BBBB.....B.BBBB..BBB......D...DDDDDDDDDDDDDDDD..D...........DDDDDDDDDDDDDDDDDDDD....................A.AAAAAA..........AAAAAAAAAAAAAAAAA...AA.A..AAAAAAAAA............AAAA...AAAAAAA..A..AAAAAA.A.AAAAAA..AAAAAA.....................................................................................................................BBBBBBBBBBBBBBB..........BBBBBBBBBBBBBB.....BB.BBBBBBBBBBBBBBBBB.........BBBBBB.BBBBBBB..BB.BBBBB....BBBBB..BBBB......DD.DDDDDDDDDDDDDDDDDDDDD........DDDDDDDDDD.....DDDDDDDDD..................A.AAAAAAA.........AAAAAAAAAAAAAAAAA..A.A...AAAAAAAAAA....A.............AAAAAAAAA.....AAAAA...AAAAAA...AAAAA.....................................................................................................................BBBBBBBBBBBBBBBB......BBBBBBBBBBBBBBBBB....B..B.BBBBBBBBBBB.BBBB........BBBBBBBBBBBBBB.B.BBBBBBBBBBB.BBB....BBBB......D..DDDDDDDDDDDDDDDDDDDDD.......DDDDDDDD........DDDDDDDDDD....................AAAAAA.........AAAAAAAAAAAAAAAAA..AA..A.AAAAAAAAAAAAAAAA.............AAAAAAAAA.AAAAAAA.A..AAAAAA..AAA........................................................................................................................BBBBB.B.BBBBBBBBB....BBBBBBBBBBBBBBBBBBB..BBB.BBBBBB.BBBBB................BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB....BBBB......D...DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD.........DDDDDDDDDD............A.A....AAAAAA.A........AAAAAAAAAAAAAAAAA.A.A.AAAAAAA...AAAAAAAAA..........A.AAAAAAAAAAAAAAAAA..A.A.AAAA....AA........................................................................................................................BBBBBBBBBBBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.BBBB.................BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB....BBBB.......D..DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD........DDDDDDDDDD.............AAAAAAAAAAAAAA........AAAAAAAAAAAAAAAAA.AA.AAAAAAA....AAAAAAAAA........AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.............................................................................................................................BBBBBBBBBBBBBB.......BBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBBBB............B.B.BBBBBBB..BBBBBBBBBBB...BBBBBBBB.BBBBBBB........D..DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD.......DDDDDDDDDD............AAAAAAAAAAAAAA..A.......AAAAAAAAAAAAAAA..A..AAAAAAA....AAAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA....A............................................................................................................................BB.B.BBBBBB........BBBBBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBB..........BBBBBBBBBBBB....BBBBBBBB......BBBBBBBBBBBBB.........DD.DDDDDDDDDDD.DDDDDDDDDDDDDDDDDDDDDDD.......DD.DDDDDDDD............AAAAAAAAAAAAAAAAAA......AAAAAAAAAAAAAA.A..AAAAAAAAA...AAAAAAAA....AAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAA...A............................................................................................................................BBB.B..BBBB.....BBBBBBBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBB...........BBBBBBBBBBBBBB.....BBBBBBB........BBBBBBBBBB...........DDD.DDDDDD......DDDDDDDDDDDDDDDDDDDDDD........DDDDDD.DD...............AAAAAAAAAAAAAAAAA.....AAAAAAAAAAAAA.AA.AAAAAAAAAAAAAAAAAA.....AAAAAAAAAAAAAAAAA......AAAAAAAAAAAAAAAAA....................................................................................................................................BBBB...BBBBBBBBBBB..BBBBBBBBBBBBBBBBBBBBBBBBBBBB.......BBBBBBBBBB..BBB.......BBBBB..........BBBBBBB.............DDDDDDDDDDD......DDDDDDDDDDDDDDDDDDDD.........DDDDDDDDDD................A.AAAAAAAAAAAA.......AAAAAAAAAAAA.A..AAAAAAAAAAAAAAAAAAAA...AAAAAAAAAAAAAAAAAAA..AAAAAAAA.AAAAAAAAAA...................................................................................................................................BBBBBBBBBBBBBBBBB....BBBBBBBBBBBBBBBBBBBBBBBBBBB.B..BB.BBB.BBBBBBBBBBB........BBBB.B.........BBBB................DDDDDDDDD.......DDDDDD.DDDDDDDDDDDDDDDDD......DDDDDDDDDD.................A.AAAAAAAAAA......................A.AAAAAAAAAAAAAAAAAAAA.A.AAAAA.A.AAAAAAAAAA.....AAA...AAAAAAA.A...............................................................................................................................BBBBBBBBBBBBBBBBBBBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.BBBB...BBBBBBBBB.........B.B..........BBBBB..................DDDDDDDD......DDDDDDDDDDDDDDDDDDDDDDDDDD..DDDDDDDDDDDD......................AAAAA.A..........................AAAAAAAAAAAAAAAAAA.AAAAAAAA...AAAA..AAAA......AA...AAAAA...................................................................................................................................BBBBBBBBBBBBBBBBBBBBBB.........BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.............................................DDDDDDDDDD....DDDDDDDDDDDDDDDDDDDDDDDDDDD.DDDDDDDD.DD.................AAA.A..AAAA............................AAAAAAAAA...AAAAAAAAAAAAAA....AAAA...A............AAAAAA...................................................................................................................................BBB..BBBBBBBBBBB.B.BBBB.......BBBBBBBB.B..BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..B..........................................DDDDDDDD.......DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD...DDDD..............AA.AAAAA..AAAA............................AAAAAAAA.....AAAAAAAAAAAA..AAAAAAAA..............AAAAAA....................................................................................................................BB..............BB...BBBBBBB.BBBB.BBBB........BBBBBBB.....BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.B..........................................DDDDDDDD.D.....DDDDDDDDDDDDDDDDDDDDDDDDDDDDDD.......................A.AAAAA..AAAA............................AAAAAAAA.....AAAAAAAAAA...AAAAAAAAAAA...........AAAAAAAA.........................AAAA....................................................................................BBBBBB............BBBBBBBBBBB....BBBBBBB.......BBBBBBBBB.......BBBBBBBBBBBBBBB.BBBBBBBBBBBBB.BB...........................................DDDDDDDDDDDDD.DDDDDDDDDDDDDDDDDDDDDDDDDDDDDD.......................AA.AAA...AAAAAAA...........................AAAAAAAAA..A.AAAAAAAAAA..AAAAAAAAAAA............AAAAAAA.........................AAAAA...............................................................................BBBBBBBBBBBB..........BBB..BBBBBB.B..BBBBBB.B.......BBBBBBB.......BBBBBBBBBBBBBBBB..BBBBBBB..BBBB..B..........................................DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD.DDDD.....................AAAA.AAAA.AAAAAAA..........................AAAAA.AAAAAA.AAAAAAAAAA..AAAAAAAAAAA.............AAAAA..........................AAAAAAA...........................................................................BBBBBBBBBBBBBBBB..........BBBBBBBB.B..B.BBBBB........BBBBBBBB.......BBBBBBBBBBBBBB......BBBBBB.BBBBB..B.B........................................DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD....DDD..................AAAAA.....AAAAAAAA............................A...AAAAAAAAAA.AAAAAA..AAAAAAAAAAA...........................................AAAAAAAA..........................................................................BBBBBBBBBBBB.BBBBB.........BBBBBBBBBB..BBBBBB..........BBBBBBBB.......BBBBBBBBBBBB.......BBBBBBB.BBBB...BBB...................................D.D.D.DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD.......DD................AAAAAAA.AAAAAAAAAAA...............................AAAAAAAAAA.AAAAA..AAAAAAAAAAAA............................AAAA..........AAAAAAAAA........................................................................BBBBBBBBB....BBBBBBB.........BBB..BBB...BBBBBB.B..........BBBBBBB.......BBBBBBBBB..........BBBBBBBBBBBBBBBBBB....................................DD..DDDDDDDDDDDD.DDDDDDDD..DDDDDDDDDDDDDDDDD.......DD...............AA.AAAAAAAAAAAAAA.AA..............................AAAAAAAAAAAAAAAA..AA......AAAA...........................A.AAAAAAAAAA..AAAAA..AAA......................................................................BB...BBBB...BBBBBB.............BBB.BBBB.BBBBBBBBBBB.B......BBBBBBBBB.......BBBBBB...........B.BBBBBBBBBBBBBBBBB....................................DDDDDDDDDDDDDDD.DDDDD..DD.DDDDDDDDDD.DDDDDDDD..D.D..D...................AAAAAA.AAAAAAAAAA..............................AAAAAAAAAAAAAAA..AA.AAAAAAAAA..........................AAAAAAAAAAA..A.AAAA....AA.....................................................................BBBBB.BBB.....BBBBBB.B...........BBBBBBBBBBBBBBBBBBBB.......BBBBBBBB........BBBBB............BBBBBBBBBBBBBBBBBBBB...................................DDDDDDDDDD.DDD..DDDD......DDDDDDDDDD.DDDDDDDDDDDDDD..D................AAAAAAAA.AAAAAAAAAAA.....A..A.................A..AAAAA.A.AAAAAAA.AAA.AAAAAAAAA.........A................AAAAAAAAAAAA....AAAAA...A.....................................................................B...BBBBB..BB.BBBBBBB.....B......BBBBBBBBBBBBBBBBBBBB....BBBBBBBBBBBBBB.....BBBBB....B.B.......BBBBBBBBBBBBBBBBBB..................................D.DDDDDDDDDDDDD.DDDD.......DDDDDDDDDD..D.DDDDDDDDDDDD................AA.AAAAAAAA.AAAAAAAAAA.....AAAAAA..................AAAA.A.AAAAAAAA..AA.AAAAAAAAA..........A..............AAAAAAAAAAAAAAA..AAAAA.........................................................................B.BBBBBBB.B..BBBBB.BBBB..BBBBBB.BBBBBBBBBBB.BBBBBBBB.....BBBBBBBBBBBB.......BBBBB...BBBBB.......BB.BBBBBBBBBBB.....................................DDDDDDDDDDDDDDDDDDD.DDDD..DDDDD..DDDDD....DDDDDDDDDDD................AAAAAA...AAAAAAAAAA.AA...AAAAAAAAAA................AAAA.....AAAAAA..A.AAAAAAAAA..........AAA...........A..AAAAAAAAAAAAAAAA.AAAA.........................................................................BBBBBBBBBB.BBBBBB.BBBBBBBBBBBBBBBBBBBBBBBBB...BBBBBB.....BB.B.BBBBBBBBB.....BBBBBBBBBBBBBB..B.......BBBBBBBBB.....................................DDDD..DDDDDDDDDDDDDDDDDDDDDDDDD.DDDDDDD...DDDDDDDDDDDDD...............AAAAAAA...A.AAAAAAAA.....AA.AAAA.AAAA..............AAAA....AAAAA......AAAAAAAA.A......AAAAA.A.........AAAAAAAAAAAAAAAAAAAA.AAAAA........................................................................BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..BBBBBBBB.......B.BBBBBBBB.B....BBBBBBBBBBBBBBBB..B.....BBBBBBBBBB..........................................DDDDDDDDDDDDDDDDDDDDDDDDD.DDDDDDDDD..DDDDDDDDDDDDD...............A..A........AAAAAAAAA.AAA.AAAAAAA.AAA..............AAAAAAAAAAA........................AAAAAA..........AAAAAAAAAAAAAAAAAAAAAAAAAAAA......................................................................BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB...BBBBBBB.........BBBBBBBBBBB....BBBBBBBBBBBBBBBB..B.....BB.BBBBBB..........................................DDDDDDDDDDDD.DDDDDDDDDDDDD.DDDDDDD...DDD.DDDDDDDD..............................AAAAAAAAAAAAAAAAAAAAAAA.............AAAAAAAAAAA.........................AAAAAAAA.....A...AAAAAAAAAAAAAAAAAAAAAAAAAAA......................................................................BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBBBBB......BBBBBBBBBBBBB....BBBBBBBBBBBBBBBBBB.......B.BBBBB......................................DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD..DDDDDD..DDD...DDDDDDDD..........................AAAAAAAAAAAAAAAAAAAAAAAAAAA...........AAAAAAAAAAAAA........................AAAAAAAAA....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA......................................................................BB..BBBBBBBBBBBBBBBBBBBBBBB...BBBBBBBBBBBB.BBBBBBBB.........BBBBBBBBBBBB.BB.BBBBBBBBBBBBBBBBBBBB.......B.BB.......................................DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD.DDDDDD..DD....DDDDDDDD.....................AAAA..AAAAAAAA.AAAAA.AAAAAA.AAAA............AAAAAAAAAAAAA.A....................AAAAAAAAAAA...AAAAAAAAAAAAAA....AAAAAAAA.AAA........................................................................B.BBBB.BBBBBBBBBBBBB..BBBBB.....B.B.BBBB..B..BBBBBBB......BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB................................................DDDDD.DDDDDDDDDDDDDDDDDDDDDDDDD..DDDDDDD.DDD....DDDDDD......................AAAA.AAAAAAAAA.AAAAAAAAAAAAAAAA...........AAAAAAAAAAAAAAA......................AAAAAA...AAAAAAAAAAAAAAAAAAA....AAAAAAAAA.A...........................................................................BBB..BBBBBBBBBBB.....B.B.........B.BBBBB...BBBBB.........BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB...............................................DDD.DDDDDDDDDDDDDDDDDDDDDDDDDDD..DDDDD..DDD..D..DDDDD.............AAA......AAAAAAAAAAAAAA.AAAAAAAAAAAAAAAA.............AAAAAAAAAAAAAA.....................AAAAAAA.AAAAAAAAAAAA..AAAAA.....AAAAAAAAAAA..........................................................................BBBB.BB.BBBBBBBBB................BBBBBBB....BBBBB..........BBBBBBBBBBBBBBBBBB.BBBBBBBBBBBBBBBBBBBBBBBB...............................................DDDDDDDDDDDDDD..DDDD.DDDD.....DD...D.DDD.DDDDDDD.............AAAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.........AAAAAAAAAAAAAAAAAAA..................AA.AAAAAAAAAAAAAAAAAAA...AAA.....AAAAAAAAAAAA..........................................................................BBBBB.B.BBBBBB.B.................BBBBBBBB.BBBBBBB..B......BBBBBBBBBBBBBBBBBB.B..BBBBBBBBBBBBBBBBBBBBBBBBBBBB.....................................DD..DDDDDDDDDDDD...DDDD...D.....DD..DDDDDDDDDDDDDDD.............AAAAAA......AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA........AAAAAAAAAAAAAAAAAAAAAA...........AAA..AA..AAAAAAAAAAAAAAAAA..........AAAAAAAAAAAAAA.............................................................................BBBBBBBBBBBBB.................BBBBBBBB.BBBBBBBBB.......BBBBBBBBBBBBBBBBB..B.BBBBBBBBBBBBBBBBBBBBBBBBBBBBB.....................................DDDDDDDDDD...DD..DDDDDDD..........D.DDDDDDDDDDDDDDD..............AAAAAA.......AAAA...AAAA.AAAAAAAAAAAAAAA.........AAAAAAAAAAAAAAAAA.AAAAA.....AA.AAAAAAAAAAAAAAAAAAAAAAAAAA...........AAAAAAAAAAAAAAA.............................................................................BBB.BBBBBBBB..................B..BBB....BBBBBBBB........B...BBBBBBBBBB.......BBBBBBBBBBBBBBBBBBBBBBBBBBBB......................................DD.DDDDD......DDDDDDDDDD...D.....D.DDDDDDDDDDDDDDD.............AAAAAA.A......AAAAA.AAAA..AAAAAAAAAAAAAAA....AAAAA.AAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.A..........AAA.AAAAAAAA.AAA...................................................................................BBBBB....................B.BBBBB..BBBBBBB..B..........BBBBBBBB.BB......BB.BBBBBBBBBBBBBB..BBBBBBBBB......................................DDDD.........DDDDDDDDDDDD.D.....DDDDDDDDDDDDDD................AAAAAAAAA.....AAAAAAAAAAA..AAAAAAAAAAAAAAA...AA.AAAAAAAAAA.A..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAAAAAAAAA.A.........AAAAAAAAAAA..AAA...................................................................................BBBBB....................BBBBBBBBBBBBBBBBBBBB.........B.BBBBB.BB........BBBBBBBBBBBBB.......B.B.BBB.......................................DDD..D..D...DDDDDDDDDDDDDD.....DDDDDDDDDDDDDDD................AAAAAAAAAAA.AAA..AAAAAAAA.AAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAAAAAAAAAA..A.........AAAAAAAAAA....A...................................................................................BBBBBB.....................BBBBBBBBBBBBBBBBBBBB.........BBBBBBB...........BB..BBBBBBBB............BB........................................D.....DDD.DDDDDDDDDDDDDD...D..DDDDDDDD..DDDDDD..............AAAAAAAAAAAAAAAAA.AAAAAAAAA.AAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA......A.........AAAAAAAAAAAA..................................................................................BBBBBBBBB.......................BBBBBBBBB.BBBBBBB...........BBBBB.................BBBBBB..............B.........................................D....DDD.DDDDDDDDDDDDDDD..DDDDDDDDDDDD..DDDD................AAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAA....AAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAA.........AAAAAAAAAAAAAAA.............................................................................BB.BBBBBBB.......................BBBBBBBBBBBBBBBBB............BBBB.................BBBBB.......B.......................................................D..DDDDDDDDDDDDDD...DDDDDDDDDDD..DDDD..................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAA.AAAAAA..A...AA.AAAAAAAAAAAA.AA.AAAAAAAAAAAAA........AAA..AAAAAAAAAAA.............................................................................BB.BBBBBB.........................BBBBBBBBBBBBBBB............BBBB................B.BBBBB.BB...BB..........................................................DDDDDDDDDDDD..DD..DDDDDDDDDD..DD...................AAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA......AAAAAAAAAA.AAAAA..A.........AA...AAA.A..AAAAAAAAAAAAAA........AAAAAAAAAAAAAAAA..............................................................................BBBBBBBBB........................BBBBBBBBBBB................BBBBB...............BB.BBB.BBBBBBBB...........................................................DDDDDDDDDDDDDDDD..DDD.DDDDDD.......................AAA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA........AAAAAAAAAAAAAAAA.................AAA...A..AAAAAAAA.A..........AAAAAAAAAAAAAA..............................................................................BBBBBBBBB.........................BBBBBBBBBBB...............BBBBB.B.......BB....BBBBBBBBBBBBBBBBB...........................................................DDDDDDDDDDDDDDDD.....DDDDDDDD.............................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA............AAAAAAAAAAAAAAA...............AAA...A.AAAAAAA.AA.AAA.........AAAAAAAAAAA...............................................................................BBBBBBBBBB........................BBBBBBBBB.................BBBBBBBBBB.....B......BBBBBBBBBBBBBBBB............................................................DDDDDDDDDDDDDDD....DDDDDDDDD...........................AAAAAAAAAAAAA...AAAAAAAAAAAAAAAAAAAAAAA.AA.AAA.......AAAAAAAAAAAAAAAA..AA.A......AAAAAAAAAAAAAAAAAA..A............AAAAAAA.A................................................................................BBBBBBBBBB........................BBBBBBB...................BBBBBBBBBBB....BBB..BBBBBBBBBBBBBBBBBBB............................................................DDDDDDDDDDDDDDD....DDDDDDDD...........................AAAAAAAAAAAAAA....AA.AAAAAAAAAAAAAAAAAAAAAAAAAAAA...AAAA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAA....A........AAAAAAA....................................................................................BBBBBBBB........................BBBBBBBB.................BBBBBBBBBBBBB....BBBBBBBBBBBBBBBBBBBBBBB..............................................................DDDDDDDDDDDDDD....DDDDDDDDD..........................AAAAAAAAAAAAAAA..AAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAAA.....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAA..AA..........AA.......................................................................................BBBBBBB......................BBBBBBBBBB..B...........BBBBBBB..BBBBBB.B....B.BBBBBBBBBBBBBBBBBBBB...............................................................DDDDDDDDDDDDD....DDDDDDDDDD........................AAAAAAAAAA..AAAAA..AAAAAAAAA...AA.AAAAAAAAAAAAAAAAA.AAAA.....A..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAA.AA.................................................................................................BBBBB.B......................BB.BBBBBBB.BB......B..BBBBBBBBBBBBBBBBBBB......B.BBBBBBBBBBBBBBBBBB....................................................................DDDD.........DDDDDDDDDD.......................AAAAAAAAA....AAAAAAA.AAAAAAAA......AAAAAAA....AAAAAAAAAA.........AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA...AAAAA.................................................................................................BBBBBBB...................BB..BBBBBBBBBBBBBBB.BBBBBBBBBBBB.....BBBBB......B.BBBBBBBBBBBB.BBBBB....................................................................DDDD.........DDDDDDDDDD.....................AAAAAAAAAAA..AAAAAAAA..AAAAAAA......AA.AAAA......AAAAAAAAA.........AAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAA...........AAA..................................................................................BBBBBBB........................BBBBBBBBBBBBBBBBBBBBBBBBBBB......BBBBB.......BBBBBBBBBBBBBBBBBBB....................................................................DDDD..........D.DDDDDD........................AAAAAAAAA..AAAAAAA.....AAAAAA..A..A...AAA.....AAAAAAAAA...............AAAA.......AAAAAAAA.A..A.AAAAAAAAAAAAAAA............AAAA.................................................................................BBBBBB..........................BBBBBBBBBBBBBBBBBBBBBBBB........BBBB..........BB..BBBBBBBBBBBBB....................................................................DDD................DDD.D......................AAAAAAAAA...AAAAAA....AAAAAAAA..A.A...AAAA....AAAAAAAAA...............AAAA........AAAA........AAAAAAAA....AAAA..........AAAAAA................................................................................BBBBBB............................BBBBBBBBBBBBBBBBBBBBBBBB........BBB..........BB.BB..BBBBBBB.BB..........................................................................................D.......................AAAAAAAAA....AAAAAA..AAAAAAA.A.A.A....AAAAA...AAAAAAAAA......AAA......AAAA........AAAA........A.AAAAAAAAA..A........AAA.AAAAAA..................................................................................................................BBBBBBBBBBBBBBBBBBBBBB.BBBBB..BBBBBB............BB...BBB..BB...........................................................................................D.........................AAAAAAAA.....AAAAAAAAAAAAAAA....AA...AAAAA......AAA.........AA.AAAAAAAAAAAA....AAAAA..........AAA.AAA..A........AAAAAAAAAAA...............................................................................................................BB..BBBBB.BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB............BB...BBB..BBBBB......................................................................................DDDD........................AAAAAAA.....AAAAAAAAAAAAAAAAAAAAAA....AAAA..................AAAAAA...AAA....AAAAAAA............AAAAAAAAA.......AAAAAAAAAAAA...A.A...................................................................................................BB...BBBBBBBBBBBBBBBBBBB.BBBBBBBBBBBBBB.BBBBBBB............BBBBBB.BBBB...B.......................................................................................D.........................AAAAAAAA.......AAAAAAAAAAAAAAAAAAAAA....AA...................AAAAAAAAAAAAAAAAAAAAA.............A.AAAAAAA........AAAA.AAAAAA.....AA...............................................................................................BB.BBBBBBBBBBBBBBBBBB.BBBBB.......BBBBBBBBBBBBBBBB.................B...BB......................................................................................................................AAAAAA.A..........AAAAAAAAAAAAAAAAAAA........................AAAAAAAAAAAAAAAAAAAAAA................AAAAA......A.A.A.AA....AAA..A.AA............................................................................................BBBBBBBBBBBBBBBBBBBBB..BBBB...........BBBBBBBBBBBBBB.....................B.......................................................................................................................AAAAA.............AAA..AAAAAAAA...AAAAA......................AAAAAAAAAAAAAAAAAAAAAA...............AAAAA.......AAAAAAA....AAAAAAA.A..........................................................................................BBBBBBBBBBBBBBBBBBBBBBBB.BBBBBBB.........BB.BBBBBB...BB.............................................................................................................................................AAAAA...................AAAA......AAAAA......................AAAAAAAAAA.AAAAAAAAAAA........A......AAAAA.......AAA..AAAAAAAAAAAAAAAAA........................................................................................BBBBBBBBBBBBBBBBBBBB..B......BBB.........B....BBB...................................................................................................................................................AAAAA..................AA...........AAA......................AA.AAAAAAAAAAAAAAAAAA.........A.A...AAAAAAA.......AA.AAAAAAAAAAAAAAAAAA.......................................................................................BBBBBBBBBBBBBBB..BB...........B.....................................................................................................................................................................AAAAAA........................................AAAA...........AAAAAAAAAAAAAAAAAAAAAA......AAAAAAA..AAAAAAA......A.AAAAAAAAAAAAAAAAAAAAA....................................................................................BBBBBBBBBB...BB.....B.................................................................................................................................................................................AAAAAA......................................AAAAAAAA.........AAAAAAAAAAAAAAAAAAAAAA.......AAAAAAAAAAAAAAA.AA....AAAAAAAAAAAAAAA.AAAAAAA.................................................................................BBBBBBBBBB..............................................................................................................................................................................................AAAAA......................................AAAAAAAAA.........AAAAAAAAAAAAAAAAAA.AAA......AAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAA.AAAAA..............................................................................BBBBBBBBBBBB..............................................................................................................................................................................................AAAAAA......................................AAAAAAAAAA........AAAAAAAAAAAAAAAAAAAA.......A.AAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAA.AAAAAAAA...............................................................................BBBBBBB.BBBB..............................................................................................................................................................................................AAAAAA......................................AAAAAAAAAAA.......AAAAAAAAAAAAAAAAAAAAA.........AAAAAAAAAAAAAAAAAAAAAAAA.......AAA...AAAAA................................................................................BBBBBBBBBBB....BBB...B...................................................................................................................................................................................AAAAAAA........................................AAAAAAAAA........AAAAA.AAAAAAAAAAAAAA.........AAAAAA..AAAAAAAAAAAAAAAAA......AAAAAAAAAA.................................................................................BBBBBBBBBBBBBBBBBBBBBBBB.................................................................................................................................................................................AAAAAAA.........................................AAAAAAAAA.A..AA.AAAAAAAAAAAAAAAAAAAA......AAAAAA.....AAAAAAAAAAAAAAAA......AAAAAAAAAA.....................................................................................BBBBBBBBBB......B..BBB................................................................................................................................................................................AAAAAAA.........................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.......AAAAA....AAAAAAAAAAAA.AAAA......AAAA..AA......................................................................................BBBB.BBBBBBBBBB.BBB...BBB...............................................................................................................................................................................AAAAAA...........................................AAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAA......AAAAAAA....AAAAAAAA....AAAA..A..AAAAA..A.......................................................................................BBB.BBBBBBBBBBBBBB.B.BB.B...............................................................................................................................................................................AAAAAA...........................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.......AAAAAA....AAAAAAA.....AAAAAA...AAAAAA...AAA....................................................................................B..BBBBBBBBBBBBBB.B..BB.BBB.............................................................................................................................................................................AAAAAA............................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.........AAAAAAAAAAAAAAAA......AAAAAA...AAA...AA.A.........................................................................................BBBBBBBBBBBB.BB...B.B.BB............................................................................................................................................................................AAAAAA.............................................AAAAAAAAAAAAAAAAA.AAAAAAAAAAA........AAAAAAAAAAAAAAAAAA......AAAAAAA.AAAA...............................................................................................BB..BBBBBBBBBBB...BB....B.........................................................................AA.............................................................................................AAA..AAAA..AAA..........................................AAAAAAAAAAAAAAA........AAA.........AAAA.AAA.AAAAAAAAAAA.....AAAAAAAAAAAA..............................................................................................BB...........BBBBBBBBB...BBB.....................................................................AAAAA..........................................................................................AAAAA..AAAA.AAAAAAA......................................AAAAAAAAAAAAAAAAA.......AAA........AAAAA.AA..AAA.AAAAAAA...AAAAAAAAAAAAAA..............................................................................................BB...........BBBBBBBBBBB..BB...................................................................AAA.AAAA........................................................................................AAAAAA..AAAA.AAAAAAAAA.....................................AAAAAAAAAAAAAAAAA....AAAAAAA.......AA...AAA.....AA.AAAAA..AAA.AAAAAAAAA...............................................................................................BB.........BBBBBBBBBBBBB...B...................................................................AAAAAAAA........................................................................................AAAAAA..AAAA..AAAAAA..A....................................AAAAAAAA...AAAAAAA..AAAAAAAA..........AAAAAA....AAAAAAAAAAAAAA.AAAAA..A..............................................................................................BB..B..BB..BBBBBBBBBBBBBBBBBB.....................................................................AA.A...........................................................................................AAAAA.AAAAAAAAAAAAAAAA....................................AAAAAAA....AAAAAAA...AAAAAA..........AAAAAAA..AAAAAAAAAAAAAAAA.AAAAAAAA..............................................................................................BBB.BBBBB.BBBBBBBBBBBBBBBB..BB....................................................................AA.A..........................................................................................AAAAAAAAAAAAAAAAAAAAAAA....................................AAAAAA.......AAAA....AAAAA...........AAAAAAAA.AAAAAAAAAAAAAAAA.AAAAAAAA.............................................................................................BB..BBBBBBBBBBBBBBB..BBBBBBB.B....................................................................AAAAAA........................................................................................A.AAAAAAAAAAAAAAAAAA.AAAA....................................AAAAA.......AAAAA....AAAA...........AAAAAAAAAAAAAAAAAAAAAAAAA...AAAAAA..............................................................................................BBBBBBBBB.BBBBBBB....BBBBBB.B................................................................AAAAAAAAAAAA.......................................................................................AAAAAAAAAAA....AAAAAAAAA.....................................AAAAA.......A.......................AAAAAAAA..AAAAAAAAAAAAA...A.AAAA..............................................................................................BBBBBBBBBBBBBBBBB.....BBBBBBBB...............................................................AAAAAAAAAAAAAA.....AAA..............................................................................AAAAAA.AAA....AAAAAA.AA...A....................................AAA..............................AAAA.A..A....AAAAAA....A...AAAAAA..............................................................................................BBB.BBBBBBBBBBBB.BBB..BBBBBBB................................................................AAAAAAAAAAAAAAA...AAAAA.............................................................................AAAAA.........AAAAA..AAAAAAA....................................AA................................AA.........AAAA...AAA.....AAAA...A..........................................................................................B.BB.BBBBBBBBBBB.BBBB...BBBBBB................................................................AAAAAAAAAAAAAAAAAAAAAAA...........................................................................AAAAAAAA.........AAAA.AAAAAAAA...................................AAA.....................................A.AAAAAAAA...........AAAAAA.A............................................................................................BBBBBB....BBBBBBBB.......BBB.............................................................AAAAAAAAAAAAAAAAAAAAAAAAAA...........................................................................AAAAAAAA..........AAAAAAAAAAAA...................................A................................AA.AAAAAAAAAAAAAAAA....AAA..AAAAAAAAA...........................................................................................BBBBB.....BBBBBBBBB.B....BBB.............................................................AAAAAAAAAAAAAA.AAAA..AAAA.............................................................................AAAAA...........AAAAAAAAAAAAAA..................................................................AAAAAAAAAAAAAAAAAAAAA.AAAAA..AAAAAAAAA...........................................................................................BBBBB.BBBBB.BBBBBBBBB......B.............................................................AAAA..AAAAAAAAA.AAA....AA.......................................AA..A...............................A..AAAA.AA.....AAAAAAAAAAAAAAAAA...............................................................AAAAAAAAAA..AAAAAAAAAAAAAAA.AA..AAA.AAAAA............................................................................................BBBBBBBBBB..BBBBBBBBBB..BBB.............................................................AAA..AAAAAAAAAA.AAAAAAAAAAAA......A...A........................AAAA.AA.....AA.........AAAAA..........A.AAA...A....AAAAAAAAAAAAAAAAA...............................................................AAAAAAAAAAA....AAAAAAAAAAAAAA....AAA.AAAA..............................................................................................BBBBBBBBB..BBBBBBBBBB..B..............................................................AAAA..AAAAAAAAAA..AAAAAAAAAAAA.AAAA.AAAAAAAA..................AAAAA..AAA...AAA........AAAAAA....AAAAAAAAAAAAAAA..AAAAAAAAAA.A..AAAA................................................................AAAAAAAAAAAAAAAAAAAAAA...AAAAA...AAAAAAAA...............................................................................................BBBBBBBB..BBBBBBBBBBBBB..............................................................AAAA..AAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAA...............AAAAAAAA..AAA..AA.AAA.....AAAAAAA...AAAA...AAAAAAAAAA..AAAAAAAAA.....A................................................................AAAAAAAA.AAAAAAAAAAAAAA.AAAA......AAAAA.A.................................................................................................B.BBBBB.BBBBBBB..BBB...............................................................AAAAAA.AAAA..AAAAAAAAAA..AA.AAAAAAAAAAAAAAAAAAA.......AAA..AAAAAAAAAA.AAAA..AAAAAAA.....AAAAA....AAAAAAAAAAAAAAAA.AAAAAAAAAAAAA....................................................................A..AA...A.AAAAAAAAAAAAAAAAAA...AA.AAAAA..................................................................................................BB..BBBBBBBBBBB...BB................................................................AAAAAAAAAA..AAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAA...AA.AAAA..AAAAAAAAA.AAAAAAAAAAAAA.....AAAAAA....AAAAAAAAAAAAAAAAAAAAAAA.AAAAAAA..................................................................A.........A.AAAAAAAAAAAA.AAA....AAAAAAA...................................................................................................BB....BBBBBBBBB.B..................................................................AAAAAAAAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAA.....AAAAAA...A.AAAAAAAAAAAAAAAAAAAAAA.AAAAAAA..............................................................................AAAAAAAAAAA..AA...AAAAAAA......................................................................................................BBBB...BBBBBB.BBB.................................................................AAAAAAAAAA.AA.AAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA...AAAAAAAA....AAAAAAA.AAAAAAAAAAAAAAAAAA.AAAA.......................................................................AAA...A.AAAAAAA.A...A...AAAAAA............................................................................................................BBBBB...BBB...................................................................AAAAAAAAAA.A..AAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA....................................................................AAAA.A......AAAA.........A.AAAAA..............................................................................................................BB...B......................................................................AAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAA..AAAAAAAAAAAAAAAAAA..AAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.A.AA.AAAAAA..AAAAA.AA....................................................................AAAAAAA..A..AAAA...A.AA.AAAAAAAAAAAAA....................................................................................................................................................................................AAAA....AAAAAAAAAAA.AAAAAAAAAAAAAA.AAAAAAA.AAAAAAAAA...AAAAA.AAAAAAAAAAAA.A.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.A.AA.AA..AAAAA..A..AAAAAA...................................................................AAAAAAAA.A.AAAAAAAA..AA..AAAAAAAAAAAA.................................................................................................................................................................................A..AAAA......AAAAAAAAAAAAA.AAAAAAAAAA.AAAAAA...AAAAAAAAAAAAAAA..AAAAAAAAAA..A.AAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAA.A......AAAAAA...A..AAAAA.................................................................A.AAAAAAAAAAA.AAAAAAAAAA...AAAAAAAAAAAA.............................................................................................................................................................................AA..AAAAAAA........AAAAAAAAAAA.A.AAAA.AAA.AAAAAA...AAAAAAAAAAAAAAA...AAAAAAAA.....AAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAA.A.......AAAA...AAA..AAA...................................................................A.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA...........................................................................................................................................................................AAAAAAAAAAA.........AAAAAAAAAAAAAAAAAA..AA.AAAAAAAAAAAAAAAAAAAAAA....AAAAAAAA........AAAAAAAAAAAA.AAAAAAAA.AA.AAAA.A........AAAA...AA...AAAA........................................................................AAAAAAAAAAAAAAAAAAAAAA...AAAAA.AA..........................................................................................................................................................................A.AAAAAAAAA.........AAAAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA..........AAAAAAAA.AAA.A.AAAAAAAA...AA..AAAAA.....AAA.AA........AAAA.AAAAAA.AAAA..........................................................................AAAAAA.AAAAAAAAAAAAAA..AAAA..............................................................................................................................................................................AAAAAAAAAAAA........AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA......AAAAAAAAAA..AAAA.AAAAAAAA.A..AA..AAAA......AA.AAA........AAAA...AA...AAA...........................................................................A.AAAA..AAAAAA..A..A....AAA..............................................................................................................................................................................AAAAAAAAAA..........AAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAA.A.AAA..AAAAAAA....AAAAAAAAA....AAAAAAAA......AAAAAA......AAAAA.........................................................................AAAAAAAAAAAAAAAAAAAAA.....AAA..............................................................................................................................................................................AAAAAAAA...........AAAAA.AAAAAAAAAAAAAAAAAAAAAAA.A..AAAAAAAAAAAAAAAAAAAAAAAAAA......AAAAAAAAAA..A.AAAAAAAAA.AAAAAAAAA.....AAAAAAAAA..AAA.AAA.........................................................................AA.AAAAAAAAAAAAAAAA.AA.....AA.............................................................................................................................................................................AAAAAAAA.A.........AAAAAAAAAAAAAAAAAAAAAAAA..AA..A...AAAAAAAAAAAAAAAAAAAAAAAAAAAA.....AAAAAAAA.A.AAAA.AAAAA.AAAAAAAAAAA.AA.AAAAAAAAAAAAAAAAAA.........................................................................A.AAAAAAAAAAAAAAAAAAAAA...A..............................................................................................................................................................................A.AAAAA.AA..........AAAAAAAA.AAAAAAAAAAAAAA..AA.......AAAA..AAAAAAAAAAAAAAAAAAAAAAA....AAAAAAAA......A..AAAA.AAAAAAAAAAAAA.AAAAAAAA.A.A.AAAAAA.........................................................................AAAAAAAAAAAAAAAAAAAAAAAA..A...............................................................................................................................................................................AAAA.AA...........AA.AAAAAAAAAAAAAAAAAAAA.AA...A..........AAAAAAAAAAAAAAAAAAAA..AAA..AAAAAAAA.......AAAAAAAAAAAAAAAAAAAAAA.AAAAAAA.AA.AAAAAA..........................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAA...............................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAA.....A.AA.......AAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAA.....AAAAAAAAAAAAAAAA.AAAAAAAAAAAA.AAA.AAAAAAAA..........................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAA..............................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAA...A.A.AAAAA..AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....A..AAAAAAAAA.AAAAAAAAAAAAAAA.AAAAAAAAAAAA...........................................................................AAAAAAAAAAAAAAA.AAAAAAAAAAAAA..............................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAA..AAAA..A....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....A.AAAAAAAAAA..AAAAA.A.AAAAAAAAAAAAAAAAAAAA...........................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAA..............................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAA...A.A..AA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....AAAAAAAAAA.A.A..AAAAAAAAA.AAAAAAA.AAAA.AAA..........................................................................A.AAAAAAAAAAAAAAAAAAAAAAAAAAA.................................................................................................................................................................................................AAAAAAAA.AAA..AAAAAAAAA.A.AAA.......AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.A...AAAAAAAAAAAA....AAAAAAA.AAAAAAAAAAAAAA.AAAA..........................................................................AAAAAAA.AAAAAAAAAAAAAAAAAAAAA.................................................................................................................................................................................................AAAAA........A.AAAAAAA..AAA..AA.......AAAAAAAAAAAAAAAAAAAAAA.....AAAAAAAAAA...AAAAAAAA.A......AAAAAA.AAAAA...AAAAAAAAAAAA.......................................................................AAAAAAAAAA.AAAAAAAAAAAAAAAAAAAA.................................................................................................................................................................................................AAAAA.........A.A....A...A....AA...A..AAAAAAAAAAAA..AAAAAAA........AAAAAAA....AAAA.AAAA.........AAAAAAAAA......AAAAAAA.AA.................................................................AAA....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.................................................................................................................................................................................................AAAAAA.........A.A.A..A.A.AA....AAA....A.AAAA.AAA.AA.AAAAAA........AAAAAAA...AAAAA.............AAAAAAAAAAA......AAAA....AAAA..........................................................AAA.AAA...AAAAAAAAAAAAAAAAAAAAAAAA.AAAAAA...............................................................................................................................................................................................AAAAAAAAAAAAAA......A.AA.A...AA..AA.AAA......AAA.A.A....AAAAA.........AAAAAA..AAAAAAAAA.........A.AAAAAAAAAAAAAA..AAAAA....AAAA..........................................................AAAAAAAAAAAAA..A.AAAAAAAAAAAAAAAA.AAAAAA...............................................................................................................................................................................................AAAAAAAAAAAAAAAAA.....AAAAAAA.AAAA...AAAA......AA........AAAA.........A.AAAAA.AAAAAAAAA.......A.AAAAAAA.AAAAAAAA...AAAA....AAAAA........................................................AAAAAAAAAAAAA....AA..AAAAA..............................................................................................................................................................................................................AAAAAAAAAAAAAAAAAA......A.A..AA..AAAAAAA..A.............AAAAA......A.....AAAAAAAAAAAAAAA...A.AA.AAAAAA....AAAAAAA..AAA......AAAA........................................................AAAAAAAAAAAA....A.A..AAAAA..............................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAA......A.AA...A..AAAAAA..............AAAAAAA..AA......AAAAAAA.AA.AAAA..AAAAAAAAAA......AAAAAAAA..A.........A..........................................................AAAAAAAAA.......AA..AAAAA..............................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAA...A.AAA.AAAA.A....AAA.....AA.....A.AAAAAAAAAAA.....AAAAAAAAAAAAAAAA..AAAAAAA.A.....AAAAAAA........................................................................AAAAAAAA............AAAAA..............................................................................................................................................................................................................AAAAAAAAAAA.AAAAAAAAAAA...A...A............AA...AAAA....AAAAAA.AAAAAAA....AAAAAAAAAAAAAAAAAAAAAAAA.AAAA.AAAAAA.AA............................................................................AAAAAAAA........AAAAA..............................................................................................................................................................................................................AAAAAAAAAA.A.AAAAAAAAAA...A.AAA...........AAA...AAAAA....AAAAAAAAAAAAAA..AAAAAAAAAAAAA.AAAAAAAAAAA..AAAAAAAAA................................................................................AAAAAAAA........AAAAA..............................................................................................................................................................................................................AAAAAAAA.....AAAAAAAAAAA.....A...AAAAAA.....AA..AAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA....................................................................................AAAAA.........AAAAA...........................................................................................................................................................................................................A.AAAAAAAAA.AAA..AAAAAAAAAAA..A.AAA.AAAAAA....AA....AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAA...................................................................................AAA............AAAAA...........................................................................................................................................................................................................AAAAAAAAA..AAAA..AAAAAAA.AAA...A.A.AAAAAAA....A.A...AA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA......................................................................................AA............AAAAA...........................................................................................................................................................................................................AAAAAAAAA.AAAAA.AAAAAA...AAA...A...AAAAAAAA....AA...AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....................................................................................AA............AAAAA...........................................................................................................................................................................................................A..AAAAAA.AAAAAAAAAAAAAAAAAA..A....AAAAAAAAA..AAA..AAAAAAAAAAAAAAAAAAAAAAAAA..AAAAAAAAA...AAAAAAA.AAAAAAAAA.A...................................................................................................AAAAA...............................................................................................................................................................................................................AAAAAA.AAA..AAAAAAAA.AA...AA..AAAAAAAAAA...A....AAAAAAAAAAAAAAAAAAAAA........A.AAAAA..AAAAAAAAAAAAAAAAAAAA.A...............................................................................................AAAAAA...............................................................................................................................................................................................................AAAAAA..AA.AAAAAAAAAAAA..A...AAAAAAAAAAA..AA..AAAAAAAAAAAAAA....AAAAAA........A..AAA...AAAAAAAA.AAAAAAAAAAAAA..............................................................................................AAAAA.A..............................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAA..A..AAAAAAAAA....AA....AAAAAAAAAAAAA..A..AAAA..A.AA.A...........AAA...A.AAAAAAAAAAAAA..............................................................................................AAAAAA...............................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAA....A.AAAAAAAAAA..........AA...AAAAAAA.A.A.AAAAA..A.AAA.A...............A.AAAAAAAAAAAAA..............................................................................................AAAAAA.A..............................................................................................................................................................................................................A.AAAAAAAAAAAAAAAAAAA.....AAAAAAAAAAAA...AA.A......AAAAAAAA..A..AAAAAA.....AA.A...AA.A........AA.AAAAAAAAAAA.............................................................................................AAAAAAAAAA................................................................................................................................................................................................................AAAAAAAAA...AAAAAA.....AAAAAAAAAAAA..A..AAAA...AAAAAAAAA..AA.AAAAAAAA....AAAA.AAAA.A....AA....AAAAAAAAAAAA............................................................................................AAAAAAAAAA..............................................................................................................................................................................................................AAAAAAAAAAA..AAAAAA..A...AAAAAAAAAAAA.A..AAAAA..AAAAAAAAAAA..AA.AAAAAA.AAA..A..A..A..AA...AA..AAAAAAAAAAAAAAAA.........................................................................................AAAAAAAA.A.............................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAA.AA..A..AAAAAAAAAAA..A.AAAAAAAA.AAAAAAAAAAA.AA.A.AAAAAAAA.....A..AAA..AAA....A.AAAAAAAAAAAAAAA......................................................................................A.AAAAAAAAAAA.............................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAA...A.AAAAAAAA..AA.A...AAAAAAAAAAAA.AAAAAA.A.A.AAAAAAAAAA..AAAA..AAAAAA.AA...A...AAAAAAAAAAAA.......................................................................................AAAAAAAAAAAAA.............................................................................................................................................................................................................AAAAAAAAAAAAAAAAA.AA.A.AAAAAAAA.....AA.AAAAAAAAAAAAA...AAAAA..AAA.A.A.AAAAAAAAAAAA...A.A.A.A...AA.......AAAAAAAA.....................................................................................AAAAAAAAAAAAAA...............................................................................................................................................................................................................AAAAAAA...AAAAAAA.AAAAAAAAA.A.AAA.A..AAAAAAAAAAAA.AA..AAAAA...AA.A..AAAAAAAAAAAAAA..A..................AAAAAA.......................................................................................AAAAAAAAAAAAA..........................................................................................................................................................................................................................AAAAA.A.AAAAAAAA...AAAA.AAAAAAAAAAAAAA...A..AAAAAA..AAAAA.A.AAAAAAAAAAAA..A.A................AAAA.........................................................................................AAAAAAAAAAA..........................................................................................................................................................................................................................A.AAAA..AAAAAAAAAA..AA...AAAAAAAAAAAAAA.AA..AAAAAAA......A.AAAAAAAAAAAAAAAA.A.A.A..............AA..........................................................................................AAAAAA.AAAA.........................................................................................................................................................................................................................A...AAAA.AAAAAAAAAA......AAAAAAAAAAAAA..AA..AAAAAAAAAAAAAA..AAAAAAAAAAAAAAA.AAA.AA.AAA.......................................................................................................AAA....AA...........................................................................................................................................................................................................................A.AAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAA.AA.AAAAAA...AAAAAAAAAAAAAAA.A...AAAAAAAAAAAA.AA......................................................................................................A...................................................................................................................................................................................................................................A.AAAAAAAAAAAAAAAAAAAAAAAAAAAAA..AAAA..AA.AAAAA.A..AAAAAAAAAAAAAA......AAAAAAAAAAAAA..A..........................................................................................................................................................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.....A.A..AAAAAAA.AAAAAAAAAAAAA......AAAAAAAAAAAAAAA..........................................................................................................................................................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AA.A.A...AAAAA..A.AAAA.AA.AAAAA......AAAAAAAAAAAAA.A...................................................................................................................................................................................................................................................................................................................................AAA.A.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA...AA.AA..AAAAAAAAAA.AAAA..AAAA......AAAAAAAAAAAAA.....................................................................................................................................................................................................................................................................................................................................A..AA.AAAAAA.AAAAAAAA.AAAAAAAAAAAAA.AAAAAAA..A......AAAAAAAA.AAA.AAAAAAAA....AAAAAA.AAAAAA..A.........................................................................................................................................................................................................................................................................................................................................AAAAAA.AAAAA......AAAAAAAAA....AAAAA........A.AAAAAAAAAAAAA.AAAAAAAAAAAAAAAA...AAAAAAA..........................................................................................................................................................................................................................................................................................................................................AAAAAAAAAAA..AAAAA..AA..........AAAAA...AAA.AAAAAAAAAAAAAAAAA.AAAAAAAAAAAAA.AA.AAAAAAA...........................................................................................................................................................................................................................................................................................................................................AAAAAAAAAA.AAAAAAAAAAAAAAA.A.A.AAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.A...........................................................................................................................................................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAA.A............................................................................................................................................................................................................................................................................................................................................AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAA.AAA.A...............................................................................................................................................................................................................................................................................................................................................A.AAAA.AAAAA.AAAAAAAAAAAAAAAAAAAAAAAAA.....AAAA..A.A....AA...A.AAAAA.AAAA.A.AAAA..............................................................................................................................................................................................................................................................................................................................................A....A...A.A.A....AAAAAA.AAAAAAA.A.A.AAA......AA.AAAAA..AAA.AAAAAAAAAAAAAA.A.AAA.A.............................................................................................................................................................................................................................................................................................................................................A.A.AAAAAAA..A.....AAA......AAAA.A.A.AA..............AAAAA.AAAAAAAAAA.AAA..AAA.AAA.................................................................................................................................................................................................................................................................................................................................................A..AAAAAA.AAAAA.......A......AA..AA................AAA...........A......AAA.A........................................................................................................................................................................................................................................................................................................................................................AA......AAAAAAAAAAAAA.AAAA.A..A...............AAA..................AAA.....................................................................................................................................................................................................................................................................................................................................................................A.A..AAAAAAAA..A...................AA...................A.A.............................................................................................................................................................................................................................................................................................................................................................................................................................AAA.....................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................";

    public static int Landmass(float x, float y)
    {
      var col = (int)((x - MinX) / CellW);
      var row = (int)((y - MinY) / CellW);
      if (col < 0 || col >= GW || row < 0 || row >= GH)
      {
        return -1;
      }

      var ch = Grid[row * GW + col];
      return ch == '.' ? -1 : ch - 'A';
    }
  }

  // ---- freebie: complete each faction's first quest at 2:00 ----
  public static class QuestAutoComplete
  {
    private const bool ComputerOnly = true; // v27: bots only -- humans must complete their own starting quests

    private static readonly HashSet<int> _timedDone = new();

    public static void Setup()
    {
      var t = CreateTimer();
      TimerStart(t, 120f, false, OnFire);
      var tq = CreateTimer();
      TimerStart(tq, 30f, true, OnTimedQuests); // v30
      var te = CreateTimer();
      TimerStart(te, 3f, true, OnEarlyQuestTick); // v32
    }

    // v30/v31: AiConfig.QuestRules -- force-complete a bot faction's quest once all its conditions hold.
    private static void OnTimedQuests()
    {
      var list = AiConfig.QuestRules;
      for (var i = 0; i < list.Count; i++)
      {
        var r = list[i];
        if (_timedDone.Contains(i) || SimpleBot.Clock < r.AtSec)
        {
          continue;
        }

        var f = FindFaction(r.Faction);
        if (f == null || f.Player == null || GetPlayerController(f.Player) != MAP_CONTROL_COMPUTER)
        {
          _timedDone.Add(i); // faction absent or human-played: never applies
          continue;
        }

        if (r.UnlessPlayed != "")
        {
          var other = FindFaction(r.UnlessPlayed);
          if (other != null && other.Player != null
              && GetPlayerSlotState(other.Player) == PLAYER_SLOT_STATE_PLAYING)
          {
            _timedDone.Add(i);
            SimpleBot.Log("quest rule skipped: " + r.Title + " (" + r.UnlessPlayed + " is played)");
            continue;
          }
        }

        if (r.CapitalLost != 0 && !CapitalLost(r.CapitalLost, f.Player))
        {
          continue; // condition not met yet -- re-check in 30 s
        }

        if (r.CpHeldByAlly != 0 && !CpHeldByAlly(r.CpHeldByAlly, f.Player))
        {
          continue;
        }

        if (r.CapitalHeldByAlly != 0 && !CapitalHeldByAlly(r.CapitalHeldByAlly, f.Player))
        {
          continue;
        }

        if (r.MinCps > 0 && f.Player.GetPlayerData().ControlPoints.Count < r.MinCps)
        {
          continue;
        }

        if (r.HeroAlive != 0 && !HeroAlive(r.HeroAlive, f.Player))
        {
          continue;
        }

        _timedDone.Add(i);
        var q = FindQuest(f, r.Title);
        if (q == null)
        {
          SimpleBot.Log("quest rule: '" + r.Title + "' not found for " + r.Faction);
        }
        else if (q.Progress == QuestProgress.Incomplete || q.Progress == QuestProgress.Undiscovered)
        {
          if (ForceComplete(q, r.Faction))
          {
            SimpleBot.LogPin("quest rule COMPLETED: " + r.Faction + " / " + r.Title);
          }
        }
        else
        {
          SimpleBot.Log("quest rule not needed: " + r.Faction + " / " + r.Title
                        + (q.Progress == QuestProgress.Complete ? " (already completed)" : " (already failed)"));
        }
      }
    }

    private static QuestData FindQuest(Faction f, string title)
    {
      var quests = f.GetAllQuests();
      for (var k = 0; k < quests.Count; k++)
      {
        if (quests[k].Title == title)
        {
          return quests[k];
        }
      }

      return null;
    }

    // v32 fix: "lost" = a capital of this unit type is DEAD (or removed) or owned by an ENEMY PLAYER. A capital that
    // is still neutral (e.g. Stormwind Keep, preplaced Neutral-Passive and handed over later) is NOT lost.
    private static bool CapitalLost(int unitType, player owner)
    {
      var caps = CapitalManager.GetAll();
      for (var i = 0; i < caps.Count; i++)
      {
        if (caps[i].UnitType != unitType)
        {
          continue;
        }

        var cu = caps[i].Unit;
        if (cu == null || GetUnitTypeId(cu) == 0 || GetUnitState(cu, UNIT_STATE_LIFE) <= 0.405f)
        {
          return true; // destroyed
        }

        var o = GetOwningPlayer(cu);
        if (o != owner && GetPlayerId(o) < 24 && !IsPlayerAlly(o, owner))
        {
          return true; // captured by an enemy player
        }
      }

      return false;
    }

    private static bool CapitalHeldByAlly(int unitType, player owner)
    {
      var caps = CapitalManager.GetAll();
      for (var i = 0; i < caps.Count; i++)
      {
        var cu = caps[i].Unit;
        if (caps[i].UnitType != unitType || cu == null || GetUnitTypeId(cu) == 0
            || GetUnitState(cu, UNIT_STATE_LIFE) <= 0.405f)
        {
          continue;
        }

        var o = GetOwningPlayer(cu);
        if (o == owner || (GetPlayerId(o) < 24 && IsPlayerAlly(o, owner)))
        {
          return true;
        }
      }

      return false;
    }

    private static bool HeroAlive(int unitType, player owner)
    {
      var all = LegendaryHeroManager.GetAll();
      for (var i = 0; i < all.Count; i++)
      {
        var hu = all[i].Unit;
        if (hu != null && GetUnitTypeId(hu) == unitType && GetOwningPlayer(hu) == owner
            && GetUnitState(hu, UNIT_STATE_LIFE) > 0.405f)
        {
          return true;
        }
      }

      return false;
    }

    private static bool CpHeldByAlly(int cpId, player owner)
    {
      var cp = SimpleBot.SafeCp(cpId);
      if (cp == null || cp.Unit == null)
      {
        return false;
      }

      var o = GetOwningPlayer(cp.Unit);
      return o == owner || (GetPlayerId(o) < 24 && IsPlayerAlly(o, owner));
    }

    private static Faction FindFaction(string name)
    {
      var all = FactionManager.GetAllFactions();
      for (var i = 0; i < all.Count; i++)
      {
        if (all[i].Name == name)
        {
          return all[i];
        }
      }

      return null;
    }

    private static void OnFire()
    {
      var factions = FactionManager.GetAllFactions();
      for (var i = 0; i < factions.Count; i++)
      {
        var f = factions[i];
        var pl = f.Player;
        if (pl == null)
        {
          continue;
        }

        if (ComputerOnly && GetPlayerController(pl) != MAP_CONTROL_COMPUTER)
        {
          continue;
        }

        var q = f.StartingQuest;
        if (q != null && q.Progress != QuestProgress.Complete)
        {
          ForceComplete(q, f.Name); // v32: Kul Tiras no longer skipped -- its '%' text bug is caught instead
        }
      }
    }

    // v32: complete a quest; a WL error inside its reward code (e.g. Kul Tiras' unescaped '%') is caught and
    // logged instead of breaking the AI timer. The quest's own OnComplete may then be only partly applied.
    private static bool ForceComplete(QuestData q, string facName)
    {
      try
      {
        q.Progress = QuestProgress.Complete;
        return true;
      }
      catch (System.Exception)
      {
        SimpleBot.LogPin("quest '" + q.Title + "' (" + facName + "): WL error during completion -- caught");
        return false;
      }
    }

    // ---- v32 early quests: complete during turn N-1 (N = the WL turn the quest expires on) ----------------
    private static List<QuestData> _earlyQ;
    private static List<int> _earlyTurn;
    private static List<string> _earlyFac;
    private static int _earlyCursor;

    private static void OnEarlyQuestTick()
    {
      var turn = GameTimeManager.Turn;
      if (turn < 1)
      {
        return;
      }

      if (_earlyQ == null)
      {
        BuildEarlyQuestList();
      }

      // At most ONE completion per tick (3 s), so the rescued bases/heroes don't all land in the same frame.
      for (; _earlyCursor < _earlyQ.Count; _earlyCursor++)
      {
        if (turn < _earlyTurn[_earlyCursor] - 1)
        {
          return; // list is sorted by expiry turn -> nothing else is due yet
        }

        var q = _earlyQ[_earlyCursor];
        if (q.Progress == QuestProgress.Incomplete || q.Progress == QuestProgress.Undiscovered)
        {
          if (ForceComplete(q, _earlyFac[_earlyCursor]))
          {
            SimpleBot.LogPin("early quest COMPLETED (turn " + I2S(turn) + "): " + _earlyFac[_earlyCursor] + " / " + q.Title);
          }

          _earlyCursor++;
          return;
        }

        SimpleBot.Log("early quest skipped: " + _earlyFac[_earlyCursor] + " / " + q.Title
                      + (q.Progress == QuestProgress.Complete ? " (already completed)" : " (already failed)"));
      }
    }

    // Bot factions' quests whose title is in AiConfig.EarlyQuests, sorted by expiry turn (insertion sort -- stable,
    // deterministic, no Dictionary iteration).
    private static void BuildEarlyQuestList()
    {
      _earlyQ = new List<QuestData>();
      _earlyTurn = new List<int>();
      _earlyFac = new List<string>();
      var factions = FactionManager.GetAllFactions();
      for (var i = 0; i < factions.Count; i++)
      {
        var f = factions[i];
        if (f.Player == null || GetPlayerController(f.Player) != MAP_CONTROL_COMPUTER)
        {
          continue;
        }

        for (var e = 0; e < AiConfig.EarlyQuests.Count; e++)
        {
          var eq = AiConfig.EarlyQuests[e];
          var q = FindQuest(f, eq.Title);
          if (q == null)
          {
            continue;
          }

          var at = _earlyQ.Count;
          while (at > 0 && _earlyTurn[at - 1] > eq.ExpireTurn)
          {
            at--;
          }

          _earlyQ.Insert(at, q);
          _earlyTurn.Insert(at, eq.ExpireTurn);
          _earlyFac.Insert(at, f.Name);
        }
      }

      SimpleBot.Log("early quests queued for bots: " + I2S(_earlyQ.Count));
    }
  }

  public static class SimpleBot
  {
    private const float Tick = 0.5f;              // v8: one bot per tick -> ~13 bots serviced every ~6.5s
    private const float ProductionEvery = 15.0f;
    private const float ProdTick = 1.0f;           // v12: production timer interval; one bot per tick
    private const float HeartbeatEvery = 20.0f;    // v12: re-issue attack-move at most this often per bot
    private const float ObjectiveRadius = 1500f;   // v14: scripted-doctrine step clear/capture radius
    private const int QueuePerBuilding = 2;
    private const float ScanRadius = 10000f;
    private const float AttackEvery = 6.0f;       // (unused in v8; stagger sets the cadence)
    private const float CommitTimeout = 30.0f;
    private const int MinArmy = 20;          // v13: mass this many before attacking (was 5)
    public static int BotGoldBonus = 25;     // v13/v21/v24: flat gold per bot per production service; set live with -gold*
    private const float ReviveEvery = 180.0f; // v13: free hero revival for bots every 3 min
    private const float DiagEvery = 3.0f;
    private const int DefendThreshold = 5;         // Rule 0: recall if >N enemies near an owned anchor
    private const float DefendRadius = 1500f;      // how close counts as "at my base/CP"
    private const float DamagedFraction = 0.97f;   // v29: anchor below this share of max HP = "being hit"
    private const float NeutralBias = 0.5f;        // v31: a neutral frontier counts as this much of its distance
                                                   // v34.2: 0.75 -> 0.5 (stronger pull to free CPs before players)

    // v8 control toggles (chat-driven -> evaluated in lockstep on every machine, so desync-safe)
    public static bool BotBreak = false;          // -botbreak : master emergency stop
    public static bool ProdOn = true;             // -prod     : production on/off
    public static bool AtkOn = true;              // -atk      : attack/gather orders on/off

    private static readonly List<player> Bots = new();
    private static readonly Dictionary<int, float> LastAttack = new();
    public static readonly Dictionary<int, unit> Committed = new();
    private static readonly Dictionary<int, float> CommitTime = new();
    public static readonly Dictionary<int, int> Cand = new();

    private static int _cursor;                    // v8: round-robin bot index
    // per-bot state written by logic, read-only for AiDebug
    public static readonly Dictionary<int, int> BotArmy = new();
    public static readonly Dictionary<int, int> BotLm = new();
    public static readonly Dictionary<int, int> BotFac = new();
    public static readonly Dictionary<int, string> BotState = new();
    public static readonly Dictionary<int, string> BotTargetName = new(); // v21: current graph/attack target
    // deterministic op counters (same on every machine -> safe to display, never branch logic on them)
    public static int OpUnits;
    public static int OpOrders;
    public static int LastOpUnits;
    public static int LastOpOrders;
    public static int MaxOpUnits;
    public static int ProdOps;
    public static int MaxProdOps;
    private static int _prodCursor;                // v12: round-robin for staggered production
    // v28: per-subsystem op counters (deterministic; display only). Combat = OpUnits above.
    public static int DefOps;          // part of OpUnits spent in the defense scan (this combat tick)
    public static int LastDefOps;
    public static int MaxDefOps;
    public static int WaveOps;         // units touched by the 15s wave tick (gate sweep, wave gates, gather, teleport)
    public static int LastWaveOps;
    public static int MaxWaveOps;
    public static int LastSweepOps;    // units touched by the last whole-map gate sweep
    // v28: diagnostics written by logic, read by AiDebug. Never branch logic on these.
    public static readonly Dictionary<int, BotDiag> Diag = new();
    public static readonly List<string> EventLog = new();
    private const int EventLogCap = 300;
    // v28: hero types that died PERMANENTLY (from LegendaryHero.Died). Never re-train / revive these.
    public static readonly HashSet<int> PermaDead = new();
    private static int _legendsHooked;
    private static readonly int PermaDiesMarker = FourCC("LEgo"); // WL adds this dummy ability to PermaDies heroes
    private static int _defWorst;      // enemies near the anchor FindDefenseTarget picked (for the log)
    private static int _defCreeps;     // ...of which neutral (creeps)
    private static int _lastCreepCount;
    private static readonly Dictionary<int, float> LastPtX = new();
    private static readonly Dictionary<int, float> LastPtY = new();
    private static readonly Dictionary<int, float> LastIssueTime = new();
    private static readonly Dictionary<int, bool> FiredEvents = new(); // v15: node events fired once
    private static readonly Dictionary<int, int> _wavePhase = new();      // v23: 0 idle, 1 gather, 2 channel
    private static readonly Dictionary<int, float> _wavePhaseAt = new();
    private static readonly Dictionary<int, float> _waveLastDone = new();
    private static readonly Dictionary<int, float> _waveDestX = new();
    private static readonly Dictionary<int, float> _waveDestY = new();
    private static readonly Dictionary<int, effect> _waveSrcFx = new();
    private static readonly Dictionary<int, effect> _waveDstFx = new();
    private static readonly Dictionary<int, bool> _waveWasActive = new();
    // v24: per-faction node-system state. An event (invasion arrival, Legion summon) writes the faction's
    // current edge-set name here; the graph walker reads it. Absent -> AiGraph.DefaultEdgeSet decides.
    private static readonly Dictionary<string, string> _factionEdgeSet = new();
    private static readonly Dictionary<string, bool> _gateOpen = new(); // named graph gates (e.g. "greymane")
    private static bool _thandolOpened;      // v25: force-open Thandol Span once at 20 min
    private static bool _tempestWired;       // v25: inject Blackwald<->Tempest Reach edge once
    private static HashSet<int> _combinedNodes; // v26: CP ids in each graph, for runtime edge-set resolution
    private static HashSet<int> _kalimdorNodes;
    private static readonly HashSet<int> _oneShotDone = new(); // consumed one-shot objective indices
    private static rect _itemRect;           // v24: reused hero item-grab rect (no per-call allocation)
    private static unit _grabHero;           // v24: hero currently grabbing (read by the enum callback)
    private static float _clock;
    private static float _diag;

    public static int BotCount => Bots.Count;

    public static float Clock => _clock;

    // v31: flat subsidy scaled by AiConfig.FactionGoldPct (balance knob; 100% when the faction isn't listed).
    private static int GoldBonusFor(player p)
    {
      var f = p.GetPlayerData().Faction;
      if (f != null && AiConfig.FactionGoldPct.ContainsKey(f.Name))
      {
        return BotGoldBonus * AiConfig.FactionGoldPct[f.Name] / 100;
      }

      return BotGoldBonus;
    }

    // ---- v32 tech pool / event researches / CP-capture counting ----------------------------------------------
    // v33: unit/ability tech steps per faction (built once): ByFaction + Extra, minus events / never-grant,
    // deduplicated, then expanded by level (all level 1 -> multi-level ones at 2 -> at 3).
    private sealed class TechSteps
    {
      public readonly List<int> Ids = new();
      public readonly List<int> Levels = new();
    }

    private static readonly Dictionary<string, TechSteps> _techSteps = new();
    private static HashSet<int> _eventIds;

    private static TechSteps TechStepsFor(string facName)
    {
      if (_techSteps.ContainsKey(facName))
      {
        return _techSteps[facName];
      }

      if (_eventIds == null)
      {
        _eventIds = new HashSet<int>();
        for (var i = 0; i < AiUpgrades.Events.Count; i++)
        {
          _eventIds.Add(AiUpgrades.Events[i].ResearchId);
        }
      }

      var ids = new List<int>();
      AddPoolIds(ids, AiUpgrades.ByFaction, facName);
      AddPoolIds(ids, AiUpgrades.Extra, facName);
      var st = new TechSteps();
      for (var lv = 1; lv <= 3; lv++)
      {
        for (var i = 0; i < ids.Count; i++)
        {
          var max = AiUpgrades.MaxLevel.ContainsKey(ids[i]) ? AiUpgrades.MaxLevel[ids[i]] : 1;
          if (lv <= max)
          {
            st.Ids.Add(ids[i]);
            st.Levels.Add(lv);
          }
        }
      }

      _techSteps[facName] = st;
      return st;
    }

    private static void AddPoolIds(List<int> into, Dictionary<string, List<int>> src, string facName)
    {
      if (!src.TryGetValue(facName, out var ups) || ups == null)
      {
        return;
      }

      for (var i = 0; i < ups.Count; i++)
      {
        var id = ups[i];
        if (!_eventIds.Contains(id) && !AiUpgrades.NeverGrant.Contains(id) && !into.Contains(id))
        {
          into.Add(id);
        }
      }
    }

    // v33: spend ONE building/misc credit: upgrade a town hall (else another upgradable building) for free --
    // WC3 refuses the order if the tier's requirements aren't met; otherwise grant the next misc research.
    private static readonly Dictionary<int, int> _bSpent = new();
    private static readonly Dictionary<int, int> _miscIdx = new();

    private static void SpendBuildingCredit(player p, int pid, unit hall, unit other)
    {
      var spent = _bSpent.ContainsKey(pid) ? _bSpent[pid] : 0;
      if (TryFreeUpgrade(p, hall, spent) || TryFreeUpgrade(p, other, spent))
      {
        _bSpent[pid] = spent + 1;
        return;
      }

      var mi = _miscIdx.ContainsKey(pid) ? _miscIdx[pid] : 0;
      if (mi < AiUpgrades.MiscAll.Count)
      {
        SetPlayerTechResearched(p, AiUpgrades.MiscAll[mi], 1);
        _miscIdx[pid] = mi + 1;
        _bSpent[pid] = spent + 1;
        Log("P" + I2S(pid) + " " + FacName(p) + ": misc tech granted (" + I2S(AiUpgrades.MiscAll[mi]) + ")");
      }
    }

    private static bool TryFreeUpgrade(player p, unit b, int salt)
    {
      if (b == null)
      {
        return false;
      }

      var opts = AiUpgrades.BuildingUpgrades[GetUnitTypeId(b)];
      for (var k = 0; k < opts.Count; k++)
      {
        var target = opts[(salt + k) % opts.Count]; // vary the branch (e.g. which demon gate) between upgrades
        if (FreeOrder(p, b, target))
        {
          Log("P" + I2S(GetPlayerId(p)) + " " + FacName(p) + ": building upgrade " + GetUnitName(b) + " -> "
              + GetObjectName(target));
          return true;
        }
      }

      return false;
    }

    // Issue an order that costs resources for free: top up, order, restore. Returns WC3's accept/refuse.
    private static bool FreeOrder(player p, unit u, int orderId)
    {
      var gold = GetPlayerState(p, PLAYER_STATE_RESOURCE_GOLD);
      var wood = GetPlayerState(p, PLAYER_STATE_RESOURCE_LUMBER);
      SetPlayerState(p, PLAYER_STATE_RESOURCE_GOLD, gold + 10000);
      SetPlayerState(p, PLAYER_STATE_RESOURCE_LUMBER, wood + 10000);
      var ok = IssueImmediateOrderById(u, orderId);
      SetPlayerState(p, PLAYER_STATE_RESOURCE_GOLD, gold);
      SetPlayerState(p, PLAYER_STATE_RESOURCE_LUMBER, wood);
      return ok;
    }

    // Event research: if this building researches an event the bot doesn't have yet, order it. WC3 itself refuses
    // the order while the research's requirements aren't met ("only when they can"); once accepted it is free --
    // gold/lumber are topped up for the order and restored right after, so the research costs nothing.
    private static readonly Dictionary<int, float> _eventTried = new(); // key: pid*100 + event index

    private static void TryEventResearch(player p, unit building, int typeId)
    {
      var evs = AiUpgrades.Events;
      for (var i = 0; i < evs.Count; i++)
      {
        var ev = evs[i];
        if (ev.BuildingId != typeId || _clock < ev.NotBefore || GetPlayerTechCount(p, ev.ResearchId, true) > 0)
        {
          continue;
        }

        var key = GetPlayerId(p) * 100 + i;
        if (_eventTried.ContainsKey(key) && _clock - _eventTried[key] < 60f)
        {
          continue; // accepted recently (research in progress) or refused recently
        }

        _eventTried[key] = _clock;
        if (FreeOrder(p, building, ev.ResearchId))
        {
          Log("P" + I2S(GetPlayerId(p)) + " " + FacName(p) + ": event research ordered (" + I2S(ev.ResearchId) + ")");
        }
      }
    }

    // v34: quest building upgrades -- free, as soon as WC3 accepts (requirements met), retried every 60 s.
    private static readonly Dictionary<unit, float> _questUpTried = new(); // per building (only looked up, never iterated)

    private static void TryQuestUpgrade(player p, unit building, int typeId)
    {
      if (!AiUpgrades.QuestUpgrades.ContainsKey(typeId))
      {
        return;
      }

      if (_questUpTried.ContainsKey(building) && _clock - _questUpTried[building] < 60f)
      {
        return;
      }

      _questUpTried[building] = _clock;
      if (FreeOrder(p, building, AiUpgrades.QuestUpgrades[typeId]))
      {
        Log("P" + I2S(GetPlayerId(p)) + " " + FacName(p) + ": quest upgrade " + GetUnitName(building) + " ordered");
      }
    }

    // CP captures per bot (for the tech pool). A WC3 change-owner trigger on every player; deduplicated per
    // unit+owner so a double fire counts once; transfers made by the AI itself are not counted.
    private static readonly Dictionary<int, int> _cpCaptures = new();
    private static unit _lastCapUnit;
    private static player _lastCapOwner;
    private static bool _suppressCaptureCount;

    private static void SetupCaptureCounting()
    {
      var t = CreateTrigger();
      for (var i = 0; i < 28; i++)
      {
        TriggerRegisterPlayerUnitEvent(t, Player(i), EVENT_PLAYER_UNIT_CHANGE_OWNER, null);
      }

      TriggerAddAction(t, OnUnitChangeOwner);
    }

    private static void OnUnitChangeOwner()
    {
      if (_suppressCaptureCount)
      {
        return;
      }

      var u = GetTriggerUnit();
      var owner = GetOwningPlayer(u);
      if (u == _lastCapUnit && owner == _lastCapOwner)
      {
        return;
      }

      if (!IsBot(owner) || !ControlPointManager.Instance.UnitIsControlPoint(u))
      {
        return;
      }

      _lastCapUnit = u;
      _lastCapOwner = owner;
      var pid = GetPlayerId(owner);
      _cpCaptures[pid] = (_cpCaptures.ContainsKey(pid) ? _cpCaptures[pid] : 0) + 1;
    }

    // Ownership transfers done by the AI (grants, hand-overs) -- not counted as captures.
    private static void GiveUnit(unit u, player to)
    {
      _suppressCaptureCount = true;
      SetUnitOwner(u, to, true);
      _suppressCaptureCount = false;
    }

    // ---- v32 starter kit: every bot owns at least one barracks and one altar --------------------------------
    // Bots never construct buildings. A faction that starts without them (Stormwind, Illidan, ...) would idle
    // until a quest rescues its base. At 0:20 and again at 3:00 (after the 2:00 starting-quest rescue) each
    // missing building is created -- the cheapest barracks / altar type of the faction -- on a worker's spot
    // (the worker is removed: "workers become buildings"), else next to the main building / home CP.
    private static readonly float[] StarterKitAt = { 20f, 180f };
    private static int _starterKitDone;

    private static void RunStarterKit()
    {
      if (_starterKitDone >= StarterKitAt.Length || _clock < StarterKitAt[_starterKitDone])
      {
        return;
      }

      _starterKitDone++;
      for (var b = 0; b < Bots.Count; b++)
      {
        var p = Bots[b];
        var f = p.GetPlayerData().Faction;
        if (f == null)
        {
          continue;
        }

        var barracks = new List<int>();
        AddCategory(f, UnitCategory.Barracks, barracks);
        var altars = new List<int>();
        AddCategory(f, UnitCategory.Altar, altars);

        var haveBarracks = false;
        var haveAltar = false;
        unit hall = null;
        var workers = new List<unit>();
        var g = CreateGroup();
        GroupEnumUnitsOfPlayer(g, p, null);
        unit u;
        while ((u = FirstOfGroup(g)) != null)
        {
          GroupRemoveUnit(g, u);
          WaveOps++;
          if (GetUnitState(u, UNIT_STATE_LIFE) <= 0.405f)
          {
            continue;
          }

          var t = GetUnitTypeId(u);
          if (IsUnitType(u, UNIT_TYPE_STRUCTURE))
          {
            haveBarracks = haveBarracks || ListHas(barracks, t);
            haveAltar = haveAltar || ListHas(altars, t);
            if (hall == null && IsUnitType(u, UNIT_TYPE_TOWNHALL))
            {
              hall = u;
            }
          }
          else if (IsUnitType(u, UNIT_TYPE_PEON))
          {
            workers.Add(u);
          }
        }

        DestroyGroup(g);
        if (!haveBarracks)
        {
          PlaceStarter(p, Cheapest(barracks), workers, hall, "barracks");
        }

        if (!haveAltar)
        {
          PlaceStarter(p, Cheapest(altars), workers, hall, "altar");
        }
      }
    }

    private static int Cheapest(List<int> types)
    {
      var best = 0;
      var bestCost = 0;
      for (var i = 0; i < types.Count; i++)
      {
        var c = GetUnitGoldCost(types[i]);
        if (best == 0 || c < bestCost)
        {
          best = types[i];
          bestCost = c;
        }
      }

      return best;
    }

    private static void PlaceStarter(player p, int typeId, List<unit> workers, unit hall, string what)
    {
      if (typeId == 0)
      {
        return; // faction has no building of this category
      }

      // where: a worker's spot (worker removed) > ring around the main building > ring around the first CP
      var x = 0f;
      var y = 0f;
      var found = false;
      if (workers.Count > 0)
      {
        var w = workers[workers.Count - 1];
        workers.RemoveAt(workers.Count - 1);
        x = GetUnitX(w);
        y = GetUnitY(w);
        RemoveUnit(w);
        found = true;
      }

      if (!found && hall != null)
      {
        found = FindOpenSpot(GetUnitX(hall), GetUnitY(hall));
        x = _spotX;
        y = _spotY;
      }

      if (!found)
      {
        var cps = p.GetPlayerData().ControlPoints;
        if (cps.Count > 0 && cps[0].Unit != null)
        {
          found = FindOpenSpot(GetUnitX(cps[0].Unit), GetUnitY(cps[0].Unit));
          x = _spotX;
          y = _spotY;
        }
      }

      if (!found)
      {
        Log("P" + I2S(GetPlayerId(p)) + " " + FacName(p) + ": starter " + what + " -- no place found");
        return;
      }

      CreateUnit(p, typeId, x, y, 270f);
      LogPin("P" + I2S(GetPlayerId(p)) + " " + FacName(p) + ": starter " + what + " created ("
          + GetObjectName(typeId) + ")");
    }

    // First walkable point on a ring 600 units around (cx, cy) -> _spotX/_spotY. Overlap with other buildings
    // isn't checked (CreateUnit places structures regardless).
    private static float _spotX;
    private static float _spotY;

    private static bool FindOpenSpot(float cx, float cy)
    {
      for (var i = 0; i < 8; i++)
      {
        var a = i * 0.785398f;
        _spotX = cx + 600f * Cos(a);
        _spotY = cy + 600f * Sin(a);
        if (!IsTerrainPathable(_spotX, _spotY, PATHING_TYPE_WALKABILITY))
        {
          return true;
        }
      }

      return false;
    }

    // ---- v32/v33 Ragnaros pedestal mission (one shot) -------------------------------------------------------
    // 1. A bot owning one of AiConfig.RagnarosTriggerCps sends its highest-level hero to the pedestal; within
    //    1500 the hero is raised to RagnarosHeroLevel. WL completes "Lord of the Firelands" (level >= 10 in the
    //    rect) and hands the pedestal to the bot.
    // 2. With that level-12 hero standing by (<= 1500), the bot casts the pedestal's Summon Ragnaros
    //    (order "selfdestruct", WL's channel ability A0PY). WL spawns Ragnaros as a hostile boss.
    // 3. The hero kills Ragnaros "for free" (one lethal damage instance credited to the hero), so the kill and
    //    its rewards (Sulfuras) go to the bot.
    // The hero ignores army orders for the whole mission. Travel gives up after 6 min; at most 3 attempts.
    private static unit _ragHero;
    private static player _ragBot;
    private static float _ragSince;
    private static int _ragAttempts;
    private static bool _ragDone;
    private static unit _ragPedestal;
    private static bool _ragSummoned;
    private static float _ragSummonAt;
    private const float RagSummonX = 12332f; // where WL's QuestRagnaros spawns him
    private const float RagSummonY = -10597f;

    private static void RunRagnarosMission()
    {
      if (_ragDone)
      {
        return;
      }

      if (_ragSummoned)
      {
        RagnarosKill();
        return;
      }

      if (_ragPedestal == null)
      {
        _ragPedestal = FindUnitOfType(AiConfig.RagnarosPedestalId, AiConfig.RagnarosX, AiConfig.RagnarosY, 800f);
        if (_ragPedestal == null)
        {
          RagnarosEnd("pedestal not found -> mission off");
          return;
        }
      }

      if (GetUnitTypeId(_ragPedestal) == 0 || GetUnitState(_ragPedestal, UNIT_STATE_LIFE) <= 0.405f)
      {
        RagnarosEnd("pedestal already used");
        return;
      }

      var pedOwner = GetOwningPlayer(_ragPedestal);
      if (GetPlayerId(pedOwner) < 24)
      {
        if (pedOwner != _ragBot)
        {
          RagnarosEnd("pedestal taken by P" + I2S(GetPlayerId(pedOwner)) + " " + FacName(pedOwner));
          return;
        }

        // phase 2: our bot owns the pedestal -- summon only with the level-12 hero standing by
        if (!RagHeroValid())
        {
          _ragHero = BestHero(_ragBot);
          if (_ragHero == null)
          {
            return; // wait for a hero (revive / training)
          }
        }

        if (RagHeroReady() && IssueImmediateOrder(_ragPedestal, "selfdestruct"))
        {
          _ragSummoned = true;
          _ragSummonAt = _clock;
          Log("Ragnaros mission: " + FacName(_ragBot) + " summons Ragnaros");
          return;
        }

        RagHeroApproach();
        return;
      }

      // phase 1: travel
      if (_ragHero != null)
      {
        if (!RagHeroValid() || _clock - _ragSince > 360f)
        {
          Log("Ragnaros mission: " + FacName(_ragBot) + " hero " + (RagHeroValid() ? "too slow" : "lost") + " -> released");
          _ragHero = null;
          return;
        }

        RagHeroApproach();
        return;
      }

      if (_ragAttempts >= 3)
      {
        RagnarosEnd("3 attempts used");
        return;
      }

      for (var c = 0; c < AiConfig.RagnarosTriggerCps.Count; c++)
      {
        var cp = SafeCp(AiConfig.RagnarosTriggerCps[c]);
        if (cp == null || cp.Unit == null)
        {
          continue;
        }

        var o = GetOwningPlayer(cp.Unit);
        if (!IsBot(o))
        {
          continue;
        }

        var hero = BestHero(o);
        if (hero == null)
        {
          continue;
        }

        _ragHero = hero;
        _ragBot = o;
        _ragSince = _clock;
        _ragAttempts++;
        Log("Ragnaros mission: " + FacName(o) + " sends " + GetUnitName(hero) + " (lvl " + I2S(GetHeroLevel(hero)) + ")");
        RagHeroApproach();
        return;
      }
    }

    private static bool RagHeroValid()
    {
      return _ragHero != null && GetUnitTypeId(_ragHero) != 0 && GetUnitState(_ragHero, UNIT_STATE_LIFE) > 0.405f
             && GetOwningPlayer(_ragHero) == _ragBot;
    }

    private static bool RagHeroNear(float x, float y)
    {
      var dx = GetUnitX(_ragHero) - x;
      var dy = GetUnitY(_ragHero) - y;
      return dx * dx + dy * dy < 1500f * 1500f;
    }

    private static bool RagHeroReady()
    {
      return RagHeroValid() && RagHeroNear(AiConfig.RagnarosX, AiConfig.RagnarosY)
             && GetHeroLevel(_ragHero) >= AiConfig.RagnarosHeroLevel;
    }

    // walk to the pedestal; raise to level 12 once within 1500 (BEFORE entering WL's rect)
    private static void RagHeroApproach()
    {
      if (!RagHeroValid())
      {
        return;
      }

      if (RagHeroNear(AiConfig.RagnarosX, AiConfig.RagnarosY) && GetHeroLevel(_ragHero) < AiConfig.RagnarosHeroLevel)
      {
        SetHeroLevel(_ragHero, AiConfig.RagnarosHeroLevel, true);
      }

      IssuePointOrder(_ragHero, "move", AiConfig.RagnarosX, AiConfig.RagnarosY);
    }

    // phase 3: find the summoned boss (hostile hero at WL's spawn point) and kill him with the standing-by hero
    private static void RagnarosKill()
    {
      unit rag = null;
      var g = CreateGroup();
      GroupEnumUnitsInRange(g, RagSummonX, RagSummonY, 1000f, null);
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        if (rag == null && IsUnitType(u, UNIT_TYPE_HERO) && GetOwningPlayer(u) == Player(PLAYER_NEUTRAL_AGGRESSIVE)
            && GetUnitState(u, UNIT_STATE_LIFE) > 0.405f)
        {
          rag = u;
        }
      }

      DestroyGroup(g);
      if (rag == null)
      {
        if (_clock - _ragSummonAt > 45f)
        {
          RagnarosEnd("Ragnaros not found after the summon (already dead?)");
        }

        return;
      }

      if (!RagHeroValid())
      {
        _ragHero = BestHero(_ragBot);
      }

      if (!RagHeroValid())
      {
        return;
      }

      if (!RagHeroNear(RagSummonX, RagSummonY) || GetHeroLevel(_ragHero) < AiConfig.RagnarosHeroLevel)
      {
        RagHeroApproach(); // the level-12 hero must be present for the free kill
        return;
      }

      UnitDamageTarget(_ragHero, rag, 1000000f, true, false, ATTACK_TYPE_CHAOS, DAMAGE_TYPE_UNIVERSAL,
        WEAPON_TYPE_WHOKNOWS);
      if (GetUnitState(rag, UNIT_STATE_LIFE) <= 0.405f)
      {
        RagnarosEnd(FacName(_ragBot) + "'s " + GetUnitName(_ragHero) + " slew Ragnaros");
      }
    }

    private static void RagnarosEnd(string why)
    {
      _ragDone = true;
      _ragHero = null;
      Log("Ragnaros mission: " + why);
    }

    private static unit BestHero(player p)
    {
      unit best = null;
      var g = CreateGroup();
      GroupEnumUnitsOfPlayer(g, p, null);
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        WaveOps++;
        if (IsUnitType(u, UNIT_TYPE_HERO) && GetUnitState(u, UNIT_STATE_LIFE) > 0.405f
            && (best == null || GetHeroLevel(u) > GetHeroLevel(best)))
        {
          best = u;
        }
      }

      DestroyGroup(g);
      return best;
    }

    // ---- v32 Legion hand-overs to the Scourge --------------------------------------------------------------
    // Before the betrayal (Legion still allied to the Scourge): Capital Palace / Lordaeron City taken by a bot
    // Legion go to the Scourge -- the Scourge, not the demons, sacks Lordaeron.
    private static void LegionPalaceHandover()
    {
      var legion = PlayerOfFaction("Legion");
      var scourge = PlayerOfFaction("Scourge");
      if (legion == null || scourge == null || !IsBot(legion) || !IsPlayerAlly(legion, scourge))
      {
        return;
      }

      var cp = SafeCp(AiConfig.LordaeronCityCpId);
      if (cp != null && cp.Unit != null && GetOwningPlayer(cp.Unit) == legion)
      {
        GiveUnit(cp.Unit, scourge);
        Log("Legion took Lordaeron City before the betrayal -> handed to the Scourge");
      }

      var caps = CapitalManager.GetAll();
      for (var i = 0; i < caps.Count; i++)
      {
        var cu = caps[i].Unit;
        if (caps[i].UnitType == AiConfig.CapitalPalaceId && cu != null && GetOwningPlayer(cu) == legion
            && GetUnitState(cu, UNIT_STATE_LIFE) > 0.405f)
        {
          GiveUnit(cu, scourge);
          Log("Legion took Capital Palace before the betrayal -> handed to the Scourge");
        }
      }
    }

    // At the summon/betrayal: every CP the bot Legion holds on the Combined graph (Lordaeron, Northrend, EK) goes
    // to the Scourge, so the Legion stops defending/reinforcing a continent it has left.
    private static void LegionNorthHandover(player legion)
    {
      var scourge = PlayerOfFaction("Scourge");
      if (legion == null || scourge == null || !IsBot(legion))
      {
        return;
      }

      EnsureNodeSets();
      var mine = legion.GetPlayerData().ControlPoints;
      var give = new List<unit>();
      for (var i = 0; i < mine.Count; i++)
      {
        var cu = mine[i].Unit;
        if (cu != null && _combinedNodes.Contains(GetUnitTypeId(cu)) && !AiGraph.ArgusNodes.Contains(GetUnitTypeId(cu)))
        {
          give.Add(cu); // collect first: transferring changes the list being read (v34: Argus stays Legion)
        }
      }

      for (var i = 0; i < give.Count; i++)
      {
        GiveUnit(give[i], scourge);
      }

      Log("Legion betrayal: " + I2S(give.Count) + " northern CPs handed to the Scourge");
    }

    // ---- v34.1 Kul Tiras repair -------------------------------------------------------------------------------
    // WL's Admiralty reward crashes on a '%' in its power text before it rescues the Kul Tiras base (seen as
    // "Kul'tiras failed to execute OnQuestProgressChanged: invalid use of '%' in replacement string"). Once the
    // quest is Complete, every Neutral-Passive unit still inside WL's Kul Tiras rescue area is rescued for Kul
    // Tiras with WL's own Rescue -- the step the crash skipped. Runs once; finds nothing if WL's reward worked.
    // (The City of Admirals power itself is still lost; that needs the text fix in WL's Loc.cs.)
    private static bool _ktRepairDone;

    private static void KulTirasRepair()
    {
      if (_ktRepairDone)
      {
        return;
      }

      var kt = PlayerOfFaction("Kul'tiras");
      var fac = kt != null ? kt.GetPlayerData().Faction : null;
      if (fac == null)
      {
        return;
      }

      QuestData adm = null;
      foreach (var q in fac.GetAllQuests())
      {
        if (q.Title == "The Admiralty of Kul Tiras")
        {
          adm = q;
        }
      }

      if (adm == null || adm.Progress == QuestProgress.Failed)
      {
        _ktRepairDone = true;
        return;
      }

      if (adm.Progress != QuestProgress.Complete)
      {
        return;
      }

      _ktRepairDone = true;
      var found = new List<unit>();
      var g = CreateGroup();
      GroupEnumUnitsOfPlayer(g, Player(PLAYER_NEUTRAL_PASSIVE), null); // includes hidden (prepared) units
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        WaveOps++;
        var x = GetUnitX(u);
        var y = GetUnitY(u);
        if (x >= AiWorld.KulTirasRescueMinX && x <= AiWorld.KulTirasRescueMaxX
            && y >= AiWorld.KulTirasRescueMinY && y <= AiWorld.KulTirasRescueMaxY)
        {
          found.Add(u);
        }
      }

      DestroyGroup(g);
      for (var i = 0; i < found.Count; i++)
      {
        found[i].Rescue(kt);
      }

      LogPin("Kul Tiras repair: " + I2S(found.Count) + " units/buildings rescued (Admiralty reward)");
    }

    // ---- v31 CP grants ----------------------------------------------------------------------------------------
    private static readonly HashSet<int> _grantDone = new();

    private static void RunCpGrants()
    {
      var list = AiConfig.CpGrants;
      for (var i = 0; i < list.Count; i++)
      {
        if (_grantDone.Contains(i) || _clock < list[i].AtSec)
        {
          continue;
        }

        _grantDone.Add(i);
        var gr = list[i];
        var p = PlayerOfFaction(gr.Faction);
        if (p == null || !IsBot(p))
        {
          continue;
        }

        for (var c = 0; c < gr.CpIds.Count; c++)
        {
          var cp = SafeCp(gr.CpIds[c]);
          if (cp == null || cp.Unit == null || GetOwningPlayer(cp.Unit) != Player(PLAYER_NEUTRAL_AGGRESSIVE))
          {
            continue; // only free (creep-held) CPs; never take from a player or a quest lock
          }

          ClearCreeps(GetUnitX(cp.Unit), GetUnitY(cp.Unit), 1100f);
          GiveUnit(cp.Unit, p);
          Log(gr.Faction + ": granted " + GetUnitName(cp.Unit));
        }
      }
    }

    private static void ClearCreeps(float x, float y, float r)
    {
      var g = CreateGroup();
      GroupEnumUnitsInRange(g, x, y, r, null);
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        WaveOps++;
        if (GetOwningPlayer(u) == Player(PLAYER_NEUTRAL_AGGRESSIVE) && !IsUnitType(u, UNIT_TYPE_STRUCTURE)
            && !ControlPointManager.Instance.UnitIsControlPoint(u))
        {
          RemoveUnit(u);
        }
      }

      DestroyGroup(g);
    }

    public static BotDiag GetDiag(int pid)
    {
      if (!Diag.ContainsKey(pid))
      {
        Diag[pid] = new BotDiag();
      }

      return Diag[pid];
    }

    // v34.1: important one-off events (portals, summon, repairs, caught errors) are ALSO kept in a pinned list
    // that the ring buffer can't push out -- early-game lines were lost in long games.
    public static readonly List<string> PinnedLog = new();

    public static void LogPin(string msg)
    {
      Log(msg);
      if (PinnedLog.Count < 150)
      {
        PinnedLog.Add(TimeTag(_clock) + " " + msg);
      }
    }

    // v28: append one line to the decision/event ring buffer (state CHANGES only -- never per-tick spam).
    public static void Log(string msg)
    {
      EventLog.Add(TimeTag(_clock) + " " + msg);
      if (EventLog.Count > EventLogCap)
      {
        EventLog.RemoveAt(0);
      }
    }

    public static string TimeTag(float t)
    {
      var s = R2I(t);
      var m = s / 60;
      var r = s - m * 60;
      return I2S(m) + ":" + (r < 10 ? "0" : "") + I2S(r);
    }

    private static string FacName(player p)
    {
      var f = p.GetPlayerData().Faction;
      return f != null ? f.Name : "?";
    }

    public static player BotAt(int i) => Bots[i];

    // 0.34.3: these collections are created here instead of with "= new()" at their declaration. The C#->Lua
    // translator (CSharp.lua) builds every static field that has an initializer inside one Lua function, and it
    // only handles about 55 of them per class (Lua upvalue limit); fields past that were silently left nil -- that
    // broke the portals, area clears and with them all bot attacks in v34-v34.2. Keep SimpleBot below ~40
    // initialized static fields (tools/check_refs.py checks it) and create new collections here instead.
    private static void InitState()
    {
      _nodeSets = new Dictionary<string, HashSet<int>>();
      _goalHopCache = new Dictionary<string, Dictionary<int, int>>();
      _stuckTarget = new Dictionary<int, unit>();
      _stuckSince = new Dictionary<int, float>();
      _stuckHp = new Dictionary<int, float>();
      _stuckOwner = new Dictionary<int, player>();
      _blackUnits = new Dictionary<int, List<unit>>();
      _blackUntil = new Dictionary<int, List<float>>();
      _tickErrors = new HashSet<string>();
      _killTarget = new Dictionary<int, unit>();
      _killDone = new HashSet<int>();
      _killSince = new Dictionary<int, float>();
      _clearDone = new HashSet<int>();
      _clearActive = new Dictionary<int, int>();
      _clearSince = new Dictionary<int, float>();
      _pairLink = new List<int>();
      _pairA = new List<unit>();
      _pairB = new List<unit>();
    }

    public static void Setup()
    {
      InitState();
      for (var i = 0; i < 24; i++)
      {
        var p = Player(i);
        if (GetPlayerSlotState(p) == PLAYER_SLOT_STATE_PLAYING && GetPlayerController(p) == MAP_CONTROL_COMPUTER)
        {
          Bots.Add(p);
          LastAttack[i] = -999f;
          Committed[i] = null;
          CommitTime[i] = 0f;
          Cand[i] = 0;
        }
      }

      EnsureLegendHooks();
      SetupCaptureCounting(); // v32

      RegisterControl("-botbreak", OnBotBreak);
      RegisterControl("-prod", OnProd);
      RegisterControl("-atk", OnAtk);

      var combat = CreateTimer();
      TimerStart(combat, Tick, true, OnCombatTick);
      var prod = CreateTimer();
      TimerStart(prod, ProdTick, true, OnProductionTick);
      var rev = CreateTimer();
      TimerStart(rev, ReviveEvery, true, OnHeroReviveTick);
      var evt = CreateTimer();
      TimerStart(evt, 2.0f, true, OnEventCheck);
      var wave = CreateTimer();
      TimerStart(wave, 15f, true, OnWaveTick);

      // v34: a bot Legion starts in ARGUS (its map start): its workers there become buildings (the v24
      // Northrend base is gone). Argus reaches Northrend through the portal until the summon.
      var legionBase = CreateTimer();
      TimerStart(legionBase, 3.0f, false, SpawnLegionBase);
      var portals = CreateTimer();
      TimerStart(portals, 2.0f, false, SetupPortals);
    }

    // A minimal, functional Legion base beside the Crystalsong Forest CP: a town hall, an altar, a couple of
    // farms for food, and 5 producers -- all pulled from Legion's own faction categories. Bots only, so a
    // human Legion is never handed free structures. Ids come from the faction, so a WL rename can't wrong-spawn.
    private static void SpawnLegionBase()
    {
      var legion = PlayerOfFaction("Legion");
      if (legion == null || !IsBot(legion))
      {
        return;
      }

      var fac = legion.GetPlayerData().Faction;
      if (fac == null)
      {
        return;
      }

      // remove the Legion Heralds (workers) at the Argus start -- they become the buildings below
      var g = CreateGroup();
      GroupEnumUnitsInRange(g, AiWorld.LegionStartX, AiWorld.LegionStartY, 1500f, null);
      unit u;
      var removed = 0;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        if (GetOwningPlayer(u) == legion && GetUnitTypeId(u) == AiWorld.LegionHeraldId)
        {
          RemoveUnit(u);
          removed++;
        }
      }

      DestroyGroup(g);

      // 0.34.3: each building gets its own open spot (searched outward from the start location, footprint
      // checked, kept apart from the others) -- the old fixed ring of 8 found only 1 walkable spot in Argus.
      // 5 barracks, 1 altar, 2 farms.
      var types = new List<int>();
      var barracks = new List<int>();
      AddCategory(fac, UnitCategory.Barracks, barracks);
      for (var i = 0; i < 5 && barracks.Count > 0; i++)
      {
        types.Add(barracks[i % barracks.Count]);
      }

      AddFirstOf(fac, UnitCategory.Altar, types);
      AddFirstOf(fac, UnitCategory.Farm, types);
      AddFirstOf(fac, UnitCategory.Farm, types);
      var placed = 0;
      var usedX = new List<float>();
      var usedY = new List<float>();
      for (var i = 0; i < types.Count; i++)
      {
        if (!FindBuildSpot(AiWorld.LegionStartX, AiWorld.LegionStartY, usedX, usedY))
        {
          continue;
        }

        CreateUnit(legion, types[i], _spotX, _spotY, 270f);
        usedX.Add(_spotX);
        usedY.Add(_spotY);
        placed++;
      }

      LogPin("Legion: Argus start -- " + I2S(removed) + " workers replaced by " + I2S(placed) + " buildings");
    }

    // open ground for a ~512-wide building: rings 500..2000 out, 16 directions; centre and 4 corners walkable and
    // >= 600 from every spot already used. Result in _spotX/_spotY.
    private static bool FindBuildSpot(float cx, float cy, List<float> usedX, List<float> usedY)
    {
      for (var r = 500f; r <= 2000f; r += 250f)
      {
        for (var i = 0; i < 16; i++)
        {
          var a = i * 0.392699f;
          var x = cx + r * Cos(a);
          var y = cy + r * Sin(a);
          if (IsTerrainPathable(x, y, PATHING_TYPE_WALKABILITY)
              || IsTerrainPathable(x + 220f, y + 220f, PATHING_TYPE_WALKABILITY)
              || IsTerrainPathable(x - 220f, y + 220f, PATHING_TYPE_WALKABILITY)
              || IsTerrainPathable(x + 220f, y - 220f, PATHING_TYPE_WALKABILITY)
              || IsTerrainPathable(x - 220f, y - 220f, PATHING_TYPE_WALKABILITY))
          {
            continue;
          }

          var free = true;
          for (var k = 0; k < usedX.Count && free; k++)
          {
            var dx = usedX[k] - x;
            var dy = usedY[k] - y;
            free = dx * dx + dy * dy >= 600f * 600f;
          }

          if (free)
          {
            _spotX = x;
            _spotY = y;
            return true;
          }
        }
      }

      return false;
    }

    private static void AddFirstOf(Faction fac, UnitCategory cat, List<int> into)
    {
      var ids = new List<int>();
      AddCategory(fac, cat, ids);
      if (ids.Count > 0)
      {
        into.Add(ids[0]);
      }
    }

    private static void PlaceFirstOf(player p, Faction fac, UnitCategory cat, float x, float y)
    {
      var ids = new List<int>();
      AddCategory(fac, cat, ids);
      if (ids.Count > 0)
      {
        CreateUnit(p, ids[0], x, y, 270f);
      }
    }

    private static void RegisterControl(string cmd, System.Action act)
    {
      var tr = CreateTrigger();
      for (var i = 0; i < 24; i++)
      {
        TriggerRegisterPlayerChatEvent(tr, Player(i), cmd, true);
      }

      TriggerAddAction(tr, () =>
      {
        try
        {
          act();
        }
        catch (System.Exception e)
        {
          DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 15, "|cffff4444AI command " + cmd + " failed:|r " + e.Message);
        }
      });
    }

    private static void OnBotBreak()
    {
      BotBreak = !BotBreak;
      DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 6,
        BotBreak ? "|cffff5555bots PAUSED (-botbreak)|r" : "|cff55ff55bots RESUMED|r");
    }

    private static void OnProd()
    {
      ProdOn = !ProdOn;
      DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 6, "production " + (ProdOn ? "ON" : "OFF"));
    }

    private static void OnAtk()
    {
      AtkOn = !AtkOn;
      DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 6, "attacking " + (AtkOn ? "ON" : "OFF"));
    }

    private static void AddCategory(Faction f, UnitCategory cat, List<int> into)
    {
      if (f.TryGetObjectByCategory(cat, out var ids) && ids != null)
      {
        for (var i = 0; i < ids.Count; i++)
        {
          into.Add(ids[i]);
        }
      }
    }

    private static bool ListHas(List<int> list, int v)
    {
      for (var i = 0; i < list.Count; i++)
      {
        if (list[i] == v)
        {
          return true;
        }
      }

      return false;
    }

    private static void TrainSome(unit building, List<int> pool)
    {
      for (var i = 0; i < QueuePerBuilding; i++)
      {
        IssueImmediateOrderById(building, pool[GetRandomInt(0, pool.Count - 1)]);
      }
    }

    // Heroes train at Altars by unit-type. No public API maps an UNtrained hero to its faction, so we read
    // faction-correct ids from AiConfig (source-derived) -- no id spray. Trains any config hero the bot
    // lacks alive (a dead one gets revived instead), one per altar per cycle, gold-gated; WC3's hero limit
    // self-caps. Fallback: a faction missing from AiConfig simply trains no heroes (no crash, no spray).
    private static void TrainHeroes(unit altar, Faction faction, player p)
    {
      if (GetUnitCurrentOrder(altar) != 0)
      {
        return; // altar already training or reviving something
      }

      if (!AiConfig.Heroes.TryGetValue(faction.Name, out var ids) || ids == null)
      {
        return; // no config for this faction -> skip hero training (graceful fallback)
      }

      for (var i = 0; i < ids.Count; i++)
      {
        var hid = ids[i];
        if (PermaDead.Contains(hid))
        {
          continue; // v28: died permanently (e.g. Cenarius) -- WL would adopt a re-trained copy, so never train it
        }

        if (HaveLiveHero(p, hid))
        {
          continue; // already have this hero alive
        }

        if (IssueImmediateOrderById(altar, hid))
        {
          return; // trained a new hero, or revived a dead one; one per altar per cycle
        }
      }
    }

    private static bool HaveLiveHero(player p, int heroType)
    {
      var all = LegendaryHeroManager.GetAll();
      for (var i = 0; i < all.Count; i++)
      {
        var hu = all[i].Unit;
        if (hu != null && all[i].UnitType == heroType && GetOwningPlayer(hu) == p
            && GetUnitState(hu, UNIT_STATE_LIFE) > 0.405f)
        {
          return true;
        }
      }

      return false;
    }

    // v13: bring back bots' dead (non-perma) heroes for free every few minutes, at their start location.
    // Bots are too gold-starved to pay altar revive costs, so heroes were staying dead.
    private static void OnHeroReviveTick()
    {
      if (BotBreak)
      {
        return;
      }

      var heroes = LegendaryHeroManager.GetAll();
      for (var b = 0; b < Bots.Count; b++)
      {
        var p = Bots[b];
        var loc = GetPlayerStartLocation(p);
        var hx = GetStartLocationX(loc);
        var hy = GetStartLocationY(loc);
        for (var i = 0; i < heroes.Count; i++)
        {
          var hu = heroes[i].Unit;
          if (hu == null || GetUnitTypeId(hu) == 0)
          {
            continue; // never had a unit, or WL already removed it (perma-death disposes the unit)
          }

          if (GetOwningPlayer(hu) != p || GetUnitState(hu, UNIT_STATE_LIFE) > 0.405f)
          {
            continue;
          }

          // v28: perma-death guard. 'LEgo' = WL's PermaDies marker ability (readable even though the C# property
          // is set-only); PermaDead = types WL reported as permanently killed. Either -> stays dead.
          if (GetUnitAbilityLevel(hu, PermaDiesMarker) > 0 || PermaDead.Contains(heroes[i].UnitType))
          {
            continue;
          }

          ReviveHero(hu, hx, hy, true);
          Log("P" + I2S(GetPlayerId(p)) + " " + FacName(p) + ": free revive " + GetUnitName(hu));
        }
      }
    }

    // v28: subscribe to every registered LegendaryHero's Died event (append-only list -> hook only the new ones;
    // cheap enough to call every production tick, so legends registered mid-game are covered too).
    // v32: also remember each legend's unit type at hook time, so a permanent death blacklists the ORIGINAL form
    // even when WL has already switched the legend to a new form (Cenarius -> ghost) in an earlier handler.
    private static readonly List<LegendaryHero> _hookedLegends = new();
    private static readonly List<int> _hookedType = new();

    private static void EnsureLegendHooks()
    {
      var all = LegendaryHeroManager.GetAll();
      for (var i = _legendsHooked; i < all.Count; i++)
      {
        all[i].Died += OnLegendDied;
        _hookedLegends.Add(all[i]);
        _hookedType.Add(all[i].UnitType);
      }

      _legendsHooked = all.Count;
    }

    private static void OnLegendDied(LegendDiedEventArgs e)
    {
      var lh = e.LegendaryHero;
      if (!e.Permanent || lh == null)
      {
        return;
      }

      var orig = lh.UnitType;
      for (var i = 0; i < _hookedLegends.Count; i++)
      {
        if (_hookedLegends[i] == lh && _hookedType[i] != 0)
        {
          orig = _hookedType[i];
          break;
        }
      }

      if (orig != 0 && !PermaDead.Contains(orig))
      {
        PermaDead.Add(orig);
        LogPin("PERMA-DEATH " + lh.Name + " (" + I2S(orig) + ") -> excluded from train/revive");
      }

      RemoveBannedForm(lh); // the new form may already exist if WL's handler ran first
    }

    // v32: a perma-dead legend that WL brought back as a BANNED new form (AiConfig.BannedNewForms, e.g. the
    // Cenarius ghost) is removed again when a bot owns it. Also scanned on the 15 s wave tick, because WL's
    // handler may run after ours.
    private static void RemoveBannedForm(LegendaryHero lh)
    {
      var lu = lh.Unit;
      if (lu == null || GetUnitTypeId(lu) == 0 || !AiConfig.BannedNewForms.Contains(GetUnitTypeId(lu)))
      {
        return;
      }

      var owner = GetOwningPlayer(lu);
      if (!IsBot(owner))
      {
        return; // humans keep WL's mechanic
      }

      PermaDead.Add(GetUnitTypeId(lu));
      Log("P" + I2S(GetPlayerId(owner)) + " " + FacName(owner) + ": " + lh.Name
          + " returned as a banned new form -> removed (stays dead for bots)");
      RemoveUnit(lu);
    }

    private static void ScanBannedForms()
    {
      for (var i = 0; i < _hookedLegends.Count; i++)
      {
        RemoveBannedForm(_hookedLegends[i]);
      }
    }

    private static void OnProductionTick()
    {
      EnsureLegendHooks();
      if (BotBreak || !ProdOn)
      {
        return;
      }

      if (Bots.Count == 0)
      {
        return;
      }

      if (_prodCursor >= Bots.Count)
      {
        _prodCursor = 0;
      }

      ProdOps = 0;
      var start = _prodCursor;
      _prodCursor++;
      for (var b = start; b == start; b++) // v12: exactly one bot per production tick
      {
        var p = Bots[b];
        var pid0 = GetPlayerId(p);
        SetPlayerState(p, PLAYER_STATE_RESOURCE_GOLD,
          GetPlayerState(p, PLAYER_STATE_RESOURCE_GOLD) + GoldBonusFor(p)); // v13: subsidy; v31: per faction
        var techLvl = R2I(_clock / 600f); // v21: 0 before 10 min, then +1 every 10 min
        if (techLvl > 5)
        {
          techLvl = 5;
        }

        for (var ui = 0; ui < AiConfig.CombatUpgrades.Count; ui++)
        {
          SetPlayerTechResearched(p, AiConfig.CombatUpgrades[ui], techLvl);
        }
        var faction = p.GetPlayerData().Faction;
        if (faction == null)
        {
          continue;
        }

        // v33: UNIT/ABILITY pool (casters + abilities, AiUpgrades.ByFaction + Extra): steps unlocked =
        // 1 + one per 5 min (old timer) + one per 2 CPs captured. Steps = every upgrade at level 1 first, then
        // level 2, then level 3, so casters reach their higher tiers too.
        var caps = _cpCaptures.ContainsKey(pid0) ? _cpCaptures[pid0] : 0;
        var steps = TechStepsFor(faction.Name);
        if (steps.Ids.Count > 0)
        {
          var un = 1 + R2I(_clock / 300f) + caps / 2;
          if (un > steps.Ids.Count)
          {
            un = steps.Ids.Count;
          }

          for (var uk = 0; uk < un; uk++)
          {
            SetPlayerTechResearched(p, steps.Ids[uk], steps.Levels[uk]); // later steps raise the same id further
          }
        }

        // v33: BUILDING/MISC pool -- one credit per 2 CPs captured (no timer); spent after the structure scan.
        var bCredits = caps / 2 - (_bSpent.ContainsKey(pid0) ? _bSpent[pid0] : 0);
        unit upHall = null;
        unit upOther = null;

        var barracks = new List<int>();
        AddCategory(faction, UnitCategory.Barracks, barracks);
        var siegeShops = new List<int>();
        AddCategory(faction, UnitCategory.SiegeWorkshop, siegeShops);
        var magicBuildings = new List<int>();
        AddCategory(faction, UnitCategory.Magic, magicBuildings);
        var flyBuildings = new List<int>();
        AddCategory(faction, UnitCategory.FlyingBuilding, flyBuildings);
        var altars = new List<int>();
        AddCategory(faction, UnitCategory.Altar, altars);

        var fighters = new List<int>();
        AddCategory(faction, UnitCategory.Fighter, fighters);
        AddCategory(faction, UnitCategory.Marksman, fighters);
        var siege = new List<int>();
        AddCategory(faction, UnitCategory.Siege, siege);
        AddCategory(faction, UnitCategory.Tank, siege);
        AddCategory(faction, UnitCategory.Destroyer, siege);
        var support = new List<int>();
        AddCategory(faction, UnitCategory.Support, support);
        var flyers = new List<int>();
        AddCategory(faction, UnitCategory.Flyer, flyers);

        var g = CreateGroup();
        GroupEnumUnitsOfPlayer(g, p, null);
        unit u;
        while ((u = FirstOfGroup(g)) != null)
        {
          GroupRemoveUnit(g, u);
          ProdOps++;
          if (!IsUnitType(u, UNIT_TYPE_STRUCTURE))
          {
            continue;
          }

          var tid = GetUnitTypeId(u);
          TryEventResearch(p, u, tid); // v32: event researches first, while the building's queue is free
          TryQuestUpgrade(p, u, tid);  // v34: quest building upgrades (Stormwind's Construction Sites)
          if (bCredits > 0 && AiUpgrades.BuildingUpgrades.ContainsKey(tid) && GetUnitState(u, UNIT_STATE_LIFE) > 0.405f)
          {
            if (IsUnitType(u, UNIT_TYPE_TOWNHALL))
            {
              if (upHall == null)
              {
                upHall = u;
              }
            }
            else if (upOther == null)
            {
              upOther = u;
            }
          }

          if (fighters.Count > 0 && ListHas(barracks, tid))
          {
            TrainSome(u, fighters);
          }
          else if (siege.Count > 0 && ListHas(siegeShops, tid))
          {
            TrainSome(u, siege);
          }
          else if (support.Count > 0 && ListHas(magicBuildings, tid))
          {
            TrainSome(u, support);
          }
          else if (flyers.Count > 0 && ListHas(flyBuildings, tid))
          {
            TrainSome(u, flyers);
          }
          else if (ListHas(altars, tid))
          {
            TrainHeroes(u, faction, p);
          }
        }

        DestroyGroup(g);
        if (bCredits > 0)
        {
          SpendBuildingCredit(p, pid0, upHall, upOther);
        }
      }

      if (ProdOps > MaxProdOps)
      {
        MaxProdOps = ProdOps;
      }
    }

    private static void OnCombatTick()
    {
      _clock += Tick;
      if (BotBreak || Bots.Count == 0)
      {
        return;
      }

      // v8: service exactly ONE bot this tick, then advance the cursor. This is the whole
      // multiplayer fix -- per-second unit enumeration + orders drop ~13x versus v7.
      if (_cursor >= Bots.Count)
      {
        _cursor = 0;
      }

      var p = Bots[_cursor];
      _cursor++; // v26.1: advance BEFORE Process -- if Process ever throws, the cursor still moves on so the
                 // round-robin can't wedge on one bot (the bug that left every bot but P0 doing nothing).
      OpUnits = 0;
      OpOrders = 0;
      DefOps = 0;
      Process(p);
      LastOpUnits = OpUnits;
      LastOpOrders = OpOrders;
      LastDefOps = DefOps;
      if (OpUnits > MaxOpUnits)
      {
        MaxOpUnits = OpUnits;
      }

      if (DefOps > MaxDefOps)
      {
        MaxDefOps = DefOps;
      }
    }

    private static bool ValidTarget(unit u, player p)
    {
      return u != null
             && GetUnitState(u, UNIT_STATE_LIFE) > 0.405f
             && GetOwningPlayer(u) != p
             && !IsUnitAlly(u, p);
    }

    private static void Process(player p)
    {
      var pid = GetPlayerId(p);
      var hasFaction = p.GetPlayerData().Faction != null;
      float bx = 0f, by = 0f;
      var haveBase = false;
      float sumX = 0f, sumY = 0f;
      var army = 0;

      var g = CreateGroup();
      GroupEnumUnitsOfPlayer(g, p, null);
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        OpUnits++;
        if (IsUnitType(u, UNIT_TYPE_STRUCTURE))
        {
          if (!haveBase || IsUnitType(u, UNIT_TYPE_TOWNHALL))
          {
            bx = GetUnitX(u);
            by = GetUnitY(u);
            haveBase = true;
          }
        }
        else if (!IsUnitType(u, UNIT_TYPE_PEON))
        {
          army++;
          sumX += GetUnitX(u);
          sumY += GetUnitY(u);
          if (IsUnitType(u, UNIT_TYPE_HERO))
          {
            HeroGrabItems(u); // v24: only heroes, only on this bot's turn -> effectively free
          }
        }
      }

      DestroyGroup(g);

      float ax, ay;
      if (haveBase)
      {
        ax = bx;
        ay = by;
      }
      else if (army > 0)
      {
        ax = sumX / army;
        ay = sumY / army;
      }
      else
      {
        BotArmy[pid] = 0;
        BotFac[pid] = hasFaction ? 1 : 0;
        BotLm[pid] = -999;
        SetState(p, pid, "idle");
        NoteTarget(p, pid, null, "no-army");
        return;
      }

      // v30: TARGET anchor = where the army actually is (its centroid), not the town hall. Picking "nearest
      // frontier to the base" sent armies that had marched far away back across the map (e.g. Scourge army in
      // Lordaeron re-targeting Borean Tundra in Northrend). Nearest-to-army is also naturally sticky: an army
      // walking toward X keeps X as its nearest frontier. ax/ay (base) is still used for landmass + holding.
      float tx = ax, ty = ay;
      if (army > 0)
      {
        tx = sumX / army;
        ty = sumY / army;
      }

      var diag = GetDiag(pid);
      diag.AnchorX = tx;
      diag.AnchorY = ty;
      diag.AnchorIsBase = army == 0;

      var lm = Geography.Landmass(ax, ay);
      var state = "off";

      // v27: ARBITRATION. If an invasion wave is currently mustering or channelling this faction's army, that
      // wave owns the army -- combat yields, so the two systems never issue conflicting orders to the same
      // units. The wave self-aborts (its %/enemy gate) if home is threatened, handing control straight back.
      var facWave = p.GetPlayerData().Faction;
      if (facWave != null && WaveOwnsFaction(facWave.Name))
      {
        BotArmy[pid] = army;
        BotLm[pid] = lm;
        BotFac[pid] = 1;
        SetState(p, pid, "wave");
        NoteTarget(p, pid, null, "wave-owns-army");
        return;
      }

      if (AtkOn)
      {
        // reachability anchor: prefer an owned capital/CP's landmass -- reliable even when the army sits
        // in a grid cell the downsample never covered (this is the Druids "l-1" fix).
        var homeLm = HomeLandmass(p, lm);
        diag.HomeLm = homeLm;

        var defend = FindDefenseTarget(p);
        if (defend != null)
        {
          // Rule 0: base/CP under attack -> recall, overriding whatever offense was committed.
          Committed[pid] = defend;
          CommitTime[pid] = _clock;
          state = "def";
          NoteTarget(p, pid, defend, "defend(" + I2S(_defWorst) + " enemy units, " + I2S(_defCreeps) + " neutrals ignored)");
          TrackStuck(p, pid, null);
          IssueAttackToPoint(p, pid, GetUnitX(defend), GetUnitY(defend));
        }
        else
        {
          // v26: no MinArmy gate any more -- the staging/clash system IS the gathering, so bots always act.
          var handled = false;
          var fac = p.GetPlayerData().Faction;
          BotTargetName[pid] = "-";

          // v31: a scripted kill target (AiConfig.KillTargets, e.g. Scourge -> Sapphiron) outranks expansion until
          // the unit is dead. Whole army attack-moves it; defence above still pre-empts.
          var kill = ActiveKillTarget(p, pid);
          if (kill != null)
          {
            state = "kill";
            BotTargetName[pid] = "K:" + GetUnitName(kill);
            NoteTarget(p, pid, kill, "kill-target");
            TrackStuck(p, pid, null);
            IssueAttackToPoint(p, pid, GetUnitX(kill), GetUnitY(kill));
            handled = true;
          }

          // authored CP graph -- expand from my frontier to the nearest bordering CP I don't own.
          if (!handled && fac != null)
          {
            var gnode = PickGraphTarget(p, fac, tx, ty, diag);
            if (gnode != null)
            {
              // v28: no staging / clash window any more -- neutral AND enemy frontiers are attacked directly.
              // (Enemy frontiers still can't be returned before 10 min; PickGraphTarget filters them.)
              var enemyCp = IsEnemyCp(gnode, p);
              Committed[pid] = gnode;
              CommitTime[pid] = _clock;
              state = "atk";
              BotTargetName[pid] = (enemyCp ? "E:" : "N:") + GetUnitName(gnode);
              NoteTarget(p, pid, gnode, enemyCp ? "graph-enemy" : "graph-neutral");
              TrackStuck(p, pid, gnode);
              IssueAttackToPoint(p, pid, GetUnitX(gnode), GetUnitY(gnode));
              handled = true;
            }
          }

          if (!handled && fac != null && AiConfig.Doctrine.TryGetValue(fac.Name, out var steps) && steps != null)
          {
            for (var i = 0; i < steps.Count; i++)
            {
              var st = steps[i];
              if (_clock < st.AfterSec || (st.X == 0f && st.Y == 0f))
              {
                continue; // time-gate not open, or unfilled placeholder (fill via -xy)
              }

              if (AreaNeedsAttention(p, st.X, st.Y, ObjectiveRadius))
              {
                Committed[pid] = null;
                state = "atk";
                NoteTarget(p, pid, null, "doctrine-step-" + I2S(i));
                IssueAttackToPoint(p, pid, st.X, st.Y);
                handled = true;
                break;
              }
            }
          }

          if (!handled)
          {
            var tgt = Committed[pid];
            if (!ValidTarget(tgt, p) || _clock - CommitTime[pid] > CommitTimeout)
            {
              tgt = PickObjective(p, pid, tx, ty, homeLm);
              Committed[pid] = tgt;
              CommitTime[pid] = _clock;
            }

            if (tgt != null)
            {
              state = "atk";
              BotTargetName[pid] = "F:" + GetUnitName(tgt);
              NoteTarget(p, pid, tgt, "fallback-nearest");
              TrackStuck(p, pid, tgt);
              IssueAttackToPoint(p, pid, GetUnitX(tgt), GetUnitY(tgt));
            }
            else
            {
              state = "hold";
              NoteTarget(p, pid, null, "hold-nothing-reachable");
              Gather(p, ax, ay); // nothing reachable -> hold at home
            }
          }
        }
      }
      else
      {
        NoteTarget(p, pid, null, "atk-off");
      }

      BotArmy[pid] = army;
      BotLm[pid] = lm;
      BotFac[pid] = hasFaction ? 1 : 0;
      SetState(p, pid, state);
    }

    // v28: state/target bookkeeping for diagnostics. Logs CHANGES only.
    private static void SetState(player p, int pid, string state)
    {
      var old = BotState.ContainsKey(pid) ? BotState[pid] : "-";
      if (old != state)
      {
        Log("P" + I2S(pid) + " " + FacName(p) + ": state " + old + " -> " + state);
      }

      BotState[pid] = state;
    }

    private static void NoteTarget(player p, int pid, unit tgt, string reason)
    {
      var d = GetDiag(pid);
      if (d.Target != tgt)
      {
        var from = d.Target != null ? GetUnitName(d.Target) : "-";
        var to = tgt != null ? GetUnitName(tgt) : "-";
        Log("P" + I2S(pid) + " " + FacName(p) + ": target " + from + " -> " + to + " [" + reason + "]");
        d.Target = tgt;
        d.TargetSince = _clock;
      }

      d.Reason = reason;
    }

    // nearest reachable enemy/unowned capital (Scourge only) -- attack-move clears the yard
    // v9: pick the best reachable objective -- enemy/neutral CONTROL POINTS first (income), then CAPITALS.
    // Attack-moving to the anchor clears the yard en route; the commit persists until it's captured/timeout.
    private static unit PickObjective(player p, int pid, float ax, float ay, int homeLandmass)
    {
      unit bestCp = null;
      var bestCpD = 0f;
      unit bestCap = null;
      var bestCapD = 0f;
      var cand = 0;

      var cps = ControlPointManager.Instance.GetAllControlPoints();
      for (var i = 0; i < cps.Count; i++)
      {
        OpUnits++;
        var cu = cps[i].Unit;
        if (cu == null || GetOwningPlayer(cu) == p || IsUnitAlly(cu, p) || IsUncapturable(cu) || IsBlacklisted(pid, cu))
        {
          continue; // v28: invulnerable / Neutral-Passive CPs can't be taken; v31: nor ones we got stuck on
        }

        if (_clock < 600f && IsEnemyCp(cu, p))
        {
          continue; // v34: the fallback respects the 10-min lock on player-owned CPs too
        }

        var cx = GetUnitX(cu);
        var cy = GetUnitY(cu);
        if (homeLandmass >= 0 && Geography.Landmass(cx, cy) != homeLandmass)
        {
          continue;
        }

        cand++;
        var dx = cx - ax;
        var dy = cy - ay;
        var d = dx * dx + dy * dy;
        if (bestCp == null || d < bestCpD)
        {
          bestCpD = d;
          bestCp = cu;
        }
      }

      var caps = CapitalManager.GetAll();
      for (var i = 0; i < caps.Count; i++)
      {
        OpUnits++;
        var cu = caps[i].Unit;
        if (cu == null || GetUnitState(cu, UNIT_STATE_LIFE) <= 0.405f
            || GetOwningPlayer(cu) == p || IsUnitAlly(cu, p))
        {
          continue;
        }

        var cx = GetUnitX(cu);
        var cy = GetUnitY(cu);
        if (homeLandmass >= 0 && Geography.Landmass(cx, cy) != homeLandmass)
        {
          continue;
        }

        cand++;
        var dx = cx - ax;
        var dy = cy - ay;
        var d = dx * dx + dy * dy;
        if (bestCap == null || d < bestCapD)
        {
          bestCapD = d;
          bestCap = cu;
        }
      }

      Cand[pid] = cand;
      return bestCp != null ? bestCp : bestCap;
    }

    // Rule 0 (v29 rewrite): the most-threatened owned anchor (capital or control point), or null.
    // An anchor counts as threatened only if BOTH hold:
    //   1. it is DAMAGED (HP below DamagedFraction of max). WL gives owned CPs regeneration, so this means
    //      "something hit it recently". Undamaged anchors cost one HP read -- no unit enumeration at all,
    //      which removes the per-CP range scan that dominated the combat-tick load.
    //   2. more than DefendThreshold units of ENEMY PLAYERS stand near it on walkable ground.
    // Never counted: neutrals (creeps, critters, the ~1900 preplaced Neutral-Passive rescue units that sit next
    // to many CPs), allies, structures, and anything on unwalkable terrain (ships offshore, flyers over sea) --
    // an army attack-moved toward those can't reach them and just stands at the coast.
    private static unit FindDefenseTarget(player p)
    {
      unit best = null;
      var worst = 0;
      _defCreeps = 0;

      var caps = CapitalManager.GetAll();
      for (var i = 0; i < caps.Count; i++)
      {
        var cu = caps[i].Unit;
        if (cu == null || GetOwningPlayer(cu) != p || !IsDamaged(cu))
        {
          continue;
        }

        var n = CountEnemiesNear(p, GetUnitX(cu), GetUnitY(cu));
        if (n > DefendThreshold && n > worst)
        {
          worst = n;
          best = cu;
          _defCreeps = _lastCreepCount;
        }
      }

      var mine = p.GetPlayerData().ControlPoints;
      for (var i = 0; i < mine.Count; i++)
      {
        var cu = mine[i].Unit;
        if (cu == null || !IsDamaged(cu))
        {
          continue;
        }

        var n = CountEnemiesNear(p, GetUnitX(cu), GetUnitY(cu));
        if (n > DefendThreshold && n > worst)
        {
          worst = n;
          best = cu;
          _defCreeps = _lastCreepCount;
        }
      }

      _defWorst = worst;
      return best;
    }

    private static bool IsDamaged(unit u)
    {
      var max = GetUnitState(u, UNIT_STATE_MAX_LIFE);
      return max > 0f && GetUnitState(u, UNIT_STATE_LIFE) < max * DamagedFraction;
    }

    // Enemy-PLAYER ground units near (x,y). _lastCreepCount = neutral units that were seen and ignored
    // (diagnostics only: shows how often the old rule would have recalled the army for nothing).
    private static int CountEnemiesNear(player p, float x, float y)
    {
      var n = 0;
      _lastCreepCount = 0;
      var g = CreateGroup();
      GroupEnumUnitsInRange(g, x, y, DefendRadius, null);
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        OpUnits++;
        DefOps++;
        if (GetUnitState(u, UNIT_STATE_LIFE) <= 0.405f || IsUnitType(u, UNIT_TYPE_STRUCTURE))
        {
          continue;
        }

        var owner = GetOwningPlayer(u);
        if (GetPlayerId(owner) >= 24)
        {
          _lastCreepCount++;
          continue; // neutral: never a reason to recall
        }

        if (owner == p || IsUnitAlly(u, p) || !IsPlayerEnemy(owner, p))
        {
          continue;
        }

        // IsTerrainPathable returns TRUE when the point is NOT walkable (WC3's inverted API): deep water / cliffs.
        if (IsTerrainPathable(GetUnitX(u), GetUnitY(u), PATHING_TYPE_WALKABILITY))
        {
          continue;
        }

        n++;
      }

      DestroyGroup(g);
      return n;
    }

    // An owned capital/CP sits on real terrain, so it reads a valid landmass even when the army centroid
    // falls in an uncovered grid cell. Falls back to the army's own landmass.
    private static int HomeLandmass(player p, int fallback)
    {
      var caps = CapitalManager.GetAll();
      for (var i = 0; i < caps.Count; i++)
      {
        var cu = caps[i].Unit;
        if (cu != null && GetOwningPlayer(cu) == p && GetUnitState(cu, UNIT_STATE_LIFE) > 0.405f)
        {
          var l = Geography.Landmass(GetUnitX(cu), GetUnitY(cu));
          if (l >= 0)
          {
            return l;
          }
        }
      }

      var mine = p.GetPlayerData().ControlPoints;
      for (var i = 0; i < mine.Count; i++)
      {
        var cu = mine[i].Unit;
        if (cu != null)
        {
          var l = Geography.Landmass(GetUnitX(cu), GetUnitY(cu));
          if (l >= 0)
          {
            return l;
          }
        }
      }

      return fallback;
    }

    // Re-issue attack-move only when the destination moved a lot or the heartbeat elapsed -- avoids the
    // stop-and-go stutter from constant re-issuing. Works for a unit target (pass its x/y) or a waypoint.
    private static void IssueAttackToPoint(player p, int pid, float x, float y)
    {
      var moved = !LastPtX.ContainsKey(pid)
        || (x - LastPtX[pid]) * (x - LastPtX[pid]) + (y - LastPtY[pid]) * (y - LastPtY[pid]) > 300f * 300f;
      var recent = LastIssueTime.ContainsKey(pid) && _clock - LastIssueTime[pid] <= HeartbeatEvery;
      if (!moved && recent)
      {
        return;
      }

      AttackMove(p, x, y);
      LastPtX[pid] = x;
      LastPtY[pid] = y;
      LastIssueTime[pid] = _clock;
    }

    // v15: walk the authored graph. Return the CP where I own one end of an edge but not the other
    // (my frontier), nearest to my anchor. Owning both ends = captured -> that edge is skipped, so the front
    // advances naturally; unfilled (0) ids and unresolved CPs are ignored. Null -> fall back to the picker.
    // v28: writes frontier counts into d (diagnostics only) and skips uncapturable (invulnerable) frontiers.
    private static unit PickGraphTarget(player p, Faction fac, float ax, float ay, BotDiag d)
    {
      d.Neutral = 0;
      d.Enemy = 0;
      d.Locked = 0;
      d.Invuln = 0;
      d.Closed = 0;
      d.NoCp = 0;
      d.Skipped = 0;
      d.Stuck = 0;
      var setName = _factionEdgeSet.ContainsKey(fac.Name)
        ? _factionEdgeSet[fac.Name]
        : ResolveEdgeSet(fac, p);
      // v34: until the Legion summon every faction walks its own continent's graph. The summon MERGES the graphs
      // (event): from then on everyone walks the whole world, but frontiers on the faction's HOME graph (the one
      // it walked before the merge) always come first -- foreign ones only once home has none left.
      var useName = _graphMerged && setName != null ? "All" : setName;
      d.EdgeSet = setName == null ? "none" : (_graphMerged ? "All(home " + setName + ")" : setName);
      var edges = useName != null ? AiGraph.EdgesByName(useName) : null;
      if (edges == null || edges.Count == 0)
      {
        return null;
      }

      var home = _graphMerged ? NodeSetOf(setName) : null;

      // v31: one best candidate, ranked by (goal hops, weighted distance).
      //  * before 10 min: neutral frontiers only (enemy CPs are locked below).
      //  * after 10 min: whichever is closer to the ARMY, with neutral CPs weighted NeutralBias closer
      //    (keeps the "healthy" v29 movement: no marching past an adjacent enemy CP to reach a far neutral one).
      //  * a faction with an AiConfig.Goals entry (Legion -> Nordrassil) first minimises graph hops to that goal,
      //    so it advances CP-by-CP along the shortest route instead of wandering.
      var pid = GetPlayerId(p);
      var goalHops = GoalHopsFor(fac.Name, useName, p);
      unit best = null;
      var bestForeign = 0;
      var bestHops = 0;
      var bestScore = 0f;
      for (var i = 0; i < edges.Count; i++)
      {
        var a = edges[i].A;
        var b = edges[i].B;
        if (a == 0 || b == 0)
        {
          continue;
        }

        var ca = SafeCp(a);
        var cb = SafeCp(b);
        if (ca == null || cb == null)
        {
          d.NoCp++;
          continue;
        }

        var ownA = GetOwningPlayer(ca.Unit) == p || IsUnitAlly(ca.Unit, p);
        var ownB = GetOwningPlayer(cb.Unit) == p || IsUnitAlly(cb.Unit, p);
        unit frontier = null;
        if (ownA && !ownB)
        {
          frontier = cb.Unit;
        }
        else if (ownB && !ownA)
        {
          frontier = ca.Unit;
        }

        if (frontier == null)
        {
          continue;
        }

        // conditional edges: not yet time-open, or behind a closed gate -> skip until they open.
        // (checked after the frontier test so d.Closed only counts edges that would otherwise be usable)
        if (edges[i].OpenTime > _clock || (edges[i].Gate != "" && !GateOpen(edges[i].Gate)))
        {
          d.Closed++;
          continue;
        }

        // skip nodes: walk past them (they stay a waypoint) but never pick them as a capture target.
        if (AiGraph.SkipNodes.Contains(GetUnitTypeId(frontier)))
        {
          d.Skipped++;
          continue;
        }

        // v28: Neutral-Passive CPs are made invulnerable by WL (quest-locked). Attack-move ignores them and they
        // can't be damaged, so a bot that picks one parks beside it forever. Never a target.
        if (IsUncapturable(frontier))
        {
          d.Invuln++;
          continue;
        }

        var enemy = IsEnemyCp(frontier, p);

        // v26: before 10 min only neutral CPs are valid targets; enemy CPs unlock at the 10-min mark.
        if (_clock < 600f && enemy)
        {
          d.Locked++;
          continue;
        }

        // v31: a frontier this bot gave up on (stuck detection) is skipped until its blacklist expires.
        if (IsBlacklisted(pid, frontier))
        {
          d.Stuck++;
          continue;
        }

        if (enemy)
        {
          d.Enemy++;
        }
        else
        {
          d.Neutral++;
        }

        var dx = GetUnitX(frontier) - ax;
        var dy = GetUnitY(frontier) - ay;
        var score = dx * dx + dy * dy;
        if (!enemy)
        {
          score = score * NeutralBias * NeutralBias;
        }

        var fid = GetUnitTypeId(frontier);
        var hops = 0;
        if (goalHops != null)
        {
          hops = goalHops.ContainsKey(fid) ? goalHops[fid] : 999;
        }

        // home-first (after the merge); a goal route (Legion -> Nordrassil) overrides it
        var foreign = home != null && goalHops == null && !home.Contains(fid) ? 1 : 0;
        if (best == null || foreign < bestForeign
            || (foreign == bestForeign && (hops < bestHops || (hops == bestHops && score < bestScore))))
        {
          best = frontier;
          bestForeign = foreign;
          bestHops = hops;
          bestScore = score;
        }
      }

      return best;
    }

    // v34: graph merge (set at the Legion summon) and node sets per edge-set name (built once).
    public static bool GraphMerged => _graphMerged;
    private static bool _graphMerged;
    private static Dictionary<string, HashSet<int>> _nodeSets; // 0.34.3: created in InitState (see there)

    private static HashSet<int> NodeSetOf(string setName)
    {
      if (setName == null)
      {
        return null;
      }

      if (_nodeSets.ContainsKey(setName))
      {
        return _nodeSets[setName];
      }

      var set = new HashSet<int>();
      var edges = AiGraph.EdgesByName(setName);
      if (edges != null)
      {
        for (var i = 0; i < edges.Count; i++)
        {
          set.Add(edges[i].A);
          set.Add(edges[i].B);
        }
      }

      _nodeSets[setName] = set;
      return set;
    }

    // v31: hop distance from every node to the faction's goal CP over its current edge set (all edges, open or
    // not -- topology only). Cached per (set, goal). Null when the faction has no goal, the goal isn't in this
    // edge set, or the faction (or an ally) already owns it.
    private static Dictionary<string, Dictionary<int, int>> _goalHopCache; // 0.34.3: created in InitState (see there)

    private static Dictionary<int, int> GoalHopsFor(string facName, string setName, player p)
    {
      if (setName == null || !AiConfig.Goals.ContainsKey(facName))
      {
        return null;
      }

      var goal = AiConfig.Goals[facName];
      var gcp = SafeCp(goal);
      if (gcp != null && (GetOwningPlayer(gcp.Unit) == p || IsUnitAlly(gcp.Unit, p)))
      {
        return null; // goal reached -> normal expansion
      }

      var key = setName + "|" + I2S(goal);
      if (_goalHopCache.ContainsKey(key))
      {
        return _goalHopCache[key];
      }

      var edges = AiGraph.EdgesByName(setName);
      var hops = new Dictionary<int, int>();
      var queue = new List<int>();
      var inSet = false;
      for (var i = 0; i < edges.Count && !inSet; i++)
      {
        inSet = edges[i].A == goal || edges[i].B == goal;
      }

      if (inSet)
      {
        hops[goal] = 0;
        queue.Add(goal);
        for (var qi = 0; qi < queue.Count; qi++) // BFS; list-as-queue, never iterates a Dictionary
        {
          var cur = queue[qi];
          var next = hops[cur] + 1;
          for (var i = 0; i < edges.Count; i++)
          {
            var other = edges[i].A == cur ? edges[i].B : edges[i].B == cur ? edges[i].A : 0;
            if (other != 0 && !hops.ContainsKey(other))
            {
              hops[other] = next;
              queue.Add(other);
            }
          }
        }
      }

      var result = inSet ? hops : null;
      _goalHopCache[key] = result;
      return result;
    }

    // ---- v31 stuck detection -------------------------------------------------------------------------------
    // If a bot keeps the same capture target for StuckSec and the target neither changed owner nor lost HP
    // (army can't reach it, can't hurt it, keeps losing), the bot gives up on it for BlacklistSec. Catches
    // every "parked forever" cause at once: unreachable islands, blocked bridges, invulnerable CPs, etc.
    private const float StuckSec = 240f;
    private const float BlacklistSec = 300f;
    private static Dictionary<int, unit> _stuckTarget; // 0.34.3: created in InitState (see there)
    private static Dictionary<int, float> _stuckSince; // 0.34.3: created in InitState (see there)
    private static Dictionary<int, float> _stuckHp; // 0.34.3: created in InitState (see there)
    private static Dictionary<int, player> _stuckOwner; // 0.34.3: created in InitState (see there)
    private static Dictionary<int, List<unit>> _blackUnits; // 0.34.3: created in InitState (see there)
    private static Dictionary<int, List<float>> _blackUntil; // 0.34.3: created in InitState (see there)

    private static void TrackStuck(player p, int pid, unit tgt)
    {
      if (tgt == null)
      {
        _stuckTarget[pid] = null;
        return;
      }

      var hp = GetUnitState(tgt, UNIT_STATE_LIFE);
      var owner = GetOwningPlayer(tgt);
      var same = _stuckTarget.ContainsKey(pid) && _stuckTarget[pid] == tgt && _stuckOwner[pid] == owner;
      if (!same)
      {
        _stuckTarget[pid] = tgt;
        _stuckSince[pid] = _clock;
        _stuckHp[pid] = hp;
        _stuckOwner[pid] = owner;
        return;
      }

      if (hp < _stuckHp[pid] * 0.9f)
      {
        _stuckSince[pid] = _clock; // real progress -> reset the timer
        _stuckHp[pid] = hp;
        return;
      }

      if (_clock - _stuckSince[pid] < StuckSec)
      {
        return;
      }

      if (!_blackUnits.ContainsKey(pid))
      {
        _blackUnits[pid] = new List<unit>();
        _blackUntil[pid] = new List<float>();
      }

      _blackUnits[pid].Add(tgt);
      _blackUntil[pid].Add(_clock + BlacklistSec);
      _stuckTarget[pid] = null;
      Log("P" + I2S(pid) + " " + FacName(p) + ": STUCK on " + GetUnitName(tgt) + " for "
          + I2S(R2I(StuckSec)) + "s, no progress -> skip it for " + I2S(R2I(BlacklistSec)) + "s");
    }

    private static bool IsBlacklisted(int pid, unit cp)
    {
      if (!_blackUnits.ContainsKey(pid))
      {
        return false;
      }

      var us = _blackUnits[pid];
      var ts = _blackUntil[pid];
      for (var i = us.Count - 1; i >= 0; i--)
      {
        if (ts[i] <= _clock)
        {
          us.RemoveAt(i);
          ts.RemoveAt(i);
        }
        else if (us[i] == cp)
        {
          return true;
        }
      }

      return false;
    }

    // v28: a CP nobody can capture right now -- owned by Neutral Passive (WL makes those invulnerable) or
    // invulnerable for any other reason. Public so AiDebug's graph audit uses the exact same test.
    public static bool IsUncapturable(unit cp)
    {
      return GetOwningPlayer(cp) == Player(PLAYER_NEUTRAL_PASSIVE) || BlzIsUnitInvulnerable(cp);
    }

    // A frontier is "enemy" only if a live bot/human owns it (and isn't our ally). Neutral/unowned CPs are not.
    // v30: the neutral players (24..27) ALSO report PLAYER_SLOT_STATE_PLAYING, so before this check every
    // creep-held CP counted as an "enemy" frontier: locked before 10 min (the graph found nothing -> every bot
    // ran on the fallback picker) and pushed through the muster/clash yo-yo after 10 min in older builds.
    // An enemy CP must belong to a real player slot (0..23).
    private static bool IsEnemyCp(unit cp, player p)
    {
      var owner = GetOwningPlayer(cp);
      return owner != p
             && GetPlayerId(owner) < 24
             && GetPlayerSlotState(owner) == PLAYER_SLOT_STATE_PLAYING
             && !IsPlayerAlly(p, owner);
    }

    // v15: fire a node's event once, when a bot of its ByFaction owns that control point.
    private static unit CapitalUnitOfType(int typeId)
    {
      var caps = CapitalManager.GetAll();
      for (var i = 0; i < caps.Count; i++)
      {
        var cu = caps[i].Unit;
        if (cu != null && GetUnitTypeId(cu) == typeId && GetUnitState(cu, UNIT_STATE_LIFE) > 0.405f)
        {
          return cu;
        }
      }

      return null;
    }

    private static void OnEventCheck()
    {
      if (BotBreak)
      {
        return;
      }

      var evs = AiGraph.Events;
      for (var i = 0; i < evs.Count; i++)
      {
        var ev = evs[i];
        if (ev.NodeId == 0 || FiredEvents.ContainsKey(ev.NodeId))
        {
          continue;
        }

        // v34: an event node may also be a CAPITAL (The Sunwell is not a control point -- that is why the
        // Scourge took it and nothing happened).
        var cp = SafeCp(ev.NodeId);
        var nodeUnit = cp != null ? cp.Unit : CapitalUnitOfType(ev.NodeId);
        if (nodeUnit == null)
        {
          continue;
        }

        var owner = GetOwningPlayer(nodeUnit);
        if (GetPlayerId(owner) >= 24)
        {
          continue; // still neutral / unclaimed
        }

        var ownerFac = owner.GetPlayerData().Faction;
        if (ownerFac != null && ownerFac.Name == ev.ByFaction)
        {
          FiredEvents[ev.NodeId] = true;
          FireEvent(ev);
        }
      }
    }

    private static void FireEvent(NodeEvent ev)
    {
      if (ev.Kind == "legion")
      {
        Log("event: " + ev.ByFaction + " holds node " + I2S(ev.NodeId) + " -> Legion summon");
        SummonLegion();
      }
      else if (ev.Kind == "chat")
      {
        for (var i = 0; i < 24; i++)
        {
          DisplayTimedTextToPlayer(Player(i), 0, 0, 10, ev.Text);
        }
      }
    }

    // v27: true while an invasion wave has this faction in its gather (1) or channel (2) phase -- the window
    // during which the wave is actively commanding/teleporting the army, so combat must stand down.
    private static bool WaveOwnsFaction(string facName)
    {
      var waves = AiWaves.All;
      for (var i = 0; i < waves.Count; i++)
      {
        var phase = _wavePhase.ContainsKey(i) ? _wavePhase[i] : 0;
        if ((phase == 1 || phase == 2) && InGroup(waves[i], facName))
        {
          return true;
        }
      }

      return false;
    }

    public static bool GroupHasBot(WaveConfig cfg)
    {
      for (var b = 0; b < Bots.Count; b++)
      {
        var f = Bots[b].GetPlayerData().Faction;
        if (f != null && InGroup(cfg, f.Name))
        {
          return true;
        }
      }

      return false;
    }

    private static bool InGroup(WaveConfig cfg, string facName)
    {
      for (var i = 0; i < cfg.Factions.Count; i++)
      {
        if (cfg.Factions[i] == facName)
        {
          return true;
        }
      }

      return false;
    }

    private static bool OwnsCp(WaveConfig cfg, int cpId)
    {
      if (cpId == 0)
      {
        return false;
      }

      var cp = SafeCp(cpId);
      if (cp == null)
      {
        return false;
      }

      var owner = GetOwningPlayer(cp.Unit);
      if (GetPlayerId(owner) >= 24)
      {
        return false;
      }

      var f = owner.GetPlayerData().Faction;
      return f != null && InGroup(cfg, f.Name);
    }

    private static void Announce(string msg)
    {
      for (var i = 0; i < 24; i++)
      {
        DisplayTimedTextToPlayer(Player(i), 0, 0, 12, msg);
      }
    }

    // Bright flashing minimap ping (visible to everyone) so a big event isn't missed.
    private static void PingAt(float x, float y)
    {
      PingMinimapEx(x, y, 6f, 255, 40, 40, true);
      PingMinimapEx(x, y, 8f, 255, 0, 255, true); // second, offset-colour ping = "double ping"
    }

    // v34.2: run one wave-tick subtask; an error is logged (once per subtask) instead of aborting the tick.
    private static HashSet<string> _tickErrors; // 0.34.3: created in InitState (see there)

    private static void Safe(string name, System.Action task)
    {
      try
      {
        task();
      }
      catch (System.Exception e)
      {
        TickError(name, e);
      }
    }

    private static void TickError(string name, System.Exception e)
    {
      if (!_tickErrors.Contains(name))
      {
        _tickErrors.Add(name);
        LogPin("TICK ERROR in " + name + ": " + e.Message);
      }
    }

    private static void OnWaveTick()
    {
      if (BotBreak)
      {
        return;
      }

      WaveOps = 0;
      _validCpIds = null;        // v27: rebuild the valid-CP set this tick so SafeCp sees CPs created at runtime

      // v34.2: every subtask runs isolated. Before, one failing subtask aborted the whole tick every 15 s --
      // which is why the gate sweeps (they ran LAST) stopped and the gates came back. Gates now go first, and a
      // failure is written once to the pinned log ("TICK ERROR in ...") instead of killing everything after it.
      Safe("destructible gates", SweepDestructibleGates); // v27.1: destructible gates ONCE (not bridges/walls)
      Safe("gate sweep", SweepBotGates);                  // v26: gate units, at start then every ~5 min
      Safe("tempest reach", WireTempestReach);            // v25
      Safe("one-shots", RunOneShots);                     // v25: runestones
      Safe("kill targets", AssignKillTargets);            // v31/v34.2: Sapphiron, Murmur, area clears
      Safe("cp grants", RunCpGrants);                     // v31
      Safe("starter kit", RunStarterKit);                 // v32: barracks + altar (0:20 and 3:00)
      Safe("ragnaros", RunRagnarosMission);               // v32/v33
      Safe("palace handover", LegionPalaceHandover);      // v32
      Safe("banned forms", ScanBannedForms);              // v32: Cenarius stays dead for bots
      Safe("demon portal", () => FixDemonPortal());       // v31: WL's caster-less portal off (0,0)
      Safe("legion check", CheckLegionSummoned);          // v34.1: summon handling, whatever completed it
      Safe("portal watchdog", PortalWatchdog);            // v34.1
      Safe("kul tiras repair", KulTirasRepair);           // v34.1

      var waves = AiWaves.All;
      for (var w = 0; w < waves.Count; w++)
      {
        try
        {
          RunWave(w, waves[w]);
        }
        catch (System.Exception e)
        {
          TickError("wave " + I2S(w), e);
        }
      }

      LastWaveOps = WaveOps;
      if (WaveOps > MaxWaveOps)
      {
        MaxWaveOps = WaveOps;
      }
    }

    public static bool GateOpen(string gate) => _gateOpen.ContainsKey(gate) && _gateOpen[gate];

    // v26.1: ControlPointManager.GetFromUnitType THROWS for any id that isn't a registered control point, and
    // our authored graphs contain non-CP ids (Legion-nether zones, special leaves). This wrapper returns null
    // for those instead, so the walker/staging/objective code simply skips them. Never call the raw method.
    private static HashSet<int> _validCpIds;
    public static ControlPoint SafeCp(int id)
    {
      if (_validCpIds == null)
      {
        _validCpIds = new HashSet<int>();
        var all = ControlPointManager.Instance.GetAllControlPoints();
        for (var i = 0; i < all.Count; i++)
        {
          _validCpIds.Add(all[i].UnitType);
        }
      }

      return _validCpIds.Contains(id) ? ControlPointManager.Instance.GetFromUnitType(id) : null;
    }

    private static float _lastGateSweep = -1f;
    private static HashSet<int> _gateSet;
    private static bool _destGatesSwept;
    private static HashSet<int> _destGateSet;

    // One-time: kill every destructible GATE doodad so no chokepoint gate blocks anyone. Gates are preplaced,
    // so the single whole-map destructible enum runs once. Only AiGraph.GateDestructibles types (gates) are
    // touched -- bridges and tree/stone walls are excluded, so this cannot delete a bridge deck.
    private static void SweepDestructibleGates()
    {
      if (_destGatesSwept)
      {
        return;
      }

      _destGatesSwept = true;
      if (_destGateSet == null)
      {
        _destGateSet = new HashSet<int>();
        for (var i = 0; i < AiGraph.GateDestructibles.Count; i++)
        {
          _destGateSet.Add(AiGraph.GateDestructibles[i]);
        }
      }

      EnumDestructablesInRect(GetWorldBounds(), null, KillGateDestructable);
    }

    private static void KillGateDestructable()
    {
      var d = GetEnumDestructable();
      if (_destGateSet.Contains(GetDestructableTypeId(d)))
      {
        SetDestructableInvulnerable(d, false);
        KillDestructable(d);
      }
    }

    // Remove every bot-owned wall-gate so a bot can never wall its own army in. Runs once immediately, then
    // ~every 5 min (gates can be built later). Strict type match against AiGraph.GateTypes -> only real gates.
    private static void SweepBotGates()
    {
      if (_lastGateSweep >= 0f && _clock - _lastGateSweep < 300f)
      {
        return;
      }

      _lastGateSweep = _clock;
      if (_gateSet == null)
      {
        _gateSet = new HashSet<int>();
        for (var i = 0; i < AiGraph.GateTypes.Count; i++)
        {
          _gateSet.Add(AiGraph.GateTypes[i]);
        }
      }

      // v27.2: whole-map unit enum (not per-player) -- catches gate units no matter who owns them or how they
      // were placed. Strict gate-type match -> only real gates are removed. Use -id on a surviving gate to read
      // its true type id; if it isn't one of these, add it to AiGraph.GateTypes.
      var g = CreateGroup();
      GroupEnumUnitsInRect(g, GetWorldBounds(), null);
      unit u;
      var touched = 0;
      var removed = 0;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        touched++;
        if (_gateSet.Contains(GetUnitTypeId(u)))
        {
          RemoveUnit(u);
          removed++;
        }
      }

      DestroyGroup(g);
      WaveOps += touched;
      LastSweepOps = touched;
      if (removed > 0)
      {
        Log("gate sweep: removed " + I2S(removed) + " gate units (" + I2S(touched) + " units scanned)");
      }
    }

    private static void EnsureNodeSets()
    {
      if (_combinedNodes != null)
      {
        return;
      }

      _combinedNodes = new HashSet<int>();
      for (var i = 0; i < AiGraph.Combined.Count; i++)
      {
        _combinedNodes.Add(AiGraph.Combined[i].A);
        _combinedNodes.Add(AiGraph.Combined[i].B);
      }

      _kalimdorNodes = new HashSet<int>();
      for (var i = 0; i < AiGraph.Kalimdor.Count; i++)
      {
        _kalimdorNodes.Add(AiGraph.Kalimdor[i].A);
        _kalimdorNodes.Add(AiGraph.Kalimdor[i].B);
      }
    }

    // v26: pick a faction's node graph. Scourge/Legion are special-cased (Combined; Legion flips to Kalimdor
    // at the summon). Everyone else resolves by which graph holds the CPs they currently own, then caches it
    // (an invasion's DestEdgeSet later overwrites the cache). Island factions own no graph CP -> null -> the
    // generic picker, until they invade. This is why only continent factions use the CP system.
    private static string ResolveEdgeSet(Faction fac, player p)
    {
      var def = AiGraph.DefaultEdgeSet(fac.Name, _clock);
      if (def != null)
      {
        return def;
      }

      EnsureNodeSets();
      var cc = 0;
      var kc = 0;
      var cps = ControlPointManager.Instance.GetAllControlPoints();
      for (var i = 0; i < cps.Count; i++)
      {
        if (GetOwningPlayer(cps[i].Unit) != p)
        {
          continue;
        }

        var t = cps[i].UnitType;
        if (_combinedNodes.Contains(t))
        {
          cc++;
        }
        else if (_kalimdorNodes.Contains(t))
        {
          kc++;
        }
      }

      if (cc == 0 && kc == 0)
      {
        return null; // no foothold on either graph yet
      }

      var name = kc > cc ? "Kalimdor" : "Combined";
      _factionEdgeSet[fac.Name] = name;
      Log(fac.Name + ": edge set resolved -> " + name + " (owns " + I2S(cc) + " Combined / " + I2S(kc) + " Kalimdor CPs)");
      return name;
    }

    // Tempest Reach has no named CP constant in WL source (CPs are auto-detected by buff), so we resolve it at
    // runtime from its coord (8960,-1152): find the nearest CP and add the Blackwald edge. Niche Gilneas spur --
    // if nothing sits there, it just stays a leaf.
    private static void WireTempestReach()
    {
      if (_tempestWired)
      {
        return;
      }

      _tempestWired = true;
      const int blackwald = 1848653398;
      var bestType = 0;
      var bestD = 1500f * 1500f;
      var cps = ControlPointManager.Instance.GetAllControlPoints();
      for (var i = 0; i < cps.Count; i++)
      {
        var ex = GetUnitX(cps[i].Unit) - 8960f;
        var ey = GetUnitY(cps[i].Unit) - (-1152f);
        var d = ex * ex + ey * ey;
        if (d < bestD)
        {
          bestD = d;
          bestType = cps[i].UnitType;
        }
      }

      if (bestType != 0 && bestType != blackwald)
      {
        AiGraph.Combined.Add(new Edge(blackwald, bestType));
      }
    }

    // WL sets Thandol Span (destructible LT08 ~15695,457) invulnerable and never opens it, so bots can never
    // cross Arathi <-> Dun Modr. Force it open at 20 min, matching that edge's OpenTime.
    private static void OpenThandolIfDue()
    {
      if (_thandolOpened || _clock < 1200f)
      {
        return;
      }

      _thandolOpened = true;
      var best = FindDestructableNear(1280585784, 15695f, 457f, 2000f);
      if (best != null)
      {
        SetDestructableInvulnerable(best, false);
        KillDestructable(best);
        PingMinimap(15695f, 457f, 4f);
      }
    }

    private static destructable _destFound;
    private static int _destWant;
    private static float _destBx, _destBy, _destBest;
    private static destructable FindDestructableNear(int typeId, float x, float y, float r)
    {
      _destFound = null;
      _destWant = typeId;
      _destBx = x;
      _destBy = y;
      _destBest = r * r;
      if (_itemRect == null)
      {
        _itemRect = Rect(0f, 0f, 1f, 1f);
      }

      SetRect(_itemRect, x - r, y - r, x + r, y + r);
      EnumDestructablesInRect(_itemRect, null, PickDestructable);
      return _destFound;
    }

    private static void PickDestructable()
    {
      var d = GetEnumDestructable();
      if (GetDestructableTypeId(d) != _destWant)
      {
        return;
      }

      var ex = GetDestructableX(d) - _destBx;
      var ey = GetDestructableY(d) - _destBy;
      var dist = ex * ex + ey * ey;
      if (dist <= _destBest)
      {
        _destBest = dist;
        _destFound = d;
      }
    }

    // ---- v31 kill targets ------------------------------------------------------------------------------------
    // AiConfig.KillTargets: a faction's bot hunts one specific unit (found by TYPE id near X/Y) from AtSec until
    // it is dead. Assigned on the 15 s wave tick (one range scan, once); Process just reads _killTarget.
    private static Dictionary<int, unit> _killTarget; // 0.34.3: created in InitState (see there)
    private static HashSet<int> _killDone; // 0.34.3: created in InitState (see there)
    private static Dictionary<int, float> _killSince; // 0.34.3: created in InitState (see there)
    private const float KillGiveUpSec = 360f; // army can't finish it (too strong / unreachable) -> give up

    private static void AssignKillTargets()
    {
      AssignClearAreas(); // v34.2
      var list = AiConfig.KillTargets;
      for (var i = 0; i < list.Count; i++)
      {
        if (_killDone.Contains(i) || _clock < list[i].AtSec)
        {
          continue;
        }

        var kt = list[i];
        var bot = PlayerOfFaction(kt.Faction);
        if (bot == null || !IsBot(bot))
        {
          _killDone.Add(i);
          continue;
        }

        _killDone.Add(i);
        var g = CreateGroup();
        GroupEnumUnitsInRange(g, kt.X, kt.Y, kt.Radius, null);
        unit found = null;
        unit u;
        while ((u = FirstOfGroup(g)) != null)
        {
          GroupRemoveUnit(g, u);
          WaveOps++;
          if (found == null && GetUnitTypeId(u) == kt.UnitType && GetUnitState(u, UNIT_STATE_LIFE) > 0.405f)
          {
            found = u;
          }
        }

        DestroyGroup(g);
        if (found == null)
        {
          Log("kill target " + kt.Label + ": not found near " + I2S(R2I(kt.X)) + "," + I2S(R2I(kt.Y)) + " -> skipped");
          continue;
        }

        _killTarget[GetPlayerId(bot)] = found;
        _killSince[GetPlayerId(bot)] = _clock;
        Log(kt.Faction + ": KILL TARGET " + kt.Label + " assigned");
      }
    }

    // ---- v34.2 area clears (AiConfig.ClearAreas) ----
    private static HashSet<int> _clearDone; // 0.34.3: created in InitState (see there)
    private static Dictionary<int, int> _clearActive; // 0.34.3: created in InitState (see there)   // pid -> ClearAreas index (-1 = none)
    private static Dictionary<int, float> _clearSince; // 0.34.3: created in InitState (see there)

    private static void AssignClearAreas()
    {
      var list = AiConfig.ClearAreas;
      for (var i = 0; i < list.Count; i++)
      {
        if (_clearDone.Contains(i))
        {
          continue;
        }

        var cp = SafeCp(list[i].TriggerCpId);
        if (cp == null || cp.Unit == null)
        {
          continue;
        }

        var o = GetOwningPlayer(cp.Unit);
        var pid = GetPlayerId(o);
        if (!IsBot(o) || (_clearActive.ContainsKey(pid) && _clearActive[pid] >= 0)
            || (_killTarget.ContainsKey(pid) && _killTarget[pid] != null))
        {
          continue; // owner is no bot, or already busy with a kill target / another clear
        }

        _clearDone.Add(i);
        _clearActive[pid] = i;
        _clearSince[pid] = _clock;
        LogPin(FacName(o) + ": AREA CLEAR " + list[i].Label + " started (holds trigger CP)");
      }
    }

    // nearest living Neutral Hostile unit in the bot's active clear area, or null (area done / given up)
    private static unit ActiveClearTarget(player p, int pid)
    {
      if (!_clearActive.ContainsKey(pid) || _clearActive[pid] < 0)
      {
        return null;
      }

      var ca = AiConfig.ClearAreas[_clearActive[pid]];
      if (_clock - _clearSince[pid] > 480f)
      {
        _clearActive[pid] = -1;
        LogPin(FacName(p) + ": area clear " + ca.Label + " given up after 8 min");
        return null;
      }

      unit best = null;
      var bestD = 0f;
      var g = CreateGroup();
      GroupEnumUnitsInRange(g, ca.X, ca.Y, ca.Radius, null);
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        OpUnits++;
        if (GetOwningPlayer(u) != Player(PLAYER_NEUTRAL_AGGRESSIVE) || GetUnitState(u, UNIT_STATE_LIFE) <= 0.405f
            || BlzIsUnitInvulnerable(u) || ControlPointManager.Instance.UnitIsControlPoint(u))
        {
          continue;
        }

        var dx = GetUnitX(u) - ca.X;
        var dy = GetUnitY(u) - ca.Y;
        var dd = dx * dx + dy * dy;
        if (best == null || dd < bestD)
        {
          best = u;
          bestD = dd;
        }
      }

      DestroyGroup(g);
      if (best == null)
      {
        _clearActive[pid] = -1;
        LogPin(FacName(p) + ": area clear " + ca.Label + " DONE");
      }

      return best;
    }

    private static unit ActiveKillTarget(player p, int pid)
    {
      if (!_killTarget.ContainsKey(pid) || _killTarget[pid] == null)
      {
        return ActiveClearTarget(p, pid); // v34.2: area clears run when no single kill target is active
      }

      var u = _killTarget[pid];
      if (GetUnitTypeId(u) == 0 || GetUnitState(u, UNIT_STATE_LIFE) <= 0.405f)
      {
        _killTarget[pid] = null;
        Log("P" + I2S(pid) + " " + FacName(p) + ": kill target dead -> back to expansion");
        return null;
      }

      if (_killSince.ContainsKey(pid) && _clock - _killSince[pid] > KillGiveUpSec)
      {
        _killTarget[pid] = null;
        Log("P" + I2S(pid) + " " + FacName(p) + ": kill target " + GetUnitName(u) + " still alive after "
            + I2S(R2I(KillGiveUpSec)) + "s -> giving up");
        return null;
      }

      return u;
    }

    // One-shots: when a non-excluded bot owns the trigger CP (or any CP within radius), send a small force to
    // the point once, then mark it consumed. Evaluated on the 15s wave tick -> no scanning loop.
    private static void RunOneShots()
    {
      for (var i = 0; i < AiGraph.OneShots.Count; i++)
      {
        if (_oneShotDone.Contains(i))
        {
          continue;
        }

        var os = AiGraph.OneShots[i];
        var claimant = OneShotClaimant(os);
        if (claimant == null)
        {
          continue;
        }

        _oneShotDone.Add(i);
        SendForceTo(claimant, os.X, os.Y, 6);
        PingMinimap(os.X, os.Y, 4f);
      }
    }

    // The first bot that owns the trigger CP (or a CP within radius) and isn't excluded. Null if none yet.
    private static player OneShotClaimant(AiGraph.OneShot os)
    {
      for (var b = 0; b < Bots.Count; b++)
      {
        var p = Bots[b];
        var fac = p.GetPlayerData().Faction;
        if (fac == null || os.Exclude.Contains(fac.Name))
        {
          continue;
        }

        if (os.NearCpId != 0)
        {
          var cp = SafeCp(os.NearCpId);
          if (cp != null && GetOwningPlayer(cp.Unit) == p)
          {
            return p;
          }
        }
        else
        {
          var cps = ControlPointManager.Instance.GetAllControlPoints();
          for (var c = 0; c < cps.Count; c++)
          {
            var cu = cps[c].Unit;
            if (GetOwningPlayer(cu) != p)
            {
              continue;
            }

            var ex = GetUnitX(cu) - os.X;
            var ey = GetUnitY(cu) - os.Y;
            if (ex * ex + ey * ey <= os.NearRadius * os.NearRadius)
            {
              return p;
            }
          }
        }
      }

      return null;
    }

    // Order up to n of a bot's nearest army units to attack-move a point (clears whatever is there).
    private static void SendForceTo(player p, float x, float y, int n)
    {
      var g = CreateGroup();
      GroupEnumUnitsOfPlayer(g, p, null);
      var sent = 0;
      unit u;
      while ((u = FirstOfGroup(g)) != null && sent < n)
      {
        GroupRemoveUnit(g, u);
        if (IsUnitType(u, UNIT_TYPE_STRUCTURE) || IsUnitType(u, UNIT_TYPE_PEON)
            || GetUnitState(u, UNIT_STATE_LIFE) <= 0.405f)
        {
          continue;
        }

        IssuePointOrder(u, "attack", x, y);
        sent++;
      }

      // drain the rest without ordering
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
      }

      DestroyGroup(g);
    }

    // v27.1: a position is "home" if it's on the home landmass -- or, when the home landmass is -1 (islands the
    // downsample can't resolve, e.g. Kul Tiras), within HomeWaveRadius of the home CP. Without this, island
    // waves count the whole map's -1 coastline and their %/enemy gate can never pass -> they never teleport.
    private static bool IsHomeArea(int homeLm, float hx, float hy, float x, float y)
    {
      if (homeLm != -1)
      {
        return Geography.Landmass(x, y) == homeLm;
      }

      var dx = x - hx;
      var dy = y - hy;
      return dx * dx + dy * dy <= AiWorld.HomeWaveRadius * AiWorld.HomeWaveRadius;
    }

    private static bool WaveActive(WaveConfig cfg, int homeLm, float hx, float hy)
    {
      // v30: cheapest test first -- an unowned trigger CP rules the wave out before any unit enumeration.
      if (cfg.TriggerCpId != 0 && !OwnsCp(cfg, cfg.TriggerCpId))
      {
        return false;
      }

      HomeCpCounts(cfg, homeLm, hx, hy, out var owned, out var total);
      var thresholdMet = total > 0 && owned >= R2I(total * cfg.Threshold);

      // v24: launch when the group either holds the %-gate OR owns every monument on its home area,
      // AND has cleared home down to a handful of enemies. Optional TriggerCpId is still an extra AND.
      return (thresholdMet || AllMonumentsOwned(cfg, homeLm, hx, hy))
             && EnemiesOnLandmass(cfg, homeLm, hx, hy, AiWorld.EnemyLeaveThreshold) < AiWorld.EnemyLeaveThreshold;
    }

    // v28: home-area CP counts for a wave group (shared by WaveActive and the diagnostics snapshot).
    private static void HomeCpCounts(WaveConfig cfg, int homeLm, float hx, float hy, out int owned, out int total)
    {
      total = 0;
      owned = 0;
      var cps = ControlPointManager.Instance.GetAllControlPoints();
      for (var i = 0; i < cps.Count; i++)
      {
        var cu = cps[i].Unit;
        if (!IsHomeArea(homeLm, hx, hy, GetUnitX(cu), GetUnitY(cu)))
        {
          continue;
        }

        total++;
        var owner = GetOwningPlayer(cu);
        if (GetPlayerId(owner) < 24)
        {
          var of = owner.GetPlayerData().Faction;
          if (of != null && InGroup(cfg, of.Name))
          {
            owned++;
          }
        }
      }
    }

    // v28: diagnostics snapshot of one wave (on demand from AiDebug; never called by logic).
    public static WaveDiag GetWaveDiag(int idx)
    {
      var w = new WaveDiag();
      if (idx < 0 || idx >= AiWaves.All.Count)
      {
        return w;
      }

      var cfg = AiWaves.All[idx];
      w.HasBot = GroupHasBot(cfg);
      w.Phase = _wavePhase.ContainsKey(idx) ? _wavePhase[idx] : 0;
      w.PhaseAge = _wavePhaseAt.ContainsKey(idx) ? _clock - _wavePhaseAt[idx] : 0f;
      var lastDone = _waveLastDone.ContainsKey(idx) ? _waveLastDone[idx] : -99999f;
      w.CooldownLeft = cfg.IntervalSec - (_clock - lastDone);
      if (w.CooldownLeft < 0f)
      {
        w.CooldownLeft = 0f;
      }

      var homeCp = SafeCp(cfg.HomeCpId);
      if (homeCp == null)
      {
        return w;
      }

      w.HomeOk = true;
      var hx = GetUnitX(homeCp.Unit);
      var hy = GetUnitY(homeCp.Unit);
      w.HomeLm = Geography.Landmass(hx, hy);
      HomeCpCounts(cfg, w.HomeLm, hx, hy, out var owned, out var total);
      w.Owned = owned;
      w.Total = total;
      w.Need = R2I(total * cfg.Threshold);
      w.Monuments = AllMonumentsOwned(cfg, w.HomeLm, hx, hy);
      w.Enemies = EnemiesOnLandmass(cfg, w.HomeLm, hx, hy, 500);
      w.TriggerOk = cfg.TriggerCpId == 0 || OwnsCp(cfg, cfg.TriggerCpId);
      w.Active = WaveActive(cfg, w.HomeLm, hx, hy);
      return w;
    }

    // All monuments in the group's home area must be owned by the group. Monuments elsewhere are ignored,
    // so the same two lists serve every wave regardless of which theatre it launches from.
    private static bool AllMonumentsOwned(WaveConfig cfg, int homeLm, float hx, float hy)
    {
      var any = false;
      if (!MonumentsHeld(cfg, homeLm, hx, hy, AiWorld.CombinedMonuments, ref any))
      {
        return false;
      }

      if (!MonumentsHeld(cfg, homeLm, hx, hy, AiWorld.KalimdorMonuments, ref any))
      {
        return false;
      }

      return any; // true only if at least one monument was actually in this home area and all such were held
    }

    private static bool MonumentsHeld(WaveConfig cfg, int homeLm, float hx, float hy, List<int> monuments, ref bool any)
    {
      for (var i = 0; i < monuments.Count; i++)
      {
        var cp = SafeCp(monuments[i]);
        if (cp == null || !IsHomeArea(homeLm, hx, hy, GetUnitX(cp.Unit), GetUnitY(cp.Unit)))
        {
          continue;
        }

        any = true;
        var owner = cp.Unit != null ? GetOwningPlayer(cp.Unit) : null;
        var of = owner != null && GetPlayerId(owner) < 24 ? owner.GetPlayerData().Faction : null;
        if (of == null || !InGroup(cfg, of.Name))
        {
          return false;
        }
      }

      return true;
    }

    // Count enemies (non-group, living, non-structure) on the home landmass, stopping at cap. Runs on the
    // 15s wave tick for a few active waves only -> cheap. "Cleared home enough to leave" gate.
    private static int EnemiesOnLandmass(WaveConfig cfg, int homeLm, float hx, float hy, int cap)
    {
      var count = 0;
      for (var i = 0; i < 24; i++)
      {
        var p = Player(i);
        var f = p.GetPlayerData().Faction;
        if (f != null && InGroup(cfg, f.Name))
        {
          continue; // own group isn't "enemy"
        }

        var g = CreateGroup();
        GroupEnumUnitsOfPlayer(g, p, null);
        unit u;
        while ((u = FirstOfGroup(g)) != null)
        {
          GroupRemoveUnit(g, u);
          WaveOps++;
          if (IsUnitType(u, UNIT_TYPE_STRUCTURE) || GetUnitState(u, UNIT_STATE_LIFE) <= 0.405f)
          {
            continue;
          }

          if (IsHomeArea(homeLm, hx, hy, GetUnitX(u), GetUnitY(u)))
          {
            count++;
          }
        }

        DestroyGroup(g);
        if (count >= cap)
        {
          return count;
        }
      }

      return count;
    }

    // idle -> gather (units walk to staging) -> channel (portals open) -> jump to ONE random destination.
    private static void RunWave(int idx, WaveConfig cfg)
    {
      var homeCp = SafeCp(cfg.HomeCpId);
      if (homeCp == null)
      {
        return;
      }

      // v30: waves are a BOT mechanic. With no bot in the group (e.g. Kul Tiras played by a human) the wave
      // never evaluates -- no fleet/portal messages, and no whole-map enemy count every 15 s.
      if (!GroupHasBot(cfg))
      {
        if (_wavePhase.ContainsKey(idx) && _wavePhase[idx] != 0)
        {
          Log("wave " + I2S(idx) + ": no bot in group -> aborted");
          AbortWave(idx);
        }

        _waveWasActive[idx] = false;
        return;
      }

      var homeLm = Geography.Landmass(GetUnitX(homeCp.Unit), GetUnitY(homeCp.Unit));
      var active = WaveActive(cfg, homeLm, GetUnitX(homeCp.Unit), GetUnitY(homeCp.Unit));
      var was = _waveWasActive.ContainsKey(idx) && _waveWasActive[idx];
      if (!active && was)
      {
        Announce(cfg.StopMsg);
        Log("wave " + I2S(idx) + ": gate lost -> stopped");
        AbortWave(idx);
      }

      if (active && !was)
      {
        Log("wave " + I2S(idx) + ": gate now OPEN (active)");
      }

      _waveWasActive[idx] = active;

      var gx = cfg.GatherX == 0f && cfg.GatherY == 0f ? GetUnitX(homeCp.Unit) : cfg.GatherX;
      var gy = cfg.GatherX == 0f && cfg.GatherY == 0f ? GetUnitY(homeCp.Unit) : cfg.GatherY;
      var phase = _wavePhase.ContainsKey(idx) ? _wavePhase[idx] : 0;

      if (phase == 0)
      {
        if (!active)
        {
          return;
        }

        var lastDone = _waveLastDone.ContainsKey(idx) ? _waveLastDone[idx] : -99999f;
        if (_clock - lastDone < cfg.IntervalSec || cfg.Dests.Count == 0)
        {
          return;
        }

        var d = cfg.Dests[GetRandomInt(0, cfg.Dests.Count - 1)];
        var dx = d.X;
        var dy = d.Y;
        if (d.CpId != 0)
        {
          var dc = SafeCp(d.CpId);
          if (dc == null)
          {
            return;
          }

          dx = GetUnitX(dc.Unit);
          dy = GetUnitY(dc.Unit);
        }

        if (dx == 0f && dy == 0f)
        {
          return; // destination not set yet
        }

        _waveDestX[idx] = dx;
        _waveDestY[idx] = dy;
        Announce(cfg.PrepMsg);
        Log("wave " + I2S(idx) + ": gather -> dest " + I2S(R2I(dx)) + "," + I2S(R2I(dy)));
        PingAt(gx, gy);
        _wavePhase[idx] = 1;
        _wavePhaseAt[idx] = _clock;
        return;
      }

      if (!active)
      {
        AbortWave(idx);
        return;
      }

      if (phase == 1)
      {
        GatherToPoint(cfg, homeLm, gx, gy);
        if (_clock - _wavePhaseAt[idx] >= cfg.GatherSec)
        {
          _waveSrcFx[idx] = AddSpecialEffect(cfg.PortalModel, gx, gy);
          _waveDstFx[idx] = AddSpecialEffect(cfg.PortalModel, _waveDestX[idx], _waveDestY[idx]);
          PingAt(gx, gy);
          Log("wave " + I2S(idx) + ": channel");
          _wavePhase[idx] = 2;
          _wavePhaseAt[idx] = _clock;
        }

        return;
      }

      if (_clock - _wavePhaseAt[idx] >= cfg.ChannelSec)
      {
        var moved = TeleportGathered(cfg, homeLm, gx, gy, _waveDestX[idx], _waveDestY[idx]);
        Log("wave " + I2S(idx) + ": JUMP, " + I2S(moved) + " units teleported");
        Announce(cfg.ArriveMsg);
        PingAt(_waveDestX[idx], _waveDestY[idx]);
        AbortWave(idx);
        _waveLastDone[idx] = _clock;
      }
    }

    private static void GatherToPoint(WaveConfig cfg, int homeLm, float gx, float gy)
    {
      for (var b = 0; b < Bots.Count; b++)
      {
        var p = Bots[b];
        var fac = p.GetPlayerData().Faction;
        if (fac == null || !InGroup(cfg, fac.Name))
        {
          continue;
        }

        var g = CreateGroup();
        GroupEnumUnitsOfPlayer(g, p, null);
        unit u;
        while ((u = FirstOfGroup(g)) != null)
        {
          GroupRemoveUnit(g, u);
          WaveOps++;
          if (IsUnitType(u, UNIT_TYPE_STRUCTURE) || IsUnitType(u, UNIT_TYPE_PEON))
          {
            continue;
          }

          var ux = GetUnitX(u);
          var uy = GetUnitY(u);
          // In the home area AND not already sitting at a landing zone. The second test stops an away-team from
          // being dragged back home when home + landing share an ambiguous landmass id (islands).
          if (IsHomeArea(homeLm, gx, gy, ux, uy) && !NearAnyDest(cfg, ux, uy, 6000f) && u != _ragHero)
          {
            IssuePointOrder(u, "attack", gx, gy);
          }
        }

        DestroyGroup(g);
      }
    }

    private static int TeleportGathered(WaveConfig cfg, int homeLm, float gx, float gy, float dx, float dy)
    {
      var moved = 0;
      for (var b = 0; b < Bots.Count; b++)
      {
        var p = Bots[b];
        var fac = p.GetPlayerData().Faction;
        if (fac == null || !InGroup(cfg, fac.Name))
        {
          continue;
        }

        var g = CreateGroup();
        GroupEnumUnitsOfPlayer(g, p, null);
        unit u;
        while ((u = FirstOfGroup(g)) != null)
        {
          GroupRemoveUnit(g, u);
          WaveOps++;
          var ux = GetUnitX(u);
          var uy = GetUnitY(u);
          if (IsUnitType(u, UNIT_TYPE_STRUCTURE) || !IsHomeArea(homeLm, gx, gy, ux, uy)
              || NearAnyDest(cfg, ux, uy, 6000f) || u == _ragHero)
          {
            continue; // structures, off-continent units, units at a landing, the Ragnaros hero: left alone
          }

          // v24: teleport ALL home-landmass units (gather is flavor now) -> nobody is stranded by a radius.
          SetUnitX(u, dx + GetRandomReal(-512f, 512f));
          SetUnitY(u, dy + GetRandomReal(-512f, 512f));
          moved++;
        }

        DestroyGroup(g);

        // v24: the arriving group adopts the destination continent's node system (no fallback to non-node).
        if (cfg.DestEdgeSet != "")
        {
          _factionEdgeSet[fac.Name] = cfg.DestEdgeSet;
          Log(fac.Name + ": edge set -> " + cfg.DestEdgeSet + " (wave arrival)");
        }
      }

      return moved;
    }

    // True if (x,y) is within r of any of the wave's landing zones. Used to exclude an already-departed
    // away-team from the gather/teleport passes so it isn't pulled home again (islands share landmass ids).
    private static bool NearAnyDest(WaveConfig cfg, float x, float y, float r)
    {
      var r2 = r * r;
      for (var i = 0; i < cfg.Dests.Count; i++)
      {
        var d = cfg.Dests[i];
        var dx = d.X;
        var dy = d.Y;
        if (d.CpId != 0)
        {
          var dc = SafeCp(d.CpId);
          if (dc == null)
          {
            continue;
          }

          dx = GetUnitX(dc.Unit);
          dy = GetUnitY(dc.Unit);
        }

        if (dx == 0f && dy == 0f)
        {
          continue;
        }

        var ex = x - dx;
        var ey = y - dy;
        if (ex * ex + ey * ey <= r2)
        {
          return true;
        }
      }

      return false;
    }

    private static void AbortWave(int idx)
    {
      if (_waveSrcFx.ContainsKey(idx) && _waveSrcFx[idx] != null)
      {
        DestroyEffect(_waveSrcFx[idx]);
        _waveSrcFx[idx] = null;
      }

      if (_waveDstFx.ContainsKey(idx) && _waveDstFx[idx] != null)
      {
        DestroyEffect(_waveDstFx[idx]);
        _waveDstFx[idx] = null;
      }

      _wavePhase[idx] = 0;
    }

    // Debug: instant jump of the group's home-landmass units to a random destination (skips prep/channel).
    public static void ForceWave(int idx)
    {
      if (idx < 0 || idx >= AiWaves.All.Count)
      {
        return;
      }

      var cfg = AiWaves.All[idx];
      if (cfg.Dests.Count == 0)
      {
        return;
      }

      var homeCp = SafeCp(cfg.HomeCpId);
      if (homeCp == null)
      {
        return;
      }

      var homeLm = Geography.Landmass(GetUnitX(homeCp.Unit), GetUnitY(homeCp.Unit));
      var d = cfg.Dests[GetRandomInt(0, cfg.Dests.Count - 1)];
      var dx = d.X;
      var dy = d.Y;
      if (d.CpId != 0)
      {
        var dc = SafeCp(d.CpId);
        if (dc == null)
        {
          return;
        }

        dx = GetUnitX(dc.Unit);
        dy = GetUnitY(dc.Unit);
      }

      var sfx = AddSpecialEffect(cfg.PortalModel, dx, dy);
      DestroyEffect(sfx);
      PingAt(dx, dy);
      for (var b = 0; b < Bots.Count; b++)
      {
        var p = Bots[b];
        var fac = p.GetPlayerData().Faction;
        if (fac == null || !InGroup(cfg, fac.Name))
        {
          continue;
        }

        var g = CreateGroup();
        GroupEnumUnitsOfPlayer(g, p, null);
        unit u;
        while ((u = FirstOfGroup(g)) != null)
        {
          GroupRemoveUnit(g, u);
          if (!IsUnitType(u, UNIT_TYPE_STRUCTURE) && Geography.Landmass(GetUnitX(u), GetUnitY(u)) == homeLm)
          {
            SetUnitX(u, dx + GetRandomReal(-512f, 512f));
            SetUnitY(u, dy + GetRandomReal(-512f, 512f));
          }
        }

        DestroyGroup(g);
      }

      Announce(cfg.ArriveMsg);
    }

    public static void SummonLegion()
    {
      foreach (var f in FactionManager.GetAllFactions())
      {
        if (f.Name != "Legion")
        {
          continue;
        }

        foreach (var q in f.GetAllQuests())
        {
          if (q is QuestSummonLegion)
          {
            if (_legionSummonStarted)
            {
              return; // v34: once only (Sunwell + corrupted Sunwell events, or a repeated -legion)
            }

            _legionSummonStarted = true;
            var t = CreateTimer();
            TimerStart(t, 1.0f, false, OnLegionSummoned); // let the quest's own CreatePortals run first
            try
            {
              q.Progress = QuestProgress.Complete;
            }
            catch (System.Exception e)
            {
              LogPin("Legion summon quest: WL error during completion -- " + e.Message);
            }

            return;
          }
        }
      }
    }

    // v24: forget the quest's invisible exterior portal (it spawns at the caster's spot -> (0,0) for a bot, and
    // its model may not render). Instead spawn a clean, visible waygate pair, pull the Legion to the Kalimdor
    // exit so the Hyjal march begins at once, switch its node system to Kalimdor, and trigger the betrayal.
    // v34.1: the summon quest can complete by ANY path (bot ritual, WL, -legion, the Sunwell event). The wave
    // tick polls it, so the AI's summon handling (portals, merge, gold, betrayal) always runs -- exactly once.
    private static bool _legionHandled;
    private static QuestData _legionQuest;

    private static void CheckLegionSummoned()
    {
      if (_legionHandled)
      {
        return;
      }

      if (_legionQuest == null)
      {
        var f = PlayerOfFaction("Legion");
        var fac = f != null ? f.GetPlayerData().Faction : null;
        if (fac == null)
        {
          return;
        }

        foreach (var q in fac.GetAllQuests())
        {
          if (q is QuestSummonLegion)
          {
            _legionQuest = q;
          }
        }

        if (_legionQuest == null)
        {
          return;
        }
      }

      if (_legionQuest.Progress == QuestProgress.Complete)
      {
        LogPin("Legion summon quest is complete -> running the summon handling");
        OnLegionSummoned();
      }
    }

    private static void OnLegionSummoned()
    {
      if (_legionHandled)
      {
        return;
      }

      _legionHandled = true;
      try
      {
        OnLegionSummonedSteps();
      }
      catch (System.Exception e)
      {
        LogPin("Legion summon handling ERROR: " + e.Message);
      }

      // the graph switches must happen even if a step above failed
      _factionEdgeSet["Legion"] = "Kalimdor";
      _gateOpen["legion"] = true;
      _gateOpen["prelegion"] = false;
      _graphMerged = true;
      LogPin("LEGION SUMMONED: Legion home -> Kalimdor, Argus hub open, Northrend link closed, graphs MERGED");
    }

    private static void OnLegionSummonedSteps()
    {
      // 1) v31: WL's own portal pair. QuestSummonLegion creates the exterior Demon Portal (n037) at the casting
      //    hero's position -- (0,0) when the quest is completed without a caster (bot, -legion, Sunwell event) --
      //    and points the interior Antoran portal (n03C) at it. Recreate it at the real exit and re-point the
      //    interior portal. Only if WL's portal can't be found do we fall back to the old workaround pair.
      //    v34: that portal now goes beside Quel'Danas (Argus <-> Sunwell). The Argus <-> Northrend portal is
      //    removed and the Argus <-> Felwood portal opens (AiGraph.Portals, phase "legion").
      Safe("summon: demon portal", () =>
      {
        if (!FixDemonPortal())
        {
          LogPin("Legion: WL Demon Portal not found at (0,0) -- Argus <-> Sunwell link missing");
        }
      });
      Safe("summon: portals", SwitchPortalsToLegionPhase);

      var legion = PlayerOfFaction("Legion");

      // v31: one-time war chest for a bot Legion arriving on Kalimdor.
      if (legion != null && IsBot(legion) && !_legionGoldGiven)
      {
        _legionGoldGiven = true;
        SetPlayerState(legion, PLAYER_STATE_RESOURCE_GOLD,
          GetPlayerState(legion, PLAYER_STATE_RESOURCE_GOLD) + AiWorld.LegionSummonGold);
        Log("Legion: +" + I2S(AiWorld.LegionSummonGold) + " gold (summon bonus)");
      }

      // v32: the north stays with the Scourge -- the Legion stops defending a continent it has left.
      Safe("summon: north handover", () => LegionNorthHandover(legion));

      // 2) collect the Legion's host at the Kalimdor exit -- bots only, so a human Legion keeps control of theirs.
      if (legion != null && IsBot(legion))
      {
        var g = CreateGroup();
        GroupEnumUnitsOfPlayer(g, legion, null);
        unit u;
        while ((u = FirstOfGroup(g)) != null)
        {
          GroupRemoveUnit(g, u);
          if (IsUnitType(u, UNIT_TYPE_STRUCTURE))
          {
            continue;
          }

          SetUnitX(u, AiWorld.LegionExitX + GetRandomReal(-384f, 384f));
          SetUnitY(u, AiWorld.LegionExitY + GetRandomReal(-384f, 384f));
        }

        DestroyGroup(g);
      }

      // 3) graph switches (Legion home -> Kalimdor, hub gates, MERGE) happen in OnLegionSummoned, after this.

      // 4) the betrayal: the Scourge turns on its demon masters. Scattered northern Legion units are left to die.
      var scourge = PlayerOfFaction("Scourge");
      if (scourge != null && legion != null)
      {
        SetPlayerAlliance(scourge, legion, ALLIANCE_PASSIVE, false);
        SetPlayerAlliance(legion, scourge, ALLIANCE_PASSIVE, false);
      }

      PingAt(AiWorld.LegionExitX, AiWorld.LegionExitY);
      Announce("|cffff5555The Burning Legion pours into Kalimdor -- and the Scourge turns on its masters! (see the ping)|r");
    }

    private static bool _legionGoldGiven;
    private static bool _legionSummonStarted;

    // v31: find WL's exterior Demon Portal stranded near (0,0) and rebuild it at AiWorld.LegionExit, re-pointing
    // WL's interior Antoran portal at it. Returns true if a portal was (or already is) in place. Called after a
    // summon and from the 15 s wave tick (one small range scan at the map centre -- cheap, catches any summon path).
    private static bool FixDemonPortal()
    {
      var portalType = FourCC("n037");
      var g = CreateGroup();
      GroupEnumUnitsInRange(g, 0f, 0f, 900f, null);
      unit stranded = null;
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        WaveOps++;
        if (stranded == null && GetUnitTypeId(u) == portalType)
        {
          stranded = u;
        }
      }

      DestroyGroup(g);
      if (stranded == null)
      {
        return _demonPortalFixed;
      }

      var owner = GetOwningPlayer(stranded);
      var x = AiWorld.LegionSunwellPortalX; // v34: beside Quel'Danas (was: beside Felwood)
      var y = AiWorld.LegionSunwellPortalY;
      var fresh = CreateUnit(owner, portalType, x, y, 0f);
      BlzSetUnitName(fresh, "Demon Portal to Argus");

      // interior portal: preplaced n03C in Antoran (WL points it at the exterior portal's old position)
      var interior = FindUnitOfType(FourCC("n03C"), AiWorld.LegionInteriorX, AiWorld.LegionInteriorY, 1500f);
      if (interior != null)
      {
        LandBeside(GetUnitX(interior), GetUnitY(interior));
        WaygateSetDestination(fresh, _spotX, _spotY);
        LandBeside(x, y);
        WaygateSetDestination(interior, _spotX, _spotY);
        WaygateActivate(interior, true);
        BlzSetUnitName(interior, "Demon Portal to Quel'Danas");
      }
      else
      {
        WaygateSetDestination(fresh, WaygateGetDestinationX(stranded), WaygateGetDestinationY(stranded));
      }

      WaygateActivate(fresh, true);
      RemoveUnit(stranded);
      DestroyEffect(AddSpecialEffect(AiWorld.TeleportFx, x, y));
      FloatLabel("Legion Portal", x, y);
      PingAt(x, y);
      _demonPortalFixed = true;
      LogPin("Legion: Demon Portal moved (0,0) -> " + I2S(R2I(x)) + "," + I2S(R2I(y))
          + (interior != null ? ", Antoran portal re-pointed" : ", Antoran portal NOT found"));
      return true;
    }

    private static bool _demonPortalFixed;

    private static unit FindUnitOfType(int typeId, float x, float y, float r)
    {
      var g = CreateGroup();
      GroupEnumUnitsInRange(g, x, y, r, null);
      unit found = null;
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        if (found == null && GetUnitTypeId(u) == typeId)
        {
          found = u;
        }
      }

      DestroyGroup(g);
      return found;
    }

    // ---- v34 physical portals (AiGraph.Portals) ------------------------------------------------------------
    // Real waygates -- usable by every player. "always" + "prelegion" are created 2 s into the game, the
    // "prelegion" ones removed and the "legion" ones created at the summon. Travellers land on open ground
    // BESIDE the other end (landing exactly on a waygate can bounce them straight back).
    // v34.1: owned by Neutral EXTRA (player 26), not Neutral Passive -- WL removes Neutral-Passive units from
    // whole areas at runtime (CleanupNeutralPassiveUnits), which is the likely reason only one ship pair
    // survived. A watchdog on the wave tick rebuilds any pair whose unit disappeared, and every pair is created
    // inside try/catch so one failure can't stop the others.
    private static List<int> _pairLink; // 0.34.3: created in InitState (see there)
    private static List<unit> _pairA; // 0.34.3: created in InitState (see there)
    private static List<unit> _pairB; // 0.34.3: created in InitState (see there)

    public static int PortalPairCount => _pairLink.Count;
    public static unit PortalA(int i) => _pairA[i];
    public static unit PortalB(int i) => _pairB[i];

    private static void SetupPortals()
    {
      _gateOpen["prelegion"] = true;
      CreatePortals("always");
      CreatePortals("prelegion");
    }

    private static void SwitchPortalsToLegionPhase()
    {
      for (var i = _pairLink.Count - 1; i >= 0; i--)
      {
        if (AiGraph.Portals[_pairLink[i]].Phase == "prelegion")
        {
          RemoveUnit(_pairA[i]);
          RemoveUnit(_pairB[i]);
          _pairLink.RemoveAt(i);
          _pairA.RemoveAt(i);
          _pairB.RemoveAt(i);
        }
      }

      CreatePortals("legion");
    }

    private static void CreatePortals(string phase)
    {
      var list = AiGraph.Portals;
      var n = 0;
      for (var i = 0; i < list.Count; i++)
      {
        if (list[i].Phase == phase && CreatePair(i, -1))
        {
          n++;
        }
      }

      LogPin("portals: " + I2S(n) + " pair(s) created (" + phase + ")");
    }

    // build link i; slot >= 0 replaces an existing pair (watchdog), -1 appends
    private static bool CreatePair(int link, int slot)
    {
      var pl = AiGraph.Portals[link];
      try
      {
        var a = CreatePortalEnd(pl.AX, pl.AY, pl.Skin, pl.NameA);
        var b = CreatePortalEnd(pl.BX, pl.BY, pl.Skin, pl.NameB);
        LandBeside(pl.BX, pl.BY);
        WaygateSetDestination(a, _spotX, _spotY);
        LandBeside(pl.AX, pl.AY);
        WaygateSetDestination(b, _spotX, _spotY);
        WaygateActivate(a, true);
        WaygateActivate(b, true);
        if (slot < 0)
        {
          _pairLink.Add(link);
          _pairA.Add(a);
          _pairB.Add(b);
        }
        else
        {
          _pairA[slot] = a;
          _pairB[slot] = b;
        }

        return true;
      }
      catch (System.Exception e)
      {
        LogPin("portal '" + pl.NameA + "' FAILED: " + e.Message);
        return false;
      }
    }

    // wave tick: rebuild any pair whose unit was removed or killed
    private static void PortalWatchdog()
    {
      for (var i = 0; i < _pairLink.Count; i++)
      {
        var aOk = GetUnitTypeId(_pairA[i]) != 0 && GetUnitState(_pairA[i], UNIT_STATE_LIFE) > 0.405f;
        var bOk = GetUnitTypeId(_pairB[i]) != 0 && GetUnitState(_pairB[i], UNIT_STATE_LIFE) > 0.405f;
        if (aOk && bOk)
        {
          continue;
        }

        if (aOk)
        {
          RemoveUnit(_pairA[i]);
        }

        if (bOk)
        {
          RemoveUnit(_pairB[i]);
        }

        if (CreatePair(_pairLink[i], i))
        {
          LogPin("portal '" + AiGraph.Portals[_pairLink[i]].NameA + "' was gone -> rebuilt");
        }
      }
    }

    private static unit CreatePortalEnd(float x, float y, string skin, string name)
    {
      var u = CreateUnit(Player(26), AiWorld.PortalUnitId, x, y, 0f); // 26 = Neutral Extra (see above)
      if (skin != "")
      {
        BlzSetUnitSkin(u, FourCC(skin));
      }

      BlzSetUnitName(u, name);
      SetUnitInvulnerable(u, true);
      return u;
    }

    // open, walkable point ~300 from a portal (falls back to the portal itself) -> _spotX/_spotY
    private static void LandBeside(float x, float y)
    {
      for (var i = 0; i < 8; i++)
      {
        var a = i * 0.785398f;
        _spotX = x + 300f * Cos(a);
        _spotY = y + 300f * Sin(a);
        if (!IsTerrainPathable(_spotX, _spotY, PATHING_TYPE_WALKABILITY))
        {
          return;
        }
      }

      _spotX = x;
      _spotY = y;
    }

    // A linked, active waygate pair. Units that walk onto either end are teleported to the other; a portal
    // edge in the node graph routes bots to one end so they cross. Uses one confirmed-present portal effect.
    private static void SpawnPortalPair(float ax, float ay, float bx, float by)
    {
      var pa = CreateUnit(Player(PLAYER_NEUTRAL_PASSIVE), AiWorld.PortalUnitId, ax, ay, 0f);
      var pb = CreateUnit(Player(PLAYER_NEUTRAL_PASSIVE), AiWorld.PortalUnitId, bx, by, 0f);
      WaygateSetDestination(pa, bx, by);
      WaygateSetDestination(pb, ax, ay);
      WaygateActivate(pa, true);
      WaygateActivate(pb, true);
      DestroyEffect(AddSpecialEffect(AiWorld.TeleportFx, ax, ay));
      DestroyEffect(AddSpecialEffect(AiWorld.TeleportFx, bx, by));
      // Distinct label: this waygate uses the same model as the map's Dark Portals, so name it to tell it apart.
      FloatLabel("Legion Portal", ax, ay);
      FloatLabel("Legion Portal", bx, by);
    }

    private static void FloatLabel(string text, float x, float y)
    {
      var tt = CreateTextTag();
      SetTextTagText(tt, text, 0.023f);
      SetTextTagPos(tt, x, y, 128f);
      SetTextTagColor(tt, 255, 90, 90, 255);
      SetTextTagVisibility(tt, true);
      SetTextTagPermanent(tt, true);
    }

    // v24: heroes auto-claim ground items within 1000 range. Called only for a bot's own heroes during that
    // bot's combat pass (a handful of units, once per ~6.5s) -> no timer, effectively free. One reused rect.
    private static void HeroGrabItems(unit h)
    {
      if (UnitInventorySize(h) <= 0)
      {
        return;
      }

      if (_itemRect == null)
      {
        _itemRect = Rect(0f, 0f, 1f, 1f);
      }

      var x = GetUnitX(h);
      var y = GetUnitY(h);
      SetRect(_itemRect, x - 1000f, y - 1000f, x + 1000f, y + 1000f);
      _grabHero = h;
      EnumItemsInRect(_itemRect, null, GrabItemEnum);
    }

    private static void GrabItemEnum()
    {
      var it = GetEnumItem();
      if (it == null || _grabHero == null || GetWidgetLife(it) <= 0.405f || IsItemOwned(it))
      {
        return;
      }

      UnitAddItem(_grabHero, it); // fails harmlessly when the inventory is full or the item isn't carriable
    }

    private static player PlayerOfFaction(string name)
    {
      for (var i = 0; i < 24; i++)
      {
        var p = Player(i);
        var f = p.GetPlayerData().Faction;
        if (f != null && f.Name == name)
        {
          return p;
        }
      }

      return null;
    }

    private static bool IsBot(player p)
    {
      for (var i = 0; i < Bots.Count; i++)
      {
        if (Bots[i] == p)
        {
          return true;
        }
      }

      return false;
    }

    // A scripted step still needs work if an enemy army is near it, OR a CP/capital near it isn't yet mine.
    private static bool AreaNeedsAttention(player p, float x, float y, float radius)
    {
      var g = CreateGroup();
      GroupEnumUnitsInRange(g, x, y, radius, null);
      unit u;
      var army = false;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        OpUnits++;
        if (!army && GetOwningPlayer(u) != p && !IsUnitAlly(u, p)
            && GetUnitState(u, UNIT_STATE_LIFE) > 0.405f && !IsUnitType(u, UNIT_TYPE_STRUCTURE))
        {
          army = true;
        }
      }

      DestroyGroup(g);
      if (army)
      {
        return true;
      }

      var cps = ControlPointManager.Instance.GetAllControlPoints();
      for (var i = 0; i < cps.Count; i++)
      {
        var cu = cps[i].Unit;
        if (cu == null || GetOwningPlayer(cu) == p || IsUnitAlly(cu, p))
        {
          continue;
        }

        var dx = GetUnitX(cu) - x;
        var dy = GetUnitY(cu) - y;
        if (dx * dx + dy * dy <= radius * radius)
        {
          return true;
        }
      }

      var caps = CapitalManager.GetAll();
      for (var i = 0; i < caps.Count; i++)
      {
        var cu = caps[i].Unit;
        if (cu == null || GetUnitState(cu, UNIT_STATE_LIFE) <= 0.405f
            || GetOwningPlayer(cu) == p || IsUnitAlly(cu, p))
        {
          continue;
        }

        var dx = GetUnitX(cu) - x;
        var dy = GetUnitY(cu) - y;
        if (dx * dx + dy * dy <= radius * radius)
        {
          return true;
        }
      }

      return false;
    }

    private static void AttackMove(player p, float tx, float ty)
    {
      var g = CreateGroup();
      GroupEnumUnitsOfPlayer(g, p, null);
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        OpUnits++;
        if (!IsUnitType(u, UNIT_TYPE_STRUCTURE) && !IsUnitType(u, UNIT_TYPE_PEON) && u != _ragHero)
        {
          OpOrders++;
          IssuePointOrder(u, "attack", tx, ty);
        }
      }

      DestroyGroup(g);
    }

    private static void Gather(player p, float ax, float ay)
    {
      var g = CreateGroup();
      GroupEnumUnitsOfPlayer(g, p, null);
      unit u;
      while ((u = FirstOfGroup(g)) != null)
      {
        GroupRemoveUnit(g, u);
        OpUnits++;
        if (!IsUnitType(u, UNIT_TYPE_STRUCTURE) && !IsUnitType(u, UNIT_TYPE_PEON) && u != _ragHero)
        {
          OpOrders++;
          IssuePointOrder(u, "attack", ax, ay); // v12: fight to the rally point, not walk blindly
        }
      }

      DestroyGroup(g);
    }
  }

  // v28: per-bot diagnostics, written by SimpleBot.Process, read by AiDebug. Display only.
  public sealed class BotDiag
  {
    public string EdgeSet = "-";
    public int Neutral;      // capturable neutral frontiers found this service
    public int Enemy;        // capturable enemy frontiers (after the 10-min lock)
    public int Locked;       // enemy frontiers skipped because t < 10 min
    public int Invuln;       // frontiers skipped as uncapturable (Neutral Passive / invulnerable)
    public int Closed;       // frontiers behind a not-yet-open time edge or closed named gate
    public int Skipped;      // frontiers that are SkipNodes
    public int NoCp;         // edges with an id that isn't a registered control point
    public int Stuck;        // v31: frontiers skipped because this bot gave up on them (stuck blacklist)
    public unit Target;      // current objective unit (null = point/none)
    public string Reason = "-";
    public float TargetSince;
    public float AnchorX;
    public float AnchorY;
    public bool AnchorIsBase;
    public int HomeLm = -999;
  }

  // v28: one wave's gate status, built on demand by SimpleBot.GetWaveDiag.
  public sealed class WaveDiag
  {
    public int Phase;        // 0 idle, 1 gather, 2 channel
    public float PhaseAge;
    public float CooldownLeft;
    public bool HomeOk;
    public int HomeLm;
    public int Owned;
    public int Total;
    public int Need;
    public bool Monuments;
    public int Enemies;
    public bool TriggerOk;
    public bool Active;
    public bool HasBot;      // v30: false -> wave disabled (no bot plays any faction of the group)
  }

  public static class AiSetup
  {
    // Bump on every code drop. Shown in chat 3s into the game and in every -dump header, so we can always
    // tell which AI build a map actually contains (a stale build looked exactly like "the new commands are broken").
    public const string Build = "0.34.3";

    public static void Setup()
    {
      AiDebug.Setup();
      QuestAutoComplete.Setup();
      SimpleBot.Setup();
      var t = CreateTimer();
      TimerStart(t, 3f, false, AnnounceBuild);
    }

    private static void AnnounceBuild()
    {
      for (var i = 0; i < 24; i++)
      {
        DisplayTimedTextToPlayer(Player(i), 0, 0, 15, "|cff00ffffWL AI " + Build + "|r  (type -help for AI commands)");
      }
    }
  }
}
