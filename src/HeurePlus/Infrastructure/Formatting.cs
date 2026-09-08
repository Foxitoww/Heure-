using System;
using System.Globalization;

namespace HeurePlus.Infrastructure;

/// <summary>Helpers de formatage partagés (heures décimales &lt;-&gt; h:mm, montants…).</summary>
public static class Fmt
{
    /// <summary>Culture française utilisée pour les libellés (mois, jours de semaine).</summary>
    public static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>7.5 -&gt; "7 h 30" ; 8 -&gt; "8 h" ; -1.25 -&gt; "-1 h 15".</summary>
    public static string H(double hours)
    {
        bool neg = hours < 0;
        hours = Math.Abs(hours);
        int h = (int)Math.Floor(hours);
        int m = (int)Math.Round((hours - h) * 60);
        if (m == 60) { h++; m = 0; }
        string body = m == 0 ? $"{h} h" : $"{h} h {m:00}";
        return neg ? "-" + body : body;
    }

    /// <summary>Montant formaté avec la devise (ex. "1 234,56 €").</summary>
    public static string Money(double amount, string currency)
        => amount.ToString("N2", Fr) + " " + currency;

    /// <summary>Découpe des heures décimales en (heures, minutes).</summary>
    public static (int Hours, int Minutes) Split(double hours)
    {
        bool neg = hours < 0;
        hours = Math.Abs(hours);
        int h = (int)Math.Floor(hours);
        int m = (int)Math.Round((hours - h) * 60);
        if (m == 60) { h++; m = 0; }
        return neg ? (-h, m) : (h, m);
    }

    /// <summary>(7, 30) -&gt; 7.5</summary>
    public static double ToDecimal(int hours, int minutes) => hours + minutes / 60.0;

    /// <summary>Analyse "8:15", "8h15", "8.25", "8,25" -&gt; heures décimales. null si invalide.</summary>
    public static double? ParseDuration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().Replace('H', 'h');

        int sep = text.IndexOf(':');
        if (sep < 0) sep = text.IndexOf('h');

        if (sep >= 0)
        {
            var left = text[..sep].Trim();
            var right = text[(sep + 1)..].Trim();
            if (!int.TryParse(left, NumberStyles.Integer, Fr, out int h)) return null;
            int m = 0;
            if (right.Length > 0 && !int.TryParse(right, NumberStyles.Integer, Fr, out m)) return null;
            return (h < 0 ? -1 : 1) * (Math.Abs(h) + m / 60.0);
        }

        if (double.TryParse(text.Replace('.', ','), NumberStyles.Any, Fr, out double dec)) return dec;
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out dec)) return dec;
        return null;
    }
}
