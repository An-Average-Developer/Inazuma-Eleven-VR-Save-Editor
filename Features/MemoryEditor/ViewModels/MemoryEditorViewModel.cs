using System;
using System.Collections.Generic;
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
        private readonly UnlimitedSpiritsService _unlimitedSpiritsService;
        private readonly PlayerLevelService _playerLevelService;
        private readonly CustomPassivesService _customPassivesService;
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
        private bool _isEliteSpiritIncrementEnabled = false;
        private bool _isCustomBaseSpiritIncrementEnabled = false;
        private bool _isCustomHeroSpiritIncrementEnabled = false;
        private bool _isStoreItemMultiplierEnabled = false;
        private bool _isPassiveValueEditingEnabled = false;
        private bool _isUnlimitedSpiritsEnabled = false;
        private bool _isPlayerLevelEnabled = false;
        private string _playerLevelInput = "99";
        private bool _showPlayerLevelInput = false;

        // Individual maintenance flags for each card
        private bool _isStarsUnderMaintenance = true;
        private bool _isFlowersUnderMaintenance = true;
        private bool _isSpiritsUnderMaintenance = true;
        private bool _isBeansUnderMaintenance = true;
        private bool _isVictoryItemsUnderMaintenance = true;
        private bool _isPassiveValuesUnderMaintenance = true;

        // Individual maintenance flags for toggle options
        private bool _isFreezeItemsUnderMaintenance = false;
        private bool _isIncrementItemsUnderMaintenance = false;
        private bool _isStoreMultiplierUnderMaintenance = false;
        private bool _isFreezeSpiritsUnderMaintenance = false;
        private bool _isIncrementSpiritsUnderMaintenance = false;
        private bool _isIncrementEliteSpiritsUnderMaintenance = false;
        private bool _isCustomBaseSpiritIncrementUnderMaintenance = false;
        private bool _isCustomHeroSpiritIncrementUnderMaintenance = false;
        private bool _isUnlimitedHeroesUnderMaintenance = false;
        private bool _isUnlimitedHeroesEnabled = true;
        private bool _isFreeBuySpiritMarketEnabled = false;
        private bool _isFreeBuySpiritMarketUnderMaintenance = true;
        private bool _isPlayerSpiritsUnderMaintenance = true;
        private bool _isFreeBuyShopEnabled = false;
        private bool _isFreeBuyShopUnderMaintenance = true;
        private bool _isPlayerLevelUnderMaintenance = false;

        // Hidden flags for cards
        private bool _isFreezeItemsHidden = false;
        private bool _isIncrementItemsHidden = false;
        private bool _isStoreMultiplierHidden = false;
        private bool _isStarsHidden = true;
        private bool _isFlowersHidden = true;
        private bool _isBeansHidden = true;
        private bool _isVictoryItemsHidden = true;
        private bool _isFreezeSpiritsHidden = false;
        private bool _isIncrementSpiritsHidden = false;
        private bool _isIncrementEliteSpiritsHidden = false;
        private bool _isSpiritsHidden = false;
        private bool _isPlayerSpiritsHidden = false;
        private bool _isFreeBuySpiritMarketHidden = false;
        private bool _isFreeBuyShopHidden = false;
        private bool _isUnlimitedHeroesHidden = false;
        private bool _isPlayerLevelHidden = false;
        private bool _isPassiveValuesHidden = false;
        private bool _isCustomPassivesHidden = false;

        // Passive value tracking
        private string _passiveValueType = "Unknown";
        private string _passiveCurrentValue = "N/A";
        private string _passiveNewValue = "";
        private bool _hasPassiveValue = false;
        private PassiveInfo? _selectedPassive = null;

        // Custom Passives
        private string _customPassive1 = "";
        private string _customPassive2 = "";
        private string _customPassive3 = "";
        private string _customPassive4 = "";
        private string _customPassive5 = "";
        private bool _isCustomPassivesUnderMaintenance = true;

        // Tutorials
        private TutorialsViewModel? _tutorialsViewModel;
        private string _currentTutorialFeature = "";

        private const long STAR_FREEZE_ADDRESS = 0xCA1F76;

        private const long FLOWER_INCREMENT_ADDRESS = 0xCA1F69;

        private const long SPIRIT_FREEZE_ADDRESS = 0xCD19DB;
        private const long ELITE_SPIRIT_FREEZE_ADDRESS = 0xCD1917;

        private static readonly byte[] FREEZE_BYTES = new byte[] { 0x90, 0x90, 0x90, 0x90 };
        private static readonly byte[] ORIGINAL_BYTES = new byte[] { 0x44, 0x89, 0x40, 0x10, };

        private static readonly byte[] FLOWER_ORIGINAL_BYTES = new byte[] { 0x2B, 0xCD };
        private static readonly byte[] FLOWER_INCREMENT_BYTES = new byte[] { 0x03, 0xCD };

        private static readonly byte[] SPIRIT_ORIGINAL_BYTES = new byte[] { 0x66, 0x89, 0x70, 0x10 };
        private static readonly byte[] SPIRIT_FREEZE_BYTES = new byte[] { 0x90, 0x90, 0x90, 0x90 };

        private static readonly byte[] ELITE_SPIRIT_ORIGINAL_BYTES = new byte[] { 0x66, 0x41, 0x89, 0x74, 0x42, 0x14 };
        private static readonly byte[] ELITE_SPIRIT_FREEZE_BYTES = new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };

        public MemoryEditorViewModel()
        {
            _memoryService = new MemoryEditorService();
            _unlimitedSpiritsService = new UnlimitedSpiritsService();
            _playerLevelService = new PlayerLevelService();
            _customPassivesService = new CustomPassivesService();

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
                SelectedTool = "spiritcards";
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
            RestartGameCommand = new RelayCommand(RestartGame, CanRestartGame);
            ToggleSpiritsFreezeCommand = new RelayCommand(ToggleSpiritsFreeze, CanToggleSpiritsFreeze);
            ToggleSpiritIncrementCommand = new RelayCommand(ToggleSpiritIncrement, CanToggleSpiritIncrement);
            ToggleEliteSpiritIncrementCommand = new RelayCommand(ToggleEliteSpiritIncrement, CanToggleEliteSpiritIncrement);
            ToggleCustomBaseSpiritIncrementCommand = new RelayCommand(ToggleCustomBaseSpiritIncrement, CanToggleCustomBaseSpiritIncrement);
            ToggleCustomHeroSpiritIncrementCommand = new RelayCommand(ToggleCustomHeroSpiritIncrement, CanToggleCustomHeroSpiritIncrement);
            ToggleStoreItemMultiplierCommand = new RelayCommand(ToggleStoreItemMultiplier, CanToggleStoreItemMultiplier);
            ToggleUnlimitedSpiritsCommand = new RelayCommand(ToggleUnlimitedSpirits, CanToggleUnlimitedSpirits);
            ToggleUnlimitedHeroesCommand = new RelayCommand(ToggleUnlimitedHeroes, CanToggleUnlimitedHeroes);
            ToggleFreeBuySpiritMarketCommand = new RelayCommand(ToggleFreeBuySpiritMarket, CanToggleFreeBuySpiritMarket);
            ToggleFreeBuyShopCommand = new RelayCommand(ToggleFreeBuyShop, CanToggleFreeBuyShop);
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
            SelectSpiritCardsEditorCommand = new RelayCommand(() =>
            {
                SelectedTool = "spiritcards";
                SelectedValue = null;
            });
            AddSpiritCardCommand = new RelayCommand(AddSpiritCard, CanAddSpiritCard);
            ToggleSpiritCardCommand = new RelayCommand(ToggleSpiritCard, CanToggleSpiritCard);
            ToggleAllSpiritCardsCommand = new RelayCommand(ToggleAllSpiritCards, CanToggleAllSpiritCards);
            SelectPlayerSpiritsEditorCommand = new RelayCommand(() =>
            {
                SelectedTool = "playerspirits";
                SelectedValue = null;
            });
            TogglePlayerSpiritCommand = new RelayCommand(TogglePlayerSpirit, CanTogglePlayerSpirit);
            ToggleAllPlayerSpiritsCommand = new RelayCommand(ToggleAllPlayerSpirits, CanToggleAllPlayerSpirits);
            TogglePlayerLevelCommand = new RelayCommand(TogglePlayerLevel, CanTogglePlayerLevel);
            ApplyPlayerLevelCommand = new RelayCommand(ApplyPlayerLevel, CanApplyPlayerLevel);
            SelectCustomPassivesEditorCommand = new RelayCommand(() =>
            {
                SelectedTool = "custompassives";
                SelectedValue = null;
            });
            ApplyCustomPassivesCommand = new RelayCommand(ApplyCustomPassives, CanApplyCustomPassives);
            ClearCustomPassivesCommand = new RelayCommand(ClearCustomPassives);
            OpenTutorialsCommand = new RelayCommand(OpenTutorials);

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
                    BaseAddress = 0x01C726D0,
                    Offsets = new int[] { 0x900, 0x78, 0xF0, 0x4BC },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Intelligence",
                    Description = "Intelligence Bean",
                    BaseAddress = 0x01C726D0,
                    Offsets = new int[] { 0x900, 0x70, 0x5F0, 0xFE0 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Kicking Power",
                    Description = "Kicking Power Bean",
                    BaseAddress = 0x01C726D0,
                    Offsets = new int[] { 0x900, 0x78, 0xC0, 0x494 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Mind's Eye",
                    Description = "Mind's Eye Bean",
                    BaseAddress = 0x01C726D0,
                    Offsets = new int[] { 0x900, 0x70, 0xC70, 0x5E8 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Strength",
                    Description = "Strength Bean",
                    BaseAddress = 0x01C726D0,
                    Offsets = new int[] { 0x900, 0x80, 0xC0, 0x48 },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Technique",
                    Description = "Technique Bean",
                    BaseAddress = 0x01C726D0,
                    Offsets = new int[] { 0x900, 0x78, 0x20, 0x5BC },
                    CurrentValue = 0,
                    NewValue = 0
                },
                new MemoryValue
                {
                    Name = "Unshakable",
                    Description = "Unshakable Bean",
                    BaseAddress = 0x01C726D0,
                    Offsets = new int[] { 0x900, 0x78, 0x20, 0x62C },
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

            // Initialize Spirit Cards Collection with all spirit IDs from the Cheat Engine script
            SpiritCardsCollection = new ObservableCollection<SpiritCardInfo>
            {
                new SpiritCardInfo { Name = "Alpha", Variant = "Pink", SpiritId = 0x1FB2701F },
                new SpiritCardInfo { Name = "Alpha", Variant = "White-Black", SpiritId = 0x367AC4ED },
                new SpiritCardInfo { Name = "Arion Sherwind", Variant = "Pink", SpiritId = 0x24DFD4DA },
                new SpiritCardInfo { Name = "Arion Sherwind", Variant = "White-Black", SpiritId = 0x0D176028 },
                new SpiritCardInfo { Name = "Archer Hawkins", Variant = "Pink", SpiritId = 0x75FFCCFC },
                new SpiritCardInfo { Name = "Archer Hawkins", Variant = "Red", SpiritId = 0x5C37780E },
                new SpiritCardInfo { Name = "Arculus Orbes", Variant = "Pink", SpiritId = 0x2CCA249C },
                new SpiritCardInfo { Name = "Arculus Orbes", Variant = "White-Black", SpiritId = 0x0502906E },
                new SpiritCardInfo { Name = "Austin Hobbes", Variant = "Pink", SpiritId = 0x5ED29F3F },
                new SpiritCardInfo { Name = "Austin Hobbes", Variant = "Red", SpiritId = 0x771A2BCD },
                new SpiritCardInfo { Name = "Axel Blaze", Variant = "Pink", SpiritId = 0xBFFC1B42 },
                new SpiritCardInfo { Name = "Axel Blaze", Variant = "White-Black", SpiritId = 0x9634AFB0 },
                new SpiritCardInfo { Name = "Bailong", Variant = "Pink", SpiritId = 0xD1B08725 },
                new SpiritCardInfo { Name = "Bailong", Variant = "Red", SpiritId = 0xF87833D7 },
                new SpiritCardInfo { Name = "Bash Lancer", Variant = "Pink", SpiritId = 0xA9FB7299 },
                new SpiritCardInfo { Name = "Bash Lancer", Variant = "Red", SpiritId = 0x8033C66B },
                new SpiritCardInfo { Name = "Bellatrix", Variant = "Red", SpiritId = 0x62A929F4 },
                new SpiritCardInfo { Name = "Bellatrix", Variant = "White-Black", SpiritId = 0x4B619D06 },
                new SpiritCardInfo { Name = "Beta", Variant = "Pink", SpiritId = 0x35FE1083 },
                new SpiritCardInfo { Name = "Beta", Variant = "Red", SpiritId = 0x1C36A471 },
                new SpiritCardInfo { Name = "Buddy Fury", Variant = "Red", SpiritId = 0x6A6392AD },
                new SpiritCardInfo { Name = "Buddy Fury", Variant = "Pink", SpiritId = 0x43AB265F },
                new SpiritCardInfo { Name = "Byron Love", Variant = "Pink", SpiritId = 0x209B996D },
                new SpiritCardInfo { Name = "Byron Love", Variant = "Red", SpiritId = 0x09532D9F },
                new SpiritCardInfo { Name = "Briar Bloomhurst", Variant = "Pink", SpiritId = 0x540A8E2B },
                new SpiritCardInfo { Name = "Briar Bloomhurst", Variant = "White-Black", SpiritId = 0x7DC23AD9 },
                new SpiritCardInfo { Name = "Caleb Stonewall", Variant = "Pink", SpiritId = 0x1376EEC9 },
                new SpiritCardInfo { Name = "Caleb Stonewall", Variant = "Red", SpiritId = 0x3ABE5A3B },
                new SpiritCardInfo { Name = "Circulus Corona", Variant = "Red", SpiritId = 0x06864400 },
                new SpiritCardInfo { Name = "Circulus Corona", Variant = "White-Black", SpiritId = 0x2F4EF0F2 },
                new SpiritCardInfo { Name = "Darren LaChance", Variant = "Pink", SpiritId = 0xD963436D },
                new SpiritCardInfo { Name = "Darren LaChance", Variant = "Red", SpiritId = 0xF0ABF79F },
                new SpiritCardInfo { Name = "Dvalin", Variant = "Pink", SpiritId = 0x5735BAC2 },
                new SpiritCardInfo { Name = "Dvalin", Variant = "Red", SpiritId = 0x7EFD0E30 },
                new SpiritCardInfo { Name = "Erik Eagle", Variant = "Pink", SpiritId = 0x9594520B },
                new SpiritCardInfo { Name = "Erik Eagle", Variant = "White-Black", SpiritId = 0xBC5CE6F9 },
                new SpiritCardInfo { Name = "Falco Flashman", Variant = "Red", SpiritId = 0xCBBB7F46 },
                new SpiritCardInfo { Name = "Falco Flashman", Variant = "Pink", SpiritId = 0xE273CBB4 },
                new SpiritCardInfo { Name = "Fei Rune", Variant = "Pink", SpiritId = 0x28685D70 },
                new SpiritCardInfo { Name = "Fei Rune", Variant = "White-Black", SpiritId = 0x01A0E982 },
                new SpiritCardInfo { Name = "Gabriel Garcia", Variant = "Pink", SpiritId = 0xC72B0D11 },
                new SpiritCardInfo { Name = "Gabriel Garcia", Variant = "White-Black", SpiritId = 0xEEE3B9E3 },
                new SpiritCardInfo { Name = "Gamma", Variant = "Black", SpiritId = 0x3EA257BE },
                new SpiritCardInfo { Name = "Gamma", Variant = "Red", SpiritId = 0x176AE34C },
                new SpiritCardInfo { Name = "Gandares Baran", Variant = "Red", SpiritId = 0xBCDFC454 },
                new SpiritCardInfo { Name = "Gandares Baran", Variant = "White-Black", SpiritId = 0x951770A6 },
                new SpiritCardInfo { Name = "Gazelle", Variant = "Pink", SpiritId = 0x030F668B },
                new SpiritCardInfo { Name = "Gazelle", Variant = "White-Black", SpiritId = 0x2AC7D279 },
                new SpiritCardInfo { Name = "Goldie Lemmon", Variant = "Red", SpiritId = 0x7CD71DC7 },
                new SpiritCardInfo { Name = "Goldie Lemmon", Variant = "White-Black", SpiritId = 0x551FA935 },
                new SpiritCardInfo { Name = "Harper Evans", Variant = "Pink", SpiritId = 0x3887A738 },
                new SpiritCardInfo { Name = "Harper Evans", Variant = "Red", SpiritId = 0x114F13CA },
                new SpiritCardInfo { Name = "Hector Helio", Variant = "Pink", SpiritId = 0xF75F83DA },
                new SpiritCardInfo { Name = "Hector Helio", Variant = "White-Black", SpiritId = 0xDE973728 },
                new SpiritCardInfo { Name = "Hurley Kane", Variant = "Red", SpiritId = 0x37D0DE29 },
                new SpiritCardInfo { Name = "Hurley Kane", Variant = "White-Black", SpiritId = 0x1E186ADB },
                new SpiritCardInfo { Name = "Jack Wallside", Variant = "Pink", SpiritId = 0x951322B6 },
                new SpiritCardInfo { Name = "Jack Wallside", Variant = "White-Black", SpiritId = 0xBCDB9644 },
                new SpiritCardInfo { Name = "Janus", Variant = "Red", SpiritId = 0x4C6835DA },
                new SpiritCardInfo { Name = "Janus", Variant = "White-Black", SpiritId = 0x65A08128 },
                new SpiritCardInfo { Name = "Jean-Pierre Lapin", Variant = "Red", SpiritId = 0x9171AA97 },
                new SpiritCardInfo { Name = "Jean-Pierre Lapin", Variant = "White-Black", SpiritId = 0xB8B91E65 },
                new SpiritCardInfo { Name = "Joseph King", Variant = "Red", SpiritId = 0xA4A1945A },
                new SpiritCardInfo { Name = "Joseph King", Variant = "Pink", SpiritId = 0x8D6920A8 },
                new SpiritCardInfo { Name = "Jude Sharp", Variant = "Red", SpiritId = 0xBC78CF2C },
                new SpiritCardInfo { Name = "Jude Sharp", Variant = "White-Black", SpiritId = 0x95B07BDE },
                new SpiritCardInfo { Name = "Mark Evans", Variant = "Pink", SpiritId = 0xA7254034 },
                new SpiritCardInfo { Name = "Mark Evans", Variant = "White-Black", SpiritId = 0x8EEDF4C6 },
                new SpiritCardInfo { Name = "Nathan Swift", Variant = "Red", SpiritId = 0x8C0813F7 },
                new SpiritCardInfo { Name = "Nathan Swift", Variant = "White-Black", SpiritId = 0xA5C0A705 },
                new SpiritCardInfo { Name = "Ozrock Boldar", Variant = "Red", SpiritId = 0xB02972DD },
                new SpiritCardInfo { Name = "Ozrock Boldar", Variant = "Pink", SpiritId = 0x99E1C62F },
                new SpiritCardInfo { Name = "Paolo Bianchi", Variant = "Red", SpiritId = 0xFA3ADF9E },
                new SpiritCardInfo { Name = "Paolo Bianchi", Variant = "White-Black", SpiritId = 0xD3F26B6C },
                new SpiritCardInfo { Name = "Plink Powai", Variant = "Red", SpiritId = 0xD4B76E4B },
                new SpiritCardInfo { Name = "Plink Powai", Variant = "White-Black", SpiritId = 0xFD7FDAB9 },
                new SpiritCardInfo { Name = "Quentin Cinquedea", Variant = "Pink", SpiritId = 0xCDE4A0E1 },
                new SpiritCardInfo { Name = "Quentin Cinquedea", Variant = "Red", SpiritId = 0xE42C1413 },
                new SpiritCardInfo { Name = "Riccardo Di Rigo", Variant = "Red", SpiritId = 0x3DC4E59B },
                new SpiritCardInfo { Name = "Riccardo Di Rigo", Variant = "White-Black", SpiritId = 0x140C5169 },
                new SpiritCardInfo { Name = "Rondula Flehl", Variant = "Red", SpiritId = 0x9A3492BB },
                new SpiritCardInfo { Name = "Rondula Flehl", Variant = "Pink", SpiritId = 0xB3FC2649 },
                new SpiritCardInfo { Name = "Ruger Baran", Variant = "Red", SpiritId = 0xA5C4F515 },
                new SpiritCardInfo { Name = "Ruger Baran", Variant = "Pink", SpiritId = 0x8C0C41E7 },
                new SpiritCardInfo { Name = "Samguk Han", Variant = "Pink", SpiritId = 0xDCD5DB61 },
                new SpiritCardInfo { Name = "Samguk Han", Variant = "White-Black", SpiritId = 0xF51D6F93 },
                new SpiritCardInfo { Name = "Simeon Ayp", Variant = "Pink", SpiritId = 0x098AA343 },
                new SpiritCardInfo { Name = "Simeon Ayp", Variant = "Red", SpiritId = 0x204217B1 },
                new SpiritCardInfo { Name = "Shawn Froste", Variant = "Red", SpiritId = 0x2D3CA337 },
                new SpiritCardInfo { Name = "Shawn Froste", Variant = "White-Black", SpiritId = 0x04F417C5 },
                new SpiritCardInfo { Name = "Terry Archibald", Variant = "Pink", SpiritId = 0x2522046A },
                new SpiritCardInfo { Name = "Terry Archibald", Variant = "White-Black", SpiritId = 0x0CEAB098 },
                new SpiritCardInfo { Name = "Tezcat", Variant = "Pink", SpiritId = 0xD234534B },
                new SpiritCardInfo { Name = "Tezcat", Variant = "White-Black", SpiritId = 0xFBFCE7B9 },
                new SpiritCardInfo { Name = "Torch", Variant = "Red", SpiritId = 0xFAC70307 },
                new SpiritCardInfo { Name = "Torch", Variant = "Pink", SpiritId = 0xD30FB7F5 },
                new SpiritCardInfo { Name = "Victor Blade", Variant = "Red", SpiritId = 0xC40C8017 },
                new SpiritCardInfo { Name = "Victor Blade", Variant = "White-Black", SpiritId = 0xEDC434E5 },
                new SpiritCardInfo { Name = "Xene", Variant = "Red", SpiritId = 0x527AAC47 },
                new SpiritCardInfo { Name = "Xene", Variant = "White-Black", SpiritId = 0x7BB218B5 },
                new SpiritCardInfo { Name = "Zanark Avalonic", Variant = "Pink", SpiritId = 0x8EC6A388 },
                new SpiritCardInfo { Name = "Zanark Avalonic", Variant = "Red", SpiritId = 0xA70E177A }
            };

            // Initialize Player Spirits Collection with all player spirit IDs from the Cheat Engine script
            PlayerSpiritsCollection = new ObservableCollection<PlayerSpiritInfo>
            {
                new PlayerSpiritInfo { Name = "Arion Sherwind", MixDescription = "Arion x King Arthur", PlayerId = 0x489A980B },
                new PlayerSpiritInfo { Name = "Arion Sherwind", MixDescription = "Arion x Victor", PlayerId = 0xC4DB5F07 },
                new PlayerSpiritInfo { Name = "Arion Sherwind", MixDescription = "Arion x Mark", PlayerId = 0x8B9AC9C0 },
                new PlayerSpiritInfo { Name = "Riccardo Di Rigo", MixDescription = "Riccardo x Nobunaga", PlayerId = 0x061964FB },
                new PlayerSpiritInfo { Name = "Riccardo Di Rigo", MixDescription = "Riccardo x Gabi", PlayerId = 0xEFF60CC4 },
                new PlayerSpiritInfo { Name = "Victor Blade", MixDescription = "Victor x Soji", PlayerId = 0x7AACFA89 },
                new PlayerSpiritInfo { Name = "Victor Blade", MixDescription = "Victor x Bailong", PlayerId = 0xB9ACAB42 },
                new PlayerSpiritInfo { Name = "Vladimir Blade", MixDescription = "Vladimir x Victor", PlayerId = 0x5043C37D },
                new PlayerSpiritInfo { Name = "Gabriel Garcia", MixDescription = "Gabi x Juana de Arco", PlayerId = 0x1F0255BA },
                new PlayerSpiritInfo { Name = "Gabriel Garcia", MixDescription = "Gabi x Aitor", PlayerId = 0xF6ED3D85 },
                new PlayerSpiritInfo { Name = "Goldie Lemmon", MixDescription = "Goldie x Reina de los Dragones", PlayerId = 0x5181A94A },
                new PlayerSpiritInfo { Name = "Jean-Pierre Lapin", MixDescription = "JP x Liu Bei", PlayerId = 0x2D343738 },
                new PlayerSpiritInfo { Name = "Ryoma Nishiki", MixDescription = "Roma x Ryoma", PlayerId = 0x63B7CBC8 },
                new PlayerSpiritInfo { Name = "Fei Rune", MixDescription = "Fei x T-REX", PlayerId = 0x7B6E90BE },
                new PlayerSpiritInfo { Name = "Fei Rune", MixDescription = "Fei x Big", PlayerId = 0xB3B71AB6 },
                new PlayerSpiritInfo { Name = "Sor", MixDescription = "Sor x Padre de Sor", PlayerId = 0xAAAC2BF7 },
                new PlayerSpiritInfo { Name = "Zanark Avalonic", MixDescription = "Zanark x Cao Cao", PlayerId = 0x1EC03F8D },
                new PlayerSpiritInfo { Name = "Zanark Avalonic", MixDescription = "Zanark x Zeta", PlayerId = 0x35ED6C4E },
                new PlayerSpiritInfo { Name = "Sol Daystar", MixDescription = "Sol x Zhuge Liang", PlayerId = 0x342F0679 },
                new PlayerSpiritInfo { Name = "Bailong", MixDescription = "Bailong x Zhuge Liang", PlayerId = 0x07DB0ECC },
                new PlayerSpiritInfo { Name = "Bailong", MixDescription = "Bailong x Tezcat", PlayerId = 0xA0B79A03 },
                new PlayerSpiritInfo { Name = "Axel Blaze", MixDescription = "Axel x Shawn", PlayerId = 0x9281F881 },
                new PlayerSpiritInfo { Name = "Jude Sharp", MixDescription = "Jude x Caleb", PlayerId = 0x1519E44E },
                new PlayerSpiritInfo { Name = "Mike", MixDescription = "Miximaxed x Zanark", PlayerId = 0x79282EE7 },
                new PlayerSpiritInfo { Name = "Gamma", MixDescription = "Miximaxed x Zanark", PlayerId = 0x60331FA6 },
                new PlayerSpiritInfo { Name = "Juliet", MixDescription = "Miximaxed x Zanark", PlayerId = 0xB033CED8 },
                new PlayerSpiritInfo { Name = "November", MixDescription = "Miximaxed x Zanark", PlayerId = 0x2EB0E356 },
                new PlayerSpiritInfo { Name = "Quebec", MixDescription = "Miximaxed x Zanark", PlayerId = 0x1C8681D4 },
                new PlayerSpiritInfo { Name = "Romeo", MixDescription = "Miximaxed x Zanark", PlayerId = 0x78EA44D0 },
                new PlayerSpiritInfo { Name = "Desmodus Drakul", MixDescription = "Mix 'n' Match", PlayerId = 0x02941849 },
                new PlayerSpiritInfo { Name = "Wolfram Vulpeen", MixDescription = "Mix 'n' Match", PlayerId = 0x4DD58E8E },
                new PlayerSpiritInfo { Name = "Simeon Ayp", MixDescription = "Mix 'n' Match", PlayerId = 0x54CEBFCF }
            };

            // Initialize AllPassiveIds collection with both normal and hero passives
            AllPassiveIds = new ObservableCollection<PassiveIdInfo>
            {
                new PassiveIdInfo { Id = "3A2BCAF4", Name = "Passive 01 (Type 1)" },
                new PassiveIdInfo { Id = "3E305FFD", Name = "Passive 01 (Type 2)" },
                new PassiveIdInfo { Id = "A3229B4E", Name = "Passive 02 (Type 1)" },
                new PassiveIdInfo { Id = "2C85F013", Name = "Passive 02 (Type 2)" },
                new PassiveIdInfo { Id = "D425ABD8", Name = "Passive 03 (Type 1)" },
                new PassiveIdInfo { Id = "94399776", Name = "Passive 03 (Type 2)" },
                new PassiveIdInfo { Id = "09EEAFCF", Name = "Passive 04 (Type 1)" },
                new PassiveIdInfo { Id = "4A413E7B", Name = "Passive 04 (Type 2)" },
                new PassiveIdInfo { Id = "3D460EED", Name = "Passive 05 (Type 1)" },
                new PassiveIdInfo { Id = "B152C8AA", Name = "Passive 05 (Type 2)" },
                new PassiveIdInfo { Id = "A44F5F57", Name = "Passive 06 (Type 1)" },
                new PassiveIdInfo { Id = "A3E76744", Name = "Passive 06 (Type 2)" },
                new PassiveIdInfo { Id = "D3486FC1", Name = "Passive 07 (Type 1)" },
                new PassiveIdInfo { Id = "1B5B0021", Name = "Passive 07 (Type 2)" },
                new PassiveIdInfo { Id = "43F77250", Name = "Passive 08 (Type 1)" },
                new PassiveIdInfo { Id = "43381077", Name = "Passive 08 (Type 2)" },
                new PassiveIdInfo { Id = "34F042C6", Name = "Passive 09 (Type 1)" },
                new PassiveIdInfo { Id = "FB847712", Name = "Passive 09 (Type 2)" },
                new PassiveIdInfo { Id = "5437CB23", Name = "Passive 10 (Type 1)" },
                new PassiveIdInfo { Id = "BBEC1128", Name = "Passive 10 (Type 2)" },
                new PassiveIdInfo { Id = "2330FBB5", Name = "Passive 11 (Type 1)" },
                new PassiveIdInfo { Id = "0350764D", Name = "Passive 11 (Type 2)" },
                new PassiveIdInfo { Id = "BA39AA0F", Name = "Passive 14 (Type 1)" },
                new PassiveIdInfo { Id = "11E5D9A3", Name = "Passive 14 (Type 2)" },
                new PassiveIdInfo { Id = "A959BEC6", Name = "Passive 15 (Type 1)" },
                new PassiveIdInfo { Id = "CD3E9A99", Name = "Passive 15 (Type 2)" },
                new PassiveIdInfo { Id = "535A0F3A", Name = "Passive 16 (Type 1)" },
                new PassiveIdInfo { Id = "348E867F", Name = "Passive 16 (Type 2)" },
                new PassiveIdInfo { Id = "245D3FAC", Name = "Passive 17 (Type 1)" },
                new PassiveIdInfo { Id = "8C32E11A", Name = "Passive 17 (Type 2)" },
                new PassiveIdInfo { Id = "BD546E16", Name = "Passive 18 (Type 1)" },
                new PassiveIdInfo { Id = "9E874EF4", Name = "Passive 18 (Type 2)" },
                new PassiveIdInfo { Id = "CA535E80", Name = "Passive 19 (Type 1)" },
                new PassiveIdInfo { Id = "263B2991", Name = "Passive 19 (Type 2)" },
                new PassiveIdInfo { Id = "5AEC4311", Name = "Passive 20 (Type 1)" },
                new PassiveIdInfo { Id = "7E5839C7", Name = "Passive 20 (Type 2)" },
                new PassiveIdInfo { Id = "2DEB7387", Name = "Passive 21 (Type 1)" },
                new PassiveIdInfo { Id = "7F1A98E0", Name = "Passive 21 (Type 2)" },
                new PassiveIdInfo { Id = "081DA876", Name = "Passive 22 (Type 1)" },
                new PassiveIdInfo { Id = "9114F9CC", Name = "Passive 22 (Type 2)" },
                new PassiveIdInfo { Id = "E613C95A", Name = "Passive 23 (Type 1)" },
                new PassiveIdInfo { Id = "78775CF9", Name = "Passive 23 (Type 2)" },
                new PassiveIdInfo { Id = "96793DD5", Name = "Passive 24 (Type 1)" },
                new PassiveIdInfo { Id = "0F706C6F", Name = "Passive 24 (Type 2)" },
                new PassiveIdInfo { Id = "E17E0D43", Name = "Passive 25 (Type 1)" },
                new PassiveIdInfo { Id = "71C110D2", Name = "Passive 25 (Type 2)" },
                new PassiveIdInfo { Id = "06C62044", Name = "Passive 26 (Type 1)" },
                new PassiveIdInfo { Id = "6601A9A1", Name = "Passive 26 (Type 2)" },
                new PassiveIdInfo { Id = "11069937", Name = "Passive 27 (Type 1)" },
                new PassiveIdInfo { Id = "880FC88D", Name = "Passive 27 (Type 2)" },
                new PassiveIdInfo { Id = "FF08F81B", Name = "Passive 28 (Type 1)" },
                new PassiveIdInfo { Id = "616C6DB8", Name = "Passive 28 (Type 2)" },
                new PassiveIdInfo { Id = "166B5D2E", Name = "Passive 29 (Type 1)" },
                new PassiveIdInfo { Id = "8F620C94", Name = "Passive 29 (Type 2)" },
                new PassiveIdInfo { Id = "F8653C02", Name = "Passive 30 (Type 1)" },
                new PassiveIdInfo { Id = "5CFB7AF1", Name = "Passive 30 (Type 2)" },
                new PassiveIdInfo { Id = "68DA2193", Name = "Passive 31 (Type 1)" },
                new PassiveIdInfo { Id = "1FDD1105", Name = "Passive 31 (Type 2)" },
                new PassiveIdInfo { Id = "29403F66", Name = "Passive 32 (Type 1)" },
                new PassiveIdInfo { Id = "5E470FF0", Name = "Passive 32 (Type 2)" },
                new PassiveIdInfo { Id = "C74E5E4A", Name = "Passive 33 (Type 1)" },
                new PassiveIdInfo { Id = "B0496EDC", Name = "Passive 33 (Type 2)" },
                new PassiveIdInfo { Id = "2E2DFB7F", Name = "Passive 34 (Type 1)" },
                new PassiveIdInfo { Id = "FC6E090F", Name = "Passive 34 (Type 2)" },
                new PassiveIdInfo { Id = "592ACBE9", Name = "Passive 35 (Type 1)" },
                new PassiveIdInfo { Id = "C0239A53", Name = "Passive 35 (Type 2)" },
                new PassiveIdInfo { Id = "279BB754", Name = "Passive 36 (Type 1)" },
                new PassiveIdInfo { Id = "B724AAC5", Name = "Passive 36 (Type 2)" },
                new PassiveIdInfo { Id = "509C87C2", Name = "Passive 37 (Type 1)" },
                new PassiveIdInfo { Id = "0E04D1D2", Name = "Passive 37 (Type 2)" },
                new PassiveIdInfo { Id = "305B0E27", Name = "Passive 38" },
                new PassiveIdInfo { Id = "475C3EB1", Name = "Passive 39" },
                new PassiveIdInfo { Id = "A9525F9D", Name = "Passive 40 (Type 1)" },
                new PassiveIdInfo { Id = "DE556F0B", Name = "Passive 40 (Type 2)" },
                new PassiveIdInfo { Id = "3736CA3E", Name = "Passive 41 (Type 1)" },
                new PassiveIdInfo { Id = "4031FAA8", Name = "Passive 41 (Type 2)" },
                new PassiveIdInfo { Id = "AE3F9B84", Name = "Passive 42 (Type 1)" },
                new PassiveIdInfo { Id = "D938AB12", Name = "Passive 42 (Type 2)" },
                new PassiveIdInfo { Id = "4987B683", Name = "Passive 43 (Type 1)" },
                new PassiveIdInfo { Id = "3E808615", Name = "Passive 43 (Type 2)" },
                new PassiveIdInfo { Id = "1B765DE4", Name = "Passive 44 (Type 1)" },
                new PassiveIdInfo { Id = "6C716D72", Name = "Passive 44 (Type 2)" },
                new PassiveIdInfo { Id = "F5783CC8", Name = "Passive 45 (Type 1)" },
                new PassiveIdInfo { Id = "827F0C5E", Name = "Passive 45 (Type 2)" },
                new PassiveIdInfo { Id = "6B1CA96B", Name = "Passive 46 (Type 1)" },
                new PassiveIdInfo { Id = "1C1B99FD", Name = "Passive 46 (Type 2)" },
                new PassiveIdInfo { Id = "8512C847", Name = "Passive 47 (Type 1)" },
                new PassiveIdInfo { Id = "F215F8D1", Name = "Passive 47 (Type 2)" },
                new PassiveIdInfo { Id = "62AAE540", Name = "Passive 48 (Type 1)" },
                new PassiveIdInfo { Id = "15ADD5D6", Name = "Passive 48 (Type 2)" },
                new PassiveIdInfo { Id = "026D6CA5", Name = "Passive 49 (Type 1)" },
                new PassiveIdInfo { Id = "756A5C33", Name = "Passive 49 (Type 2)" },
                new PassiveIdInfo { Id = "9B643D1F", Name = "Passive 50 (Type 1)" },
                new PassiveIdInfo { Id = "EC630D89", Name = "Passive 50 (Type 2)" },
                new PassiveIdInfo { Id = "0500A8BC", Name = "Passive 51 (Type 1)" },
                new PassiveIdInfo { Id = "7207982A", Name = "Passive 51 (Type 2)" },
                new PassiveIdInfo { Id = "EB0EC990", Name = "Passive 52 (Type 1)" },
                new PassiveIdInfo { Id = "9C09F906", Name = "Passive 52 (Type 2)" },
                new PassiveIdInfo { Id = "7BB1D401", Name = "Passive 53 (Type 1)" },
                new PassiveIdInfo { Id = "0CB6E497", Name = "Passive 53 (Type 2)" },
                new PassiveIdInfo { Id = "F2F240FC", Name = "Passive 54 (Type 1)" },
                new PassiveIdInfo { Id = "85F5706A", Name = "Passive 54 (Type 2)" },
                new PassiveIdInfo { Id = "6BFB1146", Name = "Passive 55 (Type 1)" },
                new PassiveIdInfo { Id = "1CFC21D0", Name = "Passive 55 (Type 2)" },
                new PassiveIdInfo { Id = "8298B473", Name = "Passive 56" },
                new PassiveIdInfo { Id = "F59F84E5", Name = "Passive 57 (Type 1)" },
                new PassiveIdInfo { Id = "6C96D55F", Name = "Passive 57 (Type 2)" },
                new PassiveIdInfo { Id = "1B91E5C9", Name = "Passive 58 (Type 1)" },
                new PassiveIdInfo { Id = "8B2EF858", Name = "Passive 58 (Type 2)" },
                new PassiveIdInfo { Id = "FC29C8CE", Name = "Passive 59 (Type 1)" },
                new PassiveIdInfo { Id = "9CEE412B", Name = "Passive 59 (Type 2)" },
                new PassiveIdInfo { Id = "EBE971BD", Name = "Passive 60 (Type 1)" },
                new PassiveIdInfo { Id = "72E02007", Name = "Passive 60 (Type 2)" },
                new PassiveIdInfo { Id = "05E71091", Name = "Passive 61 (Type 1)" },
                new PassiveIdInfo { Id = "9B838532", Name = "Passive 61 (Type 2)" },
                new PassiveIdInfo { Id = "EC84B5A4", Name = "Passive 62 (Type 1)" },
                new PassiveIdInfo { Id = "758DE41E", Name = "Passive 62 (Type 2)" },
                new PassiveIdInfo { Id = "028AD488", Name = "Passive 63 (Type 1)" },
                new PassiveIdInfo { Id = "9235C919", Name = "Passive 63 (Type 2)" },
                new PassiveIdInfo { Id = "E532F98F", Name = "Passive 64 (Type 1)" },
                new PassiveIdInfo { Id = "4CEE9055", Name = "Passive 64 (Type 2)" },
                new PassiveIdInfo { Id = "3BE9A0C3", Name = "Passive 65 (Type 1)" },
                new PassiveIdInfo { Id = "A2E0F179", Name = "Passive 65 (Type 2)" },
                new PassiveIdInfo { Id = "D5E7C1EF", Name = "Passive 66 (Type 1)" },
                new PassiveIdInfo { Id = "4B83544C", Name = "Passive 66 (Type 2)" },
                new PassiveIdInfo { Id = "3C8464DA", Name = "Passive 67 (Type 1)" },
                new PassiveIdInfo { Id = "A58D3560", Name = "Passive 67 (Type 2)" },
                new PassiveIdInfo { Id = "D28A05F6", Name = "Passive 68 (Type 1)" },
                new PassiveIdInfo { Id = "42351867", Name = "Passive 68 (Type 2)" },
                new PassiveIdInfo { Id = "353228F1", Name = "Passive 69 (Type 1)" },
                new PassiveIdInfo { Id = "55F5A114", Name = "Passive 69 (Type 2)" },
                new PassiveIdInfo { Id = "5C2AF11A", Name = "Hero Passive 01" },
                new PassiveIdInfo { Id = "6BF40128", Name = "Hero Passive 04" },
                new PassiveIdInfo { Id = "2122BE90", Name = "Hero Passive 08 (Type 1)" },
                new PassiveIdInfo { Id = "2894F2BB", Name = "Hero Passive 08 (Type 2)" },
                new PassiveIdInfo { Id = "999ED9F5", Name = "Hero Passive 09" },
                new PassiveIdInfo { Id = "EE284FFD", Name = "Hero Passive 17" },
                new PassiveIdInfo { Id = "FC9DE013", Name = "Hero Passive 18" },
                new PassiveIdInfo { Id = "1C429720", Name = "Hero Passive 20" },
                new PassiveIdInfo { Id = "845A23F9", Name = "Hero Passive 21" },
                new PassiveIdInfo { Id = "6A5442D5", Name = "Hero Passive 22" },
                new PassiveIdInfo { Id = "6D3986CC", Name = "Hero Passive 24" },
                new PassiveIdInfo { Id = "8A81ABCB", Name = "Hero Passive 25" },
                new PassiveIdInfo { Id = "9D4112B8", Name = "Hero Passive 26" },
                new PassiveIdInfo { Id = "734F7394", Name = "Hero Passive 27" },
                new PassiveIdInfo { Id = "9A2CD6A1", Name = "Hero Passive 28" },
                new PassiveIdInfo { Id = "3EE1D416", Name = "Hero Passive 30" },
                new PassiveIdInfo { Id = "939A9A8A", Name = "Hero Passive 31 (Type 1)" },
                new PassiveIdInfo { Id = "E49DAA1C", Name = "Hero Passive 31 (Type 2)" },
                new PassiveIdInfo { Id = "A507B4E9", Name = "Hero Passive 32" },
                new PassiveIdInfo { Id = "9E74A7E8", Name = "Hero Passive 34" },
                new PassiveIdInfo { Id = "6C1E7F35", Name = "Hero Passive 37" },
                new PassiveIdInfo { Id = "5212E484", Name = "Hero Passive 40" },
                new PassiveIdInfo { Id = "BB7141B1", Name = "Hero Passive 41" },
                new PassiveIdInfo { Id = "793FB747", Name = "Hero Passive 45 (Type 1)" },
                new PassiveIdInfo { Id = "0E64FCA7", Name = "Hero Passive 45 (Type 2)" },
                new PassiveIdInfo { Id = "905C1272", Name = "Hero Passive 46 (Type 1)" },
                new PassiveIdInfo { Id = "2B0FA37B", Name = "Hero Passive 46 (Type 2)" },
                new PassiveIdInfo { Id = "EEED6ECF", Name = "Hero Passive 48 (Type 1)" },
                new PassiveIdInfo { Id = "99EA5E59", Name = "Hero Passive 48 (Type 2)" },
                new PassiveIdInfo { Id = "80F16F18", Name = "Hero Passive 53" },
                new PassiveIdInfo { Id = "E7BC9AC9", Name = "Hero Passive 55" },
                new PassiveIdInfo { Id = "0EDF3FFC", Name = "Hero Passive 57" },
                new PassiveIdInfo { Id = "706E4341", Name = "Hero Passive 58" },
                new PassiveIdInfo { Id = "67AEFA32", Name = "Hero Passive 59" },
                new PassiveIdInfo { Id = "1E724296", Name = "Hero Passive 64 (Type 1)" },
                new PassiveIdInfo { Id = "B7AE2B4C", Name = "Hero Passive 64 (Type 2)" },
                new PassiveIdInfo { Id = "1FBC8477", Name = "Hero Passive 67" },
                new PassiveIdInfo { Id = "FF63F344", Name = "Hero Passive 68" },
                new PassiveIdInfo { Id = "07B7F21B", Name = "Hero Passive 69" }
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

        public ObservableCollection<SpiritCardInfo> SpiritCardsCollection { get; }

        public ObservableCollection<PlayerSpiritInfo> PlayerSpiritsCollection { get; }

        public ObservableCollection<ItemInfo> WorkingItems { get; }

        public ObservableCollection<PassiveIdInfo> AllPassiveIds { get; }

        public string CustomPassive1
        {
            get => _customPassive1;
            set
            {
                _customPassive1 = value;
                OnPropertyChanged();
            }
        }

        public string CustomPassive2
        {
            get => _customPassive2;
            set
            {
                _customPassive2 = value;
                OnPropertyChanged();
            }
        }

        public string CustomPassive3
        {
            get => _customPassive3;
            set
            {
                _customPassive3 = value;
                OnPropertyChanged();
            }
        }

        public string CustomPassive4
        {
            get => _customPassive4;
            set
            {
                _customPassive4 = value;
                OnPropertyChanged();
            }
        }

        public string CustomPassive5
        {
            get => _customPassive5;
            set
            {
                _customPassive5 = value;
                OnPropertyChanged();
            }
        }

        public bool IsCustomPassivesEnabled => !IsCustomPassivesUnderMaintenance;

        public bool IsCustomPassivesUnderMaintenance
        {
            get => _isCustomPassivesUnderMaintenance;
            set
            {
                _isCustomPassivesUnderMaintenance = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomPassivesEnabled));
            }
        }

        public PassiveInfo? SelectedPassive
        {
            get => _selectedPassive;
            set
            {
                _selectedPassive = value;
                OnPropertyChanged();
                // When a passive is selected from dropdown, load its value
                if (_selectedPassive != null)
                {
                    LoadSelectedPassiveValue();
                }
            }
        }

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
                ((RelayCommand)ToggleEliteSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleStoreItemMultiplierCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleUnlimitedSpiritsCommand).RaiseCanExecuteChanged();
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

        public TutorialsViewModel? TutorialsViewModel
        {
            get => _tutorialsViewModel;
            set
            {
                _tutorialsViewModel = value;
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
        public ICommand RestartGameCommand { get; }
        public ICommand ToggleSpiritsFreezeCommand { get; }
        public ICommand ToggleSpiritIncrementCommand { get; }
        public ICommand ToggleEliteSpiritIncrementCommand { get; }
        public ICommand ToggleCustomBaseSpiritIncrementCommand { get; }
        public ICommand ToggleCustomHeroSpiritIncrementCommand { get; }
        public ICommand ToggleStoreItemMultiplierCommand { get; }
        public ICommand ToggleUnlimitedSpiritsCommand { get; }
        public ICommand ToggleUnlimitedHeroesCommand { get; }
        public ICommand ToggleFreeBuySpiritMarketCommand { get; }
        public ICommand ToggleFreeBuyShopCommand { get; }
        public ICommand TogglePassiveValueEditingCommand { get; }
        public ICommand AddSpiritsCommand { get; }
        public ICommand AddBeansCommand { get; }
        public ICommand OpenItemListCommand { get; }
        public ICommand SelectPassiveValuesEditorCommand { get; }
        public ICommand ApplyPassiveValueCommand { get; }
        public ICommand SelectSpiritCardsEditorCommand { get; }
        public ICommand AddSpiritCardCommand { get; }
        public ICommand ToggleSpiritCardCommand { get; }
        public ICommand ToggleAllSpiritCardsCommand { get; }
        public ICommand SelectPlayerSpiritsEditorCommand { get; }
        public ICommand TogglePlayerSpiritCommand { get; }
        public ICommand ToggleAllPlayerSpiritsCommand { get; }
        public ICommand TogglePlayerLevelCommand { get; }
        public ICommand ApplyPlayerLevelCommand { get; }
        public ICommand SelectCustomPassivesEditorCommand { get; }
        public ICommand ApplyCustomPassivesCommand { get; }
        public ICommand ClearCustomPassivesCommand { get; }
        public ICommand OpenTutorialsCommand { get; }

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
                ((RelayCommand)ToggleSpiritsFreezeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritIncrementCommand).RaiseCanExecuteChanged();
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
                ((RelayCommand)ToggleSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleEliteSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomBaseSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomHeroSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleStoreItemMultiplierCommand).RaiseCanExecuteChanged();
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
                ((RelayCommand)ToggleSpiritsFreezeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleEliteSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomBaseSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomHeroSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleStoreItemMultiplierCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsEliteSpiritIncrementEnabled
        {
            get => _isEliteSpiritIncrementEnabled;
            set
            {
                _isEliteSpiritIncrementEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleEliteSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritsFreezeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomBaseSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomHeroSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleStoreItemMultiplierCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsCustomBaseSpiritIncrementEnabled
        {
            get => _isCustomBaseSpiritIncrementEnabled;
            set
            {
                _isCustomBaseSpiritIncrementEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleCustomBaseSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritsFreezeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleEliteSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomHeroSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleStoreItemMultiplierCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsCustomHeroSpiritIncrementEnabled
        {
            get => _isCustomHeroSpiritIncrementEnabled;
            set
            {
                _isCustomHeroSpiritIncrementEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleCustomHeroSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritsFreezeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleEliteSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomBaseSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleStoreItemMultiplierCommand).RaiseCanExecuteChanged();
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
                ((RelayCommand)ToggleSpiritsFreezeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleEliteSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomBaseSpiritIncrementCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleCustomHeroSpiritIncrementCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsUnlimitedSpiritsEnabled
        {
            get => _isUnlimitedSpiritsEnabled;
            set
            {
                _isUnlimitedSpiritsEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleUnlimitedSpiritsCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsPlayerLevelEnabled
        {
            get => _isPlayerLevelEnabled;
            set
            {
                _isPlayerLevelEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)TogglePlayerLevelCommand).RaiseCanExecuteChanged();

                // Show/hide input when toggling
                ShowPlayerLevelInput = value;
            }
        }

        public string PlayerLevelInput
        {
            get => _playerLevelInput;
            set
            {
                _playerLevelInput = value;
                OnPropertyChanged();
                ((RelayCommand)ApplyPlayerLevelCommand).RaiseCanExecuteChanged();
            }
        }

        public bool ShowPlayerLevelInput
        {
            get => _showPlayerLevelInput;
            set
            {
                _showPlayerLevelInput = value;
                OnPropertyChanged();
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

        // Toggle option maintenance properties
        public bool IsFreezeItemsUnderMaintenance
        {
            get => _isFreezeItemsUnderMaintenance;
            set
            {
                _isFreezeItemsUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsIncrementItemsUnderMaintenance
        {
            get => _isIncrementItemsUnderMaintenance;
            set
            {
                _isIncrementItemsUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsStoreMultiplierUnderMaintenance
        {
            get => _isStoreMultiplierUnderMaintenance;
            set
            {
                _isStoreMultiplierUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsFreezeSpiritsUnderMaintenance
        {
            get => _isFreezeSpiritsUnderMaintenance;
            set
            {
                _isFreezeSpiritsUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsIncrementSpiritsUnderMaintenance
        {
            get => _isIncrementSpiritsUnderMaintenance;
            set
            {
                _isIncrementSpiritsUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsUnlimitedHeroesUnderMaintenance
        {
            get => _isUnlimitedHeroesUnderMaintenance;
            set
            {
                _isUnlimitedHeroesUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsUnlimitedHeroesEnabled
        {
            get => _isUnlimitedHeroesEnabled;
            set
            {
                _isUnlimitedHeroesEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsFreeBuySpiritMarketEnabled
        {
            get => _isFreeBuySpiritMarketEnabled;
            set
            {
                _isFreeBuySpiritMarketEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsFreeBuySpiritMarketUnderMaintenance
        {
            get => _isFreeBuySpiritMarketUnderMaintenance;
            set
            {
                _isFreeBuySpiritMarketUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsPlayerSpiritsUnderMaintenance
        {
            get => _isPlayerSpiritsUnderMaintenance;
            set
            {
                _isPlayerSpiritsUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsFreeBuyShopEnabled
        {
            get => _isFreeBuyShopEnabled;
            set
            {
                _isFreeBuyShopEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsFreeBuyShopUnderMaintenance
        {
            get => _isFreeBuyShopUnderMaintenance;
            set
            {
                _isFreeBuyShopUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsPlayerLevelUnderMaintenance
        {
            get => _isPlayerLevelUnderMaintenance;
            set
            {
                _isPlayerLevelUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsIncrementEliteSpiritsUnderMaintenance
        {
            get => _isIncrementEliteSpiritsUnderMaintenance;
            set
            {
                _isIncrementEliteSpiritsUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsCustomBaseSpiritIncrementUnderMaintenance
        {
            get => _isCustomBaseSpiritIncrementUnderMaintenance;
            set
            {
                _isCustomBaseSpiritIncrementUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        public bool IsCustomHeroSpiritIncrementUnderMaintenance
        {
            get => _isCustomHeroSpiritIncrementUnderMaintenance;
            set
            {
                _isCustomHeroSpiritIncrementUnderMaintenance = value;
                OnPropertyChanged();
            }
        }

        // Card visibility properties (for hiding cards)
        public bool IsFreezeItemsVisible => !_isFreezeItemsHidden;
        public bool IsIncrementItemsVisible => !_isIncrementItemsHidden;
        public bool IsStoreMultiplierVisible => !_isStoreMultiplierHidden;
        public bool IsStarsVisible => !_isStarsHidden;
        public bool IsFlowersVisible => !_isFlowersHidden;
        public bool IsBeansVisible => !_isBeansHidden;
        public bool IsVictoryItemsVisible => !_isVictoryItemsHidden;
        public bool IsFreezeSpiritsVisible => !_isFreezeSpiritsHidden;
        public bool IsIncrementSpiritsVisible => !_isIncrementSpiritsHidden;
        public bool IsIncrementEliteSpiritsVisible => !_isIncrementEliteSpiritsHidden;
        public bool IsSpiritsVisible => !_isSpiritsHidden;
        public bool IsPlayerSpiritsVisible => !_isPlayerSpiritsHidden;
        public bool IsFreeBuySpiritMarketVisible => !_isFreeBuySpiritMarketHidden;
        public bool IsFreeBuyShopVisible => !_isFreeBuyShopHidden;
        public bool IsUnlimitedHeroesVisible => !_isUnlimitedHeroesHidden;
        public bool IsPlayerLevelVisible => !_isPlayerLevelHidden;
        public bool IsPassiveValuesVisible => !_isPassiveValuesHidden;
        public bool IsCustomPassivesVisible => !_isCustomPassivesHidden;

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

                    // Enable custom passives tracking if on custom passives screen
                    if (SelectedTool == "custompassives")
                    {
                        try
                        {
                            // Attach and enable tracking for custom passives
                            if (_customPassivesService.AttachToProcess())
                            {
                                _customPassivesService.EnableTracking();

                                // Wait a moment for the game to trigger the hook and populate current values
                                await System.Threading.Tasks.Task.Delay(1000);

                                // Refresh current passive values into the dropdowns
                                RefreshCustomPassives();
                            }
                        }
                        catch (Exception ex)
                        {
                            // Don't fail the whole attach if custom passives fails
                            StatusMessage = $"Custom passives tracking error: {ex.Message}";
                        }
                    }

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
                _unlimitedSpiritsService.DetachFromProcess();
                _playerLevelService.DetachFromProcess();
                _customPassivesService.DetachFromProcess();
                IsAttached = false;
                _lastKnownGoodTicketValue = 0;
                IsStarsFrozen = false;
                IsFlowersIncrementEnabled = false;
                IsSpiritsFrozen = false;
                IsSpiritIncrementEnabled = false;
                IsStoreItemMultiplierEnabled = false;
                IsUnlimitedSpiritsEnabled = false;
                IsPlayerLevelEnabled = false;
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
                    _unlimitedSpiritsService.DetachFromProcess();
                    _playerLevelService.DetachFromProcess();
                    IsAttached = false;
                    _lastKnownGoodTicketValue = 0;
                    IsStarsFrozen = false;
                    IsFlowersIncrementEnabled = false;
                    IsSpiritsFrozen = false;
                    IsSpiritIncrementEnabled = false;
                    IsStoreItemMultiplierEnabled = false;
                    IsUnlimitedSpiritsEnabled = false;
                    IsPlayerLevelEnabled = false;
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
                        // Verify the bytes were actually restored
                        byte[]? readBack = _memoryService.ReadBytes(FLOWER_INCREMENT_ADDRESS, FLOWER_ORIGINAL_BYTES.Length);
                        bool verified = readBack != null && readBack.SequenceEqual(FLOWER_ORIGINAL_BYTES);

                        if (verified)
                        {
                            IsFlowersIncrementEnabled = false;
                            StatusMessage = "Flower increment disabled - original bytes verified and restored!";
                        }
                        else
                        {
                            StatusMessage = "WARNING: Disable may have failed - bytes not verified!";
                            MessageBox.Show(
                                "The disable operation completed, but verification failed.\n\n" +
                                "The original bytes may not have been restored correctly.\n\n" +
                                "Try disabling again or restart the game.",
                                "Verification Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        StatusMessage = "Failed to disable flower increment";
                        MessageBox.Show(
                            "Failed to disable flower increment.\n\n" +
                            "The game memory could not be restored.\n\n" +
                            "You may need to restart the game.",
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
            return IsAttached && !IsSpiritIncrementEnabled && !IsEliteSpiritIncrementEnabled
                && !IsCustomBaseSpiritIncrementEnabled && !IsCustomHeroSpiritIncrementEnabled
                && !IsStoreItemMultiplierEnabled;
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
                    // Freeze spirits directly without dialog
                    success1 = _memoryService.WriteBytes(SPIRIT_FREEZE_ADDRESS, SPIRIT_FREEZE_BYTES);
                    success2 = _memoryService.WriteBytes(ELITE_SPIRIT_FREEZE_ADDRESS, ELITE_SPIRIT_FREEZE_BYTES);

                    if (success1 && success2)
                    {
                        IsSpiritsFrozen = true;
                        StatusMessage = "Spirits frozen - unlimited hero & elite spirits enabled!";
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
            return IsAttached && !IsSpiritsFrozen && !IsEliteSpiritIncrementEnabled
                && !IsCustomBaseSpiritIncrementEnabled && !IsCustomHeroSpiritIncrementEnabled
                && !IsStoreItemMultiplierEnabled;
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
                        StatusMessage = "Hero spirit increment enabled - hero spirits will increase by 2 when used!";
                    }
                    else
                    {
                        StatusMessage = "Failed to enable hero spirit increment";
                        MessageBox.Show(
                            "Failed to enable hero spirit increment. Make sure the game is running and you are attached to the process.",
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
                        StatusMessage = "Hero spirit increment disabled - normal hero spirit behavior restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable hero spirit increment";
                        MessageBox.Show(
                            "Failed to disable hero spirit increment.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling hero spirit increment: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling hero spirit increment:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleEliteSpiritIncrement(object? parameter)
        {
            return IsAttached && !IsSpiritsFrozen && !IsSpiritIncrementEnabled
                && !IsCustomBaseSpiritIncrementEnabled && !IsCustomHeroSpiritIncrementEnabled
                && !IsStoreItemMultiplierEnabled;
        }

        private void ToggleEliteSpiritIncrement(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsEliteSpiritIncrementEnabled)
                {
                    success = _memoryService.InjectEliteSpiritIncrement();

                    if (success)
                    {
                        IsEliteSpiritIncrementEnabled = true;
                        StatusMessage = "Elite spirit increment enabled - elite spirits will increase by 2 when used!";
                    }
                    else
                    {
                        StatusMessage = "Failed to enable elite spirit increment";
                        MessageBox.Show(
                            "Failed to enable elite spirit increment. Make sure the game is running and you are attached to the process.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.RemoveEliteSpiritIncrement();

                    if (success)
                    {
                        IsEliteSpiritIncrementEnabled = false;
                        StatusMessage = "Elite spirit increment disabled - normal elite spirit behavior restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable elite spirit increment";
                        MessageBox.Show(
                            "Failed to disable elite spirit increment.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling elite spirit increment: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling elite spirit increment:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleCustomBaseSpiritIncrement(object? parameter)
        {
            return IsAttached && !IsSpiritsFrozen && !IsSpiritIncrementEnabled
                && !IsEliteSpiritIncrementEnabled && !IsCustomHeroSpiritIncrementEnabled
                && !IsStoreItemMultiplierEnabled;
        }

        private void ToggleCustomBaseSpiritIncrement(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsCustomBaseSpiritIncrementEnabled)
                {
                    success = _memoryService.InjectEliteSpiritIncrement();

                    if (success)
                    {
                        IsCustomBaseSpiritIncrementEnabled = true;
                        StatusMessage = "Custom (Base) Basara increment enabled!";
                    }
                    else
                    {
                        StatusMessage = "Failed to enable Custom (Base) Basara increment";
                        MessageBox.Show(
                            "Failed to enable Custom (Base) Basara increment. Make sure the game is running and you are attached to the process.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.RemoveEliteSpiritIncrement();

                    if (success)
                    {
                        IsCustomBaseSpiritIncrementEnabled = false;
                        StatusMessage = "Custom (Base) Basara increment disabled";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable Custom (Base) Basara increment";
                        MessageBox.Show(
                            "Failed to disable Custom (Base) Basara increment.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling Custom (Base) Basara increment: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling Custom (Base) Basara increment:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleCustomHeroSpiritIncrement(object? parameter)
        {
            return IsAttached && !IsSpiritsFrozen && !IsSpiritIncrementEnabled
                && !IsEliteSpiritIncrementEnabled && !IsCustomBaseSpiritIncrementEnabled
                && !IsStoreItemMultiplierEnabled;
        }

        private void ToggleCustomHeroSpiritIncrement(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsCustomHeroSpiritIncrementEnabled)
                {
                    success = _memoryService.InjectSpiritIncrement();

                    if (success)
                    {
                        IsCustomHeroSpiritIncrementEnabled = true;
                        StatusMessage = "Custom (Hero) Basara increment enabled!";
                    }
                    else
                    {
                        StatusMessage = "Failed to enable Custom (Hero) Basara increment";
                        MessageBox.Show(
                            "Failed to enable Custom (Hero) Basara increment. Make sure the game is running and you are attached to the process.",
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
                        IsCustomHeroSpiritIncrementEnabled = false;
                        StatusMessage = "Custom (Hero) Basara increment disabled";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable Custom (Hero) Basara increment";
                        MessageBox.Show(
                            "Failed to disable Custom (Hero) Basara increment.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling Custom (Hero) Basara increment: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling Custom (Hero) Basara increment:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleStoreItemMultiplier(object? parameter)
        {
            return IsAttached && !IsSpiritsFrozen && !IsSpiritIncrementEnabled
                && !IsEliteSpiritIncrementEnabled && !IsCustomBaseSpiritIncrementEnabled
                && !IsCustomHeroSpiritIncrementEnabled;
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

                        MessageBox.Show(
                            "Store Item Multiplier has been enabled!\n\n" +
                            "WARNING: Turn OFF this multiplier before summoning spirits!\n\n" +
                            "Using spirit actions (summoning, etc.) while this multiplier is active will crash the game.",
                            "Warning - Disable Before Using Spirits",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
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

        private bool CanToggleUnlimitedSpirits(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleUnlimitedSpirits(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsUnlimitedSpiritsEnabled)
                {
                    // Attach the service to the process first
                    if (!_unlimitedSpiritsService.AttachToProcess())
                    {
                        StatusMessage = "Failed to attach unlimited spirits service to game process";
                        MessageBox.Show(
                            "Failed to attach to game process.\n\n" +
                            "Make sure the game is running.",
                            "Attach Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    success = _unlimitedSpiritsService.EnableUnlimitedSpirits();

                    if (success)
                    {
                        IsUnlimitedSpiritsEnabled = true;
                        StatusMessage = "Unlimited heroes enabled - you can now have up to 5 heroes in team!";
                    }
                    else
                    {
                        StatusMessage = "Failed to enable unlimited heroes";
                        MessageBox.Show(
                            "Failed to enable unlimited heroes.\n\n" +
                            "Make sure the game is running and you are in the correct game screen.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _unlimitedSpiritsService.DisableUnlimitedSpirits();

                    if (success)
                    {
                        IsUnlimitedSpiritsEnabled = false;
                        StatusMessage = "Unlimited heroes disabled - normal team dock limit restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable unlimited heroes";
                        MessageBox.Show(
                            "Failed to disable unlimited heroes.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling unlimited heroes: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling unlimited heroes:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleUnlimitedHeroes(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleUnlimitedHeroes(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsUnlimitedHeroesEnabled)
                {
                    success = _memoryService.InjectUnlimitedHeroes();

                    if (success)
                    {
                        IsUnlimitedHeroesEnabled = true;
                        StatusMessage = "Unlimited Heroes enabled - you can now have up to 5 heroes in your team dock!";
                        MessageBox.Show(
                            "Unlimited Heroes has been enabled.\n\n" +
                            "You can now add up to 5 hero characters to your team dock instead of the normal limit of 2.\n\n" +
                            "This will remain active until you disable it or restart the game.",
                            "Unlimited Heroes Enabled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = "Failed to enable unlimited heroes";
                        MessageBox.Show(
                            "Failed to enable unlimited heroes.\n\n" +
                            "Make sure the game is running.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.RemoveUnlimitedHeroes();

                    if (success)
                    {
                        IsUnlimitedHeroesEnabled = false;
                        StatusMessage = "Unlimited Heroes disabled - normal team dock limit (2 heroes) restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable unlimited heroes";
                        MessageBox.Show(
                            "Failed to disable unlimited heroes.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling unlimited heroes: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling unlimited heroes:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleFreeBuySpiritMarket(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleFreeBuySpiritMarket(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsFreeBuySpiritMarketEnabled)
                {
                    success = _memoryService.InjectFreeBuySpiritMarket();

                    if (success)
                    {
                        IsFreeBuySpiritMarketEnabled = true;
                        StatusMessage = "Free Buy Spirit Market enabled - spirits now cost 0 and quantity set to 999!";

                        // Ask if user wants to enable Store Item Multiplier (x2457)
                        var multiplierResult = MessageBox.Show(
                            "Free Buy Spirit Market has been enabled!\n\n" +
                            "Do you also want to activate the Store Item Multiplier (x2457)?\n\n" +
                            "⚠️ IMPORTANT: This multiplier only works for:\n" +
                            "  • Items\n" +
                            "  • Hissatsus\n" +
                            "  • Kenshin\n\n" +
                            "❌ Does NOT work for spirits!\n\n" +
                            "Click YES to enable multiplier, or NO to skip.",
                            "Enable Store Item Multiplier?",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (multiplierResult == MessageBoxResult.Yes)
                        {
                            // Enable Store Item Multiplier
                            if (!IsStoreItemMultiplierEnabled)
                            {
                                ToggleStoreItemMultiplier(null);
                            }
                        }

                        MessageBox.Show(
                            "Free Buy Spirit Market has been enabled.\n\n" +
                            "Spirit Market Features:\n" +
                            "• All spirits cost 0 (free)\n" +
                            "• Spirit quantity automatically set to 999\n" +
                            "• Player level set to 99 for purchases\n" +
                            "• Works in Spirit Market and Atrium of the Untamed\n\n" +
                            "This will remain active until you disable it or restart the game.",
                            "Free Buy Spirit Market Enabled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = "Failed to enable Free Buy Spirit Market";
                        MessageBox.Show(
                            "Failed to enable Free Buy Spirit Market.\n\n" +
                            "Make sure the game is running.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.RemoveFreeBuySpiritMarket();

                    if (success)
                    {
                        IsFreeBuySpiritMarketEnabled = false;
                        StatusMessage = "Free Buy Spirit Market disabled - normal spirit costs restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable Free Buy Spirit Market";
                        MessageBox.Show(
                            "Failed to disable Free Buy Spirit Market.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling Free Buy Spirit Market: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling Free Buy Spirit Market:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleFreeBuyShop(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleFreeBuyShop(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsFreeBuyShopEnabled)
                {
                    success = _memoryService.InjectFreeBuyShop();

                    if (success)
                    {
                        IsFreeBuyShopEnabled = true;
                        StatusMessage = "Free Buy Shop enabled - items now cost 0 (Token quantity set to 999)!";

                        MessageBox.Show(
                            "Free Buy Shop has been enabled.\n\n" +
                            "Shop Features:\n" +
                            "• All store items cost 0 (free)\n" +
                            "• Token quantity automatically set to 999\n" +
                            "• Works for regular store/shop (not Spirit Market)\n\n" +
                            "This will remain active until you disable it or restart the game.",
                            "Free Buy Shop Enabled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = "Failed to enable Free Buy Shop";
                        MessageBox.Show(
                            "Failed to enable Free Buy Shop.\n\n" +
                            "Make sure the game is running.",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _memoryService.RemoveFreeBuyShop();

                    if (success)
                    {
                        IsFreeBuyShopEnabled = false;
                        StatusMessage = "Free Buy Shop disabled - normal shop costs restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable Free Buy Shop";
                        MessageBox.Show(
                            "Failed to disable Free Buy Shop.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling Free Buy Shop: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling Free Buy Shop:\n\n{ex.Message}",
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

        private void LoadSelectedPassiveValue()
        {
            if (_selectedPassive == null || !IsPassiveValueEditingEnabled || !IsAttached)
                return;

            try
            {
                // This is a placeholder - in a full implementation, you would:
                // 1. Look up the passive value from game memory based on PassiveId
                // 2. Read the current value
                // 3. Update PassiveValueType, PassiveCurrentValue, and HasPassiveValue

                // For now, just show that a passive was selected
                PassiveValueType = _selectedPassive.Type == "Normal" ? "DWord" : "Float";
                PassiveCurrentValue = "0"; // Placeholder - would read from memory
                HasPassiveValue = true;
                StatusMessage = $"Selected passive: {_selectedPassive.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading passive value: {ex.Message}";
            }
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

        private bool CanAddSpiritCard(object? parameter)
        {
            return IsAttached;
        }

        private void AddSpiritCard(object? parameter)
        {
            if (!IsAttached || parameter == null)
                return;

            var spiritCard = parameter as SpiritCardInfo;
            if (spiritCard == null)
                return;

            try
            {
                bool success = _memoryService.AddSpiritCardToTeam(spiritCard.SpiritId, 50);

                if (success)
                {
                    StatusMessage = $"Successfully added 50x {spiritCard.DisplayName} ({spiritCard.VariantDisplay}) to your team!";
                    MessageBox.Show(
                        $"Successfully added 50x {spiritCard.DisplayName} ({spiritCard.VariantDisplay})!\n\n" +
                        "The spirit cards have been queued. Open Team Dock - Spirits to receive them.",
                        "Spirit Card Added",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Failed to add {spiritCard.DisplayName}";
                    MessageBox.Show(
                        $"Failed to add {spiritCard.DisplayName}.\n\n" +
                        "Make sure the game is running and you have attached to the process.",
                        "Add Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding spirit card: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while adding spirit card:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleSpiritCard(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleSpiritCard(object? parameter)
        {
            if (!IsAttached || parameter == null)
                return;

            var spiritCard = parameter as SpiritCardInfo;
            if (spiritCard == null)
                return;

            try
            {
                if (spiritCard.IsEnabled)
                {
                    // Add spirit card when toggled ON
                    bool success = _memoryService.AddSpiritCardToTeam(spiritCard.SpiritId, 50);

                    if (success)
                    {
                        StatusMessage = $"Enabled: {spiritCard.DisplayName} ({spiritCard.VariantDisplay}) - will be added";
                        MessageBox.Show(
                            $"{spiritCard.DisplayName} ({spiritCard.VariantDisplay}) is now enabled!\n\n" +
                            "Open Team Dock - Spirits in the game to receive this spirit card.",
                            "Spirit Card Enabled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = $"Failed to enable {spiritCard.DisplayName}";
                        spiritCard.IsEnabled = false; // Revert toggle
                        MessageBox.Show(
                            $"Failed to enable {spiritCard.DisplayName}.\n\n" +
                            "Make sure:\n" +
                            "1. You are attached to the game\n" +
                            "2. You have opened Team Dock - Spirits at least once",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Disabled
                    StatusMessage = $"Disabled: {spiritCard.DisplayName} ({spiritCard.VariantDisplay})";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling spirit card: {ex.Message}";
                spiritCard.IsEnabled = false; // Revert toggle on error
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Exception",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleAllSpiritCards(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleAllSpiritCards(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool enableAll = SpiritCardsCollection.Any(s => !s.IsEnabled);

                if (enableAll)
                {
                    // Enable all spirits
                    foreach (var spiritCard in SpiritCardsCollection)
                    {
                        spiritCard.IsEnabled = true;
                    }

                    // Collect all spirit IDs
                    List<uint> allSpiritIds = SpiritCardsCollection.Select(s => s.SpiritId).ToList();

                    // Use the Add-All mode to add all spirits at once
                    bool success = _memoryService.SetAllSpiritCardsToAdd(allSpiritIds);

                    if (success)
                    {
                        StatusMessage = "All spirit cards enabled - Open Team Dock - Spirits to receive them!";
                        MessageBox.Show(
                            $"All {allSpiritIds.Count} spirit cards have been enabled!\n\n" +
                            "Open Team Dock - Spirits in the game to receive the spirit cards.",
                            "All Cards Enabled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = "Failed to enable all spirit cards";
                        // Revert all toggles
                        foreach (var spiritCard in SpiritCardsCollection)
                        {
                            spiritCard.IsEnabled = false;
                        }
                    }
                }
                else
                {
                    // Disable all spirits
                    foreach (var spiritCard in SpiritCardsCollection)
                    {
                        spiritCard.IsEnabled = false;
                    }

                    // Set back to Add-One mode with no spirit
                    _memoryService.SetSpiritCardToAdd(0, 50);

                    StatusMessage = "All spirit cards disabled";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling all spirit cards: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling all spirit cards:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Revert all toggles on error
                foreach (var spiritCard in SpiritCardsCollection)
                {
                    spiritCard.IsEnabled = false;
                }
            }
        }

        private bool CanTogglePlayerSpirit(object? parameter)
        {
            return IsAttached;
        }

        private void TogglePlayerSpirit(object? parameter)
        {
            if (!IsAttached || parameter == null)
                return;

            var playerSpirit = parameter as PlayerSpiritInfo;
            if (playerSpirit == null)
                return;

            try
            {
                if (playerSpirit.IsEnabled)
                {
                    // Add player spirit when toggled ON
                    bool success = _memoryService.AddPlayerSpiritToTeam(playerSpirit.PlayerId, playerSpirit.SelectedRarity);

                    if (success)
                    {
                        StatusMessage = $"Enabled: {playerSpirit.DisplayName} ({playerSpirit.MixDisplay}) - {playerSpirit.RarityDisplay}";
                        MessageBox.Show(
                            $"{playerSpirit.DisplayName} ({playerSpirit.MixDisplay}) is now enabled!\n" +
                            $"Rarity: {playerSpirit.RarityDisplay}\n\n" +
                            "Open Team Dock - Spirits in the game to receive this player spirit.",
                            "Player Spirit Enabled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = $"Failed to enable {playerSpirit.DisplayName}";
                        playerSpirit.IsEnabled = false; // Revert toggle
                        MessageBox.Show(
                            $"Failed to enable {playerSpirit.DisplayName}.\n\n" +
                            "Make sure:\n" +
                            "1. You are attached to the game\n" +
                            "2. You have opened Team Dock - Spirits at least once",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Disable player spirit when toggled OFF
                    _memoryService.ClearPlayerSpiritToAdd();
                    StatusMessage = $"Disabled: {playerSpirit.DisplayName} ({playerSpirit.MixDisplay})";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling player spirit: {ex.Message}";
                playerSpirit.IsEnabled = false; // Revert toggle on error
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Exception",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanToggleAllPlayerSpirits(object? parameter)
        {
            return IsAttached;
        }

        private void ToggleAllPlayerSpirits(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool enableAll = PlayerSpiritsCollection.Any(s => !s.IsEnabled);

                if (enableAll)
                {
                    // Get the rarity from the first spirit (they should all use the same rarity when adding all)
                    int rarity = PlayerSpiritsCollection.FirstOrDefault()?.SelectedRarity ?? 1;

                    // Enable all player spirits
                    foreach (var playerSpirit in PlayerSpiritsCollection)
                    {
                        playerSpirit.IsEnabled = true;
                    }

                    // Collect all player IDs
                    List<uint> allPlayerIds = PlayerSpiritsCollection.Select(s => s.PlayerId).ToList();

                    // Enable injection if needed
                    if (!_memoryService.AddPlayerSpiritToTeam(allPlayerIds[0], rarity))
                    {
                        throw new Exception("Failed to enable player spirit injection");
                    }

                    // Use the Add-All mode to add all player spirits at once
                    bool success = _memoryService.SetAllPlayerSpiritsToAdd(allPlayerIds, rarity);

                    if (success)
                    {
                        StatusMessage = "All player spirits enabled - Open Team Dock - Spirits to receive them!";
                        MessageBox.Show(
                            $"All {allPlayerIds.Count} player spirits have been enabled!\n\n" +
                            "Open Team Dock - Spirits in the game to receive the player spirits.",
                            "All Player Spirits Enabled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = "Failed to enable all player spirits";
                        // Revert all toggles
                        foreach (var playerSpirit in PlayerSpiritsCollection)
                        {
                            playerSpirit.IsEnabled = false;
                        }
                    }
                }
                else
                {
                    // Disable all player spirits
                    foreach (var playerSpirit in PlayerSpiritsCollection)
                    {
                        playerSpirit.IsEnabled = false;
                    }

                    // Set back to Add-One mode with no player
                    _memoryService.SetPlayerSpiritToAdd(0, 1);

                    StatusMessage = "All player spirits disabled";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling all player spirits: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling all player spirits:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Revert all toggles on error
                foreach (var playerSpirit in PlayerSpiritsCollection)
                {
                    playerSpirit.IsEnabled = false;
                }
            }
        }

        private bool CanTogglePlayerLevel(object? parameter)
        {
            return IsAttached;
        }

        private void TogglePlayerLevel(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                bool success;

                if (!IsPlayerLevelEnabled)
                {
                    // Parse the level input
                    if (!int.TryParse(PlayerLevelInput, out int level) || level < 1 || level > 99)
                    {
                        MessageBox.Show(
                            "Please enter a valid level between 1 and 99.",
                            "Invalid Level",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    // Attach the service to the process first if not already attached
                    if (!_playerLevelService.AttachToProcess())
                    {
                        StatusMessage = "Failed to attach player level service to game process";
                        MessageBox.Show(
                            "Failed to attach to game process.\n\n" +
                            "Make sure the game is running.",
                            "Attach Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    success = _playerLevelService.EnablePlayerLevel(level);

                    if (success)
                    {
                        IsPlayerLevelEnabled = true;
                        StatusMessage = $"Player level feature enabled - level set to {level}!";
                    }
                    else
                    {
                        StatusMessage = "Failed to enable player level feature";
                        MessageBox.Show(
                            "Failed to enable player level feature.\n\n" +
                            "Make sure:\n" +
                            "1. The game is running\n" +
                            "2. You are in the Team Dock menu\n" +
                            "3. The game version is correct",
                            "Enable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    success = _playerLevelService.DisablePlayerLevel();

                    if (success)
                    {
                        IsPlayerLevelEnabled = false;
                        StatusMessage = "Player level feature disabled - normal level behavior restored";
                    }
                    else
                    {
                        StatusMessage = "Failed to disable player level feature";
                        MessageBox.Show(
                            "Failed to disable player level feature.",
                            "Disable Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling player level: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while toggling player level:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanApplyPlayerLevel(object? parameter)
        {
            return IsAttached && IsPlayerLevelEnabled && !string.IsNullOrEmpty(PlayerLevelInput);
        }

        private void ApplyPlayerLevel(object? parameter)
        {
            if (!IsAttached || !IsPlayerLevelEnabled)
                return;

            try
            {
                // Parse the level input
                if (!int.TryParse(PlayerLevelInput, out int level) || level < 1 || level > 99)
                {
                    MessageBox.Show(
                        "Please enter a valid level between 1 and 99.",
                        "Invalid Level",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                bool success = _playerLevelService.UpdatePlayerLevel(level);

                if (success)
                {
                    StatusMessage = $"Player level updated to {level}!";
                    MessageBox.Show(
                        $"Player level successfully updated to {level}!\n\n" +
                        "The new level will be applied to players you select in the Team Dock.",
                        "Level Applied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "Failed to update player level";
                    MessageBox.Show(
                        "Failed to update player level.\n\n" +
                        "Make sure the feature is enabled and the game is running.",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying player level: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while applying player level:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanApplyCustomPassives(object? parameter)
        {
            return IsAttached;
        }

        private void RefreshCustomPassives()
        {
            try
            {
                // Read current passives from the tracking array
                string[] currentPassives = _customPassivesService.GetCurrentPassives();

                // Update dropdown selections with current values
                // Only update if a value is actually present (non-empty)
                if (!string.IsNullOrEmpty(currentPassives[0]))
                    CustomPassive1 = currentPassives[0];

                if (!string.IsNullOrEmpty(currentPassives[1]))
                    CustomPassive2 = currentPassives[1];

                if (!string.IsNullOrEmpty(currentPassives[2]))
                    CustomPassive3 = currentPassives[2];

                if (!string.IsNullOrEmpty(currentPassives[3]))
                    CustomPassive4 = currentPassives[3];

                if (!string.IsNullOrEmpty(currentPassives[4]))
                    CustomPassive5 = currentPassives[4];

                StatusMessage = "Current passives loaded";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing passives: {ex.Message}";
            }
        }

        private void ApplyCustomPassives(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                // Collect the 5 passive IDs
                string[] passiveIds = new string[]
                {
                    CustomPassive1 ?? "",
                    CustomPassive2 ?? "",
                    CustomPassive3 ?? "",
                    CustomPassive4 ?? "",
                    CustomPassive5 ?? ""
                };

                // Check if at least one passive is selected
                if (passiveIds.All(string.IsNullOrEmpty))
                {
                    MessageBox.Show(
                        "Please select at least one passive before applying.",
                        "No Passives Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Attach to process if not already attached
                if (!_customPassivesService.IsEnabled)
                {
                    if (!_customPassivesService.AttachToProcess())
                    {
                        throw new Exception("Failed to attach to game process");
                    }
                }

                // Apply custom passives
                bool success = _customPassivesService.ApplyCustomPassives(passiveIds);

                if (success)
                {
                    MessageBox.Show(
                        "Custom passives configured successfully!\n\n" +
                        "Your selected passives will be applied when you:\n" +
                        "• Open the Abilearn Board in the game\n" +
                        "• Hover over a player with passives\n\n" +
                        "Make sure to close and reopen the Abilearn Board to see the changes.",
                        "Custom Passives Applied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    StatusMessage = "Custom passives applied successfully";

                    // Clear dropdown selections after applying
                    CustomPassive1 = "";
                    CustomPassive2 = "";
                    CustomPassive3 = "";
                    CustomPassive4 = "";
                    CustomPassive5 = "";
                }
                else
                {
                    throw new Exception("Failed to apply custom passives");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying custom passives: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while applying custom passives:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ClearCustomPassives(object? parameter)
        {
            try
            {
                // Clear all custom passive selections
                CustomPassive1 = "";
                CustomPassive2 = "";
                CustomPassive3 = "";
                CustomPassive4 = "";
                CustomPassive5 = "";

                // If service is enabled, apply empty passives to clear memory
                if (_customPassivesService.IsEnabled)
                {
                    string[] emptyPassives = new string[] { "", "", "", "", "" };
                    _customPassivesService.ApplyCustomPassives(emptyPassives);
                }

                StatusMessage = "Custom passives cleared";
                MessageBox.Show(
                    "All custom passives have been cleared.\n\nYou can now select new passives for a different player.",
                    "Custom Passives Cleared",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error clearing custom passives: {ex.Message}";
            }
        }

        private void OpenTutorials(object? parameter)
        {
            if (parameter is string featureName)
            {
                _currentTutorialFeature = featureName;
                TutorialsViewModel = new TutorialsViewModel(featureName, () =>
                {
                    // Close action - go back to menu
                    SelectedTool = "menu";
                    TutorialsViewModel = null;
                });
                SelectedTool = "tutorials";
            }
        }

        private bool CanRestartGame(object? parameter)
        {
            return IsAttached;
        }

        private async void RestartGame(object? parameter)
        {
            if (!IsAttached)
                return;

            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to restart the game?\n\n" +
                    "The game process will be terminated and restarted.\n" +
                    "Make sure you have saved your progress!",
                    "Restart Game",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Get the process by name
                    var processes = System.Diagnostics.Process.GetProcessesByName("nie");
                    if (!processes.Any())
                    {
                        StatusMessage = "Game process not found";
                        MessageBox.Show(
                            "Could not find the game process.\n\n" +
                            "Please restart the game manually.",
                            "Process Not Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    var process = processes[0];
                    string? processPath = process.MainModule?.FileName;
                    string? workingDirectory = System.IO.Path.GetDirectoryName(processPath);

                    // Detach first
                    DetachFromProcess(null);

                    // Kill the process
                    process.Kill();
                    process.WaitForExit(5000);

                    // Wait 10 seconds before restarting
                    StatusMessage = "Game terminated. Waiting 10 seconds before restarting...";
                    await System.Threading.Tasks.Task.Delay(10000);

                    // Start a new instance if we have the path
                    if (!string.IsNullOrEmpty(processPath) && System.IO.File.Exists(processPath))
                    {
                        try
                        {
                            var startInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = processPath,
                                WorkingDirectory = workingDirectory,
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(startInfo);
                            StatusMessage = "Game restarted successfully!";
                        }
                        catch (Exception startEx)
                        {
                            StatusMessage = $"Failed to start game: {startEx.Message}";
                            MessageBox.Show(
                                $"The game was closed but failed to restart:\n\n{startEx.Message}\n\n" +
                                $"Game path: {processPath}\n\n" +
                                "Please start the game manually.",
                                "Restart Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        StatusMessage = "Game terminated - please start it manually";
                        MessageBox.Show(
                            $"The game was closed but could not find the executable to restart.\n\n" +
                            $"Path found: {processPath ?? "null"}\n\n" +
                            "Please start the game manually.",
                            "Path Not Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error restarting game: {ex.Message}";
                MessageBox.Show(
                    $"Error occurred while restarting the game:\n\n{ex.Message}\n\n" +
                    "Please restart the game manually.",
                    "Restart Failed",
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
