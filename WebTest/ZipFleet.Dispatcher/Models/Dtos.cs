using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipFleet.Dispatcher.Models;

public class LogEntry
{
    public DateTime Time { get; } = DateTime.Now;
    public string Message { get; set; } = "";
    public string Level { get; set; } = "info"; // success | error | warning | info
}

public record AuthResponse(string AccessToken);
public record LoginRequest(string PhoneNumber, string Password);
public record RegisterRequest(string PhoneNumber, string Role, string Password);
public record RegisterResponse(Guid UserId, string Role);

public record CreateRideRequest(
    decimal PickupLatitude, decimal PickupLongitude,
    decimal DestinationLatitude, decimal DestinationLongitude);

public record LocationDto(decimal Latitude, decimal Longitude);

public record RideDto(
    Guid Id,
    Guid UserId,
    Guid? DriverId,
    LocationDto PickupLocation,
    LocationDto DestinationLocation,
    decimal DistanceKm,
    decimal Price,
    JsonElement Status,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public string StatusText
    {
        get
        {
            if (Status.ValueKind == JsonValueKind.Number)
            {
                return Status.GetInt32() switch
                {
                    0 => "Requested",
                    1 => "Offered",
                    2 => "DriverAssigned",
                    3 => "Arrived",
                    4 => "OnTheWay",
                    5 => "Completed",
                    6 => "Cancelled",
                    _ => "Unknown"
                };
            }
            if (Status.ValueKind == JsonValueKind.String)
            {
                return Status.GetString() ?? "Unknown";
            }
            return "Unknown";
        }
    }
}

public record DriverDto(
    Guid Id, Guid UserId, bool IsAvailable,
    LocationDto CurrentLocation, DateTime LastUpdatedAt);

public class GeoPosition
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
}

public class AvailabilityRequest
{
    public bool IsAvailable { get; set; }
}

public class LocationRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class ApiSettings
{
    public string BaseUrl { get; set; } = "http://94.130.230.4:5143";
}


public class NewRideAvailableEvent
{
    [JsonPropertyName("id")]
    public string RideId { get; set; } = "";

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("distanceKm")]
    public double Distance { get; set; }

    [JsonPropertyName("pickupLocation")]
    public LocationDto Pickup { get; set; } = default!;

    [JsonPropertyName("destinationLocation")]
    public LocationDto Destination { get; set; } = default!;
}
