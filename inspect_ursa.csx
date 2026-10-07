using System.Reflection;
using System;
using System.Linq;

Assembly ursa = Assembly.LoadFrom("src/NetPrints.Editor/bin/Debug/net10.0/Ursa.dll");
var toastType = ursa.GetType("Ursa.Controls.Toast");
if (toastType != null) {
    foreach(var method in toastType.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
        Console.WriteLine($"{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
    }
} else {
    Console.WriteLine("Toast type not found in Ursa.Controls.");
    foreach (var type in ursa.GetTypes().Where(t => t.Name.Contains("Toast"))) {
        Console.WriteLine(type.FullName);
    }
}
