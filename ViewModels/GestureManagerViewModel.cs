using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AirGestureAI.Services;
using AirGestureAI.Utilities;

namespace AirGestureAI.ViewModels
{
    // ── Gesture Binding ViewModel ─────────────────────────────────────────────

    /// <summary>Observable wrapper around a <see cref="GestureBinding"/> for list display and editing.</summary>
    public sealed class GestureBindingViewModel : ViewModelBase
    {
        private readonly GestureBinding _model;

        /// <summary>Initialises the wrapper from a <see cref="GestureBinding"/> model.</summary>
        public GestureBindingViewModel(GestureBinding model) =>
            _model = model ?? throw new ArgumentNullException(nameof(model));

        /// <summary>Gets the gesture name string.</summary>
        public string GestureName => _model.Gesture.ToString();

        /// <summary>Gets the emoji icon for this gesture.</summary>
        public string GestureIcon => _model.Gesture switch
        {
            Models.GestureType.ScrollUp   => "☝️",
            Models.GestureType.ScrollDown => "👇",
            Models.GestureType.OpenPalm  => "🖐️",
            _                             => "✋"
        };

        /// <summary>Gets or sets whether this gesture is enabled.</summary>
        public bool IsEnabled
        {
            get => _model.IsEnabled;
            set { _model.IsEnabled = value; OnPropertyChanged(); }
        }

        /// <summary>Gets or sets the sensitivity multiplier (0.1 – 2.0).</summary>
        public double Sensitivity
        {
            get => _model.Sensitivity;
            set { _model.Sensitivity = Math.Clamp(value, 0.1, 2.0); OnPropertyChanged(); }
        }

        /// <summary>Gets or sets the assigned action display string.</summary>
        public string ActionDisplay
        {
            get
            {
                if (_model.Action.Type == GestureActionType.None) return "(none)";
                return $"{_model.Action.Type}: {_model.Action.Parameter}";
            }
            set
            {
                // Simple parameter update (action type unchanged)
                _model.Action.Parameter = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Gets the underlying model for persistence.</summary>
        public GestureBinding Model => _model;
    }

    // ── Profile ViewModel ─────────────────────────────────────────────────────

    /// <summary>Observable wrapper for a <see cref="GestureProfile"/> name in the profile list.</summary>
    public sealed class GestureProfileViewModel : ViewModelBase
    {
        private GestureProfile _model;
        private bool           _isSelected;

        /// <summary>Initialises the wrapper from a <see cref="GestureProfile"/> model.</summary>
        public GestureProfileViewModel(GestureProfile model) =>
            _model = model ?? throw new ArgumentNullException(nameof(model));

        /// <summary>Gets or sets the profile name.</summary>
        public string Name
        {
            get => _model.Name;
            set { _model.Name = value; OnPropertyChanged(); }
        }

        /// <summary>Gets the unique profile identifier.</summary>
        public string Id => _model.Id;

        /// <summary>Gets or sets whether this profile is selected in the list.</summary>
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        /// <summary>Gets the underlying model.</summary>
        public GestureProfile Model => _model;
    }

    // ── Gesture Manager ViewModel ─────────────────────────────────────────────

    /// <summary>
    /// ViewModel for the Gesture Manager page.
    /// Allows users to view, enable/disable, tune sensitivity, assign actions,
    /// import/export profiles, and reset to factory defaults.
    /// </summary>
    public sealed class GestureManagerViewModel : ViewModelBase
    {
        private readonly GestureProfileService _profileService;

        private GestureProfileViewModel? _selectedProfile;
        private GestureBindingViewModel? _selectedBinding;
        private bool                     _isBusy;
        private string                   _statusMessage = string.Empty;

        /// <summary>Initialises the <see cref="GestureManagerViewModel"/>.</summary>
        /// <param name="profileService">Service managing gesture profile persistence.</param>
        public GestureManagerViewModel(GestureProfileService profileService)
        {
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));

            Profiles = new ObservableCollection<GestureProfileViewModel>();
            Bindings = new ObservableCollection<GestureBindingViewModel>();

            NewProfileCommand     = new RelayCommand(_ => CreateProfile());
            DeleteProfileCommand  = new RelayCommand(_ => DeleteProfile(),  _ => SelectedProfile is not null);
            SaveCommand           = new RelayCommand(async _ => await SaveAsync(), _ => SelectedProfile is not null && !_isBusy);
            ResetDefaultsCommand  = new RelayCommand(async _ => await ResetDefaultsAsync());
            ExportCommand         = new RelayCommand(async _ => await ExportAsync(), _ => SelectedProfile is not null);
            ImportCommand         = new RelayCommand(async _ => await ImportAsync());

            // Load profiles when VM is first constructed
            _ = LoadProfilesAsync();
        }

        // ── Bindable Properties ───────────────────────────────────────────────

        /// <summary>Gets the collection of available gesture profiles.</summary>
        public ObservableCollection<GestureProfileViewModel> Profiles { get; }

        /// <summary>Gets the gesture bindings for the currently selected profile.</summary>
        public ObservableCollection<GestureBindingViewModel> Bindings { get; }

