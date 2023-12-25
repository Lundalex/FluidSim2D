using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Unity.Mathematics;
using System;
using System.Linq;
using Unity.VisualScripting;
using System.Numerics;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
public class Simulation_w_jobs : MonoBehaviour
{
    public GameObject particle_prefab;
    private List<GameObject> particles = new();
    // Creating a 2D matrix where each element is a List of integers
    private List<List<HashSet<int>>> particle_chunks = new();
    private GameObject simulation_boundary;

    public int particles_num = 500;
    public int border_width = 26;
    public int border_height = 13;
    public float Gravity_force = 3f;
    public int Max_influence_radius = 2;
    public int Framerate_max = 1000;
    public float Program_speed = 2f;
    public float Target_density = 7f;
    public float Pressure_multiplier = 70f;
    public float Collision_damping_factor = 0.4f;
    public float Smooth_Max = 5f;
    public float Smooth_derivative_koefficient = 2.5f;
    public float Look_ahead_factor = 0.02f;
    public int Chunk_amount_multiplier = 2;
    public float border_thickness = 0.2f;
    public float Viscocity = 0.2f;
    private int Chunk_amount_x = 0;
    private int Chunk_amount_y = 0;
    public int Chunk_capacity = 70;
    public int Particle_chunks_tot_num = 0;
    private float Chunk_amount_multiplier_squared = 0;

    private Vector2[] position;
    private Vector2[] velocity;
    // Predicted_velocities
    private Vector2[] p_position;
    private float[] density;
    private int[] new_particle_chunks;
    private float delta_time;

