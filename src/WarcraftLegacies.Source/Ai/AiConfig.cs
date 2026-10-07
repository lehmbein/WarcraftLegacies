using System.Collections.Generic;

namespace WarcraftLegacies.Source.Ai;

// ---------------------------------------------------------------------------------------------------------
//  WORLD CONSTANTS  (edit these; every coordinate can be checked in-game with -xy, every CP id is in
//  WL-ControlPoints.txt). Monuments gate the invasion; portal endpoints drive the Legion summon and the
//  Outland node bridge. All teleport/portal visuals use one asset that is confirmed present in the map.
// ---------------------------------------------------------------------------------------------------------
public static class AiWorld
{
  // One teleport/portal effect for everything (ship model does not exist in the map; this one is present).
  public const string TeleportFx = "Abilities\\Spells\\Demon\\DarkPortal\\DarkPortalTarget.mdl";

  // A real waygate unit (units walking onto it are teleported). This is the map's Dark Portal Waygate
  // 'n036', which FelHorde/Stormwind already use, so its model is confirmed to render. Swap if a patch
  // ever changes it (generic Way Gate 'nwgt' = 1853319028 is the fallback).
  public const int PortalUnitId = 1848652598; // 'n036' Dark Portal Waygate

  // Monuments per theatre. Owning all three is an alternative to the %-gate (see WaveActive).
  public static readonly List<int> CombinedMonuments = new()
  {
    1848652882, // Icecrown Citadel (north)
    1848652122, // Arathi Highlands (middle)
    1848651849, // Black Temple (south / Outland)
  };

  public static readonly List<int> KalimdorMonuments = new()
  {
    1851016791, // Throne of the Four Winds
    1848652116, // Northern Barrens
    1848652112, // Nordrassil
  };

  // Fewer than this many enemy units may remain on the home landmass for a group to launch/continue an invasion.
  public const int EnemyLeaveThreshold = 10;

  // When a wave's home CP sits on landmass -1 (islands like Kul Tiras -- the downsample can't resolve them),
  // the wave gates count within this radius of the home CP instead of by landmass (which would span the whole
  // map's coastline and make the %/enemy gate impossible). Big enough to cover an island, small enough to
  // exclude the mainland landing.
  public const float HomeWaveRadius = 6000f;

  // LEGION SUMMON portal pair (spawned fresh at summon -- replaces hunting the invisible quest portal).
  //   nether end = Antoran Wastes (the demon staging), exit end = beside Felwood (Kalimdor). Verify with -xy.
  public const float LegionNetherX = 19456f;  // Antoran Wastes / Twisting Nether -- verify with -xy
  public const float LegionNetherY = -25008f;
  // v34 ARGUS HUB (user coordinates, checked against the pathing map):
  //  * Argus end of the AI's Legion portal pair (Antoran Wastes side). Before the summon it links to Northrend
  //    (Crystalsong Forest), after the summon to Felwood.
  public const float LegionArgusPortalX = 19644f;
  public const float LegionArgusPortalY = -26014f;
  public const float LegionNorthrendPortalX = -1500f;   // beside Crystalsong Forest
  public const float LegionNorthrendPortalY = 20875f;
  //  * Kalimdor end after the summon (Felwood). Also where a bot Legion's host is collected at the summon.
  public const float LegionExitX = -17377f;
  public const float LegionExitY = 8732f;
  //  * WL's own exterior Demon Portal (n037, created at (0,0) by a caster-less summon) is rebuilt here, beside
  //    Quel'Danas / the Sunwell, and WL's Antoran portal (n03C) is re-pointed at it.
  public const float LegionSunwellPortalX = 17856f;
  public const float LegionSunwellPortalY = 21983f;
  //  * Legion start (bot only): its Argus workers are replaced by buildings around the start location.
  public const float LegionStartX = 18816f;
  public const float LegionStartY = -30784f;
  public const int LegionHeraldId = 1966092356;          // u00D Legion Herald (worker)

  // v34.1: Kul Tiras' starting quest ("The Admiralty of Kul Tiras") crashes in WL's reward code (a '%' in the
  // City of Admirals text breaks WL's text replacement) BEFORE it hands over the base. The AI repairs that
  // step: Neutral-Passive units left in WL's Kul Tiras rescue area (Regions.Kultiras) are rescued for Kul Tiras.
  public const float KulTirasRescueMinX = 704f;
  public const float KulTirasRescueMinY = -10304f;
  public const float KulTirasRescueMaxX = 5856f;
  public const float KulTirasRescueMaxY = -4672f;
  // WL's preplaced interior Demon Portal (n03C) in Antoran -- re-pointed at the exit (from war3mapUnits.doo).
  public const float LegionInteriorX = 20443f;
  public const float LegionInteriorY = -28656f;
  public const int LegionSummonGold = 3000;   // v31: one-time gold for a bot Legion on summon
}

// AUTO-DERIVED from Warcraft Legacies source (branch: main / 4.8.x) -- DATA ONLY, no logic.
// Keyed by faction.Name (exact runtime string). Values are hero unit-type ids as generated
// constants: a WL update that RENAMES or REMOVES a hero fails the build HERE, at the exact line,
// which is the signal to refresh. A WL update that ADDS a hero compiles fine but stays uncovered
// until this file is regenerated -- after any WL update, re-extract heroes from source and diff.
// AiSystem falls back gracefully (skips hero training) for any faction not listed here.
public static class AiConfig
{
  // v30-v32: quests force-completed for BOT factions, so quest-locked CPs / units / heroes become available to
  // bots that never meet the objectives themselves. Matched by the quest's exact Title. Checked every 30 s;
  // fires once, the first time ALL of its set conditions hold (unset = no condition):
  //   AtSec              game time reached (seconds)
  //   UnlessPlayed       that faction is NOT held by a player (don't hand away a played faction's land)
  //   CapitalLost        a capital of this unit type is DEAD or owned by an ENEMY PLAYER (v32: never "neutral")
  //   CpHeldByAlly       this CP is owned by the faction or an ally
  //   CapitalHeldByAlly  a living capital of this unit type is owned by the faction or an ally
  //   MinCps             the faction owns at least this many CPs
  //   HeroAlive          the faction owns a living hero of this unit type (quests whose reward acts on him)
  public static readonly List<QuestRule> QuestRules = new()
  {
    // Dalaran's Greymane Wall (normally: hold Hinterlands + Jintha'Alor + Arathi) -> Gilneas City & co.
    new QuestRule("Dalaran", "The Greymane Wall", 900f) { UnlessPlayed = "Gilneas" },
    // Stormwind Keep (h00X) destroyed or taken by an enemy -> Galen Trollbane's Stromgarde forces join.
    // v32 fix: the keep is preplaced Neutral-Passive and handed over later; "not Stormwind's" fired at 0:29.
    new QuestRule("Stormwind", "Stromgarde", 0f) { CapitalLost = 1747988568 },
    // Quel'thalas: an Alliance member (Quel'thalas or any ally) holds Hinterlands (n01A) -> Quel'danil Lodge.
    new QuestRule("Quel'thalas", "Quel'danil Lodge", 0f) { CpHeldByAlly = 1848652097 },

    // ---- v32 late-game payoffs ----
    // Thrall becomes the World-Shaman: Frostwolf hold 20 CPs after 30 min, Thrall (Othr) alive.
    new QuestRule("Frostwolf", "The World-Shaman", 1800f) { MinCps = 20, HeroAlive = 1333028978 },
    // Guardian of Tirisfal: Dalaran or an ally holds Karazhan (h00G) after 30 min.
    new QuestRule("Dalaran", "Guardian of Tirisfal", 1800f) { CapitalHeldByAlly = 1747988551 },
    // Line of Succession: Lordaeron (or ally) holds Capital Palace (h000) + Howling Fjords (n02J) after 30 min,
    // Arthas (Hart) alive -- the reward crowns him.
    new QuestRule("Lordaeron", "Line of Succession", 1800f)
      { CapitalHeldByAlly = 1747988528, CpHeldByAlly = 1848652362, HeroAlive = 1214345844 },
    // The Ascension: Scourge (or ally) holds Icecrown Citadel (n04R) after 40 min, Arthas (Uear) alive.
    new QuestRule("Scourge", "The Ascension", 2400f) { CpHeldByAlly = 1848652882, HeroAlive = 1432707442 },
  };

