using System;
using System.Collections.Generic;

namespace BatchProcessor.Models
{
    public class TeamAvailability
    {
        public int TeamID { get; set; }

        // Hard constraint: nights this team's school says they can travel/play at all.
        public HashSet<DayOfWeek> AbleNights { get; set; } = new();

        // Soft preference: nights this team's school would prefer, a subset of AbleNights.
        public HashSet<DayOfWeek> PreferredNights { get; set; } = new();

        public bool IsAvailable(DateTime date) => AbleNights.Contains(date.DayOfWeek);
        public bool IsPreferred(DateTime date) => PreferredNights.Contains(date.DayOfWeek);

        // Loads Files/2026_ProgramSummary.csv, which gives Able/Preferred nights by day
        // of week per school per grade level (not per specific date, and not per team -
        // every team at a school/grade shares the same answer). One record is built per
        // team so every roster team gets availability data, not just the first team at
        // a given school like the 2025 loader did.
        public static List<TeamAvailability> LoadFromCsv(string filePath, List<Team> teams)
        {
            var result = new List<TeamAvailability>();
            var records = CsvUtil.ReadRecords(filePath);
            if (records.Count < 2)
            {
                if (records.Count == 0)
                    Console.WriteLine($"CSV file not found or empty: {filePath}");
                return result;
            }

            var header = records[0];
            int iSchool = Array.IndexOf(header, "School");
            int iAble7 = Array.IndexOf(header, "Able7");
            int iPref7 = Array.IndexOf(header, "Pref7");
            int iAble8 = Array.IndexOf(header, "Able8");
            int iPref8 = Array.IndexOf(header, "Pref8");

            var bySchool = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            for (int r = 1; r < records.Count; r++)
            {
                var row = records[r];
                if (row.Length <= iSchool || string.IsNullOrWhiteSpace(row[iSchool]))
                    continue;
                bySchool[row[iSchool].Trim()] = row;
            }

            foreach (var team in teams)
            {
                if (!bySchool.TryGetValue(team.School, out var row))
                {
                    Console.WriteLine($"No availability data found for school: {team.School} (team {team.Name})");
                    continue;
                }

                bool isEighth = team.Division == "8";
                var ableRaw = isEighth ? row[iAble8] : row[iAble7];
                var prefRaw = isEighth ? row[iPref8] : row[iPref7];

                result.Add(new TeamAvailability
                {
                    TeamID = team.TeamID,
                    AbleNights = ParseDays(ableRaw),
                    PreferredNights = ParseDays(prefRaw)
                });
            }

            return result;
        }

        private static HashSet<DayOfWeek> ParseDays(string raw)
        {
            var days = new HashSet<DayOfWeek>();
            if (string.IsNullOrWhiteSpace(raw))
                return days;

            foreach (var part in raw.Split(','))
            {
                if (Enum.TryParse<DayOfWeek>(part.Trim(), true, out var day))
                    days.Add(day);
            }
            return days;
        }
    }
}
