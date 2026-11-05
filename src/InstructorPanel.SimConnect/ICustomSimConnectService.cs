namespace InstructorPanel.SimConnect
{
    public interface ICustomSimConnectService : ISimConnectService
    {
        Task<bool> Configure(ConfigurationData configurationData);
    }
}
