using NBomber.Contracts;
using NBomber.CSharp;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

var baseUrl = "http://localhost:5143";

// ---------- HELPERS ----------
async Task<HttpResponseMessage> ApiPost(string token, string path, object? body = null)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    if (body != null)
    {
        var json = JsonSerializer.Serialize(body);
        return await client.PostAsync($"{baseUrl}{path}",
            new StringContent(json, Encoding.UTF8, "application/json"));
    }
    return await client.PostAsync($"{baseUrl}{path}", null);
}

#pragma warning disable CS8321
async Task<IResponse> ApiPostWithResponse(string token, string path, object? body = null)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    HttpResponseMessage res;
    if (body != null)
    {
        var json = JsonSerializer.Serialize(body);
        res = await client.PostAsync($"{baseUrl}{path}",
            new StringContent(json, Encoding.UTF8, "application/json"));
    }
    else
    {
        res = await client.PostAsync($"{baseUrl}{path}", null);
    }

    return res.IsSuccessStatusCode
        ? Response.Ok(statusCode: ((int)res.StatusCode).ToString())
        : Response.Fail(statusCode: ((int)res.StatusCode).ToString());
}
#pragma warning restore CS8321

async Task<string?> CreateRide(string customerToken)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", customerToken);

    var body = JsonSerializer.Serialize(new
    {
        pickupLatitude = 41.7 + Random.Shared.NextDouble() * 0.1,
        pickupLongitude = 44.8 + Random.Shared.NextDouble() * 0.1,
        destinationLatitude = 41.8 + Random.Shared.NextDouble() * 0.1,
        destinationLongitude = 44.9 + Random.Shared.NextDouble() * 0.1
    });

    var res = await client.PostAsync($"{baseUrl}/api/rides",
        new StringContent(body, Encoding.UTF8, "application/json"));

    if (!res.IsSuccessStatusCode) return null;

    var json = await res.Content.ReadAsStringAsync();
    var doc = JsonDocument.Parse(json);
    return doc.RootElement.GetProperty("id").GetString();
}

// ---------- PRE-REGISTER DRIVERS AND CUSTOMERS ----------
var driverTokens = new List<string>();
var customerTokens = new List<string>();

for (int i = 0; i < 10; i++)
{
    var phone = $"+995555{100000 + i}";

    using var regClient = new HttpClient();
    var regBody = JsonSerializer.Serialize(new { phoneNumber = phone, role = "Driver" });
    await regClient.PostAsync($"{baseUrl}/api/Auth/register",
        new StringContent(regBody, Encoding.UTF8, "application/json"));

    var loginBody = JsonSerializer.Serialize(new { phoneNumber = phone });
    var loginRes = await regClient.PostAsync($"{baseUrl}/api/Auth/login",
        new StringContent(loginBody, Encoding.UTF8, "application/json"));
    var loginJson = await loginRes.Content.ReadAsStringAsync();
    var doc = JsonDocument.Parse(loginJson);
    var token = doc.RootElement.GetProperty("accessToken").GetString();
    if (token != null) driverTokens.Add(token);
}

for (int i = 0; i < 5; i++)
{
    var phone = $"+995555{200000 + i}";

    using var regClient = new HttpClient();
    var regBody = JsonSerializer.Serialize(new { phoneNumber = phone, role = "Customer" });
    await regClient.PostAsync($"{baseUrl}/api/Auth/register",
        new StringContent(regBody, Encoding.UTF8, "application/json"));

    var loginBody = JsonSerializer.Serialize(new { phoneNumber = phone });
    var loginRes = await regClient.PostAsync($"{baseUrl}/api/Auth/login",
        new StringContent(loginBody, Encoding.UTF8, "application/json"));
    var loginJson = await loginRes.Content.ReadAsStringAsync();
    var doc = JsonDocument.Parse(loginJson);
    var token = doc.RootElement.GetProperty("accessToken").GetString();
    if (token != null) customerTokens.Add(token);
}

