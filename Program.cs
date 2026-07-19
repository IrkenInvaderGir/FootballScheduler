using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BatchProcessor.Models;

namespace BatchProcessor
{
    class Program
    {
        // --- 2026 season rules (Files/2026_ProgramSummary.csv, Files/2026_HostingCapacity_Long.csv) ---
        const string ByeMarker = "bye";
        const int GamesPerTeamTarget = 7;
        const int MaxHomeGames = 4;
        const int MaxAwayGames = 4;
        const int HomeGameSoftMinimum = 3; // soft rule, applies to every team including Sauk Prairie
        const int SunPrairieRivalryWeek = 3;
        const int AttemptCount = 60; // best-of-N: the greedy pass has meaningful run-to-run variance from random tiebreaks
        static readonly DateTime VeronaJamboreeDate = new DateTime(2026, 8, 22);

        // Verona asked for a better home/away balance than the last two years - small
        // priority nudge only, not a hard rule.
        static readonly HashSet<string> SchoolsPreferMoreHomeGames = new() { "Verona" };

        // No field lights, per their own survey submissions - prefer hosting earlier
        // in the season (more daylight) over the last couple weeks of October.
        static readonly HashSet<string> SchoolsAvoidLateHosting = new() { "Mt Horeb", "Oregon" };
        const int LateSeasonStartWeek = 7; // weeks 7-8 are "late"; 1-6 are fine

        // Holds all shared scheduling state so methods don't need long, easily-mismatched
        // parameter lists (a real source of bugs in the 2025 version of this file).
        private class SchedulingContext
        {
            public List<Team> Teams = new();
            public List<WeekDefinitions> Weeks = new();
            public List<SiteAvailability> Sites = new();
            public List<TeamAvailability> Availability = new();
            public List<SchoolProfile> Profiles = new();
            public List<Schedule> Schedule = new();
            public int NextGameNumber = 1;
            public Random Random = new();
            public Action<string> Log = _ => { };

            // When true, a team's no-lights late-season hosting restriction no longer
            // blocks them from being scheduled that week - used only by the last-resort
            // relaxation pass for teams still short of the game target after the normal
            // passes and the first swap-repair pass.
            public bool AllowSoftPreferenceOverride = false;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Batch Processor Started");
            ProcessBatch();
            Console.WriteLine("Batch Processing Complete");
        }

        // Assumes the process working directory is the project root (true for `dotnet run`
        // executed from there), since Files/ is a relative sibling of the .csproj.
        static void ProcessBatch()
        {
            var logPath = Path.Combine("Files", "2026_ScheduleLog.txt");
            using var logWriter = new StreamWriter(logPath, false);
            void Log(string message)
            {
                Console.WriteLine(message);
                logWriter.WriteLine(message);
                logWriter.Flush();
            }

            // Data loading is seed-independent, so it happens once and is reused
            // (read-only) across every attempt below. TeamID matching means
            // Team.Initialize() can be called fresh per attempt without affecting
            // which availability record belongs to which team.
            var weeks = WeekDefinitions.Initialize();
            var availability = TeamAvailability.LoadFromCsv(Path.Combine("Files", "2026_ProgramSummary.csv"), Team.Initialize());
            var profiles = SchoolProfile.LoadFromCsv(Path.Combine("Files", "2026_ProgramSummary.csv"));
            var pristineSites = SiteAvailability.LoadFromCsv(Path.Combine("Files", "2026_HostingCapacity_Long.csv"));

            Log($"Loaded {Team.Initialize().Count} teams, {weeks.Count} week-dates, {pristineSites.Count} site records, {availability.Count} team availability records, {profiles.Count} school profiles");
            Log("Note: school profiles (start/end times, travel notes) are loaded but not yet used by the scheduling logic below - Schedule has no time-of-day field.");

            int baseSeed = Environment.TickCount;
            Log($"Running {AttemptCount} scheduling attempts (base seed {baseSeed}), keeping the best result...");

            int bestSeed = baseSeed;
            var bestScore = (underScheduled: int.MaxValue, belowHomeMin: int.MaxValue, totalGames: -1);

            for (int attempt = 0; attempt < AttemptCount; attempt++)
            {
                int seed = baseSeed + attempt;
                var attemptCtx = RunAttempt(seed, weeks, availability, pristineSites, _ => { });
                var score = ScoreAttempt(attemptCtx);
                Log($"  Attempt seed {seed}: {score.totalGames} games, {score.underScheduled} under-scheduled, {score.belowHomeMin} below home minimum");

                if (IsBetterScore(score, bestScore))
                {
                    bestScore = score;
                    bestSeed = seed;
                }
            }

            Log($"\nBest seed: {bestSeed} ({bestScore.underScheduled} under-scheduled, {bestScore.belowHomeMin} below home minimum, {bestScore.totalGames} games) - regenerating with full logging below.\n");

            var ctx = RunAttempt(bestSeed, weeks, availability, pristineSites, Log);
            DisplaySummary(ctx);
            ReportStreakViolations(ctx);
            ReportSharedCoachViolations(ctx);
            ReportDoubleBookingViolations(ctx);

            var xlsxPath = Path.Combine("Files", "2026_Schedule.xlsx");
            XlsxWriter.Write(xlsxPath, new List<XlsxSheet>
            {
                BuildGradeGrid(ctx, "7", "7th Grade Schedule", "Alliance 7th Grade Schedule"),
                BuildGradeGrid(ctx, "8", "8th Grade Schedule", "Alliance 8th Grade Schedule"),
            });
            Log($"Wrote {xlsxPath} (7th Grade Schedule / 8th Grade Schedule tabs, matching the 2025 sheet layout).");
            Log("Note: game cells show matchup + date only, no kickoff time - the scheduler doesn't assign time slots, only date/opponent.");

            Log("Processing batch...");
        }

        // ------------------------------------------------------------------
        // Grade-grid export: reproduces the 2025 schedule sheet's layout (one row
        // per team, one column per week, "V/@ Opponent (date)" cells, a Dates
        // header/footer row, and a games/byes summary row) so this can be opened
        // directly or imported into Google Sheets in place of the old tabs.
        // 8 week columns, matching the 2025 sheet - there's no rescue week, so every
        // team's 7 games and 1 bye have to fit within weeks 1-8.
        // ------------------------------------------------------------------
        static XlsxSheet BuildGradeGrid(SchedulingContext ctx, string division, string sheetName, string title)
        {
            var teams = ctx.Teams.Where(t => t.Division == division).OrderBy(t => t.Name).ToList();
            var weekNumbers = Enumerable.Range(1, 8).ToList();

            var rows = new List<List<XlsxCell>>
            {
                new() { Cell(""), Cell(title) },
                new() { Cell(""), Cell("2026") }
            };

            var headerRow = new List<XlsxCell> { Cell(""), Cell("Team") };
            headerRow.AddRange(weekNumbers.Select(w => Cell($"Week {w}")));
            rows.Add(headerRow);

            var datesRow = new List<XlsxCell> { Cell(""), Cell("Dates") };
            datesRow.AddRange(weekNumbers.Select(w => Cell(FormatWeekDates(ctx, w, teams))));
            rows.Add(datesRow);

            foreach (var team in teams)
            {
                var row = new List<XlsxCell> { Cell(""), Cell(team.Name) };
                row.AddRange(weekNumbers.Select(w => FormatGameCell(ctx, team, w)));
                rows.Add(row);
            }

            rows.Add(datesRow);
            rows.Add(new List<XlsxCell>());

            var summaryRow = new List<XlsxCell> { Cell(""), Cell("") };
            summaryRow.AddRange(weekNumbers.Select(w => Cell(FormatWeekSummary(ctx, w, teams))));
            rows.Add(summaryRow);

            return new XlsxSheet { Name = sheetName, Rows = rows };
        }

