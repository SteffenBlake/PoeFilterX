using PoeFilterX.Business.Enums;

namespace PoeFilterX.Business.Models
{
    public class MiniMapIcon
    {
        public int? Size { get; set; }

        public FilterColor? Color { get; set; }

        public MiniMapIconShape? Shape { get; set; }

        public bool Enabled { get; set; } = true;

        public bool HasValue => Size is not null && Color is not null && Shape is not null;

        public override string ToString()
        {
            return $"{Size} {Color} {Shape}";
        }
    }
}