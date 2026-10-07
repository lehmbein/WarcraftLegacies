using System.Collections.Generic;

namespace WarcraftLegacies.Source.Ai;

// AUTO-DERIVED per-faction upgrade order (caster abilities + tech; combat weapon/armor handled separately).
// v33: ByFaction (+ Extra) is the UNIT/ABILITY tech pool -- see "TECH POOLS" below. Entries that are also in
// Events or NeverGrant are skipped (handled by their own rules). Ship/naval upgrades are placed LAST. Reorder freely;
// markers (quest/power/transform) and ambiguous tags were excluded. Re-extract from source after WL updates.
public static class AiUpgrades
{
  public static readonly Dictionary<string, List<int>> ByFaction = new()
  {
    { "Ahn'qiraj", new List<int>
      {
        1514287410, // Cleaving Attack
        1380205124, // Deep Burrow
        1514292552, // Defensive Cocooon
        1514287412, // Elongated Snouts
        1381256243, // Improved Seed of Madness
        1381586741, // Improved Swarm Beetle
        1378889779, // Progenesis
        1514295881, // Rapid Incubation
        1381642290, // Shadow Weaver Adept Training
        1514293331, // Shaped Obsidian
        1514294604, // Spell Conduction
        1380725041, // Tol'vir Statue Adept Training
        1381183537, // Tunneler Adept Training
      }
    },
    { "Black Empire", new List<int>
      {
        1380074817, // Accelerated Cycle
        1380075075, // Cursed Flesh
        1380074822, // Fateweaver Adept Training
        1380074824, // Herald Adept Training
        1380074838, // Howl of Terror
        1380074829, // Mindlasher Adept Training
        1380074832, // Paralysing Fear
        1380074835, // Unworldly Scythe
      }
    },
    { "Dalaran", new List<int>
      {
        1378890050, // A Treatise on Barriers
        1378890057, // Arcanist Adept Training
        1378894135, // Escape to Theramore
        1378890070, // Geomancer Adept Training
        1378890805, // Hard Crystal Constructs
        1378889797, // Hydromancer Adept Training
        1378891313, // Improved Forked Lightning
        1378891343, // Improved Phase Blade
        1378891338, // Improved Slow
        1378890573, // Methods of Negation
        1378890288, // Rain: An Amalgam
      }
    },
    { "Druids", new List<int>
      {
        1378891096, // Blessing of Ursol
        1378890807, // Crippling Poison
        1378890808, // Deadly Poison
        1382376564, // Druid of the Talon Advanced Training
        1378890311, // Emerald Flames
        1378890806, // Grasping Vines
        1378890037, // Improved Mana Flare
        1378890324, // Improved Moonwells
        1378889793, // Improved Thorns
        1378892118, // Storm Crow Form
        1378890821, // Ysera's Gift
      }
    },
    { "Fel Horde", new List<int>
      {
        1378892080, // Activate the Blackrock Clan
        1378890575, // Blood Runes
        1383031398, // Demonic Flux
        1378890548, // Enhanced Breath
        1378890569, // Eredar Occultist Adept Training
        1378892088, // Fel Infused Skeleton
        1378889805, // Fel Warlock Adept Training
        1378892119, // Felsteel Refining
        1378890549, // Improved Firebolt
        1378889817, // Improved Frenzy
        1378890572, // Improved Heal
        1378890550, // Incinerate
        1378890292, // Necrolyte Adept Training
        1378890307, // The Dark Portal
      }
    },
    { "Frostwolf", new List<int>
      {
        1383033958, // Airborne Toxins
        1378889810, // Improved Chain Lightning
        1383036787, // Improved Pulverize
        1378889796, // Mass Bloodlust
        1378889815, // Toughened Hides
      }
    },
    { "Gilneas", new List<int>
      {
        1378890831, // Cleric Adept Training
        1378890832, // Druid of the Scythe Adept Training
        1378892109, // Harvest-Witch Adept Training
      }
    },
    { "Illidan", new List<int>
      {
        1514289484, // Coilfang Summoners
        1381582158, // Ensnare
        1378889800, // Feedback
        1381581908, // Heavy Boulders
        1381585744, // Improved Slow Poison
        1497518152, // Kingslayer
        1514296137, // Sea Witches
        1378890039, // Siphon Mana
        1497514305, // Split Healing Wave
      }
    },
    { "Ironforge", new List<int>
      {
        1378889818, // Armor Penetration Rounds
        1414541881, // Artillery
        1378890036, // Deeprun Tram
        1414541880, // Flamethrower
        1378890315, // Gryphon Superior Breed
        1414542136, // Improved Chassis
        1378890032, // Improved Spell Resistance
        1378889806, // Improved Swig
        1448096561, // Masters of Lightning
        1378889798, // Mithril Plated Armor
        1378889812, // Overclock
        1414542385, // Potent Stormbrew
        1378889814, // Rune Priest Adept Training
        1414542386, // Rune Smith Adept Training
      }
    },
    { "Kul'tiras", new List<int>
      {
        1378890068, // Cluster Rockets
        1378890063, // Crushing Wave
        1378889777, // Rising Tides
        1378891079, // Thornspeaker Adept Training
        1378889776, // Tidesage Adept Training
      }
    },
    { "Legion", new List<int>
      {
        1378890073, // Astral Walk
        1378890295, // Eredar Summoner Adept Training
        1378890823, // Improved Carrion Swarm
        1378890802, // Nathrezim Warlock Adept Training
        1378892086, // Rematerialization
        1378890296, // Succubis Adept Training
        1378890586, // War Plating
      }
    },
    { "Lordaeron", new List<int>
      {
        1378890053, // Aerial Shackles
        1378890062, // Arathorian Legacy
        1378889801, // Mage Adept Training
        1378890819, // Minister Initiate Training
        1378891334, // Northrend Expedition
        1378890817, // Rapid Fire
        1378890820, // Seal of Righteousness
        1378889794, // Veteran Footmen
      }
    },
    { "Quel'thalas", new List<int>
      {
        1378890066, // Enchanted Bowstrings
        1378889799, // Feint
        1378890329, // Improved Glaives
        1378890297, // Magus Adept Training
        1378890067, // Rapid Shots
        1378889780, // Sunfury Training
      }
    },
    { "Scarlet Crusade", new List<int>
      {
        1378890822, // Inquisitor Initiate Training
        1378891076, // Monk Adept Training
        1378900058, // The Scarlet Crusade
      }
    },
    { "Scourge", new List<int>
      {
        1378889809, // Chilling Aura
        1514287668, // Death Knights
        1378890072, // Epidemic
        1378890052, // Howl of Terror
        1378891342, // Improved Orb of Annihilation
        1514288948, // Liches
        1383427685, // Necromancer Grandmaster Training
        1378891337, // Plague of Undeath
      }
    },
    { "Sentinels", new List<int>
      {
        1383032174, // Improved Ensnare
        1378890040, // Improved Lightning Barrage
        1378890071, // Moonhunter Adept Training
        1378889811, // Priestess Adept Training
        1378891316, // Sentinel Fortifications
        1378890570, // Wind Walk
      }
    },
    { "Skywall", new List<int>
      {
        1381193525, // Fireland Invasion
        1380273217, // Infused Steel
        1380273228, // Lurking Tempest Adept Training
        1380273232, // Shocking Blades
        1380273231, // Tidal Lord Adept Training
        1380273239, // Whipping Wind Adept Training
        1380273236, // Windforging
      }
    },
    { "Sunfury", new List<int>
      {
        1378892104, // Astromancer Adept Training
        1378892103, // Flamekeeper Adept Training
        1378892117, // Seal of Blood
      }
    },
    { "The Exodar", new List<int>
      {
        1380200500, // Aligned Crystal Mechanism
        1380200502, // Azerothian Husbandry
        1380200497, // Crystal Shields
        1378891576, // Elementalist Adept Training
        1380200505, // Endless Energy
        1380200503, // Improved Crystal Discharge
        1380200498, // Kalimdor Wilds Acclimatization
        1378891587, // Luminarch Adept Training
        1380200752, // Naaru's Shield
        1380200501, // Protector of Kings
        1380200504, // Restore Mana
      }
    },
    { "Warsong", new List<int>
      {
        1378891087, // Build Orgrimmar
        1378892110, // Flight Path
        1378890058, // Improved Ensnare
        1414541623, // Improved Frenzy
        1378890041, // Improved Shockwave
        1378891335, // Northrend Expedition
        1378889802, // Ogre Magi Adept Training
        1378890577, // Shadowpriest Adept Training
        1378890313, // Warlock Adept Training
      }
    },
  };

