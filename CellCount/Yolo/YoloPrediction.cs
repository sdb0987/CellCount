using System.Drawing;

namespace YD.AI.Yolo
{
    /// <summary>검출된 객체 1개 (원본 이미지 좌표계)</summary>
    public sealed class YoloPrediction
    {
        public RectangleF Rectangle { get; set; }
        public float Score { get; set; }
        public int ClassId { get; set; }
    }
}
