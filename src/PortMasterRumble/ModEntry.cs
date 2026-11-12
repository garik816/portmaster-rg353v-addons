using System;
using System.Runtime.InteropServices;
using System.Text;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PortMasterRumble;

public sealed class ModEntry : Mod
{
    private ModConfig Config = new();
    private RumbleWriter? Rumble;
    private int LastHealth = -1;
    private float LastStamina = -1;
    private bool WasUsingTool;
    private bool WasFishingNibble;
    private long LastToolPulseTick;
    private long LastStaminaPulseTick;

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        this.Rumble = new RumbleWriter(this.Config.FifoPath, this.Monitor);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Display.MenuChanged += this.OnMenuChanged;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.RegisterGenericModConfigMenu();
        this.Pulse(this.Config.GameLaunchedMs, "game launched");
    }

    private void RegisterGenericModConfigMenu()
    {
        var api = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(
            mod: this.ModManifest,
            reset: () =>
            {
                this.Config = new ModConfig();
                this.Rumble = new RumbleWriter(this.Config.FifoPath, this.Monitor);
            },
            save: () =>
            {
                this.Helper.WriteConfig(this.Config);
                this.Rumble = new RumbleWriter(this.Config.FifoPath, this.Monitor);
            }
        );

        api.AddSectionTitle(this.ModManifest, () => "Output");
        api.AddTextOption(this.ModManifest, () => this.Config.FifoPath, value => this.Config.FifoPath = value, () => "FIFO path", () => "Path used by rumble-helper. Default: /tmp/stardew-rumble");
        api.AddNumberOption(this.ModManifest, () => this.Config.ActiveDuty, value => this.Config.ActiveDuty = value, () => "Active PWM duty", () => "Lower is stronger with inversed polarity. Default: 10000000", min: 1000000, max: 20000000, interval: 500000);

        api.AddSectionTitle(this.ModManifest, () => "Events");
        api.AddBoolOption(this.ModManifest, () => this.Config.ToolPulse, value => this.Config.ToolPulse = value, () => "Tool use rumble");
        api.AddBoolOption(this.ModManifest, () => this.Config.FishingPulse, value => this.Config.FishingPulse = value, () => "Fishing bite rumble");
        api.AddBoolOption(this.ModManifest, () => this.Config.MenuPulse, value => this.Config.MenuPulse = value, () => "Menu rumble");

        api.AddSectionTitle(this.ModManifest, () => "Pulse lengths");
        api.AddNumberOption(this.ModManifest, () => this.Config.GameLaunchedMs, value => this.Config.GameLaunchedMs = value, () => "Game launched ms", min: 0, max: 1000, interval: 10);
        api.AddNumberOption(this.ModManifest, () => this.Config.DayStartedMs, value => this.Config.DayStartedMs = value, () => "Day started ms", min: 0, max: 1000, interval: 10);
        api.AddNumberOption(this.ModManifest, () => this.Config.WarpedMs, value => this.Config.WarpedMs = value, () => "Location change ms", min: 0, max: 1000, interval: 10);
        api.AddNumberOption(this.ModManifest, () => this.Config.DamageMs, value => this.Config.DamageMs = value, () => "Damage ms", min: 0, max: 1500, interval: 10);
        api.AddNumberOption(this.ModManifest, () => this.Config.ToolUseMs, value => this.Config.ToolUseMs = value, () => "Tool use ms", min: 0, max: 500, interval: 5);
        api.AddNumberOption(this.ModManifest, () => this.Config.StaminaDropMs, value => this.Config.StaminaDropMs = value, () => "Stamina drop ms", min: 0, max: 500, interval: 5);
        api.AddNumberOption(this.ModManifest, () => this.Config.FishNibbleMs, value => this.Config.FishNibbleMs = value, () => "Fish bite ms", min: 0, max: 1500, interval: 10);
        api.AddNumberOption(this.ModManifest, () => this.Config.MenuMs, value => this.Config.MenuMs = value, () => "Menu ms", min: 0, max: 500, interval: 5);

        api.AddSectionTitle(this.ModManifest, () => "Sensitivity");
        api.AddNumberOption(this.ModManifest, () => this.Config.StaminaDropThreshold, value => this.Config.StaminaDropThreshold = value, () => "Stamina drop threshold", () => "Minimum stamina drop before a pulse.", min: 1f, max: 30f, interval: 1f);
        api.AddNumberOption(this.ModManifest, () => this.Config.ToolPulseCooldownTicks, value => this.Config.ToolPulseCooldownTicks = value, () => "Tool cooldown ticks", min: 0, max: 120, interval: 1);
        api.AddNumberOption(this.ModManifest, () => this.Config.StaminaPulseCooldownTicks, value => this.Config.StaminaPulseCooldownTicks = value, () => "Stamina cooldown ticks", min: 0, max: 180, interval: 1);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        this.LastHealth = Game1.player?.health ?? -1;
        this.LastStamina = Game1.player?.Stamina ?? -1;
        this.Pulse(this.Config.DayStartedMs, "day started");
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.IsLocalPlayer)
            this.Pulse(this.Config.WarpedMs, "warped");
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (!this.Config.MenuPulse)
            return;
        if (e.NewMenu != null || e.OldMenu != null)
            this.Pulse(this.Config.MenuMs, "menu changed");
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player == null)
            return;

        var player = Game1.player;

        if (this.LastHealth >= 0 && player.health < this.LastHealth)
            this.Pulse(this.Config.DamageMs, "health decreased");
        this.LastHealth = player.health;

        if (this.LastStamina >= 0 && player.Stamina < this.LastStamina - this.Config.StaminaDropThreshold && e.Ticks - this.LastStaminaPulseTick > this.Config.StaminaPulseCooldownTicks)
        {
            this.LastStaminaPulseTick = e.Ticks;
            this.Pulse(this.Config.StaminaDropMs, "stamina drop");
        }
        this.LastStamina = player.Stamina;

        bool usingTool = player.UsingTool;
        if (this.Config.ToolPulse && usingTool && !this.WasUsingTool && e.Ticks - this.LastToolPulseTick > this.Config.ToolPulseCooldownTicks)
        {
            this.LastToolPulseTick = e.Ticks;
            this.Pulse(this.Config.ToolUseMs, "tool use");
        }
        this.WasUsingTool = usingTool;

        if (this.Config.FishingPulse)
            this.CheckFishingNibble();
    }

    private void CheckFishingNibble()
    {
        object? tool = Game1.player?.CurrentTool;
        bool nibble = false;
        if (tool != null && tool.GetType().Name == "FishingRod")
        {
            try
            {
                nibble = this.Helper.Reflection.GetField<bool>(tool, "isNibbling", required: false)?.GetValue() ?? false;
            }
            catch
            {
                nibble = false;
            }
        }

        if (nibble && !this.WasFishingNibble)
            this.Pulse(this.Config.FishNibbleMs, "fish nibble");
        this.WasFishingNibble = nibble;
    }

    private void Pulse(int ms, string reason)
    {
        if (ms <= 0 || this.Rumble == null)
            return;
        if (this.Rumble.Pulse(ms, this.Config.ActiveDuty))
            this.Monitor.Log($"Rumble pulse {ms}ms ({reason}).", LogLevel.Trace);
    }
}

