using System.Collections.ObjectModel;
using System.Windows;
using Velocity.Core;

namespace Velocity.UI;

public sealed class TweakViewModel : ObservableObject
{
    private readonly MainViewModel _owner;
    internal readonly Tweak Tweak;
    private bool _isOn;

    public TweakViewModel(MainViewModel owner, Tweak tweak)
    {
        _owner = owner;
        Tweak = tweak;
    }

    public string Name => Tweak.Name;
    public string Description => Tweak.Description;
    public string RiskLabel => Tweak.Risk switch
    {
        Risk.Safe => "Safe",
        Risk.Moderate => "Moderate",
        _ => "Advanced",
    };
    public bool ShowRisk => Tweak.Risk != Risk.Safe;
    public bool IsAdvanced => Tweak.Risk == Risk.Advanced;
    public string RebootLabel => Tweak.Reboot switch
    {
        RebootNeed.Restart => "Restart required",
        RebootNeed.SignOut => "Sign out to finish",
        _ => "",
    };
    public bool ShowReboot => Tweak.Reboot != RebootNeed.None;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value) return;
            _ = _owner.ToggleTweakAsync(this, value);
        }
    }

    internal void SetState(bool value)
    {
        _isOn = value;
        OnPropertyChanged(nameof(IsOn));
    }
}

public sealed class CategoryGroupViewModel
{
    public required string Category { get; init; }
    public required IReadOnlyList<TweakViewModel> Tweaks { get; init; }
}

public sealed class GameChipViewModel
{
    public required string Name { get; init; }
    public bool IsInstalled { get; init; }
}

public sealed class ProfileViewModel : ObservableObject
{
    private readonly MainViewModel _owner;
    internal readonly GameProfile Profile;
    private int _appliedCount;

    public ProfileViewModel(MainViewModel owner, GameProfile profile)
    {
        _owner = owner;
        Profile = profile;
        ApplyCommand = new RelayCommand(_ => _ = _owner.ApplyProfileAsync(this), _ => !_owner.IsBusy);
        Games = profile.Games
            .Select(g => new GameChipViewModel { Name = g.Name, IsInstalled = GameDetector.IsInstalled(g) })
            .OrderByDescending(g => g.IsInstalled)
            .ToList();
    }

    public string Name => Profile.Name;
    public string Tagline => Profile.Tagline;
    public string IconGlyph => Profile.IconGlyph;
    public IReadOnlyList<GameChipViewModel> Games { get; }
    public bool HasGames => Games.Count > 0;
    public RelayCommand ApplyCommand { get; }

    public int AppliedCount
    {
        get => _appliedCount;
        set
        {
            if (Set(ref _appliedCount, value))
            {
                OnPropertyChanged(nameof(CoverageText));
                OnPropertyChanged(nameof(IsFullyApplied));
            }
        }
    }

    public int TweakCount => Profile.TweakIds.Count;
    public string CoverageText => $"{AppliedCount} of {TweakCount} tweaks active";
    public bool IsFullyApplied => AppliedCount == TweakCount;
}

public sealed class BackupEntryViewModel
{
    public required string TweakId { get; init; }
    public required string Name { get; init; }
    public required string SavedAt { get; init; }
    public required string Detail { get; init; }
    public required RelayCommand RevertCommand { get; init; }
}

