namespace MonitorMover.Ui;

public enum UpdateState
{
    NotChecked,
    Checking,
    UpToDate,
    Available,
    Failed,
}

/// <summary>What the Updates tab's status card shows after a check.</summary>
public sealed record UpdateStatus(UpdateState State, string Title, string Detail);

/// <summary>
/// Remembers the outcome of the most recent update check, so Settings opened later shows that result:
/// the latest-version text only when the check found nothing newer, a newer release with its Update action,
/// or the failure. Each check replaces the previous result.
/// </summary>
public sealed class UpdateStatusTracker(string currentVersion)
{
    public UpdateStatus Status { get; private set; } = new(UpdateState.NotChecked, "Not checked yet",
        $"Version {currentVersion} · Use Check for updates to look now");

    /// <summary>The newer release the last check found; null unless <see cref="Status"/> is Available.</summary>
    public AppUpdate? Available { get; private set; }

    public UpdateStatus RecordCurrent(DateTime checkedAt)
    {
        Available = null;
        return Status = new UpdateStatus(UpdateState.UpToDate, UpdateMessages.Latest(currentVersion), Checked(checkedAt));
    }

    public UpdateStatus RecordNewer(AppUpdate update, DateTime checkedAt)
    {
        Available = update;
        return Status = new UpdateStatus(UpdateState.Available, $"Version {update.Version.ToString(3)} is available",
            $"You have v{currentVersion} · {Checked(checkedAt)}");
    }

    public UpdateStatus RecordFailed(string reason, DateTime checkedAt)
    {
        Available = null;
        return Status = new UpdateStatus(UpdateState.Failed, "Could not check for updates",
            $"Version {currentVersion} · {reason} · {Checked(checkedAt)}");
    }

    private static string Checked(DateTime at) => $"Last checked {at:yyyy-MM-dd HH:mm}";
}
