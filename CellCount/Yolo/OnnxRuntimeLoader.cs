using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YD.AI.Yolo
{
    /// <summary>
    /// 네이티브 onnxruntime.dll 을 InferenceSession 보다 먼저, 프로그램 폴더에서 직접 불러온다.
    ///
    /// onnxruntime.dll 은 Visual C++ 런타임(x64)이 있어야 로드되지만 .NET 은 이 런타임이 필요 없다.
    /// 그래서 런타임이 없는 PC 에서는 프로그램은 정상으로 뜨고 검출할 때만
    /// "The type initializer for 'Microsoft.ML.OnnxRuntime.NativeMethods' threw an exception." 이 나며,
    /// 실제 원인(빠진 DLL)은 내부 예외에 숨어 보이지 않는다.
    /// 프로그램 폴더에 onnxruntime.dll 이 아예 없으면 ONNX Runtime 은 Windows 11 에 내장된 오래된
    /// System32\onnxruntime.dll(1.10)을 대신 불러오고, 같은 오류가 엉뚱한 API 버전 오류로 나타난다.
    ///
    /// 여기서 먼저 불러 보고, 실패하면 무엇이 없는지와 해결 방법을 담은 <see cref="OnnxRuntimeLoadException"/> 을 던진다.
    /// 성공하면 ONNX Runtime 이 같은 경로를 다시 불러올 때 이미 올라온 모듈이 그대로 쓰인다.
    /// </summary>
    public static class OnnxRuntimeLoader
    {
        public const string VcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

        private const string LibraryName = "onnxruntime.dll";

        /// <summary>
        /// onnxruntime.dll 이 의존하는 Visual C++ 런타임 (불러올 순서대로).
        /// 빌드 시 실행 폴더에 함께 복사된다 (CellCount.csproj 참고).
        /// </summary>
        private static readonly string[] VcRuntimeDlls = { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "msvcp140_1.dll" };

        private static readonly object Gate = new object();

        /// <summary>불러온 onnxruntime.dll 의 전체 경로. 아직 불러오지 않았으면 null.</summary>
        public static string LibraryPath { get; private set; }

        /// <summary>
        /// onnxruntime.dll 을 불러온다. 이미 불러왔으면 아무것도 하지 않는다.
        /// 실패하면 아무것도 남기지 않으므로, 원인을 해결한 뒤 다시 호출하면 다시 시도한다.
        /// </summary>
        /// <exception cref="OnnxRuntimeLoadException">DLL 이 없거나, 의존하는 DLL 이 없어 불러올 수 없을 때</exception>
        public static void EnsureLoaded()
        {
            lock (Gate)
            {
                if (LibraryPath != null) return;

                string path = FindLibrary();
                if (path == null)
                    throw new OnnxRuntimeLoadException(
                        "AI 엔진 파일(onnxruntime.dll)이 프로그램 폴더에 없습니다.\n" +
                        "프로그램을 복사할 때 runtimes 폴더를 포함한 모든 파일을 함께 복사하세요.\n\n" +
                        "프로그램 폴더: " + AppContext.BaseDirectory);

                PreloadLocalVcRuntime();
                try
                {
                    NativeLibrary.Load(path); // 프로세스가 끝날 때까지 해제하지 않는다
                }
                catch (Exception ex) when (ex is DllNotFoundException || ex is BadImageFormatException)
                {
                    throw new OnnxRuntimeLoadException(DescribeLoadFailure(path, ex), ex);
                }
                LibraryPath = path;
            }
        }

        /// <summary>실제로 올라온 onnxruntime.dll, Visual C++ 런타임, DirectML.dll 의 버전과 위치 (문제 확인용)</summary>
        public static string Describe()
        {
            var names = new HashSet<string>(VcRuntimeDlls.Prepend(LibraryName).Append("DirectML.dll"), StringComparer.OrdinalIgnoreCase);
            var loaded = new List<string>();
            using (var process = Process.GetCurrentProcess())
            {
                foreach (ProcessModule m in process.Modules)
                {
                    using (m)
                    {
                        if (names.Contains(m.ModuleName))
                            loaded.Add($"{m.ModuleName} {m.FileVersionInfo.FileVersion} [{ShortDir(m.FileName)}]");
                    }
                }
            }
            return loaded.Count > 0 ? string.Join(", ", loaded) : "(불러온 DLL 없음)";
        }

        /// <summary>.NET 이 DllImport("onnxruntime") 를 찾는 순서(NATIVE_DLL_SEARCH_DIRECTORIES)대로 찾는다.</summary>
        private static string FindLibrary()
        {
            var dirs = new List<string>();
            if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string list)
                dirs.AddRange(list.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
            dirs.Add(Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native"));
            dirs.Add(AppContext.BaseDirectory);

            foreach (string dir in dirs)
            {
                string path = Path.Combine(dir, LibraryName);
                if (File.Exists(path)) return Path.GetFullPath(path);
            }
            return null;
        }

        /// <summary>
        /// 실행 폴더에 함께 배포한 Visual C++ 런타임을 먼저 올린다 (같은 이름의 DLL 이 아직 프로세스에 없을 때만).
        /// onnxruntime.dll 이 runtimes\win-x64\native 에 있으면 Windows 는 실행 폴더에서 의존 DLL 을 찾지 않기 때문이다.
        /// </summary>
        private static void PreloadLocalVcRuntime()
        {
            foreach (string name in VcRuntimeDlls)
            {
                string local = Path.Combine(AppContext.BaseDirectory, name);
                if (GetModuleHandleW(name) == IntPtr.Zero && File.Exists(local))
                    NativeLibrary.TryLoad(local, out _); // 실패하면 onnxruntime.dll 로드 오류에서 원인을 알려준다
            }
        }

        private static string DescribeLoadFailure(string path, Exception error)
        {
            if (error is BadImageFormatException)
                return "AI 엔진 파일(onnxruntime.dll 또는 함께 쓰는 DLL)이 손상되었거나 64비트(x64)용이 아닙니다.\n" +
                       "프로그램 폴더 전체를 다시 복사하세요.\n\n" +
                       "파일: " + path + "\n" +
                       "상세: " + error.Message;

            string ortDir = Path.GetDirectoryName(path);
            var found = VcRuntimeDlls.Select(n => (Name: n, Path: FindDependency(n, ortDir))).ToList();
            var missing = found.Where(f => f.Path == null).Select(f => f.Name).ToList();

            var sb = new StringBuilder();
            if (missing.Count > 0)
            {
                sb.Append("이 PC에 Microsoft Visual C++ 재배포 가능 패키지(x64)가 없어 AI 엔진(onnxruntime.dll)을 불러올 수 없습니다.\n");
                sb.Append("없는 파일: ").Append(string.Join(", ", missing)).Append('\n');
            }
            else
            {
                // Visual C++ 런타임 파일은 다 있지만 로드가 안 됨: 런타임이 오래되었거나 손상, 또는 다른 의존 DLL 문제
                sb.Append("AI 엔진(onnxruntime.dll) 또는 이 파일이 쓰는 DLL 을 불러올 수 없습니다.\n");
                sb.Append("Visual C++ 런타임이 오래되었거나 손상되었을 수 있습니다. 현재 런타임:\n");
                foreach (var f in found)
                    sb.Append("  ").Append(f.Name).Append(' ').Append(FileVersionInfo.GetVersionInfo(f.Path).FileVersion)
                      .Append(" [").Append(ShortDir(f.Path)).Append("]\n");
            }
            sb.Append('\n');
            sb.Append("해결: 아래 주소에서 vc_redist.x64.exe 를 받아 설치한 뒤 프로그램을 다시 실행하세요.\n");
            sb.Append(VcRedistUrl).Append('\n');
            sb.Append("(또는 vcruntime140.dll 등이 함께 들어 있는 최신 빌드의 프로그램 폴더 전체를 다시 복사)\n\n");
            sb.Append("상세: ").Append(error.Message);
            return sb.ToString();
        }

        /// <summary>onnxruntime.dll 을 불러올 때 Windows 가 의존 DLL 을 찾는 위치들에서 찾는다.</summary>
        private static string FindDependency(string name, string ortDir)
        {
            var dirs = new List<string> { ortDir, AppContext.BaseDirectory, Environment.SystemDirectory };
            dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
            foreach (string dir in dirs)
            {
                string path = Path.Combine(dir.Trim().Trim('"'), name);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>프로그램 폴더 안이면 상대 경로("." 은 프로그램 폴더), 밖이면 폴더 전체 경로</summary>
        private static string ShortDir(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
            string root = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            bool inside = dir.Equals(root, StringComparison.OrdinalIgnoreCase)
                          || dir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            return inside ? Path.GetRelativePath(root, dir) : dir;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr GetModuleHandleW(string moduleName);
    }

    /// <summary>onnxruntime.dll 을 불러올 수 없을 때. Message 에 원인과 해결 방법이 들어 있다.</summary>
    public sealed class OnnxRuntimeLoadException : Exception
    {
        public OnnxRuntimeLoadException(string message, Exception inner = null) : base(message, inner) { }
    }
}
