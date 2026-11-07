using System.Runtime.InteropServices;
using InstructorPanel.SimConnect.Enums;
using Microsoft.Extensions.Logging;
using SDK = Microsoft.FlightSimulator.SimConnect;

namespace InstructorPanel.SimConnect
{
    public abstract class SimConnectService : ISimConnectService, IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Position
        {
            public double Latitude,
                Longitude,
                Altitude;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Value
        {
            public double V;
        }

        // SimConnect reports pitch positive = nose DOWN. We expose positive = nose up, so we flip it.
        // If the sign is wrong in your tests, change this to 1.
        private const double PitchSign = -1;

        // Same for bank: SimConnect reports positive = LEFT wing down. We expose positive = roll right.
        // If the sign is wrong in your tests, change this to 1.
        private const double RollSign = -1;

        private readonly ILogger<SimConnectService> _log;

        private readonly AutoResetEvent _msgEvent = new(false);
        private readonly object _lock = new(); // SimConnect is not thread-safe
        protected SDK.SimConnect? _sc;
        private volatile bool _connected;
        private volatile bool _reconnect;
        private TaskCompletionSource<double>? _altitudeRequest;

        // The facilities list can be split by SimConnect into several messages, so we collect them until all arrived.
        private sealed class WaypointCollector
        {
            public readonly List<Waypoint> Items = new();

            // Result is null when the request failed (exception from the sim, disconnect, error while reading).
            public readonly TaskCompletionSource<IReadOnlyList<Waypoint>?> Tcs = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
        }

        // Max messages read in one go while waiting for a waypoint list (see Pump).
        private const int MaxDrain = 1000;

        private readonly SemaphoreSlim _waypointGate = new(1, 1); // one waypoint request at a time
        private volatile WaypointCollector? _waypointCollector;

        // The service runs its own connection thread (no hosted service needed): it is started lazily by the first
        // command / waypoint request (or explicitly with Start) and stopped by Dispose.
        private const int ReconnectDelayMs = 3000;
        private const int MessageWaitMs = 200;
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

        private readonly object _startLock = new();
        private Thread? _thread;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        private readonly Group _grp;

        protected SimConnectService(ILogger<SimConnectService> log, Group grp)
        {
            _log = log;
            _grp = grp;
        }

        public bool IsConnected => _connected;

        // ---------------------------------------------------------------- commands

        public bool Teleport(
            double latitude,
            double longitude,
            double altituOperationDefinitioneet
        ) =>
            Set(
                OperationDefinition.Teleport,
                new Position
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Altitude = altituOperationDefinitioneet,
                },
                "Teleport"
            );

        public bool SetSpeed(double knots) =>
            Set(OperationDefinition.Speed, new Value { V = knots }, "Speed");

        public bool SetIas(double knots)
        {
            // Read the current altitude first, so we can convert IAS to TAS.
            var tcs = new TaskCompletionSource<double>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            _altitudeRequest = tcs;

            var requested = Send(
                sc =>
                    sc.RequestDataOnSimObject(
                        Request.Altitude,
                        OperationDefinition.Altitude,
                        SDK.SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        SDK.SIMCONNECT_PERIOD.ONCE,
                        SDK.SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                        0,
                        0,
                        0
                    ),
                "Altitude request"
            );

            if (!requested || !tcs.Task.Wait(2000))
                return false;

            var tas = knots / Math.Sqrt(DensityRatio(tcs.Task.Result));
            return SetSpeed(tas);
        }

        public bool SetPitch(int degrees) =>
            Set(OperationDefinition.Pitch, new Value { V = degrees * PitchSign }, "Pitch");

        public bool SetHeading(int degrees) =>
            Set(
                OperationDefinition.Heading,
                new Value { V = ((degrees % 360) + 360) % 360 },
                "Heading"
            );

        public bool SetRoll(int degrees) =>
            Set(OperationDefinition.Roll, new Value { V = degrees * RollSign }, "Roll");

        public async Task<IReadOnlyList<Waypoint>?> GetWaypointsAsync(
            CancellationToken cancellationToken = default
        )
        {
            // Nobody else connects for us: start the connection thread and wait until the sim answers.
            // Without this the request below is refused (not connected yet) and the caller gets null.
            Start();
            if (!await WaitForConnectionAsync(ConnectTimeout, cancellationToken))
            {
                _log.LogWarning("Waypoint list not requested: not connected to MSFS");
                return null;
            }

            await _waypointGate.WaitAsync(cancellationToken);
            try
            {
                var collector = new WaypointCollector();
                _waypointCollector = collector;

                // Asks SimConnect for every waypoint currently in the sim's facilities cache.
                var requested = Send(
                    sc =>
                        sc.RequestFacilitiesList(
                            SDK.SIMCONNECT_FACILITY_LIST_TYPE.WAYPOINT,
                            Request.Waypoints
                        ),
                    "Waypoint list request"
                );
                if (!requested)
                    return null;

                // One-shot request: wait for the reply, no timer. The caller can still cancel via the token.
                return await collector.Tcs.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                _waypointCollector = null;
                _waypointGate.Release();
            }
        }

