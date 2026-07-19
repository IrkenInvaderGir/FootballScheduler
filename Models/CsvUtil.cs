using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BatchProcessor.Models
{
    // Minimal RFC4180-style CSV reader: handles quoted fields, embedded commas,
    // embedded newlines inside quotes, and "" as an escaped quote. The 2026 survey
    // exports (Export-Csv output) rely on all of these for their free-text columns.
    public static class CsvUtil
    {
        public static List<string[]> ReadRecords(string filePath)
        {
            var records = new List<string[]>();
            if (!File.Exists(filePath))
                return records;

            var text = File.ReadAllText(filePath);
            var field = new StringBuilder();
            var record = new List<string>();
            bool inQuotes = false;
            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    field.Append(c);
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        i++;
                        break;
                    case ',':
                        record.Add(field.ToString());
                        field.Clear();
                        i++;
                        break;
                    case '\r':
                        i++;
                        break;
                    case '\n':
                        record.Add(field.ToString());
                        field.Clear();
                        records.Add(record.ToArray());
                        record = new List<string>();
                        i++;
                        break;
                    default:
                        field.Append(c);
                        i++;
                        break;
                }
            }

            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record.ToArray());
            }

            return records;
        }
    }
}
