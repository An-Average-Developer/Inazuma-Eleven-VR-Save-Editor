using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Models
{
    public class SpiritCardInfo : INotifyPropertyChanged
    {
        private bool _isEnabled = false;

        public string Name { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public uint SpiritId { get; set; }

        public string DisplayName => $"{Name}";
        public string VariantDisplay => Variant;

        public string ImagePath
        {
            get
            {
                // Map code names to image file names
                var nameMapping = new Dictionary<string, string>
                {
                    { "Alpha", "Alfa" },
                    { "Arion Sherwind", "Arion" },
                    { "Archer Hawkins", "Archer" },
                    { "Arculus Orbes", "Arculus" },
                    { "Austin Hobbes", "Austin" },
                    { "Axel Blaze", "Axel" },
                    { "Bailong", "Bai Long" },
                    { "Bash Lancer", "Bash" },
                    { "Bellatrix", "Bellatrix" },
                    { "Beta", "Beta" },
                    { "Buddy Fury", "Buddy" },
                    { "Byron Love", "Byron" },
                    { "Briar Bloomhurst", "Briar" },
                    { "Caleb Stonewall", "Caleb" },
                    { "Circulus Corona", "Circulus" },
                    { "Darren LaChance", "Darren" },
                    { "Dvalin", "Dvalin" },
                    { "Erik Eagle", "Erik" },
                    { "Falco Flashman", "Falco" },
                    { "Fei Rune", "Fei" },
                    { "Gabriel Garcia", "Gabi" },
                    { "Gamma", "Gamma" },
                    { "Gandares Baran", "Gandares" },
                    { "Gazelle", "Gazelle" },
                    { "Goldie Lemmon", "Goldie" },
                    { "Harper Evans", "Harper Evans" },
                    { "Hector Helio", "Hector" },
                    { "Hurley Kane", "Hurley" },
                    { "Jack Wallside", "Jack" },
                    { "Janus", "Janus" },
                    { "Jean-Pierre Lapin", "JP" },
                    { "Joseph King", "Joseph" },
                    { "Jude Sharp", "Jude Sharp" },
                    { "Mark Evans", "Mark Evans" },
                    { "Nathan Swift", "Nathan" },
                    { "Ozrock Boldar", "Ozrock" },
                    { "Paolo Bianchi", "Paolo" },
                    { "Plink Powai", "Plink Powai" },
                    { "Quentin Cinquedea", "Quentin" },
                    { "Riccardo Di Rigo", "Riccardo" },
                    { "Rondula Flehl", "Rondula" },
                    { "Ruger Baran", "Ruger" },
                    { "Samguk Han", "Samguk" },
                    { "Simeon Ayp", "Simeon" },
                    { "Shawn Froste", "Shawn" },
                    { "Terry Archibald", "Terry" },
                    { "Tezcat", "Tezcat" },
                    { "Torch", "Torch" },
                    { "Victor Blade", "Victor" },
                    { "Xene", "Xene" },
                    { "Zanark Avalonic", "Zanark" }
                };

                string imageName = nameMapping.ContainsKey(Name) ? nameMapping[Name] : Name;

                // Map variant names to image file variant names
                string imageVariant = Variant switch
                {
                    "White-Black" when imageName == "Victor" => "Dark and White",
                    "White-Black" when imageName == "Xene" => "Black and Withe",
                    "White-Black" => "Black and White",
                    "Black" => "Black and White",
                    _ => Variant
                };

                return $"/Resources/Spirits/{imageName} {imageVariant}.jpg";
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
