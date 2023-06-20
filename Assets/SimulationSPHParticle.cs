using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class SimulationSPHParticle : MonoBehaviour
{
    public TextMeshPro text;

    float StdKernel (float distanceSquared) {
        float x = 1.0f - distanceSquared / (radius*radius);
        return 315.0f / (64.0f * Mathf.PI * radius*radius*radius) * x * x * x;
    }

    private float density = 0f;
    private float pressure = 0f;
    public float radius = 0.3f;

    private void Update() {
        float sum = 0f;
        Vector3 origin = transform.position;

        foreach (SimulationSPHParticle p in GameObject.FindObjectsOfType<SimulationSPHParticle>()) 
        {
            Vector3 diff = origin - p.transform.position;
            float distSqd = Vector3.Dot(diff,diff);

            if (radius > diff.magnitude)
                sum += StdKernel(distSqd);
        }

        density = sum * 1 + 0.0001f;
        text.text = "Density ( " + density.ToString("0.0") + " )\nPressure ( " + (8.134 * (density - (StdKernel(0)+0.001f))).ToString("0.0") + " )";
    }

}
