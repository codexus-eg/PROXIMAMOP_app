namespace PROXIMAMOP.Services;

public static class ServiceHelper
{
    public static IServiceProvider? Services { get; set; }

    public static T? GetService<T>() where T : class
    {
        return Services?.GetService(typeof(T)) as T;
    }
}