using Microsoft.AspNetCore.SignalR.Client;

namespace ZipFleet.Dispatcher.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _connection;
    private string? _hubUrl;
    private Func<string>? _tokenProvider;

    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => ConnectionState == HubConnectionState.Connected;

    public event Action<string>? OnNewRideAvailable;
    public event Action<string>? OnRideTaken;
    public event Action<string>? OnDriverFound;
    public event Action<string>? OnDriverArrived;
    public event Action<string>? OnRideStarted;
    public event Action<string>? OnRideCompleted;
    public event Action<string>? OnRideCancelled;
    public event Action<string>? OnRideDeclined;
    public event Action<string>? OnDriverLocationUpdated;
    public event Action? OnConnectionChanged;

    public void Configure(string hubUrl, Func<string> tokenProvider)
    {
        _hubUrl = hubUrl;
        _tokenProvider = tokenProvider;
    }

    public async Task ConnectAsync()
    {
        if (_connection != null)
            await DisconnectAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl!, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_tokenProvider?.Invoke());
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.Closed += _ => { OnConnectionChanged?.Invoke(); return Task.CompletedTask; };
        _connection.Reconnected += _ => { OnConnectionChanged?.Invoke(); return Task.CompletedTask; };
        _connection.Reconnecting += _ => { OnConnectionChanged?.Invoke(); return Task.CompletedTask; };

        _connection.On<object>("NewRideAvailable", data =>
        {
            OnNewRideAvailable?.Invoke(System.Text.Json.JsonSerializer.Serialize(data));
        });

        _connection.On<object>("RideTaken", data =>
        {
            OnRideTaken?.Invoke(System.Text.Json.JsonSerializer.Serialize(data));
        });

        _connection.On<object>("DriverFound", data =>
        {
            OnDriverFound?.Invoke(System.Text.Json.JsonSerializer.Serialize(data));
        });

        _connection.On<object>("DriverArrived", data =>
        {
            OnDriverArrived?.Invoke(System.Text.Json.JsonSerializer.Serialize(data));
        });

        _connection.On<object>("RideStarted", data =>
        {
            OnRideStarted?.Invoke(System.Text.Json.JsonSerializer.Serialize(data));
        });

        _connection.On<object>("RideCompleted", data =>
        {
            OnRideCompleted?.Invoke(System.Text.Json.JsonSerializer.Serialize(data));
        });

        _connection.On<object>("RideCancelled", data =>
        {
            OnRideCancelled?.Invoke(System.Text.Json.JsonSerializer.Serialize(data));
        });

        _connection.On<object>("RideDeclined", data =>
        {
            OnRideDeclined?.Invoke(System.Text.Json.JsonSerializer.Serialize(data));
        });

        _connection.On<object>("DriverLocationUpdated", data =>
        {
            OnDriverLocationUpdated?.Invoke(System.Text.Json.JsonSerializer.Serialize(data));
        });

        await _connection.StartAsync();
        OnConnectionChanged?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            try { await _connection.StopAsync(); } catch { }
            await _connection.DisposeAsync();
            _connection = null;
        }
        OnConnectionChanged?.Invoke();
    }

    public async Task InvokeAsync(string method, params object?[] args)
    {
        if (_connection is null || !IsConnected) return;
        await _connection.InvokeAsync(method, args);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();
    }
}
