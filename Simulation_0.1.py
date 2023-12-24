import torch
import pygame
import sys
import random
import numpy as np
import math
import time

import multiprocessing
import cProfile

# --- Todo list ---

# 1. Optimise code:
#   - 15 FPS w/o Devtools
#   - 1 FPS w Devtools

# 2. Fix increase in system energy

# SETTINGS COPY with 15 / 1 FPS:
# GravityForce = 0
# Circle_radii = 5
# Max_influence_radius = 250
# Framerate_max = 1000
# Program_speed = 2
# Particle_amount = 200
# Target_density = 100
# Pressure_multiplier = 5
# Wall_collision_damping_factor = 0.8
# Smooth_Max = 150
# Smooth_derivative_koefficient = 0.02

# # --- Pygame settings ---
# w, h = 1000, 500
# resolution_x, resolution_y = 100, 50
# Dev_tools = False

# --- Simulation Parameters ---
GravityForce = -20
Circle_radii = 5
Max_influence_radius = 250
Framerate_max = 1000
Program_speed = 2
Particle_amount = 200
Target_density = 100
Pressure_multiplier = 5
Wall_collision_damping_factor = 0.8
Smooth_Max = 150
Smooth_derivative_koefficient = 0.02

# --- Pygame settings ---
w, h = 1000, 500
resolution_x, resolution_y = 100, 50
Dev_tools = False

# --- Automatic ---
sign = lambda a: 1 if a>0 else -1 if a<0 else 0
Chunks_amount_x = w / Max_influence_radius
Chunks_amount_y = h / Max_influence_radius
if Chunks_amount_x % 1 != 0 or Chunks_amount_y % 1 != 0:
    raise TypeError("⚠ Chunks_amount not even")
scale_x, scale_y = w / resolution_x, h / resolution_y
class world_class:
    def __init__(self, width, height):
        self.width = width
        self.height = height
        self.resolution_pixels = np.zeros((resolution_x, resolution_y))
world = world_class(w, h)
particles = []
particle_chunks = np.empty((int(Chunks_amount_x), int(Chunks_amount_y)), dtype=object)
for i in range(int(Chunks_amount_x)):
    for j in range(int(Chunks_amount_y)):
        particle_chunks[i, j] = set()

# --- Pygame setup ---
pygame.init()
screen = pygame.display.set_mode((world.width, world.height))
surface = pygame.Surface((world.width, world.height))
clock = pygame.time.Clock()
pygame.display.set_caption("Liquid Simulation")

# Create particles
p_index = -1
class particle_class:
    def __init__(self, x, y, p_index):
        self.x = x
        self.y = y
        self.velocity_y = 0
        self.velocity_x = 0
        self.mass = 1
        
        self.index = p_index
        self.display_color = 255
for x in range(Particle_amount):
    p_index += 1
    particles.append(particle_class(random.randint(50, world.width - 50), random.randint(50, world.height - 50), p_index))


def Render(particles, Framerate_max, resolution_pixels):
    
    
    # Get density for every screen pixel
    if Dev_tools:
        for x in range(resolution_x):
            for y in range(resolution_y):
                resolution_pixels[x, y] = Influence_pixels(x * scale_x, y * scale_y) / 3
    
    # Visualise partciel density
    for particle in particles:
        particle.display_color = Influence(particle)
    
    # Visualise active chunks
    if Dev_tools:
        chunks_amount_x, chunks_amount_y = particle_chunks.shape
        for x in range(chunks_amount_x):
            for y in range(chunks_amount_y):
                if len(particle_chunks[x, y]) > 0:
                    resolution_pixels[x, y] = 255
                else:
                    resolution_pixels[x, y] = 50
                
                
    # Rectangle method - Draw density pixels
    if Dev_tools:
        for x in range(resolution_x):
            for y in range(resolution_y):
                pygame.draw.rect(screen, (0, resolution_pixels[x, y], 0), (x * scale_x, y * scale_y, scale_x, scale_y))
    
   # Draw particles
    for particle in particles:
        pygame.draw.circle(screen, (0, 0, min(math.floor(particle.display_color), 255)), (particle.x, particle.y), Circle_radii)
    
    # FPS counter
    fps = int(clock.get_fps())
    fps_text = font.render(f"FPS: {fps}", True, (0, 150, 0))
    screen.blit(fps_text, (10, 10))
    
    # Update the display
    pygame.display.flip()
    clock.tick(Framerate_max)