        /// <summary>Gets or sets the currently selected profile in the list.</summary>
        public GestureProfileViewModel? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                SetField(ref _selectedProfile, value);
                RefreshBindings();
            }
        }

        /// <summary>Gets or sets the binding currently selected in the detail editor.</summary>
        public GestureBindingViewModel? SelectedBinding
        {
            get => _selectedBinding;
            set => SetField(ref _selectedBinding, value);
        }

        /// <summary>Gets or sets whether a long-running operation is in progress.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetField(ref _isBusy, value);
        }

        /// <summary>Gets or sets the feedback message displayed at the bottom of the page.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>Creates a new empty gesture profile.</summary>
        public ICommand NewProfileCommand     { get; }

        /// <summary>Deletes the currently selected profile.</summary>
        public ICommand DeleteProfileCommand  { get; }

        /// <summary>Saves the current profile to disk.</summary>
        public ICommand SaveCommand           { get; }

        /// <summary>Resets the selected profile to factory defaults.</summary>
        public ICommand ResetDefaultsCommand  { get; }

        /// <summary>Exports the selected profile to a user-chosen JSON file.</summary>
        public ICommand ExportCommand         { get; }

        /// <summary>Imports a gesture profile from a JSON file.</summary>
        public ICommand ImportCommand         { get; }

        // ── Implementation ────────────────────────────────────────────────────

        private async Task LoadProfilesAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading profiles…";
            try
            {
                var profiles = await _profileService.LoadAllProfilesAsync();
                Profiles.Clear();

                if (profiles.Count == 0)
                {
                    // Seed the default profile on first use
                    var def = _profileService.BuildDefaultProfile();
                    await _profileService.SaveProfileAsync(def);
                    profiles.Add(def);
                }

                foreach (var p in profiles)
                    Profiles.Add(new GestureProfileViewModel(p));

                SelectedProfile = Profiles.FirstOrDefault();
                StatusMessage   = $"Loaded {Profiles.Count} profile(s).";
            }
            catch (Exception ex)
            {
                Logger.Error("GestureManagerViewModel: LoadProfilesAsync failed", ex);
                StatusMessage = $"Error loading profiles: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RefreshBindings()
        {
            Bindings.Clear();
            if (_selectedProfile is null) return;
            foreach (var b in _selectedProfile.Model.Bindings)
                Bindings.Add(new GestureBindingViewModel(b));
        }

        private void CreateProfile()
        {
            var def = _profileService.BuildDefaultProfile();
            def.Name = $"Profile {Profiles.Count + 1}";
            var vm = new GestureProfileViewModel(def);
            Profiles.Add(vm);
            SelectedProfile = vm;
            StatusMessage   = $"Created '{def.Name}'. Click Save to persist.";
        }

        private void DeleteProfile()
        {
            if (_selectedProfile is null) return;
            _profileService.DeleteProfile(_selectedProfile.Id);
            Profiles.Remove(_selectedProfile);
            SelectedProfile = Profiles.FirstOrDefault();
            StatusMessage   = "Profile deleted.";
        }

        private async Task SaveAsync()
        {
            if (_selectedProfile is null) return;
            IsBusy = true;
            StatusMessage = "Saving…";
            try
            {
                // Write edited bindings back to the model
                _selectedProfile.Model.Bindings.Clear();
                foreach (var bvm in Bindings)
                    _selectedProfile.Model.Bindings.Add(bvm.Model);

                await _profileService.SaveProfileAsync(_selectedProfile.Model);
                StatusMessage = "Profile saved.";
            }
            catch (Exception ex)
            {
                Logger.Error("GestureManagerViewModel: SaveAsync failed", ex);
                StatusMessage = $"Save failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ResetDefaultsAsync()
        {
            if (_selectedProfile is null) return;
            IsBusy = true;
            try
            {
                var def = _profileService.BuildDefaultProfile();
                def.Id   = _selectedProfile.Id;
                def.Name = _selectedProfile.Name;
                _selectedProfile.Model.Bindings.Clear();
                _selectedProfile.Model.Bindings.AddRange(def.Bindings);
                await _profileService.SaveProfileAsync(_selectedProfile.Model);
                RefreshBindings();
                StatusMessage = "Profile reset to defaults.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExportAsync()
        {
            if (_selectedProfile is null) return;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Export Gesture Profile",
                Filter     = "JSON Profile|*.json",
                FileName   = $"{_selectedProfile.Name}.json",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog() == true)
            {
                await _profileService.ExportProfileAsync(_selectedProfile.Model, dlg.FileName);
                StatusMessage = $"Exported to {dlg.FileName}";
            }
        }

        private async Task ImportAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Import Gesture Profile",
                Filter = "JSON Profile|*.json"
            };
            if (dlg.ShowDialog() == true)
            {
                IsBusy = true;
                StatusMessage = "Importing…";
                try
                {
                    var profile = await _profileService.ImportProfileAsync(dlg.FileName);
                    var vm      = new GestureProfileViewModel(profile);
                    Profiles.Add(vm);
                    SelectedProfile = vm;
                    StatusMessage   = $"Imported '{profile.Name}'.";
                }
                catch (Exception ex)
                {
                    Logger.Error("GestureManagerViewModel: ImportAsync failed", ex);
                    StatusMessage = $"Import failed: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}
