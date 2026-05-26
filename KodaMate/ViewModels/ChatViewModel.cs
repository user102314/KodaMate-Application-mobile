using System.Collections.ObjectModel;
using System.Windows.Input;
using KodaMate.Models;
using KodaMate.Services;

namespace KodaMate.ViewModels;

/// <summary>
/// ViewModel for the Chat AI page.
/// Envoie les messages via IDistributeurService.SendToN8nAsync → workflow n8n → réponse IA.
/// </summary>
public class ChatViewModel : BaseViewModel
{
    private readonly IDistributeurService _distributeurService;
    private readonly IWifiModalService _wifiModal;
    private string _messageText = string.Empty;
    private bool _isTyping;
    private Color _wifiIndicatorColor = Color.FromArgb("#E53935");

    public Color WifiIndicatorColor
    {
        get => _wifiIndicatorColor;
        set => SetProperty(ref _wifiIndicatorColor, value);
    }

    public ICommand OpenWifiNetworksCommand { get; }

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public string MessageText
    {
        get => _messageText;
        set => SetProperty(ref _messageText, value);
    }

    public bool IsTyping
    {
        get => _isTyping;
        set => SetProperty(ref _isTyping, value);
    }

    public ICommand SendMessageCommand      { get; }
    public ICommand TakePhotoCommand        { get; }
    public ICommand ClearConversationCommand { get; }

    public ChatViewModel(IDistributeurService distributeurService, IWifiModalService wifiModal)
    {
        _distributeurService = distributeurService;
        _wifiModal = wifiModal;
        Title = "Chat avec Koda";

        SendMessageCommand       = new AsyncRelayCommand(SendMessageAsync,
            () => !string.IsNullOrWhiteSpace(MessageText));
        TakePhotoCommand         = new AsyncRelayCommand(TakePhotoAsync);
        ClearConversationCommand = new RelayCommand(ClearConversation);
        OpenWifiNetworksCommand  = new AsyncRelayCommand(OpenWifiNetworksAsync);

        // Message de bienvenue
        Messages.Add(new ChatMessage
        {
            Content       = "Bonjour ! Je suis Koda, votre assistant intelligent. Comment puis-je vous aider ? 🤖",
            IsUserMessage = false,
            Timestamp     = DateTime.Now
        });
    }

    // ──────────────────────────────────────────────────────
    //  SEND → POST /api/trigger-n8n
    // ──────────────────────────────────────────────────────
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText)) return;

        var userText = MessageText.Trim();
        MessageText = string.Empty;

        // Afficher le message de l'utilisateur immédiatement
        Messages.Add(new ChatMessage
        {
            Content       = userText,
            IsUserMessage = true,
            Timestamp     = DateTime.Now
        });

        try
        {
            IsTyping = true;

            // Appel backend → n8n workflow
            var reply = await _distributeurService.SendToN8nAsync(userText);

            Messages.Add(new ChatMessage
            {
                Content       = string.IsNullOrWhiteSpace(reply)
                                    ? "Je n'ai pas pu générer une réponse. Réessayez."
                                    : reply,
                IsUserMessage = false,
                Timestamp     = DateTime.Now
            });
        }
        catch (HttpRequestException)
        {
            Messages.Add(new ChatMessage
            {
                Content       = "⚠️ Backend inaccessible. Vérifiez la connexion réseau et l'adresse IP dans AppConfig.",
                IsUserMessage = false,
                Timestamp     = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Content       = $"Erreur : {ex.Message}",
                IsUserMessage = false,
                Timestamp     = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
        }
    }

    // ──────────────────────────────────────────────────────
    //  PHOTO (conservé, envoie aussi via n8n)
    // ──────────────────────────────────────────────────────
    private async Task TakePhotoAsync()
    {
        try
        {
            var result = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Prendre une photo pour Koda"
            });

            if (result != null)
            {
                Messages.Add(new ChatMessage
                {
                    Content       = "📷 [Image jointe]",
                    IsUserMessage = true,
                    ImagePath     = result.FullPath,
                    Timestamp     = DateTime.Now
                });

                IsTyping = true;
                // On envoie une description textuelle pour que n8n traite
                var reply = await _distributeurService.SendToN8nAsync(
                    "L'utilisateur a envoyé une image. Analyze et réponds.");

                Messages.Add(new ChatMessage
                {
                    Content       = reply,
                    IsUserMessage = false,
                    Timestamp     = DateTime.Now
                });
            }
        }
        catch (FeatureNotSupportedException)
        {
            await Shell.Current.DisplayAlert("Erreur", "Caméra non disponible sur cet appareil.", "OK");
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlert("Permission", "L'accès à la caméra est requis.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Erreur", ex.Message, "OK");
        }
        finally
        {
            IsTyping = false;
        }
    }

    private void ClearConversation()
    {
        Messages.Clear();
        Messages.Add(new ChatMessage
        {
            Content       = "Conversation effacée. Comment puis-je vous aider ? 🤖",
            IsUserMessage = false,
            Timestamp     = DateTime.Now
        });
    }

    public override async Task OnAppearingAsync()
    {
        UpdateWifiIndicator();
        await Task.CompletedTask;
    }

    public override Task OnDisappearingAsync() => base.OnDisappearingAsync();

    private void UpdateWifiIndicator()
    {
        WifiIndicatorColor = string.IsNullOrEmpty(Preferences.Get("km_wifi_saved_ssid", ""))
            ? Color.FromArgb("#E53935")
            : Color.FromArgb("#43A047");
    }

    private async Task OpenWifiNetworksAsync()
    {
        await _wifiModal.ShowWifiNetworksAsync(() =>
            MainThread.BeginInvokeOnMainThread(UpdateWifiIndicator));
    }
}
