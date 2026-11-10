using System.Text.Json;

namespace CellCount.Core
{
    /// <summary>마지막 사용 설정 (%AppData%\CellCount\settings.json)</summary>
    public sealed class AppSettings
    {
        public const double DefaultConfidence = 0.35;
        public const double DefaultIou = 0.70;
        public const string DefaultModelFile = "cellcount_yolo26s.onnx";

        public string ImageFolder { get; set; } = string.Empty;
        public string ModelPath { get; set; } = string.Empty;
        public double Confidence { get; set; } = DefaultConfidence;
        public double Iou { get; set; } = DefaultIou;
        public bool UseGpu { get; set; }
        public bool IncludeSubfolders { get; set; }
        public bool ShowScores { get; set; }

        public static string DefaultModelPath => Path.Combine(AppContext.BaseDirectory, "Models", DefaultModelFile);

        private static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CellCount", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
