namespace WlxOverlay.Core;

public static class TaskScheduler
{
    private static readonly Queue<(DateTime notBefore, Action action)> _scheduledTasks = new();
    private static readonly object _lockObject = new();

    public static void ScheduleTask(DateTime notBefore, Action action)
    {
        lock (_lockObject)
            _scheduledTasks.Enqueue((notBefore, action));
    }

    public static bool TryDequeue(out Action action)
    {
        lock (_lockObject)
            if (_scheduledTasks.TryPeek(out var task) && task.notBefore < DateTime.UtcNow)
            {
                _scheduledTasks.Dequeue();
                action = task.action;
                return true;
            }
        action = null!;
        return false;
    }
}