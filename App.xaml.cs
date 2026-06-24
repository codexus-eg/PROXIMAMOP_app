using Microsoft.Maui.Storage;
using PROXIMAMOP.Pages;

namespace PROXIMAMOP;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        var isLoggedIn = Preferences.Get("IsLoggedIn", false);
        var isActivated = Preferences.Get("IsActivated", false);

        if (!isLoggedIn)
            MainPage = new NavigationPage(new LoginPage());
        else if (!isActivated)
            MainPage = new NavigationPage(new ActivationGatePage());
        else
            MainPage = new AppShell();
    }

    public static void OpenActivation()
    {
        Current!.MainPage = new NavigationPage(new ActivationGatePage());
    }

    public static void OpenApp()
    {
        Current!.MainPage = new AppShell();
    }
}