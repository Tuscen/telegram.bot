using Scriban;
using System;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Text;

namespace EnumSerializer.Generator;

public static class SourceGenerationHelper
{
    private const string Header = @"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the EnumSerializer.Generator source generator
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable
";

    public const string EnumConverter = Header + @"
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;

namespace Telegram.Bot.Converters;

internal abstract class EnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : Enum
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract TEnum GetEnumValue(string value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract string GetStringValue(TEnum value);

    public override void WriteJson(JsonWriter writer, TEnum value, JsonSerializer serializer) =>
        writer.WriteValue(GetStringValue(value));

    public override TEnum ReadJson(
        JsonReader reader,
        Type objectType,
        TEnum existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    ) =>
        GetEnumValue(JToken.ReadFrom(reader).Value<string>());
}
";

    public const string converterTemplate = @"
{{ header }}

using System;
using Telegram.Bot.Converters;

{{~ if enum_namespace ~}}
namespace {{ enum_namespace }};
{{~ end ~}}

internal partial class {{ enum_name }}Converter : EnumConverter<{{ enum_name }}>
{
    protected override {{ enum_name }} GetEnumValue(string value) =>
        value switch
        {
        {{~ for enum_member in enum_members ~}}
            ""{{ enum_member.value }}"" => {{ enum_name }}.{{ enum_member.key }},
        {{~ end ~}}
        {{~ if has_unknown_member ~}}
            _ => {{ enum_name }}.Unknown,
        {{~ else ~}}
            _ => 0,
        {{~ end ~}}
        };

    protected override string GetStringValue({{ enum_name }} value) =>
        value switch
        {
        {{~ for enum_member in enum_members ~}}
            {{ enum_name }}.{{enum_member.key}} => ""{{ enum_member.value }}"",
        {{~ end ~}}
        {{~ if has_unknown_member ~}}
            _ => ""unknown"",
        {{~ else ~}}
            _ => throw new NotSupportedException(),
        {{~ end ~}}
        };
}";

    public static string GenerateConverterClass(Template template, EnumInfo enumToGenerate)
    {
        var hasUnknownMember = enumToGenerate.Members.Any(
            e => string.Equals(e.Value, "Unknown", StringComparison.OrdinalIgnoreCase));

        var result = template.Render(new
        {
            EnumNamespace = enumToGenerate.Namespace,
            EnumName = enumToGenerate.Name,
            EnumMembers = enumToGenerate.Members,
            HasUnknownMember = hasUnknownMember
        });

        return result;
    }
}
