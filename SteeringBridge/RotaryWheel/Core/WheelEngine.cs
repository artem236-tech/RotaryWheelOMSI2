namespace RotaryWheel.Core;

public sealed record WheelOptions
{
    public double Deadzone { get; init; } = .2;
    public double Sensitivity { get; init; } = 1;
    // Maximum displacement in EACH direction, not lock-to-lock travel.
    public double MaxAngleDegrees { get; init; } = 900;
    public double SmoothingMs { get; init; } = 0;
    public bool Invert { get; init; }
    public double CenterX { get; init; }
    public double CenterY { get; init; }
    public void Validate()
    {
        if (!double.IsFinite(Deadzone) || Deadzone < 0 || Deadzone > .999999 ||
            !double.IsFinite(Sensitivity) || Sensitivity < .01 || Sensitivity > 100 ||
            !double.IsFinite(MaxAngleDegrees) || MaxAngleDegrees < 1 || MaxAngleDegrees > 10000 ||
            !double.IsFinite(SmoothingMs) || SmoothingMs < 0 || SmoothingMs > 2000 ||
            !double.IsFinite(CenterX) || Math.Abs(CenterX) > .5 ||
            !double.IsFinite(CenterY) || Math.Abs(CenterY) > .5)
            throw new ArgumentException("Недопустимые параметры руля.");
    }
}

public sealed class WheelEngine
{
    private readonly WheelOptions options;
    private double previous;
    private bool tracking;
    public double WheelAngleDegrees { get; private set; }
    public double Output { get; private set; }
    public double Radius { get; private set; }
    public double Angle { get; private set; }
    public double Delta { get; private set; }
    public WheelEngine(WheelOptions options) { options.Validate(); this.options = options; }
    public void Center() { WheelAngleDegrees = Output = Delta = 0; tracking = false; }
    public void Rebase() => tracking = false;
    public double Step(double x, double y, bool connected, double dt)
    {
        Delta = 0;
        if (!connected || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(dt) || dt <= 0)
        { tracking = false; return Output; }
        x -= options.CenterX; y -= options.CenterY;
        Radius = Math.Sqrt(x*x + y*y); Angle = Math.Atan2(y,x);
        if (Radius < options.Deadzone || Radius < 1e-9)
        {
            // Angle at the origin is undefined: rebase again on the first sample outside.
            previous = Angle; tracking = false;
        }
        else
        {
            if (tracking)
            {
                Delta = Math.Atan2(Math.Sin(Angle-previous), Math.Cos(Angle-previous));
                WheelAngleDegrees = Math.Clamp(WheelAngleDegrees + Delta * 180 / Math.PI * options.Sensitivity * (options.Invert ? -1 : 1), -options.MaxAngleDegrees, options.MaxAngleDegrees);
            }
            previous = Angle; tracking = true;
        }
        double target = WheelAngleDegrees / options.MaxAngleDegrees;
        double alpha = options.SmoothingMs == 0 ? 1 : 1-Math.Exp(-dt*1000/options.SmoothingMs);
        Output += (target-Output)*alpha;
        return Output;
    }
}