        static XlsxCell Cell(string text, XlsxCellStyle style = XlsxCellStyle.Default) => new(text, style);

        static string FormatWeekDates(SchedulingContext ctx, int week, List<Team> divisionTeams)
        {
            var teamNames = new HashSet<string>(divisionTeams.Select(t => t.Name));
            var dates = ctx.Schedule.Where(g => g.WeekNumber == week && teamNames.Contains(g.HostTeam))
                .Select(g => g.Date).Distinct().OrderBy(d => d).ToList();
            return string.Join(" ", dates.Select(d => $"{DayAbbrev(d.DayOfWeek)}/{d.Day}"));
        }

        static string DayAbbrev(DayOfWeek d) => d switch
        {
            DayOfWeek.Sunday => "Su",
            DayOfWeek.Monday => "M",
            DayOfWeek.Tuesday => "T",
            DayOfWeek.Wednesday => "W",
            DayOfWeek.Thursday => "Th",
            DayOfWeek.Friday => "F",
            DayOfWeek.Saturday => "Sa",
            _ => "?"
        };

        static XlsxCell FormatGameCell(SchedulingContext ctx, Team team, int week)
        {
            var game = ctx.Schedule.FirstOrDefault(g => g.WeekNumber == week && (g.HostTeam == team.Name || g.AwayTeam == team.Name));
            if (game == null)
                return Cell("");

            if (game.AwayTeam == ByeMarker)
                return Cell($"V bye ({game.Date:MM/dd/yyyy})", XlsxCellStyle.Bye);

            bool isHost = game.HostTeam == team.Name;
            string opponent = isHost ? game.AwayTeam : game.HostTeam;
            string text = $"{(isHost ? "V" : "@")} {opponent} ({game.Date:MM/dd/yyyy})";
            return Cell(text, isHost ? XlsxCellStyle.Home : XlsxCellStyle.Away);
        }

        static string FormatWeekSummary(SchedulingContext ctx, int week, List<Team> divisionTeams)
        {
            var teamNames = new HashSet<string>(divisionTeams.Select(t => t.Name));
            var weekGames = ctx.Schedule.Where(g => g.WeekNumber == week && teamNames.Contains(g.HostTeam)).ToList();
            int byes = weekGames.Count(g => g.AwayTeam == ByeMarker);
            int games = weekGames.Count(g => g.AwayTeam != ByeMarker);
            return byes > 0 ? $"{games} Games {byes} Byes" : $"{games} Games";
        }

        static SchedulingContext RunAttempt(int seed, List<WeekDefinitions> weeks, List<TeamAvailability> availability, List<SiteAvailability> pristineSites, Action<string> log)
        {
            var ctx = new SchedulingContext
            {
                Teams = Team.Initialize(),
                Weeks = weeks,
                Sites = CloneSites(pristineSites),
                Availability = availability,
                Random = new Random(seed),
                Log = log
            };

            ScheduleVeronaJamboree(ctx);
            ScheduleSunPrairieRivalry(ctx);
            RunPriorityAssignmentPasses(ctx);
            RunGeneralAssignmentPasses(ctx);
            RunSwapRepairPasses(ctx, allowOverride: false);
            RelaxSoftPreferencesForStillShortTeams(ctx);
            RunSwapRepairPasses(ctx, allowOverride: true);
            AssignByes(ctx);

            return ctx;
        }

        // Last resort: a team that's still short after the first swap-repair pass gets
        // the no-lights late-season hosting restriction lifted - a team that would
        // otherwise stay short shouldn't lose a home game just to keep Mt Horeb/Oregon
        // out of October.
        static void RelaxSoftPreferencesForStillShortTeams(SchedulingContext ctx)
        {
            int stillShort = ctx.Teams.Count(t => (t.HomeGames + t.AwayGames) < GamesPerTeamTarget);
            if (stillShort == 0)
                return;

            ctx.Log($"{stillShort} team(s) still short after general assignment and swap repair - allowing late-season hosting to be used as a last resort (overriding that soft preference).");

            ctx.AllowSoftPreferenceOverride = true;
            try
            {
                const int maxPasses = 8;
                var sites = ctx.Sites.Where(s => s.Grade == null && s.WeekNumber <= 8)
                    .OrderBy(s => s.Date).ThenBy(s => ctx.Random.Next()).ToList();

                for (int pass = 1; pass <= maxPasses; pass++)
                {
                    bool anyScheduled = false;
                    foreach (var site in sites)
                    {
                        int before = ctx.Schedule.Count;
                        TryScheduleSite(ctx, site);
                        if (ctx.Schedule.Count > before)
                            anyScheduled = true;
                    }
                    if (!anyScheduled)
                        break;
                }
            }
            finally
            {
                ctx.AllowSoftPreferenceOverride = false;
            }
        }

        static int CountUnderScheduled(SchedulingContext ctx)
            => ctx.Teams.Count(t => (t.HomeGames + t.AwayGames) < GamesPerTeamTarget);

        // ------------------------------------------------------------------
        // Swap repair: the greedy passes above only ever add games, never revisit a
        // decision - once a flexible team (lots of able-nights) claims the one slot a
        // tightly-constrained team (e.g. Beaver Dam, Tuesday-only) actually needed, that
        // opportunity is gone for the rest of the attempt. Re-rolling the random seed
        // doesn't target that specific collision.
        //
        // This is a "min-conflicts" style local-search repair: for a team that's still
        // short with room left on both sides (i.e. it ran out of eligible opponents, not
        // capacity - see DisplaySummary's "room left on both sides" note), look for
        // another team that WOULD be an eligible opponent except that it's already busy
        // that week with someone else, and try displacing that existing game. The
        // displaced team gets one immediate shot at being rematched into the vacated
        // slot; whether or not that succeeds, the swap is only kept if it strictly
        // reduces the league-wide under-scheduled count, so this can never make the
        // overall schedule worse - only revisited decisions that pay off get kept.
        // ------------------------------------------------------------------
        // A team is repairable if it's under target and has room on at least one side -
        // not necessarily both. A team capped on away games but with home room left can
        // still be helped by the host-side branch of TryRepairForTeam (and vice versa);
        // requiring room on both sides here would wrongly exclude it from repair entirely.
        static bool IsSwapRepairable(Team t)
            => (t.HomeGames + t.AwayGames) < GamesPerTeamTarget
                && (t.HomeGames < MaxHomeGames || t.AwayGames < MaxAwayGames);