  // v33: TECH POOLS (replaces v32's caster schedule). Combat weapon/armor (AiConfig.CombatUpgrades) is untouched.
  //  * UNIT / ABILITY pool = ByFaction (casters + abilities) + Extra below. Granted step by step: first every
  //    upgrade at level 1 (list order), then every multi-level one at level 2, then level 3. Steps unlocked =
  //    1 + one per 5 min (the old timer) + one per 2 control points captured (retakes count).
  //  * BUILDING / MISC pool = BuildingUpgrades (town halls first, then other buildings) and MiscAll. One step per
  //    2 control points captured, nothing on a timer.
  // Extra = researches found at each faction's buildings in the 4.8.2 map that the auto-derived list missed
  // (stock abilities such as Ghoul Frenzy, Animal War Training, ...). Quest-gated ones (requirement = another
  // research, e.g. "Quest Completed: ...") are NOT included -- quests stay the way to get those.
  public static readonly Dictionary<string, List<int>> Extra = new()
  {
    { "Ahn'qiraj", new List<int>
      {
        1380469817, // RHL9 Web (Cthun)
      }
    },
    { "Druids", new List<int>
      {
        1382376547, // Redc Druid of the Claw Training
        1382380918, // Reuv Ultravision
      }
    },
    { "Fel Horde", new List<int>
      {
        1378891318, // R066 Burning Oil
        1383035760, // Rosp Spiked Barricades
        1383035490, // Rorb Reinforced Defenses
      }
    },
    { "Frostwolf", new List<int>
      {
        1383035760, // Rosp Spiked Barricades
        1383035490, // Rorb Reinforced Defenses
        1383036788, // Rowt Spirit Walker Training
        1383035764, // Rost Shaman Training
        1383034983, // Ropg Pillage
      }
    },
    { "Illidan", new List<int>
      {
        1378890326, // R02V Shadowcaster Adept Training
        1382970231, // Rnsw Siren Master Training
      }
    },
    { "Ironforge", new List<int>
      {
        1382574178, // Rhhb Storm Hammers
        1382573676, // Rhfl Flare
        1382573683, // Rhfs Fragmentation Shards
      }
    },
    { "Kul'tiras", new List<int>
      {
        1378891842, // R08B Long Rifles
      }
    },
    { "Lordaeron", new List<int>
      {
        1378890064, // R01P Ensnare
        1382572398, // Rhan Animal War Training
        1382576997, // Rhse Magic Sentry
        1378890309, // R02E Light's Praise Adept Training
      }
    },
    { "Quel'thalas", new List<int>
      {
        1382576244, // Rhpt Priest Training
        1382577012, // Rhst Sorceress Training
        1382577011, // Rhss Control Magic
        1382572900, // Rhcd Cloud
      }
    },
    { "Scourge", new List<int>
      {
        1383425634, // Rufb Freezing Breath
        1383425894, // Rugf Ghoul Frenzy
        1383429986, // Ruwb Web
        1383425400, // Ruex Exhume Corpses
        1383428972, // Rusl Skeletal Longevity
        1383428973, // Rusm Skeletal Mastery
        1383424609, // Ruba Banshee Grandmaster Training
      }
    },
    { "Sentinels", new List<int>
      {
        1382381427, // Rews Well Spring
        1382380918, // Reuv Ultravision
        1382377826, // Reib Improved Bows
      }
    },
    { "Skywall", new List<int>
      {
        1380274007, // REOW Offensive Wishes
        1380273223, // RELG Inspiring Gifts
        1380273218, // RELB Bursting Miracles
        1380273235, // RELS Magma Heart
        1380273220, // RELD Magma Fire
      }
    },
    { "Stormwind", new List<int>
      {
        1378889781, // R005 Clergyman Adept Training
        1378890309, // R02E Light's Praise Adept Training
        1378890580, // R03T Electric Strike Ritual
        1378890581, // R03U Solar Flare Ritual
        1378890582, // R03V Mages of Stromgarde
        1378890583, // R03W Knowledge of Honor Hold
        1378890584, // R03X Conjurers
        1378890330, // R02Z Reflective Plating
        1378890564, // R03D Veteran Guard
        1378890306, // R02B Chainmail Layering
        1382572398, // Rhan Animal War Training
      }
    },
    { "Warsong", new List<int>
      {
        1383035760, // Rosp Spiked Barricades
        1383035490, // Rorb Reinforced Defenses
        1383031411, // Robs Berserker Strength
        1383036531, // Rovs Envenomed Spears
      }
    },
  };

