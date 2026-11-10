using YD.AI.Yolo;

namespace CellCount.Core
{
    /// <summary>
    /// 이미지 파일명 규칙: "{Lot} {위치} {샘플}.jpg"
    ///   예) "2602 12시 01.jpg"          -> Lot=2602,      위치=12시, 샘플=01
    ///       "EZC062604 중앙 1_ 03.jpg"  -> Lot=EZC062604, 위치=중앙, 샘플="1_ 03"
    /// </summary>
    public sealed class ImageNameInfo
    {
        public string Lot { get; private set; } = string.Empty;
        public string Position { get; private set; } = string.Empty;
        public string Sample { get; private set; } = string.Empty;

        public static ImageNameInfo Parse(string fileName)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName).Trim();
            string[] t = stem.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var info = new ImageNameInfo();
            if (t.Length >= 2)
            {
                info.Lot = t[0];
                info.Position = t[1];
                info.Sample = string.Join(" ", t.Skip(2));
            }
            else
            {
                // 규칙에 맞지 않는 파일명: 파일명 전체를 Lot 으로 쓰고 '개수' 열에 기록
                info.Lot = stem;
            }
            return info;
        }
    }

    /// <summary>이미지 1장과 그 검출 결과</summary>
    public sealed class ImageItem
    {
        public int No { get; set; }
        public string FullPath { get; set; }
        /// <summary>선택한 폴더 기준 하위 폴더 경로 (최상위면 빈 문자열)</summary>
        public string RelativeDir { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FullPath);
        public ImageNameInfo Info { get; set; }

        /// <summary>추론 원본 후보(점수 내림차순). null 이면 아직 검출 전.</summary>
        public List<YoloPrediction> Candidates { get; set; }
        /// <summary>현재 Threshold 를 통과한 검출 결과</summary>
        public List<YoloPrediction> Detections { get; private set; }
        public long ElapsedMs { get; set; }
        public string Error { get; set; }

        public int? Count => Detections?.Count;

        public void ApplyThreshold(float conf, float iou)
        {
            Detections = Candidates == null ? null : Yolo26.Filter(Candidates, conf, iou);
        }
    }

    /// <summary>숫자 구간을 수치로 비교하는 자연 정렬 ("9_ 01" &lt; "10_ 01"), 그 외는 Ordinal</summary>
    public sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new NaturalComparer();

        public int Compare(string a, string b)
        {
            a ??= string.Empty; b ??= string.Empty;
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                    while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                    string na = a.Substring(si, i - si).TrimStart('0');
                    string nb = b.Substring(sj, j - sj).TrimStart('0');
                    if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                    int c = string.CompareOrdinal(na, nb);
                    if (c != 0) return c;
                }
                else
                {
                    if (a[i] != b[j]) return a[i].CompareTo(b[j]);
                    i++; j++;
                }
            }
            int rest = (a.Length - i).CompareTo(b.Length - j);
            return rest != 0 ? rest : string.CompareOrdinal(a, b);
        }
    }

    public static class FolderScanner
    {
        public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };

        public static bool IsImage(string path) =>
            ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        public static bool HasImages(string folder) =>
            Directory.EnumerateFiles(folder).Any(IsImage);

        public static List<ImageItem> Scan(string folder, bool includeSubfolders)
        {
            var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var items = new List<ImageItem>();
            foreach (string f in Directory.EnumerateFiles(folder, "*.*", option).Where(IsImage))
            {
                string rel = Path.GetRelativePath(folder, Path.GetDirectoryName(f));
                items.Add(new ImageItem
                {
                    FullPath = f,
                    RelativeDir = rel == "." ? string.Empty : rel,
                    Info = ImageNameInfo.Parse(f)
                });
            }
            items = items.OrderBy(x => x.RelativeDir, NaturalComparer.Instance)
                         .ThenBy(x => x.FileName, NaturalComparer.Instance).ToList();
            for (int i = 0; i < items.Count; i++) items[i].No = i + 1;
            return items;
        }

        /// <summary>파일을 잠그지 않도록 메모리로 복사해서 읽는다.</summary>
        public static Bitmap LoadBitmap(string path)
        {
            using (var tmp = Image.FromFile(path))
            {
                return new Bitmap(tmp);
            }
        }
    }
}
