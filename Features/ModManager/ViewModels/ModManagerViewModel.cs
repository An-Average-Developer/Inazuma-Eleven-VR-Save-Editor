using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using InazumaElevenVRSaveEditor.Common.Infrastructure;
using InazumaElevenVRSaveEditor.Features.ModManager.Models;
using InazumaElevenVRSaveEditor.Features.ModManager.Services;
using InazumaElevenVRSaveEditor.Configuration;
using Microsoft.Win32;

namespace InazumaElevenVRSaveEditor.Features.ModManager.ViewModels
{
    public class ModManagerViewModel : ViewModelBase
    {
        private ModManagerService? _modManagerService;
        private ObservableCollection<ModInfo> _mods = new();
        private ModInfo? _selectedMod;
        private string _statusMessage = "Ready";
        private bool _isLoading;
        private string _searchText = string.Empty;
        private bool _isPriorityMode;
        private bool _isGameRunning;
        private readonly DispatcherTimer _updateCheckTimer;
        private const string GAME_PROCESS_NAME = "ASRT-Win64-Shipping";
        private int _updateCheckCounter = 0;
        private const int UPDATE_CHECK_INTERVAL = 60;
        private bool _isInitialized = false;

        public ModManagerViewModel()
        {
            RefreshModsCommand = new RelayCommand(async () => await RefreshModsAsync());
            ToggleModCommand = new RelayCommand(async () => await ToggleSelectedModAsync(), () => SelectedMod != null && !IsGameRunning);
            EnableAllModsCommand = new RelayCommand(async () => await EnableAllModsAsync(), () => Mods.Any(m => !m.IsEnabled) && !IsGameRunning);
            DisableAllModsCommand = new RelayCommand(async () => await DisableAllModsAsync(), () => Mods.Any(m => m.IsEnabled) && !IsGameRunning);
            OpenModsFolderCommand = new RelayCommand(() => OpenModsFolder());
            SelectModsFolderCommand = new RelayCommand(async () => await SelectModsFolderAsync());
            CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
            ShowUpdateDialogCommand = new RelayCommand<ModInfo>(async (mod) => await ShowUpdateDialogAsync(mod), (mod) => !IsGameRunning);
            TogglePriorityModeCommand = new RelayCommand(() => TogglePriorityMode(), () => !IsGameRunning);
            MoveModUpCommand = new RelayCommand(() => MoveModUp(), () => SelectedMod != null && IsPriorityMode && !IsGameRunning);
            MoveModDownCommand = new RelayCommand(() => MoveModDown(), () => SelectedMod != null && IsPriorityMode && !IsGameRunning);

            _updateCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateCheckTimer.Tick += UpdateCheckTimer_Tick;
            _updateCheckTimer.Start();
        }

        public ObservableCollection<ModInfo> Mods
        {
            get => _mods;
            set => SetProperty(ref _mods, value);
        }

        public ModInfo? SelectedMod
        {
            get => _selectedMod;
            set
            {
                SetProperty(ref _selectedMod, value);
                ((RelayCommand)ToggleModCommand).RaiseCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                FilterMods();
            }
        }

        public bool ModsDirectoryExists => _modManagerService?.ModsDirectoryExists() ?? false;

        public string ModsDirectory => _modManagerService?.ModsDirectory ?? string.Empty;

        public int EnabledModCount => Mods.Count(m => m.IsEnabled);

        public int DisabledModCount => Mods.Count(m => !m.IsEnabled);

        public int TotalModCount => Mods.Count;

