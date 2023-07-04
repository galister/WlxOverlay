namespace WlxOverlay.Core.Subsystem;

public interface ISubsystem : IDisposable
{
    void Initialize();
    void Update();
}