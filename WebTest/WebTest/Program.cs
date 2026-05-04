using System;
using NBomber.Contracts;
using NBomber.CSharp;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

var baseUrl = "http://localhost:5143";

// ---------------- ინფრასტრუქტურული კონფიგურაცია ----------------
using var handler = new SocketsHttpHandler { 
    MaxConnectionsPerServer = 2000,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5) 
};
using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

var driverTokens = new ConcurrentBag<string>();
var customerTokens = new ConcurrentBag<string>();

// ---------------- მომხმარებლების მომზადება ----------------
async Task RegisterUsers(int driverCount, int customerCount)
{
    Console.WriteLine($"--- მზადება: {driverCount} დრაივერის და {customerCount} კლიენტის რეგისტრაცია ---");
    var tasks = new List<Task>();
    for (int i = 0; i < driverCount; i++) {
        var id = i;
        tasks.Add(Task.Run(async () => {
            var phone = $"+9955551{id:D5}";
            await httpClient.PostAsync($"{baseUrl}/api/Auth/register", new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Driver" }), Encoding.UTF8, "application/json"));
            var login = await httpClient.PostAsync($"{baseUrl}/api/Auth/login", new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }), Encoding.UTF8, "application/json"));
            if (login.IsSuccessStatusCode) {
                var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                driverTokens.Add(doc.RootElement.GetProperty("accessToken").GetString()!);
            }
        }));
    }
    for (int i = 0; i < customerCount; i++) {
        var id = i;
        tasks.Add(Task.Run(async () => {
            var phone = $"+9955552{id:D5}";
            await httpClient.PostAsync($"{baseUrl}/api/Auth/register", new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Customer" }), Encoding.UTF8, "application/json"));
            var login = await httpClient.PostAsync($"{baseUrl}/api/Auth/login", new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }), Encoding.UTF8, "application/json"));
            if (login.IsSuccessStatusCode) {
                var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                customerTokens.Add(doc.RootElement.GetProperty("accessToken").GetString()!);
            }
        }));
    }
    await Task.WhenAll(tasks);
}

await RegisterUsers(1000, 2000); // გავზარდეთ პული კონფლიქტების შესამცირებლად
var drivers = driverTokens.ToArray();
var customers = customerTokens.ToArray();

// ---------------- HTTP დამხმარე მეთოდი ----------------
async Task<HttpResponseMessage> Api(string token, HttpMethod method, string path, object? body = null)
{
    var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (body != null)
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
    
    return await httpClient.SendAsync(request);
}

// ---------------- სცენარები ----------------

// 1. FULL RIDE LIFECYCLE (Happy Path)
var fullFlow = Scenario.Create("full_flow", async ctx => {
    var customer = customers[Random.Shared.Next(customers.Length)];
    var driver = drivers[Random.Shared.Next(drivers.Length)];

    // 1. Create
    var res = await Api(customer, HttpMethod.Post, "/api/rides", new { 
        pickupLatitude = 41.71m, pickupLongitude = 44.80m, 
        destinationLatitude = 41.75m, destinationLongitude = 44.85m 
    });
    if (!res.IsSuccessStatusCode) return Response.Fail();
    var rideId = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();

    // 2. Accept (აქ ხშირად იქნება 400, თუ დრაივერი დაკავებულია - ამიტომ OK-ს ვაბრუნებთ მაინც)
    var resAcc = await Api(driver, HttpMethod.Post, $"/api/rides/{rideId}/accept");
    if (!resAcc.IsSuccessStatusCode) return Response.Ok(); // ვაიგნორებთ ბიზნეს კონფლიქტს

    // 3. Arrived
    await Api(driver, HttpMethod.Post, $"/api/rides/{rideId}/arrived", new { latitude = 41.71m, longitude = 44.80m });

    // 4. Start
    await Api(driver, HttpMethod.Post, $"/api/rides/{rideId}/start");

    // 5. Complete
    var resFinal = await Api(driver, HttpMethod.Post, $"/api/rides/{rideId}/complete");

    return resFinal.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
})
.WithLoadSimulations(Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2)));

// 2. THE ACCEPTANCE WAR (Race Condition Test)
var acceptRace = Scenario.Create("accept_race", async ctx => {
    var customer = customers[Random.Shared.Next(customers.Length)];
    
    var res = await Api(customer, HttpMethod.Post, "/api/rides", new { pickupLatitude = 41.71m, pickupLongitude = 44.80m, destinationLatitude = 41.75m, destinationLongitude = 44.85m });
    if (!res.IsSuccessStatusCode) return Response.Fail();
    var rideId = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();

    // 10 დრაივერი ერთდროულად ცდილობს აყვანას
    var tasks = Enumerable.Range(0, 10).Select(_ => Api(drivers[Random.Shared.Next(drivers.Length)], HttpMethod.Post, $"/api/rides/{rideId}/accept"));
    var results = await Task.WhenAll(tasks);

    // წარმატებაა, თუ ზუსტად 1-მა დრაივერმა მოიგო, სხვებმა კი წააგეს (400)
    bool oneWinner = results.Count(r => r.IsSuccessStatusCode) == 1;
    return oneWinner ? Response.Ok() : Response.Fail();
})
.WithLoadSimulations(Simulation.Inject(rate: 15, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2)));

// 3. BACKGROUND LOCATION UPDATES
var locationUpdates = Scenario.Create("location", async ctx => {
    var driver = drivers[Random.Shared.Next(drivers.Length)];
    var res = await Api(driver, HttpMethod.Post, "/api/drivers/me/location", new { latitude = 41.71m, longitude = 44.80m });
    return res.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
})
.WithLoadSimulations(Simulation.Inject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2)));

// 4. ACTIVE RIDE LOOKUP (Index Stress)
var activePolling = Scenario.Create("polling", async ctx => {
    var user = Random.Shared.Next(2) == 0 ? drivers[Random.Shared.Next(drivers.Length)] : customers[Random.Shared.Next(customers.Length)];
    var res = await Api(user, HttpMethod.Get, "/api/rides/active");
    return res.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
})
.WithLoadSimulations(Simulation.Inject(rate: 200, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2)));

// ---------------- გაშვება ----------------
NBomberRunner.RegisterScenarios(fullFlow, acceptRace, locationUpdates, activePolling).Run();