def Sort_particles(particles):
    
    # Chunks_amount_x
    # Sort
    for particle in particles:
        in_chunk_x = math.floor(particle.x / Max_influence_radius)
        in_chunk_y = math.floor(particle.y / Max_influence_radius)
        relative_position_x = (particle.x / Max_influence_radius) % 1
        relative_position_y = (particle.y / Max_influence_radius) % 1
        chunks_amount_x, chunks_amount_y = particle_chunks.shape
        
        for x in range(-1, 2):
            for y in range(-1, 2):
                if (-1 < (in_chunk_x + x) < chunks_amount_x) and (-1 < (in_chunk_y + y) < chunks_amount_y):
                    particle_chunks[in_chunk_x + x, in_chunk_y + y].add(particle)
        
        
        # "Min_distance" May have unintended consequences if changed from sqrt(2) = ca 1.41
        Min_distance = 1.41
        # lower-left, lower-right, upper-left, upper-right. Only ONE of these cases are possible, thus elif
        if (math.sqrt(relative_position_x**2 + relative_position_y**2) > Min_distance) and (0 < in_chunk_x) and (0 < in_chunk_y):
            particle_chunks[in_chunk_x - 1, in_chunk_y - 1].remove(particle)
        if (math.sqrt((1 - relative_position_x)**2 + relative_position_y**2) > Min_distance) and (in_chunk_x < chunks_amount_x - 1) and (0 < in_chunk_y):
            particle_chunks[in_chunk_x + 1, in_chunk_y - 1].remove(particle)
        if (math.sqrt(relative_position_x**2 + (1 - relative_position_y)**2) > Min_distance) and (0 < in_chunk_x) and (in_chunk_y < chunks_amount_y - 1):
            particle_chunks[in_chunk_x - 1, in_chunk_y + 1].remove(particle)
        if (math.sqrt((1 - relative_position_x)**2 + (1 - relative_position_y)**2) > Min_distance) and (in_chunk_x < chunks_amount_x - 1) and (in_chunk_y < chunks_amount_y - 1):
            particle_chunks[in_chunk_x + 1, in_chunk_y + 1].remove(particle)
    
    return


# Finds the "influence" value at a specific (particle's) location
def Influence(particle):
    
    in_chunk_x = math.floor(particle.x / Max_influence_radius)
    in_chunk_y = math.floor(particle.y / Max_influence_radius)
    
    tot_influence = 0
    for other_particle in particle_chunks[in_chunk_x, in_chunk_y]:
        
        if other_particle == particle:
            continue
        
        distance = math.sqrt((particle.x - other_particle.x)**2 + (particle.y - other_particle.y)**2)
        tot_influence += Smooth(distance) * other_particle.mass
    
    # 255 is max for hex color input
    return tot_influence if type(tot_influence) != int else 0

# Finds the "influence" value at a specific (particle's) location and calcultes pressure
def Pressure_gradient(particle):
    
    in_chunk_x = math.floor(particle.x / Max_influence_radius)
    in_chunk_y = math.floor(particle.y / Max_influence_radius)
    
    tot_gradient = np.array([0.0, 0.0])
    for other_particle in particle_chunks[in_chunk_x, in_chunk_y]:
        
        if other_particle == particle:
            continue
        
        relative_distance_x = other_particle.x - particle.x
        relative_distance_y = other_particle.y - particle.y
        
        distance = math.sqrt(relative_distance_x**2 + relative_distance_y**2)
        
        abs_influence_gradient = Smooth_gradient(distance) * other_particle.mass
        
        if distance == 0:
            continue
        #                            ("(x/y)"_distance_vector_value)
        influence_gradient_x = (relative_distance_x / distance) * abs_influence_gradient
        influence_gradient_y = (relative_distance_y / distance) * abs_influence_gradient
        
        tot_gradient[0] += influence_gradient_x
        tot_gradient[1] += influence_gradient_y
    
    tot_influence_x, tot_influence_y = tot_gradient
    
    return tot_influence_x, tot_influence_y

