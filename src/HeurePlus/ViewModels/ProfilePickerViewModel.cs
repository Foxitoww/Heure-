using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>Écran « Qui utilise Heure+ ? » : choix, création et gestion des profils.</summary>
public sealed class ProfilePickerViewModel : ObservableObject
{
    private readonly ProfileStore _store;
    private readonly Func<LoginViewModel, bool?> _showLogin;
    private readonly Func<ProfileEditViewModel, bool?> _showEdit;

    public ProfilePickerViewModel(
        ProfileStore store,
        Func<LoginViewModel, bool?> showLogin,
        Func<ProfileEditViewModel, bool?> showEdit)
    {
        _store = store;
        _showLogin = showLogin;
        _showEdit = showEdit;

        SelectCommand = new RelayCommand(p =>
        {
            if (p is ProfileCardViewModel card) Select(card);
        });
        EditCommand = new RelayCommand(p =>
        {
            if (p is ProfileCardViewModel { Profile: { } profile })
            {
                _showEdit(new ProfileEditViewModel(_store, ProfileEditMode.Edit, profile));
                RefreshCards();
            }
        });
        DeleteCommand = new RelayCommand(p =>
        {
            if (p is ProfileCardViewModel { Profile: { } profile })
            {
                var confirm = MessageBox.Show(
                    $"Supprimer « {profile.Name} » et toutes ses données ?",
                    "Heure+", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;

                try
                {
                    _store.Delete(profile.Id);
                    RefreshCards();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Heure+", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        });
        ToggleManageCommand = new RelayCommand(_ => ManageMode = !ManageMode);

        RefreshCards();
    }

    public event Action<bool>? CloseRequested;

    public Profile? Result { get; private set; }

    public ObservableCollection<ProfileCardViewModel> Cards { get; } = new();

    public string Title => "Qui utilise Heure+ ?";

    private bool _manageMode;
    public bool ManageMode
    {
        get => _manageMode;
        set => SetProperty(ref _manageMode, value);
    }

    public RelayCommand SelectCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ToggleManageCommand { get; }

    public void RefreshCards()
    {
        Cards.Clear();
        foreach (var p in _store.All.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            Cards.Add(new ProfileCardViewModel { Profile = p });
        Cards.Add(new ProfileCardViewModel { Profile = null });
    }

    private void Select(ProfileCardViewModel card)
    {
        if (card.IsAddCard)
        {
            CreateFlow();
            return;
        }

        var p = card.Profile!;

        if (!p.HasCredentials)
        {
            var ev = new ProfileEditViewModel(_store, ProfileEditMode.SetupExisting, p);
            if (_showEdit(ev) == true) Finish(_store.Get(p.Id));
            return;
        }

        var lv = new LoginViewModel(p);
        if (_showLogin(lv) == true)
        {
            if (lv.RememberMe) _store.RememberedProfileId = p.Id;
            Finish(p);
        }
    }

    private void CreateFlow()
    {
        var ev = new ProfileEditViewModel(_store, ProfileEditMode.Create, null);
        if (_showEdit(ev) == true && ev.SavedProfile is { } np)
        {
            if (ev.RememberMe) _store.RememberedProfileId = np.Id;
            Finish(np);
        }
        else
        {
            RefreshCards();
        }
    }

    private void Finish(Profile? p)
    {
        Result = p;
        CloseRequested?.Invoke(p is not null);
    }
}
