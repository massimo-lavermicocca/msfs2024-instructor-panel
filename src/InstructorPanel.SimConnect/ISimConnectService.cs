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

        /// <summary>
        /// Returns all the waypoints currently held in the simulator's facilities cache
        /// (name/ident, region and coordinates in decimal degrees).
        /// Performs a single one-shot request when called (no subscription, no timer).
        /// Returns null if the sim is not connected or the request cannot be sent.
        /// </summary>
        Task<IReadOnlyList<Waypoint>?> GetWaypointsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>A waypoint (fix/intersection) from the simulator's facilities cache.</summary>
    /// <param name="Name">Waypoint identifier (e.g. "TOC01").</param>
    /// <param name="Region">ICAO region code of the waypoint (may be empty).</param>
    /// <param name="Latitude">Latitude in decimal degrees.</param>
    /// <param name="Longitude">Longitude in decimal degrees.</param>
    public sealed record Waypoint(string Name, string Region, double Latitude, double Longitude);

}