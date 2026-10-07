using System.Collections.Generic;
using MacroTools.Extensions;
using MacroTools.Factions;
using MacroTools.Shared;
using MacroTools.ControlPoints;
using MacroTools.Quests;
using MacroTools.Legends;
using WarcraftLegacies.Source.Factions.Legion.Quests;

namespace WarcraftLegacies.Source.Ai;

// All inspection / measurement lives here, separate from the logic in AiSystem.cs.
//  * Master switch: set Enabled = false for a release build with NO debug commands and NO display work at
//    all. The op counters in AiSystem stay (they're near-free and always on); only the display is gated.
//  * Per-player, default OFF -> zero screen spam until someone opts in. Toggles are independent:
//      -debug1  per-bot state     (army / landmass / candidates / has-target)
//      -debug2  load profiler      (combat ops per tick + peak, orders, production peak)
//      -debug3  targeting decision (what each bot is doing: atk/def/gather/hold)
//      -xy      selected unit's X/Y + landmass id (one-shot)
//      -fac     each bot's faction + buildable category counts (one-shot)
//      -help    lists every command
//  * v28 AI-development tools (all display-only / local file output -> cannot desync):
//      -dump        write a full snapshot (load, bots, waves, event log, node + edge audit) to
//                   Documents\Warcraft III\CustomMapData\WLAI_dump_<time>.txt  (paste that file to the assistant)
//      -autodump    toggle: write a dump every 5 min for you (WLAI_dump_05m00s.txt, WLAI_dump_10m00s.txt, ...)
//      -why [pid]   decision trace on screen: one bot in detail, or every bot in one line each
//      -edges       toggle: draw every graph edge as a lightning line, coloured by state (only you see it)
//  * IMPORTANT: the op counters measure the BOT LOGIC only, never this display. So an on-screen number is a
//    pessimistic reading -- the real (debug-off) game always has more headroom than what you see here.
//  * Op counts are DETERMINISTIC (identical on every machine), so displaying them can't desync. They are a
//    load *proxy* (units/candidates touched, orders issued), not wall-clock ms -- the right metric for
//    lockstep, and the one to watch: MaxOpUnits in the heaviest single tick, full-bots lategame.
public static class AiDebug
{
  private const bool Enabled = true;   // <- flip to false to ship a zero-debug build
  private const float ShowEvery = 3.0f;
  private const float AutoDumpEvery = 300f;
  private const int PreloadChunk = 180; // keep every Preload() line short; longer lines are wrapped with "  ~"

  private static readonly Dictionary<int, bool> On1 = new();
  private static readonly Dictionary<int, bool> On2 = new();
  private static readonly Dictionary<int, bool> On3 = new();
  private static readonly Dictionary<int, bool> On4 = new();

  public static void Setup()
  {
    if (!Enabled)
    {
      return;
    }

    Register("-debug1", () => Toggle(On1, "state"));
    Register("-debug2", () => Toggle(On2, "load"));
    Register("-debug3", () => Toggle(On3, "decisions"));
    Register("-debug4", () => Toggle(On4, "economy"));
    Register("-xy", OnXy);
    Register("-fac", OnFac);
    Register("-legion", OnLegion);
    Register("-ids", OnIds);
    Register("-id", OnId);
    Register("-portals", OnPortals);
    Register("-ogwave", OnOgWave);
    Register("-gold0", () => SetGold(0));
    Register("-gold50", () => SetGold(50));
    Register("-gold100", () => SetGold(100));
    Register("-gold150", () => SetGold(150));
    Register("-gold200", () => SetGold(200));
    Register("-help", OnHelp);
    Register("-dump", () => WriteDump(GetTriggerPlayer(), "manual"));
    Register("-autodump", OnAutoDump);
    Register("-edges", OnEdges);
    RegisterLoose("-why", OnWhy);
    Register("-ship", OnShipTest);

    var t = CreateTimer();
    TimerStart(t, ShowEvery, true, Show);
    var ad = CreateTimer();
    TimerStart(ad, AutoDumpEvery, true, OnAutoDumpTick);
  }

  // Like Register, but matches any chat line CONTAINING cmd (so "-why 3" works).
  private static void RegisterLoose(string cmd, System.Action act)
  {
    var tr = CreateTrigger();
    for (var i = 0; i < 24; i++)
    {
      TriggerRegisterPlayerChatEvent(tr, Player(i), cmd, false);
    }

    TriggerAddAction(tr, () => RunCommand(cmd, act));
  }

  private static void Register(string cmd, System.Action act)
  {
    var tr = CreateTrigger();
    for (var i = 0; i < 24; i++)
    {
      TriggerRegisterPlayerChatEvent(tr, Player(i), cmd, true);
    }

    TriggerAddAction(tr, () => RunCommand(cmd, act));
  }

