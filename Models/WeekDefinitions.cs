using System;
using System.Collections.Generic;

namespace BatchProcessor.Models
{
    public class WeekDefinitions
    {
        public int WeekNumber { get; set; }
        public DateTime Date { get; set; }

        // Non-null for fixed/special dates outside the normal Mon-Thu grid, e.g. the
        // Verona Jamboree on the Saturday before Week 1.
        public string? SpecialLabel { get; set; }

        public static List<WeekDefinitions> Initialize()
        {
            return new List<WeekDefinitions>
            {
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2026, 8, 22), SpecialLabel = "Verona Jamboree" },
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2026, 8, 24) },
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2026, 8, 25) },
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2026, 8, 26) },
                new WeekDefinitions { WeekNumber = 1, Date = new DateTime(2026, 8, 27) },

                new WeekDefinitions { WeekNumber = 2, Date = new DateTime(2026, 8, 31) },
                new WeekDefinitions { WeekNumber = 2, Date = new DateTime(2026, 9, 1) },
                new WeekDefinitions { WeekNumber = 2, Date = new DateTime(2026, 9, 2) },
                new WeekDefinitions { WeekNumber = 2, Date = new DateTime(2026, 9, 3) },

                // Week of Sep 7 (Labor Day) has no hosting-capacity columns - skipped entirely.

                new WeekDefinitions { WeekNumber = 3, Date = new DateTime(2026, 9, 14) },
                new WeekDefinitions { WeekNumber = 3, Date = new DateTime(2026, 9, 15) },
                new WeekDefinitions { WeekNumber = 3, Date = new DateTime(2026, 9, 16) },
                new WeekDefinitions { WeekNumber = 3, Date = new DateTime(2026, 9, 17) },

                new WeekDefinitions { WeekNumber = 4, Date = new DateTime(2026, 9, 21) },
                new WeekDefinitions { WeekNumber = 4, Date = new DateTime(2026, 9, 22) },
                new WeekDefinitions { WeekNumber = 4, Date = new DateTime(2026, 9, 23) },
                new WeekDefinitions { WeekNumber = 4, Date = new DateTime(2026, 9, 24) },

                new WeekDefinitions { WeekNumber = 5, Date = new DateTime(2026, 9, 28) },
                new WeekDefinitions { WeekNumber = 5, Date = new DateTime(2026, 9, 29) },
                new WeekDefinitions { WeekNumber = 5, Date = new DateTime(2026, 9, 30) },
                new WeekDefinitions { WeekNumber = 5, Date = new DateTime(2026, 10, 1) },

                new WeekDefinitions { WeekNumber = 6, Date = new DateTime(2026, 10, 5) },
                new WeekDefinitions { WeekNumber = 6, Date = new DateTime(2026, 10, 6) },
                new WeekDefinitions { WeekNumber = 6, Date = new DateTime(2026, 10, 7) },
                new WeekDefinitions { WeekNumber = 6, Date = new DateTime(2026, 10, 8) },

                new WeekDefinitions { WeekNumber = 7, Date = new DateTime(2026, 10, 12) },
                new WeekDefinitions { WeekNumber = 7, Date = new DateTime(2026, 10, 13) },
                new WeekDefinitions { WeekNumber = 7, Date = new DateTime(2026, 10, 14) },
                new WeekDefinitions { WeekNumber = 7, Date = new DateTime(2026, 10, 15) },

                new WeekDefinitions { WeekNumber = 8, Date = new DateTime(2026, 10, 19) },
                new WeekDefinitions { WeekNumber = 8, Date = new DateTime(2026, 10, 20) },
                new WeekDefinitions { WeekNumber = 8, Date = new DateTime(2026, 10, 21) },
                new WeekDefinitions { WeekNumber = 8, Date = new DateTime(2026, 10, 22) },

                // Week 9 removed: teams now have exactly 8 weeks to fit 7 games and a bye,
                // with no rescue/makeup week left as a safety valve.
            };
        }
    }
}
