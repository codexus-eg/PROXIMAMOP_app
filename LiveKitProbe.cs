using System.Reflection;
using System.Text;
using LiveKit;

namespace PROXIMAMOP;

public static class LiveKitProbe
{
    public static string Dump()
    {
        var sb = new StringBuilder();

        try
        {
            var assembly = typeof(Room).Assembly;

            sb.AppendLine("=== ASSEMBLY ===");
            sb.AppendLine(assembly.FullName);
            sb.AppendLine();

            var roomType = typeof(Room);

            sb.AppendLine("=== ROOM TYPE ===");
            sb.AppendLine(roomType.FullName);
            sb.AppendLine();

            sb.AppendLine("=== ROOM CONSTRUCTORS ===");
            foreach (var ctor in roomType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                sb.AppendLine(ctor.ToString());
            }

            sb.AppendLine();
            sb.AppendLine("=== ROOM STATIC METHODS ===");
            foreach (var method in roomType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                           .OrderBy(x => x.Name))
            {
                sb.AppendLine(FormatMethod(method));
            }

            sb.AppendLine();
            sb.AppendLine("=== ROOM INSTANCE METHODS ===");
            foreach (var method in roomType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                           .Where(x => !x.IsSpecialName)
                                           .OrderBy(x => x.Name))
            {
                sb.AppendLine(FormatMethod(method));
            }

            sb.AppendLine();
            sb.AppendLine("=== ROOM PROPERTIES ===");
            foreach (var prop in roomType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                         .OrderBy(x => x.Name))
            {
                sb.AppendLine($"{prop.PropertyType.Name} {prop.Name}");
            }

            sb.AppendLine();
            sb.AppendLine("=== ALL TYPES CONTAINING 'Participant' OR 'Track' ===");
            foreach (var type in assembly.GetTypes()
                                         .Where(t =>
                                             t.Name.Contains("Participant", StringComparison.OrdinalIgnoreCase) ||
                                             t.Name.Contains("Track", StringComparison.OrdinalIgnoreCase))
                                         .OrderBy(t => t.FullName))
            {
                sb.AppendLine(type.FullName);

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                           .Where(x => !x.IsSpecialName)
                                           .OrderBy(x => x.Name)
                                           .Take(25))
                {
                    sb.AppendLine("  " + FormatMethod(method));
                }

                sb.AppendLine();
            }

            sb.AppendLine("=== ALL TYPES CONTAINING 'Connect' OR 'Room' ===");
            foreach (var type in assembly.GetTypes()
                                         .Where(t =>
                                             t.Name.Contains("Connect", StringComparison.OrdinalIgnoreCase) ||
                                             t.Name.Contains("Room", StringComparison.OrdinalIgnoreCase))
                                         .OrderBy(t => t.FullName))
            {
                sb.AppendLine(type.FullName);

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                           .Where(x => !x.IsSpecialName)
                                           .OrderBy(x => x.Name)
                                           .Take(20))
                {
                    sb.AppendLine("  " + FormatMethod(method));
                }
                sb.AppendLine();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("PROBE ERROR:");
            sb.AppendLine(ex.ToString());
        }

        return sb.ToString();
    }

    private static string FormatMethod(MethodInfo method)
    {
        var parameters = string.Join(", ",
            method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));

        return $"{method.ReturnType.Name} {method.Name}({parameters})";
    }
}