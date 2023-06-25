# Smoothed Particle Hydrodynamics Unity

Very basic implementation of realtime fluid simulation using the SPH algorithm.

## File Breakdown

- `Raymarching.compute` : A Compute Shader used for the Ray-marching (simplified version from Sebastian Lague) 
- `SPHComputeShader.compute` : A Compute Shader running SPH w/ various kernels that operate on different segments of SPH
- `SPH.cs` - Controls the SPH Compute Shader
- `FluidRayMarching.cs` - Controls the Ray Marching Compute Shader

## YouTube Explanation
https://youtu.be/zbBwKMRyavE

## WIP

- Adding refraction & transparency to visuals
- Adding physical interaction between a rigidbody & water

# Version 3 

Simple Lego Renderer for the Fluid Simulation based on this video https://youtu.be/LrEHoaq6QFE (`feat/lego-renderer`)

![fluid-2](https://github.com/AJTech2002/Smoothed-Particle-Hydrodynamics/assets/25098044/001bbbcb-ade1-45ce-a704-4a02c1bbb2fc)

# Version 2

Optimized SPH with 32,000+ particles using Dynamic Hashed Grid (`feat/optimization`)

![fluid](https://github.com/AJTech2002/Smoothed-Particle-Hydrodynamics/assets/25098044/f1eab369-fabf-4d15-832b-f8700c53171d)

# Version 1

Contains basic ray marching & very un-optimized SPH. 

![sph-demo](https://user-images.githubusercontent.com/25098044/233352440-c5178813-5c8e-4aff-b07a-3a9e3f14c682.gif)

