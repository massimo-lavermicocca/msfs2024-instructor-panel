namespace InstructorPanel.SimConnect
{
    public interface ISimConnectService
    {
        /// <summary>True when connected to MSFS.</summary>
        bool IsConnected { get; }

        /// <summary>Moves the aircraft. Altitude in feet MSL, coordinates in decimal degrees.</summary>
        bool Teleport(double latitude, double longitude, double altitudeFeet);

        /// <summary>Sets the forward speed along the nose axis (true speed) in knots.</summary>
        bool SetSpeed(double knots);

        /// <summary>Sets the indicated airspeed in knots (converted to true speed using the current altitude).</summary>
        bool SetIas(double knots);

        /// <summary>Sets pitch in whole degrees (positive = nose up).</summary>
        bool SetPitch(int degrees);

        /// <summary>Sets true heading in whole degrees (0-359).</summary>
        bool SetHeading(int degrees);

        /// <summary>Sets the PMDG 737 flap lever. Valid detents: 1, 5, 15, 30, 40.</summary>
        bool SetFlaps(int detent);

        /// <summary>Sets roll in degree.</summary>
        bool SetRoll(int roll);
    }

}
