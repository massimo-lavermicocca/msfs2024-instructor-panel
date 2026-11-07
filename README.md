# MSFS 2024 Instructor Panel

A web-based instructor panel for **Microsoft Flight Simulator 2024**, inspired by the instructor stations (FSI panels) used with full-flight simulators. It lets an instructor reposition the aircraft and set its flight state from a browser while the sim is running, using SimConnect.

The first supported aircraft is the **PMDG 737**. SimConnect access is split into a generic part and an aircraft-specific part, so other aircraft can be added without touching the generic code.

> **Status: early work in progress, never tested against a running simulator.** This README describes the code as it stands in the repository.

## What it does today

From a single page the instructor can:

- Pick a **waypoint** by name (autocomplete) to fill in latitude and longitude
- Enter latitude / longitude, altitude (feet MSL), heading (true, degrees) and speed (knots, **indicated**)
- Press **Prepare aircraft**: the aircraft is teleported to the position, then heading, pitch (fixed at 3°), roll (fixed at 0°) and indicated speed are applied. This repeats about once per second until **Stop** is pressed.

The backend can also set the PMDG 737 flap lever (1, 5, 15, 30, 40) and press PMDG FMC keys, but neither is exposed in the UI yet.

### Waypoint list

Waypoints come from the simulator's **facilities cache** through one SimConnect request (`RequestFacilitiesList`) made when the page first renders. The cache only holds what MSFS currently has loaded around the aircraft, so this is not the full navigation database. The request waits up to 15 seconds for the sim to connect; if it never does, the list stays empty until the page is reloaded.

## Architecture

```
InstructorPanel.slnx
├── SDK/                                    MSFS SimConnect SDK (headers, native + managed libs)
└── src/
    ├── InstructorPanel.SimConnect/         Class library: SimConnect integration
    │   ├── ISimConnectService.cs           Generic contract (+ Waypoint record)
    │   ├── ICustomSimConnectService.cs     Aircraft-specific contract (Configure, SetFlaps)
    │   ├── SimConnectService.cs            Abstract base: connection thread, generic commands, waypoint list
    │   ├── SimConnectFactory.cs            Creates the service for an AirplaneType
    │   ├── AirplaneType.cs, ConfigurationData.cs
    │   ├── Enums/                          SimConnect ids (data definitions, requests, events, groups)
    │   └── Airplanes/PMDG737/
    │       ├── PMDG738.cs                  PMDG 737 service: flaps and FMC key presses
    │       ├── Models/PMDG_FMC_BUTTONS.cs  FMC key event offsets
    │       └── Converters/                 Text to FMC key sequence
    └── InstructorPanel.UI/                 Blazor Server app (interactive server render mode)
        ├── Components/Pages/Home.razor     Instructor form (MudBlazor)
        ├── Components/Pages/Home.razor.cs  Page logic: prepare / stop loop, waypoint search
        └── Models/InstructorData.cs        Form model
```

