using System;
using System.Collections.Generic;

namespace BatchProcessor.Models
{
    public class WeekDefinitions
    {
        public int WeekNumber { get; set; }
        public DateTime Date { get; set; }
        
        public static List<WeekDefinitions> Initialize()
        {
            return new List<WeekDefinitions>
            {
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2025, 8, 23) },
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2025, 8, 25) },
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2025, 8, 26) },
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2025, 8, 27) },
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2025, 8, 28) },
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2025, 9, 1) },
                new WeekDefinitions { WeekNumber = 2, Date = new DateTime(2025, 9, 2) },
                new WeekDefinitions { WeekNumber = 2, Date = new DateTime(2025, 9, 3) },
                new WeekDefinitions { WeekNumber = 2, Date = new DateTime(2025, 9, 4) },
                new WeekDefinitions { WeekNumber = 3, Date = new DateTime(2025, 9, 8) },
                new WeekDefinitions { WeekNumber = 3, Date = new DateTime(2025, 9, 9) },
                new WeekDefinitions { WeekNumber = 3, Date = new DateTime(2025, 9, 10) },
                new WeekDefinitions { WeekNumber = 3, Date = new DateTime(2025, 9, 11) },
                new WeekDefinitions { WeekNumber = 4, Date = new DateTime(2025, 9, 15) },
                new WeekDefinitions { WeekNumber = 4, Date = new DateTime(2025, 9, 16) },
                new WeekDefinitions { WeekNumber = 4, Date = new DateTime(2025, 9, 17) },
                new WeekDefinitions { WeekNumber = 4, Date = new DateTime(2025, 9, 18) },
                new WeekDefinitions { WeekNumber = 5, Date = new DateTime(2025, 9, 22) },
                new WeekDefinitions { WeekNumber = 5, Date = new DateTime(2025, 9, 23) },
                new WeekDefinitions { WeekNumber = 5, Date = new DateTime(2025, 9, 24) },
                new WeekDefinitions { WeekNumber = 5, Date = new DateTime(2025, 9, 25) },
                new WeekDefinitions { WeekNumber = 6, Date = new DateTime(2025, 9, 29) },
                new WeekDefinitions { WeekNumber = 6, Date = new DateTime(2025, 9, 30) },
                new WeekDefinitions { WeekNumber = 6, Date = new DateTime(2025, 10, 1) },
                new WeekDefinitions { WeekNumber = 6, Date = new DateTime(2025, 10, 2) },
                new WeekDefinitions { WeekNumber = 7, Date = new DateTime(2025, 10, 6) },
                new WeekDefinitions { WeekNumber = 7, Date = new DateTime(2025, 10, 7) },
                new WeekDefinitions { WeekNumber = 7, Date = new DateTime(2025, 10, 8) },
                new WeekDefinitions { WeekNumber = 7, Date = new DateTime(2025, 10, 9) },
                new WeekDefinitions { WeekNumber = 8, Date = new DateTime(2025, 10, 13) },
                new WeekDefinitions { WeekNumber = 8, Date = new DateTime(2025, 10, 14) },
                new WeekDefinitions { WeekNumber = 8, Date = new DateTime(2025, 10, 15) },
                new WeekDefinitions { WeekNumber = 8, Date = new DateTime(2025, 10, 16) }
            };
        }
    }
}