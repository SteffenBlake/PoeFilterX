using PoeFilterX.Business.Enums;

namespace PoeFilterX.Business.Models
{
    public class PlayEffect
    {
        public FilterColor? Color { get; set; }

        public bool Temporary { get; set; } = false;

        public bool Enabled { get; set; } = true;

        public override string ToString()
        {
            return Temporary ? $"{Color} Temp" : $"{Color}";
        }
    }
}