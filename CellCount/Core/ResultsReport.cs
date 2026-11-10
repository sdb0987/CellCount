using System.IO.Compression;
using System.Security;
using System.Text;

namespace CellCount.Core
{
    /// <summary>
    /// "Results Report" 결과표. 참조 CSV 와 같은 배치로 엑셀(.xlsx) 파일에 기록한다.
    ///
    ///   ,,3시,6시,9시,12시,중앙
    ///   2601,01,389,347,364,350,377
    ///   EZC062603,1_ 01,377,369,389,407,401
    ///
    /// 행 = (Lot, 샘플), 열 = 촬영 위치, 값 = 검출된 세포 수.
    /// 하위 폴더(고농도/저농도 등)가 여러 개면 폴더마다 시트를 하나씩 만든다.
    /// 출력 경로 확장자가 .csv 이면 예전처럼 CSV(CP949, CRLF)로 저장한다.
    /// </summary>
    public sealed class ResultsReport
    {
        public const string DefaultFileName = "Results Report.xlsx";
        public static readonly string[] StandardPositions = { "3시", "6시", "9시", "12시", "중앙" };
        /// <summary>파일명에서 위치를 알 수 없는 이미지의 개수를 기록하는 열</summary>
        public const string UnknownPositionColumn = "개수";

        public sealed class Row
        {
            public string Lot;
            public string Sample;
            public Dictionary<string, int> Counts = new Dictionary<string, int>();
        }

        public List<string> Positions { get; } = new List<string>(StandardPositions);
        public List<Row> Rows { get; } = new List<Row>();

        public static ResultsReport Build(IEnumerable<ImageItem> items)
        {
            var report = new ResultsReport();
            var map = new Dictionary<(string, string), Row>();
            foreach (var item in items)
            {
                if (item.Count == null) continue; // 미검출/오류 이미지는 빈칸
                string pos = string.IsNullOrEmpty(item.Info.Position) ? UnknownPositionColumn : item.Info.Position;
                if (!report.Positions.Contains(pos)) report.Positions.Add(pos);

                var key = (item.Info.Lot, item.Info.Sample);
                if (!map.TryGetValue(key, out Row row))
                {
                    row = new Row { Lot = item.Info.Lot, Sample = item.Info.Sample };
                    map[key] = row;
                }
                row.Counts[pos] = item.Count.Value;
            }
            report.Rows.AddRange(map.Values
                .OrderBy(r => r.Lot, NaturalComparer.Instance)
                .ThenBy(r => r.Sample, NaturalComparer.Instance));
            return report;
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.Append(",,").Append(string.Join(",", Positions.Select(Escape))).Append("\r\n");
            foreach (var r in Rows)
            {
                sb.Append(Escape(r.Lot)).Append(',').Append(Escape(r.Sample));
                foreach (string p in Positions)
                {
                    sb.Append(',');
                    if (r.Counts.TryGetValue(p, out int n)) sb.Append(n);
                }
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToCsv(), Encoding.GetEncoding(949));
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        /// <summary>
        /// 결과를 저장하고 저장된 파일 경로를 돌려준다.
        /// .csv 가 아니면 엑셀 통합 문서 하나(폴더별 시트), .csv 이면 폴더별 CSV 파일.
        /// </summary>
        public static List<string> SaveAll(IEnumerable<ImageItem> items, string outputPath)
        {
            var reports = BuildAll(items, outputPath);
            var saved = new List<string>();
            if (Path.GetExtension(outputPath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (path, _, report) in reports)
                {
                    report.Save(path);
                    saved.Add(path);
                }
                return saved;
            }

            var sheets = new List<(string Name, ResultsReport Report)>();
            foreach (var (_, group, report) in reports)
                sheets.Add((MakeSheetName(group, sheets.Select(s => s.Name)), report));
            SaveXlsx(outputPath, sheets);
            saved.Add(outputPath);
            return saved;
        }

        #region 엑셀(.xlsx) 저장 — 외부 라이브러리 없이 Open XML 을 직접 쓴다

        private static string MakeSheetName(string group, IEnumerable<string> used)
        {
            string name = string.IsNullOrEmpty(group) ? "Results Report" : group;
            foreach (char c in new[] { '\\', '/', '?', '*', '[', ']', ':' }) name = name.Replace(c, '_');
            name = name.Trim('\'', ' ');
            if (name.Length == 0) name = "Sheet";
            if (name.Length > 31) name = name.Substring(0, 31); // 엑셀 시트명 최대 31자
            string result = name;
            var taken = new HashSet<string>(used, StringComparer.OrdinalIgnoreCase);
            for (int n = 2; taken.Contains(result); n++)
            {
                string suffix = "_" + n;
                result = (name.Length + suffix.Length > 31 ? name.Substring(0, 31 - suffix.Length) : name) + suffix;
            }
            return result;
        }

        private static void SaveXlsx(string path, List<(string Name, ResultsReport Report)> sheets)
        {
            const string Decl = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
            const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            const string Ct = "application/vnd.openxmlformats-officedocument.spreadsheetml";

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var types = new StringBuilder();
                var book = new StringBuilder();
                var rels = new StringBuilder();
                for (int i = 0; i < sheets.Count; i++)
                {
                    int id = i + 1;
                    types.Append($"<Override PartName=\"/xl/worksheets/sheet{id}.xml\" ContentType=\"{Ct}.worksheet+xml\"/>");
                    book.Append($"<sheet name=\"{Xml(sheets[i].Name)}\" sheetId=\"{id}\" r:id=\"rId{id}\"/>");
                    rels.Append($"<Relationship Id=\"rId{id}\" Type=\"{Rel}/worksheet\" Target=\"worksheets/sheet{id}.xml\"/>");
                    AddEntry(zip, $"xl/worksheets/sheet{id}.xml", Decl + sheets[i].Report.ToSheetXml(Main));
                }

                AddEntry(zip, "[Content_Types].xml", Decl +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    $"<Override PartName=\"/xl/workbook.xml\" ContentType=\"{Ct}.sheet.main+xml\"/>" +
                    $"<Override PartName=\"/xl/styles.xml\" ContentType=\"{Ct}.styles+xml\"/>" +
                    types + "</Types>");
                AddEntry(zip, "_rels/.rels", Decl +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    $"<Relationship Id=\"rId1\" Type=\"{Rel}/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");
                AddEntry(zip, "xl/workbook.xml", Decl +
                    $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Rel}\"><sheets>{book}</sheets></workbook>");
                AddEntry(zip, "xl/_rels/workbook.xml.rels", Decl +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + rels +
                    $"<Relationship Id=\"rId{sheets.Count + 1}\" Type=\"{Rel}/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");
                // 셀 서식: 0 = 기본, 1 = 머리글(굵게, 가운데), 2 = 텍스트(Lot/샘플. "01" 이 숫자 1 로 바뀌지 않게)
                AddEntry(zip, "xl/styles.xml", Decl +
                    $"<styleSheet xmlns=\"{Main}\">" +
                    "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"맑은 고딕\"/></font><font><b/><sz val=\"11\"/><name val=\"맑은 고딕\"/></font></fonts>" +
                    "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                    "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"3\">" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" +
                    "<xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
                    "</cellXfs></styleSheet>");
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, ms.ToArray());
        }