  // Max level of every pool upgrade with more than one level (map value, else the stock base upgrade's).
  public static readonly Dictionary<int, int> MaxLevel = new()
  {
    { 1378889776, 2 }, // Tidesage Adept Training
    { 1378889781, 2 }, // Clergyman Adept Training
    { 1378889797, 3 }, // Hydromancer Adept Training
    { 1378889801, 2 }, // Mage Adept Training
    { 1378889802, 2 }, // Ogre Magi Adept Training
    { 1378889805, 2 }, // Fel Warlock Adept Training
    { 1378889811, 2 }, // Priestess Adept Training
    { 1378889814, 2 }, // Rune Priest Adept Training
    { 1378890057, 3 }, // Arcanist Adept Training
    { 1378890070, 3 }, // Geomancer Adept Training
    { 1378890071, 2 }, // Moonhunter Adept Training
    { 1378890292, 2 }, // Necrolyte Adept Training
    { 1378890295, 2 }, // Eredar Summoner Adept Training
    { 1378890296, 2 }, // Succubis Adept Training
    { 1378890297, 2 }, // Magus Adept Training
    { 1378890309, 2 }, // Light's Praise Adept Training
    { 1378890313, 2 }, // Warlock Adept Training
    { 1378890326, 2 }, // Shadowcaster Adept Training
    { 1378890569, 2 }, // Eredar Occultist Adept Training
    { 1378890575, 3 }, // Blood Runes
    { 1378890577, 2 }, // Shadowpriest Adept Training
    { 1378890802, 2 }, // Nathrezim Warlock Adept Training
    { 1378890819, 2 }, // Minister Initiate Training
    { 1378890822, 2 }, // Inquisitor Initiate Training
    { 1378890823, 2 }, // Improved Carrion Swarm
    { 1378890831, 2 }, // Cleric Adept Training
    { 1378890832, 2 }, // Druid of the Scythe Adept Training
    { 1378891076, 2 }, // Monk Adept Training
    { 1378891079, 2 }, // Thornspeaker Adept Training
    { 1378891316, 2 }, // Sentinel Fortifications
    { 1378891576, 2 }, // Elementalist Adept Training
    { 1378891587, 2 }, // Luminarch Adept Training
    { 1378892103, 2 }, // Flamekeeper Adept Training
    { 1378892104, 2 }, // Astromancer Adept Training
    { 1378892109, 2 }, // Harvest-Witch Adept Training
    { 1380074822, 2 }, // Fateweaver Adept Training
    { 1380074824, 2 }, // Herald Adept Training
    { 1380074829, 2 }, // Mindlasher Adept Training
    { 1380273228, 2 }, // Lurking Tempest Adept Training
    { 1380273231, 2 }, // Tidal Lord Adept Training
    { 1380273239, 2 }, // Whipping Wind Adept Training
    { 1380725041, 2 }, // Tol'vir Statue Adept Training
    { 1381183537, 2 }, // Tunneler Adept Training
    { 1381642290, 2 }, // Shadow Weaver Adept Training
    { 1382376547, 2 }, // Druid of the Claw Training
    { 1382376564, 2 }, // Druid of the Talon Advanced Training
    { 1382576244, 2 }, // Priest Training
    { 1382577012, 2 }, // Sorceress Training
    { 1382970231, 2 }, // Siren Master Training
    { 1383035764, 2 }, // Shaman Training
    { 1383036788, 2 }, // Spirit Walker Training
    { 1383424609, 2 }, // Banshee Grandmaster Training
    { 1383427685, 2 }, // Necromancer Grandmaster Training
    { 1414542386, 2 }, // Rune Smith Adept Training
  };

