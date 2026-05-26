using System.Collections.ObjectModel;
using System.Windows.Input;
using KodaMate.Helpers;
using KodaMate.Services;

namespace KodaMate.ViewModels;

public sealed class WifiNetworksViewModel : BaseViewModel
{
    private readonly IWifiNetworkService _wifi;

    public ObservableCollection<string> Networks { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand PickNetworkCommand { get; }
    public ICommand CloseCommand { get; }

    public WifiNetworksViewModel(IWifiNetworkService wifi)
    {
        _wifi = wifi;
        Title = "Wi‑Fi";
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        PickNetworkCommand = new AsyncRelayCommand<string>(PickAsync);
        CloseCommand = new AsyncRelayCommand(CloseAsync);
    }

    public override async Task OnAppearingAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var list = await _wifi.GetAvailableSsidsAsync();
            Networks.Clear();
            foreach (var s in list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
                Networks.Add(s);
        }
        catch (Exception ex)
        {
            await PageAlerts.DisplayAlertAsync("Wi‑Fi", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PickAsync(string? ssid)
    {
        if (string.IsNullOrWhiteSpace(ssid)) return;

        var pwd = await PageAlerts.DisplayPromptAsync(
            "Connexion Wi‑Fi",
            $"Mot de passe pour « {ssid} » (simulation — aucune connexion réelle pour l’instant).",
            placeholder: "Mot de passe",
            accept: "Valider",
            cancel: "Annuler");

        if (pwd is null)
            return;

        Preferences.Set("km_wifi_saved_ssid", ssid);
        await PageAlerts.DisplayAlertAsync("Wi‑Fi", $"Réseau « {ssid} » enregistré localement (simulation).");
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (Shell.Current is null) return;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.Navigation.PopModalAsync();
        });
    }
}
