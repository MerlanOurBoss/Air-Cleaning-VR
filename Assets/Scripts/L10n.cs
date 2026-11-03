using System;
using System.Globalization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Тексты из таблицы локализации UI. Строки, собранные в коде, берутся отсюда, а подписи без логики
/// привязаны в префабах и сцене через LocalizeStringEvent. Язык переключается только через LocalizationSettings.
/// </summary>
public static class L10n
{
    private const string Table = "UI";

    // В русском и казахском дробная часть пишется через запятую, а разряды разделяются пробелом.
    private static readonly NumberFormatInfo CommaNumbers = new NumberFormatInfo { NumberDecimalSeparator = ",", NumberGroupSeparator = " " };
    private static readonly NumberFormatInfo DotNumbers = new NumberFormatInfo { NumberDecimalSeparator = ".", NumberGroupSeparator = "," };

    /// <summary>Язык сменился: всё, что собрано в коде, нужно собрать заново.</summary>
    public static event Action Changed;

    static L10n()
    {
        LocalizationSettings.SelectedLocaleChanged += _ => Changed?.Invoke();
    }

    public static string Get(string key, params object[] args) =>
        LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, args);

    public static string Number(double value, string format) =>
        value.ToString(format, CurrentCode == "en" ? DotNumbers : CommaNumbers);

    public static string CurrentCode => LocalizationSettings.SelectedLocale.Identifier.Code;

    public static void SetLanguage(string code) =>
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(code);
}
