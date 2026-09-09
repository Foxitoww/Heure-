using System;
using System.Collections.Generic;
using System.Windows;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;
using HeurePlus.Services;

namespace HeurePlus.ViewModels;

/// <summary>Mode d'ouverture de la fenêtre d'édition de profil.</summary>
public enum ProfileEditMode
{
    /// <summary>Nouveau profil.</summary>
    Create,

    /// <summary>Modification d'un profil existant.</summary>
    Edit,

    /// <summary>Profil hérité sans identifiants : première définition du mot de passe.</summary>
    SetupExisting
}

/// <summary>Fenêtre de création / édition d'un profil du foyer.</summary>
public sealed class ProfileEditViewModel : ObservableObject
{
    private readonly ProfileStore _store;
    private readonly Profile? _existing;

    public ProfileEditViewModel(ProfileStore store, ProfileEditMode mode, Profile? existing, bool allowPasswordChange = false)
    {
        _store = store;
        _existing = existing;
        Mode = mode;
        AllowPasswordChange = allowPasswordChange;

        _name = existing?.Name ?? "";
        _username = existing?.Username ?? "";
        _colorHex = existing?.ColorHex ?? store.SuggestColor();

        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        PickColorCommand = new RelayCommand(p =>
        {
            if (p is string hex) ColorHex = hex;
        });
        DeleteCommand = new RelayCommand(_ => Delete(), _ => CanDelete);
    }

    public event Action<bool>? CloseRequested;

    public Profile? SavedProfile { get; private set; }

    public ProfileEditMode Mode { get; }

    public bool IsEditMode => Mode == ProfileEditMode.Edit;

    public string Heading => Mode switch
    {
        ProfileEditMode.Create => "Nouveau profil",
        ProfileEditMode.SetupExisting => "Protège ton profil",
        _ => "Modifier le profil"
    };

    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _username;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _colorHex;
    public string ColorHex
    {
        get => _colorHex;
        set => SetProperty(ref _colorHex, value);
    }

    public IReadOnlyList<string> Palette => ProfileStore.Palette;

    public bool AlwaysAskPassword => Mode is ProfileEditMode.Create or ProfileEditMode.SetupExisting;

    private bool _changePassword;
    public bool ChangePassword
    {
        get => _changePassword;
        set
        {
            if (SetProperty(ref _changePassword, value))
                OnPropertyChanged(nameof(ShowPasswordFields));
        }
    }

    public bool ShowPasswordFields => AlwaysAskPassword || ChangePassword;

    /// <summary>Le changement de mot de passe rechiffre la base : possible seulement
    /// pour le profil actif (base ouverte), donc depuis Réglages, pas le sélecteur.</summary>
    public bool AllowPasswordChange { get; }

    public bool CanChangePassword => Mode == ProfileEditMode.Edit && AllowPasswordChange;

    public bool CanDelete => Mode == ProfileEditMode.Edit;

    /// <summary>Création / première protection : clé pour créer ou chiffrer la base.</summary>
    public byte[]? DbKey { get; private set; }

    /// <summary>Changement de mot de passe : le caller doit rechiffrer la base ouverte avec cette clé.</summary>
    public byte[]? NewDbKey { get; private set; }

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
    public RelayCommand PickColorCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public bool TrySave(string password, string confirm)
    {
        string name = Name?.Trim() ?? "";
        string user = Username?.Trim() ?? "";

        if (name.Length < 1)
        {
            Error = "Indique un nom.";
            return false;
        }

        if (user.Length < 3)
        {
            Error = "L'identifiant doit faire au moins 3 caractères.";
            return false;
        }

        var clash = _store.GetByUsername(user);
        if (clash != null && clash.Id != _existing?.Id)
        {
            Error = "Cet identifiant est déjà utilisé.";
            return false;
        }

        bool setPassword = false;
        if (ShowPasswordFields)
        {
            if ((password ?? "").Length < 4)
            {
                Error = "Mot de passe : 4 caractères minimum.";
                return false;
            }

            if (password != confirm)
            {
                Error = "Les mots de passe ne correspondent pas.";
                return false;
            }

            setPassword = !string.IsNullOrEmpty(password);
        }

        var p = _existing ?? new Profile();
        p.Name = name;
        p.Username = user;
        p.ColorHex = ColorHex;

        if (setPassword)
        {
            var (h, s) = PasswordHasher.Hash(password);
            p.PasswordHash = h;
            p.PasswordSalt = s;

            byte[] dbSalt = PasswordHasher.NewDbSalt();
            byte[] dbKey = PasswordHasher.DeriveDbKey(password, dbSalt);
            p.DbKdfSalt = Convert.ToBase64String(dbSalt);

            if (Mode == ProfileEditMode.Edit)
                NewDbKey = dbKey;   // base déjà chiffrée et ouverte → le caller fait Rekey
            else
                DbKey = dbKey;      // Create : base à créer ; SetupExisting : base en clair à chiffrer
        }

        if (_existing is null) _store.Add(p);
        else _store.Update(p);

        SavedProfile = p;
        CloseRequested?.Invoke(true);
        return true;
    }

    private void Delete()
    {
        var confirm = MessageBox.Show(
            $"Supprimer le profil « {_existing!.Name} » et TOUTES ses données (heures, salaire, historique) ? Cette action est irréversible.",
            "Supprimer le profil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _store.Delete(_existing.Id);
            CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
