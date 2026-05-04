using NBomber.Contracts;
using NBomber.CSharp;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Collections.Concurrent;

var baseUrl = "http://localhost:5143";

// ---------------- GLOBAL SETUP ----------------
using var handler = new SocketsHttpHandler { MaxConnectionsPerServer = 2000 };
using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

// ვიყენებთ ConcurrentBag-ს მრავალ ნაკადში უსაფრთხო მუშაობისთვის
var driverTokens = new ConcurrentBag<string>();
var customerTokens = new ConcurrentBag<string>();

// ---------------- REGISTRATION HELPER ----------------
async Task RegisterUsers(int driverCount, int customerCount)
{
    Console.WriteLine($"--- მზადება: {driverCount} დრაივერის და {customerCount} კლიენტის რეგისტრაცია ---");

    var tasks = new List<Task>();

    // დრაივერების რეგისტრაცია
    for (int i = 0; i < driverCount; i++)
    {
        var id = i;
        tasks.Add(Task.Run(async () => {
            var phone = $"+9955551{id:D5}";
            // ვცდილობთ რეგისტრაციას (თუ უკვე არსებობს, ლოგინი მაინც იმუშავებს)
            await httpClient.PostAsync($"{baseUrl}/api/Auth/register", 
                new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Driver" }), Encoding.UTF8, "application/json"));
            
            var login = await httpClient.PostAsync($"{baseUrl}/api/Auth/login", 
                new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }), Encoding.UTF8, "application/json"));

            if (login.IsSuccessStatusCode) {
                var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                driverTokens.Add(doc.RootElement.GetProperty("accessToken").GetString()!);
            }
        }));
    }

    // კლიენტების რეგისტრაცია
    for (int i = 0; i < customerCount; i++)
    {
        var id = i;
        tasks.Add(Task.Run(async () => {
            var phone = $"+9955552{id:D5}";
            await httpClient.PostAsync($"{baseUrl}/api/Auth/register", 
                new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone, role = "Customer" }), Encoding.UTF8, "application/json"));
            
            var login = await httpClient.PostAsync($"{baseUrl}/api/Auth/login", 
                new StringContent(JsonSerializer.Serialize(new { phoneNumber = phone }), Encoding.UTF8, "application/json"));

            if (login.IsSuccessStatusCode) {
                var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                customerTokens.Add(doc.RootElement.GetProperty("accessToken").GetString()!);
            }
        }));
    }

    await Task.WhenAll(tasks);
}

// 1. ჯერ ვავსებთ სიებს
await RegisterUsers(500, 1000);

// 2. 🔥 SAFETY CHECK: თუ სიები ცარიელია, ტესტს არ ვიწყებთ!
if (driverTokens.IsEmpty || customerTokens.IsEmpty)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"კრიტიკული შეცდომა: მომხმარებლები ვერ დარეგისტრირდნენ! Drivers: {driverTokens.Count}, Customers: {customerTokens.Count}");
    Console.ResetColor();
    return;
}

Console.WriteLine($"რეგისტრაცია დასრულდა: {driverTokens.Count} დრაივერი, {customerTokens.Count} კლიენტი.");

// სიებს ვაქცევთ მასივებად სწრაფი ინდექსაციისთვის
var drivers = driverTokens.ToArray();
var customers = customerTokens.ToArray();

// ---------------- HELPERS ----------------
async Task<HttpResponseMessage> ApiPost(string token, string path, object? body = null)
{
    var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{path}");
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (body != null)
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
    return await httpClient.SendAsync(req);
}

// ---------------- SCENARIOS ----------------

var locationUpdates = Scenario.Create("location_updates", async ctx => {
    var driver = drivers[Random.Shared.Next(drivers.Length)];
    var res = await ApiPost(driver, "/api/drivers/me/location", new { latitude = 41.71m, longitude = 44.80m });
    return res.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
})
.WithLoadSimulations(Simulation.Inject(rate: 400, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(1)));

var taxiLifeCycle = Scenario.Create("taxi_lifecycle", async ctx => {
    var customer = customers[Random.Shared.Next(customers.Length)];
    var driver = drivers[Random.Shared.Next(drivers.Length)];

    var resCreate = await ApiPost(customer, "/api/rides", new { pickupLatitude = 41.7m, pickupLongitude = 44.8m, destinationLatitude = 41.71m, destinationLongitude = 44.81m });
    if (!resCreate.IsSuccessStatusCode) return Response.Fail();
    var rideId = JsonDocument.Parse(await resCreate.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();

    var resAcc = await ApiPost(driver, $"/api/rides/{rideId}/accept");
    if (!resAcc.IsSuccessStatusCode) return Response.Fail();

    var resComp = await ApiPost(driver, $"/api/rides/{rideId}/complete");
    return resComp.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
})
.WithLoadSimulations(Simulation.Inject(rate: 60, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(1)));

// ---------------- RUN ----------------
NBomberRunner.RegisterScenarios(locationUpdates, taxiLifeCycle).Run();