        // ---------------------------------------------------------------- helpers

        private bool Set<T>(OperationDefinition OperationDefinition, T data, string what)
            where T : struct =>
            Send(
                sc =>
                    sc.SetDataOnSimObject(
                        OperationDefinition,
                        SDK.SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        SDK.SIMCONNECT_DATA_SET_FLAG.DEFAULT,
                        data
                    ),
                what
            );

        protected bool Send(Action<SDK.SimConnect> action, string what)
        {
            Start(); // no-op once running; the first command only triggers the connection

            lock (_lock)
            {
                if (!_connected || _sc is null)
                    return false;
                try
                {
                    action(_sc);
                    return true;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "{What} failed", what);
                    _reconnect = true;
                    return false;
                }
            }
        }

        // ISA density ratio (sigma) from altitude in feet. Good enough for IAS -> TAS at normal speeds.
        private static double DensityRatio(double altituOperationDefinitioneet)
        {
            var h = Math.Max(altituOperationDefinitioneet, -1000);
            return h < 36089
                ? Math.Pow(1 - 6.8755856e-6 * h, 4.2558797)
                : 0.2970756 * Math.Exp(-(h - 36089) / 20805.7);
        }

        // ---------------------------------------------------------------- connection thread

        /// <summary>
        /// Starts the connection thread (connect to MSFS, read its messages, reconnect). Safe to call many times.
        /// It is called automatically by the commands and by <see cref="GetWaypointsAsync"/>.
        /// </summary>
        public void Start()
        {
            lock (_startLock)
            {
                if (_thread is not null || _disposed)
                    return;

                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _thread = new Thread(() => RunLoop(token))
                {
                    IsBackground = true, // never keeps the process alive
                    Name = "SimConnect message pump",
                };
                _thread.Start();
            }
        }

        private void RunLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                bool hasConnection;
                lock (_lock)
                {
                    if (_reconnect)
                    {
                        Disconnect();
                        _reconnect = false;
                    }
                    if (_sc is null)
                        TryConnect();

                    hasConnection = _sc is not null;
                }

                if (!hasConnection)
                {
                    // Sim not running yet: wait, but wake up immediately on Dispose.
                    if (ct.WaitHandle.WaitOne(ReconnectDelayMs))
                        break;
                    continue;
                }

                if (_msgEvent.WaitOne(MessageWaitMs))
                {
                    lock (_lock)
                        Pump();
                }
            }

            lock (_lock)
                Disconnect();
        }

        private async Task<bool> WaitForConnectionAsync(TimeSpan timeout, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!_connected)
            {
                if (DateTime.UtcNow >= deadline)
                    return false;
                await Task.Delay(100, ct);
            }
            return true;
        }

        public void Dispose()
        {
            Thread? thread;
            lock (_startLock)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _cts?.Cancel();
                thread = _thread;
            }

            thread?.Join(2000); // the loop disconnects on exit
            _cts?.Dispose();
            _msgEvent.Dispose();
        }

        // _msgEvent is an AutoResetEvent: it is signalled once even when several messages are queued, and
        // ReceiveMessage() reads only one. A waypoint list is split into many messages that arrive together, so
        // reading one per signal leaves the last chunks in the queue and the request never completes.
        // While a waypoint request is waiting we therefore keep reading until it completes (or the queue is empty).
        private void Pump()
        {
            try
            {
                _sc?.ReceiveMessage();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ReceiveMessage failed");
                _reconnect = true;
                return;
            }

            for (
                var i = 0;
                i < MaxDrain && _waypointCollector is { Tcs.Task.IsCompleted: false };
                i++
            )
            {
                try
                {
                    _sc?.ReceiveMessage();
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "No more messages to read");
                    break;
                }
            }
        }

        private void TryConnect()
        {
            try
            {
                var sc = new SDK.SimConnect(_grp.ToString(), IntPtr.Zero, 0, _msgEvent, 0);
                sc.OnRecvOpen += OnOpen;
                sc.OnRecvQuit += (_, _) =>
                {
                    _log.LogInformation("MSFS closed");
                    _connected = false;
                    _reconnect = true;
                };
                sc.OnRecvException += (_, d) =>
                {
                    _log.LogWarning(
                        "SimConnect exception: {Ex}",
                        (SDK.SIMCONNECT_EXCEPTION)d.dwException
                    );
                    // The sim refused something while we wait for the waypoints: don't leave the caller waiting forever.
                    _waypointCollector?.Tcs.TrySetResult(null);
                };
                sc.OnRecvSimobjectData += (_, d) =>
                {
                    if ((Request)d.dwRequestID == Request.Altitude)
                        _altitudeRequest?.TrySetResult(((Value)d.dwData[0]).V);
                };
                sc.OnRecvWaypointList += OnWaypointList;
                _sc = sc;
            }
            catch (Exception)
            {
                _sc = null; // sim not running yet: try again later
            }
        }

        private void OnWaypointList(SDK.SimConnect sc, SDK.SIMCONNECT_RECV_WAYPOINT_LIST data)
        {
            if ((Request)data.dwRequestID != Request.Waypoints)
                return;

            var collector = _waypointCollector;
            if (collector is null)
                return;

            try
            {
                var before = collector.Items.Count;
                if (data.rgData is not null)
                {
                    foreach (var item in data.rgData)
                    {
                        if (item is SDK.SIMCONNECT_DATA_FACILITY_WAYPOINT w)
                            collector.Items.Add(
                                new Waypoint(
                                    w.Ident?.Trim() ?? "",
                                    w.Region?.Trim() ?? "",
                                    w.Latitude,
                                    w.Longitude
                                )
                            );
                    }
                }

                // dwEntryNumber = 0..dwOutOf-1: which message this is; dwOutOf = total messages of the list.
                _log.LogInformation(
                    "Waypoint list message {Entry}/{Total}: {Received} entries, {Parsed} parsed",
                    data.dwEntryNumber + 1,
                    data.dwOutOf,
                    data.dwArraySize,
                    collector.Items.Count - before
                );

                if (data.dwEntryNumber + 1 >= data.dwOutOf)
                    collector.Tcs.TrySetResult(collector.Items);
            }
            catch (Exception ex)
            {
                // Must not escape: it would make ReceiveMessage fail, trigger a reconnect and strand the caller.
                _log.LogError(ex, "Reading the waypoint list failed");
                collector.Tcs.TrySetResult(null);
            }
        }

        private void OnOpen(SDK.SimConnect sc, SDK.SIMCONNECT_RECV_OPEN data)
        {
            _log.LogInformation("Connected to {App}", data.szApplicationName);

            sc.AddToDataDefinition(
                OperationDefinition.Teleport,
                "PLANE LATITUDE",
                "degrees",
                SDK.SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SDK.SimConnect.SIMCONNECT_UNUSED
            );
            sc.AddToDataDefinition(
                OperationDefinition.Teleport,
                "PLANE LONGITUDE",
                "degrees",
                SDK.SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SDK.SimConnect.SIMCONNECT_UNUSED
            );
            sc.AddToDataDefinition(
                OperationDefinition.Teleport,
                "PLANE ALTITUDE",
                "feet",
                SDK.SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SDK.SimConnect.SIMCONNECT_UNUSED
            );
            sc.RegisterDataDefineStruct<Position>(OperationDefinition.Teleport);

            sc.AddToDataDefinition(
                OperationDefinition.Speed,
                "VELOCITY BODY Z",
                "knots",
                SDK.SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SDK.SimConnect.SIMCONNECT_UNUSED
            );
            sc.RegisterDataDefineStruct<Value>(OperationDefinition.Speed);

            sc.AddToDataDefinition(
                OperationDefinition.Pitch,
                "PLANE PITCH DEGREES",
                "degrees",
                SDK.SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SDK.SimConnect.SIMCONNECT_UNUSED
            );
            sc.RegisterDataDefineStruct<Value>(OperationDefinition.Pitch);

            sc.AddToDataDefinition(
                OperationDefinition.Heading,
                "PLANE HEADING DEGREES TRUE",
                "degrees",
                SDK.SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SDK.SimConnect.SIMCONNECT_UNUSED
            );
            sc.RegisterDataDefineStruct<Value>(OperationDefinition.Heading);

            sc.AddToDataDefinition(
                OperationDefinition.Altitude,
                "PLANE ALTITUDE",
                "feet",
                SDK.SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SDK.SimConnect.SIMCONNECT_UNUSED
            );
            sc.RegisterDataDefineStruct<Value>(OperationDefinition.Altitude);

            sc.AddToDataDefinition(
                OperationDefinition.Roll,
                "PLANE BANK DEGREES",
                "degrees",
                SDK.SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SDK.SimConnect.SIMCONNECT_UNUSED
            );
            sc.RegisterDataDefineStruct<Value>(OperationDefinition.Roll);

            _connected = true;
        }

        private void Disconnect()
        {
            _connected = false;
            try
            {
                _sc?.Dispose();
            }
            catch { }
            _sc = null;

            // Connection lost (sim closed, call failed): a pending waypoint request can no longer be answered.
            _waypointCollector?.Tcs.TrySetResult(null);
        }
    }
}
