using System.Text.Json;
using RotaryWheel.Core;
namespace RotaryWheel;

public sealed record BridgeConfig
{
    public string DeviceId { get; init; } = "xinput:0";
    public bool RightStick { get; init; }
    public string DirectAxisX { get; init; } = "RotationX";
    public string DirectAxisY { get; init; } = "RotationY";
    public bool MonitorOnly { get; init; } = true;
    public int CenterButton { get; init; } = 10;
    public uint VJoyId { get; init; } = 1;
    public int AxisUsage { get; init; } = 0x30;
    public string VJoyDll { get; init; } = "";
    public int PollHz { get; init; } = 250;
    public WheelOptions Wheel { get; init; } = new();
    public bool TraceSamples { get; init; } = true;
    public void Validate()
    {
        if (Wheel is null) throw new ArgumentException("Отсутствует раздел Wheel.");
        Wheel.Validate();
        string[] axes = ["X","Y","Z","RotationX","RotationY","RotationZ"];
        if (!axes.Contains(DirectAxisX) || !axes.Contains(DirectAxisY)) throw new ArgumentException("Недопустимые оси DirectInput.");
        if (PollHz < 125 || PollHz > 250 || CenterButton < 0 || CenterButton > 128 || VJoyId < 1 || VJoyId > 16 || AxisUsage < 0x30 || AxisUsage > 0x37 || string.IsNullOrWhiteSpace(DeviceId))
            throw new ArgumentException("Проверьте устройство, кнопку, ось и частоту опроса.");
    }
    public static string FilePath => Path.Combine(AppContext.BaseDirectory,"rotary-wheel.json");
    public static BridgeConfig Load() { var c = File.Exists(FilePath) ? JsonSerializer.Deserialize<BridgeConfig>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("Пустой JSON") : new(); c.Validate(); return c; }
    public void Save()
    {
        Validate();
        string temp=FilePath+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        if(File.Exists(FilePath)) File.Copy(FilePath,FilePath+".bak",true);
        File.Move(temp,FilePath,true);
    }
}
