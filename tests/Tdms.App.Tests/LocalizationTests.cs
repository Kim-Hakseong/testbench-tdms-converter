using Tdms.App.Localization;
using Xunit;

namespace Tdms.App.Tests;

/// <summary>Guards the language switcher and catches missing translations.</summary>
public sealed class LocalizationTests
{
    [Fact]
    public void EnglishIsTheDefaultLanguage() =>
        Assert.Equal("Open", LocStrings.Get(AppLanguage.En, "Open"));

    [Theory]
    [InlineData(AppLanguage.Ko)]
    [InlineData(AppLanguage.Ja)]
    [InlineData(AppLanguage.De)]
    [InlineData(AppLanguage.Zh)]
    public void EveryLanguageTranslatesTheWholeUi(AppLanguage language)
    {
        var defined = LocStrings.KeysDefinedIn(language).ToHashSet(StringComparer.Ordinal);
        var missing = LocStrings.AllKeys
            .Where(key => !defined.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, $"{language} is missing: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData(AppLanguage.Ko)]
    [InlineData(AppLanguage.Ja)]
    [InlineData(AppLanguage.De)]
    [InlineData(AppLanguage.Zh)]
    public void NoLanguageDefinesAKeyEnglishDoesNot(AppLanguage language)
    {
        var english = LocStrings.AllKeys.ToHashSet(StringComparer.Ordinal);

        Assert.All(LocStrings.KeysDefinedIn(language), key => Assert.Contains(key, english));
    }

    [Fact]
    public void UnknownKeysFallBackToTheKeyItself() =>
        Assert.Equal("NoSuchKey", LocStrings.Get(AppLanguage.Ko, "NoSuchKey"));

    [Fact]
    public void SwitchingLanguageChangesWhatTheUiReads()
    {
        var loc = Loc.Instance;
        var original = loc.Language;
        try
        {
            loc.Language = AppLanguage.En;
            Assert.Equal("Export", loc["Export"]);

            loc.Language = AppLanguage.Ko;
            Assert.Equal("내보내기", loc["Export"]);

            loc.Language = AppLanguage.De;
            Assert.Equal("Exportieren", loc["Export"]);

            loc.Language = AppLanguage.Ja;
            Assert.Equal("エクスポート", loc["Export"]);

            loc.Language = AppLanguage.Zh;
            Assert.Equal("导出", loc["Export"]);
        }
        finally
        {
            loc.Language = original;
        }
    }

    [Fact]
    public void ChangingTheLanguageRaisesTheIndexerInvalidation()
    {
        var loc = Loc.Instance;
        var original = loc.Language;
        var changed = new List<string?>();
        loc.PropertyChanged += Handler;
        try
        {
            loc.Language = original == AppLanguage.De ? AppLanguage.En : AppLanguage.De;

            Assert.Contains("Item[]", changed);
            Assert.Contains(nameof(Loc.LanguageIndex), changed);
        }
        finally
        {
            loc.PropertyChanged -= Handler;
            loc.Language = original;
        }

        void Handler(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
            changed.Add(e.PropertyName);
    }

    [Fact]
    public void TheSwitcherOffersAllFiveLanguages()
    {
        Assert.Equal(5, Loc.Languages.Length);
        Assert.Equal(Loc.Languages.Length, Loc.LanguageOptions.Length);
        Assert.Equal(AppLanguage.En, Loc.Languages[0]);
        Assert.Equal(["English", "한국어", "日本語", "Deutsch", "简体中文"], Loc.LanguageOptions);
    }

    [Fact]
    public void TheLanguageIndexIgnoresOutOfRangeValues()
    {
        var loc = Loc.Instance;
        var original = loc.Language;
        try
        {
            loc.LanguageIndex = 99;
            Assert.Equal(original, loc.Language);

            loc.LanguageIndex = 1;
            Assert.Equal(AppLanguage.Ko, loc.Language);
        }
        finally
        {
            loc.Language = original;
        }
    }
}