    void Start()
    {
        // Create border
        Create_simulation_boundary();

        // initialize particle property arrays
        position = new Vector2[particles_num];
        velocity = new Vector2[particles_num];
        p_position = new Vector2[particles_num];
        density = new float[particles_num];


        Chunk_amount_x = (int)(border_width * Chunk_amount_multiplier / Max_influence_radius);
        Chunk_amount_y = (int)(border_height * Chunk_amount_multiplier / Max_influence_radius);


        Chunk_amount_multiplier_squared = Chunk_amount_multiplier * Chunk_amount_multiplier;

        Particle_chunks_tot_num = Chunk_amount_x * Chunk_amount_y * Chunk_capacity;
        new_particle_chunks = new int[Particle_chunks_tot_num];

        for (int i = 0; i < particles_num; i++)
        {
            position[i] = new(0.0f, 0.0f);
            velocity[i] = new(0.0f, 0.0f);
            p_position[i] = new(0.0f, 0.0f);
            density[i] = 0.0f;
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
        delta_time = Time.deltaTime * Program_speed;

        Array.Fill(new_particle_chunks, -1);

        particle_chunks = new List<List<HashSet<int>>>();
        for (int depth1 = 0; depth1 < Chunk_amount_x; depth1++)
        {
            List<HashSet<int>> innerList = new();
            
            for (int depth2 = 0; depth2 < Chunk_amount_y; depth2++)
            {
                innerList.Add(new HashSet<int>());
            }

            particle_chunks.Add(innerList);
        }

        Sort_particles();

        Precalculate_densities();

        Physics();

        // Render
        for (int i = 0; i < particles_num; i++)
        {
            particles[i].transform.position = new Vector3(position[i].x, position[i].y, 0);
        }
    }

    void Sort_particles()
    {
        for(int i = 0; i < particles_num; i++)
        {
            // Set predicted positions
            p_position[i] = position[i] + velocity[i] * Look_ahead_factor;

            int inChunkX = (int)(p_position[i].x * Chunk_amount_multiplier / Max_influence_radius);
            int inChunkY = (int)(p_position[i].y * Chunk_amount_multiplier / Max_influence_radius);
            float relative_position_x = p_position[i].x * Chunk_amount_multiplier / Max_influence_radius % 1;
            float relative_position_y = p_position[i].y * Chunk_amount_multiplier / Max_influence_radius % 1;

            for (int x = -Chunk_amount_multiplier; x <= Chunk_amount_multiplier; x++)
            {
                for (int y = -Chunk_amount_multiplier; y <= Chunk_amount_multiplier; y++)
                {
                    // current chunk
                    int cur_chunk_x = inChunkX + x;
                    int cur_chunk_y = inChunkY + y;

                    if (cur_chunk_x >= 0 && cur_chunk_x < Chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < Chunk_amount_y)
                    {
                        if (Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y-relative_position_y)*(y-relative_position_y) ||
                            Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y-relative_position_y)*(y-relative_position_y)||
                            Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y) ||
                            Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y))
                        {
                            // REMOVE    V              !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                            // particle_chunks[cur_chunk_x][cur_chunk_y].Add(i);
                            // curx * Chunk_amount_x * Chunk_amount_y + cury * Chunk_amount_y + curz
                            int xy_index = cur_chunk_x * Chunk_amount_y * Chunk_capacity + cur_chunk_y * Chunk_capacity;
                            int z_index = 0;
                            while (new_particle_chunks[xy_index + z_index] != -1 && z_index < Chunk_capacity - 1)
                            {
                                z_index += 1;
                            }
                            // Add to list if space is available
                            if (z_index < Chunk_capacity)
                            {
                                new_particle_chunks[xy_index + z_index] = i;
                            }
                        }
                    }
                }
            }
        }
        return;
    }

    float Influence(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        // float totInfluence = particle_chunks[in_chunk_x][in_chunk_y]
        //     .Sum(other_particle_index =>
        //         Smooth(Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[other_particle_index].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[other_particle_index].y, 2))));


        float tot_influence = 0;
        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        // List<int> chunk_list = new_particle_chunks.Skip(start_i).Take(Chunk_capacity).ToList();
        List<int> chunk_list = new_particle_chunks.Skip(start_i).TakeWhile(item => item != -1).ToList();

        foreach (var other_particle_index in chunk_list)
        {
            // if (other_particle_index != -1)
            // {
                tot_influence += Smooth(Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[other_particle_index].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[other_particle_index].y, 2)));
            // }
        }

        return tot_influence;

    }

    void Precalculate_densities()
    {

        // MAYBE - MIGHT NEED TO RESET THE VALUES EACH ITERATION --- PLACE SOMEWHERE ELSE TO AVOID FOR LOOP WITH ca 50000 elements each update iteration !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<float> n_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<int> n_new_particle_chunks = new NativeArray<int>(Particle_chunks_tot_num, Allocator.TempJob);

        for (int i = 0; i < particles_num; i++)
        {
            n_p_position[i] = p_position[i];
            n_position[i] = position[i];
        }

        for (int i = 0; i < Particle_chunks_tot_num; i++)
        {
            n_new_particle_chunks[i] = new_particle_chunks[i];
        }

        Calculate_density_job calculate_density_job = new Calculate_density_job {
            Chunk_amount_multiplier = Chunk_amount_multiplier,
            Max_influence_radius = Max_influence_radius,
            Smooth_Max = Smooth_Max,
            Smooth_derivative_koefficient = Smooth_derivative_koefficient,
            Chunk_amount_y = Chunk_amount_y,
            Chunk_amount_x = Chunk_amount_x,
            Chunk_capacity = Chunk_capacity,
            p_position = n_p_position,
            position = n_position,
            new_particle_chunks = n_new_particle_chunks,
            density = n_density
        };

        JobHandle jobhandle = calculate_density_job.Schedule(particles_num, 64);

        jobhandle.Complete();

        for (int i = 0; i < particles_num; i++)
        {
            density[i] = n_density[i];
        }
 
        n_p_position.Dispose();
        n_position.Dispose();
        n_density.Dispose();
        n_new_particle_chunks.Dispose();

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

        int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        // float tot_influence = 0;
        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        // List<int> chunk_list = new_particle_chunks.Skip(start_i).Take(Chunk_capacity).ToList();
        List<int> chunk_list = new_particle_chunks.Skip(start_i).TakeWhile(item => item != -1).ToList();

        foreach (int other_particle_index in chunk_list)
        {
            if (other_particle_index == particle_index)
            {
                continue;
            }

            Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

            float distance = relative_distance.magnitude;

            float abs_pressure_gradient = Smooth_der(distance);

            Vector2 pressure_gradient = new(0.0f, 0.0f);

            if (distance != 0)
            {
                pressure_gradient = relative_distance.normalized * abs_pressure_gradient;
            }
            else
            {
                Vector2 randomNormalizedVector = new(Random.onUnitSphere.x, Random.onUnitSphere.y);

                // * 0.1f to decrease pressure forces (for "corner particles")
                pressure_gradient = 0.1f * abs_pressure_gradient * randomNormalizedVector;
            }

            float avg_pressure = Shared_pressure(density[particle_index], density[other_particle_index]);

            // p(pos) = ∑_i (p_i * m / ρ_i * Smooth(pos - pos_i))
            pressure_force += avg_pressure * pressure_gradient / density[other_particle_index];

        }

        return -pressure_force;

    }

    Vector2 Apply_viscocity_to_velocity(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);
        int tot_mass = 0;

        Vector2 tot_force = new(0.0f, 0.0f);

        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        // List<int> chunk_list = new_particle_chunks.Skip(start_i).Take(Chunk_capacity).ToList();
        List<int> chunk_list = new_particle_chunks.Skip(start_i).TakeWhile(item => item != -1).ToList();

        foreach (int other_particle_index in chunk_list)
        {
            // if (other_particle_index == -1)
            // {
            //     continue;
            // }

            tot_mass += 1;

            if (other_particle_index == particle_index)
            {
                continue;
            }

            Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

            float distance = relative_distance.magnitude;

            if (distance == 0)
                continue;
            
            float abs_force = Smooth(distance);

            Vector2 force = new(relative_distance.normalized.x * abs_force, relative_distance.normalized.y * abs_force);

            tot_force += force;
        }

        if (tot_mass == 0)
        {
            tot_mass = 1;
        }

        Vector2 average_viscocity_velocity_x = tot_force / tot_mass;

        Vector2 new_velocity = (1 - Viscocity * delta_time) * velocity[particle_index] + Viscocity * delta_time * average_viscocity_velocity_x;

        return new_velocity;
    }

    void Physics()
    {
        for (int i = 0; i < particles_num; i++)
        {
            velocity[i].y -= Gravity_force * delta_time;

            Vector2 pressure_force = Pressure_force(i);
            
            velocity[i] += pressure_force * delta_time / density[i];

            velocity[i] = Apply_viscocity_to_velocity(i);

            position[i] += velocity[i] * delta_time;

            if (position[i].y > border_height - border_thickness)
            {
                velocity[i].y = -Mathf.Abs(velocity[i].y * Collision_damping_factor);
                position[i].y = border_height - border_thickness;
            }
            if (position[i].y < border_thickness)
            {
                velocity[i].y = +Mathf.Abs(velocity[i].y * Collision_damping_factor);
                position[i].y = border_thickness;
            }
            if (position[i].x > border_width - border_thickness)
            {
                velocity[i].x = -Mathf.Abs(velocity[i].x * Collision_damping_factor);
                position[i].x = border_width - border_thickness;
            }
            if (position[i].x < border_thickness)
            {
                velocity[i].x = +Mathf.Abs(velocity[i].x * Collision_damping_factor);
                position[i].x = border_thickness;
            }
        }
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



[BurstCompile]
public struct Calculate_density_job : IJobParallelFor {

    public int Chunk_amount_multiplier;
    public int Max_influence_radius;
    public int flat_particle_chunks_tot_indexes;
    public float Smooth_Max;
    public float Smooth_derivative_koefficient;
    public int Chunk_amount_y;
    public int Chunk_amount_x;
    public int Chunk_capacity;
    [ReadOnly] public NativeArray<Vector2> p_position;
    [ReadOnly] public NativeArray<Vector2> position;
    [ReadOnly] public NativeArray<int> new_particle_chunks;
    public NativeArray<float> density;
    // [ReadOnly] public NativeArray<int> particle_chunks;
    public void Execute(int i) {

        density[i] = Influence(i);
    }

    float Influence(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        int end_i = start_i + Chunk_capacity;

        float totInfluence = 0.0f;

        for (int i = start_i; i < end_i; i++)
        {
            if (new_particle_chunks[i] == -1){break;}

            float distance = Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[new_particle_chunks[i]].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[new_particle_chunks[i]].y, 2));

            totInfluence += Smooth(distance);
        }

        return totInfluence;
    }

    float Smooth(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        return Smooth_Max * Mathf.Exp(-Smooth_derivative_koefficient * distance);
    }
}




















