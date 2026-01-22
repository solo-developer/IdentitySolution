using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

// Bypass SSL certificate validation for development (localhost)
var handler = new HttpClientHandler();
handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

using var client = new HttpClient(handler);
var authority = "https://localhost:7242"; // Check port, uses https so 7242

Console.WriteLine("Recovery Project Sample - Connecting to User Management...");

// 1. Get Token
Console.WriteLine($"Requesting token from {authority}/connect/token...");

var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{authority}/connect/token")
{
    Content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "client_credentials",
        ["client_id"] = "recovery-project",
        ["client_secret"] = "recovery-secret",
        ["scope"] = "api roles" 
    })
};

try 
{
    var tokenResponse = await client.SendAsync(tokenRequest);
    if (!tokenResponse.IsSuccessStatusCode)
    {
        Console.WriteLine($"Error getting token: {tokenResponse.StatusCode}");
        Console.WriteLine(await tokenResponse.Content.ReadAsStringAsync());
        return;
    }

    var tokenContent = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
    var accessToken = tokenContent?.AccessToken;

    if (string.IsNullOrEmpty(accessToken))
    {
        Console.WriteLine("Error: Access token is empty.");
        return;
    }

    Console.WriteLine("Successfully retrieved access token.");

    // 2. Call UserManagement API
    Console.WriteLine("Calling UserManagement/users...");
    
    // Reuse client with new headers or create new request
    var request = new HttpRequestMessage(HttpMethod.Get, $"{authority}/api/UserManagement/users");
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    var usersResponse = await client.SendAsync(request);

    if (usersResponse.IsSuccessStatusCode)
    {
        var users = await usersResponse.Content.ReadAsStringAsync();
        Console.WriteLine("Successfully connected to UserManagement!");
        Console.WriteLine($"Users data: {users}");
    }
    else
    {
        Console.WriteLine($"Error calling API: {usersResponse.StatusCode}");
        Console.WriteLine(await usersResponse.Content.ReadAsStringAsync());
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Exception: {ex.Message}");
    if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
}

record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType
);
