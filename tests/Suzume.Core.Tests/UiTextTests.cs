using System.Reflection;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class UiTextTests
{
    [Fact]
    public void JapaneseSettingShowsJapanese() =>
        Assert.Same(UiText.Japanese, UiText.For("ja"));

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherSettingsShowEnglish(string? language) =>
        Assert.Same(UiText.English, UiText.For(language));

    public static TheoryData<string> Languages => ["ja", "en"];

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

    [Fact]
    public void EnglishHasNoJapaneseLeftOver()
    {
        var texts = StringProperties().Select(p => (string)p.GetValue(UiText.English)!)
            .Concat(Enum.GetValues<ChirpKind>().Select(UiText.English.ChirpLabel));
        Assert.All(texts, s => Assert.DoesNotContain(s, c => c is >= '぀' and <= 'ヿ' or >= '一' and <= '鿿'));
    }

    [Fact]
    public void JapaneseAndEnglishDiffer()
    {
        foreach (var property in StringProperties())
            Assert.NotEqual(property.GetValue(UiText.English), property.GetValue(UiText.Japanese));
    }

    private static IEnumerable<PropertyInfo> StringProperties() =>
        typeof(UiText).GetProperties().Where(p => p.PropertyType == typeof(string));
}
