# Torus Physics Simulation & Software Rasterizer

A physics simulation of a torus on a flat plane, visualised through a custom-built, purely software-based raster rendering engine. 

This application is built with **C# and WPF**. All rendering logic is implemented completely from scratch using direct bitmap writing, without relying on any graphics APIs. The only external dependency is **GlmSharp**, which is used for vector algebra.

### Performance
Because this is entirely software-rendered, it requires a modern CPU to run smoothly.  Use only the **Release build** and run **without a debugger** for the best performance! If it is still lagging, try lowering the resolution, disabling lighting, or slowing down the simulation. Zooming out can also improve performance by reducing the number of pixels rasterised.

### Renderer features
* render polygons of triangles
* render lines
* load obj models
* ambient and diffuse lighting
* multiple light sources
* apply different linear transformations
* depth-aware rendering



 ### Physics Simulation
* Runge-Kutta Integration is used to solve motion equations.