// using UnityEngine;
// using System.Collections.Generic;
// using UnityEngine.UIElements;
// using Unity.Mathematics;
// using System;
// using System.Linq;
// using Unity.VisualScripting;
// using System.Numerics;
// using Vector2 = UnityEngine.Vector2;
// using Vector3 = UnityEngine.Vector3;
// using Quaternion = UnityEngine.Quaternion;
// using Unity.Jobs;
// using Unity.Collections;
// using Unity.Burst;
// public class Simulation_w_jobs : MonoBehaviour
// {
//     public GameObject particle_prefab;
//     private List<GameObject> particles = new();
//     // Creating a 2D matrix where each element is a List of integers
//     private List<List<HashSet<int>>> particle_chunks = new();
//     private GameObject simulation_boundary;

//     public int particles_num = 600;
//     public int border_width = 26;
//     public int border_height = 13;
//     public float Gravity_force = 3f;
//     public int Max_influence_radius = 1;
//     public int Framerate_max = 1000;
//     public float Program_speed = 2f;
//     public float Target_density = 10f;
//     public float Pressure_multiplier = 70f;
//     public float Collision_damping_factor = 0.4f;
//     public float Smooth_Max = 5f;
//     public float Smooth_derivative_koefficient = 2.5f;
//     public float Look_ahead_factor = 0.02f;
//     public int Chunk_amount_multiplier = 2;
//     public float border_thickness = 0.2f;
//     public float Viscocity = 0.2f;
//     public int Chunk_particle_capacity = 20;
//     private int Chunk_amount_x = 0;
//     private int Chunk_amount_y = 0;
//     private int Chunk_amount_xy = 0;
//     private float Chunk_amount_multiplier_squared = 0;