        static void RunSwapRepairPasses(SchedulingContext ctx, bool allowOverride)
        {
            if (!ctx.Teams.Any(IsSwapRepairable))
                return;

            bool previousOverride = ctx.AllowSoftPreferenceOverride;
            ctx.AllowSoftPreferenceOverride = allowOverride;
            try
            {
                const int maxPasses = 6;
                for (int pass = 1; pass <= maxPasses; pass++)
                {
                    bool anyRepaired = false;
                    var stillShort = ctx.Teams
                        .Where(IsSwapRepairable)
                        .OrderBy(t => AwayAvailabilityScarcity(ctx, t))
                        .ToList();

                    foreach (var team in stillShort)
                    {
                        if (TryRepairForTeam(ctx, team))
                            anyRepaired = true;
                    }

                    if (!anyRepaired)
                        break;
                }
            }
            finally
            {
                ctx.AllowSoftPreferenceOverride = previousOverride;
            }
        }

        static bool TryRepairForTeam(SchedulingContext ctx, Team shortTeam)
        {
            foreach (var asHost in new[] { true, false })
            {
                if (asHost && shortTeam.HomeGames >= MaxHomeGames) continue;
                if (!asHost && shortTeam.AwayGames >= MaxAwayGames) continue;

                for (int week = 1; week <= 8; week++)
                {
                    if (HasPlayedThisWeek(ctx, shortTeam.Name, week))
                        continue;

                    var sites = asHost
                        ? ctx.Sites.Where(s => s.School == shortTeam.School && s.WeekNumber == week && s.Grade == null && s.HasOpenSlot).ToList()
                        : ctx.Sites.Where(s => s.School != shortTeam.School && s.WeekNumber == week && s.Grade == null && s.HasOpenSlot).ToList();

                    foreach (var site in sites)
                    {
                        if (asHost)
                        {
                            if (!IsHostEligible(ctx, shortTeam, site))
                                continue;
                        }
                        else
                        {
                            if (SunPrairieHostConflict(ctx, site.School, site.Date)) continue;
                            if (shortTeam.School == site.School && !SameSchoolAllowed(shortTeam.School, site.School)) continue;
                            if (!IsAvailableOn(ctx, shortTeam, site.Date)) continue;
                            if (HasSharedCoachConflict(ctx, shortTeam, site.Date, site.School)) continue;
                            if (WouldCreateGameTypeStreak(ctx, shortTeam, week, asHost: false)) continue;
                        }

                        if (TryStealOpponentAt(ctx, shortTeam, asHost, site, week))
                            return true;
                    }
                }
            }
            return false;
        }

        // Looks for a team that's already busy this week with a game that isn't a
        // mandatory host pairing, but would otherwise be a perfectly eligible opponent
        // for `shortTeam` right now - then displaces that game so `shortTeam` can take
        // the slot instead. Tries the most flexible available teammate-of-opportunity
        // first (most able-nights), since a flexible team is the one most likely to
        // recover a replacement game of its own. Only committed if it provably helps.
        static bool TryStealOpponentAt(SchedulingContext ctx, Team shortTeam, bool asHost, SiteAvailability site, int week)
        {
            var opponentPool = ctx.Teams.Where(u =>
                    u.Division == shortTeam.Division &&
                    u.Name != shortTeam.Name &&
                    (u.School != shortTeam.School || SameSchoolAllowed(u.School, shortTeam.School)) &&
                    !HasPlayedEachOther(ctx, shortTeam.Name, u.Name) &&
                    IsAvailableOn(ctx, u, site.Date) &&
                    (asHost ? u.AwayGames < MaxAwayGames : u.HomeGames < MaxHomeGames) &&
                    !WouldCreateGameTypeStreak(ctx, u, week, asHost: !asHost) &&
                    !HasSharedCoachConflict(ctx, u, site.Date, asHost ? site.School : u.School) &&
                    HasPlayedThisWeek(ctx, u.Name, week))
                .OrderByDescending(u => AwayAvailabilityScarcity(ctx, u))
                .ToList();

            foreach (var u in opponentPool)
            {
                var existingGame = ctx.Schedule.First(g => g.WeekNumber == week && (g.HostTeam == u.Name || g.AwayTeam == u.Name));
                if (IsInMandatoryHostPair(existingGame.HostTeam))
                    continue; // never break a mandatory pair's shared hosting night

                var existingHost = ctx.Teams.First(t => t.Name == existingGame.HostTeam);
                var existingAway = ctx.Teams.First(t => t.Name == existingGame.AwayTeam);
                var displaced = existingHost.Name == u.Name ? existingAway : existingHost;
                var vacatedSite = ctx.Sites.FirstOrDefault(s => s.School == existingHost.School && s.Date == existingGame.Date && s.Grade == null);

                int baseline = CountUnderScheduled(ctx);

                RemoveGame(ctx, existingGame);
                var newGame = asHost
                    ? RecordGame(ctx, shortTeam, u, site.Date, week, site)
                    : RecordGame(ctx, u, shortTeam, site.Date, week, site);

                Schedule? mopUpGame = null;
                if (vacatedSite != null && vacatedSite.HasOpenSlot)
                {
                    int before = ctx.Schedule.Count;
                    TryScheduleOneGame(ctx, vacatedSite);
                    if (ctx.Schedule.Count > before)
                        mopUpGame = ctx.Schedule[^1];
                }

                // Every individual step above re-checked its own conflict rules live, but
                // the mop-up call re-enters the general matching logic mid-mutation, and
                // that's exactly the kind of ordering gap that let a shared-coach split
                // slip through during testing. Re-derive both invariants from scratch
                // before accepting, rather than trusting the chain of live pre-checks.
                if (CountUnderScheduled(ctx) < baseline
                    && !FindSharedCoachViolations(ctx).Any()
                    && !FindStreakViolations(ctx).Any()
                    && !FindDoubleBookingViolations(ctx).Any())
                {
                    ctx.Log(mopUpGame != null
                        ? $"Swap repair: {u.Name} moves from its week {week} game against {displaced.Name} to play {shortTeam.Name} instead; {displaced.Name} was rematched at {vacatedSite!.School}."
                        : $"Swap repair: {u.Name} moves from its week {week} game against {displaced.Name} to play {shortTeam.Name} instead; {displaced.Name} is open that week for a later pass to pick up.");
                    return true;
                }

                if (mopUpGame != null)
                    RemoveGame(ctx, mopUpGame);
                RemoveGame(ctx, newGame);
                AddGameBack(ctx, existingGame);
            }

            return false;
        }

