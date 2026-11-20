# Air Cleaning VR: Emulsifier Operator Trainer

A VR trainer for operators of an industrial gas-cleaning emulsifier, built in Unity 6. The trainee works inside a full-scale workshop: puts on PPE, sets up the plant from a tablet or a manual control post, brings gas cleaning up to the target level, and then deals with equipment faults by reading the instruments. In **Training** mode a 23-step guided scenario walks through every fault. In **Exam** mode a fault starts without warning, and the trainee gets a score out of 100.

The emulsifier, its calculation formulas and its fluid animation come from the desktop digital twin [AirCleaningMachine](https://github.com/MerlanOurBoss/AirCleaningMachine). This project rebuilds that unit for VR, as an operator workplace.

<!-- Add a 20–40 s gameplay GIF or video link here, recorded on Quest or in the XR Interaction Simulator, e.g.:
![Gameplay](docs/gameplay.gif) -->

![Workshop model, Blender render](Blender/renders/r_cut.png)
*Workshop model (Blender render, before Unity materials and lighting).*

The interface is available in **Kazakh, Russian and English**.

## What the trainee does

1. **Inspect the plant.** Point the ray at any of the 8 plant nodes: a frame and a hint appear. Press the trigger or grip, or pinch with hand tracking, to open the description. The nodes are the gas inlet, pump, column, pressure gauge, level gauge, tank, gas outlet and exhaust fan.
2. **Put on PPE.** At the stand, grab the hard hat, goggles and respirator and bring them to your head and face. Put the gloves on with the ray: they then replace your hand model. The danger zone is marked with a yellow line on the floor.
3. **Set up the plant.** On the tablet, choose the liquid (water, soda or caustic soda) and pump power (1–4). At the manual control post, set the liquid flow with the valve. The tablet and the post stay in sync both ways.
4. **Start and reach normal operation.** Start the plant with the breaker. The smoke at the column outlet gets lighter as cleaning improves. The target is at least 80 % cleaning.
5. **Handle faults.** Notice the fault on the instruments, stop the plant, lock the breaker, fix the fault on the tablet, remove the lock and restart.

## Faults

| Fault | What the trainee sees | Fix |
|---|---|---|
| Clog | Pressure rises on the gauge | Clean |
| Level drop | Level falls in the level gauge; below the minimum mark, water stops and pressure rises | Refill |
| Pump failure | Liquid jets in the column disappear, the pump sound stops, pressure rises | Repair pump |
| Exhaust fan failure | Smoke disappears, the fan hum stops, pressure rises | Repair fan |
| Faulty pressure gauge | The gauge shows a rise, but the siren is silent and the pressure sensor on the tablet is normal | Replace gauge |

Pressure is normally 2.5 kPa. The alarm (siren and red lamp) starts at 6 kPa, and at 10 kPa the column bursts. Pressure rises only while the plant is running, so **Stop is always the right first action**. Repairs are refused unless the plant is stopped, the breaker is locked and the PPE is on. Refilling only needs the PPE.

A fault can also be caused by **wrong settings**. A dangerous combination held for 12 seconds on a running plant starts one:

| Setting | Result |
|---|---|
| Soda at 5 °C or below | Crystallisation, clog |
| Gas 14 m³/s with liquid 0.01 m³/s | Too little irrigation, deposits, clog |
| Pump level 1 with liquid 0.1 m³/s | The pump can't keep up, level drop |
| Pump level 1 with plant gas from 400,000 m³/h | The pump is too weak, level drop |

If the column bursts, its casing breaks into debris, with fire and smoke. The camera doesn't shake, because that causes motion sickness in a headset. The impact comes through sound and controller vibration instead.

## Training and exam

Both modes reset the plant and remove the PPE. The plant starts from deliberately wrong settings (pump 1 and a low liquid flow against a high gas flow), which the trainee has to correct before starting it.

**Training:** 23 steps covering the safety procedure, setup, start-up and all five faults. The current task is shown in a bar above the tablet on every tab. Each step checks the **state of the plant**, not which button was pressed, so the trainee can use the tablet, the breaker or the valve. Faults caused by wrong settings are turned off in training, so they can't contradict the step text.

**Exam:** the tablet shows only the goal. After 20–60 seconds of normal operation, a random fault starts without warning. Scoring starts at 100:

| Mistake | Penalty |
|---|---|
| A wrong setting caused a fault | −20 |
| Reaction slower than 30 s | −10 |
| Reaction slower than 45 s | −25 |
| Started the plant without PPE | −15 |
| Entered the danger zone without PPE | −15 |
| Wrong action on the tablet (fixing a fault that isn't there, servicing a running plant, servicing without the lock or without PPE) | −5 each |

Reaction time is counted from **the start of the fault, not from the alarm**. The alarm sounds about 30 seconds in, so a trainee who reads the instruments can react before it. A fault not fixed within 90 seconds, or an explosion, fails the exam. The pass mark is 70.

At the end, the tablet shows the score, the time to reach normal operation, the reaction and fix time for each fault, and every penalty.

> The thresholds, cleaning coefficients and penalties are training values, chosen for teaching. They are marked as such in the code and should be checked against the plant's operating procedure before real use.

## Tablet

| Tab | What it does |
|---|---|
| Control | Start/stop, pump power 1–4, liquid type, transparent casing to see inside the column |
| Calculation | Plant gas, gas flow, liquid flow and temperature as inputs. Outputs: gas and liquid velocity and mass flow, mass transfer coefficient, liquid consumption, vessel diameter and height, consumables cost in KZT |
| Faults | Practice faults (a chosen one or a random one within a minute), live readings, repair actions, settings rules on/off, plant restore |
| Training | Mode and language selection, the result of the last run |

Changing the plant gas also resizes the emulsifier model, and the connecting pipes rebuild to reach its new nozzles.

## Tech stack

| | |
|---|---|
| Unity | **6000.6.5f1** (Unity 6), Universal Render Pipeline 17.6 |
| XR | XR Interaction Toolkit 3.6, OpenXR 1.18, XR Hands 1.9, Meta OpenXR, Android XR OpenXR, Composition Layers |
| Other packages | Localization (string tables kk/ru/en), Alembic and Timeline (fluid animation), Input System |
| Platforms | Meta Quest (Android, standalone) and PC VR through OpenXR (for example Quest Link). Hand tracking is enabled on both |

The manifest also includes [MCP for Unity](https://github.com/CoplayDev/unity-mcp), which is used for editor automation during development only. The app doesn't need it.

## Getting started

1. Install [Git LFS](https://git-lfs.com) **before** cloning. Models, textures, audio and the Blender file are stored in LFS.
   ```bash
   git lfs install
   git clone https://github.com/MerlanOurBoss/Air-Cleaning-VR.git
   ```
2. Open the project in Unity **6000.6.5f1**. Building for Quest requires the **Android Build Support** module.
3. Open `Assets/Scenes/EmulsifierScene.unity` and press **Play**.

**Without a headset**, the XR Interaction Simulator starts automatically in Play Mode, so you can test with keyboard and mouse. With a headset connected over Quest Link, the simulator stays off. XRI's built-in simulator autostart is disabled because it interferes with Link.

**Building for Quest:** in **File → Build Profiles**, choose **Android**, connect the headset in developer mode and click **Build And Run**. The minimum Android API level is 34.

## Project structure

```
Assets/
├── Scenes/EmulsifierScene.unity    Main scene: workshop, emulsifier, PPE stand, control post
├── Scripts/                        VR layer
│   ├── ControllerHand, HandGloves      Hands instead of controller models; gloves as a mesh swap
│   ├── PpeItem                         PPE you grab and bring to your head or face
│   ├── InspectableNode, NodeLabelCard  Hover frame, hint and description card for plant nodes
│   ├── RoomscaleBody                   Room-scale walking that collides with equipment and climbs stairs
│   ├── PipeConnector                   Rebuilds pipes when the emulsifier is resized
│   ├── EmulsifierAudio                 Fan, pump and water sounds that follow the settings
│   ├── L10n                            Strings built in code, read from the localization table
│   └── XRSimulatorAutoLoader           Starts the XR simulator only when no headset is connected
├── AirCleaningMachine/             Emulsifier imported from the desktop digital twin
│   └── Scripts/Facilities/Emulsifier/
│       ├── EmulsifierScenario          Training and exam: steps, faults, scoring, report
│       ├── EmulsifierFault             Faults, pressure and level, alarm, explosion trigger
│       ├── EmulsifierFaultRules        Faults caused by wrong settings
│       ├── EmulsifierSafety            PPE, danger zone, breaker lock
│       ├── EmulsifierGasCleaning       Cleaning efficiency and smoke colour
│       ├── EmulsifierCalculation       Emulsifier formulas from the digital twin
│       ├── EmulsifierManualControls    Breaker and valve at the control post
│       └── Emulsifier*Panel, Tablet    Tablet pages
├── Models/                         Emulsifier, workshop and PPE models
├── Localization/                   String tables: Kazakh, Russian, English
├── Audio/                          Workshop ambience, equipment, siren, explosion
└── XR/, XRI/, VRTemplateAssets/    XR settings and Unity VR template assets
Blender/                            Workshop source file and renders
```

## Design notes

- **Check the state, not the button.** Training steps don't care which control the trainee used, only whether the plant ended up in the right state. That leaves room to act naturally instead of hunting for a highlighted button.
- **Read the instruments.** In the exam, nothing tells the trainee that a fault has started until the alarm goes off. Reaction time counts from the fault itself, so noticing it early on the gauges is rewarded.
- **A false alarm on purpose.** The faulty gauge shows rising pressure while the siren and the sensor disagree. It teaches the trainee to cross-check instruments before acting.
- **Forgiving input.** A wrong setting has to be held for 12 seconds on a running plant before it causes a fault, so an accidental tap doesn't punish the trainee.
- **One clear rule for emergencies.** Pressure rises only while the plant runs, so stopping it is always safe. The trainee learns the rule in training and is scored on it in the exam.
- **Comfort first.** No camera shake, smoothed camera height on stairs, and room-scale walking that doesn't let your head pass through walls.
- **Tunable numbers.** Every threshold and penalty is a serialized field with a tooltip, so a methodologist can tune them in the Inspector without touching code.

## Limitations

- The coefficients and thresholds are training values, not a validated process model (see the note above).
- The exam result is shown on the tablet only and is not saved.
- Only the emulsifier is in VR so far. The other units of the digital twin (electrofilter, catalyst, CO₂ capture) exist in [AirCleaningMachine](https://github.com/MerlanOurBoss/AirCleaningMachine).

## Author

[MerlanOurBoss](https://github.com/MerlanOurBoss): sole developer, part of the digital twin project at the D.V. Sokolsky Institute of Fuel, Catalysis and Electrochemistry.
