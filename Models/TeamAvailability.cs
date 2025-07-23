using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BatchProcessor.Models
{
    public class TeamAvailability
    {
        public int TeamID { get; set; }
        public DateTime Date { get; set; }
        public bool IsAvailable { get; set; }
        
        public static List<TeamAvailability> LoadFromCsv(string filePath, List<Team> teams)
        {
            var availability = new List<TeamAvailability>();
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"CSV file not found: {filePath}");
                return availability;
            }
            
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var columns = line.Split(',');
                if (columns.Length >= 3 && DateTime.TryParse(columns[2], out DateTime date))
                {
                    var school = columns[1].Trim();
                    var team = teams.FirstOrDefault(t => t.School == school);
                    if (team != null)
                    {
                        availability.Add(new TeamAvailability
                        {
                            TeamID = team.TeamID,
                            Date = date,
                            IsAvailable = true
                        });
                    }
                }
            }
            
            return availability;
        }
    }
}