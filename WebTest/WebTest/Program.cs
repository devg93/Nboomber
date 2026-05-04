// using NBomber.Contracts;
// using NBomber.CSharp;
// using Microsoft.AspNetCore.SignalR.Client;
// using System.Text;
// using System.Text.Json;
// using System.Net.Http.Headers;
//
// var baseUrl = "http://localhost:5143";
// var httpTimeout = TimeSpan.FromSeconds(10);
//
// // ---------------- HELPERS ----------------
// async Task<HttpResponseMessage> ApiPost(string token, string path, object? body = null)
// {
//     using var client = new HttpClient();
//     client.Timeout = httpTimeout;
//     client.DefaultRequestHeaders.Authorization =
//         
//         new AuthenticationHeaderValue("Bearer", token);
//
//     try
//     {
//         if (body != null)
//         {
//             var json = JsonSerializer.Serialize(body);
//             return await client.PostAsync($"{baseUrl}{path}",
//                 new StringContent(json, Encoding.UTF8, "application/json"));
//         }
//
//         return await client.PostAsync($"{baseUrl}{path}", null);
//     }
//     catch
//     {
//         return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
//     }
// }
//
// async Task<CreatedRide?> CreateRide(string customerToken)
// {
//     try
//     {
//         using var client = new HttpClient();
//         client.Timeout = httpTimeout;
//         client.DefaultRequestHeaders.Authorization =
//             new AuthenticationHeaderValue("Bearer", customerToken);
//
//         var pickupLatitude = 41.68m + (decimal)Random.Shared.NextDouble() * 0.07m;
//         var pickupLongitude = 44.75m + (decimal)Random.Shared.NextDouble() * 0.1m;
//
//         var body = JsonSerializer.Serialize(new
//         {
//             pickupLatitude,
//             pickupLongitude,
//             destinationLatitude = 41.68m + (decimal)Random.Shared.NextDouble() * 0.07m,
//             destinationLongitude = 44.75m + (decimal)Random.Shared.NextDouble() * 0.1m
//         });
//
//         var res = await client.PostAsync($"{baseUrl}/api/rides",
//             new StringContent(body, Encoding.UTF8, "application/json"));
//
//         if (!res.IsSuccessStatusCode) return null;
//
//         var json = await res.Content.ReadAsStringAsync();
//         var doc = JsonDocument.Parse(json);
//         var id = doc.RootElement.GetProperty("id").GetString();
//
//         return id == null
//             ? null
//             : new CreatedRide(id, pickupLatitude, pickupLongitude);
//     }
//     catch
//     {
//         return null;
//     }
// }
//
// async Task<bool> PostOk(string token, string path, object? body = null)
// {
//     var res = await ApiPost(token, path, body);
//     return res.IsSuccessStatusCode;
// }
//
// // ---------------- PRE-REGISTER ----------------
// var driverTokens = new List<string>();
// var customerTokens = new List<string>();
//
// // 50 DRIVERS
// for (int i = 0; i < 15000; i++)
// {
//     var phone = $"+995555{100000 + i}";
//
//     using var client = new HttpClient();
//
//     try
//     {
//         await client.PostAsync($"{baseUrl}/api/Auth/register",
//             new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Driver" }),
//             Encoding.UTF8, "application/json"));
//
//         var loginRes = await client.PostAsync($"{baseUrl}/api/Auth/login",
//             new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }),
//             Encoding.UTF8, "application/json"));
//
//         var json = await loginRes.Content.ReadAsStringAsync();
//         var doc = JsonDocument.Parse(json);
//         var token = doc.RootElement.GetProperty("accessToken").GetString();
//
//         if (token != null) driverTokens.Add(token);
//     }
//     catch { }
// }
//
// // 20 CUSTOMERS
// for (int i = 0; i < 210000; i++)
// {
//     var phone = $"+995555{200000 + i}";
//
//     using var client = new HttpClient();
//
//     try
//     {
//         await client.PostAsync($"{baseUrl}/api/Auth/register",
//             new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Customer" }),
//             Encoding.UTF8, "application/json"));
//
//         var loginRes = await client.PostAsync($"{baseUrl}/api/Auth/login",
//             new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }),
//             Encoding.UTF8, "application/json"));
//
//         var json = await loginRes.Content.ReadAsStringAsync();
//         var doc = JsonDocument.Parse(json);
//         var token = doc.RootElement.GetProperty("accessToken").GetString();
//
//         if (token != null) customerTokens.Add(token);
//     }
//     catch { }
// }
//
// Console.WriteLine($"Drivers: {driverTokens.Count}, Customers: {customerTokens.Count}");
//
// string RandomDriver() => driverTokens[Random.Shared.Next(driverTokens.Count)];
// string RandomCustomer() => customerTokens[Random.Shared.Next(customerTokens.Count)];
//
// // ---------------- SIGNALR POOL ----------------
// var signalRConnections = new List<HubConnection>();
//
// async Task InitSignalR()
// {
//     foreach (var token in driverTokens.Take(50))
//     {
//         var conn = new HubConnectionBuilder()
//             .WithUrl($"{baseUrl}/hubs/rides", o =>
//             {
//                 o.AccessTokenProvider = () => Task.FromResult<string?>(token);
//             })
//             .Build();
//
//         try
//         {
//             await conn.StartAsync();
//             signalRConnections.Add(conn);
//         }
//         catch { }
//     }
// }
//
// // ---------------- SCENARIO 1: RIDE LIFECYCLE ----------------
// var rideLifecycle = Scenario.Create("real_taxi_flow", async context =>
// {
//     try
//     {
//         var customer = RandomCustomer();
//         var driver = RandomDriver();
//
//         var ride = await CreateRide(customer);
//         if (ride == null) return Response.Fail();
//
//         var available = await PostOk(driver, "/api/drivers/me/availability", new { isAvailable = true });
//         var locationUpdated = await PostOk(driver, "/api/drivers/me/location", new
//         {
//             latitude = ride.PickupLatitude,
//             longitude = ride.PickupLongitude
//         });
//         var accepted = await PostOk(driver, $"/api/rides/{ride.Id}/accept");
//         var arrived = await PostOk(driver, $"/api/rides/{ride.Id}/arrived", new
//         {
//             latitude = ride.PickupLatitude,
//             longitude = ride.PickupLongitude
//         });
//         var started = await PostOk(driver, $"/api/rides/{ride.Id}/start");
//         var completed = await PostOk(driver, $"/api/rides/{ride.Id}/complete");
//
//         return available && locationUpdated && accepted && arrived && started && completed
//             ? Response.Ok()
//             : Response.Fail();
//     }
//     catch
//     {
//         return Response.Fail();
//     }
// })
// .WithLoadSimulations(
//     Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20))
// );
//
// // ---------------- SCENARIO 2: LOCATION UPDATES ----------------
// var locationUpdates = Scenario.Create("driver_location_updates", async context =>
// {
//     try
//     {
//         var connections = signalRConnections
//             .OrderBy(_ => Random.Shared.Next())
//             .Take(10)
//             .ToList();
//
//         if (connections.Count == 0) return Response.Fail();
//
//         var tasks = connections.Select(async conn =>
//         {
//             try
//             {
//                 var latitude = 41.68m + (decimal)Random.Shared.NextDouble() * 0.07m;
//                 var longitude = 44.75m + (decimal)Random.Shared.NextDouble() * 0.1m;
//                 await conn.InvokeAsync("UpdateLocation", latitude, longitude);
//                 return true;
//             }
//             catch
//             {
//                 return false;
//             }
//         });
//
//         var results = await Task.WhenAll(tasks);
//         await Task.Delay(3000);
//
//         return results.All(x => x) ? Response.Ok() : Response.Fail();
//     }
//     catch
//     {
//         return Response.Fail();
//     }
// })
// .WithLoadSimulations(
//     Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20))
// );
//
// // ---------------- SCENARIO 3: CONCURRENT ACCEPT ----------------
// var concurrentAccept = Scenario.Create("race_condition", async context =>
// {
//     try
//     {
//         // Pick 5 distinct random drivers
//         var drivers = driverTokens.OrderBy(_ => Random.Shared.Next()).Take(5).ToList();
//
//         // Reset all 5 to available before the race
//         await Task.WhenAll(drivers.Select(token =>
//             ApiPost(token, "/api/drivers/me/availability", new { isAvailable = true })));
//
//         var ride = await CreateRide(RandomCustomer());
//         if (ride == null) return Response.Fail();
//
//         // All 5 race to accept simultaneously
//         var results = await Task.WhenAll(drivers.Select(async token =>
//         {
//             try
//             {
//                 var res = await ApiPost(token, $"/api/rides/{ride.Id}/accept");
//                 return (token, success: res.IsSuccessStatusCode);
//             }
//             catch { return (token, success: false); }
//         }));
//
//         var successCount = results.Count(r => r.success);
//
//         return successCount == 1 ? Response.Ok() : Response.Fail();
//     }
//     catch
//     {
//         return Response.Fail();
//     }
// })
// .WithLoadSimulations(
//     Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(5), during: TimeSpan.FromSeconds(30))
// );
//
// // ---------------- SCENARIO 4: SIGNALR STEADY ----------------
// var signalRSteady = Scenario.Create("driver_online_offline", async context =>
// {
//     var token = RandomDriver();
//
//     var conn = new HubConnectionBuilder()
//         .WithUrl($"{baseUrl}/hubs/rides", o =>
//         {
//             o.AccessTokenProvider = () => Task.FromResult<string?>(token);
//         })
//         .Build();
//
//     try
//     {
//         await conn.StartAsync();
//         await Task.Delay(30000);
//
//         return conn.State == HubConnectionState.Connected
//             ? Response.Ok()
//             : Response.Fail();
//     }
//     catch
//     {
//         return Response.Fail();
//     }
//     finally
//     {
//         await conn.DisposeAsync();
//     }
// })
// .WithLoadSimulations(
//     Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
// );
//
// //SCENARIO: Customer cancel race
// var customerCancelRace = Scenario.Create("customer_cancel_race", async context =>
// {
//     var customer = RandomCustomer();
//     var drivers = driverTokens.OrderBy(_ => Random.Shared.Next()).Take(5).ToList();
//
//     var ride = await CreateRide(customer);
//     if (ride == null) return Response.Fail();
//
//     await Task.WhenAll(drivers.Select(token =>
//         ApiPost(token, "/api/drivers/me/availability", new { isAvailable = true })));
//
//     var acceptTasks = drivers.Select(d => ApiPost(d, $"/api/rides/{ride.Id}/accept")).ToList();
//     var cancelTask = ApiPost(customer, $"/api/rides/{ride.Id}/cancel");
//
//     await Task.WhenAll(acceptTasks.Append(cancelTask));
//
//     var acceptSuccessCount = acceptTasks.Count(t => t.Result.IsSuccessStatusCode);
//     var cancelSucceeded = cancelTask.Result.IsSuccessStatusCode;
//
//     return acceptSuccessCount <= 1 && (acceptSuccessCount > 0 || cancelSucceeded)
//         ? Response.Ok()
//         : Response.Fail();
// })
// .WithLoadSimulations(
//     Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(5), during: TimeSpan.FromSeconds(30))
// );
//
// // SCENARIO: Cancelled ride cannot be accepted
// var cancelledRideCannotBeAccepted = Scenario.Create("cancelled_ride_cannot_be_accepted", async context =>
// {
//     var customer = RandomCustomer();
//     var driver = RandomDriver();
//
//     var ride = await CreateRide(customer);
//     if (ride == null) return Response.Fail();
//
//     var cancelled = await PostOk(customer, $"/api/rides/{ride.Id}/cancel");
//     var acceptAfterCancel = await PostOk(driver, $"/api/rides/{ride.Id}/accept");
//
//     return cancelled && !acceptAfterCancel ? Response.Ok() : Response.Fail();
// })
// .WithLoadSimulations(
//     Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(5), during: TimeSpan.FromSeconds(30))
// );
//
//
// //SCENARIO: Invalid transitions
//
// var invalidFlow = Scenario.Create("invalid_flow", async context =>
// {
//     var customer = RandomCustomer();
//     var driver = RandomDriver();
//
//     var ride = await CreateRide(customer);
//     if (ride == null) return Response.Fail();
//
//     // try start without accept
//     var res = await ApiPost(driver, $"/api/rides/{ride.Id}/start");
//
//     return res.IsSuccessStatusCode ? Response.Fail() : Response.Ok();
// })
// .WithLoadSimulations(
//     Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(5), during: TimeSpan.FromSeconds(30))
// );
//
// //SCENARIO: Double actions
//
// var duplicateActions = Scenario.Create("duplicate_actions", async context =>
// {
//     var customer = RandomCustomer();
//     var driver = RandomDriver();
//
//     var ride = await CreateRide(customer);
//     if (ride == null) return Response.Fail();
//
//     await PostOk(driver, "/api/drivers/me/availability", new { isAvailable = true });
//     await PostOk(driver, "/api/drivers/me/location", new
//     {
//         latitude = ride.PickupLatitude,
//         longitude = ride.PickupLongitude
//     });
//
//     var accepted = await PostOk(driver, $"/api/rides/{ride.Id}/accept");
//     var duplicateAccept = await PostOk(driver, $"/api/rides/{ride.Id}/accept");
//     var arrived = await PostOk(driver, $"/api/rides/{ride.Id}/arrived", new
//     {
//         latitude = ride.PickupLatitude,
//         longitude = ride.PickupLongitude
//     });
//     var started = await PostOk(driver, $"/api/rides/{ride.Id}/start");
//     var duplicateStart = await PostOk(driver, $"/api/rides/{ride.Id}/start");
//
//     return accepted && !duplicateAccept && arrived && started && !duplicateStart
//         ? Response.Ok()
//         : Response.Fail();
// })
// .WithLoadSimulations(
//     Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(5), during: TimeSpan.FromSeconds(30))
// );
//
// // ---------------- RUN ----------------
// await InitSignalR();
//
// NBomberRunner
//     .RegisterScenarios(
//         rideLifecycle,
//         locationUpdates,
//         concurrentAccept,
//         signalRSteady,
//         customerCancelRace,
//         cancelledRideCannotBeAccepted,
//         invalidFlow,
//         duplicateActions)
//     .Run();
//
// record CreatedRide(string Id, decimal PickupLatitude, decimal PickupLongitude);
//
//