        /// <summary>시트 1장: 1행 = 머리글(A, B 는 빈칸), 2행부터 Lot, 샘플, 위치별 개수</summary>
        private string ToSheetXml(string ns)
        {
            var sb = new StringBuilder();
            sb.Append($"<worksheet xmlns=\"{ns}\">");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane xSplit=\"2\" ySplit=\"1\" topLeftCell=\"C2\" activePane=\"bottomRight\" state=\"frozen\"/></sheetView></sheetViews>");
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"14\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"10\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            sb.Append("<row r=\"1\">");
            for (int c = 0; c < Positions.Count; c++)
                sb.Append($"<c r=\"{ColumnName(c + 2)}1\" s=\"1\" t=\"inlineStr\"><is><t>{Xml(Positions[c])}</t></is></c>");
            sb.Append("</row>");

            for (int i = 0; i < Rows.Count; i++)
            {
                int r = i + 2;
                Row row = Rows[i];
                sb.Append($"<row r=\"{r}\">");
                sb.Append($"<c r=\"A{r}\" s=\"2\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Xml(row.Lot)}</t></is></c>");
                sb.Append($"<c r=\"B{r}\" s=\"2\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Xml(row.Sample)}</t></is></c>");
                for (int c = 0; c < Positions.Count; c++)
                    if (row.Counts.TryGetValue(Positions[c], out int n))
                        sb.Append($"<c r=\"{ColumnName(c + 2)}{r}\"><v>{n}</v></c>");
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string ColumnName(int index) // 0 → A, 26 → AA
        {
            string name = string.Empty;
            for (index++; index > 0; index = (index - 1) / 26)
                name = (char)('A' + (index - 1) % 26) + name;
            return name;
        }

        private static string Xml(string s) => SecurityElement.Escape(s ?? string.Empty);

        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            using var w = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
            w.Write(content);
        }

        #endregion

        /// <summary>
        /// 하위 폴더별로 결과표를 나눈다. Path 는 CSV 로 저장할 때만 쓴다: 폴더가 하나뿐이면 outputPath 그대로,
        /// 여러 개면 "Results Report_고농도.csv" 처럼 폴더명을 붙인다.
        /// </summary>
        public static List<(string Path, string Group, ResultsReport Report)> BuildAll(IEnumerable<ImageItem> items, string outputPath)
        {
            var groups = items.GroupBy(i => i.RelativeDir).ToList();
            var list = new List<(string, string, ResultsReport)>();
            foreach (var g in groups)
            {
                string path = outputPath;
                if (groups.Count > 1 && g.Key.Length > 0)
                {
                    string suffix = g.Key.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
                    path = Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty,
                        Path.GetFileNameWithoutExtension(outputPath) + "_" + suffix + Path.GetExtension(outputPath));
                }
                list.Add((path, g.Key, Build(g)));
            }
            return list;
        }
    }
}
