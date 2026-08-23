using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DriftyApi.Core.Models;

public enum KycSegment
{
    Retail,
    Corporate,
    Institutional,
    FinancialInstitution
}

public enum KycStatus
{
    NotStarted,
    InProgress,
    PendingReview,
    Approved,
    Rejected,
    Archived
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Prohibited
}

/// <summary>
/// The spec declares dateOfIncorporation as `format: date` — plain DateTime
/// serialization would emit a time component the schema doesn't promise.
/// </summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-dd";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}

public class Client
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 20)]
    [RegularExpression("^[A-Z0-9]{18}[0-9]{2}$")]
    public string Lei { get; set; } = string.Empty;

    public KycSegment KycSegment { get; set; }

    public KycStatus KycStatus { get; set; }

    public string? AvoxId { get; set; }

    public RiskLevel RiskLevel { get; set; }

    [Required]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTime DateOfIncorporation { get; set; }

    public List<Document> Documents { get; set; } = new();
}
