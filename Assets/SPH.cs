using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SPH : MonoBehaviour
{
    
    [System.Serializable]
    private class Particle {
        public float pressure;
        public float density;

        public Vector3 currentForce;

        public Vector3 velocity;
        public Vector3 position;

        public bool onBoundary = false;
    }

    [Header("Display")]
    public bool wireframeSpheres = false;

    [Header("General")]
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

    private float time = 0f;

    
    private List<Particle> particles = new List<Particle>();

    private void OnDrawGizmos() {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);

        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(spawnBoxCenter, spawnBox);
        }
        else {
            // Show Particles
            foreach (Particle p in particles) {

                // if (!p.onBoundary)
                // Gizmos.color = Color.white;
                // else
                // Gizmos.color = Color.red;

                Gizmos.color = Color.cyan;

                if (!wireframeSpheres)
                    Gizmos.DrawSphere(p.position, particleRadius);
                else
                    Gizmos.DrawWireSphere(p.position, particleRadius);
            }
        }
    }

    private void SpawnParticlesInBox() {
        Vector3 spawnTopLeft = spawnBoxCenter - spawnBox / 2;
        int xIterations = Mathf.RoundToInt(spawnBox.x / (particleRadius * 2));
        int yIterations = Mathf.RoundToInt(spawnBox.y / (particleRadius * 2));
        int zIterations = Mathf.RoundToInt(spawnBox.z / (particleRadius * 2));

        for (int x = 1; x < xIterations; x++) {
            for (int y = 1; y < yIterations; y++) {
                for (int z = 1; z < zIterations; z++) {

                    Vector3 spawnPosition = spawnTopLeft + new Vector3(x * particleRadius * 2, y * particleRadius * 2, z * particleRadius * 2) + Random.onUnitSphere * particleRadius * 0.5f;

                    Particle p = new Particle
                    {
                        position = spawnPosition
                    };

                    particles.Add(p);

                }
            }
        }

    }

    // Run after accelerations have been set
    private void MoveParticles (float timestep) {
        
        Vector3 topRight = boxSize / 2;
        Vector3 bottomLeft = -boxSize / 2;
        
        for (int i = 0; i < particles.Count; i++)
        {
            Particle p = particles[i];

            p.velocity += timestep * p.currentForce / particleMass;
            p.position += timestep * p.velocity;

            p.onBoundary = false;

            // Minimum Enforcements

            if (p.position.x - particleRadius < bottomLeft.x) {
                p.velocity.x *= boundDamping;
                p.position.x = bottomLeft.x + particleRadius;
                p.onBoundary = true;
            }

            if (p.position.y - particleRadius < bottomLeft.y) {
                p.velocity.y *= boundDamping;
                p.position.y = bottomLeft.y + particleRadius;
                p.onBoundary = true;
            }

            if (p.position.z - particleRadius < bottomLeft.z) {
                p.velocity.z *= boundDamping;
                p.position.z = bottomLeft.z + particleRadius;
                p.onBoundary = true;
            }

            // Maximum Enforcements

            if (p.position.x + particleRadius > topRight.x) {
                p.velocity.x *= boundDamping;
                p.position.x = topRight.x - particleRadius;
                p.onBoundary = true;
            }

            if (p.position.y + particleRadius > topRight.y) {
                p.velocity.y *= boundDamping;
                p.position.y = topRight.y - particleRadius;
                p.onBoundary = true;
            }

            if (p.position.z + particleRadius > topRight.z) {
                p.velocity.z *= boundDamping;
                p.position.z = topRight.z - particleRadius;
                p.onBoundary = true;
            }
        }
    }
    

    private void Awake() {
        SpawnParticlesInBox();
    }
    
    [Header("Error Reduction")]
    public float scalingConstant = 0.004f;

    private void ComputeDensities() {
        // The density at any given point is simply the weighted sums of mass near it, as density is simply the 'amount of mass at a point'

        float scaledRadius = particleRadius * scalingConstant;
        float scaledRadiusSquared = (scaledRadius * scaledRadius);
        float weightConstant = 4 / (Mathf.PI * Mathf.Pow(scaledRadius, 8.0f));

        for (int i = 0; i < particles.Count; i++) {

            Particle a = particles[i];
            a.density = 0f;

            for (int j = 0; j < particles.Count; j++) {
                Particle b = particles[j];
                float distanceSquared = (b.position - a.position).sqrMagnitude;

                if (scaledRadiusSquared > distanceSquared)
                {
                    a.density += particleMass * weightConstant * Mathf.Pow(scaledRadiusSquared - distanceSquared, 3.0f);
                }
            }

            // Calculate Pressure from Density ( pressure = gas_const * (density - resting_density) )
            a.pressure = gasConstant * (a.density - restingDensity);
        }

        

    }

    // Kernel by Müller et al.
    private float StdKernel(float distanceSquared)
    {
        // Doyub Kim
        float x = 1.0f - distanceSquared / Mathf.Pow(particleRadius,2);
        return 315f / ( 64f * Mathf.PI * Mathf.Pow(particleRadius,3) ) * x * x * x;
    } 

    private void ComputeAcceleration() {

        float scaledRadius = particleRadius * scalingConstant;
        float pressureWeightConstant = -45 / (Mathf.PI * Mathf.Pow(scaledRadius, 6));
        float viscWeightConstant = 45 / (Mathf.PI * Mathf.Pow(scaledRadius, 6));
        float SPIKY_GRAD = -10.0f / (Mathf.PI * Mathf.Pow(particleRadius, 5.0f));
        float VISC_LAP = 40.0f / (Mathf.PI * Mathf.Pow(particleRadius, 5.0f));

        for (int i = 0; i < particles.Count; i++)
        {
            Particle a = particles[i];

            Vector3 pressure = Vector3.zero;
            Vector3 visc = Vector3.zero;
            
            float particleDensitySquared = a.density * a.density;

            for (int j = 0; j < particles.Count; j++)
            {
                Particle b = particles[j];

                if (i == j) continue;
                
                float scaledDistance = Vector3.Distance(a.position, b.position) * scalingConstant;
                

                if (scaledDistance < particleRadius*2)
                {
                    // Calculate Pressure Gradient [ the water moves from high pressure -> low pressure (affecting velocity) ]
                    float pressureScalar = (a.pressure / (Mathf.Pow(a.density, 2))) + (b.pressure / (Mathf.Pow(b.density, 2)));
                    Vector3 pressureGradientDirection = ((a.position - b.position).normalized);

                    pressure += pressureGradientDirection * particleMass * (a.pressure + b.pressure) / (2.0f * b.density) * Mathf.Pow(particleRadius - scaledDistance, 3.0f) * SPIKY_GRAD;

                    // pressure += (particleMass * pressureScalar * pressureWeightConstant * Mathf.Pow(scaledRadius - scaledDistance, 3)) * -pressureGradientDirection;

                    // Calculate force of Viscosity (Helps particles move together - velocities tend to the same values)
                    visc += viscosity * particleMass * (b.velocity - a.velocity) / b.density * VISC_LAP * (particleRadius - scaledDistance);
                }
            }

            // Vector3 gravForce = gravity * particleMass / a.density;

            a.currentForce = (gravity * particleMass) + pressure + visc/a.density;

            // Debug.DrawRay(a.position, pressure.normalized*particleRadius, Color.yellow, Time.deltaTime);
        }

    }

    private Vector3 SpikyKernelGradient(float distance, Vector3 directionFromCenter)
    {
        return SpikyKernelFirstDerivative(distance) * directionFromCenter;
    }

    private float SpikyKernelFirstDerivative(float distance)
    {
        float x = 1.0f - distance / particleRadius;
        return -45.0f / ( Mathf.PI * Mathf.Pow(particleRadius,4) ) * x * x;
    }

    private void FixedUpdate() {

        // Density Solver
        ComputeDensities();

        // Force Solver
        ComputeAcceleration();

        MoveParticles(timeScale);
    }

}
