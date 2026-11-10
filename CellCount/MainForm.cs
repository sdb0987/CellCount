using System.Diagnostics;
using System.Drawing.Imaging;
using CellCount.Core;
using YD.AI.Yolo;

namespace CellCount
{
    public partial class MainForm : Form
    {
        private readonly AppSettings _settings;
        private List<ImageItem> _items = new List<ImageItem>();
        private readonly Dictionary<ImageItem, DataGridViewRow> _rows = new Dictionary<ImageItem, DataGridViewRow>();

        private Yolo26 _yolo;
        private bool _yoloGpu;
        private readonly object _modelLock = new object();

        private CancellationTokenSource _cts;
        private bool _running;
        private bool _loading;          // 컨트롤 값을 코드에서 바꾸는 동안 이벤트 무시
        private ImageItem _current;
        private int _showVersion;

        // 백그라운드 검출 스레드가 읽는 현재 Threshold
        private volatile float _conf = (float)AppSettings.DefaultConfidence;
        private volatile float _iou = (float)AppSettings.DefaultIou;
        private readonly System.Windows.Forms.Timer _thresholdTimer = new System.Windows.Forms.Timer { Interval = 150 };

        public MainForm()
        {
            InitializeComponent();
            InitGrids();

            _settings = AppSettings.Load();
            _loading = true;
            nudConf.Value = ClampDecimal((decimal)_settings.Confidence, nudConf.Minimum, nudConf.Maximum);
            trackConf.Value = (int)Math.Round(nudConf.Value * 100);
            nudIou.Value = ClampDecimal((decimal)_settings.Iou, nudIou.Minimum, nudIou.Maximum);
            chkGpu.Checked = _settings.UseGpu;
            chkSubfolders.Checked = _settings.IncludeSubfolders;
            chkShowScores.Checked = _settings.ShowScores;
            txtModel.Text = File.Exists(_settings.ModelPath) ? _settings.ModelPath : AppSettings.DefaultModelPath;
            _loading = false;
            _conf = (float)nudConf.Value;
            _iou = (float)nudIou.Value;
            viewer.ShowScores = chkShowScores.Checked;

            btnBrowseFolder.Click += (s, e) => BrowseFolder();
            btnBrowseModel.Click += (s, e) => BrowseModel();
            btnBrowseOutput.Click += (s, e) => BrowseOutput();
            btnOpenOutput.Click += (s, e) => OpenOutputFolder();
            btnStart.Click += async (s, e) => await RunBatchAsync();
            btnStop.Click += (s, e) => _cts?.Cancel();
            btnSaveCsv.Click += (s, e) => SaveCsv(true);
            btnSaveImage.Click += (s, e) => SaveAnnotatedImage();
            txtFolder.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SetFolder(txtFolder.Text.Trim()); }
            };
            chkSubfolders.CheckedChanged += (s, e) =>
            {
                if (!_loading && Directory.Exists(txtFolder.Text.Trim())) SetFolder(txtFolder.Text.Trim());
            };
            chkShowScores.CheckedChanged += (s, e) => { viewer.ShowScores = chkShowScores.Checked; viewer.Invalidate(); };
            nudConf.ValueChanged += (s, e) => OnThresholdChanged(true);
            trackConf.ValueChanged += (s, e) => OnThresholdChanged(false);
            nudIou.ValueChanged += (s, e) => OnThresholdChanged(true);
            _thresholdTimer.Tick += (s, e) => { _thresholdTimer.Stop(); RecomputeAll(true); };
            dgvFiles.SelectionChanged += (s, e) =>
            {
                if (!_loading && dgvFiles.CurrentRow?.Tag is ImageItem item && item != _current) ShowItem(item);
            };
            FormClosing += OnFormClosing;

            HookDragDrop(this);

