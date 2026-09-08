using System;
using HeurePlus.Infrastructure;
using HeurePlus.Models;
using HeurePlus.Services;

namespace HeurePlus.ViewModels;

/// <summary>Fenêtre de connexion à un profil protégé (identifiant + mot de passe).</summary>
public sealed class LoginViewModel : ObservableObject
{
    public LoginViewModel(Profile profile)
    {
        Profile = profile;
        _username = profile.Username;
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
    }

    public event Action<bool>? CloseRequested;

    public Profile Profile { get; }

    public string Heading => $"Connexion · {Profile.Name}";

    private string _username;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private bool _rememberMe;
    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    private string? _error;
    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public RelayCommand CancelCommand { get; }

    public bool TryLogin(string password)
    {
        if (!string.Equals(Username?.Trim(), Profile.Username, StringComparison.OrdinalIgnoreCase))
        {
            Error = "Identifiant incorrect.";
            return false;
        }

        if (!PasswordHasher.Verify(password ?? "", Profile.PasswordHash, Profile.PasswordSalt))
        {
            Error = "Mot de passe incorrect.";
            return false;
        }

        CloseRequested?.Invoke(true);
        return true;
    }
}