  // v32: EARLY quests that expire ("fail") on a WL turn. For BOT factions each still-open one is force-completed
  // during the turn BEFORE it expires (turn N-1, read from WL's own turn counter), one every few seconds so the
  // rewards (rescued bases, heroes) don't all land in the same frame. Expiry turns extracted from the 4.8.2 map
  // script (ObjectiveExpire). Remove a line to let that quest fail for bots.
  public static readonly List<EarlyQuest> EarlyQuests = new()
  {
    new EarlyQuest("Awakening of Stormrage", 8),       // Druids -> Malfurion
    new EarlyQuest("Daughters of the Moon", 8),        // Sentinels
    new EarlyQuest("Hyjal's Rest", 8),                 // Druids
    new EarlyQuest("The Admiralty of Kul Tiras", 8),   // Kul Tiras -> Katherine Proudmoore (chain to Booty Bay)
    new EarlyQuest("The City of Invention", 8),        // Ironforge
    new EarlyQuest("The Crossroads", 8),               // Frostwolf (8) / Warsong (10) -- earliest used
    new EarlyQuest("The Druid's Rise", 8),             // Druids
    new EarlyQuest("Clear the Outskirts", 10),         // Stormwind
    new EarlyQuest("Gnoll Troubles", 10),              // Stormwind
    new EarlyQuest("Murloc Menace", 10),               // Ironforge
    new EarlyQuest("Nethergarde Relief", 10),          // Stormwind
    new EarlyQuest("The Citadel", 10),                 // Fel Horde
    new EarlyQuest("The Scourge of Elwynn", 10),       // Stormwind
    new EarlyQuest("The Spirits of Ashenvale", 10),    // Druids
    new EarlyQuest("Blackrock and Roll", 11),          // Lordaeron
    new EarlyQuest("Coilfang Reservoir", 11),          // Illidan
    new EarlyQuest("Drak'tharon Keep", 11),            // Scourge
    new EarlyQuest("Hearthglen", 11),                  // Lordaeron
    new EarlyQuest("Hearthlands", 11),                 // Lordaeron
    new EarlyQuest("Marauding Ogres", 11),             // Stormwind
    new EarlyQuest("Mountain Village", 11),            // Ironforge
    new EarlyQuest("Murloc Troubles", 11),             // Dalaran
    new EarlyQuest("Outskirts", 11),                   // Dalaran
    new EarlyQuest("Return to Outland", 11),           // Illidan
    new EarlyQuest("Shadowfang Keep", 11),             // Dalaran / Gilneas
    new EarlyQuest("Shadows of Silverpine Forest", 11),// Dalaran
    new EarlyQuest("SouthShore", 11),                  // Dalaran / Gilneas
    new EarlyQuest("Temple City of En'kilah", 11),     // Scourge
    new EarlyQuest("The Cape of Stranglethorn", 11),   // Illidan
    new EarlyQuest("The Defense of Strahnbrad", 11),   // Lordaeron
    new EarlyQuest("The Siege of Silvermoon", 11),     // Quel'thalas
    new EarlyQuest("War of the Spider", 11),           // Scourge
    // Old Gods early quests (Skywall / Black Empire / Ahn'qiraj) -- remove if they make the Old Gods too strong:
    new EarlyQuest("Desolace", 11),
    new EarlyQuest("Feralas", 11),
    new EarlyQuest("Rebuilding of Ahn'Qiraj", 11),
    new EarlyQuest("Shimmering Flats", 11),
    new EarlyQuest("Slithering Forward", 11),
    new EarlyQuest("The Noxious Lair", 11),
    new EarlyQuest("The Throne of the Four Winds", 11),
    new EarlyQuest("The Vortex Pinnacle", 11),
    new EarlyQuest("The Waking City", 11),
    new EarlyQuest("Titan Jailors", 11),
    new EarlyQuest("Twilight landing", 11),
    new EarlyQuest("The Long March", 12),              // Frostwolf
    new EarlyQuest("To Tame a Land", 13),              // Frostwolf
    new EarlyQuest("Blackrock Unification", 15),       // Fel Horde
    new EarlyQuest("The Darkspear Trolls", 15),        // Frostwolf
  };

  // v32: a perma-dead legend that WL brings back in a NEW FORM is removed again when a BOT owns it.
  // Cenarius -> his revivable ghost (E00H): for bots Cenarius stays dead after his first death.
  public static readonly HashSet<int> BannedNewForms = new()
  {
    1160785992, // E00H Demigod of the Night Elves (Druids-GHOST)
  };

  // v32: Ragnaros summoning pedestal (n02B). WL's neutral quest "Lord of the Firelands" completes when a hero
  // of level >= 10 stands in its rect; the pedestal then belongs to that hero's owner. A bot that owns one of
  // RagnarosTriggerCps sends its best hero there ONCE (level raised to RagnarosHeroLevel on arrival).
  // Blackrock Depths itself starts Neutral-Passive (uncapturable), so its graph neighbours also count.
  public const int RagnarosPedestalId = 1848652354;   // n02B
  public const float RagnarosX = 12352f;
  public const float RagnarosY = -10624f;
  public const int RagnarosHeroLevel = 12;
  public static readonly List<int> RagnarosTriggerCps = new()
  {
    1848652340, // n024 Blackrock Depths
    1848652376, // n02X Searing Gorge
    1848652082, // n012 Burning Steppes
  };

  // v32: Lordaeron's Capital Palace (h000) and Lordaeron City (n01G): taken by a bot Legion while it is still
  // allied to the Scourge -> handed to the Scourge (the Scourge, not the demons, sacks the capital).
  public const int CapitalPalaceId = 1747988528;
  public const int LordaeronCityCpId = 1848652103;

  // v31: extra share of the flat bot gold subsidy (SimpleBot.BotGoldBonus) per faction, in percent.
  // Scourge/Legion were losing to the Alliance -> +50%. Factions not listed get 100%.
  public static readonly Dictionary<string, int> FactionGoldPct = new()
  {
    { "Scourge", 150 },
    { "Legion", 150 },
  };

  // v31: a faction whose frontier choice should head for one CP along the shortest graph route (taking the
  // CPs in between). Active only while the goal is in the faction's current edge set and not yet owned.
  public static readonly Dictionary<string, int> Goals = new()
  {
    { "Legion", 1848652112 }, // Nordrassil (Mount Hyjal) -- after the summon the Legion is on the Kalimdor set
  };

  // v31: a bot hunts one specific unit (by TYPE id, searched once near X/Y) from AtSec until it is dead.
  // Replaces the old xy "one-shot" force for Sapphiron, which sent 6 units that the next army order recalled.
  public static readonly List<KillTarget> KillTargets = new()
  {
    new KillTarget("Scourge", "Sapphiron", 1969382514 /* 'ubdr' */, -2633f, 18715f, 1200f, 180f),
    // v32: Murmur (n03T, Outland, lvl 9) -- Fel Horde clears it inside the first 10 minutes.
    new KillTarget("Fel Horde", "Murmur", 1848652628 /* 'n03T' */, -74f, -30990f, 1500f, 360f),
  };

  // v34.2: AREA CLEARS (one-shot): the bot that owns TriggerCpId sends its army to kill every Neutral Hostile
  // unit within Radius of X/Y (nearest first), once per game. Gives up after 8 min.
  public static readonly List<ClearArea> ClearAreas = new()
  {
    new ClearArea("the Oculus", 1848652336 /* n020 Tanaris */, -9103f, -15943f, 1200f),
    new ClearArea("the Nexus", 1848651847 /* n00G Borean Tundra */, -9984f, 17856f, 1200f),
  };

  // v31: neutral CPs handed to a BOT faction at a game time (creeps around each are removed so they can't
  // retake it). Only CPs still owned by Neutral Hostile are granted. No graph needed.
  public static readonly List<CpGrant> CpGrants = new()
  {
    // v34: Illidan's Broken Isles grant removed -- Illidan plays from Outland; the Broken Isles are now ordinary
    // Kalimdor island nodes reached by ship (lore-fitting connection: to do).
  };

