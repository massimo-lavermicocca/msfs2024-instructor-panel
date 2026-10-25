using InstructorPanel.SimConnect;
using InstructorPanel.UI.Models;
using Microsoft.AspNetCore.Components;

namespace InstructorPanel.UI.Components.Pages
{
    public partial class Home : ComponentBase
    {
        protected InstructorData data { get; set; } = new();
        protected CancellationTokenSource cancellationTokenSource;
        protected bool isPreparingAircraft;
        protected ISimConnectService _simConnect;

        public Home(ISimConnectService simConnect)
        {
            _simConnect = simConnect;
        }

        protected override void OnInitialized()
        {
            isPreparingAircraft = false;
        }

        protected async Task StartPreparingAircraft()
        {

            _simConnect.SetIas(data.Speed);
            _simConnect.SetSpeed(data.Speed);

            cancellationTokenSource = new CancellationTokenSource();
            isPreparingAircraft = true;

            Task.Run(() => PrepareAircraft(_simConnect, data, cancellationTokenSource.Token));
        }

        protected void Stop()
        {
            cancellationTokenSource.Cancel();
            isPreparingAircraft = false;
        }

        protected async Task PrepareAircraft(ISimConnectService simConnect, InstructorData data, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                simConnect.Teleport(data.Latitude, data.Longitude, data.Altitude);

                await Task.Delay(1000);

                simConnect.SetHeading(data.Heading);
                simConnect.SetPitch(3);

                simConnect.SetRoll(0);
            }
        }
    }
}
