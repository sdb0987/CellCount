using System.ComponentModel;
using System.Drawing.Drawing2D;
using YD.AI.Yolo;

namespace CellCount.Controls
{
    /// <summary>
    /// 검출 결과 표시용 이미지 뷰어.
    ///  - 마우스 휠: 확대/축소,  드래그: 이동,  더블클릭: 화면 맞춤
    ///  - 박스는 화면 해상도로 그리므로 축소 상태에서도 선이 뭉개지지 않는다.
    /// </summary>
    public sealed class ImageViewer : Control
    {
        private Bitmap _image;
        private IReadOnlyList<YoloPrediction> _detections;
        private float _zoom = 1f;       // 화면 픽셀 / 이미지 픽셀
        private PointF _offset;         // 이미지 (0,0) 의 화면 좌표
        private bool _fit = true;
        private Point _dragStart;
        private PointF _dragOffset;
        private bool _dragging;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public Color BoxColor { get; set; } = Color.Blue;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public bool ShowScores { get; set; }

        /// <summary>이미지 좌상단에 표시할 문구 (예: "세포 수: 372")</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Caption { get; set; }

        public ImageViewer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(40, 40, 40);
        }

        /// <summary>새 이미지를 표시한다. 이전 이미지는 뷰어가 해제한다.</summary>
        public void SetImage(Bitmap image)
        {
            Bitmap old = _image;
            _image = image;
            _fit = true;
            old?.Dispose();
            Invalidate();
        }

        public void SetDetections(IReadOnlyList<YoloPrediction> detections)
        {
            _detections = detections;
            Invalidate();
        }

        /// <summary>현재 이미지에 박스를 그려 넣은 사본을 만든다(결과 이미지 저장용).</summary>
        public Bitmap RenderAnnotated()
        {
            if (_image == null) return null;
            var bmp = new Bitmap(_image);
            using (var g = Graphics.FromImage(bmp))
            {
                DrawOverlay(g, 1f, PointF.Empty, Math.Max(2f, bmp.Width / 1000f), Math.Max(12f, bmp.Width / 110f));
            }
            return bmp;
        }

        private void UpdateFit()
        {
            if (_image == null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            _zoom = Math.Min(ClientSize.Width / (float)_image.Width, ClientSize.Height / (float)_image.Height);
            _offset = new PointF((ClientSize.Width - _image.Width * _zoom) / 2f, (ClientSize.Height - _image.Height * _zoom) / 2f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (_image == null)
            {
                TextRenderer.DrawText(g, "이미지 폴더를 여기로 끌어다 놓거나 [찾아보기]로 선택하세요.", Font, ClientRectangle,
                    Color.Silver, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            if (_fit) UpdateFit();

            g.InterpolationMode = _zoom < 1f ? InterpolationMode.Bilinear : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_image, new RectangleF(_offset.X, _offset.Y, _image.Width * _zoom, _image.Height * _zoom));
            DrawOverlay(g, _zoom, _offset, 1.6f, 11f);
        }

        private void DrawOverlay(Graphics g, float zoom, PointF offset, float penWidth, float fontSize)
        {
            g.SmoothingMode = SmoothingMode.None;
            if (_detections != null)
            {
                using (var pen = new Pen(BoxColor, penWidth))
                using (var font = new Font(FontFamily.GenericSansSerif, fontSize * 0.8f))
                using (var brush = new SolidBrush(BoxColor))
                {
                    foreach (var d in _detections)
                    {
                        float x = offset.X + d.Rectangle.X * zoom, y = offset.Y + d.Rectangle.Y * zoom;
                        g.DrawRectangle(pen, x, y, d.Rectangle.Width * zoom, d.Rectangle.Height * zoom);
                        if (ShowScores && zoom >= 0.45f)
                            g.DrawString(d.Score.ToString("0.00"), font, brush, x + 1, y + 1);
                    }
                }
            }
            if (!string.IsNullOrEmpty(Caption))
            {
                using (var font = new Font(FontFamily.GenericSansSerif, fontSize * 1.4f, FontStyle.Bold))
                {
                    SizeF sz = g.MeasureString(Caption, font);
                    float x = Math.Max(offset.X, 0) + 6, y = Math.Max(offset.Y, 0) + 6;
                    using (var back = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
                        g.FillRectangle(back, x - 3, y - 2, sz.Width + 6, sz.Height + 4);
                    using (var brush = new SolidBrush(BoxColor))
                        g.DrawString(Caption, font, brush, x, y);
                }
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_image == null) return;
            if (_fit) { UpdateFit(); _fit = false; }
            float factor = e.Delta > 0 ? 1.25f : 0.8f;
            float newZoom = Math.Max(0.05f, Math.Min(16f, _zoom * factor));
            // 커서 아래의 이미지 지점이 그대로 있도록 오프셋 보정
            float ix = (e.X - _offset.X) / _zoom, iy = (e.Y - _offset.Y) / _zoom;
            _zoom = newZoom;
            _offset = new PointF(e.X - ix * _zoom, e.Y - iy * _zoom);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Left && _image != null)
            {
                if (_fit) { UpdateFit(); _fit = false; }
                _dragging = true;
                _dragStart = e.Location;
                _dragOffset = _offset;
                Cursor = Cursors.SizeAll;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_dragging) return;
            _offset = new PointF(_dragOffset.X + e.X - _dragStart.X, _dragOffset.Y + e.Y - _dragStart.Y);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            _fit = true;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _image?.Dispose();
            base.Dispose(disposing);
        }
    }
}
