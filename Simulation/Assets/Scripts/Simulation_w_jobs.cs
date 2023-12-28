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
using UnityEngine.Jobs;
public class Simulation_w_jobs : MonoBehaviour
{
    [Header("Simulation settings")]
    public int particles_num = 1000;
    public float Gravity_force = 4f;
    public int Max_influence_radius = 1;
    public float Target_density = 50f;
    public float Pressure_multiplier = 200f;
    public float Near_density_multiplier = 20;
    public float Collision_damping_factor = 0.4f;
    public float Viscocity = 0.01f;

    [Header("Boundrary settings")]
    public int border_width = 40;
    public int border_height = 20;
    public int particle_spawner_dimensions = 15; // l x l
    public float border_thickness = 0.2f;
    public int Chunk_capacity = 120;

    [Header("Rendering settings")]
    public float Program_speed = 1.5f;
    public float Time_step = -1;
    public float Particle_visual_size = 1.5f;
    [Header("Interaction settings")]
    public float Max_interaction_radius = 7;
    public float Interaction_power = 130;

    [Header("Advanced settings")]
    private int Particle_sort_frequency = 1;
    public int Chunk_amount_multiplier = 2;

    // Not in use ---
    public float Smooth_Max = 5f;
    public float Smooth_derivative_koefficient = 2.5f;
    public float Max_velocity = 5;
    // Not in use ---
    public float Look_ahead_factor = 0.035f;

    [Header("Object reference(s)")]
    public GameObject particle_prefab;

    private Transform[] particles;
    private Renderer[] particles_renderer;
    private GameObject simulation_boundary;
    private int Particle_chunks_tot_num;
    private int Chunk_amount_x = 0;
    private int Chunk_amount_y = 0;
    private int Chunk_amount_multiplier_squared;
    public bool velocity_visuals;
    private Vector2 mouse_position;
    private bool left_mouse_button_down;
    private bool right_mouse_button_down;
    private Vector2[] position;
    private Vector2[] velocity;
    private Vector2[] last_velocity;
    // Predicted_velocities
    private Vector2[] p_position;
    private float[] density;
    private float[] near_density;
    private int[] new_particle_chunks;
    private float delta_time;