        static List<SiteAvailability> CloneSites(List<SiteAvailability> source)
            => source.Select(s => new SiteAvailability
            {
                School = s.School,
                Date = s.Date,
                WeekNumber = s.WeekNumber,
                Capacity = s.Capacity,
                SlotsUsed = 0,
                Grade = s.Grade
            }).ToList();

        static (int underScheduled, int belowHomeMin, int totalGames) ScoreAttempt(SchedulingContext ctx)
        {
            int underScheduled = ctx.Teams.Count(t => (t.HomeGames + t.AwayGames) < GamesPerTeamTarget);
            int belowHomeMin = ctx.Teams.Count(t => t.HomeGames < HomeGameSoftMinimum);
            int totalGames = ctx.Schedule.Count(g => g.AwayTeam != ByeMarker);
            return (underScheduled, belowHomeMin, totalGames);
        }

        // Fewest under-scheduled teams wins; ties broken by fewest below the home-game
        // soft minimum, then by most total games played.
        static bool IsBetterScore(
            (int underScheduled, int belowHomeMin, int totalGames) candidate,
            (int underScheduled, int belowHomeMin, int totalGames) current)
        {
            if (candidate.underScheduled != current.underScheduled)
                return candidate.underScheduled < current.underScheduled;
            if (candidate.belowHomeMin != current.belowHomeMin)
                return candidate.belowHomeMin < current.belowHomeMin;
            return candidate.totalGames > current.totalGames;
        }

        // ------------------------------------------------------------------
        // Fixed date: Verona Jamboree (Sat 8/22) - date/site/time reservation only,
        // opponents are not specified by the 2026 survey so they're assigned here
        // under the normal matching rules. Bypasses day-of-week availability since
        // the weekly survey grid only covers Monday-Thursday.
        // ------------------------------------------------------------------
        static void ScheduleVeronaJamboree(SchedulingContext ctx)
        {
            var grade7Site = ctx.Sites.FirstOrDefault(s => s.School == "Verona" && s.Date == VeronaJamboreeDate && s.Grade == "7");
            var grade8Site = ctx.Sites.FirstOrDefault(s => s.School == "Verona" && s.Date == VeronaJamboreeDate && s.Grade == "8");

            if (grade7Site == null || grade8Site == null)
            {
                ctx.Log("Verona Jamboree site data not found - skipping Jamboree scheduling.");
                return;
            }

            foreach (var host in ctx.Teams.Where(t => t.School == "Verona" && t.Division == "7"))
            {
                if (!grade7Site.HasOpenSlot) break;
                var away = FindJamboreeOpponent(ctx, host);
                if (away == null)
                {
                    ctx.Log($"Verona Jamboree: no opponent found for {host.Name}");
                    continue;
                }
                RecordGame(ctx, host, away, VeronaJamboreeDate, 1, grade7Site);
            }

            var host8 = ctx.Teams.FirstOrDefault(t => t.School == "Verona" && t.Division == "8");
            if (host8 == null)
            {
                ctx.Log("Verona Jamboree: no 8th grade Verona team found on the roster.");
                return;
            }
            if (grade8Site.HasOpenSlot)
            {
                var away = FindJamboreeOpponent(ctx, host8);
                if (away == null)
                    ctx.Log($"Verona Jamboree: no opponent found for {host8.Name}");
                else
                    RecordGame(ctx, host8, away, VeronaJamboreeDate, 1, grade8Site);
            }
        }

        static Team? FindJamboreeOpponent(SchedulingContext ctx, Team host)
        {
            return ctx.Teams.Where(t => t.Division == host.Division
                    && t.School != host.School
                    && t.AwayGames < MaxAwayGames
                    && (t.HomeGames + t.AwayGames) < GamesPerTeamTarget
                    && !HasPlayedThisWeek(ctx, t.Name, 1)
                    && !HasPlayedEachOther(ctx, host.Name, t.Name))
                .OrderBy(t => t.HomeGames + t.AwayGames)
                .ThenBy(t => ctx.Random.Next())
                .FirstOrDefault();
        }

        // ------------------------------------------------------------------
        // Fixed date: Sun Prairie rivalry, Week 3 - Sun Pr West hosts Sun Pr East
        // (host direction flipped from 2025). Paired by division; any leftover
        // teams on either side just get a normal (non-rivalry) game later, since
        // Sun Pr East is blocked from hosting that week by SunPrairieHostConflict.
        // ------------------------------------------------------------------
        static void ScheduleSunPrairieRivalry(SchedulingContext ctx)
        {
            var westSites = ctx.Sites.Where(s => s.School == "Sun Pr West" && s.WeekNumber == SunPrairieRivalryWeek && s.Grade == null)
                .OrderBy(s => s.Date).ToList();

            if (!westSites.Any())
            {
                ctx.Log("Sun Prairie rivalry: no Sun Pr West week 3 site data found - skipping.");
                return;
            }

            foreach (var division in new[] { "7", "8" })
            {
                var westHosts = ctx.Teams.Where(t => t.School == "Sun Pr West" && t.Division == division
                    && t.HomeGames < MaxHomeGames && (t.HomeGames + t.AwayGames) < GamesPerTeamTarget).ToList();
                var eastAways = ctx.Teams.Where(t => t.School == "Sun Pr East" && t.Division == division
                    && t.AwayGames < MaxAwayGames && (t.HomeGames + t.AwayGames) < GamesPerTeamTarget).ToList();

                int pairs = Math.Min(westHosts.Count, eastAways.Count);
                for (int i = 0; i < pairs; i++)
                {
                    var host = westHosts[i];
                    var away = eastAways[i];
                    var site = westSites.FirstOrDefault(s => s.HasOpenSlot);
                    if (site == null)
                    {
                        ctx.Log($"Sun Prairie rivalry: no open West site left for {host.Name} vs {away.Name}");
                        continue;
                    }
                    RecordGame(ctx, host, away, site.Date, SunPrairieRivalryWeek, site);
                }

                if (eastAways.Count > westHosts.Count)
                    ctx.Log($"Sun Prairie rivalry (grade {division}): Sun Pr East has {eastAways.Count - westHosts.Count} more team(s) than Sun Pr West can host - they'll need a non-rivalry away game elsewhere.");
            }
        }

