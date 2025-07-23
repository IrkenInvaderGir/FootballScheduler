using System;

namespace BatchProcessor.Models
{
    public class Schedule
    {
        public int GameNumber { get; set; }
        public string HostTeam { get; set; }
        public string AwayTeam { get; set; }
        public DateTime Date { get; set; }
        public int WeekNumber { get; set; }
    }
}