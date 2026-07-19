using System;
using System.Collections.Generic;

namespace BatchProcessor.Models
{
    // Per-school metadata from the 2026 program intake survey that isn't tied to a
    // specific team or date: shared-coaching status, start-time limits, and the
    // free-text requests each program submitted. Replaces the unused SpecialHandlingRules
    // stub - nothing referenced that class.
    public class SchoolProfile
    {
        public string School { get; set; } = "";
        public bool SharedCoachingStaff { get; set; }
        public TimeSpan? EarliestStart { get; set; }
        public TimeSpan? LatestStart { get; set; }
        public string SpecialDateNotes { get; set; } = "";
        public string TravelNotes { get; set; } = "";
        public string OtherConstraints { get; set; } = "";

        // Loads Files/2026_ProgramSummary.csv.
        public static List<SchoolProfile> LoadFromCsv(string filePath)
        {
            var profiles = new List<SchoolProfile>();
            var records = CsvUtil.ReadRecords(filePath);
            if (records.Count < 2)
            {
                if (records.Count == 0)
                    Console.WriteLine($"CSV file not found or empty: {filePath}");
                return profiles;
            }

            var header = records[0];
            int iSchool = Array.IndexOf(header, "School");
            int iShared = Array.IndexOf(header, "SharedCoach");
            int iEarliest = Array.IndexOf(header, "EarliestStart");
            int iLatest = Array.IndexOf(header, "LatestStart");
            int iSpecial = Array.IndexOf(header, "SpecialDates");
            int iTravel = Array.IndexOf(header, "TravelRestriction");
            int iOther = Array.IndexOf(header, "OtherConstraints");

            for (int r = 1; r < records.Count; r++)
            {
                var row = records[r];
                if (row.Length <= iSchool || string.IsNullOrWhiteSpace(row[iSchool]))
                    continue;

                profiles.Add(new SchoolProfile
                {
                    School = row[iSchool].Trim(),
                    SharedCoachingStaff = string.Equals(row[iShared].Trim(), "Yes", StringComparison.OrdinalIgnoreCase),
                    EarliestStart = DateTime.TryParse(row[iEarliest], out var earliest) ? earliest.TimeOfDay : (TimeSpan?)null,
                    LatestStart = DateTime.TryParse(row[iLatest], out var latest) ? latest.TimeOfDay : (TimeSpan?)null,
                    SpecialDateNotes = row[iSpecial].Trim(),
                    TravelNotes = row[iTravel].Trim(),
                    OtherConstraints = row[iOther].Trim()
                });
            }

            return profiles;
        }
    }
}
