using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Velvet.Hosting.Web;
using Velvet.Core.Math;
using Velvet.Core.Particles;
using Velvet.Core.Rendering;
using Velvet.Core.Rendering.Cameras;
using Velvet.Core.Rendering.Cameras.Controllers;
using Velvet.Core.Rendering.Input;
using Velvet.Core.Rendering.Lighting;
using Velvet.Graphics.WebGL;
using BlazorApp = Velvet.Hosting.Web.VelvetHost;

namespace Velvet_Site.Pages;

public partial class Scene06 : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private ElementReference canvasRef;

    private BlazorApp? app;
    private Camera? camera;
    private DirectionalLight? directional;
    private PointLight? point;
    private SpotLight? spot;
    
    private ParticleSystem? smokeSystem;
    private ParticleSystem? sparkleSystem;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateDefaultAsync);

        // Set up camera positioned to see particle volume
        camera = new Camera(
            position: new Vector3(0, 1.5f, 3.5f),
            target: new Vector3(0, 1.0f, 0),
            up: Vector3.UnitY,
            fovYRadians: 60.0f * (System.MathF.PI / 180.0f),
            aspectRatio: 16.0f / 9.0f,
            nearPlane: 0.1f,
            farPlane: 100.0f);

        // Keep lights minimal for particle focus
        directional = new DirectionalLight(
            direction: new Vector3(0.2f, -1.0f, -0.1f),
            color: new Vector3(1, 1, 1),
            intensity: 0.3f); // Very low intensity

        point = new PointLight(
            position: new Vector3(2.0f, 2.0f, 2.0f),
            color: new Vector3(1.0f, 1.0f, 1.0f),
            intensity: 0.2f, // Very low intensity
            constant: 1.0f,
            linear: 0.14f,
            quadratic: 0.07f);

        // Keep spotlight disabled
        spot = new SpotLight(
            position: new Vector3(0.0f, 2.2f, 2.2f),
            direction: new Vector3(0.0f, -1.0f, -1.0f),
            color: new Vector3(1.0f, 1.0f, 1.0f),
            intensity: 0.0f, // Disabled
            cutoff: 12.0f * (System.MathF.PI / 180.0f),
            outerCutoff: 20.0f * (System.MathF.PI / 180.0f),
            constant: 1.0f,
            linear: 0.09f,
            quadratic: 0.032f);

        // Create main smoke particle system
        var smokeEmitter = new ParticleEmitter
        {
            Shape = ParticleEmitterShape.Box,
            Position = new Vector3(0, 0, 0),
            BoxExtents = new Vector3(0.5f, 0.1f, 0.5f), // Wide, flat emission area
            SpawnRate = 35f, // particles per second
            InitialVelocity = new Vector3(0, 0.7f, 0), // Base upward velocity
            VelocityMin = new Vector3(-0.15f, 0.0f, -0.15f), // Horizontal variation
            VelocityMax = new Vector3(0.15f, 0.3f, 0.15f) // Additional upward + horizontal
        };

        smokeSystem = new ParticleSystem(120, smokeEmitter)
        {
            ParticleLifetime = 2.5f,
            StartSize = 12f,
            EndSize = 4f,
            StartColor = new Vector4(0.9f, 0.9f, 0.9f, 0.7f), // Soft white/gray
            EndColor = new Vector4(0.9f, 0.9f, 0.9f, 0.0f),   // Fade to transparent
            BlendMode = ParticleBlendMode.Alpha
        };

        // Create secondary sparkle particle system
        var sparkleEmitter = new ParticleEmitter
        {
            Shape = ParticleEmitterShape.Box,
            Position = new Vector3(0, 0, 0),
            BoxExtents = new Vector3(0.5f, 0.2f, 0.5f), // Slightly taller emission area
            SpawnRate = 12f, // Lower spawn rate for subtlety
            InitialVelocity = new Vector3(0, 1.0f, 0), // Stronger upward velocity
            VelocityMin = new Vector3(-0.25f, 0.0f, -0.25f), // More horizontal variation
            VelocityMax = new Vector3(0.25f, 0.5f, 0.25f)
        };

        sparkleSystem = new ParticleSystem(35, sparkleEmitter)
        {
            ParticleLifetime = 2.0f,
            StartSize = 5f,
            EndSize = 1f,
            StartColor = new Vector4(1.0f, 1.0f, 1.0f, 0.9f), // Brighter white
            EndColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f),   // Fade to transparent
            BlendMode = ParticleBlendMode.Additive // Subtle glow effect
        };

        // Add particle systems to app
        app.Add(smokeSystem);
        app.Add(sparkleSystem);

        // Add gradient skybox for context
        var skybox = Skybox.CreateDefault();
        await app.SetSkybox(skybox);

        app.Camera = camera;
        app.DirectionalLight = directional;
        app.PointLight = point;
        app.SpotLight = spot;
        app.SetDirectionalEnabled(true);
        app.SetPointEnabled(true);

        // Set up orbit controls around the particle volume
        var orbitController = new OrbitController(
            target: new Vector3(0, 1.0f, 0), // Focus on particle center
            yaw: 0f,
            pitch: 0.2f,
            distance: 3.5f,
            minDistance: 1.5f,
            maxDistance: 8.0f);
        app.SetController(orbitController);

        await app.StartAsync(OnFrameAsync);
    }

    private async Task OnFrameAsync(float dt)
    {
        if (app is null) return;
        // Particles are automatically updated and rendered by VelvetHost
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
        }
    }
}