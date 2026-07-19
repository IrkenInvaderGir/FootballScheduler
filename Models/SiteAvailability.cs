using System;
using System.Collections.Generic;
using System.Globalization;

namespace BatchProcessor.Models
{
    public class SiteAvailability
    {
        public string School { get; set; } = "";
        public DateTime Date { get; set; }
        public int WeekNumber { get; set; }

        // Games this school can host on this date (replaces last year's implicit
        // "one CSV row per slot" convention with an explicit number).
        public int Capacity { get; set; }
        public int SlotsUsed { get; set; }

        // Null = open to either grade. Set only for slots that are earmarked for one
        // grade level, e.g. the Verona Jamboree's 2 seventh-grade / 1 eighth-grade split.
        public string? Grade { get; set; }

        public bool HasOpenSlot => SlotsUsed < Capacity;

        // Loads Files/2026_HostingCapacity_Long.csv (columns: School,Week,Day,Date,Capacity),
        // then appends the fixed 2026 exceptions that aren't part of the weekly survey grid.
        public static List<SiteAvailability> LoadFromCsv(string filePath)
        {
            var sites = new List<SiteAvailability>();
            var records = CsvUtil.ReadRecords(filePath);
            if (records.Count < 2)
            {
                if (records.Count == 0)
                    Console.WriteLine($"CSV file not found or empty: {filePath}");
                return sites;
            }

            var header = records[0];
            int iSchool = Array.IndexOf(header, "School");
            int iWeek = Array.IndexOf(header, "Week");
            int iDate = Array.IndexOf(header, "Date");
            int iCapacity = Array.IndexOf(header, "Capacity");

            for (int r = 1; r < records.Count; r++)
            {
                var row = records[r];
                if (row.Length <= iCapacity || string.IsNullOrWhiteSpace(row[iSchool]))
                    continue;

                if (!DateTime.TryParse(row[iDate], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    continue;
                if (!int.TryParse(row[iWeek], out var week))
                    continue;
                if (!int.TryParse(row[iCapacity], out var capacity))
                    continue;

                sites.Add(new SiteAvailability
                {
                    School = row[iSchool].Trim(),
                    WeekNumber = week,
                    Date = date,
                    Capacity = capacity,
                    SlotsUsed = 0
                });
            }

            sites.AddRange(SpecialDateSites());
            return sites;
        }

        // Fixed 2026 hosting dates outside the normal weekly capacity grid.
        private static List<SiteAvailability> SpecialDateSites()
        {
            var jamboreeDate = new DateTime(2026, 8, 22);
            return new List<SiteAvailability>
            {
                new SiteAvailability { School = "Verona", Date = jamboreeDate, WeekNumber = 1, Capacity = 2, Grade = "7" },
                new SiteAvailability { School = "Verona", Date = jamboreeDate, WeekNumber = 1, Capacity = 1, Grade = "8" },
            };
        }
    }
}
