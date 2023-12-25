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
    public GameObject particle_prefab;
    private Transform[] particles;
    private Renderer[] particles_renderer;
    private GameObject simulation_boundary;
    public int particles_num = 500;
    public int border_width = 26;
    public int border_height = 13;
    public float Gravity_force = 3f;
    public int Max_influence_radius = 2;
    public float Framerate_max = 1000;
    public float Program_speed = 2f;
    public float Target_density = 12f;
    public float Pressure_multiplier = 200f;
    public float Collision_damping_factor = 0.4f;
    public float Smooth_Max = 5f;
    public float Smooth_derivative_koefficient = 2.5f;
    public float Look_ahead_factor = 0.02f;
    public int Chunk_amount_multiplier = 2;
    public float border_thickness = 0.2f;
    public float Viscocity = 0.2f;
    public float Max_interaction_radius = 5;
    private int Chunk_amount_x = 0;
    private int Chunk_amount_y = 0;
    public int Chunk_capacity = 70;
    public float Interaction_power = 120;
    public int Particle_chunks_tot_num = 0;
    private int Chunk_amount_multiplier_squared = 0;
    public float Particle_visual_size = 1;
    public bool velocity_visuals;
    private Vector2 mouse_position;
    private bool left_mouse_button_down;
    private bool right_mouse_button_down;
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

        // Set camera position and size
        Camera.main.transform.position = new Vector3(border_width / 2, border_height / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(border_width * 0.75f, border_height * 1.5f);

        // initialize particle property arrays
        position = new Vector2[particles_num];
        velocity = new Vector2[particles_num];
        p_position = new Vector2[particles_num];
        density = new float[particles_num];
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
        }

        // Create particles
        for (int i = 0; i < particles_num; i++)
        {
            Create_particle(i);
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
        Vector3 xyz_mouse_pos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        mouse_position = new(xyz_mouse_pos.x, xyz_mouse_pos.y);
        left_mouse_button_down = Input.GetMouseButton(0);
        right_mouse_button_down = Input.GetMouseButton(1);

        Array.Fill(new_particle_chunks, -1);

        Sort_particles();

        Job_precalculate_densities();

        Job_physics();

        Render();

        // Print information to the console (you can modify this part based on your needs)
        Debug.Log($"Mouse Position (World_x): {mouse_position.x}");
        Debug.Log($"Mouse Position (World_y): {right_mouse_button_down}");
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

        // NativeList<JobHandle> job_handle_list = new NativeList<JobHandle>(Allocator.Temp);

        // // MAYBE - MIGHT NEED TO RESET THE VALUES EACH ITERATION --- PLACE SOMEWHERE ELSE TO AVOID FOR LOOP WITH ca 50000 elements each update iteration !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        // NativeArray<Vector2> n_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        // NativeArray<int> n_new_particle_chunks = new NativeArray<int>(Particle_chunks_tot_num, Allocator.TempJob);
        // NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);

        // n_new_particle_chunks.CopyFrom(new_particle_chunks);
        // n_position.CopyFrom(position);
        // n_new_particle_chunks.CopyFrom(new_particle_chunks);

        // for (int i = 0; i < particles_num; i++)
        // {
        //     Particle_sort_job particle_sort_job = new Particle_sort_job {
        //         i = i,
        //         Chunk_amount_multiplier = Chunk_amount_multiplier,
        //         Max_influence_radius = Max_influence_radius,
        //         Look_ahead_factor = Look_ahead_factor,
        //         Chunk_amount_multiplier_squared = Chunk_amount_multiplier_squared,
        //         Chunk_amount_y = Chunk_amount_y,
        //         Chunk_amount_x = Chunk_amount_x,
        //         Chunk_capacity = Chunk_capacity,
        //         position = n_position,
        //         velocity = n_velocity,
        //         new_particle_chunks = n_new_particle_chunks,
        //         p_position = n_p_position
        //     };

        //     JobHandle job_handle = particle_sort_job.Schedule();

        //     job_handle_list.Add(job_handle);
        // }

        // JobHandle.CompleteAll(job_handle_list);

        // for (int i = 0; i < particles_num; i++)
        // {
        //     p_position[i] = n_p_position[i];
        // }

        // for (int i = 0; i < Particle_chunks_tot_num; i++)
        // {
        //     new_particle_chunks[i] = n_new_particle_chunks[i];
        // }
 
        // n_position.Dispose();
        // n_velocity.Dispose();
        // n_new_particle_chunks.Dispose();
        // n_p_position.Dispose();
        // job_handle_list.Dispose();
    }

    float Influence(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        float tot_influence = 0;
        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        // List<int> chunk_list = new_particle_chunks.Skip(start_i).Take(Chunk_capacity).ToList();
        List<int> chunk_list = new_particle_chunks.Skip(start_i).TakeWhile(item => item != -1).ToList();

        foreach (var other_particle_index in chunk_list)
        {
                tot_influence += Smooth(Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[other_particle_index].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[other_particle_index].y, 2)));
        }

        return tot_influence;

    }

    void Job_precalculate_densities()
    {

        // MAYBE - MIGHT NEED TO RESET THE VALUES EACH ITERATION --- PLACE SOMEWHERE ELSE TO AVOID FOR LOOP WITH ca 50000 elements each update iteration !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<float> n_density = new NativeArray<float>(particles_num, Allocator.TempJob);
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

    void Job_physics()
    {

        // MAYBE - MIGHT NEED TO RESET THE VALUES EACH ITERATION --- PLACE SOMEWHERE ELSE TO AVOID FOR LOOP WITH ca 50000 elements each update iteration !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        NativeArray<Vector2> n_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<float> n_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<int> n_new_particle_chunks = new NativeArray<int>(Particle_chunks_tot_num, Allocator.TempJob);

        n_velocity.CopyFrom(velocity);
        n_position.CopyFrom(position);
        n_p_position.CopyFrom(p_position);
        n_density.CopyFrom(density);
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
            Interaction_power = Interaction_power,
            mouse_position = mouse_position,
            left_mouse_button_down = left_mouse_button_down,
            right_mouse_button_down = right_mouse_button_down,
            velocity = n_velocity,
            position = n_position,
            p_position = n_p_position,
            density = n_density,
            new_particle_chunks = n_new_particle_chunks
        };

        JobHandle jobhandle = calculate_physics_job.Schedule(particles_num, 64);

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
        n_new_particle_chunks.Dispose();

        // for (int i = 0; i < particles_num; i++)
        // {
        //     velocity[i].y -= Gravity_force * delta_time;

        //     Vector2 pressure_force = Pressure_force(i);
            
        //     velocity[i] += pressure_force * delta_time / density[i];

        //     velocity[i] = Apply_viscocity_to_velocity(i);

        //     position[i] += velocity[i] * delta_time;

        //     if (position[i].y > border_height - border_thickness)
        //     {
        //         velocity[i].y = -Mathf.Abs(velocity[i].y * Collision_damping_factor);
        //         position[i].y = border_height - border_thickness;
        //     }
        //     if (position[i].y < border_thickness)
        //     {
        //         velocity[i].y = +Mathf.Abs(velocity[i].y * Collision_damping_factor);
        //         position[i].y = border_thickness;
        //     }
        //     if (position[i].x > border_width - border_thickness)
        //     {
        //         velocity[i].x = -Mathf.Abs(velocity[i].x * Collision_damping_factor);
        //         position[i].x = border_width - border_thickness;
        //     }
        //     if (position[i].x < border_thickness)
        //     {
        //         velocity[i].x = +Mathf.Abs(velocity[i].x * Collision_damping_factor);
        //         position[i].x = border_thickness;
        //     }
        // }
    }

    void Create_particle(int particle_index)

    {
        GameObject particle = Instantiate(particle_prefab, Random_particle_position(), Quaternion.identity);
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
    
    Vector3 Random_particle_position()
    {
        int width_max = border_width * 50 - 1;
        int height_max = border_height * 50 - 1;
        float x = 0.02f * Random.Range(1, width_max);
        float y = 0.02f * Random.Range(1, height_max);
        return new Vector3(x, y, 0);
    }
}