        public bool IsPriorityMode
        {
            get => _isPriorityMode;
            set
            {
                SetProperty(ref _isPriorityMode, value);
                ((RelayCommand)MoveModUpCommand).RaiseCanExecuteChanged();
                ((RelayCommand)MoveModDownCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsGameRunning
        {
            get => _isGameRunning;
            set
            {
                if (SetProperty(ref _isGameRunning, value))
                {

                    ((RelayCommand)ToggleModCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)EnableAllModsCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DisableAllModsCommand).RaiseCanExecuteChanged();
                    ((RelayCommand<ModInfo>)ShowUpdateDialogCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)TogglePriorityModeCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)MoveModUpCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)MoveModDownCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand RefreshModsCommand { get; }
        public ICommand ToggleModCommand { get; }
        public ICommand EnableAllModsCommand { get; }
        public ICommand DisableAllModsCommand { get; }
        public ICommand OpenModsFolderCommand { get; }
        public ICommand SelectModsFolderCommand { get; }
        public ICommand CheckForUpdatesCommand { get; }
        public ICommand ShowUpdateDialogCommand { get; }
        public ICommand TogglePriorityModeCommand { get; }
        public ICommand MoveModUpCommand { get; }
        public ICommand MoveModDownCommand { get; }

        public async Task EnsureInitializedAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;

            var modsDirectory = SettingsService.GetModsDirectory();

            if (string.IsNullOrEmpty(modsDirectory))
            {
                modsDirectory = string.Empty;
            }

            _modManagerService = new ModManagerService(modsDirectory);

            await RefreshModsAsync();

            _ = CheckForUpdatesAsync();
        }

        private void UpdateCheckTimer_Tick(object? sender, EventArgs e)
        {
            CheckGameRunning();

            _updateCheckCounter++;
            if (_updateCheckCounter >= UPDATE_CHECK_INTERVAL)
            {
                _updateCheckCounter = 0;
                _ = CheckForUpdatesAsync();
            }
        }

        private void CheckGameRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(GAME_PROCESS_NAME);
                bool wasRunning = IsGameRunning;
                IsGameRunning = processes.Length > 0;

                if (IsGameRunning && !wasRunning)
                {
                    StatusMessage = "Game is running - mod operations are disabled";
                }
                else if (!IsGameRunning && wasRunning)
                {
                    StatusMessage = "Game closed - mod operations are enabled";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking game process: {ex.Message}");
            }
        }

        private bool ShowGameRunningWarning()
        {
            if (IsGameRunning)
            {
                MessageBox.Show(
                    "The game must be closed before you can make changes to mods.\n\nPlease close the game and try again.",
                    "Game is Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return true;
            }
            return false;
        }

        private async Task RefreshModsAsync()
        {
            if (_modManagerService == null)
            {
                StatusMessage = "Mod manager not initialized";
                return;
            }

            IsLoading = true;
            StatusMessage = "Scanning for mods...";

            try
            {
                await Task.Run(() =>
                {
                    var mods = _modManagerService.ScanForMods();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Mods.Clear();
                        foreach (var mod in mods)
                        {
                            Mods.Add(mod);
                        }
                    });
                });

                IsPriorityMode = _modManagerService.IsPriorityModeEnabled(Mods.ToList());

                UpdateModCounts();

                StatusMessage = Mods.Any()
                    ? $"Found {TotalModCount} mod(s) - {EnabledModCount} enabled, {DisabledModCount} disabled"
                    : ModsDirectoryExists
                        ? "No mods found in directory"
                        : "Mods directory not found";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to scan for mods:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ToggleSelectedModAsync()
        {
            if (SelectedMod == null || _modManagerService == null)
                return;

            if (ShowGameRunningWarning())
                return;

            IsLoading = true;
            var modName = SelectedMod.Name;
            var wasEnabled = SelectedMod.IsEnabled;

            try
            {
                await Task.Run(() => _modManagerService.ToggleMod(SelectedMod));

                StatusMessage = wasEnabled
                    ? $"Disabled: {modName}"
                    : $"Enabled: {modName}";

                UpdateModCounts();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to toggle mod:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task EnableAllModsAsync()
        {
            if (_modManagerService == null)
                return;

            if (ShowGameRunningWarning())
                return;

            var result = MessageBox.Show(
                "Are you sure you want to enable all mods?",
                "Confirm Enable All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsLoading = true;
            StatusMessage = "Enabling all mods...";

            try
            {
                var disabledMods = Mods.Where(m => !m.IsEnabled).ToList();
                int count = 0;

                await Task.Run(() =>
                {
                    foreach (var mod in disabledMods)
                    {
                        _modManagerService.EnableMod(mod);
                        count++;
                    }
                });

                UpdateModCounts();
                StatusMessage = $"Enabled {count} mod(s)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to enable all mods:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DisableAllModsAsync()
        {
            if (_modManagerService == null)
                return;

            if (ShowGameRunningWarning())
                return;

            var result = MessageBox.Show(
                "Are you sure you want to disable all mods?",
                "Confirm Disable All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsLoading = true;
            StatusMessage = "Disabling all mods...";

            try
            {
                var enabledMods = Mods.Where(m => m.IsEnabled).ToList();
                int count = 0;

                await Task.Run(() =>
                {
                    foreach (var mod in enabledMods)
                    {
                        _modManagerService.DisableMod(mod);
                        count++;
                    }
                });

                UpdateModCounts();
                StatusMessage = $"Disabled {count} mod(s)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to disable all mods:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OpenModsFolder()
        {
            if (_modManagerService == null)
                return;

            try
            {
                _modManagerService.OpenModsFolder();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open mods folder:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SelectModsFolderAsync()
        {
            try
            {
                var selectedFolder = PromptForModsDirectory();

                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    SettingsService.SaveModsDirectory(selectedFolder);

                    _modManagerService = new ModManagerService(selectedFolder);

                    await RefreshModsAsync();

                    StatusMessage = "Mods folder updated successfully";
                    MessageBox.Show($"Mods folder set to:\n{selectedFolder}\n\nMods have been refreshed.",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to set mods folder:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterMods()
        {
        }

        private void UpdateModCounts()
        {
            OnPropertyChanged(nameof(EnabledModCount));
            OnPropertyChanged(nameof(DisabledModCount));
            OnPropertyChanged(nameof(TotalModCount));
            ((RelayCommand)EnableAllModsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DisableAllModsCommand).RaiseCanExecuteChanged();
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_modManagerService == null)
                return;

            IsLoading = true;
            StatusMessage = "Checking for mod updates...";

            try
            {
                var modsWithUrls = Mods.Where(m => !string.IsNullOrEmpty(m.ModPageUrl)).ToList();

                if (!modsWithUrls.Any())
                {
                    StatusMessage = "No mods with update URLs found";
                    return;
                }

                await _modManagerService.CheckForUpdatesAsync(modsWithUrls.ToList());

                var modsWithUpdates = modsWithUrls.Count(m => m.HasUpdate);
                StatusMessage = modsWithUpdates > 0
                    ? $"Found {modsWithUpdates} mod update(s) available"
                    : "All mods are up to date";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error checking for updates: {ex.Message}";
                MessageBox.Show($"Failed to check for updates:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ShowUpdateDialogAsync(ModInfo? mod)
        {
            if (mod == null || !mod.HasUpdate || _modManagerService == null)
                return;

            if (ShowGameRunningWarning())
                return;

            try
            {
                var dialogViewModel = new ModUpdateDialogViewModel(_modManagerService, mod);
                var dialog = new Views.ModUpdateDialog(dialogViewModel);

                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    dialog.Owner = mainWindow;
                }

                var result = dialog.ShowDialog();

                if (result == true)
                {
                    StatusMessage = $"Successfully updated {mod.Name} to version {mod.LatestVersion}";

                    await RefreshModsAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating mod: {ex.Message}";
                MessageBox.Show($"Failed to update {mod.Name}:\n{ex.Message}", "Update Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TogglePriorityMode()
        {
            if (_modManagerService == null)
                return;

            if (ShowGameRunningWarning())
                return;

            try
            {
                if (IsPriorityMode)
                {
                    var result = MessageBox.Show(
                        "This will remove priority numbers from mod folder names. Continue?",
                        "Disable Priority Mode",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _modManagerService.DisablePriorityMode(Mods.ToList());
                        IsPriorityMode = false;
                        StatusMessage = "Priority mode disabled";
                        _ = RefreshModsAsync();
                    }
                }
                else
                {
                    var result = MessageBox.Show(
                        "This will add priority numbers to mod folder names (e.g., \"01 - ModName\"). Continue?",
                        "Enable Priority Mode",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _modManagerService.EnablePriorityMode(Mods.ToList());
                        IsPriorityMode = true;
                        StatusMessage = "Priority mode enabled - use arrows to reorder mods";
                        _ = RefreshModsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to toggle priority mode:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MoveModUp()
        {
            if (SelectedMod == null || !IsPriorityMode || _modManagerService == null)
                return;

            if (ShowGameRunningWarning())
                return;

            var index = Mods.IndexOf(SelectedMod);
            if (index > 0)
            {
                try
                {
                    Mods.Move(index, index - 1);

                    _modManagerService.ReorderMods(Mods.ToList());

                    StatusMessage = $"Moved {SelectedMod.Name} up";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to move mod:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    _ = RefreshModsAsync();
                }
            }
        }

        private void MoveModDown()
        {
            if (SelectedMod == null || !IsPriorityMode || _modManagerService == null)
                return;

            if (ShowGameRunningWarning())
                return;

            var index = Mods.IndexOf(SelectedMod);
            if (index < Mods.Count - 1)
            {
                try
                {
                    Mods.Move(index, index + 1);

                    _modManagerService.ReorderMods(Mods.ToList());

                    StatusMessage = $"Moved {SelectedMod.Name} down";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to move mod:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    _ = RefreshModsAsync();
                }
            }
        }

        public void HandleDrop(ModInfo droppedMod, ModInfo targetMod)
        {
            if (!IsPriorityMode || droppedMod == null || targetMod == null || _modManagerService == null)
                return;

            if (ShowGameRunningWarning())
                return;

            try
            {
                var droppedIndex = Mods.IndexOf(droppedMod);
                var targetIndex = Mods.IndexOf(targetMod);

                if (droppedIndex < 0 || targetIndex < 0 || droppedIndex == targetIndex)
                    return;

                Mods.Move(droppedIndex, targetIndex);

                _modManagerService.ReorderMods(Mods.ToList());

                StatusMessage = $"Moved {droppedMod.Name} to position {targetIndex + 1}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to reorder mods:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _ = RefreshModsAsync();
            }
        }

        private string? PromptForModsDirectory()
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Select Mods Folder",
                    Multiselect = false
                };

                var result = dialog.ShowDialog();
                if (result == true)
                {
                    return dialog.FolderName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting folder:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }
    }
}
