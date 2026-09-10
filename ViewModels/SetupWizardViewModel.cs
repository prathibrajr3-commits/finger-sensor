using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AirGestureAI.Services;
using AirGestureAI.Utilities;

namespace AirGestureAI.ViewModels
{
    // ── Relay Command ─────────────────────────────────────────────────────────

    /// <summary>Lightweight MVVM relay command used by all ViewModels in this assembly.</summary>
    internal sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        /// <summary>Initialises a new <see cref="RelayCommand"/>.</summary>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute    = execute    ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <inheritdoc/>
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        /// <inheritdoc/>
        public void Execute(object? parameter) => _execute(parameter);

        /// <inheritdoc/>
        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>Raises <see cref="CanExecuteChanged"/> manually.</summary>
        public void RaiseCanExecuteChanged() =>
            System.Windows.Application.Current?.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
    }

    // ── Base ViewModel ────────────────────────────────────────────────────────

    /// <summary>Base class providing <see cref="INotifyPropertyChanged"/> for all ViewModels.</summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Raises <see cref="PropertyChanged"/> for <paramref name="name"/>.</summary>
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>Sets <paramref name="field"/> and raises <see cref="PropertyChanged"/> if the value changed.</summary>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    // ── Wizard Step ───────────────────────────────────────────────────────────

    /// <summary>Represents one page in the setup wizard.</summary>
    public sealed class WizardStepViewModel : ViewModelBase
    {
        private string _title    = string.Empty;
        private string _subtitle = string.Empty;
        private bool   _isActive;

        /// <summary>Gets or sets the step title displayed as the page heading.</summary>
        public string Title    { get => _title;    set => SetField(ref _title,    value); }

        /// <summary>Gets or sets the step subtitle / description.</summary>
        public string Subtitle { get => _subtitle; set => SetField(ref _subtitle, value); }

        /// <summary>Gets or sets whether this step is the active page.</summary>
        public bool IsActive   { get => _isActive; set => SetField(ref _isActive, value); }
    }

    // ── Check Item ────────────────────────────────────────────────────────────

    /// <summary>Wraps a <see cref="WizardCheckResult"/> for display in the check list.</summary>
    public sealed class CheckItemViewModel : ViewModelBase
    {
        private string _checkName = string.Empty;
        private string _message   = string.Empty;
        private bool   _passed;
        private bool   _running;

        /// <summary>Gets or sets the name of the environment check.</summary>
        public string CheckName { get => _checkName; set => SetField(ref _checkName, value); }

        /// <summary>Gets or sets the result message.</summary>
        public string Message   { get => _message;   set => SetField(ref _message,   value); }

        /// <summary>Gets or sets whether the check passed.</summary>
        public bool Passed      { get => _passed;     set { SetField(ref _passed, value); OnPropertyChanged(nameof(Icon)); } }

        /// <summary>Gets or sets whether the check is currently running.</summary>
        public bool Running     { get => _running;    set { SetField(ref _running, value); OnPropertyChanged(nameof(Icon)); } }

        /// <summary>Gets the icon character for this check's current state.</summary>
        public string Icon => Running ? "⏳" : Passed ? "✅" : "❌";
    }

    // ── Setup Wizard ViewModel ────────────────────────────────────────────────

    /// <summary>
    /// ViewModel for the multi-step first-run Setup Wizard.
    /// Coordinates environment checks, navigation, and persisting completion state.
    /// </summary>
    public sealed class SetupWizardViewModel : ViewModelBase
    {
        private readonly SetupWizardService _wizardService;

        private int    _currentStepIndex;
        private bool   _isRunningChecks;
        private bool   _checksComplete;
        private string _statusMessage = string.Empty;

        /// <summary>
        /// Initialises the <see cref="SetupWizardViewModel"/>.
        /// </summary>
        /// <param name="wizardService">Service that performs environment checks and persists state.</param>
        public SetupWizardViewModel(SetupWizardService wizardService)
        {
            _wizardService = wizardService ?? throw new ArgumentNullException(nameof(wizardService));

            Steps = new ObservableCollection<WizardStepViewModel>
            {
                new() { Title = "Welcome",       Subtitle = "Control your desktop with air gestures.",        IsActive = true  },
                new() { Title = "Environment",   Subtitle = "Verifying camera, Python and AI dependencies.",  IsActive = false },
                new() { Title = "Calibration",   Subtitle = "Adjust camera position for best tracking.",      IsActive = false },
                new() { Title = "Gestures",      Subtitle = "Learn the standard gesture vocabulary.",         IsActive = false },
                new() { Title = "Complete",      Subtitle = "AirGesture AI is ready to use!",                IsActive = false },
            };

            CheckItems = new ObservableCollection<CheckItemViewModel>();

            NextCommand     = new RelayCommand(_ => GoNext(),     _ => CanGoNext);
            BackCommand     = new RelayCommand(_ => GoBack(),     _ => CurrentStepIndex > 0);
            RunChecksCommand = new RelayCommand(async _ => await RunChecksAsync(), _ => !_isRunningChecks);
            FinishCommand   = new RelayCommand(_ => Finish(),     _ => _checksComplete || CurrentStepIndex == Steps.Count - 1);
        }

        // ── Bindable properties ───────────────────────────────────────────────

        /// <summary>Gets the ordered collection of wizard steps.</summary>
        public ObservableCollection<WizardStepViewModel> Steps { get; }

        /// <summary>Gets the list of environment check items shown on the Environment page.</summary>
        public ObservableCollection<CheckItemViewModel> CheckItems { get; }

        /// <summary>Gets or sets the zero-based index of the currently visible step.</summary>
        public int CurrentStepIndex
        {
            get => _currentStepIndex;
            private set
            {
                if (!SetField(ref _currentStepIndex, value)) return;
                for (int i = 0; i < Steps.Count; i++)
                    Steps[i].IsActive = i == value;
                OnPropertyChanged(nameof(CurrentStep));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(IsLastStep));
            }
        }

        /// <summary>Gets the active step ViewModel.</summary>
        public WizardStepViewModel? CurrentStep =>
            CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

        /// <summary>Gets whether the wizard is on its final step.</summary>
        public bool IsLastStep => CurrentStepIndex == Steps.Count - 1;

        /// <summary>Gets whether the Next button should be enabled.</summary>
        public bool CanGoNext => CurrentStepIndex < Steps.Count - 1;

        /// <summary>Gets or sets the status message shown at the bottom of the environment-check page.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>Advances to the next wizard step.</summary>
        public ICommand NextCommand { get; }

        /// <summary>Returns to the previous wizard step.</summary>
        public ICommand BackCommand { get; }

        /// <summary>Triggers the asynchronous environment pre-check suite.</summary>
        public ICommand RunChecksCommand { get; }

        /// <summary>Marks the wizard complete and closes the window.</summary>
        public ICommand FinishCommand { get; }

        /// <summary>Raised when the wizard has been completed and the window should close.</summary>
        public event Action? WizardCompleted;

        // ── Navigation ────────────────────────────────────────────────────────

        private void GoNext()
        {
            if (CurrentStepIndex < Steps.Count - 1)
                CurrentStepIndex++;
        }

        private void GoBack()
        {
            if (CurrentStepIndex > 0)
                CurrentStepIndex--;
        }

        private async Task RunChecksAsync()
        {
            _isRunningChecks = true;
            StatusMessage    = "Running environment checks…";
            CheckItems.Clear();

            // Populate placeholder items
            var names = new[] { "Camera", "Python", "MediaPipe", "ONNX Models", "GPU Provider" };
            foreach (var n in names)
                CheckItems.Add(new CheckItemViewModel { CheckName = n, Running = true, Message = "Checking…" });

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var report = await _wizardService.RunChecksAsync(cts.Token);

                CheckItems.Clear();
                foreach (var r in report.Results)
                    CheckItems.Add(new CheckItemViewModel
                    {
                        CheckName = r.CheckName,
                        Passed    = r.Passed,
                        Running   = false,
                        Message   = r.Message
                    });

                _checksComplete = true;
                StatusMessage   = report.AllPassed
                    ? "All checks passed. You can continue."
                    : "Some checks failed — see details above. You may still continue.";
            }
            catch (Exception ex)
            {
                Logger.Error("SetupWizardViewModel: RunChecksAsync failed", ex);
                StatusMessage = $"Check failed: {ex.Message}";
            }
            finally
            {
                _isRunningChecks = false;
                ((RelayCommand)RunChecksCommand).RaiseCanExecuteChanged();
                ((RelayCommand)FinishCommand).RaiseCanExecuteChanged();
            }
        }

        private void Finish()
        {
            try
            {
                _wizardService.MarkComplete();
            }
            catch (Exception ex)
            {
                Logger.Error("SetupWizardViewModel: Finish failed", ex);
            }
            WizardCompleted?.Invoke();
        }
    }
}
