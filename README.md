# MSFS 2024 Instructor Panel

A web-based instructor panel for **Microsoft Flight Simulator 2024**, instructor panels used in full-flight simulators. It lets an instructor reposition the aircraft and set its flight state from a browser while the sim is running, using SimConnect. For now, the project has never been tested once.

The first target aircraft is the **PMDG 737**.

> **Status: early work in progress.** This README describes the project as it stands today and is the baseline for what comes next.

## What it does today

From a single page, the instructor enters a position and flight state and presses **Fly**:

- Latitude / longitude
- Altitude (feet MSL)
- Heading (true, degrees)
- Speed (knots)

The backend can also set pitch, roll and the PMDG 737 flap lever, but these are not yet exposed in the UI (pitch and roll are currently hard-coded when you press Fly).

## Architecture

```
InstructorPanel.slnx
├── SDK/                              MSFS SimConnect SDK (headers, native + managed libs)
└── src/
    ├── InstructorPanel.SimConnect/   Class library: SimConnect integration
    │   ├── ISimConnectService.cs     Public contract used by the UI
    │   └── SimConnectService.cs      BackgroundService: connection loop + commands
    └── InstructorPanel.UI/           Blazor Server app (interactive server render mode)
        └── Components/Pages/Home.razor   The instructor form
```

- **Target framework:** .NET 10
- **UI:** Blazor Server with Bootstrap
- **Sim link:** managed SimConnect (`Microsoft.FlightSimulator.SimConnect`), referenced from `SDK/lib/managed`. `SimConnect.dll` is copied next to the build output.

`SimConnectService` is registered as a singleton and as a hosted service. It keeps trying to connect to MSFS every 3 seconds, reconnects automatically if the sim closes or a call fails, and serialises all SimConnect calls behind a lock because SimConnect is not thread-safe.

### Available commands (`ISimConnectService`)

| Method | Description |
| --- | --- |
| `Teleport(lat, lon, altFeet)` | Move the aircraft (decimal degrees, feet MSL) |
| `SetSpeed(knots)` | Forward speed along the nose axis (true speed) |
| `SetIas(knots)` | Indicated airspeed, converted to true speed using the current altitude (ISA density ratio) |
| `SetPitch(degrees)` | Positive = nose up |
| `SetRoll(degrees)` | Positive = roll right (sign configurable) |
| `SetHeading(degrees)` | True heading, normalised to 0-359 |
| `SetFlaps(detent)` | PMDG 737 flap lever: 1, 5, 15, 30, 40 |

Pitch and roll signs are flipped relative to the raw SimConnect values (`PitchSign` / `RollSign` constants in `SimConnectService.cs`). If the direction is wrong in your tests, change the constant to `1`.

## Requirements

- Windows (SimConnect is a Windows API)
- Microsoft Flight Simulator 2024
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PMDG 737 for flap control (everything else works with any aircraft)

## Getting started

```bash
git clone https://github.com/massimo-lavermicocca/msfs2024-instructor-panel
cd msfs2024-instructor-panel
dotnet run --project src/InstructorPanel.UI
```

1. Start MSFS 2024 and load a flight (the panel connects automatically once the sim is up; you can start the panel first).
2. Open the URL printed in the console (ports are defined in `src/InstructorPanel.UI/Properties/launchSettings.json`).
3. Fill in the form and press **Fly**.

## Known issues

TBD

## Roadmap

TBD

## Credits

Uses the Microsoft Flight Simulator SimConnect SDK (files in `SDK/`).
