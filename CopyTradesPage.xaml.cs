using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PROXIMAMOP.Pages;

public partial class CopyTradesPage : ContentPage
{
    private const string BaseUrl = "http://195.3.223.75:5003";
    private const string UserIdPreferenceKey = "copy_trade_user_id";

    private readonly HttpClient _httpClient = new();
    private FileResult? _selectedReceiptFile;
    private string _userId = string.Empty;
    private bool _isBusy;
    private string _currentStatus = string.Empty;

    public CopyTradesPage()
    {
        InitializeComponent();
        InitializeUserId();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshStatusAsync();
    }

    private void InitializeUserId()
    {
        _userId = Preferences.Default.Get(UserIdPreferenceKey, string.Empty);

        if (string.IsNullOrWhiteSpace(_userId))
        {
            _userId = Guid.NewGuid().ToString("N");
            Preferences.Default.Set(UserIdPreferenceKey, _userId);
        }
    }

    private async void OnPickReceiptClicked(object sender, EventArgs e)
    {
        if (_isBusy)
            return;

        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync();

            if (result is null)
                return;

            _selectedReceiptFile = result;
            ReceiptFileNameLabel.Text = result.FileName;

            await using var stream = await result.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();

            ReceiptPreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_isBusy)
            return;

        try
        {
            var validationError = ValidateForm();
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                await DisplayAlert("Validation", validationError, "OK");
                return;
            }

            if (string.Equals(_currentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("Info", "Your current request is still pending.", "OK");
                return;
            }

            SetBusyState(true);

            using var form = new MultipartFormDataContent
            {
                { new StringContent(_userId), "UserId" },
                { new StringContent(FirstNameEntry.Text!.Trim()), "FirstName" },
                { new StringContent(LastNameEntry.Text!.Trim()), "LastName" },
                { new StringContent(AccountNumberEntry.Text!.Trim()), "AccountNumber" },
                { new StringContent(PasswordEntry.Text!.Trim()), "Password" },
                { new StringContent(BrokerEntry.Text!.Trim()), "Broker" },
                { new StringContent(CountryEntry.Text!.Trim()), "Country" },
                { new StringContent(AccountValueEntry.Text!.Trim()), "AccountValue" },
                { new StringContent(PhoneNumberEntry.Text!.Trim()), "PhoneNumber" },
                { new StringContent(EmailEntry.Text!.Trim()), "Email" },
                { new StringContent(MessageEditor.Text?.Trim() ?? string.Empty), "Message" }
            };

            await using var fileStream = await _selectedReceiptFile!.OpenReadAsync();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(_selectedReceiptFile.FileName));
            form.Add(fileContent, "ReceiptImage", _selectedReceiptFile.FileName);