Console.WriteLine($"Registered {driverTokens.Count} drivers, {customerTokens.Count} customers");

string RandomDriver() => driverTokens[Random.Shared.Next(driverTokens.Count)];
string RandomCustomer() => customerTokens[Random.Shared.Next(customerTokens.Count)];

// ---------- EDGE SUITE SCENARIO ----------
var edgeScenario = Scenario.Create("edge_suite", async context =>
{
    var driverToken = RandomDriver();
    var customerToken = RandomCustomer();

    var rideId = await CreateRide(customerToken);
    if (rideId == null)
        return Response.Fail(500);

    var action = Random.Shared.Next(0, 7);

    switch (action)
    {
        case 0:
            // NORMAL FLOW
            await ApiPost(driverToken, $"/api/rides/{rideId}/accept");
            await ApiPost(driverToken, $"/api/rides/{rideId}/arrived");
            await ApiPost(driverToken, $"/api/rides/{rideId}/start");
            await ApiPost(driverToken, $"/api/rides/{rideId}/complete");
            break;

        case 1:
            // DOUBLE ACCEPT
            await ApiPost(driverToken, $"/api/rides/{rideId}/accept");
            await ApiPost(driverToken, $"/api/rides/{rideId}/accept");
            break;

        case 2:
            // WRONG ORDER
            await ApiPost(driverToken, $"/api/rides/{rideId}/start");
            break;

        case 3:
            // ARRIVE BEFORE ACCEPT
            await ApiPost(driverToken, $"/api/rides/{rideId}/arrived");
            break;

        case 4:
            // CANCEL MID FLOW
            await ApiPost(driverToken, $"/api/rides/{rideId}/accept");
            await ApiPost(customerToken, $"/api/rides/{rideId}/cancel");
            break;

        case 5:
            // CANCEL AFTER COMPLETE
            await ApiPost(driverToken, $"/api/rides/{rideId}/accept");
            await ApiPost(driverToken, $"/api/rides/{rideId}/arrived");
            await ApiPost(driverToken, $"/api/rides/{rideId}/start");
            await ApiPost(driverToken, $"/api/rides/{rideId}/complete");
            await ApiPost(customerToken, $"/api/rides/{rideId}/cancel");
            break;

        case 6:
            // INVALID GUID
            await ApiPost(driverToken, "/api/rides/00000000-0000-0000-0000-000000000000/accept");
            break;
    }

    return Response.Ok();
})
.WithLoadSimulations(
    Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
);

// ---------- HIGH CONTENTION SCENARIO ----------
var contentionScenario = Scenario.Create("contention", async context =>
{
    var customerToken = RandomCustomer();
    var rideId = await CreateRide(customerToken);
    if (rideId == null) return Response.Fail();

    // Different drivers race to accept one ride
    var tasks = driverTokens.Select(async token =>
    {
        try { await ApiPost(token, $"/api/rides/{rideId}/accept"); }
        catch { }
    });

    await Task.WhenAll(tasks);

    return Response.Ok();
})
.WithLoadSimulations(
    Simulation.Inject(rate: 30, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
);

// ---------- SIGNALR SCENARIO ----------
var signalRScenario = Scenario.Create("signalr_connections", async context =>
{
    var token = RandomDriver();

    var connection = new HubConnectionBuilder()
        .WithUrl($"{baseUrl}/hubs/rides", o =>
        {
            o.AccessTokenProvider = () => Task.FromResult<string?>(token);
        })
        .Build();

    try
    {
        await connection.StartAsync();
        await Task.Delay(2000);
        await connection.StopAsync();
    }
    catch { }
    finally
    {
        await connection.DisposeAsync();
    }

    return Response.Ok();
})
.WithLoadSimulations(
    Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
);

// ---------- RUN SEQUENTIALLY ----------
NBomberRunner
    .RegisterScenarios(edgeScenario)
    .Run();

NBomberRunner
    .RegisterScenarios(contentionScenario)
    .Run();

NBomberRunner
    .RegisterScenarios(signalRScenario)
    .Run();
