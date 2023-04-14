using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections.Generic;

public class SPH_Compute : MonoBehaviour
{
    
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential, Size=44)]
    public struct Particle {
        public float pressure; // 4
        public float density; // 8
        public Vector3 currentForce; // 20
        public Vector3 velocity; // 32
        public Vector3 position; // 44
    }


    [Header("General")]
    public int numToSpawn = 400;
    public Vector3 boxSize;
    public Vector3 spawnBoxCenter;
    public Vector3 spawnBox;
    public float particleRadius;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    [Header("Fluid Constants")]
    public float boundDamping = -0.5f;
    public float viscosity = 200f;
    public float particleMass = 2.5f;
    public float gasConstant = 2000.0f; // Includes temp
    public float restingDensity = 300.0f; // Water


    [Header("Time")]
    public float timeScale;

    [Header("Compute")]
    public ComputeShader shader;
    private Particle[] particles;
    
    private ComputeBuffer _argsBuffer;
    private ComputeBuffer _particlesBuffer;

    [Header("Particle Rendering")]
    public Mesh particleMesh;
    public float particleRenderSize = 40f;
    public Material material;

    private int num = 0;

    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

    private void Awake() {
        // Spawn Particles
        SpawnParticlesInBox();

        uint[] args = {
            particleMesh.GetIndexCount(0),
            (uint) num,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };
        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        InitializeComputeBuffers();
    }
    
    private int densityPressureKernel;
    private int computeForceKernel;
    
    private int integrateKernel;
    
    private void InitializeComputeBuffers() {
        _particlesBuffer = new ComputeBuffer(num, 44);
        _particlesBuffer.SetData(particles);
        
        densityPressureKernel = shader.FindKernel("ComputeDensityPressure");
        computeForceKernel = shader.FindKernel("ComputeForces");
        integrateKernel = shader.FindKernel("Integrate");
        
        shader.SetInt("particleLength", num);
        
        shader.SetBuffer(densityPressureKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(computeForceKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(integrateKernel, "_particles", _particlesBuffer);
        
    }


    private void SpawnParticlesInBox() {
        Vector3 spawnTopLeft = spawnBoxCenter - spawnBox / 2;
        int xIterations = Mathf.RoundToInt(spawnBox.x / (particleRadius * 2));
        int yIterations = Mathf.RoundToInt(spawnBox.y / (particleRadius * 2));
        int zIterations = Mathf.RoundToInt(spawnBox.z / (particleRadius * 2));

        // num = xIterations * yIterations * zIterations;

        List<Particle> _particles = new List<Particle>();

        // for (int x = 1; x < xIterations; x++) {
        //     for (int y = 1; y < yIterations; y++) {
        //         for (int z = 1; z < zIterations; z++) {

        //             Vector3 spawnPosition = spawnTopLeft + new Vector3(x * particleRadius * 2, y * particleRadius * 2, z * particleRadius * 2) + Random.onUnitSphere * particleRadius * 0.5f;

        //             Particle p = new Particle
        //             {
        //                 position = spawnPosition
        //             };

        //             _particles.Add(p);
        //         }
        //     }
        // }
        
        for (int i = 0; i < numToSpawn; i++) {
            Vector3 spawnPos = spawnBoxCenter + Random.onUnitSphere * spawnBox.x;
             Particle p = new Particle
            {
                position = spawnPos
            };

            _particles.Add(p);
        }
        
        num = numToSpawn;

        particles = _particles.ToArray();

    }

    private void Update() {
        
        shader.Dispatch(densityPressureKernel, num/100, 1, 1);

        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty, _particlesBuffer);

        Graphics.DrawMeshInstancedIndirect(particleMesh, 0, material, new Bounds(Vector3.zero, boxSize), _argsBuffer, castShadows: UnityEngine.Rendering.ShadowCastingMode.Off);
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);

        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(spawnBoxCenter, spawnBox);
        }
    }

}
