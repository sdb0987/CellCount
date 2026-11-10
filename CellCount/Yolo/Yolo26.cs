using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace YD.AI.Yolo
{
    /// <summary>
    /// YOLO26 ONNX 추론기.
    ///  - YOLO26 / YOLOv10 (NMS-free, end-to-end) 출력 [1, N, 6+] = (x1, y1, x2, y2, score, class, ...)
    ///  - YOLOv8/9/11 형식 출력 [1, 4+nc, anchors] = (cx, cy, w, h, class scores...) 도 함께 지원
    /// Predict 는 MinScore 이상의 모든 후보를 반환하고, Threshold/NMS 는 호출 측에서 Filter 로 적용한다.
    /// (Threshold 를 바꿀 때마다 다시 추론하지 않기 위함)
    /// </summary>
    public sealed class Yolo26 : IDisposable
    {
        /// <summary>후보로 보관하는 최소 점수. 화면의 Threshold 는 이 값 이상에서만 의미가 있다.</summary>
        public const float MinScore = 0.05f;

        private const int DefaultWidth = 1280;
        private const int DefaultHeight = 864;

        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly int _classCount;

        public int InputWidth { get; }
        public int InputHeight { get; }
        public string ModelPath { get; }
        /// <summary>실제로 사용 중인 실행 장치 ("GPU (DirectML)" 또는 "CPU")</summary>
        public string Device { get; }
        /// <summary>GPU 를 요청했으나 실패하여 CPU 로 전환된 경우의 사유</summary>
        public string GpuFallbackReason { get; }

        public Yolo26(string modelPath, bool useGpu)
        {
            ModelPath = modelPath;
            Device = "CPU";

            if (useGpu)
            {
                try
                {
                    var opts = new SessionOptions
                    {
                        // DirectML 실행 공급자 요구 사항
                        EnableMemoryPattern = false,
                        ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                        GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                    };
                    opts.AppendExecutionProvider_DML(0);
                    _session = new InferenceSession(modelPath, opts);
                    Device = "GPU (DirectML)";
                }
                catch (Exception ex)
                {
                    GpuFallbackReason = ex.Message;
                    _session = null;
                }
            }

            if (_session == null)
            {
                var opts = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
                _session = new InferenceSession(modelPath, opts);
            }

            _inputName = _session.InputMetadata.Keys.First();
            int[] dims = _session.InputMetadata[_inputName].Dimensions;
            InputHeight = dims.Length == 4 && dims[2] > 0 ? dims[2] : DefaultHeight;
            InputWidth = dims.Length == 4 && dims[3] > 0 ? dims[3] : DefaultWidth;
            _classCount = ReadClassCount(_session);
        }

        /// <summary>Ultralytics 가 넣어주는 메타데이터 names = "{0: 'cell', 1: ...}" 에서 클래스 수를 읽는다.</summary>
        private static int ReadClassCount(InferenceSession session)
        {
            try
            {
                if (session.ModelMetadata.CustomMetadataMap.TryGetValue("names", out string names))
                {
                    int n = names.Count(c => c == ':');
                    if (n > 0) return n;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>이미지 1장을 추론하여 MinScore 이상의 후보를 점수 내림차순으로 반환</summary>
        public List<YoloPrediction> Predict(Bitmap image)
        {
            int iw = image.Width, ih = image.Height;

            // letterbox: 비율 유지 축소 + 가운데 정렬 + 회색(114) 패딩 (Ultralytics 학습/검증과 동일)
            float ratio = Math.Min(InputWidth / (float)iw, InputHeight / (float)ih);
            int nw = Math.Max(1, (int)Math.Round(iw * ratio));
            int nh = Math.Max(1, (int)Math.Round(ih * ratio));
            int left = (int)Math.Round((InputWidth - nw) / 2f - 0.1f);
            int top = (int)Math.Round((InputHeight - nh) / 2f - 0.1f);

            DenseTensor<float> input;
            using (var canvas = new Bitmap(InputWidth, InputHeight, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(canvas))
                using (var attr = new ImageAttributes())
                {
                    g.Clear(Color.FromArgb(114, 114, 114));
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    attr.SetWrapMode(WrapMode.TileFlipXY); // 가장자리 번짐 방지
                    g.DrawImage(image, new Rectangle(left, top, nw, nh), 0, 0, iw, ih, GraphicsUnit.Pixel, attr);
                }
                input = ToTensor(canvas);
            }

            float[] output;
            int[] outDims;
            lock (_session) // 배치 처리와 단일 이미지 미리보기가 동시에 들어와도 순차 실행
            {
                using (var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) }))
                {
                    var t = results.First().AsTensor<float>();
                    outDims = t.Dimensions.ToArray();
                    output = t.ToArray();
                }
            }

            var list = Parse(output, outDims);

            // letterbox 좌표 -> 원본 좌표
            foreach (var p in list)
            {
                float x1 = Clamp((p.Rectangle.Left - left) / ratio, 0, iw);
                float y1 = Clamp((p.Rectangle.Top - top) / ratio, 0, ih);
                float x2 = Clamp((p.Rectangle.Right - left) / ratio, 0, iw);
                float y2 = Clamp((p.Rectangle.Bottom - top) / ratio, 0, ih);
                p.Rectangle = RectangleF.FromLTRB(x1, y1, x2, y2);
            }

            list.RemoveAll(p => p.Rectangle.Width < 2 || p.Rectangle.Height < 2);
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            return list;
        }

        private List<YoloPrediction> Parse(float[] o, int[] dims)
        {
            var list = new List<YoloPrediction>();
            if (dims.Length != 3) return list;
            int d1 = dims[1], d2 = dims[2];

            if (d1 > d2 && d2 >= 6)
            {
                // end-to-end: [1, N, 6+] (x1, y1, x2, y2, score, class, [mask coeffs...])
                for (int i = 0; i < d1; i++)
                {
                    int k = i * d2;
                    float score = o[k + 4];
                    if (score < MinScore) continue;
                    list.Add(new YoloPrediction
                    {
                        Rectangle = RectangleF.FromLTRB(o[k], o[k + 1], o[k + 2], o[k + 3]),
                        Score = score,
                        ClassId = (int)o[k + 5]
                    });
                }
            }
            else if (d1 >= 5)
            {
                // YOLOv8 형식: [1, 4+nc(+32), anchors], 채널 우선 배열
                int nc = _classCount > 0 && _classCount <= d1 - 4 ? _classCount : d1 - 4;
                for (int j = 0; j < d2; j++)
                {
                    float best = 0; int cls = 0;
                    for (int c = 0; c < nc; c++)
                    {
                        float s = o[(4 + c) * d2 + j];
                        if (s > best) { best = s; cls = c; }
                    }
                    if (best < MinScore) continue;
                    float cx = o[j], cy = o[d2 + j], w = o[2 * d2 + j], h = o[3 * d2 + j];
                    list.Add(new YoloPrediction
                    {
                        Rectangle = new RectangleF(cx - w / 2, cy - h / 2, w, h),
                        Score = best,
                        ClassId = cls
                    });
                }
            }
            return list;
        }

        /// <summary>24bpp BGR 비트맵 -> [1, 3, H, W] RGB 0~1 텐서</summary>
        private static unsafe DenseTensor<float> ToTensor(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height, hw = w * h;
            var data = new float[3 * hw];
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte* scan0 = (byte*)bd.Scan0;
                int stride = bd.Stride;
                const float inv = 1f / 255f;
                Parallel.For(0, h, y =>
                {
                    byte* row = scan0 + (long)y * stride;
                    int o = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        data[o + x] = row[x * 3 + 2] * inv;          // R
                        data[hw + o + x] = row[x * 3 + 1] * inv;     // G
                        data[2 * hw + o + x] = row[x * 3] * inv;     // B
                    }
                });
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
            return new DenseTensor<float>(data, new[] { 1, 3, h, w });
        }

        /// <summary>
        /// Confidence Threshold 와 중복 제거(NMS)를 적용한다.
        /// candidates 는 점수 내림차순이어야 한다(Predict 반환값 그대로).
        /// </summary>
        public static List<YoloPrediction> Filter(IReadOnlyList<YoloPrediction> candidates, float confThreshold, float iouThreshold)
        {
            var kept = new List<YoloPrediction>();
            foreach (var c in candidates)
            {
                if (c.Score < confThreshold) break; // 내림차순이므로 이후는 모두 미달
                bool duplicate = false;
                if (iouThreshold < 1f)
                {
                    for (int i = 0; i < kept.Count; i++)
                    {
                        if (IoU(kept[i].Rectangle, c.Rectangle) > iouThreshold) { duplicate = true; break; }
                    }
                }
                if (!duplicate) kept.Add(c);
            }
            return kept;
        }

        private static float IoU(RectangleF a, RectangleF b)
        {
            float w = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
            if (w <= 0) return 0;
            float h = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
            if (h <= 0) return 0;
            float inter = w * h;
            return inter / (a.Width * a.Height + b.Width * b.Height - inter);
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        public void Dispose() => _session.Dispose();
    }
}
