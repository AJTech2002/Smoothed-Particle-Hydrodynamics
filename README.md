# SPH-Unity-Implementation

Very basic implementation of realtime fluid simulation using the SPH algorithm.

## File Breakdown

- `Raymarching.compute` : A Compute Shader used for the Ray-marching (simplified version from Sebastian Lague) 
- `SPHComputeShader.compute` : A Compute Shader running SPH w/ various kernels that operate on different segments of SPH
- `SPH.cs` - Controls the SPH Compute Shader
- `FluidRayMarching.cs` - Controls the Ray Marching Compute Shader

## WIP

- Optimization of SPH to hash neighbours so that SPH is not n^2 
- Adding refraction & transparency to visuals
- Adding physical interaction between a rigidbody & water

# Version 1

Contains basic ray marching & very un-optimized SPH. 

![sph-demo](https://user-images.githubusercontent.com/25098044/233352440-c5178813-5c8e-4aff-b07a-3a9e3f14c682.gif)