        // ------------------------------------------------------------------
        // Priority assignment: shared-coaching-staff schools (Beaver Dam, Waunakee,
        // Middleton, Sun Pr East) face the tightest combination of constraints in the
        // league - narrow availability plus the must-stay-together rule - so they're
        // consistently the ones left short once every other, more flexible school has
        // already claimed the same shared pool of opponents. Giving them first crack at
        // the season's opponent pool, before general assignment opens it up to
        // everyone, means they're not competing for an already-picked-over set of
        // matchups. Same multi-pass structure as general assignment, just scoped to
        // these schools' own sites first; general assignment still covers them again
        // afterward for anything left unfilled.
        // ------------------------------------------------------------------
        static void RunPriorityAssignmentPasses(SchedulingContext ctx)
        {
            var prioritySchools = new HashSet<string>(ctx.Teams.Where(t => t.SharedCoachingStaff).Select(t => t.School));
            if (!prioritySchools.Any())
                return;

            const int maxPasses = 8;
            var prioritySites = ctx.Sites.Where(s => s.Grade == null && s.WeekNumber <= 8 && prioritySchools.Contains(s.School))
                .OrderBy(s => s.Date).ThenBy(s => ctx.Random.Next()).ToList();

            for (int pass = 1; pass <= maxPasses; pass++)
            {
                bool anyScheduled = false;
                foreach (var site in prioritySites)
                {
                    int before = ctx.Schedule.Count;
                    TryScheduleSite(ctx, site);
                    if (ctx.Schedule.Count > before)
                        anyScheduled = true;
                }
                if (!anyScheduled)
                    break;
            }

            ctx.Log($"Priority assignment pass complete: {ctx.Schedule.Count} games scheduled after prioritizing {string.Join(", ", prioritySchools.OrderBy(s => s))}.");
        }

        // ------------------------------------------------------------------
        // General assignment: repeats over all remaining weeks 1-8 sites until a
        // full pass makes no progress. This replaces the 2025 code's single greedy
        // sweep plus its two dead "repair" methods with an actual repair mechanism.
        // ------------------------------------------------------------------
        static void RunGeneralAssignmentPasses(SchedulingContext ctx)
        {
            const int maxPasses = 8;
            // Random (not alphabetical) tiebreak on same-date sites: an alphabetical
            // tiebreak systematically favored "Sun Pr East" over "Sun Pr West" every
            // single week, since they usually have overlapping hostable days.
            var generalSites = ctx.Sites.Where(s => s.Grade == null && s.WeekNumber <= 8)
                .OrderBy(s => s.Date).ThenBy(s => ctx.Random.Next()).ToList();

            for (int pass = 1; pass <= maxPasses; pass++)
            {
                bool anyScheduled = false;
                foreach (var site in generalSites)
                {
                    int before = ctx.Schedule.Count;
                    TryScheduleSite(ctx, site);
                    if (ctx.Schedule.Count > before)
                        anyScheduled = true;
                }
                ctx.Log($"Assignment pass {pass}: schedule now has {ctx.Schedule.Count} games");
                if (!anyScheduled)
                    break;
            }
        }

        // Schools where hosting must happen in a fixed pair - both teams host together
        // (using 2 of the site's capacity) or neither hosts. Sauk pairs its two grades
        // together (officials won't work a single game); Waunakee and Beaver Dam pair
        // their two same-grade teams together (shared coaching staff can't be split
        // between two different hosting games at once). A team listed here never hosts
        // solo via the general matching path - see IsInMandatoryHostPair.
        static readonly (string School, string TeamA, string TeamB)[] MandatoryHostPairs =
        {
            ("Sauk Prairie", "SaukPrairie7", "SaukPrairie8"),
            ("Waunakee", "Waunakee7A", "Waunakee7B"),
            ("Waunakee", "Waunakee8A", "Waunakee8B"),
            ("Beaver Dam", "BeaverDam8A", "BeaverDam8B"),
        };

        static bool IsInMandatoryHostPair(string teamName)
            => MandatoryHostPairs.Any(p => p.TeamA == teamName || p.TeamB == teamName);

        static void TryScheduleSite(SchedulingContext ctx, SiteAvailability site)
        {
            if (!site.HasOpenSlot)
                return;

            if (SunPrairieHostConflict(ctx, site.School, site.Date))
                return;

            foreach (var pair in MandatoryHostPairs.Where(p => p.School == site.School).OrderBy(_ => ctx.Random.Next()))
                TryScheduleMandatoryPair(ctx, site, pair.TeamA, pair.TeamB);

            while (site.HasOpenSlot && TryScheduleOneGame(ctx, site)) { }

            if (site.HasOpenSlot)
                ctx.Log($"No eligible matchup for {site.School} on {site.Date:MM/dd/yyyy} (week {site.WeekNumber}) - {site.Capacity - site.SlotsUsed} slot(s) still open.");
        }

        static bool IsHostEligible(SchedulingContext ctx, Team t, SiteAvailability site)
            => t.HomeGames < MaxHomeGames
                && (t.HomeGames + t.AwayGames) < GamesPerTeamTarget
                && !IsTeamBusy(ctx, t.Name, site)
                && (ctx.AllowSoftPreferenceOverride || site.WeekNumber < LateSeasonStartWeek || !SchoolsAvoidLateHosting.Contains(t.School))
                && !HasSharedCoachConflict(ctx, t, site.Date, site.School)
                && !WouldCreateGameTypeStreak(ctx, t, site.WeekNumber, asHost: true);

        // A same-school, same-grade shared-coaching teammate is already hosting here
        // today - used to nudge (not require) schools like Middleton and Sun Pr East,
        // which aren't in MandatoryHostPairs, toward hosting together when possible.
        static bool HasTeammateHostingHere(SchedulingContext ctx, Team team, SiteAvailability site)
        {
            if (!team.SharedCoachingStaff)
                return false;
            return ctx.Teams.Any(t => t.School == team.School && t.Division == team.Division && t.Name != team.Name
                && ctx.Schedule.Any(g => g.Date == site.Date && g.HostTeam == t.Name));
        }

        static bool TryScheduleOneGame(SchedulingContext ctx, SiteAvailability site)
        {
            var hostCandidates = ctx.Teams.Where(t => t.School == site.School
                    && !IsInMandatoryHostPair(t.Name)
                    && IsHostEligible(ctx, t, site))
                .OrderByDescending(t => HasTeammateHostingHere(ctx, t, site) ? 1 : 0)
                .ThenByDescending(t => PrefersMoreHomeGames(t) ? 1 : 0)
                .ThenBy(t => t.HomeGames < HomeGameSoftMinimum ? 0 : 1)
                .ThenBy(t => t.HomeGames + t.AwayGames)
                .ThenBy(t => ctx.Random.Next())
                .ToList();

            foreach (var host in hostCandidates)
            {
                var away = FindEligibleAwayTeam(ctx, host, site);
                if (away != null)
                {
                    RecordGame(ctx, host, away, site.Date, site.WeekNumber, site);
                    return true;
                }
            }
            return false;
        }

        // Atomic: either both named teams get a home game recorded, or neither does.
        static void TryScheduleMandatoryPair(SchedulingContext ctx, SiteAvailability site, string teamNameA, string teamNameB)
        {
            if (site.Capacity - site.SlotsUsed < 2)
                return;

            var teamA = ctx.Teams.FirstOrDefault(t => t.Name == teamNameA);
            var teamB = ctx.Teams.FirstOrDefault(t => t.Name == teamNameB);
            if (teamA == null || teamB == null)
            {
                ctx.Log($"Mandatory host pair {teamNameA}/{teamNameB} not found on roster - skipping.");
                return;
            }

            if (!IsHostEligible(ctx, teamA, site) || !IsHostEligible(ctx, teamB, site))
                return;

            // awayB must exclude awayA - each pick is otherwise independent, so without
            // this a narrow-availability opponent pool (e.g. Beaver Dam's Tuesday-only
            // pool) could pick the SAME away team for both halves of the pair, double-
            // booking it into two games on the same date.
            var awayA = FindEligibleAwayTeam(ctx, teamA, site);
            if (awayA == null)
                return;
            var awayB = FindEligibleAwayTeam(ctx, teamB, site, exclude: awayA);
            if (awayB == null)
                return;

            RecordGame(ctx, teamA, awayA, site.Date, site.WeekNumber, site);
            RecordGame(ctx, teamB, awayB, site.Date, site.WeekNumber, site);
        }

