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
            var teams = Team.Initialize();
            var weekDefinitions = WeekDefinitions.Initialize();
            var siteAvailability = SiteAvailability.LoadFromCsv(@"C:\Users\martin_s\OneDrive - Colony Brands\Personal\Scheduler\Files\Cleaned_Home_Hosting_Availability.csv");
            var teamAvailability = TeamAvailability.LoadFromCsv(@"C:\Users\martin_s\OneDrive - Colony Brands\Personal\Scheduler\Files\Away_Availability.csv", teams);
            
            Console.WriteLine($"Loaded {teams.Count} teams");
            Console.WriteLine($"Loaded {weekDefinitions.Count} week definitions");
            Console.WriteLine($"Loaded {siteAvailability.Count} site availability records");
            Console.WriteLine($"Loaded {teamAvailability.Count} team availability records");
            
            var schedule = Create823Schedule(teams, weekDefinitions, siteAvailability, teamAvailability);            
            schedule.AddRange(CreateSPRivalryWeekSchedule(teams, weekDefinitions, siteAvailability, teamAvailability, schedule.Count + 1));

            // Auto-schedule remaining games
            int nextGameNumber = schedule.Count + 1;
            foreach (var site in siteAvailability.OrderBy(s => s.Date).ThenBy(s => s.School))
            {
                var newGames = AssignGamesForDate(teams, weekDefinitions, teamAvailability, siteAvailability, site, nextGameNumber, schedule);
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
            Console.WriteLine($"Created schedule with {schedule.Count} games");
            
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
                Console.WriteLine($"\n=== UNDER-SCHEDULED TEAMS ({underScheduledTeams.Count}) ===");
                foreach (var team in underScheduledTeams.OrderBy(t => t.HomeGames + t.AwayGames + t.Byes))
                {
                    Console.WriteLine($"{team.Name}: Home={team.HomeGames}, Away={team.AwayGames}, Byes={team.Byes}, Total={team.HomeGames + team.AwayGames + team.Byes}");
                }
            }
            
            // Debug: Show Sun P team games
            /*var sunPGames = schedule.OrderBy(g => g.Date).ToList();
            
            Console.WriteLine($"\n=== FULL SCHEDULE GAMES ({sunPGames.Count}) ===");
            foreach (var game in sunPGames)
            {
                Console.WriteLine($"Game {game.GameNumber}: {game.HostTeam} vs {game.AwayTeam} on {game.Date:MM/dd/yyyy} (Week {game.WeekNumber})");
            }*/
            
            DisplayGameSummary(teams, schedule);
            
            Console.WriteLine("Processing batch...");
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
        
        static List<Schedule> AssignGamesForDate(List<Team> teams, List<WeekDefinitions> weekDefinitions, List<TeamAvailability> teamAvailability, List<SiteAvailability> siteAvailability, SiteAvailability site, int startingGameNumber, List<Schedule> existingSchedule = null)
        {
            var schedule = new List<Schedule>();

            // Skip if site is already taken
            if (site.SlotTaken)
            {
                Console.WriteLine($"AssignGamesForDate: {site.School} on {site.Date:MM/dd/yyyy} - site already taken");
                return schedule;
            }

            // Get week number for this date
            var weekNumber = weekDefinitions.FirstOrDefault(w => w.Date == site.Date)?.WeekNumber ?? 0;
            if (weekNumber == 0)
            {
                Console.WriteLine($"AssignGamesForDate: {site.School} on {site.Date:MM/dd/yyyy} - no week number found");
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
                    var scheduledGames = existingSchedule.Where(g => g.WeekNumber == weekNumber && 
                        schoolTeamsThisWeek.Any(t => g.HostTeam == t.Name || g.AwayTeam == t.Name)).ToList();
                    foreach (var game in scheduledGames)
                    {
                        Console.WriteLine($"AssignGamesForDate: {site.School} team already scheduled - {game.HostTeam} vs {game.AwayTeam}");
                    }
                }
                else
                {
                    Console.WriteLine($"AssignGamesForDate: {site.School} on {site.Date:MM/dd/yyyy} - no eligible host teams");
                }
                return schedule;
            }
            
            // Randomly shuffle hostTeams to add variability
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
            
            // Randomly shuffle hostTeams to add variability
            availableAwayTeams = availableAwayTeams.OrderBy(x => random.Next()).ToList();
            
            // If no away teams available, skip this site for now (don't create byes immediately)
            if (!availableAwayTeams.Any())
            {
                Console.WriteLine($"AssignGamesForDate: {site.School} on {site.Date:MM/dd/yyyy} - no eligible away teams");

                return schedule;
            }
            
            // Create games - simple division matching without travel restrictions
            int gameNumber = startingGameNumber;
            
            foreach (var hostTeam in hostTeams)
            {
                var matchingAwayTeam = availableAwayTeams.FirstOrDefault(a => 
                    a.Division == hostTeam.Division && 
                    !schedule.Any(g => g.AwayTeam == a.Name)
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
                        Console.WriteLine($"Second game created: {secondHostTeam.Name} vs {secondAwayTeam.Name} on {site.Date:MM/dd/yyyy}");
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
            
            return schedule;
        }
        
        static void DisplayGameSummary(List<Team> teams, List<Schedule> schedule)
        {
            Console.WriteLine("\n=== GAME SUMMARY BY TEAM ===");
            
            foreach (var team in teams.OrderBy(t => t.School).ThenBy(t => t.Name))
            {
                Console.WriteLine($"{team.Name}: Home={team.HomeGames}, Away={team.AwayGames}, Byes={team.Byes}, Total={team.HomeGames + team.AwayGames + team.Byes}");
            }
            
            Console.WriteLine("\n=== SCHEDULED GAMES ===");
            foreach (var game in schedule.OrderBy(g => g.Date).ThenBy(g => g.GameNumber))
            {
                Console.WriteLine($"Game {game.GameNumber}: {game.HostTeam} vs {game.AwayTeam} on {game.Date:MM/dd/yyyy} (Week {game.WeekNumber})");
            }
        }

    }
}