  // Standard combat upgrades (weapons / armor / defense), by raw code. Granted to every bot, scaling
  // with game time. A code a faction's units don't use is a harmless no-op.
  public static readonly List<int> CombatUpgrades = new()
  {
    1382575461, // Rhme  Forged Weaponry (melee/attack)
    1382572402, // Rhar  Armor Plating
    1382576745, // Rhri  (piercing/defense)
    1382572387, // Rhac  (artillery/defense)
    1383031154, // Roar  Orc Armor
    1383036772, // Rowd  (orc defense)
    1382379106, // Renb  (elf defense)
    1382380147, // Rers  (elf defense)
  };

  public static readonly Dictionary<string, List<int>> Heroes = new()
  {
    { "Ahn'qiraj", new List<int> { UNIT_E005_THE_PROPHET, UNIT_U00Z_OBSIDIAN_DESTROYER, UNIT_U02S_ANCIENT_SAND_WORM } },
    { "Black Empire", new List<int> { UNIT_E01D_HARBINGER_OF_NY_ALOTHA_NZOTH, UNIT_U00P_LIEUTENANT_OF_N_ZOTH_NZOTH, UNIT_U02B_N_RAQI_ABERRATION_NZOTH } },
    { "Dalaran", new List<int> { UNIT_H09N_MATRIARCH_OF_TIRISFAL_DALARAN, UNIT_HAAH_THE_FALLEN_GUARDIAN_DALARAN, UNIT_HANT_GRAND_MAGUS_OF_THE_KIRIN_TOR_DALARAN, UNIT_HJAI_ARCHMAGE_OF_DALARAN_DALARAN } },
    { "Druids", new List<int> { UNIT_E00A_ANCIENT_GUARDIAN_DRUIDS, UNIT_ECEN_DEMIGOD_OF_THE_NIGHT_ELVES_DRUIDS, UNIT_EFUR_ARCHDRUID_DRUIDS, UNIT_H04U_DEMIGOD_DRUIDS } },
    { "Fel Horde", new List<int> { UNIT_N03D_WARCHIEF_OF_THE_FEL_HORDE_FEL, UNIT_NBBC_WARCHIEF_OF_THE_BLACKROCK_CLAN_FEL, UNIT_NMAG_LORD_OF_OUTLAND_FEL, UNIT_U02D_DEATH_KNIGHT_LORD_FEL_HORDE } },
    { "Frostwolf", new List<int> { UNIT_OCBH_CHIEFTAIN_OF_THE_BLOODHOOF_FROSTWOLF, UNIT_OREX_BEASTMASTER_FROSTWOLF, UNIT_ORKN_CHIEFTAIN_OF_THE_DARKSPEAR_TRIBE_FROSTWOLF, UNIT_OTHR_WARCHIEF_OF_THE_HORDE_FROSTWOLF } },
    { "Gilneas", new List<int> { UNIT_E01E_ANCIENT_GUARDIAN_GILNEAS, UNIT_HHKL_KING_OF_GILNEAS_GILNEAS, UNIT_HPB2_GILNEAN_LORD_GILNEAS, UNIT_TGGN_PRINCESS_OF_GILNEAS_GILNEAS } },
    { "Illidan", new List<int> { UNIT_EEVI_DEMON_HUNTER_ILLIDARI_HYBRID_ILLIDAN, UNIT_HVSH_SEA_WITCH_ILLIDARI, UNIT_NAKA_ELDER_SAGE_ILLIDARI, UNIT_U00S_HIGH_WARLORD_ILLIDARI } },
    { "Ironforge", new List<int> { UNIT_H00S_KING_OF_KHAZ_MODAN_IRONFORGE, UNIT_H028_THANE_OF_AERIE_PEAK_IRONFORGE, UNIT_H03G_EMPEROR_OF_BLACKROCK_IRONFORGE } },
    { "Kul'tiras", new List<int> { UNIT_E016_RULER_OF_HOUSE_WAYCREST_KULTIRAS, UNIT_H05L_LADY_OF_HOUSE_PROUDMOORE_KULTIRAS, UNIT_HAPM_LORD_ADMIRAL_OF_KUL_TIRAS_KULTIRAS, UNIT_U026_MATRIARCH_OF_HOUSE_WAYCREST_KULTIRAS } },
    { "Legion", new List<int> { UNIT_U00L_ENVOY_OF_ARCHIMONDE_LEGION, UNIT_UMAL_THE_CUNNING_LEGION, UNIT_UTIC_THE_DARKENER_LEGION } },
    { "Lordaeron", new List<int> { UNIT_H01J_THE_ASHBRINGER_LORDAERON, UNIT_HARF_HIGH_KING_LORDAERON_HIGH_KING, UNIT_HART_CROWN_PRINCE_OF_LORDAERON_LORDAERON, UNIT_HLGR_GRAND_MARSHAL_SCARLET, UNIT_HUTH_LEADER_OF_THE_SILVER_HAND_LORDAERON } },
    { "Quel'thalas", new List<int> { UNIT_H00Q_KING_OF_QUEL_THALAS_QUELTHALAS, UNIT_H02E_REGENT_OF_QUEL_THALAS_QUELTHALAS_VASSAL, UNIT_H04F_ARCHMAGE_QUELTHALAS, UNIT_HVWD_RANGER_GENERAL_OF_SILVERMOON_QUELTHALAS } },
    { "Scarlet Crusade", new List<int> { UNIT_H00Y_HIGH_GENERAL_SCARLET, UNIT_H08G_GRAND_CRUSADER_SCARLET, UNIT_H08H_HIGH_INQUISITOR_SCARLET, UNIT_H0A2_SCARLET_COMMANDER_SCARLET } },
    { "Scourge", new List<int> { UNIT_N009_REVENANT_SCOURGE, UNIT_N023_LORD_OF_THE_SCOURGE_SCOURGE, UNIT_U001_MASTER_OF_THE_CULT_OF_THE_DAMNED_SCOURGE_NECROMANCER, UNIT_U00A_SCOURGE_COMMANDER_SCOURGE, UNIT_U00M_MASTER_OF_THE_CULT_OF_THE_DAMNED_SCOURGE_GHOST, UNIT_UANB_KING_OF_AZJOL_NERUB_SCOURGE, UNIT_UEAR_CHAMPION_OF_THE_SCOURGE_SCOURGE } },
    { "Sentinels", new List<int> { UNIT_E002_GENERAL_OF_THE_SENTINEL_ARMY_SENTINELS, UNIT_E025_LIEUTENANT_OF_THE_WATCHERS_SENTINELS, UNIT_ETYR_HIGH_PRIESTESS_OF_ELUNE_SENTINELS, UNIT_EWRD_LEADER_OF_THE_WATCHERS_SENTINELS } },
    { "Skywall", new List<int> { UNIT_E023_GRAND_VIZIER_SKYWALL, UNIT_U01S_WINDLORD_SKYWALL, UNIT_U02K_LORD_OF_THE_FIRELANDS_SKYWALL, UNIT_UELN_THE_TIDEHUNTER_SKYWALL } },
    { "Stormwind", new List<int> { UNIT_H00R_KING_OF_STORMWIND_STORMWIND, UNIT_H00Z_CROWN_PRINCE_OF_STROMGARDE_STORMWIND, UNIT_H017_HIGHLORD_OF_THE_ALLIANCE_STORMWIND, UNIT_H05Y_LORD_WIZARD_STORMWIND } },
    { "Sunfury", new List<int> { UNIT_H098_SUNFURY_MASTERMIND_QUELTHALAS, UNIT_HKAL_PRINCE_OF_QUEL_THALAS_QUELTHALAS, UNIT_U004_THE_DECEIVER_LEGION, UNIT_U02V_HIGH_ASTROMANCER_SUNFURY } },
    { "The Exodar", new List<int> { UNIT_E01I_AGELESS_ONE_DRUIDS, UNIT_E01J_HIGH_SHAMAN_DRUIDS, UNIT_H09M_THE_NAARU_DRAENEI, UNIT_H09S_HAMMER_OF_THE_LIGHT_DRAENEI } },
    { "Warsong", new List<int> { UNIT_MD25_DARKSPEAR_CHAMPION_WARSONG, UNIT_NMAN_MANNOROTH_THE_DESTROYER_WARSONG_BLOODPACT, UNIT_O005_WARSONG_BATTLEMASTER_WARSONG, UNIT_O06L_WARLORD_OF_THE_WARSONG_CLAN_WARSONG, UNIT_OGRH_CHIEFTAIN_OF_THE_WARSONG_CLAN_WARSONG, UNIT_OPGH_CORRUPTOR_OF_THE_WARSONG_CLAN_WARSONG_BLOODPACT, UNIT_VSWS_HIGH_OVERLORD_OF_THE_KOR_KRON_WARSONG } },
  };