            if (Directory.Exists(_settings.ImageFolder))
                Shown += (s, e) => SetFolder(_settings.ImageFolder);
        }

        private static decimal ClampDecimal(decimal v, decimal min, decimal max) => Math.Max(min, Math.Min(max, v));

        #region 그리드

        private void InitGrids()
        {
            dgvFiles.Columns.Add(MakeColumn("colNo", "No", 44, true));
            dgvFiles.Columns.Add(MakeColumn("colDir", "폴더", 70, false));
            dgvFiles.Columns.Add(MakeColumn("colFile", "파일명", 190, false));
            dgvFiles.Columns.Add(MakeColumn("colCount", "세포 수", 70, true));
            dgvFiles.Columns.Add(MakeColumn("colTime", "시간(ms)", 70, true));
            dgvFiles.Columns["colFile"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgvFiles.Columns["colCount"].DefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        }

        private static DataGridViewTextBoxColumn MakeColumn(string name, string header, int width, bool right)
        {
            var c = new DataGridViewTextBoxColumn { Name = name, HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable };
            if (right) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            return c;
        }

        private void FillFileGrid()
        {
            _loading = true;
            dgvFiles.Rows.Clear();
            _rows.Clear();
            dgvFiles.Columns["colDir"].Visible = _items.Any(i => i.RelativeDir.Length > 0);
            foreach (var item in _items)
            {
                int idx = dgvFiles.Rows.Add(item.No, item.RelativeDir, item.FileName, null, null);
                dgvFiles.Rows[idx].Tag = item;
                _rows[item] = dgvFiles.Rows[idx];
            }
            dgvFiles.ClearSelection();
            dgvFiles.CurrentCell = null;
            _loading = false;
        }

        private void UpdateRow(ImageItem item)
        {
            if (!_rows.TryGetValue(item, out DataGridViewRow row)) return;
            row.Cells["colCount"].Value = item.Error != null ? "오류" : (object)item.Count;
            row.Cells["colCount"].ToolTipText = item.Error ?? string.Empty;
            row.Cells["colTime"].Value = item.Candidates != null ? (object)item.ElapsedMs : null;
        }

        private void UpdateReportGrid()
        {
            var reports = ResultsReport.BuildAll(_items, ResultsReport.DefaultFileName);
            bool multi = reports.Count > 1;
            var positions = new List<string>(ResultsReport.StandardPositions);
            foreach (var r in reports)
                foreach (string p in r.Report.Positions)
                    if (!positions.Contains(p)) positions.Add(p);

            dgvReport.SuspendLayout();
            dgvReport.Rows.Clear();
            dgvReport.Columns.Clear();
            if (multi) dgvReport.Columns.Add(MakeColumn("colGroup", "폴더", 70, false));
            dgvReport.Columns.Add(MakeColumn("colLot", "Lot", 100, false));
            dgvReport.Columns.Add(MakeColumn("colSample", "샘플", 70, false));
            foreach (string p in positions) dgvReport.Columns.Add(MakeColumn("pos_" + p, p, 56, true));

            foreach (var r in reports)
            {
                foreach (var row in r.Report.Rows)
                {
                    var cells = new List<object>();
                    if (multi) cells.Add(r.Group);
                    cells.Add(row.Lot);
                    cells.Add(row.Sample);
                    foreach (string p in positions)
                        cells.Add(row.Counts.TryGetValue(p, out int n) ? (object)n : null);
                    dgvReport.Rows.Add(cells.ToArray());
                }
            }
            dgvReport.ResumeLayout();

            var done = _items.Where(i => i.Count != null).ToList();
            lblSummary.Text = done.Count == 0
                ? $"이미지 {_items.Count}장"
                : $"검출 {done.Count}/{_items.Count}장 · 총 세포 수 {done.Sum(i => i.Count.Value):N0} · 평균 {done.Average(i => i.Count.Value):0.0}개/장";
        }

        #endregion

        #region 폴더 / 모델 / 출력 경로

        private void BrowseFolder()
        {
            using (var dlg = new FolderBrowserDialog { Description = "세포 이미지 폴더 선택", UseDescriptionForTitle = true })
            {
                if (Directory.Exists(txtFolder.Text)) dlg.SelectedPath = txtFolder.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) SetFolder(dlg.SelectedPath);
            }
        }

        private void SetFolder(string folder)
        {
            if (_running)
            {
                MessageBox.Show(this, "검출이 진행 중입니다. 먼저 [중지]를 누르세요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, "폴더를 찾을 수 없습니다.\n" + folder, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                bool recursive = chkSubfolders.Checked;
                // 선택한 폴더에 이미지가 없고 하위 폴더에만 있으면 자동으로 하위 폴더까지 포함
                if (!recursive && !FolderScanner.HasImages(folder) &&
                    Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories).Any(FolderScanner.IsImage))
                {
                    recursive = true;
                    _loading = true;
                    chkSubfolders.Checked = true;
                    _loading = false;
                }

                _items = FolderScanner.Scan(folder, recursive);
                _current = null;
                _showVersion++;
                viewer.SetImage(null);
                viewer.SetDetections(null);
                viewer.Caption = null;

                txtFolder.Text = folder;
                txtOutput.Text = Path.Combine(folder, ResultsReport.DefaultFileName);
                FillFileGrid();
                UpdateReportGrid();
                progressBar.Value = 0;
                lblStatus.Text = _items.Count == 0
                    ? "이 폴더에는 이미지 파일(jpg, png, bmp, tif)이 없습니다."
                    : $"이미지 {_items.Count}장을 불러왔습니다. [검출 시작]을 누르세요.";

                if (dgvFiles.Rows.Count > 0)
                {
                    dgvFiles.CurrentCell = dgvFiles.Rows[0].Cells["colFile"];
                    // 첫 행이 이미 현재 행이면 SelectionChanged 가 오지 않으므로 직접 표시한다
                    if (_current == null && dgvFiles.Rows[0].Tag is ImageItem first) ShowItem(first);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "폴더를 읽는 중 오류가 발생했습니다.\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void BrowseModel()
        {
            using (var dlg = new OpenFileDialog { Filter = "ONNX 모델 (*.onnx)|*.onnx", Title = "AI 모델 선택" })
            {
                string dir = Path.GetDirectoryName(txtModel.Text);
                if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
                if (dlg.ShowDialog(this) == DialogResult.OK) SetModel(dlg.FileName);
            }
        }

        private void SetModel(string path)
        {
            if (_running) return;
            if (string.Equals(path, txtModel.Text, StringComparison.OrdinalIgnoreCase)) return;
            txtModel.Text = path;
            // 모델이 바뀌면 이전 모델의 결과와 섞이지 않도록 초기화
            foreach (var item in _items)
            {
                item.Candidates = null;
                item.Error = null;
                item.ApplyThreshold(_conf, _iou);
                UpdateRow(item);
            }
            RefreshViewer();
            UpdateReportGrid();
            lblStatus.Text = "모델이 변경되었습니다. [검출 시작]을 누르세요.";
        }

        private void BrowseOutput()
        {
            using (var dlg = new SaveFileDialog { Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv", FileName = ResultsReport.DefaultFileName, Title = "결과 엑셀 파일 저장 위치" })
            {
                string dir = Path.GetDirectoryName(txtOutput.Text);
                if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
                if (!string.IsNullOrWhiteSpace(txtOutput.Text)) dlg.FileName = Path.GetFileName(txtOutput.Text);
                if (dlg.ShowDialog(this) == DialogResult.OK) txtOutput.Text = dlg.FileName;
            }
        }

        private void OpenOutputFolder()
        {
            string dir = Path.GetDirectoryName(txtOutput.Text.Trim());
            if (Directory.Exists(dir))
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }

        #endregion

        #region 드래그 앤 드롭

        private void HookDragDrop(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += OnDragEnter;
            c.DragDrop += OnDragDrop;
            foreach (Control child in c.Controls) HookDragDrop(child);
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop) && !_running
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (!(e.Data?.GetData(DataFormats.FileDrop) is string[] paths) || paths.Length == 0) return;
            string path = paths[0];
            // 드롭 처리 중 탐색기가 멈추지 않도록 메시지 루프로 넘긴다
            BeginInvoke(new Action(() =>
            {
                if (Directory.Exists(path))
                    SetFolder(path);
                else if (File.Exists(path) && Path.GetExtension(path).Equals(".onnx", StringComparison.OrdinalIgnoreCase))
                    SetModel(path);
                else if (File.Exists(path))
                    SetFolder(Path.GetDirectoryName(path)); // 이미지 파일을 놓으면 그 파일이 있는 폴더
            }));
        }

        #endregion

        #region 검출

        private Yolo26 GetModel(string path, bool gpu)
        {
            lock (_modelLock)
            {
                if (_yolo != null && _yoloGpu == gpu && string.Equals(_yolo.ModelPath, path, StringComparison.OrdinalIgnoreCase))
                    return _yolo;
                _yolo?.Dispose();
                _yolo = null;
                if (!File.Exists(path))
                    throw new FileNotFoundException("AI 모델 파일을 찾을 수 없습니다.\n" + path);
                _yolo = new Yolo26(path, gpu);
                _yoloGpu = gpu;
                return _yolo;
            }
        }

        /// <summary>이미지 1장 검출 (백그라운드 스레드에서 호출)</summary>
        private void DetectItem(Yolo26 yolo, ImageItem item)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using (Bitmap bmp = FolderScanner.LoadBitmap(item.FullPath))
                    item.Candidates = yolo.Predict(bmp);
                item.Error = null;
            }
            catch (Exception ex)
            {
                item.Candidates = null;
                item.Error = ex.Message;
            }
            item.ElapsedMs = sw.ElapsedMilliseconds;
            item.ApplyThreshold(_conf, _iou);
        }

        /// <summary>
        /// 예외 메시지에 내부 예외 메시지를 이어 붙인다.
        /// (TypeInitializationException 처럼 바깥 메시지만으로는 원인을 알 수 없는 예외가 있다)
        /// </summary>
        private static string DescribeError(Exception ex)
        {
            string text = ex.Message;
            for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
                if (!text.Contains(inner.Message)) text += "\n" + inner.Message;
            return text;
        }

        private async Task RunBatchAsync()
        {
            if (_running) return;
            if (_items.Count == 0)
            {
                MessageBox.Show(this, "먼저 이미지 폴더를 지정하세요.\n(폴더를 창에 끌어다 놓거나 [찾아보기])", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string modelPath = txtModel.Text.Trim();
            bool gpu = chkGpu.Checked;
            List<ImageItem> items = _items;
            SetRunning(true);
            progressBar.Maximum = items.Count;
            progressBar.Value = 0;
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;
            var total = Stopwatch.StartNew();
            int done = 0;
            string deviceNote = string.Empty;

            try
            {
                lblStatus.Text = "AI 모델을 불러오는 중...";
                Yolo26 yolo = await Task.Run(() => GetModel(modelPath, gpu));
                deviceNote = yolo.GpuFallbackReason != null ? "CPU (GPU 사용 불가)" : yolo.Device;

                IProgress<ImageItem> progress = new Progress<ImageItem>(item =>
                {
                    done++;
                    UpdateRow(item);
                    progressBar.Value = Math.Min(done, progressBar.Maximum);
                    lblStatus.Text = $"검출 중... {done}/{items.Count}  {item.FileName}  [{deviceNote}]";
                    if (item == _current) RefreshViewer();
                });

                await Task.Run(() =>
                {
                    foreach (var item in items)
                    {
                        if (ct.IsCancellationRequested) break;
                        DetectItem(yolo, item);
                        progress.Report(item);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "검출 중 오류가 발생했습니다.\n" + DescribeError(ex), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetRunning(false);
            }

            // 진행 중 Threshold 가 바뀌었을 수 있으므로 현재 값으로 전체를 다시 맞춘다
            await Task.Yield();
            RecomputeAll(false);

            int errors = items.Count(i => i.Error != null);
            string summary = $"{(ct.IsCancellationRequested ? "중지됨" : "검출 완료")}: {items.Count(i => i.Count != null)}/{items.Count}장, {total.Elapsed.TotalSeconds:0.0}초 [{deviceNote}]"
                             + (errors > 0 ? $", 오류 {errors}장" : string.Empty);
            if (!ct.IsCancellationRequested && items.Any(i => i.Count != null))
            {
                string saved = SaveCsv(false);
                lblStatus.Text = saved != null ? summary + " · 결과 저장: " + saved : summary + " · 결과 저장 실패";
            }
            else
            {
                lblStatus.Text = summary;
            }
        }

        private void SetRunning(bool running)
        {
            _running = running;
            btnStart.Enabled = !running;
            btnStop.Enabled = running;
            btnBrowseFolder.Enabled = btnBrowseModel.Enabled = !running;
            txtFolder.Enabled = chkSubfolders.Enabled = chkGpu.Enabled = !running;
        }

        private void OnThresholdChanged(bool fromNumeric)
        {
            if (_loading) return;
            _loading = true;
            if (fromNumeric) trackConf.Value = (int)Math.Round(nudConf.Value * 100);
            else nudConf.Value = trackConf.Value / 100m;
            _loading = false;

            _conf = (float)nudConf.Value;
            _iou = (float)nudIou.Value;
            // 슬라이더를 끄는 동안 매번 전체 재계산하지 않도록 잠깐 모았다가 처리
            _thresholdTimer.Stop();
            _thresholdTimer.Start();
        }

        /// <summary>현재 Threshold 를 모든 결과에 다시 적용한다(재추론 없음).</summary>
        private void RecomputeAll(bool thresholdChanged)
        {
            bool any = false;
            foreach (var item in _items)
            {
                if (item.Candidates == null && item.Error == null) continue;
                item.ApplyThreshold(_conf, _iou);
                UpdateRow(item);
                any = true;
            }
            RefreshViewer();
            UpdateReportGrid();
            if (thresholdChanged && any && !_running)
                lblStatus.Text = $"Threshold {_conf:0.00} 적용됨. [엑셀 저장]을 누르면 결과 파일에 반영됩니다.";
        }

        #endregion

        #region 이미지 표시

        private async void ShowItem(ImageItem item)
        {
            _current = item;
            int version = ++_showVersion;

            Bitmap bmp = null;
            try
            {
                bmp = await Task.Run(() => FolderScanner.LoadBitmap(item.FullPath));
            }
            catch (Exception ex)
            {
                lblStatus.Text = "이미지를 열 수 없습니다: " + ex.Message;
            }
            if (version != _showVersion || IsDisposed)
            {
                bmp?.Dispose();
                return;
            }
            viewer.SetImage(bmp);
            RefreshViewer();

            // 아직 검출하지 않은 이미지를 선택하면 그 한 장만 바로 검출해서 보여준다
            if (bmp != null && item.Candidates == null && item.Error == null && !_running)
            {
                string modelPath = txtModel.Text.Trim();
                bool gpu = chkGpu.Checked;
                try
                {
                    await Task.Run(() => DetectItem(GetModel(modelPath, gpu), item));
                }
                catch (Exception ex)
                {
                    lblStatus.Text = "검출 실패: " + DescribeError(ex).Replace("\r", "").Replace("\n", " ");
                    return;
                }
                if (IsDisposed) return;
                UpdateRow(item);
                if (version == _showVersion) RefreshViewer();
                UpdateReportGrid();
            }
        }

        private void RefreshViewer()
        {
            viewer.Caption = _current?.Count != null ? $"세포 수: {_current.Count}" : null;
            viewer.SetDetections(_current?.Detections);
        }

        private void SaveAnnotatedImage()
        {
            if (_current == null) return;
            using (Bitmap bmp = viewer.RenderAnnotated())
            {
                if (bmp == null) return;
                using (var dlg = new SaveFileDialog
                {
                    Filter = "JPEG (*.jpg)|*.jpg|PNG (*.png)|*.png",
                    FileName = Path.GetFileNameWithoutExtension(_current.FileName) + "_result.jpg",
                    Title = "검출 결과 이미지 저장"
                })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        bool png = Path.GetExtension(dlg.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase);
                        bmp.Save(dlg.FileName, png ? ImageFormat.Png : ImageFormat.Jpeg);
                        lblStatus.Text = "이미지 저장: " + dlg.FileName;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "이미지를 저장할 수 없습니다.\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        #endregion

        #region CSV 저장

        /// <returns>저장된 파일 경로(여러 개면 쉼표로 연결). 실패/취소 시 null</returns>
        private string SaveCsv(bool interactive)
        {
            if (!_items.Any(i => i.Count != null))
            {
                if (interactive)
                    MessageBox.Show(this, "저장할 검출 결과가 없습니다. 먼저 [검출 시작]을 누르세요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            string output = txtOutput.Text.Trim();
            if (output.Length == 0)
            {
                output = Path.Combine(txtFolder.Text.Trim(), ResultsReport.DefaultFileName);
                txtOutput.Text = output;
            }

            try
            {
                var saved = ResultsReport.SaveAll(_items, output);
                string text = string.Join(", ", saved);
                if (interactive) lblStatus.Text = "결과 저장: " + text;
                return text;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "결과 파일을 저장할 수 없습니다. 파일이 Excel 에서 열려 있으면 닫고 [엑셀 저장]을 다시 누르세요.\n\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }

        #endregion

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _settings.ImageFolder = Directory.Exists(txtFolder.Text.Trim()) ? txtFolder.Text.Trim() : _settings.ImageFolder;
            _settings.ModelPath = txtModel.Text.Trim();
            _settings.Confidence = (double)nudConf.Value;
            _settings.Iou = (double)nudIou.Value;
            _settings.UseGpu = chkGpu.Checked;
            _settings.IncludeSubfolders = chkSubfolders.Checked;
            _settings.ShowScores = chkShowScores.Checked;
            _settings.Save();
        }
    }
}
