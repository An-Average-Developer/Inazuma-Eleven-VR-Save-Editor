using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Models
{
    public class PlayerSpiritInfo : INotifyPropertyChanged
    {
        private bool _isEnabled = false;
        private int _selectedRarity = 1; // Default to Growing (1)

        public string Name { get; set; } = string.Empty;
        public string MixDescription { get; set; } = string.Empty;
        public uint PlayerId { get; set; }

        public string DisplayName => $"{Name}";
        public string MixDisplay => MixDescription;

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

        public int SelectedRarity
        {
            get => _selectedRarity;
            set
            {
                if (_selectedRarity != value)
                {
                    _selectedRarity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RarityDisplay));
                }
            }
        }

        public string RarityDisplay => SelectedRarity switch
        {
            0 => "Common",
            1 => "Growing",
            2 => "Advanced",
            3 => "Top",
            4 => "Legendary",
            _ => "Growing"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
