using System.ComponentModel;
using System.Text.Json;

namespace Tdms.App.Localization;

/// <summary>
/// Singleton holding the display language. XAML binds through the indexer
/// (<c>{Binding [Open], Source={x:Static loc:Loc.Instance}}</c>), so switching the language
/// redraws the whole window at once. The choice is remembered in the user settings file.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private AppLanguage _language;

    private Loc() => _language = LoadSaved() ?? AppLanguage.En;

    /// <summary>The application wide instance.</summary>
    public static Loc Instance { get; } = new();

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Selectable languages, in the order the website uses.</summary>
    public static AppLanguage[] Languages { get; } =
        [AppLanguage.En, AppLanguage.Ko, AppLanguage.Ja, AppLanguage.De, AppLanguage.Zh];

    /// <summary>Language names for the combo box.</summary>
    public static string[] LanguageOptions { get; } =
        Languages.Select(l => LocStrings.LanguageNames[l]).ToArray();

    /// <summary>Current display language. Changing it invalidates every indexer binding.</summary>
    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            Save(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageIndex)));

            // "Item[]" is the conventional name for "every indexer value changed".
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    /// <summary>Language index, for the combo box binding.</summary>
    public int LanguageIndex
    {
        get => Array.IndexOf(Languages, _language);
        set
        {
            if (value >= 0 && value < Languages.Length)
            {
                Language = Languages[value];
            }
        }
    }

    /// <summary>The string for a key in the current language. Used by the XAML indexer binding.</summary>
    /// <param name="key">String key.</param>
    public string this[string key] => LocStrings.Get(_language, key);

    /// <summary>Lookup helper for code.</summary>
    /// <param name="key">String key.</param>
    /// <returns>The string in the current language.</returns>
    public static string T(string key) => LocStrings.Get(Instance._language, key);

    private static string SettingsPath
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TestBench.tools",
                "TdmsConverter");
            return Path.Combine(directory, "settings.json");
        }
    }

    private static AppLanguage? LoadSaved()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return null;
            }

            var saved = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
            return saved is not null && Enum.TryParse<AppLanguage>(saved.Language, out var parsed)
                ? parsed
                : null;
        }
        catch (Exception)
        {
            // A settings file we cannot read is no reason to fail to start.
            return null;
        }
    }

    private static void Save(AppLanguage language)
    {
        try
        {
            var path = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new Settings(language.ToString())));
        }
        catch (Exception)
        {
            // A read-only profile must not break the app.
        }
    }

    private sealed record Settings(string Language);
}
