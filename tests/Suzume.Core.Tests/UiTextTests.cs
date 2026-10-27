using System.Reflection;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class UiTextTests
{
    [Fact]
    public void JapaneseSettingShowsJapanese() =>
        Assert.Same(UiText.Japanese, UiText.For("ja"));

    [Fact]
    public void GermanSettingShowsGerman() =>
        Assert.Same(UiText.German, UiText.For("de"));

    [Fact]
    public void FrenchSettingShowsFrench() =>
        Assert.Same(UiText.French, UiText.For("fr"));

    [Theory]
    [InlineData("en")]
    [InlineData("ko")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherSettingsShowEnglish(string? language) =>
        Assert.Same(UiText.English, UiText.For(language));

    public static TheoryData<string> Languages => ["ja", "en", "de", "fr"];

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryTextIsFilledIn(string language)
    {
        var text = UiText.For(language);
        foreach (var property in StringProperties())
            Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(text)), property.Name);
        foreach (var kind in Enum.GetValues<ChirpKind>())
            Assert.NotEqual(kind.ToString(), text.ChirpLabel(kind));
    }

    public static TheoryData<string> LatinLanguages => ["en", "de", "fr"];

    [Theory]
    [MemberData(nameof(LatinLanguages))]
    public void HasNoJapaneseLeftOver(string language)
    {
        var text = UiText.For(language);
        var texts = StringProperties().Select(p => (string)p.GetValue(text)!)
            .Concat(Enum.GetValues<ChirpKind>().Select(text.ChirpLabel));
        Assert.All(texts, s => Assert.DoesNotContain(s, c => c is >= '぀' and <= 'ヿ' or >= '一' and <= '鿿'));
    }

    [Fact]
    public void JapaneseAndEnglishDiffer()
    {
        foreach (var property in StringProperties())
            Assert.NotEqual(property.GetValue(UiText.English), property.GetValue(UiText.Japanese));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("fr")]
    public void IsTranslatedFromEnglish(string language)
    {
        var text = UiText.For(language);
        // タイトルや "Volume" のように同じ綴りの語もあるので、大半が違えばよい
        var same = StringProperties().Count(p => Equals(p.GetValue(text), p.GetValue(UiText.English)));
        Assert.True(same <= 3, $"{same} 個が英語のまま");
    }

    private static IEnumerable<PropertyInfo> StringProperties() =>
        typeof(UiText).GetProperties().Where(p => p.PropertyType == typeof(string));
}
