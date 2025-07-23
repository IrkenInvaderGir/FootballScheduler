using System;
using System.Collections.Generic;
using System.IO;

namespace BatchProcessor.Models
{
    public class SiteAvailability
    {
        public DateTime Date { get; set; }
        public string School { get; set; }
        public bool SlotTaken { get; set; }
        
        public static List<SiteAvailability> LoadFromCsv(string filePath)
        {
            var siteAvailability = new List<SiteAvailability>();
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"CSV file not found: {filePath}");
                return siteAvailability;
            }
            
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var columns = line.Split(',');
                if (columns.Length >= 3 && DateTime.TryParse(columns[2], out DateTime date))
                {
                    siteAvailability.Add(new SiteAvailability
                    {
                        School = columns[0].Trim(),
                        Date = date,
                        SlotTaken = false
                    });
                }
            }
            
            return siteAvailability;
        }
    }
}