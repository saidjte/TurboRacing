# Third-Party Notices — TurboRacing

## JRS Vehicle Physics Controller (partial, MIT)

- Source: https://github.com/Jermesa-Studio/JRS_Vehicle_Physics_Controller
- License: MIT License, Copyright (c) 2024 Samborlang Pyrtuh
- Used in: `Assets/Scripts/MotionCoreVehicleController.cs` — the gearbox model
  (gear selection from speed bands, torque ratio per gear, clutch-cut dip on
  shift, hysteresis to avoid hunting at band borders) is derived from this
  project's `JrsVehicleController.cs` gear-ratio logic, re-implemented on our
  licensed MotionCore `VehicleControllerBase` engine.
- The full MIT license text:

```
MIT License

Copyright (c) 2024 Samborlang Pyrtuh

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Vehicle Physics Pro — documentation reference (no code)

- https://vehiclephysics.com/about/licensing — Community Edition is
  desktop-builds-only; NOT embedded in this Android project. Only publicly
  documented engineering concepts (traction = Fz x mu, AdvancedDamper-style
  pitch damping) informed our own implementation (v2.8.0).

## Asset Store packages (owner's account, Standard EULA — private use)

- MotionCore Vehicle Physics (licensed engine base)
- Cartoon Race Track Oval modules
- Racing Cars Pack 1, 2CV6 engine sounds
- AK Road Editor (312766), Environment Track Lowpoly: Cartoon Props (211494),
  Racing Starter Kit FREE (169750)
- ARCADE: FREE Racing Car (161085), PROMETEO: Car Controller (209444)