        static Team? FindEligibleAwayTeam(SchedulingContext ctx, Team host, SiteAvailability site, Team? exclude = null)
        {
            return ctx.Teams.Where(t =>
                    t.Division == host.Division &&
                    t.Name != host.Name &&
                    (exclude == null || t.Name != exclude.Name) &&
                    (t.School != host.School || SameSchoolAllowed(t.School, host.School)) &&
                    t.AwayGames < MaxAwayGames &&
                    (t.HomeGames + t.AwayGames) < GamesPerTeamTarget &&
                    !IsTeamBusy(ctx, t.Name, site) &&
                    !HasPlayedEachOther(ctx, host.Name, t.Name) &&
                    IsAvailableOn(ctx, t, site.Date) &&
                    !HasSharedCoachConflict(ctx, t, site.Date, host.School) &&
                    !WouldCreateGameTypeStreak(ctx, t, site.WeekNumber, asHost: false))
                // Protect teams that haven't hosted at all yet and have an opening at their
                // own site this week from being poached as someone else's away pick first -
                // otherwise the most-constrained-first rule below backfires: a team with
                // very narrow availability (e.g. Beaver Dam 7, Tuesday-only) is also the
                // team every OTHER Tuesday host wants most as an away opponent, so it gets
                // claimed away every single week and never gets a turn at its own home slot.
                .OrderBy(t => (t.HomeGames == 0 && CanHostThisWeek(ctx, t, site.WeekNumber)) ? 1 : 0)
                .ThenBy(t => AwayAvailabilityScarcity(ctx, t)) // most-constrained-first: teams with fewer able nights get first claim on a rare opportunity
                .ThenBy(t => CanHostThisWeek(ctx, t, site.WeekNumber) ? 1 : 0) // prioritize teams with no home slot open this week
                .ThenBy(t => t.HomeGames + t.AwayGames)
                .ThenByDescending(t => IsPreferredOn(ctx, t, site.Date) ? 1 : 0)
                .ThenBy(t => ctx.Random.Next())
                .FirstOrDefault();
        }

        // ------------------------------------------------------------------
        // Byes: exactly one formal bye per team, within the 8-week season, on whichever
        // week ends up open - no reserved mid-season week anymore, the bye can land
        // any night. There's no rescue week to fall back on - a team with no open week
        // at all here is a genuine, unresolved scheduling shortfall (see the
        // under-scheduled report), not a second planned rest week.
        // ------------------------------------------------------------------
        static void AssignByes(SchedulingContext ctx)
        {
            var weeks = ctx.Weeks.Select(w => w.WeekNumber).Distinct().OrderBy(w => w).ToList();

            foreach (var team in ctx.Teams)
            {
                int byeWeek = weeks.FirstOrDefault(w => !HasPlayedThisWeek(ctx, team.Name, w));

                if (byeWeek == 0)
                {
                    ctx.Log($"{team.Name}: no open week available for a formal bye at all.");
                    continue;
                }

                var weekDate = ctx.Weeks.First(w => w.WeekNumber == byeWeek && w.SpecialLabel == null).Date;
                RecordBye(ctx, team, weekDate, byeWeek);
            }
        }

        // ------------------------------------------------------------------
        // Shared helpers
        // ------------------------------------------------------------------
        static Schedule RecordGame(SchedulingContext ctx, Team host, Team away, DateTime date, int week, SiteAvailability site)
        {
            var game = new Schedule
            {
                GameNumber = ctx.NextGameNumber++,
                HostTeam = host.Name,
                AwayTeam = away.Name,
                Date = date,
                WeekNumber = week
            };
            ctx.Schedule.Add(game);
            host.HomeGames++;
            away.AwayGames++;
            site.SlotsUsed++;
            return game;
        }

        // Inverse of RecordGame - used only by the swap-repair rollback path
        // (TryStealOpponentAt) to undo a displaced game, or restore it if a candidate
        // swap turned out not to help. Looks up the site by school+date rather than
        // taking it as a parameter since the caller may not have it on hand at revert time.
        static void RemoveGame(SchedulingContext ctx, Schedule game)
        {
            ctx.Schedule.Remove(game);
            var host = ctx.Teams.First(t => t.Name == game.HostTeam);
            var away = ctx.Teams.First(t => t.Name == game.AwayTeam);
            host.HomeGames--;
            away.AwayGames--;
            var site = ctx.Sites.FirstOrDefault(s => s.School == host.School && s.Date == game.Date && s.Grade == null);
            if (site != null) site.SlotsUsed--;
        }

        static void AddGameBack(SchedulingContext ctx, Schedule game)
        {
            ctx.Schedule.Add(game);
            var host = ctx.Teams.First(t => t.Name == game.HostTeam);
            var away = ctx.Teams.First(t => t.Name == game.AwayTeam);
            host.HomeGames++;
            away.AwayGames++;
            var site = ctx.Sites.FirstOrDefault(s => s.School == host.School && s.Date == game.Date && s.Grade == null);
            if (site != null) site.SlotsUsed++;
        }

        static void RecordBye(SchedulingContext ctx, Team team, DateTime date, int week)
        {
            ctx.Schedule.Add(new Schedule
            {
                GameNumber = ctx.NextGameNumber++,
                HostTeam = team.Name,
                AwayTeam = ByeMarker,
                Date = date,
                WeekNumber = week
            });
            team.Byes++;
        }

        static bool PrefersMoreHomeGames(Team t) => SchoolsPreferMoreHomeGames.Contains(t.School) && t.HomeGames < MaxHomeGames;

        static bool HasPlayedThisWeek(SchedulingContext ctx, string teamName, int week)
            => ctx.Schedule.Any(g => g.WeekNumber == week && (g.HostTeam == teamName || g.AwayTeam == teamName));

        // At most one game per team per week - the only cadence now, with no rescue
        // week left to allow a team to play more than once in the same week.
        static bool IsTeamBusy(SchedulingContext ctx, string teamName, SiteAvailability site)
            => HasPlayedThisWeek(ctx, teamName, site.WeekNumber);

        static bool HasPlayedEachOther(SchedulingContext ctx, string a, string b)
            => ctx.Schedule.Any(g => (g.HostTeam == a && g.AwayTeam == b) || (g.HostTeam == b && g.AwayTeam == a));