def Pressure_force(particle):
    
    density = Influence(particle)
    pressure_gradient_x, pressure_gradient_y = Pressure_gradient(particle)
    
    # p(pos) = ∑_i (p_i * m / ρ_i * Smooth(pos - pos_i))
    pressure_force_x = Density_to_pressure(density) * pressure_gradient_x * particle.mass / density
    pressure_force_y = Density_to_pressure(density) * pressure_gradient_y * particle.mass / density
    
    return pressure_force_x, pressure_force_y
  
# Finds the "influence" value at a specific (pixel's) location
def Influence_pixels(x, y):
        
    tot_influence = 0
    for particle in particles:
        distance = math.sqrt((particle.x - x)**2 + (particle.y - y)**2)
        if distance > Max_influence_radius:
            continue
        tot_influence += Smooth(distance)
    
    return 255 if tot_influence > 255 else tot_influence

# (Smoothing function) - return value increases as the distance (between two particles) decreases
def Smooth(distance):
    
    # Geogebra: https://www.geogebra.org/calculator/vwapudgf
    return Smooth_Max * math.exp(-abs(Smooth_derivative_koefficient * distance))

def Density_to_pressure(density):
    density_error = density - Target_density
    pressure = density_error * Pressure_multiplier
    return pressure

def Smooth_gradient(distance):
    
    # Geogebra: https://www.geogebra.org/calculator/vwapudgf
    return -Smooth_Max * Smooth_derivative_koefficient * math.exp(-abs(Smooth_derivative_koefficient * distance))

def Physics(particles, delta_time):
    
    for particle in particles:
        
        # Apply gravitional forces
        # particle.velocity_y += GravityForce * delta_time
        
        # Add Pressure_force to velocity_"(x/y)"
        pressure_force_x, pressure_force_y = Pressure_force(particle)
        
        # MODFIFIED - VELOCITY SET DIRECTLY TO AVOID INF. ENERGY !!!!!!!!!!!!!!!!!!!!!!!!
        particle.velocity_x = pressure_force_x
        particle.velocity_y = pressure_force_y - GravityForce
        
        # Increment x,y by velocity_"(x/y)" with respect to delta_time
        particle.y += particle.velocity_y * delta_time
        particle.x += particle.velocity_x * delta_time
        
        # Solid screen borders
        if particle.y > world.height - Circle_radii:
            particle.velocity_y = -abs(particle.velocity_y * Wall_collision_damping_factor)
            particle.y = world.height - Circle_radii
        
        elif particle.y < Circle_radii:
            particle.velocity_y = +abs(particle.velocity_y * Wall_collision_damping_factor)
            particle.y = Circle_radii

        if particle.x > world.width - Circle_radii:
            particle.velocity_x = -abs(particle.velocity_x * Wall_collision_damping_factor)
            particle.x = world.width - Circle_radii

        elif particle.x < Circle_radii:
            particle.velocity_x = +abs(particle.velocity_x * Wall_collision_damping_factor)
            particle.x = Circle_radii
        



# Main game loop
running = True
start_time = pygame.time.get_ticks()
font = pygame.font.Font(None, 36)
clock = pygame.time.Clock()
while running:
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False
    # Clear the screen
    screen.fill((255, 255, 255))
    # Set ΔTime
    current_time = pygame.time.get_ticks()
    delta_time = (current_time - start_time) * Program_speed / 1000.0
    start_time = current_time
    
    # Reset particle_chunks tuple and populate with empty sets
    particle_chunks = np.empty((int(Chunks_amount_x), int(Chunks_amount_y)), dtype=object)
    for i in range(int(Chunks_amount_x)):
        for j in range(int(Chunks_amount_y)):
            particle_chunks[i, j] = set()
            
    Sort_particles(particles)
    
    Physics(particles, delta_time)
    
    Render(particles, Framerate_max, world.resolution_pixels)

    
# Quit Pygame
pygame.quit()
sys.exit()
