using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CellCount.Core;
using YD.AI.Yolo;

namespace CellCount
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        /// <summary>
        /// GUI:  CellCount.exe
        /// 배치: CellCount.exe --cli "이미지 폴더" [--conf 0.35] [--iou 0.7] [--model x.onnx] [--out "Results Report.xlsx"] [--gpu] [--recursive]
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // CP949 CSV 출력용

            if (args.Length > 0 && args[0].Equals("--cli", StringComparison.OrdinalIgnoreCase))
                return RunCli(args);

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
            return 0;
        }

        private static int RunCli(string[] args)
        {
            AttachConsole(-1);
            try
            {
                string folder = null, output = null, model = AppSettings.DefaultModelPath;
                float conf = (float)AppSettings.DefaultConfidence, iou = (float)AppSettings.DefaultIou;
                bool gpu = false, recursive = false;
                var inv = System.Globalization.CultureInfo.InvariantCulture;

                for (int i = 1; i < args.Length; i++)
                {
                    switch (args[i].ToLowerInvariant())
                    {
                        case "--conf": conf = float.Parse(args[++i], inv); break;
                        case "--iou": iou = float.Parse(args[++i], inv); break;
                        case "--model": model = args[++i]; break;
                        case "--out": output = args[++i]; break;
                        case "--gpu": gpu = true; break;
                        case "--recursive": recursive = true; break;
                        default: folder = args[i]; break;
                    }
                }
                if (folder == null || !Directory.Exists(folder))
                {
                    Console.Error.WriteLine("이미지 폴더를 지정하세요.");
                    return 2;
                }
                if (!recursive && !FolderScanner.HasImages(folder)) recursive = true;
                output ??= Path.Combine(folder, ResultsReport.DefaultFileName);

                var items = FolderScanner.Scan(folder, recursive);
                var total = Stopwatch.StartNew();
                using (var yolo = new Yolo26(model, gpu))
                {
                    Console.WriteLine($"model={Path.GetFileName(model)} device={yolo.Device} input={yolo.InputWidth}x{yolo.InputHeight} images={items.Count} conf={conf} iou={iou}");
                    Console.WriteLine("runtime: " + OnnxRuntimeLoader.Describe());
                    foreach (var item in items)
                    {
                        var sw = Stopwatch.StartNew();
                        using (Bitmap bmp = FolderScanner.LoadBitmap(item.FullPath))
                            item.Candidates = yolo.Predict(bmp);
                        item.ElapsedMs = sw.ElapsedMilliseconds;
                        item.ApplyThreshold(conf, iou);
                        Console.WriteLine($"{item.No}\t{Path.Combine(item.RelativeDir, item.FileName)}\t{item.Count}\t{item.ElapsedMs}ms");
                    }
                }
                foreach (string path in ResultsReport.SaveAll(items, output))
                    Console.WriteLine("saved: " + path);
                Console.WriteLine($"total {total.Elapsed.TotalSeconds:0.0}s");
                return 0;
            }
            catch (Exception ex)
            {
                // OnnxRuntimeLoadException 은 메시지에 원인과 해결 방법이 다 들어 있으므로 스택은 생략
                Console.Error.WriteLine(ex is OnnxRuntimeLoadException ? ex.Message : ex.ToString());
                return 1;
            }
        }
    }
}
