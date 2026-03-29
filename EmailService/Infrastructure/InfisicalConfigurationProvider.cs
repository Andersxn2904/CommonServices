using System.Net.Http.Headers;
using System.Text.Json;
using EmailService.Configurations;
using Microsoft.Extensions.Configuration;

namespace EmailService.Infrastructure;

public class InfisicalConfigurationSource(InfisicalOptions opts) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new InfisicalConfigurationProvider(opts);
}

public class InfisicalConfigurationProvider(InfisicalOptions opts) : ConfigurationProvider
{
    public override void Load()
    {
        if (!opts.Enabled) return;
        LoadAsync().GetAwaiter().GetResult();
    }

    private async Task LoadAsync()
    {
        using var http = new HttpClient();
        http.BaseAddress = new Uri(opts.SiteUrl.TrimEnd('/'));

        var accessToken = await AuthenticateAsync(http);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var secrets = await FetchSecretsAsync(http);
        Data = secrets;
    }

    private async Task<string> AuthenticateAsync(HttpClient http)
    {
        var payload = new { clientId = opts.ClientId, clientSecret = opts.ClientSecret };
        var response = await http.PostAsJsonAsync("/api/v1/auth/universal-auth/login", payload);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Infisical: no se recibió accessToken.");
    }

    private async Task<Dictionary<string, string?>> FetchSecretsAsync(HttpClient http)
    {
        var url = $"/api/v3/secrets/raw?workspaceId={opts.ProjectId}&environment={opts.Environment}&secretPath={Uri.EscapeDataString(opts.SecretPath)}";
        var response = await http.GetAsync(url);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var secret in json.GetProperty("secrets").EnumerateArray())
        {
            var key = secret.GetProperty("secretKey").GetString();
            var value = secret.GetProperty("secretValue").GetString();

            if (key is not null)
                result[key.Replace("__", ":")] = value;
        }

        return result;
    }
}