//     private Vector2[] position;
//     private Vector2[] velocity;
//     // Predicted_velocities
//     private Vector2[] p_position;
//     private List<float> density;
//     private float delta_time;

//     void Start()
//     {
//         // Create border
//         Create_simulation_boundary();

//         // initialize particle property arrays
//         position = new Vector2[particles_num];
//         velocity = new Vector2[particles_num];
//         p_position = new Vector2[particles_num];
//         density = new List<float>(particles_num);


//         Chunk_amount_x = (int)(border_width * Chunk_amount_multiplier / Max_influence_radius);
//         Chunk_amount_y = (int)(border_height * Chunk_amount_multiplier / Max_influence_radius);

//         Chunk_amount_multiplier_squared = Chunk_amount_multiplier * Chunk_amount_multiplier;
//         // Initialize particle_chunks
//         particle_chunks = new List<List<HashSet<int>>>();

//         for (int depth1 = 0; depth1 < Chunk_amount_x; depth1++)
//         {
//             List<HashSet<int>> innerList = new();
            
//             for (int depth2 = 0; depth2 < Chunk_amount_y; depth2++)
//             {
//                 innerList.Add(new HashSet<int>());
//             }

//             particle_chunks.Add(innerList);
//         }

//         density = new List<float>(particles_num);
//         for (int i = 0; i < particles_num; i++)
//         {
//             density.Add(0.0f); // Or any default value you prefer
//         }

//         // Create particles
//         for (int i = 0; i < particles_num; i++)
//         {
//             Create_particle();
//         }

//         // Assign random positions
//         for (int i = 0; i < particles_num; i++)
//         {
//             position[i] = Random_particle_position();
//         }
//     }

//     void Update()
//     {
//         delta_time = Time.deltaTime * Program_speed;

//         Sort_particles();

//         Precalculate_densities();

//         Physics(); 

//         // Render
//         for (int i = 0; i < particles_num; i++)
//         {
//             particles[i].transform.position = new Vector3(position[i].x, position[i].y, 0);
//         }
//     }

//     void Sort_particles()
//     {
//         // NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
//         // NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
//         // NativeArray<Vector2> n_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);

//         // for (int i = 0; i < particles_num; i++)
//         // {
//         //     n_p_position[i] = p_position[i];
//         //     n_position[i] = position[i];
//         //     n_velocity[i] = velocity[i];
//         // }

//         // NativeArray<int> n_particle_chunks = new NativeArray<int>(Chunk_amount_x * Chunk_amount_y * Chunk_particle_capacity, Allocator.TempJob);

//         // Particle_sort_job particle_sort_job = new Particle_sort_job {
//         //     c_particles_num = particles_num,
//         //     c_Chunk_amount_multiplier = Chunk_amount_multiplier,
//         //     c_Max_influence_radius = Max_influence_radius,
//         //     c_Chunk_amount_x = Chunk_amount_x,
//         //     c_Chunk_amount_y = Chunk_amount_y,
//         //     c_Chunk_amount_multiplier_squared = Chunk_amount_multiplier_squared,
//         //     c_Look_ahead_factor = Look_ahead_factor,
//         //     c_p_position = n_p_position,
//         //     c_position = n_position,
//         //     c_velocity = n_velocity,
//         //     c_particle_chunks = n_particle_chunks,
//         //     c_Chunk_particle_capacity = Chunk_particle_capacity
//         // };

//         // JobHandle jobhandle = particle_sort_job.Schedule(particles_num, Chunk_amount_x * Chunk_amount_y * Chunk_particle_capacity);

//         // jobhandle.Complete();

//         // for (int cur_chunk_x = 0; cur_chunk_x < Chunk_amount_x; cur_chunk_x++)
//         // {
//         //     for (int cur_chunk_y = 0; cur_chunk_y < Chunk_amount_y; cur_chunk_y++)
//         //     {
//         //         particle_chunks[cur_chunk_x][cur_chunk_y].Clear();
//         //         for (int cur_z = 0; cur_z < Chunk_particle_capacity; cur_z++)
//         //         {
//         //             // cur_chunk_x * Chunk_amount_y * Chunk_particle_capacity + cur_chunk_y * Chunk_particle_capacity + cur_z
//         //             particle_chunks[cur_chunk_x][cur_chunk_y].Add(n_particle_chunks[cur_chunk_x * Chunk_amount_y * Chunk_particle_capacity + cur_chunk_y * Chunk_particle_capacity + cur_z]);
//         //         }
//         //     }
//         // }

//         // for (int i = 0; i < particles_num; i++)
//         // {
//         //     p_position[i] = n_p_position[i];
//         // }

//         // n_p_position.Dispose();
//         // n_position.Dispose();
//         // n_velocity.Dispose();
//         // n_particle_chunks.Dispose();


        
//         // List<int> c_particle_chunks = new List<int>(Chunk_amount_x * Chunk_amount_y * Chunk_particle_capacity);

