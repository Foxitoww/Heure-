using System;
using System.Windows;
using HeurePlus.Models;

namespace HeurePlus.Services;

/// <summary>Applique le thème clair / sombre en échangeant un ResourceDictionary fusionné.</summary>
public sealed class ThemeManager
{
    private ResourceDictionary? _currentThemeDictionary;

    public AppTheme Current { get; private set; } = AppTheme.Light;

    public void Apply(AppTheme theme)
    {
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Themes/{theme}.xaml", UriKind.Relative)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_currentThemeDictionary is not null)
            merged.Remove(_currentThemeDictionary);

        // Le thème passe en premier : les styles de Shared.xaml le référencent en DynamicResource.
        merged.Insert(0, dictionary);
        _currentThemeDictionary = dictionary;
        Current = theme;
    }

    public AppTheme Toggle()
    {
        Apply(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        return Current;
    }
}