  // v34.2: a failing command reports its error to the player instead of silently doing nothing.
  private static void RunCommand(string cmd, System.Action act)
  {
    try
    {
      act();
    }
    catch (System.Exception e)
    {
      DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 15, "|cffff4444AI command " + cmd + " failed:|r " + e.Message);
    }
  }

  private static void Toggle(Dictionary<int, bool> set, string label)
  {
    var pid = GetPlayerId(GetTriggerPlayer());
    var on = !(set.ContainsKey(pid) && set[pid]);
    set[pid] = on;
    DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 6, "debug " + label + (on ? " ON" : " OFF"));
  }

  private static bool IsOn(Dictionary<int, bool> set, int pid)
  {
    return set.ContainsKey(pid) && set[pid];
  }

  private static void Show()
  {
    if (Bolts.Count > 0)
    {
      RecolorBolts(); // edge states change as CPs flip; recolor only (no new handles)
    }

    for (var i = 0; i < 24; i++)
    {
      var pl = Player(i);
      if (GetPlayerSlotState(pl) != PLAYER_SLOT_STATE_PLAYING || GetPlayerController(pl) != MAP_CONTROL_USER)
      {
        continue;
      }

      if (IsOn(On1, i))
      {
        DisplayTimedTextToPlayer(pl, 0, 0, ShowEvery + 0.5f, StateLine());
      }

      if (IsOn(On2, i))
      {
        DisplayTimedTextToPlayer(pl, 0, 0, ShowEvery + 0.5f, LoadLine());
      }

      if (IsOn(On3, i))
      {
        DisplayTimedTextToPlayer(pl, 0, 0, ShowEvery + 0.5f, DecisionLine());
      }

      if (IsOn(On4, i))
      {
        DisplayTimedTextToPlayer(pl, 0, 0, ShowEvery + 0.5f, EconLine());
      }
    }
  }

  private static string StateLine()
  {
    var s = "|cff00ffffAI|r bots=" + I2S(SimpleBot.BotCount);
    for (var b = 0; b < SimpleBot.BotCount; b++)
    {
      var pid = GetPlayerId(SimpleBot.BotAt(b));
      var hasT = SimpleBot.Committed.ContainsKey(pid) && SimpleBot.Committed[pid] != null ? 1 : 0;
      s = s + "  P" + I2S(pid)
          + " a" + I2S(GetI(SimpleBot.BotArmy, pid))
          + " l" + I2S(GetI(SimpleBot.BotLm, pid))
          + " c" + I2S(GetI(SimpleBot.Cand, pid))
          + " t" + I2S(hasT);
    }

    return s;
  }

  private static string LoadLine()
  {
    return "|cffffcc00LOAD|r combat units last=" + I2S(SimpleBot.LastOpUnits)
           + " max=" + I2S(SimpleBot.MaxOpUnits)
           + " orders=" + I2S(SimpleBot.LastOpOrders)
           + " def=" + I2S(SimpleBot.LastDefOps) + "/" + I2S(SimpleBot.MaxDefOps)
           + "  |  prod peak=" + I2S(SimpleBot.MaxProdOps)
           + "  |  wave tick=" + I2S(SimpleBot.LastWaveOps) + "/" + I2S(SimpleBot.MaxWaveOps);
  }

  private static string EconLine()
  {
    var s = "|cff44ff44ECON|r";
    for (var b = 0; b < SimpleBot.BotCount; b++)
    {
      var pl = SimpleBot.BotAt(b);
      s = s + "  P" + I2S(GetPlayerId(pl))
          + " g" + I2S(GetPlayerState(pl, PLAYER_STATE_RESOURCE_GOLD))
          + " f" + I2S(GetPlayerState(pl, PLAYER_STATE_RESOURCE_FOOD_USED))
          + "/" + I2S(GetPlayerState(pl, PLAYER_STATE_RESOURCE_FOOD_CAP));
    }

    return s;
  }

  private static string DecisionLine()
  {
    var s = "|cffff88ffDECIDE|r";
    for (var b = 0; b < SimpleBot.BotCount; b++)
    {
      var pid = GetPlayerId(SimpleBot.BotAt(b));
      s = s + "  P" + I2S(pid) + ":" + GetS(SimpleBot.BotState, pid) + " " + GetS(SimpleBot.BotTargetName, pid);
    }

    return s;
  }

  private static int GetI(Dictionary<int, int> d, int k)
  {
    return d.ContainsKey(k) ? d[k] : 0;
  }

  private static string GetS(Dictionary<int, string> d, int k)
  {
    return d.ContainsKey(k) ? d[k] : "-";
  }

  // Force-summon the Legion for testing (normally a late milestone). Completes its SummonLegion quest,
  // which unlocks the Nether Pit + Summoning Circle, rescues its units, and opens the portals.
  private static void OnLegion()
  {
    SimpleBot.SummonLegion(); // completes the quest AND spawns a fresh portal pair at the configured endpoints
    DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 8,
      "|cffff5555Legion force-summoned. Use -portals to find the exit.|r");
  }

  // Float a label + ping at the Legion portal endpoints so they can be located even if the model is unclear.
  // v34.1: label + ping every AI portal that currently exists (ship and Argus portals).
  private static void OnPortals()
  {
    var n = 0;
    for (var i = 0; i < SimpleBot.PortalPairCount; i++)
    {
      n += MarkUnit(SimpleBot.PortalA(i)) + MarkUnit(SimpleBot.PortalB(i));
    }

    DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 10, "|cffffcc00-portals|r: " + I2S(n)
      + " AI portal ends labelled + pinged for 30s (" + I2S(SimpleBot.PortalPairCount) + " pairs; graphs merged="
      + B(SimpleBot.GraphMerged) + ")");
  }

  private static int MarkUnit(unit u)
  {
    if (u == null || GetUnitTypeId(u) == 0)
    {
      return 0;
    }

    Mark(GetUnitName(u), GetUnitX(u), GetUnitY(u));
    return 1;
  }

  private static void Mark(string label, float x, float y)
  {
    var tt = CreateTextTag();
    SetTextTagText(tt, label, 0.022f);
    SetTextTagPos(tt, x, y, 96f);
    SetTextTagColor(tt, 255, 120, 120, 255);
    SetTextTagVisibility(tt, true);
    SetTextTagPermanent(tt, false);
    SetTextTagLifespan(tt, 30f);
    SetTextTagFadepoint(tt, 27f);
    PingMinimap(x, y, 30f);
  }

  // Float every control point's id + name above it for 30s, so you can read ids off the map.
  private static void OnIds()
  {
    var cps = ControlPointManager.Instance.GetAllControlPoints();
    for (var i = 0; i < cps.Count; i++)
    {
      var cp = cps[i];
      var tt = CreateTextTag();
      SetTextTagText(tt, I2S(cp.UnitType) + " " + cp.Name, 0.020f);
      SetTextTagPos(tt, GetUnitX(cp.Unit), GetUnitY(cp.Unit), 64f);
      SetTextTagColor(tt, 255, 255, 0, 255);
      SetTextTagVisibility(tt, true);
      SetTextTagPermanent(tt, false);
      SetTextTagLifespan(tt, 30f);
      SetTextTagFadepoint(tt, 27f);
    }

    DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 8, "|cffffcc00-ids|r: control-point ids shown for 30s.");
  }

  private static void SetGold(int amount)
  {
    SimpleBot.BotGoldBonus = amount;
    DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 6, "bot gold bonus = " + I2S(amount));
  }

  private static void OnOgWave()
  {
    SimpleBot.ForceWave(0); // wave 0 = Old Gods
    DisplayTimedTextToPlayer(GetTriggerPlayer(), 0, 0, 8, "|cffaa00ffwave 0 (Old Gods) forced.|r");
  }

  // Report the identity of the selected unit: type id as decimal AND 4-char rawcode (so FourCC byte-order
  // mistakes are obvious), plus name, owner and position. Read-only -> lockstep-safe, fine to leave in.
  private static void OnId()
  {
    var p = GetTriggerPlayer();
    var g = CreateGroup();
    GroupEnumUnitsSelected(g, p, null);
    var u = FirstOfGroup(g);
    DestroyGroup(g);
    if (u == null)
    {
      DisplayTimedTextToPlayer(p, 0, 0, 10, "|cffffcc00-id|r: select a unit first (destructibles aren't reported).");
      return;
    }

    var tid = GetUnitTypeId(u);
    var owner = GetOwningPlayer(u);
    DisplayTimedTextToPlayer(p, 0, 0, 30,
      "|cffffcc00-id|r " + RawCode(tid) + " = " + I2S(tid)
      + "  '" + GetUnitName(u) + "'"
      + "  owner=P" + I2S(GetPlayerId(owner))
      + "  @ " + I2S(R2I(GetUnitX(u))) + "," + I2S(R2I(GetUnitY(u))));
  }

  // Decode a big-endian FourCC int back to its 4 characters (non-printable bytes shown as '?').
  private static string RawCode(int id)
  {
    var s = "";
    for (var shift = 24; shift >= 0; shift -= 8)
    {
      var b = (id >> shift) & 0xFF;
      s = s + (b >= 32 && b <= 126 ? "" + (char)b : "?");
    }

    return s;
  }

  private static void OnXy()
  {
    var p = GetTriggerPlayer();
    var g = CreateGroup();
    GroupEnumUnitsSelected(g, p, null);
    var u = FirstOfGroup(g);
    DestroyGroup(g);
    if (u == null)
    {
      DisplayTimedTextToPlayer(p, 0, 0, 8, "|cffffcc00-xy|r: select a unit first.");
      return;
    }

    var x = GetUnitX(u);
    var y = GetUnitY(u);
    DisplayTimedTextToPlayer(p, 0, 0, 30,
      "|cffffcc00X=" + I2S(R2I(x)) + " Y=" + I2S(R2I(y)) + "|r landmass=" + I2S(Geography.Landmass(x, y)));
  }

  private static int CatCount(Faction f, UnitCategory cat)
  {
    if (f.TryGetObjectByCategory(cat, out var ids) && ids != null)
    {
      return ids.Count;
    }

    return 0;
  }

  private static string CatTok(Faction f, string label, UnitCategory cat)
  {
    var n = CatCount(f, cat);
    return n > 0 ? " " + label + I2S(n) : "";
  }

  private static void OnFac()
  {
    var p = GetTriggerPlayer();
    for (var b = 0; b < SimpleBot.BotCount; b++)
    {
      var bot = SimpleBot.BotAt(b);
      var pid = GetPlayerId(bot);
      var faction = bot.GetPlayerData().Faction;
      if (faction == null)
      {
        continue;
      }

      var line = "|cff00ffffP" + I2S(pid) + " " + faction.Name + "|r u:"
                 + CatTok(faction, "Fi", UnitCategory.Fighter)
                 + CatTok(faction, "Ma", UnitCategory.Marksman)
                 + CatTok(faction, "Si", UnitCategory.Siege)
                 + CatTok(faction, "Ta", UnitCategory.Tank)
                 + CatTok(faction, "De", UnitCategory.Destroyer)
                 + CatTok(faction, "Fl", UnitCategory.Flyer)
                 + CatTok(faction, "Su", UnitCategory.Support)
                 + CatTok(faction, "El", UnitCategory.Elite)
                 + "  b:"
                 + CatTok(faction, "Ba", UnitCategory.Barracks)
                 + CatTok(faction, "Sw", UnitCategory.SiegeWorkshop)
                 + CatTok(faction, "Mg", UnitCategory.Magic)
                 + CatTok(faction, "Fb", UnitCategory.FlyingBuilding)
                 + CatTok(faction, "Al", UnitCategory.Altar)
                 + CatTok(faction, "Sh", UnitCategory.Shipyard);
      DisplayTimedTextToPlayer(p, 0, 0, 60, line);
    }
  }

  // =========================================================================================================
  //  v28 AI-DEVELOPMENT TOOLS
  // =========================================================================================================

  // v34.2: -help is short now -- the full command list lives in README.md (AI section).
  private static void OnHelp()
  {
    var p = GetTriggerPlayer();
    DisplayTimedTextToPlayer(p, 0, 0, 20, "|cffffcc00WL AI|r " + AiSetup.Build
      + " -- full command list: README.md. Most used: -dump  -autodump  -why  -portals  -edges  -xy  -id");
  }


  // ---- -why -------------------------------------------------------------------------------------------
  private static void OnWhy()
  {
    var p = GetTriggerPlayer();
    var msg = GetEventPlayerChatString();
    var arg = msg.Length > 5 ? msg.Substring(5) : "";
    if (arg == "")
    {
      for (var b = 0; b < SimpleBot.BotCount; b++)
      {
        var pid = GetPlayerId(SimpleBot.BotAt(b));
        var d = SimpleBot.GetDiag(pid);
        DisplayTimedTextToPlayer(p, 0, 0, 20, "P" + I2S(pid) + " " + FacOf(SimpleBot.BotAt(b))
          + " " + GetS(SimpleBot.BotState, pid) + " -> " + TargetLabel(d) + " [" + d.Reason + "]"
          + " n" + I2S(d.Neutral) + " e" + I2S(d.Enemy) + " inv" + I2S(d.Invuln) + " cl" + I2S(d.Closed));
      }

      return;
    }

    var want = S2I(arg);
    for (var b = 0; b < SimpleBot.BotCount; b++)
    {
      if (GetPlayerId(SimpleBot.BotAt(b)) == want)
      {
        var lines = new List<string>();
        BotLines(want, lines, false);
        for (var i = 0; i < lines.Count; i++)
        {
          DisplayTimedTextToPlayer(p, 0, 0, 30, lines[i]);
        }

        return;
      }
    }

    DisplayTimedTextToPlayer(p, 0, 0, 10, "-why: P" + I2S(want) + " is not a bot. Use -why alone to list bots.");
  }

  // ---- -dump / -autodump ------------------------------------------------------------------------------
  private static player _autoDumpFor;

  private static void OnAutoDump()
  {
    var p = GetTriggerPlayer();
    _autoDumpFor = _autoDumpFor == p ? null : p;
    DisplayTimedTextToPlayer(p, 0, 0, 8, "autodump " + (_autoDumpFor != null ? "ON (every 5 min)" : "OFF"));
  }

  private static void OnAutoDumpTick()
  {
    if (_autoDumpFor != null)
    {
      WriteDump(_autoDumpFor, "auto");
    }
  }

  private static void WriteDump(player who, string tag)
  {
    var lines = new List<string>();
    BuildDump(lines, tag);
    var path = "WLAI_dump_" + FileTime(SimpleBot.Clock) + ".txt"; // flat name: no subfolder needed

    // The file is written ONLY on the requesting player's machine. Preload* natives create no handles and
    // change no game state, so running them inside a GetLocalPlayer() block is desync-safe (same technique
    // save/load systems use). Everything above (building the lines) runs identically on every client.
    if (GetLocalPlayer() == who)
    {
      PreloadGenClear();
      PreloadGenStart();
      for (var i = 0; i < lines.Count; i++)
      {
        var line = lines[i];
        var first = true;
        while (line.Length > PreloadChunk)
        {
          Preload((first ? "" : "  ~") + line.Substring(0, PreloadChunk));
          line = line.Substring(PreloadChunk);
          first = false;
        }

        Preload((first ? "" : "  ~") + line);
      }

      PreloadGenEnd(path);
    }

    DisplayTimedTextToPlayer(who, 0, 0, 12, "|cff88ff88dump written|r (" + I2S(lines.Count)
      + " lines) -> Documents\\Warcraft III\\CustomMapData\\" + path);
  }

  private static string FileTime(float t)
  {
    var s = R2I(t);
    var m = s / 60;
    var r = s - m * 60;
    return (m < 10 ? "0" : "") + I2S(m) + "m" + (r < 10 ? "0" : "") + I2S(r) + "s";
  }

  // v34.2: every section is isolated -- a failing section writes "## ERROR in <section>" and the file is still
  // written (a single error used to make the whole -dump silently do nothing). Pinned events + portals first.
  private static void BuildDump(List<string> o, string tag)
  {
    o.Add("=== WLAI DUMP build=" + AiSetup.Build + " t=" + SimpleBot.TimeTag(SimpleBot.Clock) + " tag=" + tag
          + " bots=" + I2S(SimpleBot.BotCount) + " goldBonus=" + I2S(SimpleBot.BotGoldBonus)
          + " botbreak=" + B(SimpleBot.BotBreak) + " prod=" + B(SimpleBot.ProdOn) + " atk=" + B(SimpleBot.AtkOn) + " ===");

    Section(o, "pinned", () =>
    {
      o.Add("## PINNED (one-off events, never pushed out)");
      for (var i = 0; i < SimpleBot.PinnedLog.Count; i++)
      {
        o.Add(SimpleBot.PinnedLog[i]);
      }
    });

    Section(o, "portals", () =>
    {
      o.Add("## PORTALS (AI waygates: name @x,y alive -> destination)  merged=" + B(SimpleBot.GraphMerged));
      for (var i = 0; i < SimpleBot.PortalPairCount; i++)
      {
        o.Add(PortalLine(SimpleBot.PortalA(i)) + "   <->   " + PortalLine(SimpleBot.PortalB(i)));
      }
    });

    Section(o, "load", () =>
    {
      o.Add("## LOAD  (units touched; last/max. combat = one bot's service; def is part of combat)");
      o.Add("combat " + I2S(SimpleBot.LastOpUnits) + "/" + I2S(SimpleBot.MaxOpUnits)
            + " orders=" + I2S(SimpleBot.LastOpOrders)
            + " | def-scan " + I2S(SimpleBot.LastDefOps) + "/" + I2S(SimpleBot.MaxDefOps)
            + " | production peak " + I2S(SimpleBot.MaxProdOps)
            + " | wave-tick " + I2S(SimpleBot.LastWaveOps) + "/" + I2S(SimpleBot.MaxWaveOps)
            + " | last gate sweep " + I2S(SimpleBot.LastSweepOps));
    });

    o.Add("## BOTS");
    for (var b = 0; b < SimpleBot.BotCount; b++)
    {
      var pid = GetPlayerId(SimpleBot.BotAt(b));
      Section(o, "bot P" + I2S(pid), () => BotLines(pid, o, true));
    }

    Section(o, "waves", () =>
    {
      o.Add("## WAVES");
      for (var w = 0; w < AiWaves.All.Count; w++)
      {
        WaveLine(w, o);
      }
    });

    Section(o, "permadead", () =>
    {
      o.Add("## PERMADEAD hero types (never re-trained/revived)");
      var all = LegendaryHeroManager.GetAll();
      var pd = "";
      for (var i = 0; i < all.Count; i++)
      {
        if (SimpleBot.PermaDead.Contains(all[i].UnitType))
        {
          pd = pd + RawCode(all[i].UnitType) + " ";
        }
      }

      o.Add(pd == "" ? "(none)" : pd);
    });

    Section(o, "event log", () =>
    {
      o.Add("## EVENT LOG (oldest first, state changes only)");
      for (var i = 0; i < SimpleBot.EventLog.Count; i++)
      {
        o.Add(SimpleBot.EventLog[i]);
      }
    });

    Section(o, "nodes", () => NodeLines(o));
    Section(o, "edges C", () => EdgeLines(o, "C", AiGraph.Combined));
    Section(o, "edges K", () => EdgeLines(o, "K", AiGraph.Kalimdor));
    o.Add("=== END ===");
  }

  private static void Section(List<string> o, string name, System.Action build)
  {
    try
    {
      build();
    }
    catch (System.Exception e)
    {
      o.Add("## ERROR in " + name + ": " + e.Message);
    }
  }


  private static string B(bool v) => v ? "1" : "0";

  private static string PortalLine(unit u)
  {
    if (u == null || GetUnitTypeId(u) == 0)
    {
      return "(gone)";
    }

    return GetUnitName(u) + " @" + I2S(R2I(GetUnitX(u))) + "," + I2S(R2I(GetUnitY(u)))
           + (GetUnitState(u, UNIT_STATE_LIFE) > 0.405f ? "" : " DEAD")
           + " -> " + I2S(R2I(WaygateGetDestinationX(u))) + "," + I2S(R2I(WaygateGetDestinationY(u)));
  }

  // ---- per-bot block (shared by -dump and -why) -------------------------------------------------------
  private static void BotLines(int pid, List<string> o, bool withHeroes)
  {
    var pl = Player(pid);
    var d = SimpleBot.GetDiag(pid);
    o.Add("P" + I2S(pid) + " " + FacOf(pl)
          + " st=" + GetS(SimpleBot.BotState, pid)
          + " set=" + d.EdgeSet
          + " army=" + I2S(GetI(SimpleBot.BotArmy, pid))
          + " gold=" + I2S(GetPlayerState(pl, PLAYER_STATE_RESOURCE_GOLD))
          + " food=" + I2S(GetPlayerState(pl, PLAYER_STATE_RESOURCE_FOOD_USED))
          + "/" + I2S(GetPlayerState(pl, PLAYER_STATE_RESOURCE_FOOD_CAP))
          + " anchor=" + (d.AnchorIsBase ? "base" : "army") + "@" + I2S(R2I(d.AnchorX)) + "," + I2S(R2I(d.AnchorY))
          + " lm=" + I2S(GetI(SimpleBot.BotLm, pid)) + " homeLm=" + I2S(d.HomeLm));

    // army by landmass (non-structure, non-worker, alive) -> exposes units stranded on another continent
    var lms = new List<int>();
    var cnt = new List<int>();
    var sx = new List<float>();
    var sy = new List<float>();
    float tx = 0f, ty = 0f;
    var tn = 0;
    var g = CreateGroup();
    GroupEnumUnitsOfPlayer(g, pl, null);
    unit u;
    while ((u = FirstOfGroup(g)) != null)
    {
      GroupRemoveUnit(g, u);
      if (IsUnitType(u, UNIT_TYPE_STRUCTURE) || IsUnitType(u, UNIT_TYPE_PEON)
          || GetUnitState(u, UNIT_STATE_LIFE) <= 0.405f)
      {
        continue;
      }

      var x = GetUnitX(u);
      var y = GetUnitY(u);
      tx += x;
      ty += y;
      tn++;
      var lm = Geography.Landmass(x, y);
      var k = lms.IndexOf(lm);
      if (k < 0)
      {
        lms.Add(lm);
        cnt.Add(0);
        sx.Add(0f);
        sy.Add(0f);
        k = lms.Count - 1;
      }

      cnt[k] = cnt[k] + 1;
      sx[k] = sx[k] + x;
      sy[k] = sy[k] + y;
    }

    DestroyGroup(g);

    var t = d.Target;
    var tline = "  tgt=" + TargetLabel(d) + " why=" + d.Reason;
    if (t != null)
    {
      var maxLife = GetUnitState(t, UNIT_STATE_MAX_LIFE);
      var hp = maxLife > 0f ? R2I(100f * GetUnitState(t, UNIT_STATE_LIFE) / maxLife) : 0;
      tline = tline + " own=" + OwnerTag(GetOwningPlayer(t))
              + " inv=" + B(BlzIsUnitInvulnerable(t))
              + " hp=" + I2S(hp) + "%"
              + " lm=" + I2S(Geography.Landmass(GetUnitX(t), GetUnitY(t)));
      if (tn > 0)
      {
        var dx = GetUnitX(t) - tx / tn;
        var dy = GetUnitY(t) - ty / tn;
        tline = tline + " armyDist=" + I2S(R2I(SquareRoot(dx * dx + dy * dy)));
      }
    }

    tline = tline + " for=" + I2S(R2I(SimpleBot.Clock - d.TargetSince)) + "s";
    o.Add(tline);

    o.Add("  frontiers: neutral=" + I2S(d.Neutral) + " enemy=" + I2S(d.Enemy) + " locked<10m=" + I2S(d.Locked)
          + " uncapturable=" + I2S(d.Invuln) + " closed=" + I2S(d.Closed) + " skipnode=" + I2S(d.Skipped)
          + " stuck-skip=" + I2S(d.Stuck)
          + " non-cp-edges=" + I2S(d.NoCp));

    var ul = "  army by landmass:";
    for (var i = 0; i < lms.Count; i++)
    {
      ul = ul + " lm" + I2S(lms[i]) + "=" + I2S(cnt[i])
           + "@" + I2S(R2I(sx[i] / cnt[i])) + "," + I2S(R2I(sy[i] / cnt[i]));
    }

    o.Add(lms.Count == 0 ? "  army by landmass: (none)" : ul);

    if (!withHeroes)
    {
      return;
    }

    var hl = "  heroes:";
    var any = false;
    var all = LegendaryHeroManager.GetAll();
    for (var i = 0; i < all.Count; i++)
    {
      var hu = all[i].Unit;
      if (hu == null || GetUnitTypeId(hu) == 0 || GetOwningPlayer(hu) != pl)
      {
        continue;
      }

      any = true;
      hl = hl + " " + GetUnitName(hu) + "(" + RawCode(GetUnitTypeId(hu)) + ","
           + (GetUnitState(hu, UNIT_STATE_LIFE) > 0.405f ? "alive" : "DEAD")
           + (GetUnitAbilityLevel(hu, FourCC("LEgo")) > 0 ? ",perma" : "") + ")";
    }

    o.Add(any ? hl : "  heroes: (none)");
  }

  private static string TargetLabel(BotDiag d)
  {
    return d.Target != null ? GetUnitName(d.Target) + "{" + RawCode(GetUnitTypeId(d.Target)) + "}" : "-";
  }

  private static string FacOf(player p)
  {
    var f = p.GetPlayerData().Faction;
    return f != null ? f.Name : "?";
  }

  private static string OwnerTag(player o)
  {
    if (o == Player(PLAYER_NEUTRAL_AGGRESSIVE))
    {
      return "NA";
    }

    if (o == Player(PLAYER_NEUTRAL_PASSIVE))
    {
      return "NP";
    }

    var id = GetPlayerId(o);
    return id < 24 ? "P" + I2S(id) + "(" + FacOf(o) + ")" : "N" + I2S(id);
  }

  // ---- waves ------------------------------------------------------------------------------------------
  private static void WaveLine(int idx, List<string> o)
  {
    var cfg = AiWaves.All[idx];
    var w = SimpleBot.GetWaveDiag(idx);
    var names = "";
    for (var i = 0; i < cfg.Factions.Count; i++)
    {
      names = names + (i > 0 ? "," : "") + cfg.Factions[i];
    }

    if (!w.HomeOk)
    {
      o.Add("W" + I2S(idx) + " [" + names + "] home CP " + I2S(cfg.HomeCpId) + " NOT a control point -> wave can never run");
      return;
    }

    var phase = w.Phase == 0 ? "idle" : w.Phase == 1 ? "GATHER" : "CHANNEL";
    var thresholdMet = w.Total > 0 && w.Owned >= w.Need;
    var blocked = "";
    if (!thresholdMet && !w.Monuments)
    {
      blocked = blocked + " needCPs(" + I2S(w.Owned) + "/" + I2S(w.Need) + ")";
    }

    if (w.Enemies >= AiWorld.EnemyLeaveThreshold)
    {
      blocked = blocked + " enemiesOnHome(" + I2S(w.Enemies) + ">=" + I2S(AiWorld.EnemyLeaveThreshold) + ")";
    }

    if (!w.TriggerOk)
    {
      blocked = blocked + " triggerCpNotOwned";
    }

    if (!w.HasBot)
    {
      blocked = blocked + " NO-BOT-IN-GROUP(disabled)";
    }

    if (w.Active && w.Phase == 0 && w.CooldownLeft > 0f)
    {
      blocked = blocked + " cooldown(" + I2S(R2I(w.CooldownLeft)) + "s)";
    }

    o.Add("W" + I2S(idx) + " [" + names + "] phase=" + phase + " age=" + I2S(R2I(w.PhaseAge)) + "s"
          + " active=" + B(w.Active) + " homeLm=" + I2S(w.HomeLm)
          + " homeCPs=" + I2S(w.Owned) + "/" + I2S(w.Total) + " need=" + I2S(w.Need)
          + " monuments=" + B(w.Monuments) + " enemiesHome=" + I2S(w.Enemies)
          + " trigger=" + B(w.TriggerOk) + " cooldown=" + I2S(R2I(w.CooldownLeft)) + "s"
          + (blocked != "" ? " BLOCKED:" + blocked : ""));
  }

  // ---- graph audit ------------------------------------------------------------------------------------
  // Every control point on the map: where it is, which landmass the grid gives it, who owns it, whether it
  // can be captured, and which authored graph (C=Combined, K=Kalimdor, -=none) contains it.
  private static void NodeLines(List<string> o)
  {
    var inC = new HashSet<int>();
    var inK = new HashSet<int>();
    for (var i = 0; i < AiGraph.Combined.Count; i++)
    {
      inC.Add(AiGraph.Combined[i].A);
      inC.Add(AiGraph.Combined[i].B);
    }

    for (var i = 0; i < AiGraph.Kalimdor.Count; i++)
    {
      inK.Add(AiGraph.Kalimdor[i].A);
      inK.Add(AiGraph.Kalimdor[i].B);
    }

    var cps = ControlPointManager.Instance.GetAllControlPoints();
    o.Add("## NODES (" + I2S(cps.Count) + " control points)  id raw name @x,y lm owner uncapturable graph");
    for (var i = 0; i < cps.Count; i++)
    {
      var cp = cps[i];
      var cu = cp.Unit;
      if (cu == null)
      {
        o.Add(I2S(cp.UnitType) + " " + RawCode(cp.UnitType) + " " + cp.Name + " (no unit)");
        continue;
      }

      var x = GetUnitX(cu);
      var y = GetUnitY(cu);
      o.Add(I2S(cp.UnitType) + " " + RawCode(cp.UnitType) + " " + cp.Name
            + " @" + I2S(R2I(x)) + "," + I2S(R2I(y))
            + " lm=" + I2S(Geography.Landmass(x, y))
            + " own=" + OwnerTag(GetOwningPlayer(cu))
            + " unc=" + B(SimpleBot.IsUncapturable(cu))
            + " g=" + (inC.Contains(cp.UnitType) ? "C" : "") + (inK.Contains(cp.UnitType) ? "K" : "")
            + (inC.Contains(cp.UnitType) || inK.Contains(cp.UnitType) ? "" : "-"));
    }
  }

  // Every edge with the facts that decide whether a bot can actually walk it. Flags:
  //   XLM   endpoints on different landmasses (probably needs water/portal -> bots park at the shore)
  //   LM?   an endpoint reads landmass -1 (island / unresolved grid cell)
  //   NOCP  an id isn't a registered control point (SafeCp null) -> edge ignored
  //   UNC   an endpoint is uncapturable (Neutral Passive / invulnerable)
  //   T<s>  time-gated edge (open/closed now)    G:<name>  named gate (open/closed now)
  private static void EdgeLines(List<string> o, string tag, List<Edge> edges)
  {
    o.Add("## EDGES " + tag + " (" + I2S(edges.Count) + ")  A <-> B | lm a/b | own a/b | flags");
    var suspect = 0;
    for (var i = 0; i < edges.Count; i++)
    {
      var e = edges[i];
      var ca = SimpleBot.SafeCp(e.A);
      var cb = SimpleBot.SafeCp(e.B);
      var flags = "";
      if (e.OpenTime > 0f)
      {
        flags = flags + " T" + I2S(R2I(e.OpenTime)) + (e.OpenTime > SimpleBot.Clock ? "(closed)" : "(open)");
      }

      if (e.Gate != "")
      {
        flags = flags + " G:" + e.Gate + (SimpleBot.GateOpen(e.Gate) ? "(open)" : "(closed)");
      }

      if (ca == null || cb == null || ca.Unit == null || cb.Unit == null)
      {
        o.Add(tag + " " + I2S(e.A) + " <-> " + I2S(e.B) + " | NOCP" + flags);
        continue;
      }

      var la = Geography.Landmass(GetUnitX(ca.Unit), GetUnitY(ca.Unit));
      var lb = Geography.Landmass(GetUnitX(cb.Unit), GetUnitY(cb.Unit));
      if (la != lb)
      {
        flags = flags + " XLM";
      }

      if (la < 0 || lb < 0)
      {
        flags = flags + " LM?";
      }

      if (SimpleBot.IsUncapturable(ca.Unit) || SimpleBot.IsUncapturable(cb.Unit))
      {
        flags = flags + " UNC";
      }

      if (la != lb || la < 0 || lb < 0)
      {
        suspect++;
      }

      o.Add(tag + " " + ca.Name + " <-> " + cb.Name
            + " | lm " + I2S(la) + "/" + I2S(lb)
            + " | own " + OwnerTag(GetOwningPlayer(ca.Unit)) + "/" + OwnerTag(GetOwningPlayer(cb.Unit))
            + " |" + (flags == "" ? " -" : flags));
    }

    o.Add("## EDGES " + tag + ": " + I2S(suspect) + " edge(s) cross landmasses or touch landmass -1");
  }

  // ---- -edges (lightning overlay) ---------------------------------------------------------------------
  private static readonly List<lightning> Bolts = new();
  private static readonly List<Edge> BoltEdges = new();
  private static player _edgesViewer;
  private static location _zLoc;

  private static void OnEdges()
  {
    var p = GetTriggerPlayer();
    if (Bolts.Count > 0)
    {
      for (var i = 0; i < Bolts.Count; i++)
      {
        DestroyLightning(Bolts[i]);
      }

      Bolts.Clear();
      BoltEdges.Clear();
      _edgesViewer = null;
      DisplayTimedTextToPlayer(p, 0, 0, 6, "edges OFF");
      return;
    }

    _edgesViewer = p;
    DrawEdges(AiGraph.Combined);
    DrawEdges(AiGraph.Kalimdor);
    RecolorBolts();
    DisplayTimedTextToPlayer(p, 0, 0, 20, "edges ON (" + I2S(Bolts.Count) + "). green=held yellow=neutral frontier "
      + "orange=uncapturable red=contested blue=neutral magenta=crosses landmass grey=closed");
  }

  private static void DrawEdges(List<Edge> edges)
  {
    if (_zLoc == null)
    {
      _zLoc = Location(0f, 0f);
    }

    for (var i = 0; i < edges.Count; i++)
    {
      var ca = SimpleBot.SafeCp(edges[i].A);
      var cb = SimpleBot.SafeCp(edges[i].B);
      if (ca == null || cb == null || ca.Unit == null || cb.Unit == null)
      {
        continue;
      }

      var x1 = GetUnitX(ca.Unit);
      var y1 = GetUnitY(ca.Unit);
      var x2 = GetUnitX(cb.Unit);
      var y2 = GetUnitY(cb.Unit);
      // GetLocationZ is client-local terrain height -> only ever used for visuals, never logic.
      MoveLocation(_zLoc, x1, y1);
      var z1 = GetLocationZ(_zLoc) + 90f;
      MoveLocation(_zLoc, x2, y2);
      var z2 = GetLocationZ(_zLoc) + 90f;
      // v30: "CLSB" uses the near-white Lightning.blp, so the tint shows true colours. ("DRAL" was a green
      // texture: tinted yellow looked green, red looked dark -> the "different greens" on the overlay.)
      Bolts.Add(AddLightningEx("CLSB", false, x1, y1, z1, x2, y2, z2));
      BoltEdges.Add(edges[i]);
    }
  }

  private static void RecolorBolts()
  {
    // Only the player who toggled -edges sees them (alpha computed locally; no handle work inside).
    var a = GetLocalPlayer() == _edgesViewer ? 1f : 0f;
    for (var i = 0; i < Bolts.Count; i++)
    {
      var c = EdgeColor(BoltEdges[i]);
      float r = 0.5f, gr = 0.5f, b = 0.5f;               // 0 grey: closed
      if (c == 1) { r = 1f; gr = 0f; b = 1f; }           // magenta: crosses landmass
      else if (c == 2) { r = 0.2f; gr = 1f; b = 0.2f; }  // green: held (same owner / allies)
      else if (c == 3) { r = 1f; gr = 0.15f; b = 0.15f; } // red: contested between players
      else if (c == 4) { r = 1f; gr = 1f; b = 0.1f; }    // yellow: player -> capturable neutral
      else if (c == 5) { r = 1f; gr = 0.55f; b = 0f; }   // orange: player -> uncapturable neutral
      else if (c == 6) { r = 0.3f; gr = 0.45f; b = 1f; } // blue: neutral <-> neutral (faded: background)
      SetLightningColor(Bolts[i], r, gr, b, c == 6 ? a * 0.35f : a);
    }
  }

  private static int EdgeColor(Edge e)
  {
    if (e.OpenTime > SimpleBot.Clock || (e.Gate != "" && !SimpleBot.GateOpen(e.Gate)))
    {
      return 0;
    }

    var ca = SimpleBot.SafeCp(e.A);
    var cb = SimpleBot.SafeCp(e.B);
    if (ca == null || cb == null || ca.Unit == null || cb.Unit == null)
    {
      return 0;
    }

    var la = Geography.Landmass(GetUnitX(ca.Unit), GetUnitY(ca.Unit));
    var lb = Geography.Landmass(GetUnitX(cb.Unit), GetUnitY(cb.Unit));
    if (la != lb || la < 0 || lb < 0)
    {
      return 1;
    }

    var oa = GetOwningPlayer(ca.Unit);
    var ob = GetOwningPlayer(cb.Unit);
    var pa = GetPlayerId(oa) < 24;
    var pb = GetPlayerId(ob) < 24;
    if (pa && pb)
    {
      return oa == ob || IsPlayerAlly(oa, ob) ? 2 : 3;
    }

    if (pa || pb)
    {
      var neutralSide = pa ? cb.Unit : ca.Unit;
      return SimpleBot.IsUncapturable(neutralSide) ? 5 : 4;
    }

    return 6;
  }

  // ---- v33 -ship : ship-portal asset test (Kul Tiras landfall) ---------------------------------------------
  // Every call removes the previous test and shows the NEXT ship model, two ways side by side:
  //   A "skin":   the waygate unit itself re-skinned to a ship unit type (BlzSetUnitSkin)
  //   B "effect": an invisible waygate with the ship model as a special effect on top (works with any model)
  // A and B are created at (0,0) and linked to each other. With a unit selected, a second linked pair is made
  // 600/2000 units beside it on land, so walking into one ship can be tested (should land at the other).
  private static readonly string[] ShipCodes = { "hbot", "h0AN", "h0B6", "h0AR", "h060", "hdes" };
  private static readonly string[] ShipNames =
    { "Transport Ship", "Juggernaut", "Boarding Vessel", "Scout Ship", "Carrier Flagship", "Frigate (stock)" };
  private static readonly string[] ShipModels =
  {
    "war3mapImported\\Unit_HumanTransportShip.mdl",
    "war3mapImported\\Unit_HumanJuggernaut.mdl",
    "war3mapImported\\Unit_HumanBoardingShip.mdl",
    "war3mapImported\\Unit_HumanScout.mdl",
    "StormhawkShip.mdl",
    "units\\human\\HumanDestroyerShip\\HumanDestroyerShip.mdl",
  };

  private static int _shipIdx = -1;
  private static readonly List<unit> ShipUnits = new();
  private static readonly List<effect> ShipFx = new();

  private static void OnShipTest()
  {
    var p = GetTriggerPlayer();
    for (var i = 0; i < ShipUnits.Count; i++)
    {
      RemoveUnit(ShipUnits[i]);
    }

    for (var i = 0; i < ShipFx.Count; i++)
    {
      DestroyEffect(ShipFx[i]);
    }

    ShipUnits.Clear();
    ShipFx.Clear();
    _shipIdx = (_shipIdx + 1) % ShipCodes.Length;

    ShipPair(0f, 0f, 900f, 0f);

    var g = CreateGroup();
    GroupEnumUnitsSelected(g, p, null);
    var sel = FirstOfGroup(g);
    DestroyGroup(g);
    if (sel != null)
    {
      var sx = GetUnitX(sel);
      var sy = GetUnitY(sel);
      ShipPair(sx + 600f, sy, sx + 2000f, sy);
    }

    DisplayTimedTextToPlayer(p, 0, 0, 20, "|cffffcc00-ship|r " + I2S(_shipIdx + 1) + "/" + I2S(ShipCodes.Length) + ": "
      + ShipNames[_shipIdx] + " (" + ShipCodes[_shipIdx] + ")  -- A = re-skinned waygate, B = effect on hidden waygate"
      + (sel != null ? "; land pair placed beside your selected unit" : "; select a unit first for a walkable pair"));
  }

  private static void ShipPair(float ax, float ay, float bx, float by)
  {
    var a = CreateUnit(Player(PLAYER_NEUTRAL_PASSIVE), AiWorld.PortalUnitId, ax, ay, 0f);
    BlzSetUnitSkin(a, FourCC(ShipCodes[_shipIdx]));

    var b = CreateUnit(Player(PLAYER_NEUTRAL_PASSIVE), AiWorld.PortalUnitId, bx, by, 0f);
    SetUnitVertexColor(b, 255, 255, 255, 0);
    var fx = AddSpecialEffect(ShipModels[_shipIdx], bx, by);
    BlzSetSpecialEffectYaw(fx, 0f);

    // v34: land BESIDE the other end -- landing exactly on a waygate can bounce units straight back, which is
    // the likely cause of the "works only one way" result.
    WaygateSetDestination(a, bx + 300f, by - 300f);
    WaygateSetDestination(b, ax + 300f, ay - 300f);
    BlzSetUnitName(a, "Ship test A (skin)");
    BlzSetUnitName(b, "Ship test B (effect)");
    WaygateActivate(a, true);
    WaygateActivate(b, true);
    ShipUnits.Add(a);
    ShipUnits.Add(b);
    ShipFx.Add(fx);
    ShipLabel("A skin", ax, ay);
    ShipLabel("B effect", bx, by);
  }

  private static void ShipLabel(string text, float x, float y)
  {
    var tt = CreateTextTag();
    SetTextTagText(tt, text, 0.022f);
    SetTextTagPos(tt, x, y, 160f);
    SetTextTagColor(tt, 120, 200, 255, 255);
    SetTextTagVisibility(tt, true);
    SetTextTagPermanent(tt, false);
    SetTextTagLifespan(tt, 60f);
    SetTextTagFadepoint(tt, 55f);
  }
}