  // Per-faction ordered attack march. Fill X,Y via in-game -xy (fly to each spot, read its coords). AfterSec
  // time-gates a step (0 = from start, 600 = after 10 min). A step left at (0,0) is an unfilled placeholder
  // and is skipped. The bot advances to the next step once a step's area has no enemy army AND no CP/capital
  // it doesn't own. Any faction not listed here (or with every step still unfilled) falls back to the generic
  // nearest-control-point-then-capital picker, so this is purely additive.
  public static readonly Dictionary<string, List<DoctrineStep>> Doctrine = new()
  {
    { "Scourge", new List<DoctrineStep>
      {
        // under 10 min: clear Northrend
        new DoctrineStep(0f, 0f, 0f),   // Storm Peaks
        new DoctrineStep(0f, 0f, 0f),   // Drak'Tharon
        new DoctrineStep(0f, 0f, 0f),   // The Nexus
        new DoctrineStep(0f, 0f, 0f),   // Borean Tundra
        new DoctrineStep(0f, 0f, 0f),   // Dragonblight
        new DoctrineStep(0f, 0f, 0f),   // Venture Bay
        new DoctrineStep(0f, 0f, 0f),   // Howling Fjord
        new DoctrineStep(0f, 0f, 0f),   // Grizzly Hills
        new DoctrineStep(0f, 0f, 0f),   // Zul'Drak
        // after 10 min: push south through Lordaeron & Quel'Thalas to the Sunwell, then west to Ironforge
        new DoctrineStep(0f, 0f, 600f), // Lordaeron City
        new DoctrineStep(0f, 0f, 600f), // Andorhal
        new DoctrineStep(0f, 0f, 600f), // Strahnbrad
        new DoctrineStep(0f, 0f, 600f), // Dalaran
        new DoctrineStep(0f, 0f, 600f), // Southshore
        new DoctrineStep(0f, 0f, 600f), // Tranquillien (runestone dies first via attack-move)
        new DoctrineStep(0f, 0f, 600f), // Eversong Woods (runestone)
        new DoctrineStep(0f, 0f, 600f), // Silvermoon
        new DoctrineStep(0f, 0f, 600f), // Quel'Danas / Sunwell
        new DoctrineStep(0f, 0f, 600f), // Stratholme
        new DoctrineStep(0f, 0f, 600f), // Gilneas City
        new DoctrineStep(0f, 0f, 600f), // Arathi Highlands
        new DoctrineStep(0f, 0f, 600f), // Dun Modr
        new DoctrineStep(0f, 0f, 600f), // Wetlands
        new DoctrineStep(0f, 0f, 600f), // Dun Morogh
        new DoctrineStep(0f, 0f, 600f), // Ironforge
      }
    },
  };
}

// ---------------------------------------------------------------------------------------------------------
// AUTHORED CONTROL-POINT GRAPH  (fill real ids via the in-game -ids overlay; comment every line)
// Edges are undirected: { A, B } means a bot holding A can push to B and vice-versa. A bot walks from the
// CPs it owns to the nearest bordering CP it does NOT own -- so the same graph drives every faction, each
// from its own side. Continents chain linearly (00->01->02...), branch only for detours (a spur just needs
// its one connecting edge; the bot captures it then backtracks). Ids left as 0 are ignored.
public sealed class QuestRule
{
  public readonly string Faction;
  public readonly string Title;
  public readonly float AtSec;
  // optional conditions (set with an object initializer; 0 / "" = not used)
  public string UnlessPlayed = "";
  public int CapitalLost;
  public int CpHeldByAlly;
  public int CapitalHeldByAlly;
  public int MinCps;
  public int HeroAlive;

  public QuestRule(string faction, string title, float atSec)
  {
    Faction = faction;
    Title = title;
    AtSec = atSec;
  }
}

public sealed class EarlyQuest
{
  public readonly string Title;
  public readonly int ExpireTurn;

  public EarlyQuest(string title, int expireTurn)
  {
    Title = title;
    ExpireTurn = expireTurn;
  }
}

public sealed class KillTarget
{
  public readonly string Faction;
  public readonly string Label;
  public readonly int UnitType;
  public readonly float X;
  public readonly float Y;
  public readonly float Radius;
  public readonly float AtSec;

  public KillTarget(string faction, string label, int unitType, float x, float y, float radius, float atSec)
  {
    Faction = faction;
    Label = label;
    UnitType = unitType;
    X = x;
    Y = y;
    Radius = radius;
    AtSec = atSec;
  }
}

public sealed class ClearArea
{
  public readonly string Label;
  public readonly int TriggerCpId;
  public readonly float X;
  public readonly float Y;
  public readonly float Radius;

  public ClearArea(string label, int triggerCpId, float x, float y, float radius)
  {
    Label = label;
    TriggerCpId = triggerCpId;
    X = x;
    Y = y;
    Radius = radius;
  }
}

public sealed class CpGrant
{
  public readonly string Faction;
  public readonly float AtSec;
  public readonly List<int> CpIds;

  public CpGrant(string faction, float atSec, List<int> cpIds)
  {
    Faction = faction;
    AtSec = atSec;
    CpIds = cpIds;
  }
}

public sealed class Edge
{
  public readonly int A;
  public readonly int B;
  public readonly float OpenTime; // seconds before this edge may be used (0 = always). Bridges/portals use this.
  public readonly string Gate;    // named gate flag that must be open ("" = none). e.g. "greymane".

  public Edge(int a, int b)
  {
    A = a;
    B = b;
    OpenTime = 0f;
    Gate = "";
  }

  public Edge(int a, int b, float openTime, string gate)
  {
    A = a;
    B = b;
    OpenTime = openTime;
    Gate = gate;
  }
}