    void Start()
    {
        // Create border
        Create_simulation_boundary();

        // Set camera position and size
        Camera.main.transform.position = new Vector3(border_width / 2, border_height / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(border_width * 0.75f, border_height * 1.5f);

        // initialize particle property arrays
        position = new Vector2[particles_num];
        velocity = new Vector2[particles_num];
        last_velocity = new Vector2[particles_num];
        p_position = new Vector2[particles_num];
        density = new float[particles_num];
        near_density = new float[particles_num];
        particles = new Transform[particles_num];

        if (velocity_visuals)
        {
            particles_renderer = new Renderer[particles_num];
        }

        Chunk_amount_x = border_width * Chunk_amount_multiplier / Max_influence_radius;
        Chunk_amount_y = border_height * Chunk_amount_multiplier / Max_influence_radius;

        Chunk_amount_multiplier_squared = Chunk_amount_multiplier * Chunk_amount_multiplier;

        Particle_chunks_tot_num = Chunk_amount_x * Chunk_amount_y * Chunk_capacity;
        new_particle_chunks = new int[Particle_chunks_tot_num];

        for (int i = 0; i < particles_num; i++)
        {
            position[i] = new(0.0f, 0.0f);
            velocity[i] = new(0.0f, 0.0f);
            p_position[i] = new(0.0f, 0.0f);
            density[i] = 0.0f;
            near_density[i] = 0.0f;
        }

        // Create particles
        for (int i = 0; i < particles_num; i++)
        {
            Create_particle(i);
        }

        // Assign random positions
        for (int i = 0; i < particles_num; i++)
        {
            position[i] = Random_particle_position(i, particles_num);
        }
    }

    void Update()
    {
        if (Time_step == -1)
        {
            delta_time = Time.deltaTime * Program_speed;
        }
        else
        {
            delta_time = Time_step;
        }
        Vector3 xyz_mouse_pos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        mouse_position = new(xyz_mouse_pos.x, xyz_mouse_pos.y);
        left_mouse_button_down = Input.GetMouseButton(0);
        right_mouse_button_down = Input.GetMouseButton(1);

        if (Particle_sort_frequency == 1)
        {
            Array.Fill(new_particle_chunks, -1);
            Sort_particles();
            Particle_sort_frequency = 0;
        }
        Particle_sort_frequency += 1;

        Job_precalculate_densities();

        Job_physics();

        Render();
    }

    void Render()
    {
        if (velocity_visuals)
        {
            for (int i = 0; i < particles_num; i++)
            {
                if (float.IsNaN(position[i].x) || float.IsNaN(position[i].y)){continue;}
                particles[i].position = position[i];
                particles_renderer[i].material.color = SetColorByFunction(velocity[i].magnitude);               
            }
        }
        else
        {
            for (int i = 0; i < particles_num; i++)
            {
                if (float.IsNaN(position[i].x) || float.IsNaN(position[i].y)){continue;}
                particles[i].position = position[i];
            }
        }

        // NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);

        // // Copy positions from managed array to native array
        // for (int i = 0; i < particles_num; i++)
        // {
        //     n_position[i] = position[i];
        // }

        // Transform_particle_position_job transform_particle_position_job = new Transform_particle_position_job
        // {
        //     position = n_position
        // };

        // // Schedule the job with TransformAccessArray
        // TransformAccessArray transformAccessArray = new TransformAccessArray(particles);
        // JobHandle jobhandle = transform_particle_position_job.Schedule(transformAccessArray);

        // jobhandle.Complete();

        // n_position.Dispose();
        // transformAccessArray.Dispose();
    }

    void Sort_particles()
    {
        for(int i = 0; i < particles_num; i++)
        {
            // Set predicted positions
            p_position[i] = position[i] + velocity[i] * Look_ahead_factor;

            last_velocity[i] = velocity[i];

            int in_chunk_x = (int)Math.Floor(p_position[i].x * Chunk_amount_multiplier / Max_influence_radius);
            int in_chunk_y = (int)Math.Floor(p_position[i].y * Chunk_amount_multiplier / Max_influence_radius);
            float relative_position_x = p_position[i].x * Chunk_amount_multiplier / Max_influence_radius % 1;
            float relative_position_y = p_position[i].y * Chunk_amount_multiplier / Max_influence_radius % 1;

            for (int x = -Chunk_amount_multiplier; x <= Chunk_amount_multiplier; x++)
            {
                for (int y = -Chunk_amount_multiplier; y <= Chunk_amount_multiplier; y++)
                {
                    // current chunk
                    int cur_chunk_x = in_chunk_x + x;
                    int cur_chunk_y = in_chunk_y + y;

                    if (cur_chunk_x >= 0 && cur_chunk_x < Chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < Chunk_amount_y)
                    {
                        if (Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y-relative_position_y)*(y-relative_position_y) ||
                            Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y-relative_position_y)*(y-relative_position_y)||
                            Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y) ||
                            Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y))
                        {
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
                            else
                            {
                                Debug.Log("Chunk particle capacity reached");
                            }
                        }
                    }
                }
            }
        }
    }
    void Job_sort_particles()
    {

        NativeList<JobHandle> job_handle_list = new NativeList<JobHandle>(Allocator.Temp);

        // MAYBE - MIGHT NEED TO RESET THE VALUES EACH ITERATION --- PLACE SOMEWHERE ELSE TO AVOID FOR LOOP WITH ca 50000 elements each update iteration !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<int> n_new_particle_chunks = new NativeArray<int>(Particle_chunks_tot_num, Allocator.TempJob);
        NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);

        n_new_particle_chunks.CopyFrom(new_particle_chunks);
        n_position.CopyFrom(position);
        n_velocity.CopyFrom(velocity);
        n_p_position.CopyFrom(p_position);

        Particle_sort_job particle_sort_job = new Particle_sort_job {
            start_index = 0,
            end_index = particles_num,
            Chunk_amount_multiplier = Chunk_amount_multiplier,
            Max_influence_radius = Max_influence_radius,
            Look_ahead_factor = Look_ahead_factor,
            Chunk_amount_multiplier_squared = Chunk_amount_multiplier_squared,
            Chunk_amount_y = Chunk_amount_y,
            Chunk_amount_x = Chunk_amount_x,
            Chunk_capacity = Chunk_capacity,
            border_width = border_width,
            border_height = border_height,
            border_thickness = border_thickness,
            position = n_position,
            velocity = n_velocity,
            new_particle_chunks = n_new_particle_chunks,
            p_position = n_p_position
        };

        JobHandle job_handle = particle_sort_job.Schedule();


        job_handle.Complete();

        for (int i = 0; i < particles_num; i++)
        {
            p_position[i] = n_p_position[i];
        }

        for (int i = 0; i < Particle_chunks_tot_num; i++)
        {
            new_particle_chunks[i] = n_new_particle_chunks[i];
        }
 
        n_position.Dispose();
        n_velocity.Dispose();
        n_new_particle_chunks.Dispose();
        n_p_position.Dispose();
        job_handle_list.Dispose();
    }

    void Job_precalculate_densities()
    {

        // MAYBE - MIGHT NEED TO RESET THE VALUES EACH ITERATION --- PLACE SOMEWHERE ELSE TO AVOID FOR LOOP WITH ca 50000 elements each update iteration !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<float> n_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<float> n_near_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<int> n_new_particle_chunks = new NativeArray<int>(Particle_chunks_tot_num, Allocator.TempJob);

        n_new_particle_chunks.CopyFrom(new_particle_chunks);
        n_p_position.CopyFrom(p_position);
        n_position.CopyFrom(position);

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
            density = n_density,
            near_density = n_near_density
        };

        JobHandle jobhandle = calculate_density_job.Schedule(particles_num, 32);

        jobhandle.Complete();

        for (int i = 0; i < particles_num; i++)
        {
            density[i] = n_density[i];
            near_density[i] = n_near_density[i];
        }
 
        n_p_position.Dispose();
        n_position.Dispose();
        n_density.Dispose();
        n_near_density.Dispose();
        n_new_particle_chunks.Dispose();

    }

    void Job_physics()
    {

        // MAYBE - MIGHT NEED TO RESET THE VALUES EACH ITERATION --- PLACE SOMEWHERE ELSE TO AVOID FOR LOOP WITH ca 50000 elements each update iteration !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        NativeArray<Vector2> n_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_last_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<float> n_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<float> n_near_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<int> n_new_particle_chunks = new NativeArray<int>(Particle_chunks_tot_num, Allocator.TempJob);

        n_velocity.CopyFrom(velocity);
        n_position.CopyFrom(position);
        n_p_position.CopyFrom(p_position);
        n_density.CopyFrom(density);
        n_near_density.CopyFrom(near_density);
        n_last_velocity.CopyFrom(last_velocity);
        n_new_particle_chunks.CopyFrom(new_particle_chunks);

        Calculate_physics_job calculate_physics_job = new Calculate_physics_job {
            Gravity_force = Gravity_force,
            delta_time = delta_time,
            border_height = border_height,
            border_width = border_width,
            border_thickness = border_thickness,
            Collision_damping_factor = Collision_damping_factor,
            Smooth_Max = Smooth_Max,
            Smooth_derivative_koefficient = Smooth_derivative_koefficient,
            Target_density = Target_density,
            Pressure_multiplier = Pressure_multiplier,
            Chunk_amount_multiplier = Chunk_amount_multiplier,
            Max_influence_radius = Max_influence_radius,
            Viscocity = Viscocity,
            Max_interaction_radius = Max_interaction_radius,
            Chunk_amount_y = Chunk_amount_y,
            Chunk_capacity = Chunk_capacity,
            Near_density_multiplier = Near_density_multiplier,
            Interaction_power = Interaction_power,
            Max_velocity = Max_velocity,
            mouse_position = mouse_position,
            left_mouse_button_down = left_mouse_button_down,
            right_mouse_button_down = right_mouse_button_down,
            velocity = n_velocity,
            position = n_position,
            p_position = n_p_position,
            density = n_density,
            near_density = n_near_density,
            last_velocity = n_last_velocity,
            new_particle_chunks = n_new_particle_chunks
        };

        JobHandle jobhandle = calculate_physics_job.Schedule(particles_num, 32);

        jobhandle.Complete();

        for (int i = 0; i < particles_num; i++)
        {
            velocity[i] = n_velocity[i];
            position[i] = n_position[i];
        }
 
        n_velocity.Dispose();
        n_position.Dispose();
        n_p_position.Dispose();
        n_density.Dispose();
        n_near_density.Dispose();
        n_last_velocity.Dispose();
        n_new_particle_chunks.Dispose();
    }

    void Create_particle(int particle_index)

    {
        GameObject particle = Instantiate(particle_prefab, Random_particle_position(particle_index, particles_num), Quaternion.identity);
        particle.transform.localScale = new Vector3(0.2f * Particle_visual_size, 0.2f * Particle_visual_size, 0.2f * Particle_visual_size);
        particle.transform.parent = transform;
        particles[particle_index] = particle.transform;
        if (velocity_visuals)
        {
            particles_renderer[particle_index] = particle.GetComponent<Renderer>();
        }
    }

    Color SetColorByFunction(float gradient_value)
    {
        gradient_value = Mathf.Min(gradient_value, 3f);

        float normalizedValue = gradient_value / 3f;

        Color newColor = Color.Lerp(Color.blue, Color.red, normalizedValue);

        return newColor;
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
    
    Vector3 Random_particle_position(int particle_index, int max_index)
    {
        float x = (border_width - particle_spawner_dimensions) / 2 + Mathf.Floor(particle_index % Mathf.Sqrt(max_index)) * (particle_spawner_dimensions / Mathf.Sqrt(max_index));
        float y = (border_height - particle_spawner_dimensions) / 2 + Mathf.Floor(particle_index / Mathf.Sqrt(max_index)) * (particle_spawner_dimensions / Mathf.Sqrt(max_index));
        if (particle_spawner_dimensions > border_width || particle_spawner_dimensions > border_height)
        {
            throw new ArgumentException("Particle spawn dimensions larger than either border_width or border_height");
        }
        return new Vector3(x, y);
    }
}

