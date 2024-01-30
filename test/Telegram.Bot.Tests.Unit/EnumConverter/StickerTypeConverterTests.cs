
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Telegram.Bot.Converters;
using Telegram.Bot.Types.Enums;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Telegram.Bot.Tests.Unit.EnumConverter;

public class StickerTypeConverterTests
{
    [Fact]
    public void Should_Verify_All_StickerType_Members()
    {
        List<string> stickerTypeMembers = Enum
            .GetNames<StickerType>()
            .OrderBy(x => x)
            .ToList();
        List<string> stickerTypeDataMembers = new StickerTypeData()
            .Select(x => Enum.GetName(typeof(StickerType), x[0]))
            .OrderBy(x => x)
            .ToList()!;

        Assert.Equal(stickerTypeMembers.Count, stickerTypeDataMembers.Count);
        Assert.Equal(stickerTypeMembers, stickerTypeDataMembers);
    }


    [Theory]
    [ClassData(typeof(StickerTypeData))]
    public void Should_Convert_StickerType_To_String(StickerType stickerType, string value)
    {
        Sticker sticker = new() { Type = stickerType };
        string expectedResult = $$"""{"type":"{{value}}"}""";

        string result = JsonSerializer.Serialize(sticker, JsonSerializerOptionsProvider.Options);

        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [ClassData(typeof(StickerTypeData))]
    public void Should_Convert_String_To_StickerType(StickerType stickerType, string value)
    {
        Sticker expectedResult = new() { Type = stickerType };
        string jsonData = $$"""{"type":"{{value}}"}""";

        Sticker result = JsonSerializer.Deserialize<Sticker>(jsonData, JsonSerializerOptionsProvider.Options)!;

        Assert.Equal(expectedResult.Type, result.Type);
    }

    [Fact]
    public void Should_Return_Zero_For_Incorrect_StickerType()
    {
        string jsonData = $$"""{"type":"{{int.MaxValue}}"}""";

        Sticker result = JsonSerializer.Deserialize<Sticker>(jsonData, JsonSerializerOptionsProvider.Options)!;

        Assert.Equal((StickerType)0, result.Type);
    }

    [Fact]
    public void Should_Throw_NotSupportedException_For_Incorrect_StickerType()
    {
        Sticker sticker = new() { Type = (StickerType)int.MaxValue };

        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(sticker, JsonSerializerOptionsProvider.Options));
    }

    class Sticker
    {
        /// <summary>
        /// Type of the sticker. The type of the sticker is independent from its format,
        /// which is determined by the fields <see cref="IsAnimated"/> and <see cref="IsVideo"/>.
        /// </summary>
        public StickerType Type { get; set; }
    }

    private class StickerTypeData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return [StickerType.Regular, "regular"];
            yield return [StickerType.Mask, "mask"];
            yield return [StickerType.CustomEmoji, "custom_emoji"];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