public sealed class MainViewModel : ObservableObject
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _isBusy;
    private string _busyText = "";
    private bool _restartRecommended;
    private string _powerPlanName = "—";
    private int _appliedCount;
    private bool _hasBackups;
    private UpdateInfo? _update;

    public MainViewModel()
    {
        var tweakVms = Catalog.Tweaks.Select(t => new TweakViewModel(this, t)).ToList();
        AllTweaks = tweakVms;
        Groups = tweakVms
            .GroupBy(t => t.Tweak.Category)
            .Select(g => new CategoryGroupViewModel { Category = g.Key, Tweaks = g.ToList() })
            .ToList();
        Profiles = Catalog.Profiles.Select(p => new ProfileViewModel(this, p)).ToList();

        RevertAllCommand = new RelayCommand(_ => _ = RevertAllAsync(), _ => !IsBusy && HasBackups);
        ApplyMaxCommand = new RelayCommand(_ =>
        {
            var max = Profiles.FirstOrDefault(p => p.Profile.Id == "max-performance");
            if (max is not null) _ = ApplyProfileAsync(max);
        }, _ => !IsBusy);
        UpdateNowCommand = new RelayCommand(_ => _ = UpdateNowAsync(), _ => UpdateAvailable && !IsBusy);
    }

    public IReadOnlyList<TweakViewModel> AllTweaks { get; }
    public IReadOnlyList<CategoryGroupViewModel> Groups { get; }
    public IReadOnlyList<ProfileViewModel> Profiles { get; }
    public ObservableCollection<BackupEntryViewModel> Backups { get; } = new();

    public RelayCommand RevertAllCommand { get; }
    public RelayCommand ApplyMaxCommand { get; }
    public RelayCommand UpdateNowCommand { get; }

    public bool UpdateAvailable => _update is not null;
    public string UpdateBanner => _update is null
        ? ""
        : $"Velocity {_update.Version} is available — you're on {UpdateService.CurrentVersion.ToString(3)}.";

    public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) OnPropertyChanged(nameof(IsIdle)); } }
    public bool IsIdle => !IsBusy;
    public string BusyText { get => _busyText; private set => Set(ref _busyText, value); }
    public bool RestartRecommended { get => _restartRecommended; private set => Set(ref _restartRecommended, value); }
    public string PowerPlanName { get => _powerPlanName; private set => Set(ref _powerPlanName, value); }
    public bool HasBackups { get => _hasBackups; private set => Set(ref _hasBackups, value); }

    public int AppliedCount
    {
        get => _appliedCount;
        private set
        {
            if (Set(ref _appliedCount, value))
            {
                OnPropertyChanged(nameof(AppliedSummary));
                OnPropertyChanged(nameof(AppliedFraction));
            }
        }
    }

    public int TotalCount => AllTweaks.Count;
    public string AppliedSummary => $"{AppliedCount} of {TotalCount}";
    public double AppliedFraction => TotalCount == 0 ? 0 : (double)AppliedCount / TotalCount;

    // ---------------- operations ----------------

    public async Task InitializeAsync()
    {
        await RunExclusive("Reading current system state…", () =>
        {
            var states = AllTweaks.ToDictionary(t => t, t => SafeIsApplied(t.Tweak));
            return (object)states;
        }, result =>
        {
            foreach (var (vm, on) in (Dictionary<TweakViewModel, bool>)result)
                vm.SetState(on);
        });

        _ = CheckForUpdatesAsync(); // fire-and-forget; never blocks startup
    }

    private async Task CheckForUpdatesAsync()
    {
        var info = await UpdateService.CheckForUpdateAsync();
        if (info is null) return;
        _update = info;
        OnPropertyChanged(nameof(UpdateAvailable));
        OnPropertyChanged(nameof(UpdateBanner));
    }

    public async Task UpdateNowAsync()
    {
        if (_update is null || IsBusy) return;

        var answer = MessageBox.Show(
            Application.Current.MainWindow!,
            $"Update to Velocity {_update.Version}?\n\nThe installer will download (~60 MB), Velocity will close to apply it, and then reopen automatically.",
            "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        if (!await _gate.WaitAsync(0)) return;
        IsBusy = true;
        BusyText = $"Downloading update {_update.Version}…";
        try
        {
            var progress = new Progress<double>(p => BusyText = $"Downloading update {_update.Version}…  {p:P0}");
            var msi = await UpdateService.DownloadAsync(_update, progress);

            BusyText = "Starting installer…";
            var exePath = Environment.ProcessPath ?? "";
            UpdateService.InstallAndRelaunch(msi, exePath);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            IsBusy = false;
            _gate.Release();
            MessageBox.Show(Application.Current.MainWindow!,
                "Could not download the update:\n\n" + ex.Message +
                "\n\nYou can always download it manually from the GitHub releases page.",
                "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public async Task ToggleTweakAsync(TweakViewModel vm, bool enable)
    {
        if (IsBusy) { vm.SetState(!enable); return; }

        if (enable && vm.IsAdvanced)
        {
            var answer = MessageBox.Show(
                Application.Current.MainWindow!,
                $"\"{vm.Name}\" is an advanced tweak:\n\n{vm.Description}\n\nApply it? You can revert at any time.",
                "Advanced tweak", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) { vm.SetState(false); return; }
        }

        var ok = await RunExclusive(enable ? $"Applying {vm.Name}…" : $"Reverting {vm.Name}…", () =>
        {
            var store = BackupStore.Load();
            if (enable) vm.Tweak.Apply(store);
            else vm.Tweak.Revert(store);
            return (object)SafeIsApplied(vm.Tweak);
        }, result =>
        {
            vm.SetState((bool)result);
            if (enable && vm.Tweak.Reboot != RebootNeed.None) RestartRecommended = true;
        });

        if (!ok) vm.SetState(SafeIsApplied(vm.Tweak));
    }

    public async Task ApplyProfileAsync(ProfileViewModel profile)
    {
        var targets = profile.Profile.TweakIds
            .Select(id => AllTweaks.FirstOrDefault(t => t.Tweak.Id == id))
            .Where(t => t is not null)
            .Cast<TweakViewModel>()
            .ToList();

        var advanced = targets.Where(t => t.IsAdvanced && !t.IsOn).ToList();
        bool includeAdvanced = false;
        if (advanced.Count > 0)
        {
            var names = string.Join("\n", advanced.Select(a => "  • " + a.Name));
            var answer = MessageBox.Show(
                Application.Current.MainWindow!,
                $"\"{profile.Name}\" includes advanced tweaks:\n\n{names}\n\nInclude them? Choose No to apply only the safe tweaks.",
                "Advanced tweaks", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return;
            includeAdvanced = answer == MessageBoxResult.Yes;
        }

        await RunExclusive($"Applying {profile.Name} profile…", () =>
        {
            var store = BackupStore.Load();
            var errors = new List<string>();
            foreach (var t in targets)
            {
                if (t.IsAdvanced && !includeAdvanced) continue;
                try
                {
                    if (!t.Tweak.IsApplied()) t.Tweak.Apply(store);
                }
                catch (Exception ex)
                {
                    errors.Add($"{t.Name}: {ex.Message}");
                }
            }
            return (object)errors;
        }, result =>
        {
            foreach (var t in targets) t.SetState(SafeIsApplied(t.Tweak));
            if (targets.Any(t => t.IsOn && t.Tweak.Reboot != RebootNeed.None)) RestartRecommended = true;

            var errors = (List<string>)result;
            if (errors.Count > 0)
                MessageBox.Show(Application.Current.MainWindow!,
                    "Some tweaks could not be applied:\n\n" + string.Join("\n", errors),
                    "Partial success", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    public async Task RevertAllAsync()
    {
        var answer = MessageBox.Show(
            Application.Current.MainWindow!,
            "Restore every setting Velocity has changed back to its original value?",
            "Revert everything", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        await RunExclusive("Restoring original settings…", () =>
        {
            var store = BackupStore.Load();
            foreach (var id in store.Tweaks.Keys.ToList())
                Catalog.Find(id)?.Revert(store);
            return (object)true;
        }, _ =>
        {
            foreach (var t in AllTweaks) t.SetState(SafeIsApplied(t.Tweak));
            RestartRecommended = true;
        });
    }

    public async Task RevertOneAsync(string tweakId)
    {
        var vm = AllTweaks.FirstOrDefault(t => t.Tweak.Id == tweakId);
        if (vm is null) return;
        await ToggleTweakAsync(vm, false);
    }

    // ---------------- helpers ----------------

    private static bool SafeIsApplied(Tweak t)
    {
        try { return t.IsApplied(); }
        catch { return false; }
    }

    /// <summary>Runs work on a background thread under the operation gate, then applies UI updates and refreshes shared state.</summary>
    private async Task<bool> RunExclusive(string busyText, Func<object> work, Action<object> onDone)
    {
        if (!await _gate.WaitAsync(0)) return false;
        IsBusy = true;
        BusyText = busyText;
        try
        {
            var result = await Task.Run(work);
            onDone(result);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(Application.Current.MainWindow!, ex.Message, "Something went wrong",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        finally
        {
            await RefreshSharedStateAsync();
            IsBusy = false;
            _gate.Release();
        }
    }

    private async Task RefreshSharedStateAsync()
    {
        var (planName, store) = await Task.Run(() =>
        {
            PowerPlanTweak.GetActiveScheme(out var name);
            return (name, BackupStore.Load());
        });

        PowerPlanName = planName ?? "Unknown";
        AppliedCount = AllTweaks.Count(t => t.IsOn);
        foreach (var p in Profiles)
            p.AppliedCount = p.Profile.TweakIds.Count(id => AllTweaks.FirstOrDefault(t => t.Tweak.Id == id)?.IsOn == true);

        Backups.Clear();
        foreach (var (id, backup) in store.Tweaks.OrderByDescending(kv => kv.Value.SavedAtUtc))
        {
            var tweak = Catalog.Find(id);
            var parts = new List<string>();
            if (backup.Registry.Count > 0) parts.Add($"{backup.Registry.Count} registry value{(backup.Registry.Count == 1 ? "" : "s")}");
            if (backup.Data.Count > 0) parts.Add("system state");
            Backups.Add(new BackupEntryViewModel
            {
                TweakId = id,
                Name = tweak?.Name ?? id,
                SavedAt = backup.SavedAtUtc.ToLocalTime().ToString("MMM d, yyyy · h:mm tt"),
                Detail = parts.Count > 0 ? string.Join(" + ", parts) + " backed up" : "backed up",
                RevertCommand = new RelayCommand(_ => _ = RevertOneAsync(id), _ => !IsBusy),
            });
        }
        HasBackups = Backups.Count > 0;
    }
}
