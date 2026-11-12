using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;

namespace PortMasterTouch;

public sealed class ModEntry : Mod
{
    private static ModEntry? Instance;
    private readonly ConcurrentQueue<TouchSample> Samples = new();
    private FieldInfo MouseField = null!;
    private TouchSample Current;
    private int XMax = 639, YMax = 479, XMin, YMin;
    private int LoggedPresses;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        MouseField = AccessTools.Field(typeof(InputState), "_currentMouseState");
        if (MouseField == null)
            throw new InvalidOperationException("Port input field not found; touch mod is incompatible.");
        string? device = TouchDevices.FindTouchDevice();
        if (device == null)
        {
            Monitor.Log("No touchscreen input device found; touch support disabled.", LogLevel.Warn);
            return;
        }
        int fd = open(device, 2048); // O_RDONLY | O_NONBLOCK; never grab exclusive access.
        if (fd < 0)
        {
            Monitor.Log($"Cannot open {device}: errno {Marshal.GetLastWin32Error()}.", LogLevel.Error);
            return;
        }
        ReadRange(fd, 0, ref XMin, ref XMax);
        ReadRange(fd, 1, ref YMin, ref YMax);
        new Harmony(ModManifest.UniqueID).Patch(
            AccessTools.Method(typeof(InputState), "UpdateStates"),
            postfix: new HarmonyMethod(typeof(ModEntry), nameof(AfterUpdateStates)));
        new Thread(() => ReadTouch(fd)) { IsBackground = true, Name = "PortMaster touch reader" }.Start();
        Monitor.Log($"Touch enabled: {device}, X={XMin}..{XMax}, Y={YMin}..{YMax}. Tap = left click; sticks remain available.", LogLevel.Info);
    }

    private static void AfterUpdateStates(InputState __instance)
    {
        Instance?.ApplyTouch(__instance);
    }

    private void ApplyTouch(InputState input)
    {
        bool wasDown = Current.Down;
        bool changed = false;
        // Deliver each press/release on a separate frame, even for very short taps.
        while (Samples.TryDequeue(out var sample))
        {
            bool edge = sample.Down != Current.Down;
            Current = sample;
            changed = true;
            if (edge) break;
        }
        if (!changed && !Current.Down) return;
        var bounds = Game1.game1.Window.ClientBounds;
        int x = Math.Clamp((Current.X - XMin) * Math.Max(1, bounds.Width - 1) / Math.Max(1, XMax - XMin), 0, Math.Max(0, bounds.Width - 1));
        int y = Math.Clamp((Current.Y - YMin) * Math.Max(1, bounds.Height - 1) / Math.Max(1, YMax - YMin), 0, Math.Max(0, bounds.Height - 1));
        var old = (MouseState)MouseField.GetValue(input)!;
        MouseField.SetValue(input, new MouseState(x, y, old.ScrollWheelValue,
            Current.Down ? ButtonState.Pressed : ButtonState.Released,
            old.MiddleButton, old.RightButton, old.XButton1, old.XButton2));
        if (Current.Down)
            Game1.options.gamepadControls = false; // Auto-detection re-enables this on controller input.
        if (Current.Down && !wasDown && LoggedPresses++ < 8)
            Monitor.Log($"Touch press ({x},{y}), window {bounds.Width}x{bounds.Height}.", LogLevel.Info);
    }

    private void ReadTouch(int fd)
    {
        byte[] buffer = new byte[24 * 128]; // Linux input_event on arm64.
        int x = 0, y = 0;
        bool down = false, dirty = false;
        try
        {
            while (true)
            {
                long length = read(fd, buffer, (UIntPtr)buffer.Length).ToInt64();
                if (length < 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error is 11 or 4) { Thread.Sleep(5); continue; }
                    throw new IOException($"Touch read failed: errno {error}");
                }
                if (length == 0) throw new IOException("Touch device disconnected");
                for (int i = 0; i + 24 <= length; i += 24)
                {
                    ushort type = BitConverter.ToUInt16(buffer, i + 16);
                    ushort code = BitConverter.ToUInt16(buffer, i + 18);
                    int value = BitConverter.ToInt32(buffer, i + 20);
                    if (type == 3 && code == 0) { x = value; dirty = true; }
                    if (type == 3 && code == 1) { y = value; dirty = true; }
                    if (type == 1 && code == 330) { down = value != 0; dirty = true; }
                    if (type == 0 && code == 0 && dirty)
                    {
                        Samples.Enqueue(new TouchSample(x, y, down));
                        dirty = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Samples.Enqueue(new TouchSample(x, y, false));
            Monitor.Log($"Touch reader stopped: {ex.Message}", LogLevel.Warn);
        }
        finally { close(fd); }
    }

    private static void ReadRange(int fd, int axis, ref int min, ref int max)
    {
        byte[] info = new byte[24];
        if (ioctl(fd, (UIntPtr)(0x80184540u + (uint)axis), info) == 0)
        {
            min = BitConverter.ToInt32(info, 4);
            max = BitConverter.ToInt32(info, 8);
        }
    }

    private readonly record struct TouchSample(int X, int Y, bool Down);
    [DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern IntPtr read(int fd, byte[] buffer, UIntPtr count);
    [DllImport("libc", SetLastError = true)] private static extern int close(int fd);
    [DllImport("libc", SetLastError = true)] private static extern int ioctl(int fd, UIntPtr request, byte[] data);
}