//         // for (int i = 0; i < Chunk_amount_x * Chunk_amount_y * Chunk_particle_capacity; i++)
//         // {
//         //     c_particle_chunks.Add(0);
//         // }

//         // for (int i = 0; i < particles_num; i++)
//         // {
//         //     p_position[i] = position[i] + velocity[i] * Look_ahead_factor;

//         //     int inChunkX = (int)(p_position[i].x * Chunk_amount_multiplier / Max_influence_radius);
//         //     int inChunkY = (int)(p_position[i].y * Chunk_amount_multiplier / Max_influence_radius);
//         //     float relative_position_x = p_position[i].x * Chunk_amount_multiplier / Max_influence_radius % 1;
//         //     float relative_position_y = p_position[i].y * Chunk_amount_multiplier / Max_influence_radius % 1;

//         //     for (int x = -Chunk_amount_multiplier; x <= Chunk_amount_multiplier; x++)
//         //     {
//         //         for (int y = -Chunk_amount_multiplier; y <= Chunk_amount_multiplier; y++)
//         //         {
//         //             // current chunk
//         //             int cur_chunk_x = inChunkX + x;
//         //             int cur_chunk_y = inChunkY + y;

//         //             if (cur_chunk_x >= 0 && cur_chunk_x < Chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < Chunk_amount_y)
//         //             {
//         //                 if (Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y-relative_position_y)*(y-relative_position_y) ||
//         //                     Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y-relative_position_y)*(y-relative_position_y)||
//         //                     Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y) ||
//         //                     Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y))
//         //                 {
//         //                     // curx * Chunk_amount_x * Chunk_amount_y + cury * Chunk_amount_y + curz
//         //                     int xy_index = cur_chunk_x * Chunk_amount_y * Chunk_particle_capacity + cur_chunk_y * Chunk_particle_capacity;
//         //                     int z_index = 0;
//         //                     while (c_particle_chunks[xy_index + z_index] != 0 && z_index < Chunk_particle_capacity - 1)
//         //                     {
//         //                         c_particle_chunks[xy_index + z_index] =
//         //                         z_index += 1;
//         //                     }
//         //                     // Don't add to list if no space is available
//         //                     if (z_index < Chunk_particle_capacity)
//         //                     {
//         //                         c_particle_chunks[xy_index + z_index] = i;
//         //                     }
//         //                 }
//         //             }
//         //         }
//         //     }
//         // }

//         // for (int cur_chunk_x = 0; cur_chunk_x < Chunk_amount_x; cur_chunk_x++)
//         // {
//         //     for (int cur_chunk_y = 0; cur_chunk_y < Chunk_amount_y; cur_chunk_y++)
//         //     {
//         //         particle_chunks[cur_chunk_x][cur_chunk_y].Clear();
//         //         for (int cur_z = 0; cur_z < Chunk_particle_capacity; cur_z++)
//         //         {
//         //             // cur_chunk_x * Chunk_amount_y * Chunk_particle_capacity + cur_chunk_y * Chunk_particle_capacity + cur_z
//         //             particle_chunks[cur_chunk_x][cur_chunk_y].Add(c_particle_chunks[cur_chunk_x * Chunk_amount_y * Chunk_particle_capacity + cur_chunk_y * Chunk_particle_capacity + cur_z]);
//         //         }
//         //     }
//         // }














//         for(int i = 0; i < particles_num; i++)
//         {
//             // Set predicted velocities
//             p_position[i] = position[i] + velocity[i] * Look_ahead_factor;

//             int inChunkX = (int)(p_position[i].x * Chunk_amount_multiplier / Max_influence_radius);
//             int inChunkY = (int)(p_position[i].y * Chunk_amount_multiplier / Max_influence_radius);
//             float relative_position_x = p_position[i].x * Chunk_amount_multiplier / Max_influence_radius % 1;
//             float relative_position_y = p_position[i].y * Chunk_amount_multiplier / Max_influence_radius % 1;

//             for (int x = -Chunk_amount_multiplier; x <= Chunk_amount_multiplier; x++)
//             {
//                 for (int y = -Chunk_amount_multiplier; y <= Chunk_amount_multiplier; y++)
//                 {
//                     // current chunk
//                     int cur_chunk_x = inChunkX + x;
//                     int cur_chunk_y = inChunkY + y;

//                     if (cur_chunk_x >= 0 && cur_chunk_x < Chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < Chunk_amount_y)
//                     {
//                         if (Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y-relative_position_y)*(y-relative_position_y) ||
//                             Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y-relative_position_y)*(y-relative_position_y)||
//                             Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y) ||
//                             Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y))
//                         {
//                             particle_chunks[cur_chunk_x][cur_chunk_y].Add(i);
//                         }
//                     }
//                 }
//             }
//         }
//     }

