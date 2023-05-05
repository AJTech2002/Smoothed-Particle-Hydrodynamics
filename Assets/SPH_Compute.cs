using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections.Generic;
[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 44)]
public struct Particle
{
    public float pressure; // 4
    public float density; // 8
    public Vector3 currentForce; // 20
    public Vector3 velocity; // 32
    public Vector3 position; // 44

    public Color color;
}

public class SPH_Compute : MonoBehaviour
{

  
    [Header("General")]
    public bool showSpheres = false;
    // public int numToSpawn = 400;
    public Vector3Int numToSpawn;
    public Vector3Int boxSize;
    public Vector3 spawnBoxCenter;
    public Vector3 spawnBox;
    public float particleRadius;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    public float cellSize;

    [Header("Fluid Constants")]
    public float boundDamping = -0.5f;
    public float viscosity = 200f;
    public float particleMass = 2.5f;
    public float gasConstant = 2000.0f; // Includes temp
    public float restingDensity = 300.0f; // Water

    [Header("Hashing")]
    public int maximumParticlesPerCell;
    public int[] _neighbourTracker;
    private int[] _neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
    private uint[] _hashGrid;
    public uint[] _hashGridTracker;


    [Header("Time")]
    public float timeScale;

    [Header("Compute")]
    public ComputeShader shader;
    public Particle[] particles;

    private ComputeBuffer _argsBuffer;
    public ComputeBuffer _particlesBuffer;

    private ComputeBuffer _neighbourListBuffer;
    private ComputeBuffer _neighbourTrackerBuffer;
    private ComputeBuffer _hashGridBuffer;
    private ComputeBuffer _hashGridTrackerBuffer;

    [Header("Particle Rendering")]
    public Mesh particleMesh;
    public float particleRenderSize = 40f;
    public Material material;

    private int num = 0;

    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

