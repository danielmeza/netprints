using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        try {
            Assembly ursa = Assembly.LoadFrom("/home/daniel-meza/Documents/repos/daniel/github/unreal/.worktrees/005-editor-shell-ui-polish/src/NetPrints.Editor/bin/Debug/net10.0/Ursa.dll");
            foreach (var type in ursa.GetTypes().Where(t => t.Name.Contains("Toast") || t.Name.Contains("MessageBox") || t.Name.Contains("Notification"))) {
                Console.WriteLine("Type: " + type.FullName);
                foreach(var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                    Console.WriteLine($"  {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
                }
            }
        } catch(Exception e) {
            Console.WriteLine(e);
        }
    }
}
