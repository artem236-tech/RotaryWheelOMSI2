using System.Runtime.InteropServices;
namespace RotaryWheel.Devices;

public sealed class VJoyOutput : IDisposable
{
    private IntPtr library;
    private readonly uint id;
    private readonly uint axis;
    private readonly int min,max;
    private readonly SetAxisFn set;
    private readonly ReleaseFn release;
    private bool acquired;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EnabledFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int StatusFn(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int AcquireFn(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ReleaseFn(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ExistsFn(uint id,uint axis);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int RangeFn(uint id,uint axis,out int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetAxisFn(int value,uint id,uint axis);
    private T Load<T>(string name) where T:Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,name));
    public VJoyOutput(uint deviceId,int axisUsage,string dllPath)
    {
        if(!Path.IsPathFullyQualified(dllPath) || !File.Exists(dllPath)) throw new FileNotFoundException("Выберите установленную x64 vJoyInterface.dll по абсолютному пути.",dllPath);
        id=deviceId; axis=(uint)axisUsage;
        library=NativeLibrary.Load(dllPath);
        try {
            set=Load<SetAxisFn>("SetAxis"); release=Load<ReleaseFn>("RelinquishVJD");
            if(Load<EnabledFn>("vJoyEnabled")()==0) throw new InvalidOperationException("Драйвер vJoy не включён.");
            if(Load<StatusFn>("GetVJDStatus")(id)!=1) throw new InvalidOperationException("Устройство vJoy занято, отключено или отсутствует.");
            if(Load<ExistsFn>("GetVJDAxisExist")(id,axis)==0) throw new InvalidOperationException("Эта ось не включена в настройках vJoy.");
            if(Load<RangeFn>("GetVJDAxisMin")(id,axis,out min)==0 || Load<RangeFn>("GetVJDAxisMax")(id,axis,out max)==0 || max<=min) throw new InvalidOperationException("Нельзя получить диапазон vJoy.");
            if(Load<AcquireFn>("AcquireVJD")(id)==0) throw new InvalidOperationException("Не удалось захватить vJoy.");
            acquired=true;
        } catch { NativeLibrary.Free(library); library=IntPtr.Zero; throw; }
    }
    public void Write(double normalized)
    {
        if(!double.IsFinite(normalized)) throw new ArgumentException("Нечисловой выход руля.");
        int value=(int)Math.Round(min+(Math.Clamp(normalized,-1,1)+1)*.5*(max-min));
        if(set(value,id,axis)==0) throw new IOException("vJoy отклонил ось. Передача остановлена; проверьте драйвер.");
    }
    public void Dispose()
    {
        if(library==IntPtr.Zero) return;
        if(acquired) release(id); // No ResetVJD: never touch pedals or other axes.
        NativeLibrary.Free(library); library=IntPtr.Zero; acquired=false;
    }
}