// Does not work since all job worker threads write to the same array (new_particle_chunks)
[BurstCompile]
public struct Particle_sort_job : IJob {
    public int start_index;
    public int end_index;
    public int Chunk_amount_multiplier;
    public int Max_influence_radius;
    public float Look_ahead_factor;
    public float Chunk_amount_multiplier_squared;
    public int Chunk_amount_y;
    public int Chunk_amount_x;
    public int Chunk_capacity;
    public int border_width;
    public int border_height;
    public float border_thickness;

    [ReadOnly] public NativeArray<Vector2> position;
    [ReadOnly] public NativeArray<Vector2> velocity;
    public NativeArray<int> new_particle_chunks;
    public NativeArray<Vector2> p_position;
    public void Execute() {

        for (int i = start_index; i < end_index; i++)
        {
            
            // Set predicted positions
            p_position[i] = position[i] + velocity[i] * Look_ahead_factor;
            if (p_position[i].x > border_width - border_thickness){p_position[i] = new(border_width - border_thickness, p_position[i].y);}
            if (p_position[i].x < border_thickness){p_position[i] = new(border_thickness, p_position[i].y);}
            if (p_position[i].y > border_height - border_thickness){p_position[i] = new(p_position[i].x, border_height - border_thickness);}
            if (p_position[i].y < border_thickness){p_position[i] = new(p_position[i].x, border_thickness);}

            int inChunkX = (int)(position[i].x * Chunk_amount_multiplier / Max_influence_radius);
            int inChunkY = (int)(position[i].y * Chunk_amount_multiplier / Max_influence_radius);
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
    public NativeArray<float> near_density;
    public void Execute(int i) {

        (density[i], near_density[i]) = Influence(i);
    }

    (float, float) Influence(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        int end_i = start_i + Chunk_capacity;

        float totInfluence = 0.0f;
        float totNearInfluence = 0.0f;

        for (int i = start_i; i < end_i; i++)
        {
            if (new_particle_chunks[i] == -1){break;}

            float distance = Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[new_particle_chunks[i]].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[new_particle_chunks[i]].y, 2));

            totInfluence += Smooth(distance);
            totNearInfluence += Smooth_near(distance);
        }

        return (totInfluence, totNearInfluence);
    }

