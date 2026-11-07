using InstructorPanel.SimConnect;
using InstructorPanel.SimConnect.Airplanes.PMDG737;
using InstructorPanel.UI.Models;
using Microsoft.AspNetCore.Components;

namespace InstructorPanel.UI.Components.Pages
{
    public partial class Home : ComponentBase
    {
        protected InstructorData Data { get; set; } = new();
        protected CancellationTokenSource cancellationTokenSource;
        protected bool isPreparingAircraft;

        protected string SelectedWaypoint = null;

        [Inject]
        protected IServiceProvider _serviceProvider { get; set; }

        protected IReadOnlyList<Waypoint?> _wayPoints { get; set; } = new List<Waypoint>();

        private ICustomSimConnectService _simConnect;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender)
                return;
        }

        protected async Task StartPreparingAircraft()
        {
            cancellationTokenSource = new CancellationTokenSource();
            isPreparingAircraft = true;

            _ = Task.Run(() => PrepareAircraft(cancellationTokenSource.Token));
        }

        protected void Stop()
        {
            cancellationTokenSource.Cancel();
            isPreparingAircraft = false;
        }

        protected async Task PrepareAircraft(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _simConnect.Configure(new ConfigurationData() { });

                _simConnect.Teleport(Data.Latitude, Data.Longitude, Data.Altitude);

                await Task.Delay(1000, token);

                _simConnect.SetHeading(Data.Heading);
                _simConnect.SetPitch(3);
                _simConnect.SetRoll(0);
                _simConnect.SetIas(Data.Speed);
            }
        }

        protected async Task<IEnumerable<string>> SearchWaypoint(
            string value,
            CancellationToken cancellationToken = default
        )
        {
            return _wayPoints is not null
                ? _wayPoints
                    .Where(x =>
                        x.Name.Contains(value ?? "", StringComparison.InvariantCultureIgnoreCase)
                    )
                    .Select(x => x.Name)
                    .OrderBy(x => x)
                : new List<string>();
        }

        protected void OnWaypointSelected(string value)
        {
            if (!string.IsNullOrEmpty(value) && _wayPoints.Any(x => x.Name == value))
            {
                var selectedWaypoint = _wayPoints.First(x => x.Name == value);
                Data.Latitude = selectedWaypoint.Latitude;
                Data.Longitude = selectedWaypoint.Longitude;
            }
        }

        protected async Task OnAircraftTypeSelected(AirplaneType? airplaneType)
        {
            if (airplaneType.HasValue)
            {
                _simConnect = SimConnectFactory.CreateInstance(
                    _serviceProvider,
                    airplaneType.Value
                );

                _wayPoints = await _simConnect.GetWaypointsAsync();
            }
        }

        protected Task<IEnumerable<AirplaneType?>> SearchAircraftType(
            string _,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(
                Enum.GetValues<AirplaneType>()
                    .Where(x => x != AirplaneType.Undefined)
                    .Select(x => x as AirplaneType?)
            );
        }
    }
}
