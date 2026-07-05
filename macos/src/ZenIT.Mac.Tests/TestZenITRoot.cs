using ZenIT.Core.Configuration;

namespace ZenIT.Mac.Tests;

internal sealed class TestZenITRoot : IDisposable
{
    public TestZenITRoot()
    {
        Root = Path.Combine(Path.GetTempPath(), "ZenITTests", Guid.NewGuid().ToString("N"));
        Paths = ZenITPathProvider.CreateForTest(Root);
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public ZenITPathProvider Paths { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; test assertions should not depend on delete timing.
        }
    }
}
