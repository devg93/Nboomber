using ZipFleet.Dispatcher.Models;

namespace ZipFleet.Dispatcher.Services;

public class AppState
{
    public string ActiveRole { get; private set; } = "Customer";

    // Customer auth
    public string? CustomerToken { get; private set; }
    public string? CustomerId { get; private set; }
    public string? CustomerPhone { get; private set; }
    public bool IsCustomerLoggedIn => !string.IsNullOrEmpty(CustomerToken);

    // Driver auth
    public string? DriverToken { get; private set; }
    public string? DriverId { get; private set; }
    public string? DriverPhone { get; private set; }
    public bool IsDriverLoggedIn => !string.IsNullOrEmpty(DriverToken);

    // Customer ride state
    public string? CustomerRideId { get; private set; }
    public string CustomerRideStatus { get; private set; } = "";

    // Driver state
    public bool DriverIsOnline { get; private set; }
    public string? DriverActiveRideId { get; private set; }
    public string DriverActiveRideStatus { get; private set; } = "";

    public event Action? OnChange;

    public void SetRole(string role)
    {
        ActiveRole = role;
        Notify();
    }

    public void SetCustomerAuth(string token, string userId, string phone)
    {
        CustomerToken = token;
        CustomerId = userId;
        CustomerPhone = phone;
        Notify();
    }

    public void SetDriverAuth(string token, string userId, string phone)
    {
        DriverToken = token;
        DriverId = userId;
        DriverPhone = phone;
        Notify();
    }

    public void SetCustomerRide(string rideId, string status)
    {
        CustomerRideId = rideId;
        CustomerRideStatus = status;
        Notify();
    }

    public void UpdateCustomerRideStatus(string status)
    {
        CustomerRideStatus = status;
        Notify();
    }

    public void ClearCustomerRide()
    {
        CustomerRideId = null;
        CustomerRideStatus = "";
        Notify();
    }

    public void SetDriverOnline(bool online)
    {
        DriverIsOnline = online;
        Notify();
    }

    public void SetDriverActiveRide(string rideId, string status)
    {
        DriverActiveRideId = rideId;
        DriverActiveRideStatus = status;
        Notify();
    }

    public void UpdateDriverActiveRideStatus(string status)
    {
        DriverActiveRideStatus = status;
        Notify();
    }

    public void ClearDriverActiveRide()
    {
        DriverActiveRideId = null;
        DriverActiveRideStatus = "";
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
