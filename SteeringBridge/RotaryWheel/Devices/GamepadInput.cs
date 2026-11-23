using System.Runtime.InteropServices;
using SharpDX.DirectInput;
namespace RotaryWheel.Devices;

public record InputDevice(string Id,string Name) { public override string ToString()=>Name; }
public record StickSample(bool Connected,double X,double Y,bool CenterPressed);
public sealed class GamepadInput : IDisposable
{
    private readonly uint? xbox;
    private readonly bool right;
    private readonly int button;
    private readonly DirectInput? direct;
    private readonly Joystick? joystick;
    private readonly string axisX, axisY;
    private static readonly ushort[] Masks=[0x1000,0x2000,0x4000,0x8000,0x100,0x200,0x20,0x10,0x40,0x80];
    public static IReadOnlyList<InputDevice> Enumerate()
    {
        var result=new List<InputDevice>();
        for(uint i=0;i<4;i++) if(XInputGetState(i,out _)==0) result.Add(new($"xinput:{i}",$"Xbox / XInput {i+1}"));
        using var d=new DirectInput();
        foreach(var dev in d.GetDevices(DeviceClass.GameControl,DeviceEnumerationFlags.AttachedOnly))
            if(!dev.ProductName.Contains("vJoy",StringComparison.OrdinalIgnoreCase)) result.Add(new(dev.InstanceGuid.ToString(),$"DirectInput: {dev.InstanceName}"));
        return result;
    }
    public GamepadInput(string id,bool rightStick,int centerButton,IntPtr window,string directAxisX="RotationX",string directAxisY="RotationY")
    {
        right=rightStick; button=centerButton; axisX=directAxisX; axisY=directAxisY;
        if(id.StartsWith("xinput:")) { xbox=uint.Parse(id[7..]); if(xbox>3) throw new ArgumentException("Индекс XInput 0–3"); return; }
        direct=new DirectInput();
        try {
            joystick=new Joystick(direct,Guid.Parse(id));
            joystick.SetCooperativeLevel(window,CooperativeLevel.Background|CooperativeLevel.NonExclusive);
            foreach(var obj in joystick.GetObjects())
                if((obj.ObjectId.Flags & DeviceObjectTypeFlags.Axis)!=0) joystick.GetObjectPropertiesById(obj.ObjectId).Range=new InputRange(-32768,32767);
            joystick.Acquire();
        } catch { joystick?.Dispose(); direct.Dispose(); throw; }
    }
    private static double Normalize(int value)=>Math.Clamp(value/(value<0?32768.0:32767.0),-1,1);
    private static int Axis(JoystickState s,string name)=>name switch {
        "X"=>s.X,"Y"=>s.Y,"Z"=>s.Z,"RotationX"=>s.RotationX,"RotationY"=>s.RotationY,"RotationZ"=>s.RotationZ,
        _=>throw new ArgumentException("Неизвестная ось DirectInput: "+name)
    };
    public StickSample Read()
    {
        if(xbox is uint index) {
            if(XInputGetState(index,out var s)!=0) return new(false,0,0,false);
            return new(true,Normalize(right?s.RX:s.LX),Normalize(right?s.RY:s.LY),button>0 && button<=Masks.Length && (s.Buttons&Masks[button-1])!=0);
        }
        try {
            joystick!.Poll(); var s=joystick.GetCurrentState();
            return new(true,Normalize(right?Axis(s,axisX):s.X),-Normalize(right?Axis(s,axisY):s.Y),button>0 && button<=s.Buttons.Length && s.Buttons[button-1]);
        } catch(SharpDX.SharpDXException) {
            // Reacquire on the next poll. The engine rebases after every lost sample.
            try { joystick!.Acquire(); } catch(SharpDX.SharpDXException) { return new(false,0,0,false); }
            return new(false,0,0,false);
        }
    }
    public void Dispose() { joystick?.Dispose(); direct?.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct State { public uint Packet; public ushort Buttons; public byte LT,RT; public short LX,LY,RX,RY; }
    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index,out State state);
}