        // Blocks a 4th consecutive home (or away) game. "Consecutive" is by calendar
        // week number - a bye or an open (unscheduled) week breaks the streak, same as
        // how a real season reads. Only sees whatever's already in ctx.Schedule at
        // decision time, so it's a best-effort check, not a global guarantee: a later
        // pass filling in an earlier week can't retroactively undo a streak that was
        // already locked in by an earlier decision.
        static bool WouldCreateGameTypeStreak(SchedulingContext ctx, Team team, int week, bool asHost)
        {
            const int maxConsecutive = 3;
            int streak = 1;

            for (int w = week - 1; w >= 1; w--)
            {
                var g = ctx.Schedule.FirstOrDefault(x => x.WeekNumber == w && (x.HostTeam == team.Name || x.AwayTeam == team.Name));
                if (g == null || g.AwayTeam == ByeMarker || (g.HostTeam == team.Name) != asHost)
                    break;
                streak++;
            }
            for (int w = week + 1; w <= 8; w++)
            {
                var g = ctx.Schedule.FirstOrDefault(x => x.WeekNumber == w && (x.HostTeam == team.Name || x.AwayTeam == team.Name));
                if (g == null || g.AwayTeam == ByeMarker || (g.HostTeam == team.Name) != asHost)
                    break;
                streak++;
            }

            return streak > maxConsecutive;
        }

        static bool SameSchoolAllowed(string schoolA, string schoolB)
            => (schoolA == "Sun Pr East" && schoolB == "Sun Pr West") || (schoolA == "Sun Pr West" && schoolB == "Sun Pr East");

        // Sun Pr East and Sun Pr West share officials and cannot both host on the same
        // day ("cannot host games at the same time on a given day" per Sun Pr West's
        // survey submission - this is a day-level constraint, not a week-level one;
        // an earlier week-level version of this check let whichever school got
        // processed first claim hosting rights for the entire week, every week).
        static bool SunPrairieHostConflict(SchedulingContext ctx, string school, DateTime date)
        {
            if (school != "Sun Pr East" && school != "Sun Pr West")
                return false;

            var otherSchool = school == "Sun Pr East" ? "Sun Pr West" : "Sun Pr East";
            return ctx.Schedule.Any(g => g.Date == date && ctx.Teams.Any(t => t.Name == g.HostTeam && t.School == otherSchool));
        }

        static bool IsAvailableOn(SchedulingContext ctx, Team t, DateTime date)
        {
            var ta = ctx.Availability.FirstOrDefault(a => a.TeamID == t.TeamID);
            return ta != null && ta.IsAvailable(date);
        }

        // Fewer able-nights means fewer future opportunities to ever get matched - a
        // classic "most constrained variable first" tiebreak. Without this, a flexible
        // team (available every weeknight) could take a slot a narrow team (e.g.
        // Tuesday-only) genuinely needed and had no other shot at.
        static int AwayAvailabilityScarcity(SchedulingContext ctx, Team t)
            => ctx.Availability.FirstOrDefault(a => a.TeamID == t.TeamID)?.AbleNights.Count ?? int.MaxValue;

        static bool IsPreferredOn(SchedulingContext ctx, Team t, DateTime date)
        {
            var ta = ctx.Availability.FirstOrDefault(a => a.TeamID == t.TeamID);
            return ta != null && ta.IsPreferred(date);
        }

        static bool CanHostThisWeek(SchedulingContext ctx, Team t, int week)
            => ctx.Sites.Any(s => s.School == t.School && s.WeekNumber == week && s.Grade == null && s.HasOpenSlot);

        // A team whose school shares a coaching staff at this grade level must be at
        // the SAME physical site as any same-grade teammate playing on that SAME DATE -
        // the coach can't be in two places at once at the same moment. Different nights
        // within the same week are fine: an A/B school's shared staff can coach team A
        // Monday and team B Tuesday without conflict, since those are two separate
        // moments in time, not a simultaneous double-booking. The one case that's never
        // a conflict is two teammates BOTH hosting on the same date, since a team's home
        // games are always at its own school's site - "both host" naturally means "both
        // at the same site." A teammate with a bye imposes no constraint.
        static bool HasSharedCoachConflict(SchedulingContext ctx, Team team, DateTime date, string proposedSiteSchool)
        {
            if (!team.SharedCoachingStaff)
                return false;

            var teammates = ctx.Teams.Where(t => t.School == team.School && t.Division == team.Division && t.Name != team.Name);
            foreach (var mate in teammates)
            {
                var mateGame = ctx.Schedule.FirstOrDefault(g => g.Date == date && (g.HostTeam == mate.Name || g.AwayTeam == mate.Name));
                if (mateGame == null || mateGame.AwayTeam == ByeMarker)
                    continue; // teammate isn't playing that date or has a bye - no constraint

                var mateSiteSchool = ctx.Teams.First(t => t.Name == mateGame.HostTeam).School;
                if (mateSiteSchool != proposedSiteSchool)
                    return true;
            }
            return false;
        }

        // Re-derives whether WouldCreateGameTypeStreak actually held for the current
        // schedule (weeks 1-8, the whole season now). It's a best-effort check applied
        // at decision time, not a globally-enforced invariant, so this confirms there's
        // no gap between intent and outcome rather than assuming one. Used both by the
        // final post-hoc report and, mid-attempt, as a hard gate on swap-repair moves
        // (see TryStealOpponentAt) - the swap machinery's own live pre-checks are
        // evaluated at one moment in a chain of mutations, so this full re-derivation is
        // the only way to be certain a move didn't introduce a violation somewhere else.
        static List<string> FindStreakViolations(SchedulingContext ctx)
        {
            var violations = new List<string>();

            foreach (var team in ctx.Teams)
            {
                int streak = 0;
                bool? currentType = null;

                for (int w = 1; w <= 8; w++)
                {
                    var g = ctx.Schedule.FirstOrDefault(x => x.WeekNumber == w && (x.HostTeam == team.Name || x.AwayTeam == team.Name));
                    if (g == null || g.AwayTeam == ByeMarker)
                    {
                        streak = 0;
                        currentType = null;
                        continue;
                    }

                    bool isHost = g.HostTeam == team.Name;
                    streak = currentType == isHost ? streak + 1 : 1;
                    currentType = isHost;

                    if (streak > 3)
                        violations.Add($"{team.Name}: {streak} consecutive {(isHost ? "home" : "away")} games ending week {w}");
                }
            }

            return violations;
        }

        static void ReportStreakViolations(SchedulingContext ctx)
        {
            var violations = FindStreakViolations(ctx);
            ctx.Log(violations.Any()
                ? $"\n=== STREAK CHECK: {violations.Count} VIOLATION(S) FOUND ===\n{string.Join("\n", violations)}"
                : "\nStreak check: no team has more than 3 consecutive home or away games.");
        }

