namespace CellCount
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            pnlTop = new Panel();
            lblFolder = new Label();
            txtFolder = new TextBox();
            btnBrowseFolder = new Button();
            chkSubfolders = new CheckBox();
            lblModel = new Label();
            txtModel = new TextBox();
            btnBrowseModel = new Button();
            chkGpu = new CheckBox();
            lblOutput = new Label();
            txtOutput = new TextBox();
            btnBrowseOutput = new Button();
            btnOpenOutput = new Button();
            lblConf = new Label();
            nudConf = new NumericUpDown();
            trackConf = new TrackBar();
            lblIou = new Label();
            nudIou = new NumericUpDown();
            chkShowScores = new CheckBox();
            btnStart = new Button();
            btnStop = new Button();
            btnSaveCsv = new Button();
            btnSaveImage = new Button();
            splitMain = new SplitContainer();
            tabLeft = new TabControl();
            tabFiles = new TabPage();
            dgvFiles = new DataGridView();
            tabReport = new TabPage();
            dgvReport = new DataGridView();
            viewer = new CellCount.Controls.ImageViewer();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            progressBar = new ToolStripProgressBar();
            lblSummary = new ToolStripStatusLabel();
            pnlTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudConf).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackConf).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudIou).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            tabLeft.SuspendLayout();
            tabFiles.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFiles).BeginInit();
            tabReport.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvReport).BeginInit();
            statusStrip.SuspendLayout();
            SuspendLayout();
            //
            // pnlTop
            //
            pnlTop.Controls.Add(lblFolder);
            pnlTop.Controls.Add(txtFolder);
            pnlTop.Controls.Add(btnBrowseFolder);
            pnlTop.Controls.Add(chkSubfolders);
            pnlTop.Controls.Add(lblModel);
            pnlTop.Controls.Add(txtModel);
            pnlTop.Controls.Add(btnBrowseModel);
            pnlTop.Controls.Add(chkGpu);
            pnlTop.Controls.Add(lblOutput);
            pnlTop.Controls.Add(txtOutput);
            pnlTop.Controls.Add(btnBrowseOutput);
            pnlTop.Controls.Add(btnOpenOutput);
            pnlTop.Controls.Add(lblConf);
            pnlTop.Controls.Add(nudConf);
            pnlTop.Controls.Add(trackConf);
            pnlTop.Controls.Add(lblIou);
            pnlTop.Controls.Add(nudIou);
            pnlTop.Controls.Add(chkShowScores);
            pnlTop.Controls.Add(btnStart);
            pnlTop.Controls.Add(btnStop);
            pnlTop.Controls.Add(btnSaveCsv);
            pnlTop.Controls.Add(btnSaveImage);
            pnlTop.Dock = DockStyle.Top;
            pnlTop.Location = new Point(0, 0);
            pnlTop.Name = "pnlTop";
            pnlTop.Size = new Size(1400, 136);
            pnlTop.TabIndex = 0;
            //
            // lblFolder
            //
            lblFolder.Location = new Point(12, 12);
            lblFolder.Name = "lblFolder";
            lblFolder.Size = new Size(112, 23);
            lblFolder.TabIndex = 0;
            lblFolder.Text = "이미지 폴더";
            lblFolder.TextAlign = ContentAlignment.MiddleLeft;
            //
            // txtFolder
            //
            txtFolder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtFolder.Location = new Point(126, 12);
            txtFolder.Name = "txtFolder";
            txtFolder.PlaceholderText = "폴더를 창에 끌어다 놓거나 [찾아보기]로 선택 (경로 입력 후 Enter)";
            txtFolder.Size = new Size(900, 23);
            txtFolder.TabIndex = 1;
            //
            // btnBrowseFolder
            //
            btnBrowseFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowseFolder.Location = new Point(1034, 11);
            btnBrowseFolder.Name = "btnBrowseFolder";
            btnBrowseFolder.Size = new Size(92, 26);
            btnBrowseFolder.TabIndex = 2;
            btnBrowseFolder.Text = "찾아보기...";
            btnBrowseFolder.UseVisualStyleBackColor = true;
            //
            // chkSubfolders
            //
            chkSubfolders.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkSubfolders.Location = new Point(1138, 12);
            chkSubfolders.Name = "chkSubfolders";
            chkSubfolders.Size = new Size(250, 24);
            chkSubfolders.TabIndex = 3;
            chkSubfolders.Text = "하위 폴더 포함";
            chkSubfolders.UseVisualStyleBackColor = true;
            //
            // lblModel
            //
            lblModel.Location = new Point(12, 42);
            lblModel.Name = "lblModel";
            lblModel.Size = new Size(112, 23);
            lblModel.TabIndex = 4;
            lblModel.Text = "AI 모델 (ONNX)";
            lblModel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // txtModel
            //
            txtModel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtModel.Location = new Point(126, 42);
            txtModel.Name = "txtModel";
            txtModel.ReadOnly = true;
            txtModel.Size = new Size(900, 23);
            txtModel.TabIndex = 5;
            //
            // btnBrowseModel
            //
            btnBrowseModel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowseModel.Location = new Point(1034, 41);
            btnBrowseModel.Name = "btnBrowseModel";
            btnBrowseModel.Size = new Size(92, 26);
            btnBrowseModel.TabIndex = 6;
            btnBrowseModel.Text = "찾아보기...";
            btnBrowseModel.UseVisualStyleBackColor = true;
            //
            // chkGpu
            //
            chkGpu.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkGpu.Location = new Point(1138, 42);
            chkGpu.Name = "chkGpu";
            chkGpu.Size = new Size(250, 24);
            chkGpu.TabIndex = 7;
            chkGpu.Text = "GPU 사용 (DirectML)";
            chkGpu.UseVisualStyleBackColor = true;
            //
            // lblOutput
            //
            lblOutput.Location = new Point(12, 72);
            lblOutput.Name = "lblOutput";
            lblOutput.Size = new Size(112, 23);
            lblOutput.TabIndex = 8;
            lblOutput.Text = "결과 엑셀";
            lblOutput.TextAlign = ContentAlignment.MiddleLeft;
            //
            // txtOutput
            //
            txtOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtOutput.Location = new Point(126, 72);
            txtOutput.Name = "txtOutput";
            txtOutput.Size = new Size(900, 23);
            txtOutput.TabIndex = 9;
            //
            // btnBrowseOutput
            //
            btnBrowseOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowseOutput.Location = new Point(1034, 71);
            btnBrowseOutput.Name = "btnBrowseOutput";
            btnBrowseOutput.Size = new Size(92, 26);
            btnBrowseOutput.TabIndex = 10;
            btnBrowseOutput.Text = "찾아보기...";
            btnBrowseOutput.UseVisualStyleBackColor = true;
            //
            // btnOpenOutput
            //
            btnOpenOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpenOutput.Location = new Point(1136, 71);
            btnOpenOutput.Name = "btnOpenOutput";
            btnOpenOutput.Size = new Size(120, 26);
            btnOpenOutput.TabIndex = 11;
            btnOpenOutput.Text = "결과 폴더 열기";
            btnOpenOutput.UseVisualStyleBackColor = true;
            //
            // lblConf
            //
            lblConf.Location = new Point(12, 103);
            lblConf.Name = "lblConf";
            lblConf.Size = new Size(112, 23);
            lblConf.TabIndex = 12;
            lblConf.Text = "검출 Threshold";
            lblConf.TextAlign = ContentAlignment.MiddleLeft;
            //
            // nudConf
            //
            nudConf.DecimalPlaces = 2;
            nudConf.Increment = new decimal(new int[] { 1, 0, 0, 131072 });
            nudConf.Location = new Point(126, 103);
            nudConf.Maximum = new decimal(new int[] { 99, 0, 0, 131072 });
            nudConf.Minimum = new decimal(new int[] { 5, 0, 0, 131072 });
            nudConf.Name = "nudConf";
            nudConf.Size = new Size(70, 23);
            nudConf.TabIndex = 13;
            nudConf.TextAlign = HorizontalAlignment.Right;
            nudConf.Value = new decimal(new int[] { 35, 0, 0, 131072 });
            //
            // trackConf
            //
            trackConf.AutoSize = false;
            trackConf.LargeChange = 5;
            trackConf.Location = new Point(202, 101);
            trackConf.Maximum = 99;
            trackConf.Minimum = 5;
            trackConf.Name = "trackConf";
            trackConf.Size = new Size(300, 28);
            trackConf.TabIndex = 14;
            trackConf.TickStyle = TickStyle.None;
            trackConf.Value = 35;
            //
            // lblIou
            //
            lblIou.Location = new Point(520, 103);
            lblIou.Name = "lblIou";
            lblIou.Size = new Size(100, 23);
            lblIou.TabIndex = 15;
            lblIou.Text = "중복 제거 IoU";
            lblIou.TextAlign = ContentAlignment.MiddleRight;
            //
            // nudIou
            //
            nudIou.DecimalPlaces = 2;
            nudIou.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
            nudIou.Location = new Point(626, 103);
            nudIou.Maximum = new decimal(new int[] { 100, 0, 0, 131072 });
            nudIou.Minimum = new decimal(new int[] { 10, 0, 0, 131072 });
            nudIou.Name = "nudIou";
            nudIou.Size = new Size(70, 23);
            nudIou.TabIndex = 16;
            nudIou.TextAlign = HorizontalAlignment.Right;
            nudIou.Value = new decimal(new int[] { 70, 0, 0, 131072 });
            //
            // chkShowScores
            //
            chkShowScores.Location = new Point(716, 103);
            chkShowScores.Name = "chkShowScores";
            chkShowScores.Size = new Size(100, 24);
            chkShowScores.TabIndex = 17;
            chkShowScores.Text = "점수 표시";
            chkShowScores.UseVisualStyleBackColor = true;
            //
            // btnStart
            //
            btnStart.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnStart.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnStart.Location = new Point(1034, 101);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(92, 28);
            btnStart.TabIndex = 18;
            btnStart.Text = "검출 시작";
            btnStart.UseVisualStyleBackColor = true;
            //
            // btnStop
            //
            btnStop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnStop.Enabled = false;
            btnStop.Location = new Point(1136, 101);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(60, 28);
            btnStop.TabIndex = 19;
            btnStop.Text = "중지";
            btnStop.UseVisualStyleBackColor = true;
            //
            // btnSaveCsv
            //
            btnSaveCsv.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveCsv.Location = new Point(1202, 101);
            btnSaveCsv.Name = "btnSaveCsv";
            btnSaveCsv.Size = new Size(84, 28);
            btnSaveCsv.TabIndex = 20;
            btnSaveCsv.Text = "엑셀 저장";
            btnSaveCsv.UseVisualStyleBackColor = true;
            //
            // btnSaveImage
            //
            btnSaveImage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveImage.Location = new Point(1292, 101);
            btnSaveImage.Name = "btnSaveImage";
            btnSaveImage.Size = new Size(96, 28);
            btnSaveImage.TabIndex = 21;
            btnSaveImage.Text = "이미지 저장";
            btnSaveImage.UseVisualStyleBackColor = true;
            //
            // splitMain
            //
            splitMain.Dock = DockStyle.Fill;
            splitMain.Location = new Point(0, 136);
            splitMain.Name = "splitMain";
            //
            // splitMain.Panel1
            //
            splitMain.Panel1.Controls.Add(tabLeft);
            //
            // splitMain.Panel2
            //
            splitMain.Panel2.Controls.Add(viewer);
            splitMain.Size = new Size(1400, 702);
            splitMain.SplitterDistance = 520;
            splitMain.TabIndex = 1;
            //
            // tabLeft
            //
            tabLeft.Controls.Add(tabFiles);
            tabLeft.Controls.Add(tabReport);
            tabLeft.Dock = DockStyle.Fill;
            tabLeft.Location = new Point(0, 0);
            tabLeft.Name = "tabLeft";
            tabLeft.SelectedIndex = 0;
            tabLeft.Size = new Size(520, 702);
            tabLeft.TabIndex = 0;
            //
            // tabFiles
            //
            tabFiles.Controls.Add(dgvFiles);
            tabFiles.Location = new Point(4, 24);
            tabFiles.Name = "tabFiles";
            tabFiles.Padding = new Padding(3);
            tabFiles.Size = new Size(512, 674);
            tabFiles.TabIndex = 0;
            tabFiles.Text = "이미지 목록";
            tabFiles.UseVisualStyleBackColor = true;
            //
            // dgvFiles
            //
            dgvFiles.AllowUserToAddRows = false;
            dgvFiles.AllowUserToDeleteRows = false;
            dgvFiles.AllowUserToResizeRows = false;
            dgvFiles.BackgroundColor = SystemColors.Window;
            dgvFiles.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvFiles.Dock = DockStyle.Fill;
            dgvFiles.Location = new Point(3, 3);
            dgvFiles.MultiSelect = false;
            dgvFiles.Name = "dgvFiles";
            dgvFiles.ReadOnly = true;
            dgvFiles.RowHeadersVisible = false;
            dgvFiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFiles.Size = new Size(506, 668);
            dgvFiles.TabIndex = 0;
            //
            // tabReport
            //
            tabReport.Controls.Add(dgvReport);
            tabReport.Location = new Point(4, 24);
            tabReport.Name = "tabReport";
            tabReport.Padding = new Padding(3);
            tabReport.Size = new Size(512, 674);
            tabReport.TabIndex = 1;
            tabReport.Text = "결과표 (Results Report)";
            tabReport.UseVisualStyleBackColor = true;
            //
            // dgvReport
            //
            dgvReport.AllowUserToAddRows = false;
            dgvReport.AllowUserToDeleteRows = false;
            dgvReport.AllowUserToResizeRows = false;
            dgvReport.BackgroundColor = SystemColors.Window;
            dgvReport.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvReport.Dock = DockStyle.Fill;
            dgvReport.Location = new Point(3, 3);
            dgvReport.Name = "dgvReport";
            dgvReport.ReadOnly = true;
            dgvReport.RowHeadersVisible = false;
            dgvReport.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvReport.Size = new Size(506, 668);
            dgvReport.TabIndex = 0;
            //
            // viewer
            //
            viewer.Dock = DockStyle.Fill;
            viewer.Location = new Point(0, 0);
            viewer.Name = "viewer";
            viewer.Size = new Size(876, 702);
            viewer.TabIndex = 0;
            //
            // statusStrip
            //
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus, progressBar, lblSummary });
            statusStrip.Location = new Point(0, 838);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1400, 22);
            statusStrip.TabIndex = 2;
            //
            // lblStatus
            //
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(1000, 17);
            lblStatus.Spring = true;
            lblStatus.Text = "준비";
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            //
            // progressBar
            //
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(200, 16);
            //
            // lblSummary
            //
            lblSummary.Name = "lblSummary";
            lblSummary.Size = new Size(0, 17);
            //
            // MainForm
            //
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1400, 860);
            Controls.Add(splitMain);
            Controls.Add(pnlTop);
            Controls.Add(statusStrip);
            MinimumSize = new Size(1100, 600);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Cell Count - 염색 세포 검출 (YOLO26)";
            pnlTop.ResumeLayout(false);
            pnlTop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudConf).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackConf).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudIou).EndInit();
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            tabLeft.ResumeLayout(false);
            tabFiles.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvFiles).EndInit();
            tabReport.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvReport).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Panel pnlTop;
        private Label lblFolder;
        private TextBox txtFolder;
        private Button btnBrowseFolder;
        private CheckBox chkSubfolders;
        private Label lblModel;
        private TextBox txtModel;
        private Button btnBrowseModel;
        private CheckBox chkGpu;
        private Label lblOutput;
        private TextBox txtOutput;
        private Button btnBrowseOutput;
        private Button btnOpenOutput;
        private Label lblConf;
        private NumericUpDown nudConf;
        private TrackBar trackConf;
        private Label lblIou;
        private NumericUpDown nudIou;
        private CheckBox chkShowScores;
        private Button btnStart;
        private Button btnStop;
        private Button btnSaveCsv;
        private Button btnSaveImage;
        private SplitContainer splitMain;
        private TabControl tabLeft;
        private TabPage tabFiles;
        private DataGridView dgvFiles;
        private TabPage tabReport;
        private DataGridView dgvReport;
        private CellCount.Controls.ImageViewer viewer;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        private ToolStripProgressBar progressBar;
        private ToolStripStatusLabel lblSummary;
    }
}
