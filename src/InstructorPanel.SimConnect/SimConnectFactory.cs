using InstructorPanel.SimConnect.Airplanes.PMDG737;
using Microsoft.Extensions.DependencyInjection;

namespace InstructorPanel.SimConnect
{
    public static class SimConnectFactory
    {
        public static ICustomSimConnectService CreateInstance(
            IServiceProvider serviceProvider,
            IServiceScopeFactory serviceScopeFactory,
            //IServiceCollection serviceCollection,
            AirplaneType airplaneType
        )
        {
            switch (airplaneType)
            {
                case AirplaneType.PMDG737:

                    var x = serviceScopeFactory.CreateScope();

                    //serviceCollection.AddSingleton<PMDG738>();
                    //serviceCollection.AddSingleton<ICustomSimConnectService>(sp =>
                    //    sp.GetRequiredService<PMDG738>()
                    //);
                    //serviceCollection.AddHostedService(sp => sp.GetRequiredService<PMDG738>());

                    return serviceProvider.GetRequiredService<ICustomSimConnectService>();
            }

            return null;
        }
    }
}
