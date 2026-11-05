using InstructorPanel.SimConnect.Airplanes.PMDG737;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InstructorPanel.SimConnect
{
    public static class SimConnectFactory
    {
        public static ICustomSimConnectService CreateInstance(
            IServiceProvider serviceProvider,
            AirplaneType airplaneType
        )
        {
            switch (airplaneType)
            {
                case AirplaneType.PMDG737:
                    var x = new PMDG738(serviceProvider.GetRequiredService<ILoggerFactory>());
                    x.StartAsync(new());
                    return x;
            }

            return null;
        }
    }
}