// EXAMPLE: 3-node chain + one detour spur. Replace the 0s with real control-point ids from -ids.
public static class AiGraph
{
  // COMBINED continent: Northrend + Lordaeron + Eastern Kingdoms + Outland, one connected graph.
  // Conditional edges (bridges/portals) carry an open-time; the Greymane gate carries a flag.
  // Auto-generated from the authored node list; edit the list and regenerate rather than hand-editing.
  public static readonly List<Edge> Combined = new()
  {
    new Edge(1848652882, 1848653892), // Icecrown Citadel <-> Icecrown Glacier
    new Edge(1848653892, 1848657219), // Icecrown Glacier <-> Mord’rethar
    new Edge(1848657219, 1848652370), // Mord’rethar <-> Crystalsong Forest
    new Edge(1848652370, 1848652371), // Crystalsong Forest <-> Storm Peaks
    new Edge(1848652370, 1848652592), // Crystalsong Forest <-> Drak'Tharon Keep
    new Edge(1848652592, 1848651848), // Drak'Tharon Keep <-> Zul'Drak
    new Edge(1848652592, 1848652629), // Drak'Tharon Keep <-> Grizzly Hills
    new Edge(1848652629, 1848652362), // Grizzly Hills <-> Howling Fjords
    new Edge(1848652362, 1848653657), // Howling Fjords <-> Venture Bay
    new Edge(1848653657, 1848652369), // Venture Bay <-> Dragonblight
    new Edge(1848652369, 1848651847), // Dragonblight <-> Borean Tundra
    new Edge(1848651847, 1848654152), // Borean Tundra <-> En'Kilah
    new Edge(1848654152, 1848651846), // En'Kilah <-> Sholazar Basin
    new Edge(1848651846, 1848653892), // Sholazar Basin <-> Icecrown Glacier
    new Edge(1848652362, 1848652102, 600f, ""), // Howling Fjords <-> Tirisfal Glades
    new Edge(1848652102, 1848652100), // Tirisfal Glades <-> Silverpine Forest
    new Edge(1848652100, 1848652098), // Silverpine Forest <-> Dalaran
    new Edge(1848652098, 1848653901), // Dalaran <-> Southshore
    new Edge(1848653901, 1848652089), // Southshore <-> Alterac Mountains
    new Edge(1848652089, 1848652099), // Alterac Mountains <-> Strahnbrad
    new Edge(1848652099, 1848652104), // Strahnbrad <-> Andorhal
    new Edge(1848652104, 1848652852), // Andorhal <-> Hearthglen
    new Edge(1848652852, 1848652616), // Hearthglen <-> Brill
    new Edge(1848652616, 1848652103), // Brill <-> Lordaeron City
    new Edge(1848652103, 1848652102), // Lordaeron City <-> Tirisfal Glades
    new Edge(1848652104, 1848652624), // Andorhal <-> Corin's Crossing
    new Edge(1848652624, 1848652375), // Corin's Crossing <-> Tyr's Hand
    new Edge(1848652375, 1848652887), // Tyr's Hand <-> Havenshire
    new Edge(1848652104, 1848652109), // Andorhal <-> Stratholme
    new Edge(1848652624, 1848652110), // Corin's Crossing <-> Tranquillien
    new Edge(1848652110, 1848652118), // Tranquillien <-> Zul'Aman
    new Edge(1848652110, 1848652108), // Tranquillien <-> Eversong Woods
    new Edge(1848652108, 1848652111), // Eversong Woods <-> Silvermoon
    new Edge(1848652111, 1848652622), // Silvermoon <-> Quel'Danas
    new Edge(1848652099, 1848652105), // Strahnbrad <-> Caer Darrow
    new Edge(1848652105, 1848652097), // Caer Darrow <-> Hinterlands
    new Edge(1848652097, 1848657218), // Hinterlands <-> Jintha'Alor
    new Edge(1848657218, 1848652122), // Jintha'Alor <-> Arathi Highlands
    new Edge(1848652122, 1848652107), // Arathi Highlands <-> Stromgarde
    new Edge(1848652107, 1848652088), // Stromgarde <-> Durnholde
    new Edge(1848652088, 1848653901), // Durnholde <-> Southshore
    new Edge(1848652088, 1848652099), // Durnholde <-> Strahnbrad
    new Edge(1848652122, 1848652088), // Arathi Highlands <-> Durnholde (v30: direct link, per map)
    new Edge(1848652122, 1848652087, 1200f, ""), // Arathi Highlands <-> Dun Modr
    new Edge(1848652087, 1848652632), // Dun Modr <-> Grim Batol
    new Edge(1848652632, 1848654150), // Grim Batol <-> Southern Highlands
    new Edge(1848654150, 1848653908), // Southern Highlands <-> Dragonmaw Port
    new Edge(1848653908, 1848652886), // Dragonmaw Port <-> Northern Highlands
    new Edge(1848652087, 1848652086), // Dun Modr <-> Wetlands
    new Edge(1848652086, 1848653399), // Wetlands <-> Menethil Harbour
    new Edge(1848652086, 1848652084), // Wetlands <-> Dun Morogh
    new Edge(1848652084, 1848652085), // Dun Morogh <-> Ironforge
    new Edge(1848652084, 1848653400), // Dun Morogh <-> Gnomeregan
    new Edge(1848652086, 1848652083), // Wetlands <-> Loch Modan
    new Edge(1848652083, 1848652084), // Loch Modan <-> Dun Morogh (v31: direct link, per map)
    new Edge(1848652083, 1848652365), // Loch Modan <-> Northern Badlands
    new Edge(1848652365, 1848652376), // Northern Badlands <-> Searing Gorge
    new Edge(1848652376, 1848652340), // Searing Gorge <-> Blackrock Depths
    new Edge(1848652376, 1848652082), // Searing Gorge <-> Burning Steppes
    new Edge(1848652082, 1848652101), // Burning Steppes <-> Fuselight
    new Edge(1848652365, 1848652101), // Northern Badlands <-> Fuselight (v30: direct link, per map)
    new Edge(1848652082, 1848652081), // Burning Steppes <-> Redridge Mountains
    new Edge(1848652081, 1848651861), // Redridge Mountains <-> Swamp of Sorrows
    new Edge(1848651861, 1848651860), // Swamp of Sorrows <-> Nethergarde Keep
    new Edge(1848651860, 1848656185), // Nethergarde Keep <-> Blasted Lands
    new Edge(1848652081, 1848651866), // Redridge Mountains <-> Elwynn Forest
    new Edge(1848651866, 1848652080), // Elwynn Forest <-> Stormwind City
    new Edge(1848651866, 1848652364), // Elwynn Forest <-> Westfall
    new Edge(1848652364, 1848651862), // Westfall <-> Duskwood
    new Edge(1848651862, 1848651864), // Duskwood <-> Stranglethorn
    new Edge(1848651864, 1848651863), // Stranglethorn <-> Zul'Gurub
    new Edge(1848651864, 1848651852), // Stranglethorn <-> Booty Bay
    new Edge(1848651852, 1848652631), // Booty Bay <-> Jaguero Isle
    new Edge(1848651864, 1848652361), // Stranglethorn <-> Western Stranglethorn
    new Edge(1848651862, 1848651865), // Duskwood <-> Deadwind Pass
    new Edge(1848651865, 1848651861), // Deadwind Pass <-> Swamp of Sorrows
    new Edge(1848651860, 1848651859, 600f, ""), // Nethergarde Keep <-> Hellfire Peninsula
    new Edge(1848651859, 1835282482), // Hellfire Peninsula <-> East-Zangarmarsh
    new Edge(1835282482, 1848652366), // East-Zangarmarsh <-> Blade's Edge Mountains
    new Edge(1848652366, 1848652367), // Blade's Edge Mountains <-> Netherstorm
    new Edge(1848652367, 1848656727), // Netherstorm <-> Farahlon
    new Edge(1848656727, 1848653649), // Farahlon <-> Area 52
    new Edge(1848653649, 1848652366), // Area 52 <-> Blade's Edge Mountains
    new Edge(1835282482, 1835282481), // East-Zangarmarsh <-> West-Zangarmarsh
    new Edge(1835282481, 1848656726), // West-Zangarmarsh <-> Halaar
    new Edge(1848656726, 1848654169), // Halaar <-> Kil'sorrow Fortress
    new Edge(1848656726, 1848651842), // Halaar <-> Nagrand
    new Edge(1835282482, 1848651842), // East-Zangarmarsh <-> Nagrand
    new Edge(1835282482, 1848654168), // East-Zangarmarsh <-> Shattrath City
    new Edge(1848654168, 1848652372), // Shattrath City <-> Terokkar Forest
    new Edge(1848652372, 1848654166), // Terokkar Forest <-> Shadowmoon Valley
    new Edge(1848654166, 1835282483), // Shadowmoon Valley <-> Warden's Cage
    new Edge(1835282483, 1835282484), // Warden's Cage <-> Altar of Shatar
    new Edge(1835282484, 1848651849), // Altar of Shatar <-> Black Temple
    new Edge(1848652100, 1848652854, 0f, "greymane"), // Silverpine Forest <-> Gilneas City
    new Edge(1848652854, 1848653912), // Gilneas City <-> Keel Harbor
    new Edge(1848652854, 1848652593), // Gilneas City <-> Duskhaven
    new Edge(1848652593, 1848653398), // Duskhaven <-> Blackwald
    new Edge(1848652622, 1848651825), // Quel'Danas <-> The Sunwell (triggers Legion)
    new Edge(1848652622, 1848653625), // Quel'Danas <-> Sunwell Corrupted

    // ---- v34: Kul Tiras island (ship portal Tiragarde Sound <-> Stormwind City, see AiGraph.Portals) ----
    new Edge(1848656470, 1848656471), // Drustvar <-> Stormsong Valley
    new Edge(1848656471, 1848652119), // Stormsong Valley <-> Boralus
    new Edge(1848652119, 1848656472), // Boralus <-> Tiragarde Sound
    new Edge(1848656472, 1848652080), // Tiragarde Sound <-> Stormwind City (ship)
    new Edge(1848656472, 1848653644), // Tiragarde Sound <-> Balor (same island, ground path -- v34.1)
    new Edge(1848653644, 1848652361), // Balor <-> Western Stranglethorn (ship -- v34.1)
    new Edge(1848651853, 1848654150), // Highbank <-> Southern Highlands (land, same ground area -- v34.2)

    // ---- v34: Argus. Open from the start (the Legion clears its home first), reached from Northrend by the
    // Antoran <-> Crystalsong portal until the summon; after it, Argus links to Quel'Danas (WL's demon portal).
    new Edge(1848656454, 1848656456), // Antoran Wastes <-> Eredath
    new Edge(1848656454, 1848656455), // Antoran Wastes <-> Krokuun
    new Edge(1848656454, 1848652609), // Antoran Wastes <-> Nath'raxas Hold
    new Edge(1848656454, 1848652370, 0f, "prelegion"), // Antoran Wastes <-> Crystalsong Forest (portal, until the summon)
    new Edge(1848656454, 1848652622, 0f, "legion"),    // Antoran Wastes <-> Quel'Danas (demon portal, after the summon)
  };

