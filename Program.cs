using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BatchProcessor.Models;

namespace BatchProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Batch Processor Started");
            
            // Your batch processing logic goes here
            ProcessBatch();
            
            Console.WriteLine("Batch Processing Complete");
        }
        
        static void ProcessBatch()
        {
            // Setup file logging
            var logPath = @"C:\Users\martin_s\OneDrive - Colony Brands\Personal\Scheduler\Files\log.txt";
            using var logWriter = new StreamWriter(logPath, false); // false = overwrite
            
            void Log(string message)
            {
                Console.WriteLine(message);
                logWriter.WriteLine(message);
                logWriter.Flush();
            }
            
            var teams = Team.Initialize();
            var weekDefinitions = WeekDefinitions.Initialize();
            var siteAvailability = SiteAvailability.LoadFromCsv(@"C:\Users\martin_s\OneDrive - Colony Brands\Personal\Scheduler\Files\Cleaned_Home_Hosting_Availability.csv");
            var teamAvailability = TeamAvailability.LoadFromCsv(@"C:\Users\martin_s\OneDrive - Colony Brands\Personal\Scheduler\Files\Away_Availability.csv", teams);
            
            Log($"Loaded {teams.Count} teams");
            Log($"Loaded {weekDefinitions.Count} week definitions");
            Log($"Loaded {siteAvailability.Count} site availability records");
            Log($"Loaded {teamAvailability.Count} team availability records");
            
            // Track bye assignments per week (weeks 2-7, max 8 teams per week)
            var byeCountByWeek = new Dictionary<int, int>();
            for (int week = 2; week <= 7; week++)
                byeCountByWeek[week] = 0;
            
            var schedule = Create823Schedule(teams, weekDefinitions, siteAvailability, teamAvailability);            
            schedule.AddRange(CreateSPRivalryWeekSchedule(teams, weekDefinitions, siteAvailability, teamAvailability, schedule.Count + 1));

            // Auto-schedule remaining games
            int nextGameNumber = schedule.Count + 1;
            foreach (var site in siteAvailability.OrderBy(s => s.Date).ThenBy(s => s.School))
            {
                var newGames = AssignGamesForDate(teams, weekDefinitions, teamAvailability, siteAvailability, site, nextGameNumber, schedule, byeCountByWeek, Log);
                schedule.AddRange(newGames);
                nextGameNumber += newGames.Count;
            }
            
            // Second pass: Fill remaining games for under-scheduled teams
            //nextGameNumber = schedule.Count + 1;
            //var additionalGames = FillRemainingGames(teams, weekDefinitions, siteAvailability, teamAvailability, schedule, nextGameNumber);
            //schedule.AddRange(additionalGames);
            
            // Third pass: Use unused hosting slots for under-scheduled teams
            //nextGameNumber = schedule.Count + 1;
            //var unusedSlotGames = UseUnusedHostingSlots(teams, weekDefinitions, siteAvailability, teamAvailability, schedule, nextGameNumber);
            //schedule.AddRange(unusedSlotGames);
            Log($"Created schedule with {schedule.Count} games");
            
            // Final step: Assign bye weeks to teams that still need them
            nextGameNumber = schedule.Count + 1;
            var teamsNeedingByes = teams.Where(t => t.Byes == 0 && (t.HomeGames + t.AwayGames + t.Byes) < 8).ToList();
            foreach (var team in teamsNeedingByes)
            {
                // Find a week where this team hasn't played yet
                var availableWeek = weekDefinitions.FirstOrDefault(w => 
                    !schedule.Any(g => g.WeekNumber == w.WeekNumber && (g.HostTeam == team.Name || g.AwayTeam == team.Name)));
                
                if (availableWeek != null)
                {
                    schedule.Add(new Schedule
                    {
                        GameNumber = nextGameNumber++,
                        HostTeam = team.Name,
                        AwayTeam = "bye",
                        Date = availableWeek.Date,
                        WeekNumber = availableWeek.WeekNumber
                    });
                    team.Byes += 1;
                }
            }
            
            // Debug: Show teams with insufficient games
            var underScheduledTeams = teams.Where(t => (t.HomeGames + t.AwayGames + t.Byes) < 8).ToList();
            if (underScheduledTeams.Any())
            {
                Log($"\n=== UNDER-SCHEDULED TEAMS ({underScheduledTeams.Count}) ===");
                foreach (var team in underScheduledTeams.OrderBy(t => t.HomeGames + t.AwayGames + t.Byes))
                {
                    Log($"{team.Name}: Home={team.HomeGames}, Away={team.AwayGames}, Byes={team.Byes}, Total={team.HomeGames + team.AwayGames + team.Byes}");
                }
            }
            
            // Debug: Show Sun P team games
            /*var sunPGames = schedule.OrderBy(g => g.Date).ToList();
            
            Console.WriteLine($"\n=== FULL SCHEDULE GAMES ({sunPGames.Count}) ===");
            foreach (var game in sunPGames)
            {
                Console.WriteLine($"Game {game.GameNumber}: {game.HostTeam} vs {game.AwayTeam} on {game.Date:MM/dd/yyyy} (Week {game.WeekNumber})");
            }*/
            
            DisplayGameSummary(teams, schedule, Log);
            
            Log("Processing batch...");
        }
        
        static List<Schedule> Create823Schedule(List<Team> teams, List<WeekDefinitions> weekDefinitions, List<SiteAvailability> siteAvailability, List<TeamAvailability> teamAvailability)
        {
            var schedule = new List<Schedule>();
            int gameNumber = 1;
            
            // Create kickoff games on 8/23
            var kickoffDate = new DateTime(2025, 8, 23);
            
            // Game 1: VeronaBlack7 vs Milton7
            schedule.Add(new Schedule
            {
                GameNumber = gameNumber++,
                HostTeam = "VeronaBlack7",
                AwayTeam = "Milton7",
                Date = kickoffDate,
                WeekNumber = 1
            });
            
            // Game 2: VeronaOrange7 vs MtHoreb7Red
            schedule.Add(new Schedule
            {
                GameNumber = gameNumber++,
                HostTeam = "VeronaOrange7",
                AwayTeam = "MtHoreb7Red",
                Date = kickoffDate,
                WeekNumber = 1
            });
            
            // Game 3: VeronaBlack8 vs Milton8
            schedule.Add(new Schedule
            {
                GameNumber = gameNumber++,
                HostTeam = "VeronaBlack8",
                AwayTeam = "Milton8",
                Date = kickoffDate,
                WeekNumber = 1
            });
            
            // Game 4: VeronaOrange8 vs MtHoreb8Red
            schedule.Add(new Schedule
            {
                GameNumber = gameNumber++,
                HostTeam = "VeronaOrange8",
                AwayTeam = "MtHoreb8Red",
                Date = kickoffDate,
                WeekNumber = 1
            });
            
            // Update site availability - mark Verona slot as taken on 8/23
            var veronaSite = siteAvailability.FirstOrDefault(s => s.School == "Verona" && s.Date == kickoffDate);
            if (veronaSite != null)
                veronaSite.SlotTaken = true;
            
            // Update team availability - mark away teams as unavailable ONLY on 8/23
            var awayTeamNames = new[] { "Milton7", "MtHoreb7Red", "Milton8", "MtHoreb8Red" };
            foreach (var teamName in awayTeamNames)
            {
                var team = teams.FirstOrDefault(t => t.Name == teamName);
                if (team != null)
                {
                    var availability = teamAvailability.Where(ta => ta.TeamID == team.TeamID && ta.Date == kickoffDate);
                    foreach (var avail in availability)
                        avail.IsAvailable = false;
                }
            }
            
            // Update team game counts for teams that actually played
            foreach (var game in schedule.Where(g => g.Date == kickoffDate))
            {
                var hostTeam = teams.FirstOrDefault(t => t.Name == game.HostTeam);
                var awayTeam = teams.FirstOrDefault(t => t.Name == game.AwayTeam);
                
                if (hostTeam != null) hostTeam.HomeGames += 1;
                if (awayTeam != null) awayTeam.AwayGames += 1;
            }
            
            return schedule;
        }
        
        static List<Schedule> CreateSPRivalryWeekSchedule(List<Team> teams, List<WeekDefinitions> weekDefinitions, List<SiteAvailability> siteAvailability, List<TeamAvailability> teamAvailability, int startingGameNumber)
        {
            var schedule = new List<Schedule>();
            int gameNumber = startingGameNumber;
            
            // Find available date in Week 4 for Sun Prairie hosting
            var week4Dates = weekDefinitions.Where(w => w.WeekNumber == 4).Select(w => w.Date).ToList();
            var availableDate = week4Dates.FirstOrDefault(date => 
                siteAvailability.Any(s => (s.School == "Sun P East" || s.School == "Sun P West") && s.Date == date && !s.SlotTaken));
            
            if (availableDate == default(DateTime))
            {
                Console.WriteLine("No available date found for Sun Prairie rivalry week");
                return schedule;
            }
            
            var sunPEastTeams = teams.Where(t => t.School == "Sun P East").ToList();
            var sunPWestTeams = teams.Where(t => t.School == "Sun P West").ToList();
            
            // Create rivalry games - Sun P East vs Sun P West
            for (int i = 0; i < Math.Min(sunPEastTeams.Count, sunPWestTeams.Count); i++)
            {
                schedule.Add(new Schedule
                {
                    GameNumber = gameNumber++,
                    HostTeam = sunPEastTeams[i].Name,
                    AwayTeam = sunPWestTeams[i].Name,
                    Date = availableDate,
                    WeekNumber = 4
                });
            }
            
            // Update site availability - mark Sun P East slot as taken
            var sunPEastSite = siteAvailability.FirstOrDefault(s => s.School == "Sun P East" && s.Date == availableDate);
            if (sunPEastSite != null)
                sunPEastSite.SlotTaken = true;
            
            // Update team availability - mark Sun P West teams as unavailable
            foreach (var team in sunPWestTeams)
            {
                var availability = teamAvailability.Where(ta => ta.TeamID == team.TeamID && ta.Date == availableDate);
                foreach (var avail in availability)
                    avail.IsAvailable = false;
            }
            
            // Update team game counts
            foreach (var game in schedule.Where(g => g.Date == availableDate))
            {
                var hostTeam = teams.FirstOrDefault(t => t.Name == game.HostTeam);
                var awayTeam = teams.FirstOrDefault(t => t.Name == game.AwayTeam);
                
                if (hostTeam != null) hostTeam.HomeGames += 1;
                if (awayTeam != null) awayTeam.AwayGames += 1;
            }
            
            return schedule;
        }
        
        static List<Schedule> AssignGamesForDate(List<Team> teams, List<WeekDefinitions> weekDefinitions, List<TeamAvailability> teamAvailability, List<SiteAvailability> siteAvailability, SiteAvailability site, int startingGameNumber, List<Schedule> existingSchedule = null, Dictionary<int, int> byeCountByWeek = null, Action<string> log = null)
        {
            var schedule = new List<Schedule>();
            if (site.School == "Sun P West")
            {

            }
            // Skip if site is already taken
                if (site.SlotTaken)
                {
                    log?.Invoke($"AssignGamesForDate: {site.School} on {site.Date:MM/dd/yyyy} - site already taken");
                    return schedule;
                }

            // Get week number for this date
            var weekNumber = weekDefinitions.FirstOrDefault(w => w.Date == site.Date)?.WeekNumber ?? 0;
            if (weekNumber == 0)
            {
                log?.Invoke($"AssignGamesForDate: {site.School} on {site.Date:MM/dd/yyyy} - no week number found");
                return schedule;
            }
            
            // Find host teams for this school that haven't exceeded limits and haven't played this week, prioritize teams with fewer total games
            var hostTeams = teams.Where(t => t.School == site.School && t.HomeGames < 4 && 
                (t.HomeGames + t.AwayGames) < 7 &&
                (existingSchedule == null || !existingSchedule.Any(g => g.WeekNumber == weekNumber && (g.HostTeam == t.Name || g.AwayTeam == t.Name)))
            ).OrderBy(t => t.HomeGames + t.AwayGames).ThenBy(t => t.HomeGames).ToList();
            if (!hostTeams.Any())
            {
                // Check if teams from this school were already scheduled this week
                var schoolTeamsThisWeek = teams.Where(t => t.School == site.School &&
                    existingSchedule != null && existingSchedule.Any(g => g.WeekNumber == weekNumber && (g.HostTeam == t.Name || g.AwayTeam == t.Name))).ToList();

                if (schoolTeamsThisWeek.Any())
                {
                    // Don't need this message at the moment, I get why this is happening.
                    /*
                    var scheduledGames = existingSchedule.Where(g => g.WeekNumber == weekNumber && 
                        schoolTeamsThisWeek.Any(t => g.HostTeam == t.Name || g.AwayTeam == t.Name)).ToList();
                    foreach (var game in scheduledGames)
                    {
                        Console.WriteLine($"AssignGamesForDate: {site.School} team already scheduled - {game.HostTeam} vs {game.AwayTeam}");
                    }
                    */
                }
                else
                {
                    log?.Invoke($"AssignGamesForDate: {site.School} on {site.Date:MM/dd/yyyy} - no eligible host teams");

                    // Debug: Show why no host teams are eligible
                    var allSchoolTeams = teams.Where(t => t.School == site.School).ToList();
                    log?.Invoke($"  Total teams at {site.School}: {allSchoolTeams.Count}");

                    var teamsAtHomeLimit = allSchoolTeams.Where(t => t.HomeGames >= 4).ToList();
                    log?.Invoke($"  Teams at home game limit (4+): {teamsAtHomeLimit.Count}");

                    var teamsAtTotalLimit = allSchoolTeams.Where(t => (t.HomeGames + t.AwayGames) >= 7).ToList();
                    log?.Invoke($"  Teams at total game limit (7+): {teamsAtTotalLimit.Count}");

                    foreach (var team in allSchoolTeams)
                    {
                        var reasons = new List<string>();
                        if (team.HomeGames >= 4) reasons.Add($"HomeGames={team.HomeGames}");
                        if ((team.HomeGames + team.AwayGames) >= 7) reasons.Add($"TotalGames={team.HomeGames + team.AwayGames}");
                        if (existingSchedule != null && existingSchedule.Any(g => g.WeekNumber == weekNumber && (g.HostTeam == team.Name || g.AwayTeam == team.Name)))
                            reasons.Add($"AlreadyPlayedWeek{weekNumber}");

                        log?.Invoke($"    {team.Name}: {(reasons.Any() ? string.Join(", ", reasons) : "ELIGIBLE")}");
                    }
                }
                return schedule;
            }
            

            // Check for teams with 2+ home games that need byes (weeks 3-7 only)
            if (byeCountByWeek != null && weekNumber >= 3 && weekNumber <= 7 && byeCountByWeek[weekNumber] < 8)
            {
                var teamsNeedingByes = hostTeams.Where(t => 
                    t.HomeGames >= 2 && 
                    t.Byes == 0 &&
                    (existingSchedule == null || !existingSchedule.Any(g => g.WeekNumber == weekNumber && (g.HostTeam == t.Name || g.AwayTeam == t.Name)))
                ).OrderBy(t => t.HomeGames + t.AwayGames).ToList();
                
                int byesAvailable = 8 - byeCountByWeek[weekNumber];
                foreach (var team in teamsNeedingByes.Take(byesAvailable))
                {
                    schedule.Add(new Schedule
                    {
                        GameNumber = startingGameNumber++,
                        HostTeam = team.Name,
                        AwayTeam = "bye",
                        Date = site.Date,
                        WeekNumber = weekNumber
                    });
                    team.Byes += 1;
                    byeCountByWeek[weekNumber]++;
                    hostTeams.Remove(team); // Remove from host candidates since they got a bye
                    log?.Invoke($"Bye assigned to team with 2+ home games: {team.Name} on {site.Date:MM/dd/yyyy} (Week {weekNumber})");
                }
            }
            
            // If a bye was created, return.  Don't block the site off this week though.
            if (schedule.Any())
            {
                //site.SlotTaken = schedule.Any(); // Mark site as taken if any byes were assigned
                return schedule;
            }
            
            // Randomly shuffle remaining hostTeams to add variability
            var random = new Random();
            hostTeams = hostTeams.OrderBy(x => random.Next()).ToList();
            
            // Find available away teams for this date that haven't played this week
            // Prioritize teams that CANNOT host this week (no hosting slots available)
            var availableAwayTeams = teams.Where(t => 
                t.School != site.School && 
                t.AwayGames < 4 &&
                (t.HomeGames + t.AwayGames) < 7 &&
                //teamAvailability.Any(ta => ta.TeamID == t.TeamID && ta.Date == site.Date && ta.IsAvailable) &&
                (existingSchedule == null || !existingSchedule.Any(g => g.WeekNumber == weekNumber && (g.HostTeam == t.Name || g.AwayTeam == t.Name)))
            ).OrderBy(t => 
                // First priority: teams that cannot host this week (no available hosting slots)
                siteAvailability.Any(s => s.School == t.School && s.Date == site.Date && !s.SlotTaken) ? 1 : 0
            ).ThenBy(t => t.HomeGames + t.AwayGames).ThenBy(t => t.AwayGames).ToList();

            var before = availableAwayTeams.Count();
            // Remove away teams that have the same school as any host team
            var hostSchools = hostTeams.Select(h => h.School).Distinct().ToList();
            availableAwayTeams = availableAwayTeams.Where(a => !hostSchools.Contains(a.School)).ToList();

            // Randomly shuffle hostTeams to add variability
            availableAwayTeams = availableAwayTeams.OrderBy(x => random.Next()).ToList();
            
            // If no away teams available, skip this site for now (don't create byes immediately)
            if (!availableAwayTeams.Any())
            {
                log?.Invoke($"AssignGamesForDate: {site.School} on {site.Date:MM/dd/yyyy} - no eligible away teams");

                return schedule;
            }
            
            // Create games - simple division matching without travel restrictions
            int gameNumber = startingGameNumber;
            
            foreach (var hostTeam in hostTeams)
            {
                var matchingAwayTeam = availableAwayTeams.FirstOrDefault(a => 
                    a.Division == hostTeam.Division && 
                    !schedule.Any(g => g.AwayTeam == a.Name) &&
                    // Prevent teams from playing each other multiple times
                    (existingSchedule == null || !existingSchedule.Any(g => 
                        (g.HostTeam == hostTeam.Name && g.AwayTeam == a.Name) ||
                        (g.HostTeam == a.Name && g.AwayTeam == hostTeam.Name)))
                );

                if (matchingAwayTeam != null)
                {
                    schedule.Add(new Schedule
                    {
                        GameNumber = gameNumber++,
                        HostTeam = hostTeam.Name,
                        AwayTeam = matchingAwayTeam.Name,
                        Date = site.Date,
                        WeekNumber = weekNumber
                    });
                    
                    
                    // Check for second game opportunity - another team from same away school vs another host team
                    var secondHostTeam = hostTeams.FirstOrDefault(h => 
                        h.Name != hostTeam.Name && 
                        h.HomeGames < 4 && 
                        (h.HomeGames + h.AwayGames) < 7 &&
                        !schedule.Any(g => g.HostTeam == h.Name));
                    var secondAwayTeam = availableAwayTeams.FirstOrDefault(a => 
                        a.School == matchingAwayTeam.School && 
                        a.Name != matchingAwayTeam.Name && 
                        a.AwayGames < 4 &&
                        (a.HomeGames + a.AwayGames) < 7 &&
                        a.Division == (secondHostTeam?.Division ?? "") &&
                        !schedule.Any(g => g.AwayTeam == a.Name));
                    
                    if (secondHostTeam != null && secondAwayTeam != null)
                    {
                        schedule.Add(new Schedule
                        {
                            GameNumber = gameNumber++,
                            HostTeam = secondHostTeam.Name,
                            AwayTeam = secondAwayTeam.Name,
                            Date = site.Date,
                            WeekNumber = weekNumber
                        });
                        log?.Invoke($"Second game created: {secondHostTeam.Name} vs {secondAwayTeam.Name} on {site.Date:MM/dd/yyyy}");
                    }
                    
                    
                    break;
                }
            }
            
            // Update data if games were created
            if (schedule.Any())
            {
                // Mark site as taken
                site.SlotTaken = true;

                // Mark away teams as unavailable
                foreach (var game in schedule)
                {
                    var awayTeam = teams.FirstOrDefault(t => t.Name == game.AwayTeam);
                    if (awayTeam != null)
                    {
                        var availability = teamAvailability.Where(ta => ta.TeamID == awayTeam.TeamID && ta.Date == site.Date);
                        foreach (var avail in availability)
                            avail.IsAvailable = false;
                    }
                }

                // Update game counts
                foreach (var game in schedule)
                {
                    var hostTeam = teams.FirstOrDefault(t => t.Name == game.HostTeam);
                    var awayTeam = teams.FirstOrDefault(t => t.Name == game.AwayTeam);

                    if (hostTeam != null) hostTeam.HomeGames += 1;
                    if (awayTeam != null) awayTeam.AwayGames += 1;
                }
            }
            else
            {
                Console.WriteLine($"AssignGamesForDate: No games created for {site.School} on {site.Date:MM/dd/yyyy} - no suitable matchups found");
            }
            foreach (var game in schedule.Where(t=> t.AwayTeam != "bye"))
            {
                var hostTeam = teams.FirstOrDefault(t => t.Name == game.HostTeam);
                var awayTeam = teams.FirstOrDefault(t => t.Name == game.AwayTeam);
                if (hostTeam.School == awayTeam.School)
                {
                    Console.WriteLine($"AssignGamesForDate: {site.School} cannot play same school!");
                }
            }
            return schedule;
        }

        static void DisplayGameSummary(List<Team> teams, List<Schedule> schedule, Action<string> log)
        {
            log("\n=== GAME SUMMARY BY TEAM ===");

            foreach (var team in teams.OrderBy(t => t.School).ThenBy(t => t.Name))
            {
                log($"{team.Name}: Home={team.HomeGames}, Away={team.AwayGames}, Byes={team.Byes}, Total={team.HomeGames + team.AwayGames + team.Byes}");
            }

            // Games and Byes count by Week and Division
            log("\n=== GAMES AND BYES BY WEEK AND DIVISION ===");
            
            var divisions = teams.Select(t => t.Division).Distinct().OrderBy(d => d).ToList();
            var weeks = schedule.Select(g => g.WeekNumber).Distinct().OrderBy(w => w).ToList();
            
            foreach (var week in weeks)
            {
                log($"\nWeek {week}:");
                foreach (var division in divisions)
                {
                    var games = schedule.Where(g => g.WeekNumber == week && g.AwayTeam != "bye").ToList();
                    var divisionGames = games.Where(g => 
                        teams.Any(t => t.Name == g.HostTeam && t.Division == division) ||
                        teams.Any(t => t.Name == g.AwayTeam && t.Division == division)
                    ).Count();
                    
                    var byes = schedule.Where(g => g.WeekNumber == week && g.AwayTeam == "bye").ToList();
                    var divisionByes = byes.Where(g => 
                        teams.Any(t => t.Name == g.HostTeam && t.Division == division)
                    ).Count();
                    
                    log($"  {division}: {divisionGames} games, {divisionByes} byes");
                }
            }
            
            log("\n=== SCHEDULED GAMES ===");
            foreach (var game in schedule.OrderBy(g => g.Date).ThenBy(g => g.GameNumber))
            {
                log($"Game {game.GameNumber}: {game.HostTeam} vs {game.AwayTeam} on {game.Date:MM/dd/yyyy} (Week {game.WeekNumber})");
            }
            

        }

    }
}