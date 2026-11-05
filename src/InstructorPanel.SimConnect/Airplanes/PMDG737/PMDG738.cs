using System;
using InstructorPanel.SimConnect.Airplanes.PMDG737.Converters;
using InstructorPanel.SimConnect.Airplanes.PMDG737.Models;
using Microsoft.Extensions.Logging;
using Microsoft.FlightSimulator.SimConnect;

namespace InstructorPanel.SimConnect.Airplanes.PMDG737
{
    public sealed class PMDG738 : SimConnectService, ICustomSimConnectService
    {
        private static Grp _grp = Grp.Pmdg;

        private readonly ILogger<PMDG738> _logger;

        // THIRD_PARTY_EVENT_ID_MIN in PMDG_NG3_SDK.h (0x00011000 = 69632).
        private const uint THIRD_PARTY_EVENT_ID_MIN = 0x00011000;

        // MOUSE_FLAG_LEFTSINGLE: the "left click" parameter PMDG expects (same value used for the flaps below).
        private const uint MouseFlagLeftSingle = 0x20000000;

        // PMDG buttons already mapped on the current SimConnect connection (only touched while holding _lock).
        private readonly HashSet<uint> _mappedPmdgButtons = new();

        public PMDG738(ILoggerFactory loggerFactory)
            : base(loggerFactory.CreateLogger<SimConnectService>(), _grp)
        {
            _logger = loggerFactory.CreateLogger<PMDG738>();

            //base.ExecuteAsync(new());
        }

        public bool Configure(ConfigurationData configurationData)
        {
            var _converter = new StringToFmcButtonConverter();

            PMDG_FMC_BUTTONS[] x = _converter.Convert(configurationData.Location);

            PressFmcButtons(x);

            return true;
        }

        private void PressFmcButtons(PMDG_FMC_BUTTONS[] buttons)
        {
            buttons.Select(PressFmcButton);
        }

        /// <summary>
        /// Presses a single PMDG 737 cockpit button with a left click, e.g. an FMC/CDU key.
        /// <paramref name="sdkEventOffset"/> is the N of <c>THIRD_PARTY_EVENT_ID_MIN + N</c> in the aircraft's
        /// <c>PMDG_NG3_SDK.h</c> (Documentation\SDK folder of the PMDG 737 package), for example 573 for EVT_CDU_L_A.
        /// Returns false if the sim is not connected or the offset is not valid.
        /// </summary>
        private bool PressFmcButton(PMDG_FMC_BUTTONS button)
        {
            var eventId = THIRD_PARTY_EVENT_ID_MIN + (uint)button;

            return Send(
                sc =>
                {
                    // Each event is mapped once per connection; the "#<id>" name addresses a PMDG custom event by number.
                    if (_mappedPmdgButtons.Add((uint)button))
                    {
                        sc.MapClientEventToSimEvent(button, "#" + eventId);
                        sc.AddClientEventToNotificationGroup(Grp.Pmdg, button, false);
                        sc.SetNotificationGroupPriority(
                            Grp.Pmdg,
                            Microsoft
                                .FlightSimulator
                                .SimConnect
                                .SimConnect
                                .SIMCONNECT_GROUP_PRIORITY_HIGHEST
                        );
                    }

                    sc.TransmitClientEvent(
                        Microsoft.FlightSimulator.SimConnect.SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        button,
                        MouseFlagLeftSingle,
                        Grp.Pmdg,
                        SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                    );
                },
                "PMDG button"
            );
        }
    }
}
