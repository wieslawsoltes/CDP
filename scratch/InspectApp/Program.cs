using System;
using System.Reflection;
using Avalonia.Input;
using Avalonia.Input.Raw;

namespace InspectApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== DETAILED WHEEL & ENUM INSPECTION ===");
        
        // Search for types with "Wheel" or "Scroll"
        foreach (var type in typeof(RawPointerEventArgs).Assembly.GetTypes())
        {
            if (type.FullName != null && (type.FullName.Contains("Wheel") || type.FullName.Contains("Scroll")))
            {
                Console.WriteLine($"Found Type: {type.FullName}");
                PrintTypeInfo(type);
            }
        }

        // Print RawPointerEventType enum values
        Console.WriteLine("\nRawPointerEventType enum values:");
        foreach (var val in Enum.GetValues(typeof(RawPointerEventType)))
        {
            Console.WriteLine($"  {val}");
        }

        // Print RawInputModifiers enum values
        Console.WriteLine("\nRawInputModifiers enum values:");
        foreach (var val in Enum.GetValues(typeof(RawInputModifiers)))
        {
            Console.WriteLine($"  {val}");
        }
    }

    static void PrintTypeInfo(Type type)
    {
        Console.WriteLine("Constructors:");
        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var ctor in ctors)
        {
            var prms = ctor.GetParameters();
            var prmList = string.Join(", ", Array.ConvertAll(prms, p => $"{p.ParameterType.Name} {p.Name}"));
            var access = ctor.IsPublic ? "public" : ctor.IsPrivate ? "private" : "internal/protected";
            Console.WriteLine($"  {access} .ctor({prmList})");
        }

        Console.WriteLine("Properties:");
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (var prop in props)
        {
            Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name} (get: {prop.CanRead}, set: {prop.CanWrite})");
        }
    }
}
