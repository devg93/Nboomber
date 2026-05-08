using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ZipFleet.Dispatcher.Models;

namespace ZipFleet.Dispatcher.Services;

public class ApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public ApiService(HttpClient http)
    {
        _http = http;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync();
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(body);
            if (json.TryGetProperty("error", out var err))
                throw new Exception(err.GetString());
        }
        catch (JsonException) { }
        throw new Exception(string.IsNullOrWhiteSpace(body) ? resp.ReasonPhrase : body);
    }

    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearToken()
    {
        _http.DefaultRequestHeaders.Authorization = null;
    }

    // Register then immediately login to obtain a token.
    public async Task<(string Token, string UserId)> RegisterAsync(string phone, string role, string pin)
    {
        var regResp = await _http.PostAsJsonAsync("/api/Auth/register", new RegisterRequest(phone, role, pin));
        await EnsureSuccessAsync(regResp);
        var reg = await regResp.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions) ?? throw new Exception("Empty register response");

        var loginResp = await _http.PostAsJsonAsync("/api/Auth/login", new LoginRequest(phone, pin));
        await EnsureSuccessAsync(loginResp);
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions) ?? throw new Exception("Empty login response");

        return (auth.AccessToken, reg.UserId.ToString());
    }

    public async Task<AuthResponse> LoginAsync(string phone, string pin)
    {
        var resp = await _http.PostAsJsonAsync("/api/Auth/login", new LoginRequest(phone, pin));
        await EnsureSuccessAsync(resp);
        return await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions) ?? throw new Exception("Empty response");
    }

    public async Task<RideDto> CreateRideAsync(decimal pickupLat, decimal pickupLng, decimal destLat, decimal destLng)
    {
        var resp = await _http.PostAsJsonAsync("/api/rides", new CreateRideRequest(pickupLat, pickupLng, destLat, destLng));
        await EnsureSuccessAsync(resp);
        return await resp.Content.ReadFromJsonAsync<RideDto>(JsonOptions) ?? throw new Exception("Empty response");
    }

    public async Task<RideDto> AcceptRideAsync(string rideId)
    {
        var url = $"/api/rides/{rideId}/accept";
        Console.WriteLine($"POST URL: {url}");
        var resp = await _http.PostAsync($"/api/rides/{rideId}/accept", null);
        Console.WriteLine($"ACCEPT RIDE ID: {rideId}");
        await EnsureSuccessAsync(resp);
        return await resp.Content.ReadFromJsonAsync<RideDto>(JsonOptions) ?? throw new Exception("Empty response");
    }

    public async Task ArriveAsync(string rideId, decimal latitude, decimal longitude)
    {
        var resp = await _http.PostAsJsonAsync($"/api/rides/{rideId}/arrived", new { latitude, longitude });
        await EnsureSuccessAsync(resp);
    }

    public async Task StartAsync(string rideId)
    {
        var resp = await _http.PostAsync($"/api/rides/{rideId}/start", null);
        await EnsureSuccessAsync(resp);
    }

    public async Task CompleteAsync(string rideId)
    {
        var resp = await _http.PostAsync($"/api/rides/{rideId}/complete", null);
        await EnsureSuccessAsync(resp);
    }

    public async Task CancelAsync(string rideId)
    {
        var resp = await _http.PostAsync($"/api/rides/{rideId}/cancel", null);
        await EnsureSuccessAsync(resp);
    }

    public async Task<RideDto> GetRideAsync(string rideId)
    {
        var resp = await _http.GetAsync($"/api/rides/{rideId}");
        await EnsureSuccessAsync(resp);
        return await resp.Content.ReadFromJsonAsync<RideDto>(JsonOptions) ?? throw new Exception("Empty response");
    }

    public async Task<RideDto?> GetActiveRideAsync()
    {
        var resp = await _http.GetAsync("/api/rides/active");
        if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        await EnsureSuccessAsync(resp);
        return await resp.Content.ReadFromJsonAsync<RideDto>(JsonOptions);
    }

    public async Task<RideDto> DeclineRideAsync(string rideId)
    {
        var resp = await _http.PostAsync($"/api/rides/{rideId}/decline", null);
        await EnsureSuccessAsync(resp);
        return await resp.Content.ReadFromJsonAsync<RideDto>(JsonOptions) ?? throw new Exception("Empty response");
    }

    public async Task ToggleAvailabilityAsync(bool isAvailable)
    {
        var resp = await _http.PostAsJsonAsync("/api/drivers/me/availability", new AvailabilityRequest { IsAvailable = isAvailable });
        await EnsureSuccessAsync(resp);
    }

    public async Task UpdateLocationAsync(double lat, double lng)
    {
        var resp = await _http.PostAsJsonAsync("/api/drivers/me/location", new LocationRequest { Latitude = lat, Longitude = lng });
        await EnsureSuccessAsync(resp);
    }
}
