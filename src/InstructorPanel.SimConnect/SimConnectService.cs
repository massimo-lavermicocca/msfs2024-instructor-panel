using System.Runtime.InteropServices;
using InstructorPanel.SimConnect;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FlightSimulator.SimConnect;

public sealed class SimConnectService : BackgroundService, ISimConnectService
{
    private enum Def { Teleport, Speed, Pitch, Heading, Altitude, Roll }
    private enum Req { Altitude }
    private enum Evt { PmdgFlaps }
    private enum Grp { Pmdg }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Position { public double Latitude, Longitude, Altitude; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Value { public double V; }

    // SimConnect reports pitch positive = nose DOWN. We expose positive = nose up, so we flip it.
    // If the sign is wrong in your tests, change this to 1.
    private const double PitchSign = -1;

    // Same for bank: SimConnect reports positive = LEFT wing down. We expose positive = roll right.
    // If the sign is wrong in your tests, change this to 1.
    private const double RollSign = -1;

    private readonly ILogger<SimConnectService> _log;
    private readonly uint _flapsEventId = 76776;
    private readonly Dictionary<int, uint> _flapsParams = new Dictionary<int, uint>()
    {
        { 1, 0},
        { 5, 536870912 },
        { 15, 0 },
        { 30, 0 },
        { 40, 0 }
    };

    private readonly AutoResetEvent _msgEvent = new(false);
    private readonly object _lock = new();   // SimConnect is not thread-safe
    private SimConnect? _sc;
    private volatile bool _connected;
    private volatile bool _reconnect;
    private TaskCompletionSource<double>? _altitudeRequest;

    public SimConnectService(ILogger<SimConnectService> log)
    {
        _log = log;
    }

    public bool IsConnected => _connected;

    // ---------------------------------------------------------------- commands

    public bool Teleport(double latitude, double longitude, double altitudeFeet)
        => Set(Def.Teleport, new Position { Latitude = latitude, Longitude = longitude, Altitude = altitudeFeet }, "Teleport");

    public bool SetSpeed(double knots)
        => Set(Def.Speed, new Value { V = knots }, "Speed");

    public bool SetIas(double knots)
    {
        // Read the current altitude first, so we can convert IAS to TAS.
        var tcs = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        _altitudeRequest = tcs;

        var requested = Send(sc => sc.RequestDataOnSimObject(Req.Altitude, Def.Altitude,
            SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0), "Altitude request");

        if (!requested || !tcs.Task.Wait(2000)) return false;

        var tas = knots / Math.Sqrt(DensityRatio(tcs.Task.Result));
        return SetSpeed(tas);
    }

    public bool SetPitch(int degrees)
        => Set(Def.Pitch, new Value { V = degrees * PitchSign }, "Pitch");

    public bool SetHeading(int degrees)
        => Set(Def.Heading, new Value { V = ((degrees % 360) + 360) % 360 }, "Heading");

    public bool SetFlaps(int detent)
    {
        if (_flapsEventId == 0 || !_flapsParams.TryGetValue(detent, out var parameter))
        {
            _log.LogWarning("Flaps {Detent} not configured (see Pmdg section in appsettings.json)", detent);
            return false;
        }

        return Send(sc => sc.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, Evt.PmdgFlaps,
            parameter, Grp.Pmdg, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Flaps");
    }

    public bool SetRoll(int degrees)
        => Set(Def.Roll, new Value { V = degrees * RollSign }, "Roll");

    // ---------------------------------------------------------------- helpers

    private bool Set<T>(Def def, T data, string what) where T : struct
        => Send(sc => sc.SetDataOnSimObject(def, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_DATA_SET_FLAG.DEFAULT, data), what);

    private bool Send(Action<SimConnect> action, string what)
    {
        lock (_lock)
        {
            if (!_connected || _sc is null) return false;
            try { action(_sc); return true; }
            catch (Exception ex)
            {
                _log.LogError(ex, "{What} failed", what);
                _reconnect = true;
                return false;
            }
        }
    }

    // ISA density ratio (sigma) from altitude in feet. Good enough for IAS -> TAS at normal speeds.
    private static double DensityRatio(double altitudeFeet)
    {
        var h = Math.Max(altitudeFeet, -1000);
        return h < 36089
            ? Math.Pow(1 - 6.8755856e-6 * h, 4.2558797)
            : 0.2970756 * Math.Exp(-(h - 36089) / 20805.7);
    }

    // ---------------------------------------------------------------- connection loop

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();

        while (!ct.IsCancellationRequested)
        {
            lock (_lock)
            {
                if (_reconnect) { Disconnect(); _reconnect = false; }
                if (_sc is null) TryConnect();
            }

            if (_sc is null)
            {
                try { await Task.Delay(3000, ct); } catch (OperationCanceledException) { break; }
                continue;
            }

            if (_msgEvent.WaitOne(200))
            {
                lock (_lock)
                {
                    try { _sc?.ReceiveMessage(); }
                    catch (Exception ex) { _log.LogWarning(ex, "ReceiveMessage failed"); _reconnect = true; }
                }
            }
        }

        lock (_lock) Disconnect();
    }

    private void TryConnect()
    {
        try
        {
            var sc = new SimConnect("Pmdg737Api", IntPtr.Zero, 0, _msgEvent, 0);
            sc.OnRecvOpen += OnOpen;
            sc.OnRecvQuit += (_, _) => { _log.LogInformation("MSFS closed"); _connected = false; _reconnect = true; };
            sc.OnRecvException += (_, d) => _log.LogWarning("SimConnect exception: {Ex}", (SIMCONNECT_EXCEPTION)d.dwException);
            sc.OnRecvSimobjectData += (_, d) =>
            {
                if ((Req)d.dwRequestID == Req.Altitude)
                    _altitudeRequest?.TrySetResult(((Value)d.dwData[0]).V);
            };
            _sc = sc;
        }
        catch (Exception)
        {
            _sc = null; // sim not running yet: try again later
        }
    }

    private void OnOpen(SimConnect sc, SIMCONNECT_RECV_OPEN data)
    {
        _log.LogInformation("Connected to {App}", data.szApplicationName);

        sc.AddToDataDefinition(Def.Teleport, "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(Def.Teleport, "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(Def.Teleport, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<Position>(Def.Teleport);

        sc.AddToDataDefinition(Def.Speed, "VELOCITY BODY Z", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<Value>(Def.Speed);

        sc.AddToDataDefinition(Def.Pitch, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<Value>(Def.Pitch);

        sc.AddToDataDefinition(Def.Heading, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<Value>(Def.Heading);

        sc.AddToDataDefinition(Def.Altitude, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<Value>(Def.Altitude);

        if (_flapsEventId != 0)
        {
            sc.MapClientEventToSimEvent(Evt.PmdgFlaps, "#" + _flapsEventId);
            sc.AddClientEventToNotificationGroup(Grp.Pmdg, Evt.PmdgFlaps, false);
            sc.SetNotificationGroupPriority(Grp.Pmdg, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);
        }

        sc.AddToDataDefinition(Def.Roll, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<Value>(Def.Roll);

        _connected = true;
    }

    private void Disconnect()
    {
        _connected = false;
        try { _sc?.Dispose(); } catch { }
        _sc = null;
    }
}