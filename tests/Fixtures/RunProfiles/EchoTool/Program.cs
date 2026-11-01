using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

var utf8 = new UTF8Encoding(false);
using var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
string? outFile = Environment.GetEnvironmentVariable("ECHO_TOOL_OUT");

void Emit(string line)
{
    stdout.WriteLine(line);
    if (!string.IsNullOrEmpty(outFile))
    {
        File.AppendAllText(outFile, line + Environment.NewLine, utf8);
    }
}

HashSet<string> modes = (Environment.GetEnvironmentVariable("ECHO_TOOL_MODE") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.Ordinal);

Emit($"pid={Environment.ProcessId}");
Emit($"argc={args.Length}");
for (int i = 0; i < args.Length; i++)
{
    Emit($"argv[{i}]={args[i]}");
}

Emit($"cwd={Environment.CurrentDirectory}");
string names = Environment.GetEnvironmentVariable("ECHO_TOOL_ENV") ?? "NETPRINTS_ECHO";
foreach (string name in names.Split(';', StringSplitOptions.RemoveEmptyEntries))
{
    string? value = Environment.GetEnvironmentVariable(name);
    Emit(value is null ? $"env:{name} unset" : $"env:{name}={value}");
}

using PosixSignalRegistration? ignoreTerm = modes.Contains("ignore-sigterm")
    ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => context.Cancel = true)
    : null;

if (modes.Contains("spawn-child"))
{
    var childStart = new ProcessStartInfo(Environment.ProcessPath ?? "dotnet") { UseShellExecute = false };
    childStart.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    childStart.Environment["ECHO_TOOL_MODE"] = "sleep,ignore-sigterm";
    childStart.Environment.Remove("ECHO_TOOL_OUT");
    using Process? child = Process.Start(childStart);
    Emit($"child={child?.Id}");
}

if (modes.Contains("read-line"))
{
    Emit("ready");
    using var stdin = new StreamReader(Console.OpenStandardInput(), utf8);
    while (await stdin.ReadLineAsync() is { } line)
    {
        Emit($"stdin={line}");
    }

    Emit("stdin-eof");
}
else if (modes.Contains("sleep"))
{
    Emit("ready");
    Thread.Sleep(Timeout.Infinite);
}

return int.TryParse(Environment.GetEnvironmentVariable("ECHO_TOOL_EXIT"), out int exit) ? exit : 0;