//     float Influence(int particle_index)
//     {
//         int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
//         int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

//         float totInfluence = particle_chunks[in_chunk_x][in_chunk_y]
//             .Sum(other_particle_index =>
//                 Smooth(Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[other_particle_index].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[other_particle_index].y, 2))));
//         return totInfluence;
//     }

//     void Precalculate_densities()
//     {

//         // NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
//         // NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
//         // NativeArray<float> n_density = new NativeArray<float>(particles_num, Allocator.TempJob);

//         // for (int i = 0; i < particles_num; i++)
//         // {
//         //     n_p_position[i] = p_position[i];
//         //     n_position[i] = position[i];
//         // }

//         // int flat_particle_chunks_tot_indexes = Chunk_amount_x * Chunk_amount_y * Chunk_particle_capacity;
//         // NativeArray<int> n_particle_chunks = new NativeArray<int>(flat_particle_chunks_tot_indexes, Allocator.TempJob);

//         // for (int cur_chunk_x = 0; cur_chunk_x < Chunk_amount_x; cur_chunk_x++)
//         // {
//         //     for (int cur_chunk_y = 0; cur_chunk_y < Chunk_amount_y; cur_chunk_y++)
//         //     {
//         //         int z_index = 0;
//         //         foreach (int value in particle_chunks[cur_chunk_x][cur_chunk_y])
//         //         {
//         //             if (z_index < Chunk_particle_capacity && value != 0)
//         //             {
//         //                 n_particle_chunks[cur_chunk_x * Chunk_amount_y * Chunk_particle_capacity + cur_chunk_y * Chunk_particle_capacity + z_index] = value;
//         //                 z_index += 1;
//         //             }
//         //             else
//         //             {
//         //                 // Handle the case where Chunk_particle_capacity is exceeded
//         //                 break;
//         //             }
//         //         }
//         //     }
//         // }


//         // Calculate_density_job calculate_density_job = new Calculate_density_job {
//         //     Chunk_amount_multiplier = Chunk_amount_multiplier,
//         //     Max_influence_radius = Max_influence_radius,
//         //     Smooth_Max = Smooth_Max,
//         //     Smooth_derivative_koefficient = Smooth_derivative_koefficient,
//         //     p_position = n_p_position,
//         //     particle_chunks = particle_chunks,
//         //     flat_particle_chunks_tot_indexes = flat_particle_chunks_tot_indexes,
//         //     density = n_density
//         // };

//         // JobHandle jobhandle = calculate_density_job.Schedule(particles_num, 150);

//         // jobhandle.Complete();

//         // for (int cur_chunk_x = 0; cur_chunk_x < Chunk_amount_x; cur_chunk_x++)
//         // {
//         //     for (int cur_chunk_y = 0; cur_chunk_y < Chunk_amount_y; cur_chunk_y++)
//         //     {
//         //         particle_chunks[cur_chunk_x][cur_chunk_y].Clear();
//         //         for (int cur_z = 0; cur_z < Chunk_particle_capacity; cur_z++)
//         //         {
//         //             // cur_chunk_x * Chunk_amount_y * Chunk_particle_capacity + cur_chunk_y * Chunk_particle_capacity + cur_z
//         //             particle_chunks[cur_chunk_x][cur_chunk_y].Add(n_particle_chunks[cur_chunk_x * Chunk_amount_y * Chunk_particle_capacity + cur_chunk_y * Chunk_particle_capacity + cur_z]);
//         //         }
//         //     }
//         // }

//         // for (int i = 0; i < particles_num; i++)
//         // {
//         //     density[i] = n_density[i];
//         // }

//         // n_p_position.Dispose();
//         // n_position.Dispose();
//         // n_density.Dispose();
//         // n_particle_chunks.Dispose();

//         for (int i = 0; i < particles_num; i++)
//         {
//             density[i] = Influence(i);
//         }
//     }

//     float Smooth(float distance)
//     {
//         // Geogebra: https://www.geogebra.org/calculator/vwapudgf
//         return Smooth_Max * Mathf.Exp(-Smooth_derivative_koefficient * distance);
//     }

//     float Smooth_der(float distance)
//     {
//         // Geogebra: https://www.geogebra.org/calculator/vwapudgf
//         return -Smooth_Max * Smooth_derivative_koefficient * Mathf.Exp(-Smooth_derivative_koefficient * distance);
//     }

//     float Density_to_pressure(float density)
//     {
//         float density_error = density - Target_density;
//         float pressure = density_error * Pressure_multiplier;
//         return pressure;
//     }

//     float Shared_pressure(float density_A, float density_B)
//     {
//         float pressure_A = Density_to_pressure(density_A);
//         float pressure_B = Density_to_pressure(density_B);

//         float Shared_value = (pressure_A + pressure_B) / 2;

//         return Shared_value;
//     }

