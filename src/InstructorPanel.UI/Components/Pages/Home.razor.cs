using InstructorPanel.SimConnect;
using InstructorPanel.UI.Models;
using Microsoft.AspNetCore.Components;
using static SimConnectService;

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

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender)
                return;

            var _simConnect = SimConnectFactory.CreateInstance(
                _serviceProvider,
                AirplaneType.PMDG737
            );

            _wayPoints = await _simConnect.GetWaypointsAsync();

            //_simConnect.PressPmdgButton(PMDG_BUTTONS.EVT_CDU_L_L1);
            //_simConnect.PressPmdgButton(PMDG_BUTTONS.EVT_CDU_L_L1);
            //_simConnect.PressPmdgButton(PMDG_BUTTONS.EVT_CDU_L_R6);
            //_simConnect.PressPmdgButton(PMDG_BUTTONS.EVT_CDU_L_L);
            //_simConnect.PressPmdgButton(PMDG_BUTTONS.EVT_CDU_L_I);
            //_simConnect.PressPmdgButton(PMDG_BUTTONS.EVT_CDU_L_B);
            //_simConnect.PressPmdgButton(PMDG_BUTTONS.EVT_CDU_L_D);
            //_simConnect.PressPmdgButton(PMDG_BUTTONS.EVT_CDU_L_L2);
        }

        protected async Task StartPreparingAircraft()
        {
            //_simConnect = SimConnectFactory.CreateInstance(
            //    _serviceProvider,
            //    _serviceProvider.Services,
            //    AirplaneType.PMDG737
            //);

            //_simConnect.SetIas(Data.Speed);
            //_simConnect.SetSpeed(Data.Speed);

            cancellationTokenSource = new CancellationTokenSource();
            isPreparingAircraft = true;

            //Task.Run(() => PrepareAircraft(_simConnect, Data, cancellationTokenSource.Token));
        }

        protected void Stop()
        {
            cancellationTokenSource.Cancel();
            isPreparingAircraft = false;
        }

        protected async Task PrepareAircraft(
            ISimConnectService simConnect,
            InstructorData data,
            CancellationToken token
        )
        {
            while (!token.IsCancellationRequested)
            {
                simConnect.Teleport(data.Latitude, data.Longitude, data.Altitude);

                await Task.Delay(1000, token);

                simConnect.SetHeading(data.Heading);
                simConnect.SetPitch(3);

                simConnect.SetRoll(0);
            }
        }

        protected async Task<IEnumerable<string>> SearchWaypoint(
            string value,
            CancellationToken token
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
    }
}
