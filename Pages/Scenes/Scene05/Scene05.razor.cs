using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Velvet.Hosting.Web;
using Velvet.Core.Assets.Gltf;
using Velvet.Core.Math;
using Velvet.Core.Rendering.Cameras;
using Velvet.Core.Rendering.Controllers;
using Velvet.Core.Rendering.Input;
using Velvet.Core.Rendering.Lighting;
using Velvet.Graphics.WebGL;
using Velvet.Core.Rendering.Environment;
using BlazorApp = Velvet.Hosting.Web.BlazorVelvetHost;
using EngineScene = Velvet.Core.Scene.Scene;
namespace Velvet_Site.Pages.Scenes;






public partial class Scene05 : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private ElementReference canvasRef;

    private BlazorApp? app;
    private EngineScene? scene;
    private Camera? camera;
    private DirectionalLight? directional;
    private PointLight? point;
    private SpotLight? spot;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateDefaultAsync);
     
       camera = new Camera(
            position: new Vector3(0, 20f, 2.6f),
            target: new Vector3(0, 0, 0),
            up: Vector3.UnitY,
            fovYRadians: 60f * (MathF.PI / 180f),
            aspectRatio: 16f / 9f,
            nearPlane: 0.1f,
            farPlane: 100f);


        directional = new DirectionalLight(
            direction: new Vector3(0.4f, -1.0f, -0.25f),
            color: new Vector3(1, 1, 1),
            intensity: 1.1f);

        point = new PointLight(
            position: new Vector3(1.5f, 1.1f, 1.6f),
            color: new Vector3(1.0f, 0.95f, 0.9f),
            intensity: 2.0f,
            constant: 1.0f,
            linear: 0.14f,
            quadratic: 0.07f);

        // Keep spotlight uniforms valid; disable by setting intensity to 0.
        spot = new SpotLight(
            position: new Vector3(0.0f, 2.2f, 2.2f),
            direction: new Vector3(0.0f, -1.0f, -1.0f),
            color: new Vector3(1.0f, 1.0f, 1.0f),
            intensity: 0.0f,
            cutoff: 12.0f * (System.MathF.PI / 180.0f),
            outerCutoff: 20.0f * (System.MathF.PI / 180.0f),
            constant: 1.0f,
            linear: 0.09f,
            quadratic: 0.032f);

        var bytes = await Http.GetByteArrayAsync("models/gltf/suzanne.glb");
        scene = await GltfLoader.LoadScene(bytes);
    //    var loadResult = await GltfLoader.LoadFromUrl(Http, "models/gltf/suzanne.glb");
    //    scene = loadResult.Scene;

    //     // // Create a beautiful sunset-themed skybox with warm orange horizon and purple zenith
    //     // // Other options: SkyboxPresets.BlueSky, SkyboxPresets.Dawn, SkyboxPresets.Twilight, 
    //     // // SkyboxPresets.Overcast, SkyboxPresets.Vibrant, SkyboxPresets.Forest
    //     var skybox = Skybox.CreateWithGradient(
    //         horizonColor: new Vector3(1.0f, 0.7f, 0.3f),  // Warm orange at horizon
    //         zenithColor: new Vector3(0.4f, 0.2f, 0.8f));   // Purple at zenith
        
    //     await app.SetSkybox(skybox);

        app.Add(scene);



         var bounds = scene.ComputeBounds();
        // camera.Frame(bounds, frameMultiplier: 1.6f);
        camera.Frame(bounds, 1.3f);

        app.Camera = camera;
        app.DirectionalLight = directional;
        app.PointLight = point;
        app.SpotLight = spot;
        app.SetDirectionalEnabled(true);
        app.SetPointEnabled(true);

        var orbitController = new OrbitController(
            target: bounds.Center,
            yaw: 0f,
            pitch: 0.2f,
            distance: (bounds.Center - camera.Position).Length,
            minDistance: bounds.Radius * 0.5f,
            maxDistance: bounds.Radius * 3f);
        app.SetController(orbitController);

        await app.StartAsync(OnFrameAsync);
    }

    private async Task OnFrameAsync(float dt)
    {
        if (app is null || scene is null) return;
        app.Render(scene);

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