    private void Awake()
    {
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

    private int clearHashGridKernel;
    private int recalculateHashGridKernel;
    private int buildNeighbourListKernel;

    private void InitializeComputeBuffers()
    {
        _particlesBuffer = new ComputeBuffer(num, 44+(16));
        _particlesBuffer.SetData(particles);

        densityPressureKernel = shader.FindKernel("ComputeDensityPressure");
        computeForceKernel = shader.FindKernel("ComputeForces");
        integrateKernel = shader.FindKernel("Integrate");
        clearHashGridKernel = shader.FindKernel("ClearHashGrid");
        recalculateHashGridKernel = shader.FindKernel("RecalculateHashGrid");
        buildNeighbourListKernel = shader.FindKernel("BuildNeighbourList");

        shader.SetInt("particleLength", num);
        shader.SetFloat("particleMass", particleMass);
        shader.SetFloat("viscosity", viscosity);
        shader.SetFloat("gasConstant", gasConstant);
        shader.SetFloat("restDensity", restingDensity);
        shader.SetFloat("boundDamping", boundDamping);

        shader.SetFloat("CellSize", cellSize); // Setting cell-size h to double particle diameter.
        shader.SetInt("maximumParticlesPerCell", maximumParticlesPerCell);

        shader.SetFloat("radius", particleRadius);
        shader.SetFloat("radius2", particleRadius * particleRadius);
        shader.SetFloat("radius3", particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius4", particleRadius * particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius5", particleRadius * particleRadius * particleRadius * particleRadius * particleRadius);

        shader.SetFloat("pi", Mathf.PI);
        shader.SetFloat("densityWeightConstant", 0.00497359197162172924277761760539f);
        shader.SetFloat("spikyGradient", -0.09947183943243458485555235210782f);
        shader.SetFloat("viscLaplacian", 0.39788735772973833942220940843129f);


        shader.SetVector("boxSize", new Vector3(boxSize.x, boxSize.y, boxSize.z));

        shader.SetBuffer(densityPressureKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(computeForceKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(integrateKernel, "_particles", _particlesBuffer);


        _neighbourList = new int[num * maximumParticlesPerCell * 8];
        _neighbourTracker = new int[num];
        _hashGrid = new uint[boxSize.x * boxSize.y * boxSize.z * maximumParticlesPerCell];
        _hashGridTracker = new uint[boxSize.x * boxSize.y * boxSize.z];

        _neighbourListBuffer = new ComputeBuffer(num * maximumParticlesPerCell * 8, sizeof(int)); 
        _neighbourListBuffer.SetData(_neighbourList);
        _neighbourTrackerBuffer = new ComputeBuffer(num, sizeof(int));
        _neighbourTrackerBuffer.SetData(_neighbourTracker);
        
        _hashGridBuffer = new ComputeBuffer(boxSize.x * boxSize.y * boxSize.z * maximumParticlesPerCell, sizeof(uint));
        _hashGridBuffer.SetData(_hashGrid);
        _hashGridTrackerBuffer = new ComputeBuffer(boxSize.x * boxSize.y * boxSize.z, sizeof(uint));
        _hashGridTrackerBuffer.SetData(_hashGridTracker);

        shader.SetBuffer(clearHashGridKernel, "_hashGridTracker", _hashGridTrackerBuffer);

        shader.SetBuffer(recalculateHashGridKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(recalculateHashGridKernel, "_hashGrid", _hashGridBuffer);
        shader.SetBuffer(recalculateHashGridKernel, "_hashGridTracker", _hashGridTrackerBuffer);
    
        shader.SetBuffer(buildNeighbourListKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(buildNeighbourListKernel, "_hashGrid", _hashGridBuffer);
        shader.SetBuffer(buildNeighbourListKernel, "_hashGridTracker", _hashGridTrackerBuffer);
        shader.SetBuffer(buildNeighbourListKernel, "_neighbourList", _neighbourListBuffer);
        shader.SetBuffer(buildNeighbourListKernel, "_neighbourTracker", _neighbourTrackerBuffer);

        shader.SetBuffer(computeForceKernel, "_neighbourList", _neighbourListBuffer);
        shader.SetBuffer(computeForceKernel, "_neighbourTracker", _neighbourTrackerBuffer);

        shader.SetBuffer(densityPressureKernel, "_neighbourList", _neighbourListBuffer);
        shader.SetBuffer(densityPressureKernel, "_neighbourTracker", _neighbourTrackerBuffer);
    }


    private void SpawnParticlesInBox()
    {
        Vector3 spawnTopLeft = spawnBoxCenter - spawnBox / 2;
        int xIterations = Mathf.RoundToInt(spawnBox.x / (particleRadius * 2));
        int yIterations = Mathf.RoundToInt(spawnBox.y / (particleRadius * 2));
        int zIterations = Mathf.RoundToInt(spawnBox.z / (particleRadius * 2));

        // num = xIterations * yIterations * zIterations;

        List<Particle> _particles = new List<Particle>();

        // for (int x = 1; x < xIterations; x++) {
        //     for (int y = 1; y < yIterations; y++) {
        //         for (int z = 1; z < zIterations; z++) {

        //             Vector3 spawnPosition = spawnTopLeft + new Vector3(x * particleRadius * 2, y * particleRadius * 2, z * particleRadius * 2);

        //             Particle p = new Particle
        //             {
        //                 position = spawnPosition
        //             };

        //             _particles.Add(p);
        //         }
        //     }
        // }

        for (int x = 0; x < numToSpawn.x; x++)
        {
            for (int y = 0; y < numToSpawn.y; y++)
            {
                for (int z = 0; z < numToSpawn.z; z++)
                {
                    Vector3 spawnPosition = spawnTopLeft + new Vector3(x * particleRadius * 2, y * particleRadius * 2, z * particleRadius * 2) + Random.onUnitSphere * particleRadius * 0.1f;
                    Particle p = new Particle
                    {
                        position = spawnPosition,
                        color = new Color(0,0,0,255)
                    };

                    _particles.Add(p);
                }
            }
        }

        num = _particles.Count;

        particles = _particles.ToArray();

    }

    public float timestep = 0.07f;

    private void FixedUpdate()
    {

        shader.SetVector("boxSize", new Vector3(boxSize.x, boxSize.y, boxSize.z));
        shader.SetFloat("timestep", timestep);

        shader.Dispatch(clearHashGridKernel, boxSize.x * boxSize.y * boxSize.z , 1, 1);
        shader.Dispatch(recalculateHashGridKernel, num / 100, 1, 1);
        shader.Dispatch(buildNeighbourListKernel, num / 100, 1, 1);

        shader.Dispatch(densityPressureKernel, num / 100, 1, 1);
        shader.Dispatch(computeForceKernel, num / 100, 1, 1);
        shader.Dispatch(integrateKernel, num / 100, 1, 1);

        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty, _particlesBuffer);

        _neighbourTrackerBuffer.GetData(_neighbourTracker);
       _hashGridTrackerBuffer.GetData(_hashGridTracker);
    }

    private void Update() {
        if (showSpheres) Graphics.DrawMeshInstancedIndirect(particleMesh, 0, material, new Bounds(Vector3.zero, boxSize), _argsBuffer, castShadows: UnityEngine.Rendering.ShadowCastingMode.Off);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);

        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(spawnBoxCenter, spawnBox);
        }
    }

}