  // Placeholder edge sets for the other theatres. Fill these as the node system is extended; until then a
  // faction switched onto them simply falls back to the generic nearest-CP picker (empty -> null).
  // KALIMDOR: one connected graph. Kalimdor-native factions resolve onto this set at runtime (by which
  // graph holds the CPs they own). The Legion nether (Argus) edges are gated behind the "legion" flag,
  // opened at the summon, so the nether only joins the Kalimdor graph once Archimonde is called.
  public static readonly List<Edge> Kalimdor = new()
  {
    new Edge(1848652112, 1848656457), // Nordrassil <-> Shrine to Malorne
    new Edge(1848656457, 1848653635), // Shrine to Malorne <-> Felwood
    new Edge(1848653635, 1848652357), // Felwood <-> Moonglade
    new Edge(1848656457, 1848656176), // Shrine to Malorne <-> Ascendant's Rise
    new Edge(1848656176, 1848652120), // Ascendant's Rise <-> Winterspring
    new Edge(1848652120, 1848652115), // Winterspring <-> Azshara Coast
    new Edge(1848652115, 1848654162), // Azshara Coast <-> Eldarath
    new Edge(1848654162, 1848653648), // Eldarath <-> Orgrimmar
    new Edge(1848653648, 1848652634), // Orgrimmar <-> Durotar
    new Edge(1848652634, 1848652374), // Durotar <-> Echo Isles
    new Edge(1848652374, 1848654147), // Echo Isles <-> Alcaz Isle
    new Edge(1848654147, 1848656198), // Alcaz Isle <-> Dustwallow Marsh
    new Edge(1848656198, 1848652338), // Dustwallow Marsh <-> Stonemaul
    new Edge(1848654162, 1848653909), // Eldarath <-> Southern Ashenvale
    new Edge(1848653909, 1848652116), // Southern Ashenvale <-> Northern Barrens
    new Edge(1848652116, 1848652353), // Northern Barrens <-> Southern Barrens
    new Edge(1848652353, 1848654151), // Southern Barrens <-> Mulgore
    new Edge(1848654151, 1848652621), // Mulgore <-> Thunderbluff
    new Edge(1848652353, 1848651857), // Southern Barrens <-> Spinebark Grove
    new Edge(1848651857, 1848653393), // Spinebark Grove <-> Dire Maul
    new Edge(1848653393, 1848653141), // Dire Maul <-> Feathermoon
    new Edge(1848653141, 1848653127), // Feathermoon <-> Isle of Dread
    new Edge(1848653127, 1851021132), // Isle of Dread <-> Twilight Landing
    new Edge(1851021132, 1850636641), // Twilight Landing <-> Ny'alotha
    new Edge(1851021132, 1850560327), // Twilight Landing <-> Maw of Gor'ma
    new Edge(1848653909, 1848652114), // Southern Ashenvale <-> Astranaar
    new Edge(1848652114, 1848652113), // Astranaar <-> Northern Ashenvale
    new Edge(1848652113, 1848653635), // Northern Ashenvale <-> Felwood
    new Edge(1848652113, 1848652373), // Northern Ashenvale <-> Darkshore
    new Edge(1848652373, 1848653382), // Darkshore <-> Silvermyst Island
    new Edge(1848653382, 1848656460), // Silvermyst Island <-> Exodar Regalis
    new Edge(1848653382, 1848652378), // Silvermyst Island <-> Azuremyst Isle
    new Edge(1848652373, 1848654148), // Darkshore <-> Auberdine
    new Edge(1848654148, 1848652377), // Auberdine <-> Teldrassil
    new Edge(1848652373, 1848653364), // Darkshore <-> Grove of the Ancients
    new Edge(1848653364, 1848652114), // Grove of the Ancients <-> Astranaar
    new Edge(1848652114, 1848653134), // Astranaar <-> Windshear Crossing
    new Edge(1848653134, 1848652117), // Windshear Crossing <-> Stonetalon Peak
    new Edge(1848652117, 1848652121), // Stonetalon Peak <-> Desolace
    new Edge(1848652121, 1848652623), // Desolace <-> Ranazjar Isle
    new Edge(1848652121, 1848652343), // Desolace <-> Feralas
    new Edge(1848652343, 1848653393), // Feralas <-> Dire Maul
    new Edge(1848656182, 1848652116), // Ratchet <-> Northern Barrens
    new Edge(1848656182, 1848652353), // Ratchet <-> Southern Barrens
    new Edge(1848656182, 1848656198), // Ratchet <-> Dustwallow Marsh
    new Edge(1848656182, 1848653393), // Ratchet <-> Dire Maul
    new Edge(1848656182, 1848651857), // Ratchet <-> Spinebark Grove
    new Edge(1848651857, 1848652342), // Spinebark Grove <-> Thousand Needles (v30: n026 was missing)
    new Edge(1848652342, 1848656462), // Thousand Needles <-> Shimmering Flats (v30: replaces Spinebark <-> Shimmering Flats)
    new Edge(1848656462, 1848653123), // Shimmering Flats <-> Gadgetzan
    new Edge(1848656462, 1848654130), // Shimmering Flats <-> Zul'Farrak
    new Edge(1848654130, 1848652336), // Zul'Farrak <-> Tanaris
    new Edge(1848652336, 1848656459), // Tanaris <-> Lost City of the Tol'vir
    new Edge(1848656459, 1851152464), // Lost City of the Tol'vir <-> The Vortex Pinnacle
    new Edge(1848652336, 1848656452), // Tanaris <-> Uldum
    new Edge(1848656452, 1851016791), // Uldum <-> Throne of the Four Winds
    new Edge(1848652336, 1848652341), // Tanaris <-> Un'Goro Crater
    new Edge(1848652341, 1848651857), // Un'Goro Crater <-> Spinebark Grove
    new Edge(1848651857, 1848652855), // Spinebark Grove <-> Silithus
    new Edge(1848652855, 1848652363), // Silithus <-> Ruins of Ahn'Qiraj
    new Edge(1848652363, 1848651851), // Ruins of Ahn'Qiraj <-> Tunnels of Ahn'Qiraj
    new Edge(1848651851, 1850495813), // Tunnels of Ahn'Qiraj <-> Temple of Ahn'Qiraj
    new Edge(1848652855, 1848653127), // Silithus <-> Isle of Dread
    new Edge(1848656454, 1848653635, 0f, "legion"), // Antoran Wastes <-> Felwood (portal, opens on Legion summon)

    // ---- v34: Kalimdor islands (ship portals in AiGraph.Portals) ----
    new Edge(1848653107, 1848653145), // Val'sharah <-> Azsuna
    new Edge(1848653107, 1848652594), // Val'sharah <-> Suramar
    new Edge(1848652594, 1848653146), // Suramar <-> Stormheim
    new Edge(1848653145, 1848652115), // Azsuna <-> Azshara Coast (ship)
    new Edge(1848651856, 1848652344), // The Abyss <-> Maelstrom
    new Edge(1848654162, 1848651856), // Eldarath <-> The Abyss (ship)
    new Edge(1848652374, 1848652889), // Echo Isles <-> Nazjatar (ship)
    new Edge(1848653104, 1848656184), // Zuldazar <-> Nazmir
    new Edge(1848656184, 1848653123), // Nazmir <-> Gadgetzan (ship)
    new Edge(1848656184, 1848652890), // Nazmir <-> Kezan (ship -- v34.2)
  };

