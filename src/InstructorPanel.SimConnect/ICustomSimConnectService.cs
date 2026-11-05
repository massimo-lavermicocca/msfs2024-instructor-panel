namespace InstructorPanel.SimConnect
{
    public interface ICustomSimConnectService : ISimConnectService
    {
        bool Configure(ConfigurationData configurationData);
    }
}