// Does not work since all job worker threads write to the same array (new_particle_chunks)
[BurstCompile]
public struct Particle_sort_job : IJob {

    public int i;
    public int Chunk_amount_multiplier;
    public int Max_influence_radius;
    public float Look_ahead_factor;
    public float Chunk_amount_multiplier_squared;
    public int Chunk_amount_y;
    public int Chunk_amount_x;
    public int Chunk_capacity;

    [ReadOnly] public NativeArray<Vector2> position;
    [ReadOnly] public NativeArray<Vector2> velocity;
    public NativeArray<int> new_particle_chunks;
    public NativeArray<Vector2> p_position;
    public void Execute() {

        // Set predicted positions
        p_position[i] = position[i] + velocity[i] * Look_ahead_factor;

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
    public void Execute(int i) {

        density[i] = Influence(i);
    }

    float Influence(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

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
    public Vector2 mouse_position;
    public bool left_mouse_button_down;
    public bool right_mouse_button_down;
    public NativeArray<Vector2> velocity;
    public NativeArray<Vector2> position;
    [ReadOnly] public NativeArray<Vector2> p_position;
    [ReadOnly] public NativeArray<float> density;
    [ReadOnly] public NativeArray<int> new_particle_chunks;

    public void Execute(int i) {

        velocity[i] = new Vector2(velocity[i].x, velocity[i].y - Gravity_force * delta_time);

        Vector2 pressure_force = Pressure_force(i);
        Vector2 interaction_force = Interaction_force(i);

        // velocity[i] += interaction_force * delta_time;
            
        velocity[i] += (pressure_force + interaction_force) * delta_time / density[i];

        velocity[i] = Apply_viscocity_to_velocity(i);

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

            Vector2 pressure_gradient = new(0.0f, 0.0f);

            if (distance != 0)
            {
                pressure_gradient = relative_distance.normalized * abs_pressure_gradient;
            }
            else
            {
                Vector2 randomNormalizedVector = new(particle_index % 0.2842f, particle_index % 0.1827f);

                // * 0.1f to decrease pressure forces (for "corner particles")
                pressure_gradient = 0.05f * abs_pressure_gradient * randomNormalizedVector;
            }

            float avg_pressure = Shared_pressure(density[particle_index], density[other_particle_index]);

            // p(pos) = ∑_i (p_i * m / ρ_i * Smooth(pos - pos_i))
            pressure_force += avg_pressure * pressure_gradient / density[other_particle_index];

        }

        return -pressure_force;
    }

    Vector2 Apply_viscocity_to_velocity(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);
        int tot_mass = 0;

        Vector2 tot_force = new(0.0f, 0.0f);

        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        int end_i = start_i + Chunk_capacity;

        for (int i = start_i; i < end_i; i++)
        {
            int other_particle_index = new_particle_chunks[i];

            if (other_particle_index == -1)
            {
                continue;
            }

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

    Vector2 Interaction_force(int particle_index)
    {
        if (!left_mouse_button_down && !right_mouse_button_down){return new(0.0f,0.0f);}
        int left_right_direction = 0;
        if (left_mouse_button_down){left_right_direction = -1;}
        else{if (right_mouse_button_down){left_right_direction = 1;}}

        Vector2 relative_distance = position[particle_index] - mouse_position;

        float distance = relative_distance.magnitude;

        float abs_interaction_gradient = Interaction_influence(distance);

        if (distance > Max_interaction_radius){return new(0.0f, 0.0f);}
        else
        {
            if (distance == 0){return new(0.0f, 0.0f);}

            Vector2 interaction_gradient = relative_distance.normalized * abs_interaction_gradient;

            return interaction_gradient * Interaction_power * left_right_direction;
        }
    }

    float Interaction_influence(float distance)
    {
        float flatness = 1;
        if (distance == Max_interaction_radius){return Interaction_power;}
        // Geogebra: https://www.geogebra.org/calculator/uneb7zw9
        return Mathf.Sqrt((Max_interaction_radius - distance) / flatness);
    }

}

