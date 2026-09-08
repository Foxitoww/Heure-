using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HeurePlus.Infrastructure;

/// <summary>Base minimaliste pour le pattern MVVM (pas de dépendance externe).</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected void RaiseAll() => OnPropertyChanged(string.Empty);
}