            var response = await _httpClient.PostAsync($"{BaseUrl}/api/copytrades/submit", form);
            var responseText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Success", "Request submitted successfully.", "OK");
                await RefreshStatusAsync();
                return;
            }

            if ((int)response.StatusCode == 409)
            {
                await DisplayAlert("Info", "You already have an active pending request.", "OK");
                await RefreshStatusAsync();
                return;
            }

            var message = ExtractMessage(responseText);
            await DisplayAlert("Server Error", string.IsNullOrWhiteSpace(message) ? responseText : message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async void OnRefreshStatusClicked(object sender, EventArgs e)
    {
        if (_isBusy)
            return;

        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            SetBusyState(true);

            var response = await _httpClient.GetAsync($"{BaseUrl}/api/copytrades/status/{_userId}");
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _currentStatus = string.Empty;
                StatusValueLabel.Text = "Status check failed";
                StatusValueLabel.TextColor = Colors.OrangeRed;
                StatusMessageLabel.Text = "Unable to load request status.";
                AdminNoteLabel.Text = "No admin note";
                UpdateSubmitAvailability(true);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var hasRequest = root.TryGetProperty("hasRequest", out var hasRequestElement) &&
                             hasRequestElement.GetBoolean();

            if (!hasRequest)
            {
                _currentStatus = string.Empty;
                StatusValueLabel.Text = "No request found";
                StatusValueLabel.TextColor = Colors.White;
                StatusMessageLabel.Text = "You can submit a new request.";
                AdminNoteLabel.Text = "No admin note";
                UpdateSubmitAvailability(true);
                return;
            }

            var status = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "Unknown"
                : "Unknown";

            _currentStatus = status;

            var adminNote = root.TryGetProperty("adminNote", out var adminNoteElement)
                ? adminNoteElement.GetString()
                : null;

            StatusValueLabel.Text = $"Current status: {status}";
            ApplyStatusColor(status);
            StatusMessageLabel.Text = BuildStatusMessage(status);
            AdminNoteLabel.Text = string.IsNullOrWhiteSpace(adminNote)
                ? "No admin note"
                : $"Admin note: {adminNote}";

            UpdateSubmitAvailability(!string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _currentStatus = string.Empty;
            StatusValueLabel.Text = "Status check failed";
            StatusValueLabel.TextColor = Colors.OrangeRed;
            StatusMessageLabel.Text = ex.Message;
            AdminNoteLabel.Text = "No admin note";
            UpdateSubmitAvailability(true);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private void ApplyStatusColor(string status)
    {
        if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            StatusValueLabel.TextColor = Color.FromArgb("#22C55E");
            return;
        }

        if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            StatusValueLabel.TextColor = Color.FromArgb("#EF4444");
            return;
        }
        if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            StatusValueLabel.TextColor = Color.FromArgb("#F59E0B");
            return;
        }

        StatusValueLabel.TextColor = Colors.White;
    }

    private static string BuildStatusMessage(string status)
    {
        if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
            return "This request is approved. You can still submit a new request if needed.";

        if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
            return "This request was rejected. You can update the form and submit again.";

        if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
            return "Your request is under review. Please wait for an update.";

        return "Status is available.";
    }

    private void UpdateSubmitAvailability(bool isEnabled)
    {
        SubmitButton.IsEnabled = isEnabled && !_isBusy;
        SubmitButton.Opacity = SubmitButton.IsEnabled ? 1.0 : 0.6;
    }

    private void SetBusyState(bool isBusy)
    {
        _isBusy = isBusy;

        LoadingContainer.IsVisible = isBusy;

        RefreshButton.IsEnabled = !isBusy;
        RefreshButton.Opacity = RefreshButton.IsEnabled ? 1.0 : 0.6;

        UpdateSubmitAvailability(!string.Equals(_currentStatus, "Pending", StringComparison.OrdinalIgnoreCase));

        FirstNameEntry.IsEnabled = !isBusy;
        LastNameEntry.IsEnabled = !isBusy;
        AccountNumberEntry.IsEnabled = !isBusy;
        PasswordEntry.IsEnabled = !isBusy;
        BrokerEntry.IsEnabled = !isBusy;
        CountryEntry.IsEnabled = !isBusy;
        AccountValueEntry.IsEnabled = !isBusy;
        PhoneNumberEntry.IsEnabled = !isBusy;
        EmailEntry.IsEnabled = !isBusy;
        MessageEditor.IsEnabled = !isBusy;
    }

    private string? ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(FirstNameEntry.Text))
            return "First Name is required.";

        if (string.IsNullOrWhiteSpace(LastNameEntry.Text))
            return "Last Name is required.";

        if (string.IsNullOrWhiteSpace(AccountNumberEntry.Text))
            return "Account Number is required.";

        if (string.IsNullOrWhiteSpace(PasswordEntry.Text))
            return "Password is required.";

        if (string.IsNullOrWhiteSpace(BrokerEntry.Text))
            return "Broker is required.";

        if (string.IsNullOrWhiteSpace(CountryEntry.Text))
            return "Country is required.";

        if (string.IsNullOrWhiteSpace(AccountValueEntry.Text))
            return "Account Value is required.";

        if (!decimal.TryParse(AccountValueEntry.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var accountValue) || accountValue <= 0)
            return "Account Value must be a valid positive number.";

        if (string.IsNullOrWhiteSpace(PhoneNumberEntry.Text))
            return "Phone Number is required.";

        if (string.IsNullOrWhiteSpace(EmailEntry.Text))
            return "Email is required.";

        if (!IsValidEmail(EmailEntry.Text.Trim()))
            return "Email format is invalid.";

        if (_selectedReceiptFile is null)
            return "Receipt image is required.";

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
    private static string? ExtractMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("message", out var messageElement))
                return messageElement.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}