  // Building upgrade paths (map field "upgrades to"). Excluded: gates (open/close states), Control Point
  // Defenders (WL levels them), Construction Sites (Stormwind quest) and monuments (wave conditions).
  public static readonly Dictionary<int, List<int>> BuildingUpgrades = new()
  {
    { 1702130288, new List<int> { 1697656880 } }, // etrp  -> e000
    { 1751217271, new List<int> { 1747988536 } }, // hatw  -> h008
    { 1751348343, new List<int> { 1747988535 } }, // hctw  -> h007
    { 1751610487, new List<int> { 1747988534 } }, // hgtw  -> h006
    { 1752659063, new List<int> { 1852073844, 1852139380, 1848653401, 1751610487, 1751348343, 1751217271 } }, // hwtw  -> ndgt,negt,n06Y,hgtw,hctw,hatw
    { 1852006768, new List<int> { 1697656913 } }, // ncap  -> e00Q
    { 1852073844, new List<int> { 1848651828 } }, // ndgt Kirin Tor Tower -> n004
    { 1852139380, new List<int> { 1848651827 } }, // negt Sun Tower -> n003
    { 1852732519, new List<int> { 1848651829 } }, // nntg Tidal Watcher -> n005
    { 1852732532, new List<int> { 1848652884 } }, // nntt Betrayer's Reservoir -> n04T
    { 1870099575, new List<int> { 1851946033, 1865429042 } }, // owtw  -> nbt1,o002
    { 1970956081, new List<int> { 1966092338 } }, // uzg1  -> u002
    { 1970956082, new List<int> { 1966092339 } }, // uzg2  -> u003
    { 1747988818, new List<int> { 1747989043 } }, // h01R  -> h023
    { 1747989043, new List<int> { 1747989059 } }, // h023  -> h02C
    { 1747989072, new List<int> { 1747993396 } }, // h02P Holding -> h0C4
    { 1747989299, new List<int> { 1747989331 } }, // h033 Steading -> h03S
    { 1747989305, new List<int> { 1747989313, 1747989314, 1852073844, 1852139380, 1848653401, 1751610487, 1751348343, 1751217271 } }, // h039  -> h03A,h03B,ndgt,negt,n06Y,hgtw,hctw,hatw
    { 1747989313, new List<int> { 1747989810 } }, // h03A  -> h052
    { 1747989314, new List<int> { 1747989582 } }, // h03B  -> h04N
    { 1747989325, new List<int> { 1747989326 } }, // h03M Hunter Tower -> h03N
    { 1747989331, new List<int> { 1747989332 } }, // h03S Mansion -> h03T
    { 1747990066, new List<int> { 1747990068 } }, // h062  -> h064
    { 1747990068, new List<int> { 1747990089 } }, // h064  -> h06I
    { 1747990069, new List<int> { 1747990070 } }, // h065 Refuge -> h066
    { 1747990070, new List<int> { 1747990072 } }, // h066 Conclave -> h068
    { 1747990091, new List<int> { 1747990093 } }, // h06K  -> h06M
    { 1747990093, new List<int> { 1747990094 } }, // h06M  -> h06N
    { 1747990102, new List<int> { 1747990105, 1747990103, 1747990104 } }, // h06V  -> h06Y,h06W,h06X
    { 1747990103, new List<int> { 1747990320 } }, // h06W  -> h070
    { 1747990104, new List<int> { 1747990321 } }, // h06X  -> h071
    { 1747990105, new List<int> { 1747990106 } }, // h06Y  -> h06Z
    { 1747990323, new List<int> { 1852139380, 1747990324, 1848653401 } }, // h073  -> negt,h074,n06Y
    { 1747990324, new List<int> { 1747990325 } }, // h074  -> h075
    { 1747990328, new List<int> { 1852073844, 1747990329 } }, // h078  -> ndgt,h079
    { 1747990329, new List<int> { 1747990337 } }, // h079  -> h07A
    { 1747990341, new List<int> { 1747990342 } }, // h07E Mining Colony -> h07F
    { 1747990342, new List<int> { 1747990343 } }, // h07F Dwarf Hold -> h07G
    { 1747990344, new List<int> { 1747989814, 1852073844, 1852139380, 1848653401, 1747990345, 1747990346, 1751217271 } }, // h07H  -> h056,ndgt,negt,n06Y,h07I,h07J,hatw
    { 1747990345, new List<int> { 1747990348 } }, // h07I  -> h07L
    { 1747990346, new List<int> { 1747990347 } }, // h07J  -> h07K
    { 1747990354, new List<int> { 1747990357, 1747990355 } }, // h07R  -> h07U,h07S
    { 1747990355, new List<int> { 1747990356 } }, // h07S  -> h07T
    { 1747990357, new List<int> { 1747990358 } }, // h07U  -> h07V
    { 1747990853, new List<int> { 1848656217, 1848656218 } }, // h09E Madness Pool -> n0AY,n0AZ
    { 1747993161, new List<int> { 1747993162 } }, // h0BI Bombard Tower -> h0BJ
    { 1747993165, new List<int> { 1747993166 } }, // h0BM  -> h0BN
    { 1747993166, new List<int> { 1747993167 } }, // h0BN  -> h0BO
    { 1747993396, new List<int> { 1747993397 } }, // h0C4 Covenant -> h0C5
    { 1747993412, new List<int> { 1848657200 } }, // h0CD  -> n0E0
    { 1848652613, new List<int> { 1848656204 } }, // n03E Iron Keep -> n0AL
    { 1848652884, new List<int> { 1848653109 } }, // n04T Betrayer's Spire -> n055
    { 1848653137, new List<int> { 1848653143 } }, // n05Q Holdfast -> n05W
    { 1848653143, new List<int> { 1848653394 } }, // n05W Fortified Burg -> n06R
    { 1848653386, new List<int> { 1848653391 } }, // n06J Sentinel Outpost -> n06O
    { 1848653391, new List<int> { 1848653392 } }, // n06O Sentinel Embassy -> n06P
    { 1848653900, new List<int> { 1848653902 } }, // n08L Lattice Spire -> n08N
    { 1848656205, new List<int> { 1848656206 } }, // n0AM Flame Pillar -> n0AN
    { 1848656210, new List<int> { 1848656211 } }, // n0AR Twisting Halls -> n0AS
    { 1848656211, new List<int> { 1848656212 } }, // n0AS Whispering Labyrinth -> n0AT
    { 1848656217, new List<int> { 1848656432 } }, // n0AY Acid Spitter -> n0B0
    { 1848656218, new List<int> { 1848656433 } }, // n0AZ Sleepless Watcher -> n0B1
    { 1848657200, new List<int> { 1848657201 } }, // n0E0 Sky-Fury Tower -> n0E1
    { 1865429059, new List<int> { 1865429586 } }, // o00C  -> o02R
    { 1865429325, new List<int> { 1747988785 } }, // o01M Engineer's Guild -> h011
    { 1865429584, new List<int> { 1865430320 } }, // o02P Crystal Hall -> o050
    { 1865429586, new List<int> { 1865429587 } }, // o02R  -> o02S
    { 1865429593, new List<int> { 1865429594 } }, // o02Y  -> o02Z
    { 1865429594, new List<int> { 1865429808 } }, // o02Z  -> o030
    { 1865429812, new List<int> { 1848656205, 1865429813 } }, // o034  -> n0AM,o035
    { 1865429817, new List<int> { 1865429825 } }, // o039  -> o03A
    { 1865429825, new List<int> { 1865429826 } }, // o03A  -> o03B
    { 1865429831, new List<int> { 1865430593 } }, // o03G Sentry Tower -> o06A
    { 1865429836, new List<int> { 1865429837 } }, // o03L  -> o03M
    { 1865429837, new List<int> { 1865429838 } }, // o03M  -> o03N
    { 1865429842, new List<int> { 1865429849 } }, // o03R  -> o03Y
    { 1865429845, new List<int> { 1865429847 } }, // o03U Rocket Tower -> o03W
    { 1865429849, new List<int> { 1865429850 } }, // o03Y  -> o03Z
    { 1865430320, new List<int> { 1865430321 } }, // o050 Metropolis -> o051
    { 1865430594, new List<int> { 1865429832 } }, // o06B Territorial Drake -> o03H
    { 1966092355, new List<int> { 1966092366 } }, // u00C Legion Bastion -> u00N
    { 1966092357, new List<int> { 1966092360 } }, // u00E Soul Prison -> u00H
    { 1966092358, new List<int> { 1966092355 } }, // u00F Dormant Spire -> u00C
    { 1966092360, new List<int> { 1966092361 } }, // u00H Soul Tower -> u00I
    { 1966092373, new List<int> { 1966092625 } }, // u00U Crystal Protector -> u01Q
  };

