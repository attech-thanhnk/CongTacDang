using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CongTacDang.Api.Json;

/// <summary>Giờ gửi lên kèm múi giờ (…+07:00) đổi về UTC; không kèm múi giờ coi là UTC như các service đang làm. Cột timestamptz chỉ nhận UTC.</summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Normalize(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value);

    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