//     Vector2 Pressure_force(int particle_index)
//     {

//         Vector2 pressure_force = new(0.0f, 0.0f);

//         int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
//         int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

//         foreach (int other_particle_index in particle_chunks[in_chunk_x][in_chunk_y])
//         {
//             if (other_particle_index == particle_index)
//             {
//                 continue;
//             }

//             Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

//             float distance = relative_distance.magnitude;

//             float abs_pressure_gradient = Smooth_der(distance);

//             Vector2 pressure_gradient = new(0.0f, 0.0f);

//             if (distance != 0)
//             {
//                 pressure_gradient = relative_distance.normalized * abs_pressure_gradient;
//             }
//             else
//             {
//                 Vector2 randomNormalizedVector = new(UnityEngine.Random.onUnitSphere.x, UnityEngine.Random.onUnitSphere.y);

//                 // * 0.1f to decrease pressure forces (for "corner particles")
//                 pressure_gradient = 0.1f * abs_pressure_gradient * randomNormalizedVector;
//             }

//             float avg_pressure = Shared_pressure(density[particle_index], density[other_particle_index]);

//             // p(pos) = ∑_i (p_i * m / ρ_i * Smooth(pos - pos_i))
//             pressure_force += avg_pressure * pressure_gradient / density[other_particle_index];

//         }

//         return -pressure_force;

//     }

//     Vector2 Apply_viscocity_to_velocity(int particle_index)
//     {
//         int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
//         int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);
//         int tot_mass = 0;

//         Vector2 tot_force = new(0.0f, 0.0f);

//         foreach (int other_particle_index in particle_chunks[in_chunk_x][in_chunk_y])
//         {
//             tot_mass += 1;

//             if (other_particle_index == particle_index)
//             {
//                 continue;
//             }

//             Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

//             float distance = relative_distance.magnitude;

//             if (distance == 0)
//                 continue;
            
//             float abs_force = Smooth(distance);

//             Vector2 force = new(relative_distance.normalized.x * abs_force, relative_distance.normalized.y * abs_force);

//             tot_force += force;
//         }

//         if (tot_mass == 0)
//         {
//             tot_mass = 1;
//         }

//         Vector2 average_viscocity_velocity_x = tot_force / tot_mass;

//         Vector2 new_velocity = (1 - Viscocity * delta_time) * velocity[particle_index] + Viscocity * delta_time * average_viscocity_velocity_x;

//         return new_velocity;
//     }

//     void Physics()
//     {
//         for (int i = 0; i < particles_num; i++)
//         {
//             velocity[i].y -= Gravity_force * delta_time;

//             Vector2 pressure_force = Pressure_force(i);
            
//             velocity[i] += pressure_force * delta_time / density[i];

//             velocity[i] = Apply_viscocity_to_velocity(i);

//             position[i] += velocity[i] * delta_time;

//             if (position[i].y > border_height - border_thickness)
//             {
//                 velocity[i].y = -Mathf.Abs(velocity[i].y * Collision_damping_factor);
//                 position[i].y = border_height - border_thickness;
//             }
//             if (position[i].y < border_thickness)
//             {
//                 velocity[i].y = +Mathf.Abs(velocity[i].y * Collision_damping_factor);
//                 position[i].y = border_thickness;
//             }
//             if (position[i].x > border_width - border_thickness)
//             {
//                 velocity[i].x = -Mathf.Abs(velocity[i].x * Collision_damping_factor);
//                 position[i].x = border_width - border_thickness;
//             }
//             if (position[i].x < border_thickness)
//             {
//                 velocity[i].x = +Mathf.Abs(velocity[i].x * Collision_damping_factor);
//                 position[i].x = border_thickness;
//             }
//         }
//     }

//     void Create_particle()
//     {
//         GameObject particle = Instantiate(particle_prefab, Random_particle_position(), Quaternion.identity);
//         particle.transform.parent = transform;
//         particles.Add(particle);
//     }
//     void Create_simulation_boundary()
//     {
//         // Create an empty GameObject for the border
//         simulation_boundary = new GameObject("SimulationBoundary");
//         simulation_boundary.transform.parent = transform;

//         // Add a LineRenderer component to represent the border
//         LineRenderer lineRenderer = simulation_boundary.AddComponent<LineRenderer>();
//         lineRenderer.positionCount = 5;

//         lineRenderer.SetPositions(new Vector3[]
//         {
//             new(0f, 0f, 0f),
//             new(border_width, 0f, 0f),
//             new(border_width, border_height, 0f),
//             new(0f, border_height, 0f),
//             new(0f, 0f, 0f),
//         });

