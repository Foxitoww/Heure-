using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>Panneau de détails sous le calendrier.</summary>
public sealed class DayDetailsViewModel : ObservableObject
{
    private DayCellViewModel? _cell;

    public void SetCell(DayCellViewModel? cell)
    {
        _cell = cell;
        RaiseAll();
    }

    public bool HasSelection => _cell is not null;

    public bool HasEntry => _cell?.Entry is not null;

    public bool IsEmpty => _cell is not null && _cell.Entry is null;

    public string DateLabel => _cell is null
        ? "Aucun jour sélectionné"
        : Fmt.Fr.TextInfo.ToTitleCase(
            _cell.Date.ToDateTime(default).ToString("dddd d MMMM yyyy", Fmt.Fr));

    public string StatusLabel => _cell?.Entry?.Status.Label() ?? "—";

    public string StatusKey => _cell?.Entry?.Status.ToString() ?? "None";

    public string HoursLabel
    {
        get
        {
            if (_cell?.Entry is not { } e) return "—";
            return $"{Fmt.H(e.NormalHours)} normales · {Fmt.H(e.OvertimeHours)} sup. · {Fmt.H(e.TotalHours)} au total";
        }
    }

    public string ScheduleLabel
    {
        get
        {
            if (_cell?.Entry is not { } e) return "—";
            if (e.StartTime is { } s && e.EndTime is { } end)
                return $"{s:HH\\:mm} – {end:HH\\:mm}  (pause {e.BreakMinutes} min)";
            return "Horaire non précisé";
        }
    }

    public string RateLabel
    {
        get
        {
            if (_cell?.Entry is not { } e) return "—";
            return e.HourlyRateOverride is { } r
                ? $"{Fmt.Money(r, "€")}/h (taux spécifique)"
                : "Taux général";
        }
    }

    public string Note => _cell?.Entry?.Note ?? string.Empty;

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}
