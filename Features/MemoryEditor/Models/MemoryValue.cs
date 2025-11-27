using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Models
{
    public class MemoryValue : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _currentValue;
        private int _newValue;
        private string _description = string.Empty;
        private long _baseAddress;
        private int[] _offsets = Array.Empty<int>();

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public int CurrentValue
        {
            get => _currentValue;
            set
            {
                _currentValue = value;
                OnPropertyChanged();
            }
        }

        public int NewValue
        {
            get => _newValue;
            set
            {
                _newValue = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public long BaseAddress
        {
            get => _baseAddress;
            set
            {
                _baseAddress = value;
                OnPropertyChanged();
            }
        }

        public int[] Offsets
        {
            get => _offsets;
            set
            {
                _offsets = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
