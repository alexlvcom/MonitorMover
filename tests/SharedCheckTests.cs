using MonitorMover;

internal static class SharedCheckTests
{
    public static async Task RunAsync()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }

        var gate = new SharedCheck<string?>();
        var pending = new TaskCompletionSource<string?>();
        var runs = 0;
        var background = gate.RunAsync(() => { runs++; return pending.Task; });
        var manual = gate.RunAsync(() => { runs++; return Task.FromResult<string?>("second"); });
        Check(gate.IsRunning, "background check is tracked while in flight");
        pending.SetResult("1.3.2");
        Check(await background == "1.3.2" && await manual == "1.3.2" && runs == 1, "manual check joins the running check instead of being refused");
        Check(!gate.IsRunning, "finished check is no longer running");

        Check(await gate.RunAsync(() => Task.FromResult<string?>("sync")) == "sync" && !gate.IsRunning, "synchronous check is not remembered as running");
        var failed = false;
        try
        {
            await gate.RunAsync(async () => { await Task.Yield(); throw new HttpRequestException("offline"); });
        }
        catch (HttpRequestException) { failed = true; }
        Check(failed && !gate.IsRunning, "failed check releases the guard");
        Check(await gate.RunAsync(async () => { await Task.Yield(); return "again"; }) == "again", "next check runs after a failure");
    }
}
