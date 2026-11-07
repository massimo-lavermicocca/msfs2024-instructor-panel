using System;
using InstructorPanel.SimConnect.Airplanes.PMDG737.Converters;
using InstructorPanel.SimConnect.Airplanes.PMDG737.Models;
using InstructorPanel.SimConnect.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.FlightSimulator.SimConnect;
using SDK = Microsoft.FlightSimulator.SimConnect;

namespace InstructorPanel.SimConnect.Airplanes.PMDG737
{
    public sealed class PMDG738 : SimConnectService, ICustomSimConnectService
    {
        private enum PmdgButton : uint { }

        private static Group _grp = Group.Pmdg;

        private readonly ILogger<PMDG738> _logger;

        // THIRD_PARTY_EVENT_ID_MIN in PMDG_NG3_SDK.h (0x00011000 = 69632).
        private const uint THIRD_PARTY_EVENT_ID_MIN = 0x00011000;

        // MOUSE_FLAG_LEFTSINGLE: the "left click" parameter PMDG expects (same value used for the flaps below).
        private const uint MouseFlagLeftSingle = 0x20000000;

        // PMDG buttons already mapped on the current SimConnect connection (only touched while holding _lock).
        private readonly HashSet<uint> _mappedPmdgButtons = new();

        private static readonly Dictionary<int, int> _flapsOffsets = new()
        {
            { 1, 7142 }, // EVT_CONTROL_STAND_FLAPS_LEVER_1  -> 76774
            { 5, 7144 }, // EVT_CONTROL_STAND_FLAPS_LEVER_5  -> 76776
            { 15, 7146 }, // EVT_CONTROL_STAND_FLAPS_LEVER_15 -> 76778
            { 30, 7148 }, // EVT_CONTROL_STAND_FLAPS_LEVER_30 -> 76780
            { 40, 7149 }, // EVT_CONTROL_STAND_FLAPS_LEVER_40 -> 76781
        };

        private readonly uint _flapsEventId = 76776;

        public PMDG738(ILoggerFactory loggerFactory)
            : base(loggerFactory.CreateLogger<SimConnectService>(), _grp)
        {
            _logger = loggerFactory.CreateLogger<PMDG738>();
            RegisterCustomEvents();
        }

        public Task<bool> Configure(ConfigurationData configurationData)
        {
            var _converter = new StringToFmcButtonConverter();

            PMDG_FMC_BUTTONS[] locationCommand = _converter.Convert(configurationData.Airport);

            PressFmcButtons(locationCommand);

            return Task.FromResult(true);
        }

        public bool SetFlaps(int detent)
        {
            var eventId = THIRD_PARTY_EVENT_ID_MIN + (uint)_flapsOffsets[detent];
            var button = (PmdgButton)eventId;

            return Send(
                sc =>
                {
                    // Each event is mapped once per connection; the "#<id>" name addresses a PMDG custom event by number.
                    if (_mappedPmdgButtons.Add(eventId))
                    {
                        sc.MapClientEventToSimEvent(button, "#" + eventId);
                        sc.AddClientEventToNotificationGroup(Group.Pmdg, button, false);
                        sc.SetNotificationGroupPriority(
                            Group.Pmdg,
                            SDK.SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST
                        );
                    }

                    sc.TransmitClientEvent(
                        SDK.SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        button,
                        MouseFlagLeftSingle,
                        Group.Pmdg,
                        SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                    );
                },
                "PMDG button"
            );
        }

        private void RegisterCustomEvents()
        {
            if (_flapsEventId != 0)
            {
                _sc.MapClientEventToSimEvent(Evt.PmdgFlaps, "#" + _flapsEventId);
                _sc.AddClientEventToNotificationGroup(_grp, Evt.PmdgFlaps, false);
                _sc.SetNotificationGroupPriority(
                    _grp,
                    SDK.SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST
                );
            }
        }

        private void PressFmcButtons(PMDG_FMC_BUTTONS[] buttons)
        {
            if (buttons.Length == 0)
                return;

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
                        sc.AddClientEventToNotificationGroup(Group.Pmdg, button, false);
                        sc.SetNotificationGroupPriority(
                            Group.Pmdg,
                            SDK.SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST
                        );
                    }

                    sc.TransmitClientEvent(
                        SDK.SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        button,
                        MouseFlagLeftSingle,
                        Group.Pmdg,
                        SDK.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
                    );
                },
                "PMDG button"
            );
        }
    }
}