  // v34: Argus nodes (Legion home). Listed in Combined, but never handed to the Scourge at the betrayal.
  public static readonly HashSet<int> ArgusNodes = new()
  {
    1848656454, // n0BF Antoran Wastes
    1848656456, // Eredath
    1848656455, // Krokuun
    1848652609, // n03A Nath'raxas Hold
  };

  // v34: physical portals (real waygates -- everyone can use them). Created at game start ("always",
  // "prelegion") or at the Legion summon ("legion"); "prelegion" ones are removed at the summon. A/B order is
  // free; each end lands its travellers on open ground beside the OTHER end. Skin = unit type whose model the
  // waygate shows (ships: "hbot" transport ship; "" = keep the Dark Portal model). Names show on mouse-over.
  public static readonly List<PortalLink> Portals = new()
  {
    new PortalLink(-2391f, 3837f, -4476f, 4880f, "hbot", "Ship to Azshara Coast", "Ship to Azsuna", "always"),
    new PortalLink(-6362f, 2171f, -4256f, 2144f, "hbot", "Ship to The Abyss", "Ship to Eldarath", "always"),
    new PortalLink(-7617f, -4539f, -4998f, -4379f, "hbot", "Ship to Nazjatar", "Ship to Echo Isles", "always"),
    new PortalLink(-4931f, -12031f, -7726f, -14142f, "hbot", "Ship to Gadgetzan", "Ship to Nazmir", "always"),
    new PortalLink(6507f, -12298f, 7869f, -10014f, "hbot", "Ship to Stormwind", "Ship to Kul Tiras", "always"),
    // v34.1: Balor <-> Western Stranglethorn. Given as x-4853 y-14342; that point is open sea near Zandalar,
    // while x+4853 y-14342 is walkable on the Kul Tiras island 2000 from Balor -> sign typo assumed.
    new PortalLink(4853f, -14342f, 8447f, -18608f, "hbot", "Ship to Stranglethorn", "Ship to Balor", "always"),
    // v34.2: Nazmir <-> Kezan. Given as x-1201 y9903 for the Kezan side; that is the Broken Isles, while
    // x-1201 y-9903 is walkable on Kezan's own island (same ground area as the Kezan CP) -> sign typo assumed.
    new PortalLink(-1843f, -11951f, -1201f, -9903f, "hbot", "Ship to Kezan", "Ship to Nazmir", "always"),
    new PortalLink(AiWorld.LegionArgusPortalX, AiWorld.LegionArgusPortalY,
      AiWorld.LegionNorthrendPortalX, AiWorld.LegionNorthrendPortalY, "", "Portal to Northrend", "Portal to Argus", "prelegion"),
    new PortalLink(AiWorld.LegionArgusPortalX, AiWorld.LegionArgusPortalY,
      AiWorld.LegionExitX, AiWorld.LegionExitY, "", "Portal to Felwood", "Portal to Argus", "legion"),
  };

  // v34: Kul Tiras (Combined), Broken Isles, Zandalar, Nazjatar, The Abyss, Maelstrom (Kalimdor) are wired via
  // ship portals now. Still NOT wired: Tomb of Sargeras 1848651850, Kezan 1848652890, Tol Barad 1848653905
  //   Unresolved       : Highbank 1848651853 (island), Balor 1848653644, Whipping Wind 1848653914

  public static readonly List<Edge> Outland = new();

  // The node system is per-faction continent-STATE now: an event (invasion arrival, Legion summon) writes a
  // faction's current edge-set name, and the walker reads whichever set is named. No time-gate, no fallback
  // to "non-node" -- a faction is only ever on ONE continent's edges at a time.
  // Gate unit-types (closed AND open variants of every wall-gate family in the map). Bot-owned gates of these
  // exact types are removed so a bot can never wall its own army in. This is a strict id set -- the "DEMON_GATE"
  // unit spawners and the Dark Portal Waygate are deliberately NOT here, so nothing but real gates is touched.
  public static readonly List<int> GateTypes = new()
  {
    // WL custom gates use LOWERCASE-h codes (confirmed via -id: h01W 'Elven Gate' = 1747988823). These are the
    // ones actually on the map; the uppercase-H stock codes below are kept too in case any gate uses them.
    1747988555, // h00K Horizontal Wooden (closed)
    1747988556, // h00L Horizontal Wooden (open)
    1747988823, // h01W Elven (closed)  <- confirmed
    1747988824, // h01X Elven (open)
    1747989064, // h02H Stormwind Harbour (closed)
    1747989066, // h02J Stormwind Harbour (open)
    1747989067, // h02K Greymane's (closed)
    1747989069, // h02M Greymane's (open)
    1747989077, // h02U Ahn'Qiraj (closed)
    1747989075, // h02S Ahn'Qiraj (open)
    1747989587, // h04S Diagonal Wooden (closed)
    1747989586, // h04R Diagonal Wooden (open)
    // uppercase-H stock variants (fallback)
    1211117643, // H00K
    1211117644, // H00L
    1211117911, // H01W
    1211117912, // H01X
    1211118152, // H02H
    1211118154, // H02J
    1211118155, // H02K
    1211118157, // H02M
    1211118165, // H02U
    1211118163, // H02S
    1211118675, // H04S
    1211118674, // H04R
  };

  // Destructible GATE doodads (the Gate_LTg / ElvenGate_LTe / etc. orientations -- these are destructibles,
  // NOT units, which is why "Elven Gate" / "Horizontal Wooden Gate" survived the unit sweep). Killed once to
  // open every chokepoint. Bridges (YT00-YT09, LT08) and tree/stone walls are NOT here -- only openable gates.
  // NOTE: these are big-endian FourCC (what the runtime GetDestructableTypeId returns), NOT the byte-reversed
  // values from the War3Api.Object DestructableType enum -- using the enum values here matched nothing.
  public static readonly List<int> GateDestructibles = new()
  {
    1280599857, 1280599858, 1280599859, 1280599860, // Gate_LTg1..4 (wooden gate orientations)
    1280599345, 1280599346, 1280599347, 1280599348, // ElvenGate_LTe1..4
    1096050481, 1096050482, 1096050483, 1096050484, // DemonicGate_ATg1..4
    1146382133, 1146382134, 1146382135, 1146382136, // IronGate_DTg5..8
    1146382129, 1146382130, 1146382131, 1146382132, // DungeonGate_DTg1..4
    1146381105, 1146381106,                         // CliffCaveGate_DTc1..2
  };

  // Nodes the walker passes through but must never capture (so a scripted event can still fire there).
  public static readonly List<int> SkipNodes = new()
  {
    1848652340, // Blackrock Depths -- leave uncaptured so Ragnaros can trigger
  };

  // One-shot objectives: off-path points a bot visits ONCE (destroy/clear) when it owns a nearby CP, then
  // never again. Not graph nodes -- evaluated on the slow wave tick, no scanning. Exclude = faction names
  // that must NOT trigger it (e.g. the Alliance won't smash elven runestones).
  public sealed class OneShot
  {
    public readonly float X;
    public readonly float Y;
    public readonly int NearCpId;              // trigger when a non-excluded bot owns this CP
    public readonly float NearRadius;          // ...or owns any CP within this radius of (X,Y) if NearCpId==0
    public readonly List<string> Exclude;

    public OneShot(float x, float y, int nearCpId, float nearRadius, List<string> exclude)
    {
      X = x; Y = y; NearCpId = nearCpId; NearRadius = nearRadius; Exclude = exclude;
    }
  }

  private static readonly List<string> AllianceFactions = new()
  {
    // v28: exact faction.Name strings -- was "Quel'Thalas" (capital T), which never matched, so a Quel'thalas bot
    // counted as non-Alliance and smashed its own runestones. ("Wildhammer" isn't a WL faction; removed.)
    "Lordaeron", "Stormwind", "Dalaran", "Kul'tiras", "Gilneas", "Ironforge", "Quel'thalas"
  };

  public static readonly List<OneShot> OneShots = new()
  {
    // Elven runestones near Quel'Thalas: destroyed by anyone NOT of the Alliance (one-shot each).
    new OneShot(17408f, 13184f, 0, 5000f, AllianceFactions),
    new OneShot(20480f, 17472f, 0, 5000f, AllianceFactions),
    // (v31: the Sapphiron xy test moved to AiConfig.KillTargets -- found by unit type 'ubdr', hunted until dead.)
  };

