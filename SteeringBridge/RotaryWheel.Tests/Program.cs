using RotaryWheel.Core;

int passed = 0;
void Near(double expected, double actual, string name) { if (!double.IsFinite(actual) || Math.Abs(expected-actual)>1e-7) throw new Exception($"{name}: expected {expected}, got {actual}"); passed++; }
WheelEngine Engine(double smoothing=0) => new(new WheelOptions { Deadzone=.2, Sensitivity=1, MaxAngleDegrees=900, SmoothingMs=smoothing });
void At(WheelEngine e, double degrees) => e.Step(Math.Cos(degrees*Math.PI/180), Math.Sin(degrees*Math.PI/180), true, .004);
var e=Engine(); At(e,179); At(e,-179); Near(2,e.WheelAngleDegrees,"wrap ±180");
e=Engine(); At(e,359); At(e,1); Near(2,e.WheelAngleDegrees,"wrap 360");
e=Engine(); At(e,0); At(e,90); Near(90,e.WheelAngleDegrees,"quarter circle");
for(int i=0;i<100;i++) e.Step(0,0,true,.004); Near(90,e.WheelAngleDegrees,"no autocenter");
At(e,180); Near(90,e.WheelAngleDegrees,"deadzone reentry baseline"); At(e,270); Near(180,e.WheelAngleDegrees,"reentry movement");
e.Step(1,0,false,.004); At(e,0); Near(180,e.WheelAngleDegrees,"reconnect baseline");
e.Center(); Near(0,e.Output,"explicit center"); At(e,180); Near(0,e.WheelAngleDegrees,"reset baseline");
e=Engine(); for(int i=0;i<30;i++) At(e,i*90); Near(900,e.WheelAngleDegrees,"positive clamp"); At(e,29*90-90); Near(810,e.WheelAngleDegrees,"immediate reversal at limit");
e=Engine(); for(int i=0;i<30;i++) At(e,-i*90); Near(-900,e.WheelAngleDegrees,"negative clamp");
e=Engine(100); At(e,0); At(e,90); double held=e.Output; e.Step(0,0,false,.004); Near(held,e.Output,"disconnect freezes smoothed output");
e.Step(double.NaN,1,true,.004); Near(held,e.Output,"NaN safe");
e=Engine(); for(int i=0;i<=360;i++) At(e,i); Near(360,e.WheelAngleDegrees,"full circle"); Near(.4,e.Output,"normalized axis");
var inverted=new WheelEngine(new WheelOptions { Invert=true }); At(inverted,0); At(inverted,90); Near(-90,inverted.WheelAngleDegrees,"inversion");
foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1.0,1.0}) { try { new WheelOptions{Deadzone=invalid}.Validate(); throw new Exception("invalid options accepted"); } catch(ArgumentException) { passed++; } }
Console.WriteLine($"PASS: {passed} regression assertions");
