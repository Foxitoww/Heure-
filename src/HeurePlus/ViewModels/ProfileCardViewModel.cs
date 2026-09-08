using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>
/// Donnée immuable d'une tuile du sélecteur de profils. <see cref="Profile"/> à
/// <c>null</c> représente la tuile « créer un profil ».
/// </summary>
public sealed class ProfileCardViewModel
{
    public Profile? Profile { get; init; }

    public bool IsAddCard => Profile is null;

    public string Name => Profile?.Name ?? "Créer un profil";

    public string Initial => Profile?.Initial ?? "+";

    public string ColorHex => Profile?.ColorHex ?? "#5A5A5A";

    public bool Protected => Profile?.HasCredentials == true;

    public bool NeedsSetup => Profile is not null && !Profile.HasCredentials;
}
