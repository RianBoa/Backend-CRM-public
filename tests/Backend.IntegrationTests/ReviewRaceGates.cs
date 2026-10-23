using backend.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Backend.IntegrationTests;

internal sealed class SaveGate(Func<DbContext, bool> matches) : SaveChangesInterceptor
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int used;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
        InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (data.Context is { } db && matches(db) && Interlocked.Exchange(ref used, 1) == 0)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
        return result;
    }
}

internal sealed class PasswordGate : IUserPasswordService, IDisposable
{
    private readonly UserPasswordService inner = new();
    private readonly ManualResetEventSlim release = new(false);
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string HashPassword(string password) => inner.HashPassword(password);
    public bool VerifyPassword(string? hash, string password)
    {
        var valid = inner.VerifyPassword(hash, password);
        Entered.TrySetResult();
        if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Password race gate was not released.");
        return valid;
    }
    public void Release() => release.Set();
    public void Dispose() => release.Dispose();
}