        // Re-derives whether HasSharedCoachConflict actually held for the current
        // schedule: for every shared-coaching-staff school, no two same-grade teammates
        // should ever be at different physical sites on the same DATE - covers away-vs-
        // different-away and home-vs-away alike. Different dates within the same week
        // are fine (an A/B school's shared staff can coach one team Monday and the other
        // Tuesday). Two teammates both hosting is never a violation (always the same
        // site); a teammate with a bye imposes no constraint. See FindStreakViolations
        // for why this is a full re-derivation rather than trusting the live pre-checks
        // alone - it's also used as a swap-repair gate.
        static List<string> FindSharedCoachViolations(SchedulingContext ctx)
        {
            var violations = new List<string>();
            var groups = ctx.Teams.Where(t => t.SharedCoachingStaff)
                .GroupBy(t => new { t.School, t.Division });

            foreach (var group in groups)
            {
                var teams = group.ToList();
                var gamesByDate = ctx.Schedule
                    .Where(g => g.AwayTeam != ByeMarker && teams.Any(t => t.Name == g.HostTeam || t.Name == g.AwayTeam))
                    .GroupBy(g => g.Date);

                foreach (var dateGroup in gamesByDate)
                {
                    // Map each distinct teammate that played this date (host or away) to
                    // the site-school(s) of their game(s) - only a violation when 2+
                    // different teammates end up at different site-schools.
                    var teamSites = new Dictionary<string, HashSet<string>>();
                    foreach (var g in dateGroup)
                    {
                        var siteSchool = ctx.Teams.First(t => t.Name == g.HostTeam).School;
                        foreach (var teamName in new[] { g.HostTeam, g.AwayTeam })
                        {
                            if (!teams.Any(t => t.Name == teamName))
                                continue;
                            if (!teamSites.TryGetValue(teamName, out var set))
                                teamSites[teamName] = set = new HashSet<string>();
                            set.Add(siteSchool);
                        }
                    }

                    if (teamSites.Count < 2)
                        continue;

                    var siteSchools = teamSites.Values.SelectMany(s => s).Distinct().ToList();
                    if (siteSchools.Count > 1)
                        violations.Add($"{group.Key.School} grade {group.Key.Division} on {dateGroup.Key:MM/dd/yyyy}: teammates split across {string.Join(", ", siteSchools)}");
                }
            }

            return violations;
        }

        static void ReportSharedCoachViolations(SchedulingContext ctx)
        {
            var violations = FindSharedCoachViolations(ctx);
            ctx.Log(violations.Any()
                ? $"\n=== SHARED COACH CHECK: {violations.Count} VIOLATION(S) FOUND ===\n{string.Join("\n", violations)}"
                : "\nShared coach check: no shared-coaching-staff teammates were split across sites on the same date.");
        }

        // Re-derives that no team is booked into two games on the same date. This is
        // already guaranteed structurally by IsTeamBusy (blocks a second game the same
        // week), but TryScheduleMandatoryPair picks each half of a host pair's away
        // opponent independently, so a narrow-availability opponent pool could pick the
        // SAME away team for both halves without either individual check ever noticing -
        // exactly the class of bug this exists to catch.
        static List<string> FindDoubleBookingViolations(SchedulingContext ctx)
        {
            var violations = new List<string>();
            var gamesByTeamAndDate = new Dictionary<(string Team, DateTime Date), int>();

            foreach (var g in ctx.Schedule.Where(g => g.AwayTeam != ByeMarker))
            {
                foreach (var teamName in new[] { g.HostTeam, g.AwayTeam })
                {
                    var key = (teamName, g.Date);
                    gamesByTeamAndDate[key] = gamesByTeamAndDate.TryGetValue(key, out var count) ? count + 1 : 1;
                }
            }

            foreach (var entry in gamesByTeamAndDate.Where(kv => kv.Value > 1))
                violations.Add($"{entry.Key.Team} is booked into {entry.Value} games on {entry.Key.Date:MM/dd/yyyy}");

            return violations;
        }

        static void ReportDoubleBookingViolations(SchedulingContext ctx)
        {
            var violations = FindDoubleBookingViolations(ctx);
            ctx.Log(violations.Any()
                ? $"\n=== DOUBLE-BOOKING CHECK: {violations.Count} VIOLATION(S) FOUND ===\n{string.Join("\n", violations)}"
                : "\nDouble-booking check: no team is booked into more than one game on the same date.");
        }

        // ------------------------------------------------------------------
        // Output: split by grade per REQUIREMENTS.md ("two schedules - one per grade").
        // ------------------------------------------------------------------
        static void DisplaySummary(SchedulingContext ctx)
        {
            ctx.Log("\n=== GAME SUMMARY BY TEAM ===");
            foreach (var team in ctx.Teams.OrderBy(t => t.School).ThenBy(t => t.Name))
                ctx.Log($"{team.Name}: Home={team.HomeGames}, Away={team.AwayGames}, Byes={team.Byes}, Total={team.HomeGames + team.AwayGames + team.Byes}");

            var underScheduled = ctx.Teams.Where(t => (t.HomeGames + t.AwayGames) < GamesPerTeamTarget).ToList();
            if (underScheduled.Any())
            {
                ctx.Log($"\n=== UNDER-SCHEDULED TEAMS ({underScheduled.Count}) - fewer than {GamesPerTeamTarget} games ===");
                foreach (var team in underScheduled.OrderBy(t => t.HomeGames + t.AwayGames))
                {
                    var notes = new List<string>();
                    if (team.HomeGames >= MaxHomeGames) notes.Add("hit home cap");
                    if (team.AwayGames >= MaxAwayGames) notes.Add("hit away cap");
                    if (!notes.Any()) notes.Add("room left on both sides - ran out of eligible matchups, not capacity");
                    ctx.Log($"{team.Name}: Home={team.HomeGames}, Away={team.AwayGames}, Byes={team.Byes}, Total={team.HomeGames + team.AwayGames + team.Byes} [{string.Join(", ", notes)}]");
                }
            }

            var belowHomeMinimum = ctx.Teams.Where(t => t.HomeGames < HomeGameSoftMinimum).ToList();
            if (belowHomeMinimum.Any())
            {
                ctx.Log($"\n=== BELOW {HomeGameSoftMinimum}-HOME-GAME SOFT MINIMUM ({belowHomeMinimum.Count}) ===");
                foreach (var team in belowHomeMinimum.OrderBy(t => t.HomeGames))
                    ctx.Log($"{team.Name}: Home={team.HomeGames}");
            }

            foreach (var division in new[] { "7", "8" })
            {
                ctx.Log($"\n=== GRADE {division} SCHEDULE ===");
                var divisionTeamNames = new HashSet<string>(ctx.Teams.Where(t => t.Division == division).Select(t => t.Name));
                var games = ctx.Schedule.Where(g => divisionTeamNames.Contains(g.HostTeam)).OrderBy(g => g.Date).ThenBy(g => g.GameNumber);
                foreach (var game in games)
                    ctx.Log($"Game {game.GameNumber}: {game.HostTeam} vs {game.AwayTeam} on {game.Date:MM/dd/yyyy} (Week {game.WeekNumber})");
            }
        }
    }
}
