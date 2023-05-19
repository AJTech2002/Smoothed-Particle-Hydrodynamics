using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size=44)]
public struct Particle {
    public float pressure; // 4
    public float density; // 8
    public Vector3 currentForce; // 20
    public Vector3 velocity; // 32
    public Vector3 position; // 44 total bytes
}

public class SPH : MonoBehaviour
{
    [Header("General")]
    public Transform collisionSphere;
    public bool showSpheres = true;
    public Vector3Int numToSpawn = new Vector3Int(10,10,10);
    private int totalParticles {
        get {
            return numToSpawn.x*numToSpawn.y*numToSpawn.z;
        }
    }
    public Vector3 boxSize = new Vector3(4,10,3);
    public Vector3 spawnCenter;
    public float particleRadius = 0.1f;
    public float spawnJitter = 0.2f;

    [Header("Particle Rendering")]
    public Mesh particleMesh;
    public float particleRenderSize = 8f;
    public Material material;
    
    [Header("Compute")]
    public ComputeShader shader;
    public Particle[] particles;

    [Header("Fluid Constants")]
    public float boundDamping = -0.3f;
    public float viscosity = -0.003f;
    public float particleMass = 1f;
    public float gasConstant = 2f;
    public float restingDensity = 1f;
    public float timestep = 0.007f;

    // Private Variables
    private ComputeBuffer _argsBuffer;
    public ComputeBuffer _particlesBuffer;
    private int integrateKernel;
    private int computeForceKernel;
    private int densityPressureKernel;

    private void OnDrawGizmos() {

        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);

        if (!Application.isPlaying) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnCenter, 0.1f);
        }

    }

    private void Awake() {

        SpawnParticlesInBox(); // Spawn Particles

        // Setup Args for Instanced Particle Rendering
        uint[] args = {
            particleMesh.GetIndexCount(0),
            (uint)totalParticles,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };

        _argsBuffer = new ComputeBuffer(1,args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        // Setup Particle Buffer
        _particlesBuffer = new ComputeBuffer(totalParticles,44);
        _particlesBuffer.SetData(particles);

        SetupComputeBuffers();

    }

    private void SetupComputeBuffers() {

        integrateKernel = shader.FindKernel("Integrate");
        computeForceKernel = shader.FindKernel("ComputeForces");
        densityPressureKernel = shader.FindKernel("ComputeDensityPressure");

        shader.SetInt("particleLength", totalParticles);
        shader.SetFloat("particleMass", particleMass);
        shader.SetFloat("viscosity", viscosity);
        shader.SetFloat("gasConstant", gasConstant);
        shader.SetFloat("restDensity", restingDensity);
        shader.SetFloat("boundDamping", boundDamping);
        shader.SetFloat("pi", Mathf.PI);
        shader.SetVector("boxSize", boxSize);

        shader.SetFloat("radius", particleRadius);
        shader.SetFloat("radius2", particleRadius * particleRadius);
        shader.SetFloat("radius3", particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius4", particleRadius * particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius5", particleRadius * particleRadius * particleRadius * particleRadius * particleRadius);

        shader.SetBuffer(integrateKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(computeForceKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(densityPressureKernel, "_particles", _particlesBuffer);

    }


    private void FixedUpdate() {

        shader.SetVector("boxSize", boxSize);
        shader.SetFloat("timestep", timestep);
        shader.SetVector("spherePos", collisionSphere.transform.position);
        shader.SetFloat("sphereRadius", collisionSphere.transform.localScale.x/2);

        // Total Particles has to be divisible by 100 
        shader.Dispatch(densityPressureKernel, totalParticles / 100, 1, 1); // 1. Compute Density/Pressure for each particle
        shader.Dispatch(computeForceKernel, totalParticles / 100, 1, 1); // 2. Use Density/Pressure to calculate forces
        shader.Dispatch(integrateKernel, totalParticles / 100, 1, 1); // 3. Use forces to move particles
    }


    private void SpawnParticlesInBox() {

        Vector3 spawnPoint = spawnCenter;
        List<Particle> _particles = new List<Particle>();

        for (int x = 0; x < numToSpawn.x; x++) {
            for (int y = 0; y < numToSpawn.y; y++) {
                for (int z = 0; z < numToSpawn.z; z++) {

                    Vector3 spawnPos = spawnPoint + new Vector3(x*particleRadius*2, y*particleRadius*2, z*particleRadius*2);

                    // Randomize spawning position a little bit for more convincing simulation
                    spawnPos += Random.onUnitSphere * particleRadius * spawnJitter; 

                    Particle p = new Particle {
                        position = spawnPos
                    };

                    _particles.Add(p);
                }
            }
        }

        particles = _particles.ToArray();

    }


    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

    private void Update() {

        // Render the particles
        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty, _particlesBuffer);

        if (showSpheres) 
            Graphics.DrawMeshInstancedIndirect (
                particleMesh,
                0,
                material,
                new Bounds(Vector3.zero, boxSize),
                _argsBuffer,
                castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
            );


    }



}
