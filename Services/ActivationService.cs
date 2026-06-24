using System.Net.Http.Json;

namespace PROXIMAMOP.Services;

public class ActivationService
{
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("https://api.streamflowapp.com")
    };

    public async Task<ActivationStatusResponse?> CheckStatusAsync(int userId, string deviceId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ActivationStatusResponse>(
                $"/api/subscriptions/status/{userId}?deviceId={Uri.EscapeDataString(deviceId)}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string Message)> SubmitRequestAsync(ActivationRequest request)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            content.Add(new StringContent(request.UserId.ToString()), "userId");
            content.Add(new StringContent(request.DeviceId ?? string.Empty), "deviceId");
            content.Add(new StringContent(request.FullName ?? string.Empty), "fullName");
            content.Add(new StringContent(request.PhoneNumber ?? string.Empty), "phoneNumber");
            content.Add(new StringContent(request.Country ?? string.Empty), "country");
            content.Add(new StringContent(request.PaymentMethod ?? string.Empty), "paymentMethod");
            content.Add(new StringContent(request.ContactMethod ?? string.Empty), "contactMethod");
            content.Add(new StringContent(request.ContactValue ?? string.Empty), "contactValue");
            content.Add(new StringContent(request.UserNote ?? string.Empty), "userNote");
            content.Add(new StringContent(request.PaymentReference ?? string.Empty), "paymentReference");

            if (!string.IsNullOrWhiteSpace(request.TransferImageFilePath) && File.Exists(request.TransferImageFilePath))
            {
                var stream = File.OpenRead(request.TransferImageFilePath);
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(GetContentType(request.TransferImageFilePath));

                content.Add(fileContent, "transferImage", Path.GetFileName(request.TransferImageFilePath));
            }

            var response = await _httpClient.PostAsync("/api/subscriptions/submit", content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return (true, body);

            return (false, $"HTTP {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}

public class ActivationStatusResponse
{
    public bool HasRequest { get; set; }
    public string Status { get; set; } = "";
    public bool IsApproved { get; set; }
    public bool IsExpired { get; set; }
    public bool CanEnterApp { get; set; }
    public bool CanSubmitNewRequest { get; set; }
    public string? AdminNote { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? ExpireAtUtc { get; set; }
    public int? DurationDays { get; set; }
}

public class ActivationRequest
{
    public int UserId { get; set; }
    public string DeviceId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Country { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public string ContactMethod { get; set; } = "";
    public string ContactValue { get; set; } = "";
    public string UserNote { get; set; } = "";
    public string PaymentReference { get; set; } = "";
    public string TransferImageFilePath { get; set; } = "";
}