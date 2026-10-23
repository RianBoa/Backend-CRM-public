using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace Backend.IntegrationTests;

// Explicit opt-in fallback for developers with PostgreSQL binaries but no Docker.
internal sealed class LocalPostgres(string binaries) : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"ffa_pg_it_{Guid.NewGuid():N}");
    private bool started;
    public string ConnectionString { get; private set; } = null!;

    public async Task StartAsync()
    {
        Directory.CreateDirectory(root);
        try
        {
            await RunAsync("initdb", "-D", Path.Combine(root, "data"), "-U", "postgres", "-A", "trust", "--no-locale", "-E", "UTF8");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            await RunAsync("pg_ctl", "-D", Path.Combine(root, "data"), "-l", Path.Combine(root, "postgres.log"),
                "-o", $"-F -h 127.0.0.1 -p {port}", "-w", "start");
            started = true;
            ConnectionString = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1", Port = port, Database = "postgres", Username = "postgres", Timeout = 10
            }.ConnectionString;
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    private async Task RunAsync(string executable, params string[] args)
    {
        var name = OperatingSystem.IsWindows() ? executable + ".exe" : executable;
        var info = new ProcessStartInfo(Path.Combine(binaries, name))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Cannot start {executable}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"{executable} failed: {output}\n{error}");
    }

    public async ValueTask DisposeAsync()
    {
        if (started)
        {
            await RunAsync("pg_ctl", "-D", Path.Combine(root, "data"), "-m", "fast", "-w", "stop");
            started = false;
        }
        // Only the generated directory under the OS temp directory is owned by this fixture.
        var resolved = Path.GetFullPath(root);
        if (Path.GetDirectoryName(resolved) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) ||
            !Path.GetFileName(resolved).StartsWith("ffa_pg_it_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a directory not owned by the test fixture.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }
}