    float Smooth(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        if (distance > Max_influence_radius){return 0;}
        return 25 * Mathf.Pow(1 - distance/Max_influence_radius, 2);
    }

    float Smooth_near(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        if (distance > Max_influence_radius){return 0;}
        // return Smooth_Max * Mathf.Exp(2 * -Smooth_derivative_koefficient * distance);
        return 25 * Mathf.Pow(1 - distance/Max_influence_radius, 3);
    }
}

// Still runs in the main-thread due to the Unity.transform method
[BurstCompile]
public struct Transform_particle_position_job : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<Vector2> position;

    public void Execute(int i, TransformAccess transform)
    {
        // Update particle positions
        transform.position = new Vector3(position[i].x, position[i].y, 0);
    }
}

[BurstCompile]
public struct Calculate_physics_job : IJobParallelFor {

    public float Gravity_force;
    public float delta_time;
    public int border_height;
    public int border_width;
    public float border_thickness;
    public float Collision_damping_factor;
    public float Smooth_Max;
    public float Smooth_derivative_koefficient;
    public float Target_density;
    public float Pressure_multiplier;
    public int Chunk_amount_multiplier;
    public int Max_influence_radius;
    public float Viscocity;
    public float Max_interaction_radius;
    public int Chunk_amount_y;
    public int Chunk_capacity;
    public float Interaction_power;
    public float Max_velocity;
    public float Near_density_multiplier;
    public Vector2 mouse_position;
    public bool left_mouse_button_down;
    public bool right_mouse_button_down;
    public NativeArray<Vector2> velocity;
    public NativeArray<Vector2> position;
    [ReadOnly] public NativeArray<Vector2> p_position;
    [ReadOnly] public NativeArray<float> density;
    [ReadOnly] public NativeArray<float> near_density;
    [ReadOnly] public NativeArray<Vector2> last_velocity;
    [ReadOnly] public NativeArray<int> new_particle_chunks;

