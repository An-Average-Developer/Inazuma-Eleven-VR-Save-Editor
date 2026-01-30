namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Models
{
    public class SpecialMove
    {
        public string HexId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";

        public string DisplayName => $"{Name} ({HexId})";

        public SpecialMove() { }

        public SpecialMove(string hexId, string name, string category)
        {
            HexId = hexId;
            Name = name;
            Category = category;
        }
    }
}
