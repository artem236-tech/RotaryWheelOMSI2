using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using RotaryWheel.Core;
using RotaryWheel.Devices;
namespace RotaryWheel;

public sealed record LiveState(bool Connected,double X,double Y,double Wheel,double Output,double Hz,string Error);
public sealed class BridgeRunner : IDisposable
{
    private readonly CancellationTokenSource cancel=new();
    private readonly Task task;
    private int center;
    private LiveState state=new(false,0,0,0,0,0,"");
    public LiveState State=>Volatile.Read(ref state);
    public void Center()=>Interlocked.Exchange(ref center,1);
    public BridgeRunner(BridgeConfig config,IntPtr window)
    {
        config.Validate();
        task=Task.Factory.StartNew(()=>Run(config,window),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
    }
    private void Run(BridgeConfig c,IntPtr window)
    {
        RollingLog? log=null;
        bool timer=false;
        try {
            log=new RollingLog();
            using var input=new GamepadInput(c.DeviceId,c.RightStick,c.CenterButton,window,c.DirectAxisX,c.DirectAxisY);
            using var output=c.MonitorOnly?null:new VJoyOutput(c.VJoyId,c.AxisUsage,c.VJoyDll);
            var engine=new WheelEngine(c.Wheel);
            timer=timeBeginPeriod(1)==0;
            var clock=Stopwatch.StartNew(); double previous=clock.Elapsed.TotalSeconds, deadline=previous;
            double reportAt=previous; int count=0; double hz=0;
            bool lastButton=false,wasConnected=false;
            while(!cancel.IsCancellationRequested) {
                double now=clock.Elapsed.TotalSeconds,dt=now-previous; previous=now;
                var sample=input.Read();
                if(sample.Connected!=wasConnected) log.Write(sample.Connected?"CONNECTED":"DISCONNECTED: output held");
                bool reset=Interlocked.Exchange(ref center,0)==1 || sample.Connected && wasConnected && sample.CenterPressed&&!lastButton;
                wasConnected=sample.Connected;
                lastButton=sample.CenterPressed;
                if(reset) { engine.Center(); log.Write("CENTER"); }
                // A scheduling pause cannot reconstruct revolutions that were not sampled.
                if(dt>.1) engine.Rebase();
                double value=engine.Step(sample.X,sample.Y,sample.Connected,Math.Max(dt,.000001));
                if(sample.Connected || reset) output?.Write(value);
                count++; if(now-reportAt>=1) { hz=count/(now-reportAt); count=0; reportAt=now; }
                Volatile.Write(ref state,new(sample.Connected,sample.X,sample.Y,engine.WheelAngleDegrees,value,hz,""));
                if(c.TraceSamples) log.Write(FormattableString.Invariant($"sample,{sample.Connected},{sample.X:F6},{sample.Y:F6},{engine.Radius:F6},{engine.Angle:F6},{engine.Delta:F6},{engine.WheelAngleDegrees:F6},{value:F6}"));
                deadline+=1.0/c.PollHz;
                if(deadline<now) deadline=now+1.0/c.PollHz;
                double wait=deadline-clock.Elapsed.TotalSeconds;
                if(wait>.0015) cancel.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(wait-.001));
                while(!cancel.IsCancellationRequested && clock.Elapsed.TotalSeconds<deadline) Thread.SpinWait(40);
            }
        } catch(Exception ex) {
            Volatile.Write(ref state,State with { Error=ex.Message });
            try { log?.Write("ERROR: "+ex); } catch(IOException) { /* Error remains visible in UI. */ }
        } finally {
            if(timer) timeEndPeriod(1);
            try { log?.Dispose(); }
            catch(Exception ex) { Volatile.Write(ref state,State with { Error="Закрытие журнала: "+ex.Message }); }
        }
    }
    public void Dispose() { cancel.Cancel(); task.GetAwaiter().GetResult(); cancel.Dispose(); }
    [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);
}

internal sealed class RollingLog : IDisposable
{
    private readonly string path=Path.Combine(AppContext.BaseDirectory,"rotary-wheel.log");
    private StreamWriter writer;
    private int lines;
    public RollingLog() { writer=Open(); }
    private StreamWriter Open()=>new(path,true,System.Text.Encoding.UTF8,65536);
    public void Write(string text)
    {
        writer.WriteLine($"{DateTime.UtcNow:O} {text}");
        if(++lines%250!=0) return;
        writer.Flush();
        if(writer.BaseStream.Length<8*1024*1024) return;
        writer.Dispose(); File.Move(path,path+".1",true); writer=Open();
    }
    public void Dispose()=>writer.Dispose();
}