    public void Execute(int i) {

        velocity[i] = new Vector2(velocity[i].x, velocity[i].y - Gravity_force * delta_time);

        Vector2 pressure_force = Pressure_force(i);
        Vector2 interaction_force = Interaction_force(i);
        Vector2 viscocity_force = Viscocity_force(i);
        //                                                                                   density[i] or ~60???
        velocity[i] += (pressure_force + interaction_force + viscocity_force) * delta_time / 60;

        // velocity[i] = new(Mathf.Min(velocity[i].x, Max_velocity), Mathf.Min(velocity[i].y, Max_velocity));

        position[i] += velocity[i] * delta_time;

        if (position[i].y > border_height - border_thickness)
        {
            velocity[i] = new Vector2(velocity[i].x, -Mathf.Abs(velocity[i].y * Collision_damping_factor));
            position[i] = new Vector2(position[i].x, border_height - border_thickness);
        }
        if (position[i].y < border_thickness)
        {
            velocity[i] = new Vector2(velocity[i].x, +Mathf.Abs(velocity[i].y * Collision_damping_factor));
            position[i] = new Vector2(position[i].x, border_thickness);
        }
        if (position[i].x > border_width - border_thickness)
        {
            velocity[i] = new Vector2(-Mathf.Abs(velocity[i].x * Collision_damping_factor), velocity[i].y);
            position[i] = new Vector2(border_width - border_thickness, position[i].y);
        }
        if (position[i].x < border_thickness)
        {
            velocity[i] = new Vector2(+Mathf.Abs(velocity[i].x * Collision_damping_factor), velocity[i].y);
            position[i] = new Vector2(border_thickness, position[i].y);
        }
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

        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        int end_i = start_i + Chunk_capacity;

        for (int i = start_i; i < end_i; i++)
        {
            int other_particle_index = new_particle_chunks[i];

            if (other_particle_index == particle_index || other_particle_index == -1)
            {
                continue;
            }

            Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

            float distance = relative_distance.magnitude;

            float abs_pressure_gradient = Smooth_der(distance);
            float abs_near_pressure_gradient = Smooth_near_der(distance);

            Vector2 pressure_gradient = new(0.0f, 0.0f);
            Vector2 near_pressure_gradient = new(0.0f, 0.0f);

            if (distance == 0)
            {
                Vector2 randomNormalizedVector = new(particle_index % 0.2842f, particle_index % 0.1827f);

                pressure_gradient = 0.05f * abs_pressure_gradient * randomNormalizedVector;
                near_pressure_gradient = 0.05f * abs_near_pressure_gradient * randomNormalizedVector;
            }
            else
            {
                pressure_gradient = relative_distance.normalized * abs_pressure_gradient;
                near_pressure_gradient = relative_distance.normalized * abs_near_pressure_gradient;
            }

            float density_A = density[particle_index];
            float density_B = density[other_particle_index];

            float avg_pressure = Shared_pressure(density_A, density_B);

            float near_pressure_A = near_density[particle_index] * Near_density_multiplier;
            float near_pressure_B = near_density[other_particle_index] * Near_density_multiplier;

            float avg_near_pressure = (near_pressure_A + near_pressure_B) / 2;

            // p(pos) = ∑_i (p_i * m / ρ_avg * Smooth(pos - pos_i))
            pressure_force += (avg_pressure * pressure_gradient + avg_near_pressure * near_pressure_gradient) / ((density_A + density_B) / 2);

        }

        return -pressure_force;
    }

