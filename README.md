# MSFS 2024 Instructor Panel

A web-based instructor panel for **Microsoft Flight Simulator 2024**, inspired by the instructor stations (FSI panels) used with full-flight simulators. It lets an instructor reposition the aircraft and set its flight state from a browser while the sim is running, using SimConnect.

The first target aircraft is the **PMDG 737**.

> **Status: early work in progress, never tested against a running simulator.** This README describes the code as it stands in the repository.

## What it does today

From a single page the instructor can:

- Pick a **waypoint** by name (autocomplete) to fill in latitude and longitude
- Enter latitude / longitude, altitude (feet MSL), heading (true, degrees) and speed (knots)
- Press **Prepare aircraft**: the speed is applied, then the aircraft is teleported to the position and its heading, pitch (fixed at 3°) and roll (fixed at 0°) are re-applied once per second until **Stop** is pressed

The backend can also set the PMDG 737 flap lever, but this is not exposed in the UI yet.

### Waypoint list

Waypoints come from the simulator's **facilities cache** through one SimConnect request (`RequestFacilitiesList`) made when the page first renders. The cache only holds what MSFS currently has loaded around the aircraft, so this is not the full navigation database. If the sim is not connected when the page loads, the list stays empty until the page is reloaded.

## Architecture

```
InstructorPanel.slnx
├── SDK/                                 MSFS SimConnect SDK (headers, native + managed libs)
└── src/
    ├── InstructorPanel.SimConnect/      Class library: SimConnect integration
    │   ├── ISimConnectService.cs        Contract used by the UI (+ Waypoint record)
    │   └── SimConnectService.cs         BackgroundService: connection loop, commands, waypoint list
    └── InstructorPanel.UI/              Blazor Server app (interactive server render mode)
        ├── Components/Pages/Home.razor      Instructor form (MudBlazor)
        ├── Components/Pages/Home.razor.cs   Page logic: prepare / stop loop, waypoint search
        └── Models/InstructorData.cs         Form model
```

- **Target framework:** .NET 10
- **UI:** Blazor Server with [MudBlazor](https://mudblazor.com/) (Bootstrap was removed)
- **Sim link:** managed SimConnect (`Microsoft.FlightSimulator.SimConnect`), referenced from `SDK/lib/managed`. `SimConnect.dll` is copied next to the build output.

`SimConnectService` is registered as a singleton, as `ISimConnectService`, and as a hosted service, so the UI and the background loop share one connection. It retries the connection to MSFS every 3 seconds, reconnects if the sim closes or a call fails, and serialises all SimConnect calls behind a lock because SimConnect is not thread-safe.

### Commands (`ISimConnectService`)

| Method | Description |
| --- | --- |
| `Teleport(lat, lon, altFeet)` | Move the aircraft (decimal degrees, feet MSL) |
| `SetSpeed(knots)` | Forward speed along the nose axis (true speed) |
| `SetIas(knots)` | Indicated airspeed, converted to true speed from the current altitude (ISA density ratio) |
| `SetPitch(degrees)` | Positive = nose up |
| `SetRoll(degrees)` | Positive = roll right |
| `SetHeading(degrees)` | True heading, normalised to 0-359 |
| `SetFlaps(detent)` | PMDG 737 flap lever: 1, 5, 15, 30, 40 |
| `GetWaypointsAsync()` | One-shot list of the waypoints in the sim's facilities cache (name, region, lat, lon) |

Pitch and roll signs are flipped relative to the raw SimConnect values (`PitchSign` / `RollSign` in `SimConnectService.cs`). If the direction is wrong in your tests, change the constant to `1`.

## Requirements

- Windows (SimConnect is a Windows API)
- Microsoft Flight Simulator 2024
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PMDG 737 for flap control (everything else works with any aircraft)
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

## Known issues

- **The Stop button does not stop.** In `Home.razor` the Stop button is wired to `StartPreparingAircraft` instead of `Stop`, so pressing it starts another loop.
- **Waypoint list can fail on load.** If the sim is not connected, `GetWaypointsAsync` returns `null`, which is assigned to the list and makes the search throw. The list is also loaded in `OnAfterRenderAsync` without a re-render afterwards.
- **IAS is overridden.** `SetSpeed` is called right after `SetIas` with the same value, replacing the IAS-to-TAS conversion.
- **`SetIas` blocks** the calling thread for up to 2 seconds waiting for the altitude reply.
- **Loop cleanup.** The prepare loop is started with a fire-and-forget `Task.Run`, throws `TaskCanceledException` when stopped, and keeps running if the browser tab is closed (the page does not cancel it on dispose).
- **MudBlazor providers** (`MudThemeProvider`, dialog, snackbar, popover) are declared in `Home.razor`; they belong once in `MainLayout.razor`.
- **Flaps:** only the 5° detent has a parameter set; 1, 15, 30 and 40 are placeholders (`0`). The warning message refers to a Pmdg section in `appsettings.json` that does not exist yet.
- Pitch (3°) and roll (0°) are hard-coded in the prepare loop.

## Roadmap

- [ ] Fix the known issues above
- [ ] Test against a running MSFS 2024 and confirm pitch / roll signs
- [ ] UI controls for pitch, roll and flaps
- [ ] Complete the PMDG 737 flap event parameters
- [ ] Move PMDG / SimConnect settings into `appsettings.json`
- [ ] Move shared MudBlazor providers into the layout
- [ ] Further FSI-style features (weather, failures, scenario presets)

## Credits

Uses the Microsoft Flight Simulator SimConnect SDK (files in `SDK/`) and [MudBlazor](https://mudblazor.com/).