- **Target framework:** .NET 10
- **UI:** Blazor Server with [MudBlazor](https://mudblazor.com/)
- **Sim link:** managed SimConnect (`Microsoft.FlightSimulator.SimConnect`), referenced from `SDK/lib/managed`. `SimConnect.dll` is copied next to the build output.

### Generic and aircraft-specific services

`SimConnectService` is an abstract base class and is **not** a hosted service. It owns its own connection thread, which:

- starts lazily on the first command or waypoint request (or explicitly with `Start()`),
- connects to MSFS, retrying every 3 seconds until the sim is running,
- reads the SimConnect message queue and reconnects if the sim closes or a call fails,
- stops, and closes the connection, on `Dispose()`.

All SimConnect calls are serialised behind a lock because SimConnect is not thread-safe.

Aircraft-specific behaviour lives in a subclass such as `PMDG738`, which implements `ICustomSimConnectService` (an extension of `ISimConnectService`). `SimConnectFactory.CreateInstance(serviceProvider, airplaneType)` returns the right one. Nothing is registered in `Program.cs` for SimConnect: `Home.razor.cs` currently asks the factory for a PMDG 737 service when the page first renders.

### Commands

`ISimConnectService` (any aircraft):

| Method | Description |
| --- | --- |
| `IsConnected` | True when connected to MSFS |
| `Teleport(lat, lon, altFeet)` | Move the aircraft (decimal degrees, feet MSL) |
| `SetSpeed(knots)` | Forward speed along the nose axis (true speed) |
| `SetIas(knots)` | Indicated airspeed, converted to true speed from the current altitude (ISA density ratio) |
| `SetPitch(degrees)` | Positive = nose up |
| `SetRoll(degrees)` | Positive = roll right |
| `SetHeading(degrees)` | True heading, normalised to 0-359 |
| `GetWaypointsAsync()` | One-shot list of the waypoints in the sim's facilities cache (name, region, lat, lon) |

`ICustomSimConnectService` (aircraft-specific, implemented by `PMDG738`):

| Method | Description |
| --- | --- |
| `SetFlaps(detent)` | PMDG 737 flap lever: 1, 5, 15, 30, 40 |
| `Configure(ConfigurationData)` | Types the airport code into the FMC by pressing the CDU keys (see known issues) |

PMDG events are addressed as `THIRD_PARTY_EVENT_ID_MIN + offset`, with the offsets taken from `PMDG_NG3_SDK.h` in the aircraft's `Documentation\SDK` folder.

Pitch and roll signs are flipped relative to the raw SimConnect values (`PitchSign` / `RollSign` in `SimConnectService.cs`). If the direction is wrong in your tests, change the constant to `1`.

## Requirements

- Windows (SimConnect is a Windows API)
- Microsoft Flight Simulator 2024
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PMDG 737 for flaps and FMC control (the generic commands work with any aircraft)
- Internet access in the browser: the UI loads the Roboto font from Google Fonts

## Getting started

```bash
git clone https://github.com/massimo-lavermicocca/msfs2024-instructor-panel
cd msfs2024-instructor-panel
dotnet run --project src/InstructorPanel.UI
```

1. Start MSFS 2024 and load a flight. The panel connects automatically once the sim is up, but start the sim first if you want the waypoint list to load.
2. Open the URL printed in the console (ports are set in `src/InstructorPanel.UI/Properties/launchSettings.json`).
3. Choose a waypoint or type a position, then press **Prepare aircraft**.

### Local default values (Debug builds only)

In `DEBUG` builds, `InstructorData` reads its starting values from `appsettings.Development.Custom.json`, which is git-ignored so each developer can keep their own. The file is looked up in the working directory and a missing or invalid file is silently ignored.

```json
{
  "Latitude": 41.05,
  "Longitude": 16.455,
  "Altitude": 3000,
  "Heading": 65,
  "Speed": 200
}
```

`Altitude`, `Heading` and `Speed` must be whole numbers, and `Latitude` / `Longitude` must contain a decimal point (`41.0`, not `41`), otherwise the cast fails and none of the values after it are loaded.

### Code style

Formatting is done with [CSharpier](https://csharpier.com/) (version pinned in `dotnet-tools.json`) and `.editorconfig`:

```bash
dotnet tool restore
dotnet csharpier format .
```

## Known issues

These come from reading the code; none of it has been run against the simulator.

- **`PMDG738` probably fails on creation.** Its constructor calls `RegisterCustomEvents()`, which uses `_sc`. That field is only assigned once the connection thread connects to the sim, so it is still `null` in the constructor.
- **`Configure` presses no keys.** `PressFmcButtons` calls `buttons.Select(PressFmcButton)` and never enumerates the result, so the lazy query does nothing. The page also calls `Configure` with an empty `ConfigurationData` on every loop iteration, and `ConfigurationData.Airport` is not filled from the UI.
- **One service per page load.** `Home.razor.cs` creates a new `PMDG738` through the factory each time the page first renders and never disposes it. Each browser tab therefore gets its own connection thread and SimConnect connection. The service is not registered with dependency injection, and the aircraft type (`PMDG737`) is hard-coded in the page.
- **Prepare loop cleanup.** The loop is started with a fire-and-forget `Task.Run`, throws `TaskCanceledException` from `Task.Delay` when stopped, and keeps running if the browser tab is closed (the page does not cancel it on dispose).
- **`SetIas` blocks** the calling thread for up to 2 seconds waiting for the altitude reply.
- **Waypoint list can be `null`.** If the request fails, `_wayPoints` becomes `null`; `SearchWaypoint` handles that but `OnWaypointSelected` does not.
- **`SetFlaps` throws on an invalid detent.** `_flapsOffsets[detent]` raises `KeyNotFoundException` for anything other than 1, 5, 15, 30 or 40.
- **MudBlazor providers** (`MudThemeProvider`, dialog, snackbar, popover) are declared in `Home.razor`; they belong once in `MainLayout.razor`.
- Pitch (3°) and roll (0°) are hard-coded in the prepare loop.
- Cosmetic: the altitude parameter of `Teleport` and `DensityRatio` is misspelt (`altituOperationDefinitioneet`), `SimConnectFactory` returns `null` for an unknown aircraft type, and the `Microsoft.Extensions.Hosting.Abstractions` package reference in the SimConnect project is no longer needed.

## Credits

Uses the Microsoft Flight Simulator SimConnect SDK (files in `SDK/`), the PMDG 737 SDK event list and [MudBlazor](https://mudblazor.com/).
