using System.Collections.Generic;

namespace BatchProcessor.Models
{
    public class Team
    {
        public int TeamID { get; set; }
        public string School { get; set; } = "";
        public string Name { get; set; } = "";
        public string Division { get; set; } = ""; // "7" or "8"

        // True when this school's teams at this grade level share a coaching staff
        // and therefore cannot play at different sites at the same time (2026 survey).
        public bool SharedCoachingStaff { get; set; }

        public int HomeGames { get; set; }
        public int AwayGames { get; set; }
        public int Byes { get; set; }

        // 2026 roster. School names match the "School" column used in
        // Files/2026_ProgramSummary.csv and Files/2026_HostingCapacity_Long.csv exactly,
        // so team/site/availability data can be joined on School without a mapping table.
        // Naming convention: School + GradeLevel, with a trailing A/B/... letter appended
        // only when a school fields more than one team at that grade level.
        public static List<Team> Initialize()
        {
            return new List<Team>
            {
                new Team { TeamID = 1, School = "Reedsburg", Name = "Reedsburg7", Division = "7" },
                new Team { TeamID = 2, School = "Reedsburg", Name = "Reedsburg8", Division = "8" },

                new Team { TeamID = 3, School = "Mt Horeb", Name = "MtHoreb7A", Division = "7" },
                new Team { TeamID = 4, School = "Mt Horeb", Name = "MtHoreb7B", Division = "7" },
                new Team { TeamID = 5, School = "Mt Horeb", Name = "MtHoreb8A", Division = "8" },
                new Team { TeamID = 6, School = "Mt Horeb", Name = "MtHoreb8B", Division = "8" },

                new Team { TeamID = 7, School = "Stoughton", Name = "Stoughton7", Division = "7" },
                new Team { TeamID = 8, School = "Stoughton", Name = "Stoughton8", Division = "8" },

                new Team { TeamID = 9, School = "Waunakee", Name = "Waunakee7A", Division = "7", SharedCoachingStaff = true },
                new Team { TeamID = 10, School = "Waunakee", Name = "Waunakee7B", Division = "7", SharedCoachingStaff = true },
                new Team { TeamID = 11, School = "Waunakee", Name = "Waunakee8A", Division = "8", SharedCoachingStaff = true },
                new Team { TeamID = 12, School = "Waunakee", Name = "Waunakee8B", Division = "8", SharedCoachingStaff = true },

                new Team { TeamID = 13, School = "Middleton", Name = "Middleton7", Division = "7" },
                new Team { TeamID = 14, School = "Middleton", Name = "Middleton8A", Division = "8", SharedCoachingStaff = true },
                new Team { TeamID = 15, School = "Middleton", Name = "Middleton8B", Division = "8", SharedCoachingStaff = true },

                new Team { TeamID = 16, School = "Beaver Dam", Name = "BeaverDam7", Division = "7" },
                new Team { TeamID = 17, School = "Beaver Dam", Name = "BeaverDam8A", Division = "8", SharedCoachingStaff = true },
                new Team { TeamID = 18, School = "Beaver Dam", Name = "BeaverDam8B", Division = "8", SharedCoachingStaff = true },

                new Team { TeamID = 19, School = "Sun Pr West", Name = "SunPrWest7", Division = "7" },
                new Team { TeamID = 20, School = "Sun Pr West", Name = "SunPrWest8A", Division = "8" },
                new Team { TeamID = 21, School = "Sun Pr West", Name = "SunPrWest8B", Division = "8" },

                new Team { TeamID = 22, School = "Lodi", Name = "Lodi7", Division = "7" },
                new Team { TeamID = 23, School = "Lodi", Name = "Lodi8", Division = "8" },

                new Team { TeamID = 24, School = "Verona", Name = "Verona7A", Division = "7" },
                new Team { TeamID = 25, School = "Verona", Name = "Verona7B", Division = "7" },
                new Team { TeamID = 26, School = "Verona", Name = "Verona8", Division = "8" },

                new Team { TeamID = 27, School = "Portage", Name = "Portage7", Division = "7" },
                new Team { TeamID = 28, School = "Portage", Name = "Portage8", Division = "8" },

                new Team { TeamID = 29, School = "Monona Grove", Name = "MononaGrove7", Division = "7" },
                new Team { TeamID = 30, School = "Monona Grove", Name = "MononaGrove8", Division = "8" },

                new Team { TeamID = 31, School = "Milton", Name = "Milton7", Division = "7" },
                new Team { TeamID = 32, School = "Milton", Name = "Milton8", Division = "8" },

                new Team { TeamID = 33, School = "Sun Pr East", Name = "SunPrEast7A", Division = "7", SharedCoachingStaff = true },
                new Team { TeamID = 34, School = "Sun Pr East", Name = "SunPrEast7B", Division = "7", SharedCoachingStaff = true },
                new Team { TeamID = 35, School = "Sun Pr East", Name = "SunPrEast8", Division = "8", SharedCoachingStaff = true },

                new Team { TeamID = 36, School = "Oregon", Name = "Oregon7", Division = "7" },
                new Team { TeamID = 37, School = "Oregon", Name = "Oregon8", Division = "8" },

                new Team { TeamID = 38, School = "Sauk Prairie", Name = "SaukPrairie7", Division = "7" },
                new Team { TeamID = 39, School = "Sauk Prairie", Name = "SaukPrairie8", Division = "8" },
            };
        }
    }
}
