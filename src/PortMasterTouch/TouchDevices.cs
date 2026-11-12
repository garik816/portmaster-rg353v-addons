using System;
using System.IO;
using System.Linq;

namespace PortMasterTouch;

public static class TouchDevices
{
    public static string? FindTouchDevice()
    {
        foreach (string block in File.ReadAllText("/proc/bus/input/devices").Split("\n\n"))
            if (block.Contains("Touchscreen", StringComparison.OrdinalIgnoreCase))
            {
                const string prefix = "H: Handlers=";
                var line = block.Split('\n').FirstOrDefault(p => p.StartsWith(prefix));
                // The first handler may be event1 itself, with no kbd/js token.
                var name = line?.Substring(prefix.Length).Split(' ')
                    .FirstOrDefault(p => p.StartsWith("event"));
                if (name != null) return "/dev/input/" + name;
            }
        return null;
    }
}
