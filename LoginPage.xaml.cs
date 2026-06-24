using Microsoft.Maui.Storage;

namespace PROXIMAMOP.Pages;

public partial class LoginPage : ContentPage
{
    private const string SecretEmail = "admin@proxima.com";
    private const string SecretPassword = "AAA-bbb-usfh25%jlvjve25pc@@##6258";

    public LoginPage()
    {
        InitializeComponent();
    }

    private void OnLoginClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        ErrorLabel.Text = string.Empty;

        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorLabel.Text = "Please enter your email.";
            ErrorLabel.IsVisible = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorLabel.Text = "Please enter your password.";
            ErrorLabel.IsVisible = true;
            return;
        }

        Preferences.Set("IsLoggedIn", true);
        Preferences.Set("UserEmail", email);

        if (email.Equals(SecretEmail, StringComparison.OrdinalIgnoreCase)
            && password == SecretPassword)
        {
            Preferences.Set("IsActivated", true);
            App.OpenApp();
            return;
        }

        var alreadyActivated = Preferences.Get("IsActivated", false);

        if (alreadyActivated)
        {
            App.OpenApp();
            return;
        }

        App.OpenActivation();
    }
}