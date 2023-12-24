// Simulation.cs

using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Unity.Mathematics;
using System;
using System.Linq;

public class Simulation : MonoBehaviour
{
    public GameObject particle_prefab;
    private List<GameObject> particles = new();
    // Creating a 2D matrix where each element is a List of integers
    private List<List<HashSet<Vector2>>> particle_chunks = new();
    private GameObject simulation_boundary;

    public int particles_num = 10;
    public int border_width = 20;
    public int border_height = 10;
    public float Gravity_force = 20f;
    public float Max_influence_radius = 1;
    public int Framerate_max = 1000;
    public float Program_speed = 5f;
    public int Particle_amount = 200;
    public float Target_density = 130f;
    public float Pressure_multiplier = 20f;
    public float Wall_collision_damping_factor = 0.8f;
    public float Smooth_Max = 150f;
    public float Smooth_derivative_koefficient = 0.1f;
    public float Look_ahead_factor = 0.02f;
    public int Chunk_amount_multiplier = 2;
    public float Viscocity = 0.1f;
    private int Chunk_amount_x = 0;
    private int Chunk_amount_y = 0;

    private Vector2[] position;
    private Vector2[] velocity;
    // Predicted_velocities
    private Vector2[] p_velocity;
    private List<float> density;
    private float dTime;

    void Start()
    {
        // Create border
        Create_simulation_boundary();

        // initialize particle property arrays
        position = new Vector2[particles_num];
        velocity = new Vector2[particles_num];
        p_velocity = new Vector2[particles_num];
        density = new List<float>(particles_num);


        Chunk_amount_x = (int)(border_width * Chunk_amount_multiplier / Max_influence_radius);
        Chunk_amount_y = (int)(border_height * Chunk_amount_multiplier / Max_influence_radius);

        // Initialize particle_chunks
        particle_chunks = new List<List<HashSet<Vector2>>>();

        for (int depth1 = 0; depth1 < Chunk_amount_x; depth1++)
        {
            List<HashSet<Vector2>> innerList = new();
            
            for (int depth2 = 0; depth2 < Chunk_amount_y; depth2++)
            {
                innerList.Add(new HashSet<Vector2>());
            }

            particle_chunks.Add(innerList);
        }

        density = new List<float>(particles_num);
        for (int i = 0; i < particles_num; i++)
        {
            density.Add(0.0f); // Or any default value you prefer
        }

        // Create particles
        for (int i = 0; i < particles_num; i++)
        {
            Create_particle();
        }

        // Assign random positions
        for (int i = 0; i < particles_num; i++)
        {
            position[i] = Random_particle_position();
        }
    }

    void Update()
    {
        dTime = Time.deltaTime;

        Sort_particles();

        Physics();

        for (int i = 0; i < particles_num; i++)
        {
            particles[i].transform.position = new Vector3(position[i].x, position[i].y, 0);
        }
    }

    void Sort_particles()
    {
        for(int i = 0; i < particles_num; i++)
        {
            // Set predicted velocities
            p_velocity[i] = position[i] + velocity[i] * Look_ahead_factor;

            int inChunkX = (int)Math.Floor(position[i].x * Chunk_amount_multiplier / Max_influence_radius);
            int inChunkY = (int)Math.Floor(position[i].y * Chunk_amount_multiplier / Max_influence_radius);
            float relative_position_x = position[i].x * Chunk_amount_multiplier / Max_influence_radius % 1;
            float relative_position_y = position[i].y * Chunk_amount_multiplier / Max_influence_radius % 1;

            for (int x = -Chunk_amount_multiplier; x <= Chunk_amount_multiplier; x++)
            {
                for (int y = -Chunk_amount_multiplier; y <= Chunk_amount_multiplier; y++)
                {
                    // current chunk
                    int cur_chunk_x = inChunkX + x;
                    int cur_chunk_y = inChunkY + y;

                    if (cur_chunk_x >= 0 && cur_chunk_x < Chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < Chunk_amount_y)
                    {
                        if (Chunk_amount_multiplier > new Vector2(x-relative_position_x, y-relative_position_y).magnitude || Chunk_amount_multiplier > new Vector2(x+1-relative_position_x, y-relative_position_y).magnitude || Chunk_amount_multiplier > new Vector2(x-relative_position_x, y+1-relative_position_y).magnitude || Chunk_amount_multiplier > new Vector2(x+1-relative_position_x, y+1-relative_position_y).magnitude)
                        {
                            particle_chunks[cur_chunk_x][cur_chunk_y].Add(position[i]);
                        }
                    }
                }
            }
        }
    }

