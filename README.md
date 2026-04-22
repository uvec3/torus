# Torus Physics Simulation & Software Rasterizer

A physics simulation of a torus on a flat plane, visualised through a custom-built, purely software-based raster rendering engine. 

This application is built with **C# and WPF**. All rendering logic is implemented completely from scratch using direct bitmap writing, without relying on any graphics APIs. The only external dependency is **GlmSharp**, which is used for vector algebra.

### Performance
Because this is entirely software-rendered, it requires a relatively modern CPU to run smoothly.  Use only the **Release build** and run **without a debugger** for the best performance! If it is still lagging, try lowering the resolution, disabling lighting, or slowing down the simulation. Zooming out can also improve performance by reducing the number of pixels rasterised.

 ## Physics Simulation
The following forces are taken into account:

https://github.com/user-attachments/assets/5a0b5d97-8204-46e8-b805-df01f71d8ba2



### 1) Gravity

- applied to the centre of the torus downward
- Magnitude: mg

### 2) Surface normal force

Forse applied only when the torus contacts the ground, accounting for surface reaction and rolling friction simultaneously.
Forse applied to the contact point with a small offset in the direction of rolling along the plane to create rolling friction torque.

Magnitude:
To simulate realistic behaviour, the force is composed of two parts:
- **spring** proportional to penetration depth: `k * penetration` directed upwards
- **damping** proportional to vertical contact-point velocity and directed in the opposite direction to the vertical velocity  
Damping force is necessary to account for energy loss during the collision, otherwise the model will bounce back with the same kinetic energy as before the collision and never stop.

### 3) Sliding friction force

- Applied when the torus contacts the plane and the contact point has non-zero speed in projection on the surface.
- Direction: opposite contact-point motion in the ground plane
- Magnitude: Proportional to the normal force

### 4) Spin friction torque
Applied when the torus touches the surface
- Opposes spin direction around the vertical axis
- Magnitude: Proportional to the normal force  
This causes realistic spin decay.


### Tweakable Physical Parameters
These parameters can be adjusted to achieve different effects
| Parameter | Meaning  | 
|---|---|
| `m`| Torus mass 
| `OuterRadius` | Outer geometric radius 
| `InnerRadius` | Inner geometric radius  
| `delta` | Contact patch deformation radius used for rolling-resistance torque 
| `k` | Contact stiffness with ground (spring constant) 
| `mu` | Surface friction coefficient 
| `g` | Gravity acceleration magnitude 
| `absorption` | Collision energy absorption (used to compute damping ratio), range: 0-1


It is possible to model a situation in which no energy escapes by setting mu, delta, and absorption to 0.



https://github.com/user-attachments/assets/8d0bec0b-6982-43e4-9743-f04b5dcf3579

Another case is when delta and absorption are 0, but mu is not; in this case, energy loss is very small after the initial stage, but the model can still use the friction force to move.

https://github.com/user-attachments/assets/4f3cdde9-8ff8-433b-a407-50957bcac422


## Integaration 
* Runge-Kutta Integration is used to solve motion equations.
* The simulation state is integrated with **4th-order Runge-Kutta (RK4)** on a small fixed max step (`0.0001 s`) for stability.