public sealed class ModConfig
{
    public string FifoPath { get; set; } = "/tmp/stardew-rumble";
    public int ActiveDuty { get; set; } = 10000000;

    public int GameLaunchedMs { get; set; } = 120;
    public int DayStartedMs { get; set; } = 120;
    public int WarpedMs { get; set; } = 50;
    public int DamageMs { get; set; } = 220;
    public int ToolUseMs { get; set; } = 90;
    public int StaminaDropMs { get; set; } = 70;
    public int FishNibbleMs { get; set; } = 260;
    public int MenuMs { get; set; } = 20;

    public bool ToolPulse { get; set; } = true;
    public bool FishingPulse { get; set; } = true;
    public bool MenuPulse { get; set; } = false;

    public float StaminaDropThreshold { get; set; } = 7f;
    public int ToolPulseCooldownTicks { get; set; } = 12;
    public int StaminaPulseCooldownTicks { get; set; } = 20;
}

internal sealed class RumbleWriter
{
    private const int O_WRONLY = 1;
    private const int O_NONBLOCK = 2048;
    private readonly string FifoPath;
    private readonly IMonitor Monitor;
    private DateTime LastError = DateTime.MinValue;

    public RumbleWriter(string fifoPath, IMonitor monitor)
    {
        this.FifoPath = fifoPath;
        this.Monitor = monitor;
    }

    public bool Pulse(int ms, int duty)
    {
        string command = duty > 0 ? $"pulse {ms} {duty}\n" : $"pulse {ms}\n";
        byte[] bytes = Encoding.ASCII.GetBytes(command);
        int fd = open(this.FifoPath, O_WRONLY | O_NONBLOCK);
        if (fd < 0)
        {
            this.LogOccasionalError("rumble FIFO is not available");
            return false;
        }

        try
        {
            IntPtr result = write(fd, bytes, (UIntPtr)bytes.Length);
            return result.ToInt64() == bytes.Length;
        }
        finally
        {
            close(fd);
        }
    }

    private void LogOccasionalError(string message)
    {
        if ((DateTime.UtcNow - this.LastError).TotalSeconds < 10)
            return;
        this.LastError = DateTime.UtcNow;
        this.Monitor.Log(message, LogLevel.Trace);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string pathname, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr write(int fd, byte[] buf, UIntPtr count);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);
}

public interface IGenericModConfigMenuApi
{
    void Register(StardewModdingAPI.IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
    void AddSectionTitle(StardewModdingAPI.IManifest mod, Func<string> text, Func<string>? tooltip = null);
    void AddBoolOption(StardewModdingAPI.IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    void AddTextOption(StardewModdingAPI.IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, string? fieldId = null);
    void AddNumberOption(StardewModdingAPI.IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, string? fieldId = null);
    void AddNumberOption(StardewModdingAPI.IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string>? tooltip = null, float? min = null, float? max = null, float? interval = null, string? fieldId = null);
}