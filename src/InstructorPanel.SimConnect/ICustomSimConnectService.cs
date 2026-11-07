namespace InstructorPanel.SimConnect
{
    public interface ICustomSimConnectService : ISimConnectService
    {
        /// <summary>Configure the FMC, waights</summary>
        Task<bool> Configure(ConfigurationData configurationData);

        /// <summary>Sets the PMDG 737 flap lever. Valid detents: 1, 5, 15, 30, 40.</summary>
        bool SetFlaps(int detent);
    }
}
