using System;
using System.Collections.Generic;

namespace FormWindApp;

/// <summary>Settings shared by the forms. Replace with your own settings storage.</summary>
internal static class AppState
{
    public static string UserName { get; set; } = "";

    public static bool DarkMode { get; set; }

    public static bool Notifications { get; set; } = true;

    /// <summary>Raised when <see cref="DarkMode"/> changes so open forms can follow.</summary>
    public static event EventHandler? ThemeChanged;

    public static void SetDarkMode(bool dark)
    {
        if (DarkMode == dark) return;
        DarkMode = dark;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }
}

internal sealed class Customer
{
    public Customer(string name, string email, string company, string city, string status)
    {
        Name = name;
        Email = email;
        Company = company;
        City = city;
        Status = status;
    }

    public string Name { get; }
    public string Email { get; }
    public string Company { get; }
    public string City { get; }
    public string Status { get; }

    public static IReadOnlyList<Customer> Samples { get; } = new[]
    {
        new Customer("Ada Lovelace", "ada@example.com", "Analytical Engines", "London", "Active"),
        new Customer("Alan Turing", "alan@example.com", "Bletchley Park", "Manchester", "Active"),
        new Customer("Grace Hopper", "grace@example.com", "Navy Labs", "Arlington", "Pending"),
        new Customer("Katherine Johnson", "katherine@example.com", "Langley Research", "Hampton", "Active"),
        new Customer("Linus Torvalds", "linus@example.com", "Kernel Works", "Portland", "Inactive"),
        new Customer("Margaret Hamilton", "margaret@example.com", "Apollo Software", "Boston", "Active"),
        new Customer("Tim Berners-Lee", "tim@example.com", "Web Consortium", "Geneva", "Pending"),
    };
}
