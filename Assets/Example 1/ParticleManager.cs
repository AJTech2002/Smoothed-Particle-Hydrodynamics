using System.Collections.Generic;
using System.Runtime.InteropServices;
using DefaultNamespace;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class ParticleManager : MonoBehaviour
{
    // ReSharper disable InconsistentNaming
    [Header("Particle properties")]
    public float radius = 1f; // particle radius, interaction radius h
    public Mesh particleMesh;
    public float particleRenderSize = 40f;
    public Material material;
    public float mass = 4f;
    public float viscosityCoefficient = 2.5f;
    private static readonly Vector3 g = new Vector3(0.0f, -9.81f * 2000f, 0.0f);
    private const float gasConstant = 2000.0f;
    private const float dt = 0.0008f;
    [SerializeField]
    private float restDensity = 1f;
    [SerializeField]
    private float damping = -0.5f;

    [Header("Simulation space properties")]
    public int numberOfParticles = 1000;
    public int dimensions = 10;
    public int maximumParticlesPerCell = 500;
    
    [Header("Debug information")]
    [Tooltip("Tracks how many neighbours each particleIndex has in " + nameof(_neighbourList))]
    [SerializeField]
    private int[] _neighbourTracker;
    [Tooltip("The absolute accumulated simulation steps")]
    public int elapsedSimulationSteps;

    private Particle[] _particles;
    // Too big for feasible serialisation (crash on expand).
    private int[] _neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
    private readonly Dictionary<int, List<int>> _hashGrid = new Dictionary<int, List<int>>();  // Hash of cell to particle indices.
    
    private ComputeBuffer _particleColorPositionBuffer;
    private ComputeBuffer _argsBuffer;
    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

    private float radius2;
    private float radius3;
    private float radius4;
    private float radius5;
    // ReSharper restore InconsistentNaming

    [StructLayout(LayoutKind.Sequential, Size=28)]
    private struct Particle
    {
        public Vector3 Position;
        public Vector4 Color;
    }

    private float[] densities;
    private float[] pressures;
    private Vector3[] forces;
    private Vector3[] velocities;

    private void Awake()
    {
        RespawnParticles();
        InitNeighbourHashing();
        InitComputeBuffers();
        radius2 = radius * radius;
        radius3 = radius2 * radius;
        radius4 = radius3 * radius;
        radius5 = radius4 * radius;
    }
    
    #region Initialisation

    private void RespawnParticles()
    {
        _particles = new Particle[numberOfParticles];
        densities = new float[numberOfParticles];
        pressures = new float[numberOfParticles];
        forces = new Vector3[numberOfParticles];
        velocities = new Vector3[numberOfParticles];

        int particlesPerDimension = Mathf.CeilToInt(Mathf.Pow(numberOfParticles, 1f / 3f));

        int counter = 0;
        while (counter < numberOfParticles)
        {
            for (int x = 0; x < particlesPerDimension; x++)
            for (int y = 0; y < particlesPerDimension; y++)
            for (int z = 0; z < particlesPerDimension; z++)
            {
                Vector3 startPos = new Vector3(dimensions - 1, dimensions - 1, dimensions - 1) - new Vector3(x / 2f, y / 2f, z / 2f)  - new Vector3(Random.Range(0f, 0.01f), Random.Range(0f, 0.01f), Random.Range(0f, 0.01f));
                _particles[counter] = new Particle
                {
                    Position = startPos,
                    Color = Color.white
                };
                densities[counter] = -1f;
                pressures[counter] = 0.0f;
                forces[counter] = Vector3.zero;
                velocities[counter] = Vector3.zero;

                if (++counter == numberOfParticles)
                {
                    return;
                }
            }
        }
    }

    private void InitNeighbourHashing()
    {
        _hashGrid.Clear();  // Only needed when resetting the simulation via testBattery approach.
        _neighbourList = new int[numberOfParticles * maximumParticlesPerCell * 8];   // 8 because we consider 8 cells
        _neighbourTracker = new int[numberOfParticles];
        SpatialHashing.CellSize = radius * 2; // Setting cell-size h to particle diameter.
        SpatialHashing.CellSize = radius * 2 * 2; // Setting cell-size h to double particle diameter.
        SpatialHashing.Dimensions = dimensions;
        for (int i = 0; i < dimensions; i++)
        for (int j = 0; j < dimensions; j++)
        for (int k = 0; k < dimensions; k++)
        {
            _hashGrid.Add(SpatialHashing.Hash(new Vector3Int(i, j, k)), new List<int>());
        }
    }

    void InitComputeBuffers()
    {
        uint[] args = {
            particleMesh.GetIndexCount(0),
            (uint) numberOfParticles,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };
        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);
        
        _particleColorPositionBuffer = new ComputeBuffer(numberOfParticles, sizeof(float) * ( 3 + 4 ));
        if (_particles != null)
        {
            _particleColorPositionBuffer.SetData(_particles);
        }
        else
        {
            Debug.Log("Particles are null during compute buffer initialisation. Are you initialising the ParticleManager in the direct order?");
        }
    }
    
    #endregion

    void Update()
    {
        // Calculate hash of all particles and build neighboring list.
        // 1. Clear HashGrid
        foreach (var cell in _hashGrid)
        {
            cell.Value.Clear();
        }
        // 2. Recalculate hashes of each particle.
        for (int i = 0; i < _particles.Length; i++)
        {
            var hash = SpatialHashing.Hash(SpatialHashing.GetCell(_particles[i].Position));
            if (_hashGrid[hash].Count == maximumParticlesPerCell) continue;   // Prevent potential UB in neighbourList if more than maxParticlesPerCell are in a cell.
            _hashGrid[hash].Add(i);
        }
        // 3. For each particle go through all their 8 neighbouring cells.
        //    Check each particle in those neighbouring cells for interference radius r and store the interfering ones inside the particles neighbour list.
        for (int particleIndex = 0; particleIndex < _particles.Length; particleIndex++)
        {
            _neighbourTracker[particleIndex] = 0;
            var cell = SpatialHashing.GetCell(_particles[particleIndex].Position);
            var cells = GetNearbyKeys(cell, _particles[particleIndex].Position);

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int j = 0; j < cells.Length; j++)
            {
                if (!_hashGrid.ContainsKey(cells[j])) continue;
                var neighbourCell = _hashGrid[cells[j]];
                foreach (var potentialNeighbour in neighbourCell)
                {
                    if (potentialNeighbour == particleIndex) continue;
                    // if (( _particles[potentialNeighbour].Position - _particles[particleIndex].Position ).magnitude < radius) // Using magnitude for debug purposes.
                    if (( _particles[potentialNeighbour].Position - _particles[particleIndex].Position ).sqrMagnitude < radius2) // Using squared length instead of magnitude for performance
                    {
                        _neighbourList[particleIndex * maximumParticlesPerCell * 8 + _neighbourTracker[particleIndex]++] = potentialNeighbour;
                    }
                }
            }
        }
        // 4. The Neighbouring-list should be n-particles big, each index containing a list of each particles neighbours in radius r.

        ComputeDensityPressure();
        ComputeForces();
        Integrate();
        elapsedSimulationSteps++;

        _particleColorPositionBuffer.SetData(_particles);
        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty, _particleColorPositionBuffer);
        Graphics.DrawMeshInstancedIndirect(particleMesh, 0, material, new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), _argsBuffer, castShadows: UnityEngine.Rendering.ShadowCastingMode.On);
    }

    // https://lucasschuermann.com/writing/implementing-sph-in-2d
    private void Integrate()
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            // forward Euler integration
            velocities[i] += dt * forces[i] / mass;
            _particles[i].Position += dt * velocities[i];
            
            // enforce boundary conditions
            if (_particles[i].Position.x - float.Epsilon < 0.0f)
            {
                velocities[i].x *= damping;
                _particles[i].Position.x = float.Epsilon;
            }
            else if(_particles[i].Position.x + float.Epsilon > dimensions - 1f) 
            {
                velocities[i].x *= damping;
                _particles[i].Position.x = dimensions - 1 - float.Epsilon;
            }
            
            if (_particles[i].Position.y - float.Epsilon < 0.0f)
            {
                velocities[i].y *= damping;
                _particles[i].Position.y = float.Epsilon;
            }
            else if(_particles[i].Position.y + float.Epsilon > dimensions - 1f) 
            {
                velocities[i].y *= damping;
                _particles[i].Position.y = dimensions - 1 - float.Epsilon;
            }
            
            if (_particles[i].Position.z - float.Epsilon < 0.0f)
            {
                velocities[i].z *= damping;
                _particles[i].Position.z = float.Epsilon;
            }
            else if(_particles[i].Position.z + float.Epsilon > dimensions - 1f) 
            {
                velocities[i].z *= damping;
                _particles[i].Position.z = dimensions - 1 - float.Epsilon;
            }
        }
    }

    private void ComputeForces()
    {
        float mass2 = mass * mass;
        for (int i = 0; i < _particles.Length; i++)
        {
            forces[i] = Vector3.zero;
            var particleDensity2 = densities[i] * densities[i];
            for (int j = 0; j < _neighbourTracker[i]; j++)
            {
                int neighbourIndex = _neighbourList[i * maximumParticlesPerCell * 8 + j];
                float distance = ( _particles[i].Position - _particles[neighbourIndex].Position ).magnitude;
                if (distance > 0.0f)
                {
                    var direction = ( _particles[i].Position - _particles[neighbourIndex].Position ) / distance;
                    // 7. Compute pressure gradient force (Doyub Kim page 136)
                    forces[i] -= mass2 * ( pressures[i] / particleDensity2 + pressures[neighbourIndex] / ( densities[neighbourIndex] * densities[neighbourIndex] ) ) * SpikyKernelGradient(distance, direction);   // Kim
                    // 8. Compute the viscosity force
                    forces[i] += viscosityCoefficient * mass2 * ( velocities[neighbourIndex] - velocities[i] ) / densities[neighbourIndex] * SpikyKernelSecondDerivative(distance);    // Kim
                }
            }
        
            // Gravity
            forces[i] += g;
        }
    }

    private void ComputeDensityPressure()
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            // Doyub Kim 121, 122, 123
            // 5. Compute densities
            Vector3 origin = _particles[i].Position;
            float sum = 0f;
            for (int j = 0; j < _neighbourTracker[i]; j++)
            {
                int neighbourIndex = _neighbourList[i * maximumParticlesPerCell * 8 + j];
                float distanceSquared = ( origin - _particles[neighbourIndex].Position ).sqrMagnitude;
                sum += StdKernel(distanceSquared);
            }

            densities[i] = sum * mass + 0.000001f;

            // 6. Compute pressure based on density
            pressures[i] = gasConstant * ( densities[i] - restDensity ); // as described in Müller et al Equation 12
        }
    }

    // Kernel by Müller et al.
    private float StdKernel(float distanceSquared)
    {
        // Doyub Kim
        float x = 1.0f - distanceSquared / radius2;
        return 315f / ( 64f * Mathf.PI * radius3 ) * x * x * x;
    }
    
    // Doyub Kim page 130
    private float SpikyKernelFirstDerivative(float distance)
    {
        float x = 1.0f - distance / radius;
        return -45.0f / ( Mathf.PI * radius4 ) * x * x;
    }

    // Doyub Kim page 130
    private float SpikyKernelSecondDerivative(float distance)
    {
        // Btw, it derives 'distance' not 'radius' (h)
        float x = 1.0f - distance / radius;
        return 90f / ( Mathf.PI * radius5 ) * x;
    }
    
    // Doyub Kim page 130
    private Vector3 SpikyKernelGradient(float distance, Vector3 directionFromCenter)
    {
        return SpikyKernelFirstDerivative(distance) * directionFromCenter;
    }

    // Derived from Doyub Kim
    private int[] GetNearbyKeys(Vector3Int originIndex, Vector3 position)
    {
        Vector3Int[] nearbyBucketIndices = new Vector3Int[8];
        for (int i = 0; i < 8; i++)
        {
            nearbyBucketIndices[i] = originIndex;
        }

        if (( originIndex.x + 0.5f ) * SpatialHashing.CellSize <= position.x)
        {
            nearbyBucketIndices[4].x += 1;
            nearbyBucketIndices[5].x += 1;
            nearbyBucketIndices[6].x += 1;
            nearbyBucketIndices[7].x += 1;
        }
        else
        {
            nearbyBucketIndices[4].x -= 1;
            nearbyBucketIndices[5].x -= 1;
            nearbyBucketIndices[6].x -= 1;
            nearbyBucketIndices[7].x -= 1;
        }

        if (( originIndex.y + 0.5f ) * SpatialHashing.CellSize <= position.y)
        {
            nearbyBucketIndices[2].y += 1;
            nearbyBucketIndices[3].y += 1;
            nearbyBucketIndices[6].y += 1;
            nearbyBucketIndices[7].y += 1;
        }
        else
        {
            nearbyBucketIndices[2].y -= 1;
            nearbyBucketIndices[3].y -= 1;
            nearbyBucketIndices[6].y -= 1;
            nearbyBucketIndices[7].y -= 1;
        }

        if (( originIndex.z + 0.5f ) * SpatialHashing.CellSize <= position.z)
        {
            nearbyBucketIndices[1].z += 1;
            nearbyBucketIndices[3].z += 1;
            nearbyBucketIndices[5].z += 1;
            nearbyBucketIndices[7].z += 1;
        }
        else
        {
            nearbyBucketIndices[1].z -= 1;
            nearbyBucketIndices[3].z -= 1;
            nearbyBucketIndices[5].z -= 1;
            nearbyBucketIndices[7].z -= 1;
        }

        int[] nearbyKeys = new int[8];
        for (int i = 0; i < 8; i++)
        {
            nearbyKeys[i] = SpatialHashing.Hash(nearbyBucketIndices[i]);
        }

        return nearbyKeys;
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void ReleaseBuffers()
    {
        _particleColorPositionBuffer.Dispose();
        _argsBuffer.Dispose();
    }
}
