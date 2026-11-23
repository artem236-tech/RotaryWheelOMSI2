using System.Runtime.InteropServices;
using RotaryWheel.Core;
using RotaryWheel.Devices;
namespace RotaryWheel;

public partial class Form1 : Form
{
    private BridgeConfig config=new();
    private BridgeRunner? runner;
    private readonly ComboBox devices=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=440};
    private readonly ComboBox stick=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox axis=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox diX=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox diY=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly NumericUpDown dead=Number(0,.999999m,.01m,6),sensitivity=Number(.01m,100,.1m,2),max=Number(1,10000,90),smooth=Number(0,2000,10),button=Number(0,128,1),id=Number(1,16,1),hz=Number(125,250,125);
    private readonly CheckBox invert=new(){Text="Инверсия"}, trace=new(){Text="Подробный журнал",AutoSize=true},monitor=new(){Text="Только диагностика (без vJoy)",AutoSize=true};
    private readonly TextBox dll=new(){Width=340};
    private readonly Label status=new(){AutoSize=true,MaximumSize=new Size(630,0)};
    private readonly Label calibration=new(){AutoSize=true};
    private readonly Button start=new(){Text="Запустить",AutoSize=true};
    private readonly Panel settings=new(){Dock=DockStyle.Fill,AutoScroll=true};
    private readonly NotifyIcon tray=new(){Icon=SystemIcons.Application,Text="Rotary Wheel — OMSI",Visible=true};
    private readonly System.Windows.Forms.Timer refresh=new(){Interval=100};
    private bool hotkey;
    private double cx,cy;
    private readonly System.Windows.Forms.Timer? smoke;
    private static NumericUpDown Number(decimal min,decimal max,decimal step,int decimals=0)=>new(){Minimum=min,Maximum=max,Increment=step,DecimalPlaces=decimals,Width=120};
    private static Button ActionButton(string text,Action action) { var b=new Button{Text=text,AutoSize=true}; b.Click+=(_,_)=>action(); return b; }
    public Form1(bool smokeTest=false)
    {
        InitializeComponent(); Text="Rotary Wheel · OMSI"; ClientSize=new Size(740,850); MinimumSize=new Size(660,650); Font=new Font("Segoe UI",10);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(16)};
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(root);
        root.Controls.Add(settings); var table=new TableLayoutPanel{AutoSize=true,ColumnCount=2,Dock=DockStyle.Top}; settings.Controls.Add(table);
        void Row(string label,Control control) { int row=table.RowCount++; table.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(0,8,20,8)},0,row); table.Controls.Add(control,1,row); control.Margin=new Padding(0,4,0,4); }
        stick.Items.AddRange(["Левый","Правый"]); axis.Items.AddRange(["X","Y","Z","Rx","Ry","Rz","Slider 1","Slider 2"]);
        foreach(var box in new[]{diX,diY}) box.Items.AddRange(["X","Y","Z","RotationX","RotationY","RotationZ"]);
        Row("Геймпад",devices); Row("",ActionButton("Обновить список",Reload)); Row("Стик",stick);
        Row("Правый DI: горизонталь",diX); Row("Правый DI: вертикаль",diY);
        Row("Радиальная мёртвая зона",dead); Row("Множитель вращения",sensitivity); Row("Предел в каждую сторону, °",max); Row("Сглаживание, мс",smooth); Row("",invert);
        Row("Кнопка центра (0 = нет)",button); Row("Опрос, Гц",hz); Row("Выход",new Label{Text="vJoy · абсолютная ось",AutoSize=true}); Row("",monitor); Row("Номер vJoy",id); Row("Ось vJoy",axis);
        var fileRow=new FlowLayoutPanel{AutoSize=true}; fileRow.Controls.Add(dll); fileRow.Controls.Add(ActionButton("Обзор…",()=>{ using var f=new OpenFileDialog{Filter="vJoyInterface.dll|vJoyInterface.dll"}; if(f.ShowDialog()==DialogResult.OK) dll.Text=f.FileName; })); Row("x64 DLL vJoy",fileRow);
        Row("",trace); Row("Калибровка",calibration);
        Row("",ActionButton("Калибровать отпущенный стик",Calibrate));
        Row("",new Label{Text="Центр руля: Ctrl+Alt+C или кнопка ниже.\nГаз, тормоз и кнопки утилита не пересылает.",AutoSize=true});
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill}; root.Controls.Add(actions); actions.Controls.Add(start); start.Click+=(_,_)=>Toggle();
        actions.Controls.Add(ActionButton("Вернуть руль в центр",()=>runner?.Center())); actions.Controls.Add(ActionButton("Сохранить",Save)); actions.Controls.Add(ActionButton("В трей",()=>Hide())); root.Controls.Add(status);
        var menu=new ContextMenuStrip(); menu.Items.Add("Открыть",null,(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();}); menu.Items.Add("Руль в центр",null,(_,_)=>runner?.Center()); menu.Items.Add("Выход",null,(_,_)=>Close()); tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>{Show();Activate();};
        try { config=BridgeConfig.Load(); } catch(Exception ex) { MessageBox.Show("Конфигурация не загружена: "+ex.Message); }
        cx=config.Wheel.CenterX; cy=config.Wheel.CenterY;
        dead.Value=(decimal)config.Wheel.Deadzone; sensitivity.Value=(decimal)config.Wheel.Sensitivity; max.Value=(decimal)config.Wheel.MaxAngleDegrees; smooth.Value=(decimal)config.Wheel.SmoothingMs; invert.Checked=config.Wheel.Invert;
        button.Value=config.CenterButton; id.Value=config.VJoyId; hz.Value=config.PollHz; dll.Text=config.VJoyDll; trace.Checked=config.TraceSamples; monitor.Checked=config.MonitorOnly; stick.SelectedIndex=config.RightStick?1:0; axis.SelectedIndex=config.AxisUsage-0x30; diX.SelectedItem=config.DirectAxisX; diY.SelectedItem=config.DirectAxisY;
        Reload(); ShowCalibration();
        refresh.Tick+=(_,_)=> { if(runner is null) return; var s=runner.State; status.Text=s.Error.Length>0?"Ошибка: "+s.Error:$"{(s.Connected?"Подключён":"Нет геймпада — выход удерживается")} · {s.Hz:F0} Гц\nСтик {s.X:F3}; {s.Y:F3} · Руль {s.Wheel:F1}° · Ось {s.Output:F4}"; if(s.Error.Length>0) Stop(); }; refresh.Start();
        Shown+=(_,_)=>{hotkey=RegisterHotKey(Handle,1,0x4003,0x43); if(!hotkey) status.Text="Ctrl+Alt+C занято. Используйте кнопку центра.";};
        FormClosed+=(_,_)=>{refresh.Stop();refresh.Dispose();Stop();tray.Dispose();if(hotkey)UnregisterHotKey(Handle,1);smoke?.Dispose();};
        if(smokeTest) { smoke=new(){Interval=800}; smoke.Tick+=(_,_)=>{ smoke.Stop(); using var image=new Bitmap(Width,Height); DrawToBitmap(image,new Rectangle(0,0,Width,Height)); image.Save(Path.Combine(AppContext.BaseDirectory,"ui-smoke.png")); Console.WriteLine("PASS: WinForms constructed, shown and closed"); Close(); }; smoke.Start(); }
    }
    private void ShowCalibration()=>calibration.Text=$"X {cx:F4}; Y {cy:F4}";
    private void Reload()
    {
        try { devices.Items.Clear(); foreach(var d in GamepadInput.Enumerate()) devices.Items.Add(d); devices.SelectedItem=devices.Items.Cast<InputDevice>().FirstOrDefault(d=>d.Id==config.DeviceId); if(devices.SelectedIndex<0 && devices.Items.Count>0) devices.SelectedIndex=0; status.Text=devices.Items.Count==0?"Геймпады не найдены. Подключите и обновите список.":"Выберите геймпад. Сначала проверьте режим диагностики."; }
        catch(Exception ex) { status.Text="Поиск устройств: "+ex.Message; }
    }
    private BridgeConfig ReadConfig()=>config with { DeviceId=(devices.SelectedItem as InputDevice)?.Id??throw new ArgumentException("Выберите геймпад."), RightStick=stick.SelectedIndex==1, DirectAxisX=diX.Text, DirectAxisY=diY.Text, MonitorOnly=monitor.Checked, CenterButton=(int)button.Value, VJoyId=(uint)id.Value, AxisUsage=axis.SelectedIndex+0x30,VJoyDll=dll.Text,PollHz=(int)hz.Value,TraceSamples=trace.Checked,Wheel=new WheelOptions{Deadzone=(double)dead.Value,Sensitivity=(double)sensitivity.Value,MaxAngleDegrees=(double)max.Value,SmoothingMs=(double)smooth.Value,Invert=invert.Checked,CenterX=cx,CenterY=cy} };
    private void Save() { try { config=ReadConfig();config.Save(); status.Text="Настройки сохранены рядом с программой."; } catch(Exception ex) { status.Text=ex.Message; } }
    private void Calibrate()
    {
        try { var c=ReadConfig(); using var input=new GamepadInput(c.DeviceId,c.RightStick,c.CenterButton,Handle,c.DirectAxisX,c.DirectAxisY); var s=input.Read(); if(!s.Connected || Math.Abs(s.X)>.5 || Math.Abs(s.Y)>.5) throw new InvalidOperationException("Отпустите стик и проверьте подключение."); cx=s.X;cy=s.Y;ShowCalibration(); }
        catch(Exception ex) {status.Text=ex.Message;}
    }
    private void Toggle()
    {
        if(runner!=null) {Stop();return;}
        try {config=ReadConfig();config.Validate();config.Save();runner=new BridgeRunner(config,Handle);settings.Enabled=false;start.Text="Остановить";}
        catch(Exception ex){status.Text=ex.Message;}
    }
    private void Stop() { runner?.Dispose();runner=null;settings.Enabled=true;start.Text="Запустить"; }
    protected override void WndProc(ref Message m) {if(m.Msg==0x312 && m.WParam==(IntPtr)1) runner?.Center();base.WndProc(ref m);}
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr h,int id);
}