//         // Optional: Set LineRenderer properties (material, color, width, etc.)
//         // lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
//         lineRenderer.startColor = Color.green;
//         lineRenderer.endColor = Color.green;
//         lineRenderer.startWidth = 0.1f;
//         lineRenderer.endWidth = 0.1f;
//     }
//     Vector2 Random_particle_position()
//     {
//         float x = UnityEngine.Random.Range(1, border_width-1);
//         float y = UnityEngine.Random.Range(1, border_height-1);
//         return new Vector2(x, y);
//     }
// }

// // [BurstCompile]
// // public struct Calculate_density_job : IJobParallelFor {

// //     public int Chunk_amount_multiplier;
// //     public int Max_influence_radius;
// //     public int flat_particle_chunks_tot_indexes;
// //     public float Smooth_Max;
// //     public float Smooth_derivative_koefficient;
// //     [ReadOnly] public NativeArray<Vector2> p_position;
// //     public NativeArray<float> density;
// //     public List<List<HashSet<int>>> particle_chunks;
// //     // [ReadOnly] public NativeArray<int> particle_chunks;
// //     public void Execute(int i) {

// //         density[i] = Influence(i);
// //     }

// //     float Influence(int particle_index)
// //     {
// //         int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
// //         int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

// //         float totInfluence = 0.0f;
// //         foreach (var other_particle_index in particle_chunks[in_chunk_x][in_chunk_y])
// //         {
// //             float distance = Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[other_particle_index].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[other_particle_index].y, 2));

// //             totInfluence += Smooth(distance);
// //         }

// //         return totInfluence;

// //         // float totInfluence = particle_chunks[in_chunk_x][in_chunk_y]
// //         //     .Sum(other_particle_index =>
// //         //         Smooth(Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[other_particle_index].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[other_particle_index].y, 2))));
// //         // return totInfluence;
// //     }

// //     float Smooth(float distance)
// //     {
// //         // Geogebra: https://www.geogebra.org/calculator/vwapudgf
// //         return Smooth_Max * Mathf.Exp(-Smooth_derivative_koefficient * distance);
// //     }
// // }

// // [BurstCompile]
// // public struct Particle_sort_job : IJobParallelFor {

// //     public int c_particles_num;
// //     public int c_Chunk_amount_multiplier;
// //     public int c_Max_influence_radius;
// //     public int c_Chunk_amount_x;
// //     public int c_Chunk_amount_y;
// //     public int c_Chunk_particle_capacity;
// //     public int c_Chunk_capacity;
// //     public float c_Chunk_amount_multiplier_squared;
// //     public float c_Look_ahead_factor;
// //     // public Vector2[] p_position;
// //     public NativeArray<Vector2> c_p_position;
// //     public NativeArray<Vector2> c_position;
// //     public NativeArray<Vector2> c_velocity;
// //     [ReadOnly] public NativeArray<int> c_particle_chunks;
// //     public void Execute(int i) {

// //             c_p_position[i] = c_position[i] + c_velocity[i] * c_Look_ahead_factor;

// //             int inChunkX = (int)(c_p_position[i].x * c_Chunk_amount_multiplier / c_Max_influence_radius);
// //             int inChunkY = (int)(c_p_position[i].y * c_Chunk_amount_multiplier / c_Max_influence_radius);
// //             float relative_position_x = c_p_position[i].x * c_Chunk_amount_multiplier / c_Max_influence_radius % 1;
// //             float relative_position_y = c_p_position[i].y * c_Chunk_amount_multiplier / c_Max_influence_radius % 1;

// //             for (int x = -c_Chunk_amount_multiplier; x <= c_Chunk_amount_multiplier; x++)
// //             {
// //                 for (int y = -c_Chunk_amount_multiplier; y <= c_Chunk_amount_multiplier; y++)
// //                 {
// //                     // current chunk
// //                     int cur_chunk_x = inChunkX + x;
// //                     int cur_chunk_y = inChunkY + y;

// //                     if (cur_chunk_x >= 0 && cur_chunk_x < c_Chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < c_Chunk_amount_y)
// //                     {
// //                         if (c_Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y-relative_position_y)*(y-relative_position_y) ||
// //                             c_Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y-relative_position_y)*(y-relative_position_y)||
// //                             c_Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y) ||
// //                             c_Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y))
// //                         {
// //                             // curx * Chunk_amount_x * Chunk_amount_y + cury * Chunk_amount_y + curz
// //                             int xy_index = cur_chunk_x * c_Chunk_amount_y * c_Chunk_particle_capacity + cur_chunk_y * c_Chunk_particle_capacity;
// //                             int z_index = 0;
// //                             while (c_particle_chunks[xy_index + z_index] == 0 && z_index < c_Chunk_capacity)
// //                             {
// //                                 z_index += 1;
// //                             }
// //                             // Add to list if space is available
// //                             if (z_index < c_Chunk_capacity)
// //                             {
// //                                 c_particle_chunks[xy_index + z_index] = i;
// //                             }
// //                         }
// //                     }
// //                 }
// //             }
// //     }
// // }