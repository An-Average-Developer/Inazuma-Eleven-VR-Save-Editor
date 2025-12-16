using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using InazumaElevenVRSaveEditor.Common.Infrastructure;
using InazumaElevenVRSaveEditor.Features.MemoryEditor.Models;
using InazumaElevenVRSaveEditor.Features.MemoryEditor.Services;
using InazumaElevenVRSaveEditor.Features.MemoryEditor.Views;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.ViewModels
{
    public class MemoryEditorViewModel : INotifyPropertyChanged
    {
        private readonly MemoryEditorService _memoryService;
        private readonly DispatcherTimer _updateTimer;
        private readonly DispatcherTimer _autoAttachTimer;
        private MemoryValue? _selectedValue;
        private bool _isAttached;
        private string _statusMessage = "Searching for game process...";
        private bool _autoRefresh = true;
        private int _lastKnownGoodTicketValue = 0;
        private string _selectedTool = "menu";
        private bool _isStarsFrozen = false;
        private bool _isFlowersIncrementEnabled = false;
        private bool _isSpiritsFrozen = false;
        private bool _isSpiritIncrementEnabled = false;
        private bool _isStoreItemMultiplierEnabled = false;
        private bool _isPassiveValueEditingEnabled = false;

        // Individual maintenance flags for each card
        private bool _isStarsUnderMaintenance = true;
        private bool _isFlowersUnderMaintenance = true;
        private bool _isSpiritsUnderMaintenance = true;
        private bool _isBeansUnderMaintenance = true;
        private bool _isVictoryItemsUnderMaintenance = true;
        private bool _isPassiveValuesUnderMaintenance = false;

        // Passive value tracking
        private string _passiveValueType = "Unknown";
        private string _passiveCurrentValue = "N/A";
        private string _passiveNewValue = "";
        private bool _hasPassiveValue = false;

        private const long STAR_FREEZE_ADDRESS = 0xD95F1D;

        private const long FLOWER_INCREMENT_ADDRESS = 0xD95F15;

        private const long SPIRIT_FREEZE_ADDRESS = 0xCE9A46;
        private const long ELITE_SPIRIT_FREEZE_ADDRESS = 0xCE9A15;

        private static readonly byte[] FREEZE_BYTES = new byte[] { 0x90, 0x90, 0x90 };
        private static readonly byte[] ORIGINAL_BYTES = new byte[] { 0x89, 0x50, 0x10 };

        private static readonly byte[] FLOWER_ORIGINAL_BYTES = new byte[] { 0x2B, 0xCD };
        private static readonly byte[] FLOWER_INCREMENT_BYTES = new byte[] { 0x03, 0xCD };

        private static readonly byte[] SPIRIT_ORIGINAL_BYTES = new byte[] { 0x66, 0x89, 0x68, 0x0C };

        private static readonly byte[] SPIRIT_FREEZE_BYTES = new byte[] { 0x90, 0x90, 0x90, 0x90 };

        private static readonly byte[] ELITE_SPIRIT_ORIGINAL_BYTES = new byte[] { 0x66, 0x41, 0x89, 0x6C, 0x78, 0x10 };
        private static readonly byte[] ELITE_SPIRIT_FREEZE_BYTES = new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };

        public MemoryEditorViewModel()
        {
            _memoryService = new MemoryEditorService();

            WorkingItems = new ObservableCollection<ItemInfo>
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

            AttachToProcessCommand = new RelayCommand(AttachToProcess, CanAttachToProcess);
            DetachFromProcessCommand = new RelayCommand(DetachFromProcess, CanDetachFromProcess);
            RefreshValuesCommand = new RelayCommand(RefreshValues, CanRefreshValues);
            ApplyValueCommand = new RelayCommand(ApplyValue, CanApplyValue);
            SelectTicketEditorCommand = new RelayCommand(() =>
            {
                SelectedTool = "tickets";
                SelectedValue = StarsValue;
            });
            SelectInaFlowersEditorCommand = new RelayCommand(() =>
            {
                SelectedTool = "inaflowers";
                SelectedValue = InaFlowersValue;
            });
            SelectSpiritsEditorCommand = new RelayCommand(() =>
            {
                SelectedTool = "spirits";
                SelectedValue = null;
            });
            SelectBeansEditorCommand = new RelayCommand(() =>
            {
                SelectedTool = "beans";
                SelectedValue = InstantaneousValue;
            });
            SelectVictoryItemsEditorCommand = new RelayCommand(() =>
            {
                SelectedTool = "victoryitems";
                SelectedValue = VictoryStarValue;
            });
            BackToMenuCommand = new RelayCommand(() => SelectedTool = "menu");
            ToggleStarsFreezeCommand = new RelayCommand(ToggleStarsFreeze, CanToggleStarsFreeze);
            ToggleFlowersIncrementCommand = new RelayCommand(ToggleFlowersIncrement, CanToggleFlowersIncrement);
            ToggleSpiritsFreezeCommand = new RelayCommand(ToggleSpiritsFreeze, CanToggleSpiritsFreeze);
            ToggleSpiritIncrementCommand = new RelayCommand(ToggleSpiritIncrement, CanToggleSpiritIncrement);
            ToggleStoreItemMultiplierCommand = new RelayCommand(ToggleStoreItemMultiplier, CanToggleStoreItemMultiplier);
            TogglePassiveValueEditingCommand = new RelayCommand(TogglePassiveValueEditing, CanTogglePassiveValueEditing);
            AddSpiritsCommand = new RelayCommand(AddSpiritsValue, CanAddSpirits);
            AddBeansCommand = new RelayCommand(AddBeansValue, CanAddBeans);
            OpenItemListCommand = new RelayCommand(OpenItemListWindow);
            SelectPassiveValuesEditorCommand = new RelayCommand(() =>
            {
                // Show warning before entering Passive Values editor
                var result = MessageBox.Show(
                    "⚠️ IMPORTANT WARNING ⚠️\n\n" +
                    "• Passive values you edit will affect ALL players who use this passive\n" +
                    "• Passive values will reset to default after restarting the game\n" +
                    "• Use this feature carefully\n\n" +
                    "Do you want to continue?",
                    "Passive Values Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    SelectedTool = "passivevalues";
                    SelectedValue = null;
                }
            });
            ApplyPassiveValueCommand = new RelayCommand(ApplyPassiveValue, CanApplyPassiveValue);

            MemoryValues = new ObservableCollection<MemoryValue>
            {
                new MemoryValue
                {
                    Name = "Stars",
                    Description = "Stars for the Gachapon",
                    BaseAddress = 0x01AD1828,
                    Offsets = new int[] { 0xFE8, 0x20F0, 0x8, 0x10, 0xA0, 0x4C },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Inazuma Flowers",
                    Description = "Inazuma Flowers (object)",
                    BaseAddress = 0x01AD1828,
                    Offsets = new int[] { 0xFE8, 0x20E0, 0x18, 0x90, 0x218 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "God Hand",
                    Description = "God Hand Flowers (object)",
                    BaseAddress = 0x01AD1828,
                    Offsets = new int[] { 0x1148, 0x2000, 0x8, 0x170, 0xB0 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Harper Evans Breach",
                    Description = "Harper Evans - Breach Spirit",
                    BaseAddress = 0x0208DBE8,
                    Offsets = new int[] { 0x10, 0x10, 0x10, 0x10, 0x8, 0x1B0, 0xC },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Hector Helio Justice",
                    Description = "Hector Helio Justice Spirit",
                    BaseAddress = 0x0208DBE8,
                    Offsets = new int[] { 0x10, 0x10, 0x10, 0x10, 0x8, 0x50, 0xD4 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Instantaneous",
                    Description = "Instantaneous Bean",
                    BaseAddress = 0x01AC27A8,
                    Offsets = new int[] { 0xFE8, 0x1F98, 0x60, 0x4100, 0x7A1C },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Intelligence",
                    Description = "Intelligence Bean",
                    BaseAddress = 0x01AC27A8,
                    Offsets = new int[] { 0xFE8, 0x1F98, 0x70, 0x3F0, 0x12E0 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Kicking Power",
                    Description = "Kicking Power Bean",
                    BaseAddress = 0x01AC27A8,
                    Offsets = new int[] { 0xFE8, 0x1F98, 0x70, 0x920, 0xAC4 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Mind's Eye",
                    Description = "Mind's Eye Bean",
                    BaseAddress = 0x01AC27A8,
                    Offsets = new int[] { 0xFE8, 0x1F98, 0x70, 0x6A0, 0xEA0 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Strength",
                    Description = "Strength Bean",
                    BaseAddress = 0x01AC27A8,
                    Offsets = new int[] { 0xFE8, 0x1F98, 0x60, 0x43A0, 0x75A0 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Technique",
                    Description = "Technique Bean",
                    BaseAddress = 0x01AC27A8,
                    Offsets = new int[] { 0xFE8, 0x1F98, 0x70, 0x1B0, 0x1624 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Unshakable",
                    Description = "Unshakable Bean",
                    BaseAddress = 0x01AC27A8,
                    Offsets = new int[] { 0x1148, 0x2000, 0x68, 0x630, 0x1DE4 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Victory Star",
                    Description = "Victory Star",
                    BaseAddress = 0x020B67A0,
                    Offsets = new int[] { 0x6018, 0x20, 0x10, 0x1018, 0x20F0, 0xA0, 0x10 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Victory Stone",
                    Description = "Victory Stone",
                    BaseAddress = 0x01AC87F8,
                    Offsets = new int[] { 0x1148, 0x2000, 0xA0, 0x9E8, 0x10, 0x50, 0x4C },
                    CurrentValue = 0,
                    NewValue = 0
                }
            };

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            _autoAttachTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _autoAttachTimer.Tick += AutoAttachTimer_Tick;
            _autoAttachTimer.Start();

            TryAutoAttach();
        }

        public ObservableCollection<MemoryValue> MemoryValues { get; }

        public MemoryValue StarsValue => MemoryValues.FirstOrDefault(v => v.Name == "Stars")!;

        public MemoryValue InaFlowersValue => MemoryValues.FirstOrDefault(v => v.Name == "Inazuma Flowers")!;

        public MemoryValue GodHandFlowersValue => MemoryValues.FirstOrDefault(v => v.Name == "God Hand")!;

        public MemoryValue HarperEvansBreachValue => MemoryValues.FirstOrDefault(v => v.Name == "Harper Evans Breach")!;

        public MemoryValue HectorHelioJusticeValue => MemoryValues.FirstOrDefault(v => v.Name == "Hector Helio Justice")!;

        public MemoryValue InstantaneousValue => MemoryValues.FirstOrDefault(v => v.Name == "Instantaneous")!;

        public MemoryValue IntelligenceValue => MemoryValues.FirstOrDefault(v => v.Name == "Intelligence")!;

        public MemoryValue KickingPowerValue => MemoryValues.FirstOrDefault(v => v.Name == "Kicking Power")!;

        public MemoryValue MindsEyeValue => MemoryValues.FirstOrDefault(v => v.Name == "Mind's Eye")!;

        public MemoryValue StrengthValue => MemoryValues.FirstOrDefault(v => v.Name == "Strength")!;

        public MemoryValue TechniqueValue => MemoryValues.FirstOrDefault(v => v.Name == "Technique")!;

        public MemoryValue UnshakableValue => MemoryValues.FirstOrDefault(v => v.Name == "Unshakable")!;

        public MemoryValue VictoryStarValue => MemoryValues.FirstOrDefault(v => v.Name == "Victory Star")!;

        public MemoryValue VictoryStoneValue => MemoryValues.FirstOrDefault(v => v.Name == "Victory Stone")!;

        public MemoryValue[] StarsCollection => new[] { StarsValue }.Where(v => v != null).ToArray();

        public MemoryValue[] InaFlowersCollection => new[] { InaFlowersValue, GodHandFlowersValue }.Where(v => v != null).ToArray();

        public MemoryValue[] SpiritsCollection => new[] { HarperEvansBreachValue, HectorHelioJusticeValue }.Where(v => v != null).ToArray();

        public MemoryValue[] BeansCollection => new[] { InstantaneousValue, IntelligenceValue, KickingPowerValue, MindsEyeValue, StrengthValue, TechniqueValue, UnshakableValue }.Where(v => v != null).ToArray();

        public MemoryValue[] VictoryItemsCollection => new[] { VictoryStarValue, VictoryStoneValue }.Where(v => v != null).ToArray();

        public ObservableCollection<ItemInfo> WorkingItems { get; }

        public MemoryValue? SelectedValue
        {
            get => _selectedValue;
            set
            {
                _selectedValue = value;
                OnPropertyChanged();
                ((RelayCommand)ApplyValueCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsAttached
        {
            get => _isAttached;
            set
            {
                _isAttached = value;
                OnPropertyChanged();
                ((RelayCommand)AttachToProcessCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DetachFromProcessCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RefreshValuesCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ApplyValueCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleStarsFreezeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleFlowersIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritsFreezeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleStoreItemMultiplierCommand).RaiseCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool AutoRefresh
        {
            get => _autoRefresh;
            set
            {
                _autoRefresh = value;
                OnPropertyChanged();

                if (_autoRefresh && IsAttached)
                {
                    _updateTimer.Start();
                }
                else
                {
                    _updateTimer.Stop();
                }
            }
        }

        public string SelectedTool
        {
            get => _selectedTool;
            set
            {
                _selectedTool = value;
                OnPropertyChanged();
            }
        }

        public ICommand AttachToProcessCommand { get; }
        public ICommand DetachFromProcessCommand { get; }
        public ICommand RefreshValuesCommand { get; }
        public ICommand ApplyValueCommand { get; }
        public ICommand SelectTicketEditorCommand { get; }
        public ICommand SelectInaFlowersEditorCommand { get; }
        public ICommand SelectSpiritsEditorCommand { get; }
        public ICommand SelectBeansEditorCommand { get; }
        public ICommand SelectVictoryItemsEditorCommand { get; }
        public ICommand BackToMenuCommand { get; }
        public ICommand ToggleStarsFreezeCommand { get; }
        public ICommand ToggleFlowersIncrementCommand { get; }
        public ICommand ToggleSpiritsFreezeCommand { get; }
        public ICommand ToggleSpiritIncrementCommand { get; }
        public ICommand ToggleStoreItemMultiplierCommand { get; }
        public ICommand TogglePassiveValueEditingCommand { get; }
        public ICommand AddSpiritsCommand { get; }
        public ICommand AddBeansCommand { get; }
        public ICommand OpenItemListCommand { get; }
        public ICommand SelectPassiveValuesEditorCommand { get; }
        public ICommand ApplyPassiveValueCommand { get; }

        public bool IsStarsFrozen
        {
            get => _isStarsFrozen;
            set
            {
                _isStarsFrozen = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleStarsFreezeCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsFlowersIncrementEnabled
        {
            get => _isFlowersIncrementEnabled;
            set
            {
                _isFlowersIncrementEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleFlowersIncrementCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsSpiritsFrozen
        {
            get => _isSpiritsFrozen;
            set
            {
                _isSpiritsFrozen = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleSpiritsFreezeCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsSpiritIncrementEnabled
        {
            get => _isSpiritIncrementEnabled;
            set
            {
                _isSpiritIncrementEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleSpiritIncrementCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsStoreItemMultiplierEnabled
        {
            get => _isStoreItemMultiplierEnabled;
            set
            {
                _isStoreItemMultiplierEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleStoreItemMultiplierCommand).RaiseCanExecuteChanged();
            }
        }

        // Stars maintenance
        public bool IsStarsUnderMaintenance
        {
            get => _isStarsUnderMaintenance;
            set
            {
                _isStarsUnderMaintenance = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStarsEnabled));
            }
        }
        public bool IsStarsEnabled => !_isStarsUnderMaintenance;

        // Flowers maintenance
        public bool IsFlowersUnderMaintenance
        {
            get => _isFlowersUnderMaintenance;
            set
            {
                _isFlowersUnderMaintenance = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFlowersEnabled));
            }
        }
        public bool IsFlowersEnabled => !_isFlowersUnderMaintenance;

        // Spirits maintenance
        public bool IsSpiritsUnderMaintenance
        {
            get => _isSpiritsUnderMaintenance;
            set
            {
                _isSpiritsUnderMaintenance = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSpiritsEnabled));
            }
        }
        public bool IsSpiritsEnabled => !_isSpiritsUnderMaintenance;

        // Beans maintenance
        public bool IsBeansUnderMaintenance
        {
            get => _isBeansUnderMaintenance;
            set
            {
                _isBeansUnderMaintenance = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBeansEnabled));
            }
        }
        public bool IsBeansEnabled => !_isBeansUnderMaintenance;

        // Victory Items maintenance
        public bool IsVictoryItemsUnderMaintenance
        {
            get => _isVictoryItemsUnderMaintenance;
            set
            {
                _isVictoryItemsUnderMaintenance = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVictoryItemsEnabled));
            }
        }
        public bool IsVictoryItemsEnabled => !_isVictoryItemsUnderMaintenance;

        // Passive Values maintenance
        public bool IsPassiveValuesUnderMaintenance
        {
            get => _isPassiveValuesUnderMaintenance;
            set
            {
                _isPassiveValuesUnderMaintenance = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPassiveValuesEnabled));
            }
        }
        public bool IsPassiveValuesEnabled => !_isPassiveValuesUnderMaintenance;

        public bool IsPassiveValueEditingEnabled
        {
            get => _isPassiveValueEditingEnabled;
            set
            {
                _isPassiveValueEditingEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)TogglePassiveValueEditingCommand).RaiseCanExecuteChanged();
            }
        }

        public string PassiveValueType
        {
            get => _passiveValueType;
            set
            {
                _passiveValueType = value;
                OnPropertyChanged();
            }
        }

        public string PassiveCurrentValue
        {
            get => _passiveCurrentValue;
            set
            {
                _passiveCurrentValue = value;
                OnPropertyChanged();
            }
        }

        public string PassiveNewValue
        {
            get => _passiveNewValue;
            set
            {
                _passiveNewValue = value;
                OnPropertyChanged();
            }
        }

        public bool HasPassiveValue
        {
            get => _hasPassiveValue;
            set
            {
                _hasPassiveValue = value;
                OnPropertyChanged();
                ((RelayCommand)ApplyPassiveValueCommand).RaiseCanExecuteChanged();
            }
        }

        private bool CanAttachToProcess(object? parameter)
        {
            return !IsAttached;
        }

        private async void AttachToProcess(object? parameter)
        {
            try
            {
                bool success = _memoryService.AttachToProcess();

                if (success)
                {
                    IsAttached = true;
                    StatusMessage = "Successfully attached - waiting for game to initialize...";
                    _autoAttachTimer.Stop();

                    // Wait 2 seconds for game to fully initialize before first read
                    await System.Threading.Tasks.Task.Delay(2000);

                    RefreshValues(null);

                    if (AutoRefresh)
                    {
                        _updateTimer.Start();
                    }
                }
                else
                {
                    StatusMessage = "Failed to attach. Make sure the game is running.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error attaching to process: {ex.Message}";
            }
        }

        private bool CanDetachFromProcess(object? parameter)
        {
            return IsAttached;
        }

        private void DetachFromProcess(object? parameter)
        {
            try
            {
                _updateTimer.Stop();
                _memoryService.DetachFromProcess();
                IsAttached = false;
                _lastKnownGoodTicketValue = 0;
                IsStarsFrozen = false;
                IsFlowersIncrementEnabled = false;
                IsSpiritsFrozen = false;
                IsSpiritIncrementEnabled = false;
                IsStoreItemMultiplierEnabled = false;
                StatusMessage = "Detached from game process. Searching for game...";

                _autoAttachTimer.Start();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error detaching from process: {ex.Message}";
            }
        }

        private bool CanRefreshValues(object? parameter)
        {
            return IsAttached;
        }

        private void RefreshValues(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                foreach (var memValue in MemoryValues)
                {
                    int value = _memoryService.ReadValue(memValue.BaseAddress, memValue.Offsets);

                    if (memValue.Name == "Tickets")
                    {
                        if (value == 0 && _lastKnownGoodTicketValue > 0)
                        {
                            value = _lastKnownGoodTicketValue;
                            StatusMessage = $"Using cached value (tickets menu may be active) - {DateTime.Now:HH:mm:ss}";
                        }
                        else if (value > 0)
                        {
                            _lastKnownGoodTicketValue = value;
                            StatusMessage = $"Values refreshed at {DateTime.Now:HH:mm:ss}";
                        }
                    }

                    memValue.CurrentValue = value;

                    if (memValue.NewValue == 0 || memValue.NewValue == memValue.CurrentValue)
                    {
                        memValue.NewValue = value;
                    }
                }

                if (!StatusMessage.Contains("cached value"))
                {
                    StatusMessage = $"Values refreshed at {DateTime.Now:HH:mm:ss}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reading values: {ex.Message}";
            }
        }

        private bool CanApplyValue(object? parameter)
        {
            return IsAttached && SelectedValue != null;
        }

        private void ApplyValue(object? parameter)
        {
            if (SelectedValue == null || !IsAttached)
                return;

            try
            {
                bool success = _memoryService.WriteValue(
                    SelectedValue.BaseAddress,
                    SelectedValue.Offsets,
                    SelectedValue.NewValue
                );

                if (success)
                {
                    SelectedValue.CurrentValue = SelectedValue.NewValue;
                    StatusMessage = $"Successfully updated {SelectedValue.Name} to {SelectedValue.NewValue}";
                }
                else
                {
                    StatusMessage = $"Failed to update {SelectedValue.Name}";

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"Failed to update {SelectedValue.Name}.\n\n" +
                            "Make sure the game is running and you are attached to the process.",
                            "Update Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error writing value: {ex.Message}";

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Error occurred while updating value:\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                });
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (IsAttached && AutoRefresh)
            {
                if (!_memoryService.IsProcessRunning())
                {
                    _updateTimer.Stop();
                    _memoryService.DetachFromProcess();
                    IsAttached = false;
                    _lastKnownGoodTicketValue = 0;
                    IsStarsFrozen = false;
                    IsFlowersIncrementEnabled = false;
                    IsSpiritsFrozen = false;
                    IsSpiritIncrementEnabled = false;
                    IsStoreItemMultiplierEnabled = false;
                    StatusMessage = "Game closed. Waiting for game to start...";
                    _autoAttachTimer.Start();
                    return;
                }

                RefreshValues(null);
            }
        }

        private void AutoAttachTimer_Tick(object? sender, EventArgs e)
        {
            TryAutoAttach();
        }

        private async void TryAutoAttach()
        {
            if (IsAttached)
                return;

            try
            {
                bool success = _memoryService.AttachToProcess();

                if (success)
                {
                    IsAttached = true;
                    StatusMessage = "Successfully attached - waiting for game to initialize...";
                    _autoAttachTimer.Stop();

                    // Wait 2 seconds for game to fully initialize before first read
                    await System.Threading.Tasks.Task.Delay(2000);

                    RefreshValues(null);

                    if (AutoRefresh)
                    {
                        _updateTimer.Start();
                    }
                }
                else
                {
                    StatusMessage = "Waiting for game to start...";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error while searching for game: {ex.Message}";
            }
        }

        private bool CanToggleStarsFreeze(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleStarsFreeze(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;
                if (!IsStarsFrozen)
                {
                    success = _memoryService.WriteBytes(STAR_FREEZE_ADDRESS, FREEZE_BYTES);
                    if (success)
                    {
                        IsStarsFrozen = true;
                        StatusMessage = "Stars frozen - unlimited stars enabled!";
                    }
                    else
                    {
                        StatusMessage = "Failed to freeze stars";
                        MessageBox.Show(
                            "Failed to freeze stars. Make sure the game is running and you are attached to the process.",
                            "Freeze Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.WriteBytes(STAR_FREEZE_ADDRESS, ORIGINAL_BYTES);
                    if (success)
                    {
                        IsStarsFrozen = false;
                        StatusMessage = "Stars unfrozen - normal star behavior restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to unfreeze stars";
                        MessageBox.Show(
                            "Failed to unfreeze stars.",
                            "Unfreeze Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling star freeze: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling star freeze:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleFlowersIncrement(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleFlowersIncrement(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsFlowersIncrementEnabled)
                {
                    success = _memoryService.WriteBytes(FLOWER_INCREMENT_ADDRESS, FLOWER_INCREMENT_BYTES);

                    if (success)
                    {
                        IsFlowersIncrementEnabled = true;
                        StatusMessage = "Flower increment enabled - flowers will increase when buying!";
                    }
                    else
                    {
                        StatusMessage = "Failed to enable flower increment";
                        MessageBox.Show(
                            "Failed to enable flower increment. Make sure the game is running and you are attached to the process.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.WriteBytes(FLOWER_INCREMENT_ADDRESS, FLOWER_ORIGINAL_BYTES);

                    if (success)
                    {
                        IsFlowersIncrementEnabled = false;
                        StatusMessage = "Flower increment disabled - normal flower behavior restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable flower increment";
                        MessageBox.Show(
                            "Failed to disable flower increment.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling flower increment: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling flower increment:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleSpiritsFreeze(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleSpiritsFreeze(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success1, success2;

                if (!IsSpiritsFrozen)
                {
                    // Show custom dialog asking about extra kenshins and hissatsus
                    var dialog = new Views.SpiritsFreezeConfirmDialog
                    {
                        Owner = Application.Current.MainWindow
                    };

                    bool? dialogResult = dialog.ShowDialog();

                    // Freeze spirits
                    success1 = _memoryService.WriteBytes(SPIRIT_FREEZE_ADDRESS, SPIRIT_FREEZE_BYTES);
                    success2 = _memoryService.WriteBytes(ELITE_SPIRIT_FREEZE_ADDRESS, ELITE_SPIRIT_FREEZE_BYTES);

                    if (success1 && success2)
                    {
                        IsSpiritsFrozen = true;
                        StatusMessage = "Spirits frozen - unlimited hero & elite spirits enabled!";

                        // If user clicked Yes, also activate the flowers increment feature
                        if (dialogResult == true && !IsFlowersIncrementEnabled)
                        {
                            bool flowerSuccess = _memoryService.WriteBytes(FLOWER_INCREMENT_ADDRESS, FLOWER_INCREMENT_BYTES);
                            if (flowerSuccess)
                            {
                                IsFlowersIncrementEnabled = true;
                                StatusMessage = "Spirits frozen with extra rewards - 9 bonus kenshins & hissatsus activated!";
                            }
                            else
                            {
                                MessageBox.Show(
                                    "Spirits frozen successfully, but failed to enable extra rewards.",
                                    "Partial Success",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                            }
                        }
                    }
                    else
                    {
                        StatusMessage = "Failed to freeze spirits";
                        MessageBox.Show(
                            "Failed to freeze spirits. Make sure the game is running and you are attached to the process.",
                            "Freeze Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Unfreeze spirits
                    success1 = _memoryService.WriteBytes(SPIRIT_FREEZE_ADDRESS, SPIRIT_ORIGINAL_BYTES);
                    success2 = _memoryService.WriteBytes(ELITE_SPIRIT_FREEZE_ADDRESS, ELITE_SPIRIT_ORIGINAL_BYTES);

                    if (success1 && success2)
                    {
                        IsSpiritsFrozen = false;
                        StatusMessage = "Spirits unfrozen - normal spirit behavior restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to unfreeze spirits";
                        MessageBox.Show(
                            "Failed to unfreeze spirits.",
                            "Unfreeze Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling spirit freeze: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling spirit freeze:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleSpiritIncrement(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleSpiritIncrement(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsSpiritIncrementEnabled)
                {
                    success = _memoryService.InjectSpiritIncrement();

                    if (success)
                    {
                        IsSpiritIncrementEnabled = true;
                        StatusMessage = "Spirit increment enabled - spirits will increase by 2 when used!";
                    }
                    else
                    {
                        StatusMessage = "Failed to enable spirit increment";
                        MessageBox.Show(
                            "Failed to enable spirit increment. Make sure the game is running and you are attached to the process.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.RemoveSpiritIncrement();

                    if (success)
                    {
                        IsSpiritIncrementEnabled = false;
                        StatusMessage = "Spirit increment disabled - normal spirit behavior restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable spirit increment";
                        MessageBox.Show(
                            "Failed to disable spirit increment.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling spirit increment: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling spirit increment:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleStoreItemMultiplier(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleStoreItemMultiplier(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsStoreItemMultiplierEnabled)
                {
                    success = _memoryService.InjectStoreItemMultiplier();

                    if (success)
                    {
                        IsStoreItemMultiplierEnabled = true;
                        StatusMessage = "Store item multiplier enabled - items will be multiplied by 2457 when purchased!";
                    }
                    else
                    {
                        StatusMessage = "Failed to enable store item multiplier";
                        MessageBox.Show(
                            "Failed to enable store item multiplier.\n\n" +
                            "Make sure the game is running and you are attached to the process.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.RemoveStoreItemMultiplier();

                    if (success)
                    {
                        IsStoreItemMultiplierEnabled = false;
                        StatusMessage = "Store item multiplier disabled - normal purchase behavior restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable store item multiplier";
                        MessageBox.Show(
                            "Failed to disable store item multiplier.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling store item multiplier: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling store item multiplier:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanAddSpirits(object? parameter)
        {
            return IsAttached;
        }

        private void AddSpiritsValue(object? parameter)
        {
            if (!IsAttached || parameter == null)
                return;

            var spiritValue = parameter as MemoryValue;
            if (spiritValue == null)
                return;

            try
            {
                int newValue = spiritValue.CurrentValue + 2;
                bool success = _memoryService.WriteValue(
                    spiritValue.BaseAddress,
                    spiritValue.Offsets,
                    newValue
                );

                if (success)
                {
                    spiritValue.CurrentValue = newValue;
                    spiritValue.NewValue = newValue;
                    StatusMessage = $"Added 2 to {spiritValue.Name}. New value: {newValue}";
                }
                else
                {
                    StatusMessage = $"Failed to update {spiritValue.Name}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding to spirit value: {ex.Message}";
            }
        }

        private bool CanAddBeans(object? parameter)
        {
            return IsAttached;
        }

        private void AddBeansValue(object? parameter)
        {
            if (!IsAttached || parameter == null)
                return;

            var beanValue = parameter as MemoryValue;
            if (beanValue == null)
                return;

            try
            {
                int newValue = beanValue.CurrentValue + 1000;
                bool success = _memoryService.WriteValue(
                    beanValue.BaseAddress,
                    beanValue.Offsets,
                    newValue
                );

                if (success)
                {
                    beanValue.CurrentValue = newValue;
                    beanValue.NewValue = newValue;
                    StatusMessage = $"Added 1000 to {beanValue.Name}. New value: {newValue}";
                }
                else
                {
                    StatusMessage = $"Failed to update {beanValue.Name}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding to bean value: {ex.Message}";
            }
        }

        private void OpenItemListWindow(object? parameter)
        {
            SelectedTool = "itemslist";
        }

        private bool CanTogglePassiveValueEditing(object? parameter)
        {
            return IsAttached;
        }

        private void TogglePassiveValueEditing(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsPassiveValueEditingEnabled)
                {
                    success = _memoryService.InjectPassiveValueEditing();

                    if (success)
                    {
                        IsPassiveValueEditingEnabled = true;
                        StatusMessage = "Passive value editing enabled! Now hover over a passive in the Abilearn Board.";

                        // Start a timer to poll for passive value updates
                        var passiveTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(100)
                        };
                        passiveTimer.Tick += (s, e) =>
                        {
                            if (!IsPassiveValueEditingEnabled || !IsAttached)
                            {
                                passiveTimer.Stop();
                                return;
                            }

                            var (hasValue, valueType, currentValue) = _memoryService.ReadPassiveValue();
                            if (hasValue)
                            {
                                HasPassiveValue = true;
                                PassiveValueType = valueType == 2 ? "Float" : "Integer (DWord)";
                                PassiveCurrentValue = currentValue.ToString();
                                if (string.IsNullOrEmpty(PassiveNewValue))
                                {
                                    PassiveNewValue = currentValue.ToString();
                                }
                            }
                            else
                            {
                                if (HasPassiveValue)
                                {
                                    // Only reset if we previously had a value
                                    HasPassiveValue = false;
                                    PassiveValueType = "Unknown";
                                    PassiveCurrentValue = "N/A";
                                }
                            }
                        };
                        passiveTimer.Start();
                    }
                    else
                    {
                        StatusMessage = "Failed to enable passive value editing";
                        MessageBox.Show(
                            "Failed to enable passive value editing.\n\n" +
                            "Make sure the game is running and you are attached to the process.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.RemovePassiveValueEditing();

                    if (success)
                    {
                        IsPassiveValueEditingEnabled = false;
                        HasPassiveValue = false;
                        PassiveValueType = "Unknown";
                        PassiveCurrentValue = "N/A";
                        PassiveNewValue = "";
                        StatusMessage = "Passive value editing disabled - normal behavior restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable passive value editing";
                        MessageBox.Show(
                            "Failed to disable passive value editing.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling passive value editing: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling passive value editing:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanApplyPassiveValue(object? parameter)
        {
            return IsAttached && HasPassiveValue && !string.IsNullOrEmpty(PassiveNewValue);
        }

        private void ApplyPassiveValue(object? parameter)
        {
            if (!IsAttached || !HasPassiveValue)
                return;

            try
            {
                bool success = _memoryService.WritePassiveValue(PassiveNewValue);

                if (success)
                {
                    StatusMessage = $"Successfully applied passive value: {PassiveNewValue}";
                    PassiveCurrentValue = PassiveNewValue;
                }
                else
                {
                    StatusMessage = "Failed to apply passive value";
                    MessageBox.Show(
                        "Failed to apply passive value.\n\n" +
                        "Make sure the game is running and you are hovering over a passive in the Abilearn Board.",
                        "Apply Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying passive value: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while applying passive value:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
