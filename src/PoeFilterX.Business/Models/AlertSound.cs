namespace PoeFilterX.Business.Models
{
    public class AlertSound
    {
        public int Id { get; set; }

        public int? Volume { get; set; }

        public bool Positional { get; set; }

        public bool Enabled { get; set; } = true;

        public override string ToString()
        {
            return Volume.HasValue ? $"{Id} {Volume}" : $"{Id}";
        }
    }
}