  // v34: Combined + Kalimdor as one list, built once (the merge EVENT happens at the Legion summon).
  private static List<Edge> _all;

  public static List<Edge> AllEdges()
  {
    if (_all == null)
    {
      _all = new List<Edge>();
      _all.AddRange(Combined);
      _all.AddRange(Kalimdor);
    }

    return _all;
  }

  public static List<Edge> EdgesByName(string name)
  {
    switch (name)
    {
      case "Combined": return Combined;
      case "Northrend": return Combined;        // aliases: the continent is one graph now
      case "EasternKingdoms": return Combined;
      case "Kalimdor": return Kalimdor;
      case "Outland": return Combined;          // Outland is reached via the Combined graph's portal edges
      case "All": return AllEdges();            // v34: merged world graph (after the Legion summon)
      default: return null;
    }
  }

  // The edge set a faction starts on. Scourge and Legion both open on the Combined graph; the 10-min bridges
  // in that graph gate their advance south naturally, so no edge-set swap is needed pre-summon.
  public static string DefaultEdgeSet(string factionName, float clockSec)
  {
    if (factionName == "Scourge" || factionName == "Legion")
    {
      return "Combined";
    }

    return null;
  }

  // Back-compat: kept for any caller still asking for edges by clock.
  public static List<Edge> ActiveEdges(string factionName, float clockSec)
    => EdgesByName(DefaultEdgeSet(factionName, clockSec));

  public static readonly List<NodeEvent> Events = new()
  {
    new NodeEvent(1848651825, "Scourge", "legion", ""), // The Sunwell -> summon Legion when Scourge takes it
    new NodeEvent(1848653625, "Scourge", "legion", ""), // v34: ... or the corrupted Sunwell (both are CAPITALS)
  };
}

public sealed class PortalLink
{
  public readonly float AX;
  public readonly float AY;
  public readonly float BX;
  public readonly float BY;
  public readonly string Skin;   // 4-char unit type code whose model the waygate shows; "" = default model
  public readonly string NameA;  // mouse-over name of the A end
  public readonly string NameB;
  public readonly string Phase;  // "always" | "prelegion" (removed at the summon) | "legion" (created at it)

  public PortalLink(float ax, float ay, float bx, float by, string skin, string nameA, string nameB, string phase)
  {
    AX = ax;
    AY = ay;
    BX = bx;
    BY = by;
    Skin = skin;
    NameA = nameA;
    NameB = nameB;
    Phase = phase;
  }
}

public sealed class NodeEvent
{
  public readonly int NodeId;
  public readonly string ByFaction;  // faction.Name that must own the node to trigger
  public readonly string Kind;       // "legion" | "chat"
  public readonly string Text;       // for "chat"

  public NodeEvent(int nodeId, string byFaction, string kind, string text)
  {
    NodeId = nodeId;
    ByFaction = byFaction;
    Kind = kind;
    Text = text;
  }
}

// ---------------------------------------------------------------------------------------------------------
//  REINFORCEMENT WAVES  (a faction group that dominates its home continent ships fresh units to a new front)
//  Active only while the group holds >= Threshold of the control points on its HOME landmass (defined by
//  HomeCpId). That gate IS the stop/restart: lose the home continent -> waves pause; retake it -> they resume.
//  Every IntervalSec, units of the group that are STILL on the home landmass teleport to the destination
//  (reinforcements only -- units already at the front are left alone). Dest = DestCpId, or DestX/DestY if 0.
// ---------------------------------------------------------------------------------------------------------
public sealed class DestPoint
{
  public readonly int CpId; // resolve to this control point's position; 0 -> use X/Y
  public readonly float X;
  public readonly float Y;

  public DestPoint(int cpId, float x, float y)
  {
    CpId = cpId;
    X = x;
    Y = y;
  }
}

// A staged invasion: prep message -> units gather at the staging point (GatherSec) -> portals open at both
// ends (ChannelSec) -> the assembled units jump to one RANDOM destination. Near-zero cost: two effects, a few
// pings, and orders. Active only while the group holds Threshold of its home landmass AND (if set) TriggerCpId.
public sealed class WaveConfig
{
  public readonly List<string> Factions;
  public readonly int HomeCpId;      // defines the home landmass + the %-gate CP set
  public readonly int TriggerCpId;   // if != 0, must also be owned (AND) for the wave to be active
  public readonly float Threshold;
  public readonly float GatherX;     // staging point on the home continent (0,0 -> home CP position)
  public readonly float GatherY;
  public readonly List<DestPoint> Dests; // one is chosen at random each invasion
  public readonly string PortalModel;    // effect shown at both ends (a portal, or a ship for naval)
  public readonly float GatherSec;   // time units get to assemble
  public readonly float ChannelSec;  // portal channel time before the jump
  public readonly float IntervalSec; // cooldown between invasions
  public readonly string PrepMsg;
  public readonly string ArriveMsg;
  public readonly string StopMsg;
  public readonly string DestEdgeSet; // v24: node set the group adopts on arrival ("" -> leave state unchanged)

  public WaveConfig(List<string> factions, int homeCpId, int triggerCpId, float threshold, float gatherX,
    float gatherY, List<DestPoint> dests, string portalModel, float gatherSec, float channelSec,
    float intervalSec, string prepMsg, string arriveMsg, string stopMsg, string destEdgeSet = "")
  {
    Factions = factions;
    HomeCpId = homeCpId;
    TriggerCpId = triggerCpId;
    Threshold = threshold;
    GatherX = gatherX;
    GatherY = gatherY;
    Dests = dests;
    PortalModel = portalModel;
    GatherSec = gatherSec;
    ChannelSec = channelSec;
    IntervalSec = intervalSec;
    PrepMsg = prepMsg;
    ArriveMsg = arriveMsg;
    StopMsg = stopMsg;
    DestEdgeSet = destEdgeSet;
  }
}

public static class AiWaves
{
  // One confirmed-present asset for every wave's "gate opens" beat (no ship model exists in the map).
  private const string PortalFx = AiWorld.TeleportFx;
  private const string ShipFx = AiWorld.TeleportFx;

  public static readonly List<WaveConfig> All = new()
  {
    // OLD GODS: hold 60% of Kalimdor AND Nordrassil -> gather on Kalimdor (2 min) -> jump to a random
    // Eastern Kingdoms foothold. Home = Durotar (main Kalimdor landmass).
    new WaveConfig(new List<string> { "Ahn'qiraj", "Black Empire", "Skywall" },
      1848652634, 1848652112, 0.6f, 0f, 0f,
      new List<DestPoint>
      {
        new DestPoint(1848652886, 0f, 0f), // Northern Highlands
        new DestPoint(1848652122, 0f, 0f), // Arathi Highlands
        new DestPoint(1848652107, 0f, 0f), // Stromgarde
      },
      PortalFx, 120f, 6f, 600f,
      "|cffaa00ffThe Old Gods stir — something gathers in the dark...|r",
      "|cffaa00ffThe Old Gods spill across the sea! (see the ping)|r",
      "|cffaa00ffThe Old Gods' tide recedes from Kalimdor.|r",
      "Combined"),

    // KUL TIRAS: must hold the WHOLE island, then a fast ship-borne push to the mainland. Set GatherX/Y to a
    // point by their island's coast via -xy; add more DestPoints for variety.
    new WaveConfig(new List<string> { "Kul'tiras" },
      1848652119, 0, 1.0f, 4672f, -7424f,
      new List<DestPoint>
      {
        new DestPoint(0, 7056f, -15270f), // mainland coast landing
      },
      ShipFx, 60f, 4f, 120f,
      "|cff66ccffKul Tiras readies its fleets...|r",
      "|cff66ccffKul Tiras makes landfall on the mainland!|r",
      "|cff66ccffKul Tiras' fleets are recalled.|r",
      "Combined"),
  };
}

public sealed class DoctrineStep
{
  public readonly float X;
  public readonly float Y;
  public readonly float AfterSec;

  public DoctrineStep(float x, float y, float afterSec)
  {
    X = x;
    Y = y;
    AfterSec = afterSec;
  }
}
