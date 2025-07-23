using System.Collections.Generic;

namespace BatchProcessor.Models
{
    public class Team
    {
        public int TeamID { get; set; }
        public string School { get; set; }
        public string Name { get; set; }
        public string Division { get; set; }
        public int HomeGames { get; set; }
        public int AwayGames { get; set; }
        public int Byes { get; set; }
        
        public static List<Team> Initialize()
        {
            return new List<Team>
            {
                new Team { TeamID = 1, School = "Sun P East", Name = "SunPrEastRed7", Division = "7" },
                new Team { TeamID = 2, School = "Sun P East", Name = "SunPrEastRed8", Division = "8" },
                new Team { TeamID = 3, School = "Oregon", Name = "Oregon7", Division = "7" },
                new Team { TeamID = 4, School = "Oregon", Name = "Oregon8", Division = "8" },
                new Team { TeamID = 5, School = "Verona", Name = "VeronaBlack7", Division = "7" },
                new Team { TeamID = 6, School = "Verona", Name = "VeronaOrange7", Division = "7" },
                new Team { TeamID = 7, School = "Verona", Name = "VeronaBlack8", Division = "8" },
                new Team { TeamID = 8, School = "Verona", Name = "VeronaOrange8", Division = "8" },
                new Team { TeamID = 9, School = "Lodi", Name = "Lodi7", Division = "7" },
                new Team { TeamID = 10, School = "Lodi", Name = "Lodi8", Division = "8" },
                new Team { TeamID = 11, School = "Beaver Dam", Name = "BeaverD7", Division = "7" },
                new Team { TeamID = 12, School = "Beaver Dam", Name = "BeaverD8", Division = "8" },
                new Team { TeamID = 13, School = "Middleton", Name = "MiddleRed7", Division = "7" },
                new Team { TeamID = 14, School = "Middleton", Name = "MiddleWhite7", Division = "7" },
                new Team { TeamID = 15, School = "Middleton", Name = "MiddleRed8", Division = "8" },
                new Team { TeamID = 16, School = "Middleton", Name = "MiddleWhite8", Division = "8" },
                new Team { TeamID = 17, School = "Milton", Name = "Milton7", Division = "7" },
                new Team { TeamID = 18, School = "Milton", Name = "Milton8", Division = "8" },
                new Team { TeamID = 19, School = "Reedsburg", Name = "Reedsburg7", Division = "7" },
                new Team { TeamID = 20, School = "Reedsburg", Name = "Reedsburg8", Division = "8" },
                new Team { TeamID = 21, School = "Stoughton", Name = "Stoughton7", Division = "7" },
                new Team { TeamID = 22, School = "Stoughton", Name = "Stoughton8", Division = "8" },
                new Team { TeamID = 23, School = "Sauk Pr", Name = "SaukPr7", Division = "7" },
                new Team { TeamID = 24, School = "Sauk Pr", Name = "SaukPr8", Division = "8" },
                new Team { TeamID = 25, School = "Monona Grove", Name = "MononaGBlue7", Division = "7" },
                new Team { TeamID = 26, School = "Monona Grove", Name = "MononaGWhite7", Division = "7" },
                new Team { TeamID = 27, School = "Monona Grove", Name = "MononaG8", Division = "8" },
                new Team { TeamID = 28, School = "Sun P West", Name = "SunPrWest7", Division = "7" },
                new Team { TeamID = 29, School = "Sun P West", Name = "SunPrWest8", Division = "8" },
                new Team { TeamID = 30, School = "Portage", Name = "Portage7", Division = "7" },
                new Team { TeamID = 31, School = "Portage", Name = "Portage8", Division = "8" },
                new Team { TeamID = 32, School = "Waunakee", Name = "WaunakeeWhite7", Division = "7" },
                new Team { TeamID = 33, School = "Waunakee", Name = "WaunakeePurple7", Division = "7" },
                new Team { TeamID = 34, School = "Waunakee", Name = "WaunakeeWhite8", Division = "8" },
                new Team { TeamID = 35, School = "Waunakee", Name = "WaunakeePurple8", Division = "8" },
                new Team { TeamID = 36, School = "MtHoreb", Name = "MtHoreb7Red", Division = "7" },
                new Team { TeamID = 37, School = "MtHoreb", Name = "MtHoreb7White", Division = "7" },
                new Team { TeamID = 38, School = "MtHoreb", Name = "MtHoreb8Red", Division = "8" }
            };
        }
    }
}