using NBomber.Contracts;
using NBomber.CSharp;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

var baseUrl = "http://localhost:5143";
var httpTimeout = TimeSpan.FromSeconds(10);

// ---------------- GLOBAL HTTP ----------------
var handler = new SocketsHttpHandler
{
MaxConnectionsPerServer = 20000
};

var httpClient = new HttpClient(handler)
{
Timeout = httpTimeout
};

// ---------------- HELPERS ----------------
async Task<HttpResponseMessage> ApiPost(string token, string path, object? body = null)
{
var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{path}");
req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

if (body != null)
{
    var json = JsonSerializer.Serialize(body);
    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
}

return await httpClient.SendAsync(req);

}

async Task<bool> PostOk(string token, string path, object? body = null)
{
var res = await ApiPost(token, path, body);
return res.IsSuccessStatusCode;
}

async Task<CreatedRide?> CreateRide(string customerToken)
{
var pickupLatitude = 41.68m + (decimal)Random.Shared.NextDouble() * 0.07m;
var pickupLongitude = 44.75m + (decimal)Random.Shared.NextDouble() * 0.1m;

var res = await ApiPost(customerToken, "/api/rides", new
{
    pickupLatitude,
    pickupLongitude,
    destinationLatitude = pickupLatitude + 0.01m,
    destinationLongitude = pickupLongitude + 0.01m
});

if (!res.IsSuccessStatusCode) return null;

var json = await res.Content.ReadAsStringAsync();
var doc = JsonDocument.Parse(json);
var id = doc.RootElement.GetProperty("id").GetString();

return id == null ? null : new CreatedRide(id, pickupLatitude, pickupLongitude);

}

