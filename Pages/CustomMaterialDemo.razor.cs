using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Velvet.Hosting.Web;
using Velvet.Core.Assets.Gltf;
using Velvet.Core.Scene;
using Velvet.Core.Math;
using Velvet.Core.Rendering;
using Velvet.Core.Rendering.Cameras;
using Velvet.Core.Rendering.Input;
using Velvet.Core.Rendering.Materials;
using Velvet.Graphics.WebGL;
using Velvet.Graphics.WebGL.Shaders;
using BlazorApp = Velvet.Hosting.Web.VelvetHost;

namespace Velvet_Site.Pages;

public partial class CustomMaterialDemo : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private ElementReference canvasRef;

    private BlazorApp? app;
    private Velvet.Core.Rendering.Materials.Material? customMaterial;
    private WebGLShader? shader;
    
    private Scene? scene;
    private Camera? camera;
    private float elapsedTime;

    // UI properties for material
    private float colorR = 0.6f;
    private float colorG = 0.3f;
    private float colorB = 0.9f;
    private float metallic = 0.5f;
    private float timeScale = 1.0f;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateDefaultAsync);
        shader = new WebGLShader(app.Program);
        customMaterial = new Velvet.Core.Rendering.Materials.Material(shader);
        customMaterial.Set("uBaseColor", new Vector3(colorR, colorG, colorB));
        customMaterial.Set("uAmbientStrength", 0.25f);

        // Setup camera
        camera = new Camera(
            position: new Vector3(0, 1.5f, 5f),
            target: new Vector3(0, 0, 0),
            up: Vector3.UnitY,
            fovYRadians: 50.0f * (MathF.PI / 180.0f),
            aspectRatio: 16.0f / 9.0f,
            nearPlane: 0.1f,
            farPlane: 100.0f);

        // Load Suzanne model
        var modelPath = "models/gltf/suzanne.glb";
        var bytes = await Http.GetByteArrayAsync(modelPath);
        var loadedScene = await GltfLoader.LoadScene(bytes);

        if (loadedScene == null || loadedScene.Roots.Count == 0)
        {
            Console.WriteLine($"Failed to load model: {modelPath}");
            return;
        }

        scene = new Scene(loadedScene.Roots);
        app.Add(scene);

        var bounds = scene.ComputeBounds();
        camera.Frame(bounds, frameMultiplier: 1.5f);

        app.Camera = camera;

        // Setup orbit controller
        var orbitController = new OrbitController(
            target: Vector3.Zero,
            yaw: 0.3f,
            pitch: 0.15f,
            distance: 5f,
            minDistance: 2f,
            maxDistance: 15f);
        app.SetController(orbitController);

        elapsedTime = 0;
        
        Console.WriteLine("[CustomMaterialDemo] Material API demo running on render path.");

        await app.StartAsync(
        onFrame: async (deltaTime) =>
        {
            if (scene == null || customMaterial == null || app == null) return;

            elapsedTime += deltaTime * timeScale;
            customMaterial.Set("uBaseColor", new Vector3(colorR, colorG, colorB));
            customMaterial.Set("uAmbientStrength", 0.15f + (metallic * 0.55f));
            app.Render(scene);
            await Task.CompletedTask;
        },
        beforeDrawMesh: mesh =>
        {
            customMaterial.Apply();
            if (shader is not null)
            {
                return shader.FlushAsync();
            }
            return Task.CompletedTask;
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
        }
    }
}
