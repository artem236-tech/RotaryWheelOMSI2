namespace RotaryWheel;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if(args.Contains("--probe")) { foreach(var d in Devices.GamepadInput.Enumerate()) Console.WriteLine($"{d.Id}: {d.Name}"); Console.WriteLine("PROBE COMPLETE"); return; }
        using var single=new Mutex(true,"Local\\OMSI-RotaryWheel",out bool owner);
        if(!owner) { MessageBox.Show("Rotary Wheel уже запущен. Откройте значок в трее.");return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1(args.Contains("--smoke-test")));
    }
}