    float Influence(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        float totInfluence = particle_chunks[in_chunk_x][in_chunk_y]
            .Sum(other_position =>
                Smooth(Mathf.Sqrt(Mathf.Pow(position[particle_index].x - other_position.x, 2) + Mathf.Pow(position[particle_index].y - other_position.y, 2))));
        return totInfluence;
    }

    void Precalculate_densities()
    {
        for (int i = 0; i < particles_num; i++)
        {
            density[i] = Influence(i);
        }
    }

    float Smooth(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        return Smooth_Max * Mathf.Exp(-Smooth_derivative_koefficient * distance);
    }

    float Smooth_der(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        return -Smooth_Max * Smooth_derivative_koefficient * Mathf.Exp(-Smooth_derivative_koefficient * distance);
    }

    float Density_to_pressure(float density)
    {
        float density_error = density - Target_density;
        float pressure = density_error * Pressure_multiplier;
        return pressure;
    }

    float Shared_pressure(float density_A, float density_B)
    {
        float pressure_A = Density_to_pressure(density_A);
        float pressure_B = Density_to_pressure(density_B);

        float Shared_value = (pressure_A + pressure_B) / 2;

        return Shared_value;
    }

    Vector2 Pressure_force(int particle_index)
    {

        Vector2 pressure_force = new(0.0f, 0.0f);

        int in_chunk_x = (int)Math.Floor(position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        foreach (Vector2 other_position in particle_chunks[in_chunk_x][in_chunk_y])
        {
            if (other_position == position[particle_index])
            {
                continue;
            }

            Vector2 relative_distance = position[particle_index] - other_position;

            float distance = relative_distance.magnitude;

            float abs_pressure_gradient = Smooth_der(distance);

            Vector2 pressure_gradient = new(0.0f, 0.0f);

            if (distance != 0)
            {
                pressure_gradient = relative_distance.normalized * abs_pressure_gradient;
            }
            else
            {
                Vector2 randomNormalizedVector = new(UnityEngine.Random.onUnitSphere.x, UnityEngine.Random.onUnitSphere.y);

                // * 0.1f to decrease pressure forces (for "corner particles")
                pressure_gradient = 0.1f * abs_pressure_gradient * randomNormalizedVector;
            }

            //                                                            SHOULD BE density[i]
            float avg_pressure = Shared_pressure(density[particle_index], density[i]);

            // p(pos) = ∑_i (p_i * m / ρ_i * Smooth(pos - pos_i))
            //                                                   SHOULD BE density[i]
            pressure_force += avg_pressure * pressure_gradient / density[i];

        }

        return pressure_force;

    }

    void Apply_viscocity_to_velocity()
    {

    }

    void Physics()
    {

    }

    void Create_particle()
    {
        GameObject particle = Instantiate(particle_prefab, Random_particle_position(), Quaternion.identity);
        particle.transform.parent = transform;
        particles.Add(particle);
    }
void Create_simulation_boundary()
{
    // Create an empty GameObject for the border
    simulation_boundary = new GameObject("SimulationBoundary");
    simulation_boundary.transform.parent = transform;

    // Add a LineRenderer component to represent the border
    LineRenderer lineRenderer = simulation_boundary.AddComponent<LineRenderer>();
    lineRenderer.positionCount = 5;

    lineRenderer.SetPositions(new Vector3[]
    {
        new(0f, 0f, 0f),
        new(border_width, 0f, 0f),
        new(border_width, border_height, 0f),
        new(0f, border_height, 0f),
        new(0f, 0f, 0f),
    });

    // Optional: Set LineRenderer properties (material, color, width, etc.)
    // lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    lineRenderer.startColor = Color.green;
    lineRenderer.endColor = Color.green;
    lineRenderer.startWidth = 0.1f;
    lineRenderer.endWidth = 0.1f;
}


    Vector2 Random_particle_position()
    {
        float x = UnityEngine.Random.Range(1, border_width-1);
        float y = UnityEngine.Random.Range(1, border_height-1);
        return new Vector2(x, y);
    }
}
