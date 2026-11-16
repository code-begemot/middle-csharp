using System.Text.Json.Serialization;

namespace MiddleCsharp.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingStatus
{
    Pending,
    Confirmed,
    Rejected
}