    Vector2 Viscocity_force(int particle_index)
    {
        Vector2 velocity_force = new(0.0f, 0.0f);

        int in_chunk_x = (int)Math.Floor(position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        int end_i = start_i + Chunk_capacity;

        for (int i = start_i; i < end_i; i++)
        {
            int other_particle_index = new_particle_chunks[i];

            if (other_particle_index == particle_index || other_particle_index == -1)
            {
                continue;
            }

            Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

            float distance = relative_distance.magnitude;

            float abs_viscocity_gradient = Smooth_viscocity_laplacian(distance);

            Vector2 viscocity_gradient = new(0.0f, 0.0f);

            if (distance == 0)
            {
                viscocity_gradient = new(0.0f, 0.0f);
            }
            else
            {
                viscocity_gradient = relative_distance.normalized * abs_viscocity_gradient;
            }

            Vector2 velocity_A = last_velocity[particle_index];
            Vector2 velocity_B = last_velocity[other_particle_index];

            float diff_velocity = velocity_A.magnitude - velocity_B.magnitude;

            float density_A = density[particle_index];
            float density_B = density[other_particle_index];

            // p(pos) = ∑_i (m * v_diff / ρ_avg * Smooth_viscocity(pos - pos_i))
            velocity_force += (diff_velocity * viscocity_gradient) / ((density_A + density_B) / 2);

        }
        // Viscocity_multiplier*
        return Viscocity * velocity_force;
    }

    Vector2 Interaction_force(int particle_index)
    {
        if (!left_mouse_button_down && !right_mouse_button_down){return new(0.0f,0.0f);}
        int left_right_direction = 0;
        if (left_mouse_button_down){left_right_direction = -1;}
        else{if (right_mouse_button_down){left_right_direction = 1;}}

        Vector2 relative_distance = p_position[particle_index] - mouse_position;

        float distance = relative_distance.magnitude;

        float abs_interaction_gradient = Interaction_influence(distance);

        if (distance > Max_interaction_radius){return new(0.0f, 0.0f);}
        else
        {
            if (distance == 0){return new(0.0f, 0.0f);}

            Vector2 interaction_gradient = relative_distance.normalized * abs_interaction_gradient ;

            return interaction_gradient * Interaction_power * left_right_direction;
        }
    }

    float Interaction_influence(float distance)
    {
        float flatness = 1;
        if (distance == Max_interaction_radius){return 0.01f;}
        // Geogebra: https://www.geogebra.org/calculator/uneb7zw9
        return Mathf.Pow((Max_interaction_radius - distance) / flatness, 0.7f);
    }

    float Smooth(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        if (distance > Max_influence_radius){return 0;}
        // return Smooth_Max * Mathf.Exp(-Smooth_derivative_koefficient * distance);
        return 25 * Mathf.Pow(1 - distance/Max_influence_radius, 2);
    }

    float Smooth_der(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        if (distance > Max_influence_radius){return 0;}
        // return -Smooth_Max * Smooth_derivative_koefficient * Mathf.Exp(-Smooth_derivative_koefficient * distance);
        return -50 * (1 - distance/Max_influence_radius) / Max_influence_radius;
    }

    float Smooth_near(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        if (distance > Max_influence_radius){return 0;}
        // return Smooth_Max * Mathf.Exp(2 * -Smooth_derivative_koefficient * distance);
        return 25 * Mathf.Pow(1 - distance/Max_influence_radius, 3);
    }

    float Smooth_near_der(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        if (distance > Max_influence_radius){return 0;}
        // return -Smooth_Max * Smooth_derivative_koefficient * Mathf.Exp(-Smooth_derivative_koefficient * distance);
        return -75 * Mathf.Pow(1 - distance/Max_influence_radius, 2) / Max_influence_radius;
    }

    float Smooth_viscocity_laplacian(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        if (distance > Max_influence_radius){return 0;}
        // return -Smooth_Max * Smooth_derivative_koefficient * Mathf.Exp(-Smooth_derivative_koefficient * distance);
        return 45 / (3.14f * Mathf.Pow(Max_influence_radius, 6)) * (Max_influence_radius - distance);
    }
}

