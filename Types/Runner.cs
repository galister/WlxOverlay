using System.Diagnostics;

namespace X11Overlay.Types;

public static class Runner
{
    public static ProcessStartInfo? StartInfoFromArgs(string[]? argv)
    {
        if (argv == null || argv.Length == 0)
        {
            return null;
        }
        
        var psi = new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = argv[0],
        };
        foreach (var arg in argv.Skip(1)) 
            psi.ArgumentList.Add(arg);
        return psi;
    }

    public static void TryStart(ProcessStartInfo psi)
    {
        try
        {
            Process.Start(psi);
        }
        catch (Exception x)
        {
            Console.WriteLine($"[Err] {x.Message}");
        }
    }
    
}