  // Misc researches every faction can use. Granted after the building upgrades run out.
  // v34: naval techs removed (bots don't build ships): R006 Fortified Hulls, R00C Improved Cannons, R04R Navigation.
  public static readonly List<int> MiscAll = new()
  {
    1378892120, // R09X Flight
    1378892087, // R097 Oil Prospecting
  };

  // v34: QUEST building upgrades -- done for free as soon as WC3 accepts the order (no credits needed).
  // Stormwind's Construction Sites (upgradeable from "Inevitable Progress", turn 6) rebuild Stormwind City;
  // bots never did it, so Stormwind City stayed locked all game.
  public static readonly Dictionary<int, int> QuestUpgrades = new()
  {
    { 1747989811, 1747989825 }, // h053 Construction Site -> h05A
    { 1747989813, 1747989834 }, // h055 Construction Site -> h05J
  };

  // v32: EVENT researches -- they trigger quests/events, not combat stats. Never on a timer. A bot orders them at
  // the building that researches them as soon as WC3 allows it (requirements met), for free, but not before
  // NotBefore. (Building ids read from the map: which unit type lists the research.)
  public static readonly List<EventResearch> Events = new()
  {
    // v34: Escape to Theramore and The Scarlet Crusade are FALLBACK paths (Dalaran leaves, Lordaeron becomes
    // the Scarlet Crusade). Fired "as soon as possible" they dismantled the North Alliance at 10-14 min, so
    // bots no longer order them -- WL/humans still can. Re-add with a losing condition later.
    //   1378894135 Escape to Theramore @ h002,  1378900058 The Scarlet Crusade @ h030
    new EventResearch(1378892080, 1865429048, 0f),   // Activate the Blackrock Clan @ Hellfire Citadel (o008)
    new EventResearch(1378890307, 1865429048, 0f),   // The Dark Portal          @ Hellfire Citadel (o008)
    new EventResearch(1378890036, 1848652610, 0f),   // Deeprun Tram             @ Deeprun Tram (n03B)
    new EventResearch(1378891334, 1752393849, 0f),   // Northrend Expedition     @ Shipyard (hshy, Lordaeron)
    new EventResearch(1378891335, 1865429588, 0f),   // Northrend Expedition     @ Shipyard (o02T, Warsong)
    new EventResearch(1378892110, 1848653402, 0f),   // Flight Path              @ Flight Path (n06Z)
    new EventResearch(1378891087, 1868984948, 0f),   // Build Orgrimmar          @ ofrt
    new EventResearch(1378891087, 1865429587, 0f),   // Build Orgrimmar          @ o02S
    new EventResearch(1381193525, 1850035267, 900f), // Fireland Invasion (Sulfuron Spire) @ Vortex Pinnacle -- 15 min
  };

  // Never granted by the AI at all: WL triggers it itself (Plague of Undeath: turn 8 by research, turn 11 auto).
  public static readonly HashSet<int> NeverGrant = new()
  {
    1378891337, // Plague of Undeath
  };
}

public sealed class EventResearch
{
  public readonly int ResearchId;
  public readonly int BuildingId;
  public readonly float NotBefore;

  public EventResearch(int researchId, int buildingId, float notBefore)
  {
    ResearchId = researchId;
    BuildingId = buildingId;
    NotBefore = notBefore;
  }
}
