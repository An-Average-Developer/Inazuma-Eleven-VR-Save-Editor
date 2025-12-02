using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using InazumaElevenVRSaveEditor.Features.MemoryEditor.Models;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.ViewModels
{
    public class ItemListWindowViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ItemInfo> Items { get; }

        public ItemListWindowViewModel()
        {
            Items = new ObservableCollection<ItemInfo>
            {
                new ItemInfo
                {
                    Name = "Inazuma Flowers",
                    ImagePath = "/Resources/Cards/Inazuma Flower.png",
                    Category = "Flowers"
                },
                new ItemInfo
                {
                    Name = "God Hand Flower",
                    ImagePath = "/Resources/Cards/God Hand Flower.png",
                    Category = "Flowers"
                },
                new ItemInfo
                {
                    Name = "Beans",
                    ImagePath = "/Resources/Cards/Kicking Power.png",
                    Category = "Beans"
                },
                new ItemInfo
                {
                    Name = "Stars",
                    ImagePath = "/Resources/Cards/Stars.png",
                    Category = "Stars"
                },
                new ItemInfo
                {
                    Name = "Spirits",
                    ImagePath = "/Resources/Cards/Spirits.png",
                    Category = "Spirits"
                },
                new ItemInfo
                {
                    Name = "Victory Stars",
                    ImagePath = "/Resources/Cards/VSTAR.png",
                    Category = "Victory Items"
                },
                new ItemInfo
                {
                    Name = "New Possibilities",
                    ImagePath = "/Resources/Cards/NPOSS.png",
                    Category = "Special"
                }
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
