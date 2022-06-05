using PoeFilterX.Business.Enums;

namespace PoeFilterX.Business.Models
{
    public class MiniMapIcon
    {
        public int Size { get; set; } = 2;

        public FilterColor Color { get; set; } = FilterColor.White;

        public MiniMapIconShape Shape { get; set; } = MiniMapIconShape.Square;

        public bool Enabled { get; set; } = true;

        public override string ToString()
        {
            return $"{Size} {Color} {Shape}";
        }
    }
}