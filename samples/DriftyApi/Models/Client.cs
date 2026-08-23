using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Newtonsoft.Json;

namespace DriftyApi.Models
{
    /// <summary>
    /// The spec declares dateOfIncorporation as `format: date` — plain DateTime
    /// serialization would emit a time component the schema doesn't promise.
    /// </summary>
    internal sealed class DateOnlyJsonConverter : JsonConverter
    {
        private const string Format = "yyyy-MM-dd";

        public override bool CanConvert(Type objectType) => objectType == typeof(DateTime);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((DateTime)value).ToString(Format, CultureInfo.InvariantCulture));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return DateTime.Parse((string)reader.Value, CultureInfo.InvariantCulture);
        }
    }

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

    public class Client
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string CompanyName { get; set; }

        [Required]
        [StringLength(20, MinimumLength = 20)]
        [RegularExpression("^[A-Z0-9]{18}[0-9]{2}$")]
        public string Lei { get; set; }

        public KycSegment KycSegment { get; set; }

        public KycStatus KycStatus { get; set; }

        public string AvoxId { get; set; }

        public RiskLevel RiskLevel { get; set; }

        [Required]
        [JsonConverter(typeof(DateOnlyJsonConverter))]
        public DateTime DateOfIncorporation { get; set; }

        public List<Document> Documents { get; set; } = new List<Document>();
    }
}
