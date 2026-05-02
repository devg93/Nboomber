using NBomber.Contracts;
using NBomber.CSharp;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

var baseUrl = "http://localhost:5143";

// ---------------- HELPERS ----------------
async Task<HttpResponseMessage> ApiPost(string token, string path, object? body = null)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization =
        
        new AuthenticationHeaderValue("Bearer", token);

    try
    {
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            return await client.PostAsync($"{baseUrl}{path}",
                new StringContent(json, Encoding.UTF8, "application/json"));
        }

        return await client.PostAsync($"{baseUrl}{path}", null);
    }
    catch
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
    }
}

async Task<string?> CreateRide(string customerToken)
{
    try
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", customerToken);

        var body = JsonSerializer.Serialize(new
        {
            pickupLatitude = 41.68 + Random.Shared.NextDouble() * 0.07,
            pickupLongitude = 44.75 + Random.Shared.NextDouble() * 0.1,
            destinationLatitude = 41.68 + Random.Shared.NextDouble() * 0.07,
            destinationLongitude = 44.75 + Random.Shared.NextDouble() * 0.1
        });

        var res = await client.PostAsync($"{baseUrl}/api/rides",
            new StringContent(body, Encoding.UTF8, "application/json"));

        if (!res.IsSuccessStatusCode) return null;

        var json = await res.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString();
    }
    catch
    {
        return null;
    }
}

// ---------------- PRE-REGISTER ----------------
var driverTokens = new List<string>();
var customerTokens = new List<string>();

// 50 DRIVERS
for (int i = 0; i < 5000; i++)
{
    var phone = $"+995555{100000 + i}";

    using var client = new HttpClient();

    try
    {
        await client.PostAsync($"{baseUrl}/api/Auth/register",
            new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Driver" }),
            Encoding.UTF8, "application/json"));

        var loginRes = await client.PostAsync($"{baseUrl}/api/Auth/login",
            new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }),
            Encoding.UTF8, "application/json"));

        var json = await loginRes.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("accessToken").GetString();

        if (token != null) driverTokens.Add(token);
    }
    catch { }
}

// 20 CUSTOMERS
for (int i = 0; i < 20000; i++)
{
    var phone = $"+995555{200000 + i}";

    using var client = new HttpClient();

    try
    {
        await client.PostAsync($"{baseUrl}/api/Auth/register",
            new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Customer" }),
            Encoding.UTF8, "application/json"));

        var loginRes = await client.PostAsync($"{baseUrl}/api/Auth/login",
            new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }),
            Encoding.UTF8, "application/json"));

        var json = await loginRes.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("accessToken").GetString();

        if (token != null) customerTokens.Add(token);
    }
    catch { }
}

Console.WriteLine($"Drivers: {driverTokens.Count}, Customers: {customerTokens.Count}");

string RandomDriver() => driverTokens[Random.Shared.Next(driverTokens.Count)];
string RandomCustomer() => customerTokens[Random.Shared.Next(customerTokens.Count)];

// ---------------- SIGNALR POOL ----------------
var signalRConnections = new List<HubConnection>();

async Task InitSignalR()
{
    foreach (var token in driverTokens)
    {
        var conn = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/rides", o =>
            {
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        try
        {
            await conn.StartAsync();
            signalRConnections.Add(conn);
        }
        catch { }
    }
}

// ---------------- SCENARIO 1: RIDE LIFECYCLE ----------------
var rideLifecycle = Scenario.Create("real_taxi_flow", async context =>
{
    try
    {
        var customer = RandomCustomer();
        var driver = RandomDriver();

        var rideId = await CreateRide(customer);
        if (rideId == null) return Response.Fail();

        await ApiPost(driver, $"/api/rides/{rideId}/accept");
        await ApiPost(driver, $"/api/rides/{rideId}/arrived");
        await ApiPost(driver, $"/api/rides/{rideId}/start");
        await ApiPost(driver, $"/api/rides/{rideId}/complete");

        return Response.Ok();
    }
    catch
    {
        return Response.Fail();
    }
})
.WithLoadSimulations(
    Simulation.Inject(rate: 10,  interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
    Simulation.Inject(rate: 50,  interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20)),
    Simulation.Inject(rate: 200, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(120)),
    Simulation.Inject(rate: 10,  interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
);

// ---------------- SCENARIO 2: LOCATION UPDATES ----------------
var locationUpdates = Scenario.Create("cancel_flow", async context =>
{
    try
    {
        var tasks = signalRConnections.Select(async conn =>
        {
            try
            {
                await conn.InvokeAsync("UpdateLocation", new
                {
                    latitude = 41.68 + Random.Shared.NextDouble() * 0.07,
                    longitude = 44.75 + Random.Shared.NextDouble() * 0.1
                });
            }
            catch { }
        });

        await Task.WhenAll(tasks);
        await Task.Delay(3000);

        return Response.Ok();
    }
    catch
    {
        return Response.Fail();
    }
})
.WithLoadSimulations(
    Simulation.Inject(rate: 3, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
);

// ---------------- SCENARIO 3: CONCURRENT ACCEPT ----------------
var concurrentAccept = Scenario.Create("race_condition", async context =>
{
    try
    {
        // Pick 5 distinct random drivers
        var drivers = driverTokens.OrderBy(_ => Random.Shared.Next()).Take(5).ToList();

        // Reset all 5 to available before the race
        await Task.WhenAll(drivers.Select(token =>
            ApiPost(token, "/api/drivers/me/availability", new { isAvailable = true })));

        var rideId = await CreateRide(RandomCustomer());
        if (rideId == null) return Response.Fail();

        // All 5 race to accept simultaneously
        var results = await Task.WhenAll(drivers.Select(async token =>
        {
            try
            {
                var res = await ApiPost(token, $"/api/rides/{rideId}/accept");
                return (token, success: res.IsSuccessStatusCode);
            }
            catch { return (token, success: false); }
        }));

        var successCount = results.Count(r => r.success);

        // Free the winning driver so they don't stay on an active ride
        var winner = results.FirstOrDefault(r => r.success);
        if (winner.token != null)
            await ApiPost(winner.token, $"/api/rides/{rideId}/complete");

        return successCount == 1 ? Response.Ok() : Response.Fail();
    }
    catch
    {
        return Response.Fail();
    }
})
.WithLoadSimulations(
    Simulation.Inject(rate: 2, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
);

// ---------------- SCENARIO 4: SIGNALR STEADY ----------------
var signalRSteady = Scenario.Create("driver_online_offline", async context =>
{
    var token = RandomDriver();

    var conn = new HubConnectionBuilder()
        .WithUrl($"{baseUrl}/hubs/rides", o =>
        {
            o.AccessTokenProvider = () => Task.FromResult<string?>(token);
        })
        .Build();

    try
    {
        await conn.StartAsync();
        await Task.Delay(30000);

        return conn.State == HubConnectionState.Connected
            ? Response.Ok()
            : Response.Fail();
    }
    catch
    {
        return Response.Fail();
    }
    finally
    {
        await conn.DisposeAsync();
    }
})
.WithLoadSimulations(
    Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
);

// ---------------- RUN ----------------
await InitSignalR();

NBomberRunner.RegisterScenarios(rideLifecycle).Run();
NBomberRunner.RegisterScenarios(locationUpdates).Run();
NBomberRunner.RegisterScenarios(concurrentAccept).Run();
NBomberRunner.RegisterScenarios(signalRSteady).Run();