// ---------------- USERS ----------------
var driverTokens = new List<string>();
var customerTokens = new List<string>();

// pre-register + login (ერთჯერადად)
for (int i = 0; i < 15000; i++)
{
var phone = $"+995555{100000 + i}";

await httpClient.PostAsync($"{baseUrl}/api/Auth/register",
    new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Driver" }),
    Encoding.UTF8, "application/json"));

var loginRes = await httpClient.PostAsync($"{baseUrl}/api/Auth/login",
    new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }),
    Encoding.UTF8, "application/json"));

var json = await loginRes.Content.ReadAsStringAsync();
var doc = JsonDocument.Parse(json);
var token = doc.RootElement.GetProperty("accessToken").GetString();

if (token != null) driverTokens.Add(token);

}

for (int i = 0; i < 20000; i++)
{
var phone = $"+995555{200000 + i}";

await httpClient.PostAsync($"{baseUrl}/api/Auth/register",
    new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Customer" }),
    Encoding.UTF8, "application/json"));

var loginRes = await httpClient.PostAsync($"{baseUrl}/api/Auth/login",
    new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }),
    Encoding.UTF8, "application/json"));

var json = await loginRes.Content.ReadAsStringAsync();
var doc = JsonDocument.Parse(json);
var token = doc.RootElement.GetProperty("accessToken").GetString();

