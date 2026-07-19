using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BatchProcessor.Models
{
    // Which of the fixed cell styles a cell should use - mirrors the "bye" / "V ..." /
    // "@ ..." conditional-format coloring from the 2025 Google Sheet (red/green/yellow).
    // Applied directly at generation time rather than as true Excel conditional-formatting
    // rules, since the writer already knows a cell's category when it builds the cell -
    // no need to re-derive it from the cell text via a formula-based rule.
    public enum XlsxCellStyle { Default, Bye, Home, Away }

    public class XlsxCell
    {
        public string Text { get; set; } = "";
        public XlsxCellStyle Style { get; set; } = XlsxCellStyle.Default;

        public XlsxCell() { }
        public XlsxCell(string text, XlsxCellStyle style = XlsxCellStyle.Default)
        {
            Text = text;
            Style = style;
        }
    }

    public class XlsxSheet
    {
        public string Name { get; set; } = "";
        public List<List<XlsxCell>> Rows { get; set; } = new();
    }

    // Minimal OOXML (.xlsx) writer - no external packages. Every cell is written as an
    // inline string with a wrap-text style (plus a fill color for bye/home/away game
    // cells), which is all this project's grid exports need.
    public static class XlsxWriter
    {
        public static void Write(string filePath, List<XlsxSheet> sheets)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);

            WriteEntry(zip, "[Content_Types].xml", ContentTypesXml(sheets.Count));
            WriteEntry(zip, "_rels/.rels", RelsXml());
            WriteEntry(zip, "xl/workbook.xml", WorkbookXml(sheets));
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml(sheets.Count));
            WriteEntry(zip, "xl/styles.xml", StylesXml());

            for (int i = 0; i < sheets.Count; i++)
                WriteEntry(zip, $"xl/worksheets/sheet{i + 1}.xml", SheetXml(sheets[i]));
        }

        static void WriteEntry(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        static string ContentTypesXml(int sheetCount)
        {
            var overrides = new StringBuilder();
            for (int i = 1; i <= sheetCount; i++)
                overrides.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                overrides +
                "</Types>";
        }

        static string RelsXml() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        static string WorkbookXml(List<XlsxSheet> sheets)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < sheets.Count; i++)
                sb.Append($"<sheet name=\"{Escape(sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                $"<sheets>{sb}</sheets>" +
                "</workbook>";
        }

        static string WorkbookRelsXml(int sheetCount)
        {
            var sb = new StringBuilder();
            for (int i = 1; i <= sheetCount; i++)
                sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            sb.Append($"<Relationship Id=\"rId{sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{sb}</Relationships>";
        }

        // Fill colors match the light red/green/yellow swatches from the 2025 Google
        // Sheet's conditional formatting (bye / home "V ..." / away "@ ..." cells).
        static string StylesXml() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"2\"><font><sz val=\"10\"/><name val=\"Arial\"/></font><font><b/><sz val=\"11\"/><name val=\"Arial\"/></font></fonts>" +
            "<fills count=\"5\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF4CCCC\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9EAD3\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFF2CC\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"6\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFill=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"0\" xfId=\"0\" applyFill=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"0\" xfId=\"0\" applyFill=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        // Indices into the cellXfs array above: 0=default, 1=wrap-only, 2=bold,
        // 3=bye (wrap+red), 4=home (wrap+green), 5=away (wrap+yellow).
        static int StyleIndex(XlsxCellStyle style) => style switch
        {
            XlsxCellStyle.Bye => 3,
            XlsxCellStyle.Home => 4,
            XlsxCellStyle.Away => 5,
            _ => 1
        };

        static string SheetXml(XlsxSheet sheet)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            for (int r = 0; r < sheet.Rows.Count; r++)
            {
                var row = sheet.Rows[r];
                sb.Append($"<row r=\"{r + 1}\">");
                for (int c = 0; c < row.Count; c++)
                {
                    var cell = row[c];
                    if (string.IsNullOrEmpty(cell.Text))
                        continue;
                    var cellRef = ColumnLetter(c) + (r + 1);
                    sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\" s=\"{StyleIndex(cell.Style)}\"><is><t xml:space=\"preserve\">{Escape(cell.Text)}</t></is></c>");
                }
                sb.Append("</row>");
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        static string ColumnLetter(int index)
        {
            var letters = "";
            index++;
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                letters = (char)('A' + rem) + letters;
                index = (index - 1) / 26;
            }
            return letters;
        }

        static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