if (token != null) customerTokens.Add(token);

}

string RandomDriver() => driverTokens[Random.Shared.Next(driverTokens.Count)];
string RandomCustomer() => customerTokens[Random.Shared.Next(customerTokens.Count)];

// ---------------- SIGNALR ----------------
var signalRConnections = new List<HubConnection>();

async Task InitSignalR()
{
foreach (var token in driverTokens.Take(50))
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

// ---------------- SCENARIOS ----------------

// HEAVY LOCATION
var locationUpdates = Scenario.Create("location", async ctx =>
{
var token = RandomDriver();

var ok = await PostOk(token, "/api/drivers/me/location", new
{
    latitude = 41.7,
    longitude = 44.8
});

return ok ? Response.Ok() : Response.Fail();

})
.WithLoadSimulations(
Simulation.Inject(rate: 5000, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
Simulation.Inject(rate: 10000, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
Simulation.Inject(rate: 15000, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20))
);

// FULL FLOW
var rideLifecycle = Scenario.Create("ride_flow", async ctx =>
{
var customer = RandomCustomer();
var driver = RandomDriver();

var ride = await CreateRide(customer);
if (ride == null) return Response.Fail();

var ok =
    await PostOk(driver, "/api/drivers/me/availability", new { isAvailable = true }) &&
    await PostOk(driver, "/api/drivers/me/location", new { latitude = ride.PickupLatitude, longitude = ride.PickupLongitude }) &&
    await PostOk(driver, $"/api/rides/{ride.Id}/accept") &&
    await PostOk(driver, $"/api/rides/{ride.Id}/arrived", new { latitude = ride.PickupLatitude, longitude = ride.PickupLongitude }) &&
    await PostOk(driver, $"/api/rides/{ride.Id}/start") &&
    await PostOk(driver, $"/api/rides/{ride.Id}/complete");

return ok ? Response.Ok() : Response.Fail();

})
.WithLoadSimulations(
Simulation.Inject(rate: 1000, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20))
);

// RACE CONDITION
var concurrentAccept = Scenario.Create("race", async ctx =>
{
var drivers = driverTokens.OrderBy(_ => Random.Shared.Next()).Take(5).ToList();

var ride = await CreateRide(RandomCustomer());
if (ride == null) return Response.Fail();

var results = await Task.WhenAll(drivers.Select(d => ApiPost(d, $"/api/rides/{ride.Id}/accept")));

return results.Count(r => r.IsSuccessStatusCode) == 1
    ? Response.Ok()
    : Response.Fail();

})
.WithLoadSimulations(
Simulation.Inject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20))
);

// ---------------- RUN ----------------
await InitSignalR();

NBomberRunner
.RegisterScenarios(locationUpdates, rideLifecycle, concurrentAccept)
.Run();

record CreatedRide(string Id, decimal PickupLatitude, decimal PickupLongitude);
