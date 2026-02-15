using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Velvet.Blazor;
using Velvet.Core.Assets.Gltf;
using Velvet.Core.Math;
using Velvet.Core.Rendering;
using Velvet.Core.Rendering.Lighting;
using Velvet.WebGL;
using BlazorApp = Velvet.Blazor.VelvetApp;
using EngineScene = Velvet.Core.Engine.Scene;

namespace Velvet_Site.Pages;

public partial class Scene04 : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private ElementReference canvasRef;

    private BlazorApp? app;
    private EngineScene? scene;
    private Camera? camera;
    private OrbitController? orbitController;
    private DirectionalLight? directional;
    private PointLight? point;
    private SpotLight? spot;

    private bool isMouseDown;
    private int lastMouseX;
    private int lastMouseY;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // Fix canvas pixelation by setting correct resolution
        var rect = await JS.InvokeAsync<CanvasRect>("CanvasHelpers.getCanvasRect", canvasRef);
        var dpr = await JS.InvokeAsync<double>("CanvasHelpers.getDevicePixelRatio");
        var canvasWidth = (int)(rect.Width * dpr);
        var canvasHeight = (int)(rect.Height * dpr);
        await JS.InvokeVoidAsync("CanvasHelpers.setCanvasResolution", canvasRef, canvasWidth, canvasHeight);

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateDefaultAsync);

        camera = new Camera(
            position: new Vector3(0, 0.6f, 2.2f),
            target: new Vector3(0, 0.2f, 0),
            up: Vector3.UnitY,
            fovYRadians: 60.0f * (System.MathF.PI / 180.0f),
            aspectRatio: 16.0f / 9.0f,
            nearPlane: 0.1f,
            farPlane: 100.0f);

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

        var bytes = await Http.GetByteArrayAsync("models/gltf/DamagedHelmet/glTF-Embedded/DamagedHelmet.gltf");
        scene = await GltfLoader.LoadScene(bytes, "models/gltf/DamagedHelmet/glTF-Embedded");

        app.Add(scene);

        var bounds = scene.ComputeBounds();
        camera.Frame(bounds, frameMultiplier: 1.6f);

        app.Camera = camera;
        app.DirectionalLight = directional;
        app.PointLight = point;
        app.SpotLight = spot;
        app.SetDirectionalEnabled(true);
        app.SetPointEnabled(true);

        orbitController = new OrbitController(
            target: bounds.Center,
            yaw: 0f,
            pitch: 0.2f,
            distance: (bounds.Center - camera.Position).Length,
            minDistance: bounds.Radius * 0.5f,
            maxDistance: bounds.Radius * 10f);

        await app.StartAsync(OnFrameAsync);
    }

    private void OnCanvasMouseDown(MouseEventArgs e)
    {
        isMouseDown = true;
        lastMouseX = (int)e.ClientX;
        lastMouseY = (int)e.ClientY;
    }

    private void OnCanvasMouseMove(MouseEventArgs e)
    {
        if (!isMouseDown || orbitController is null) return;

        var deltaX = (int)e.ClientX - lastMouseX;
        var deltaY = (int)e.ClientY - lastMouseY;

        var yawDelta = -deltaX * 0.005f;
        var pitchDelta = deltaY * 0.005f;

        orbitController.ApplyYaw(yawDelta);
        orbitController.ApplyPitch(pitchDelta);

        lastMouseX = (int)e.ClientX;
        lastMouseY = (int)e.ClientY;
    }

    private void OnCanvasMouseUp(MouseEventArgs e)
    {
        isMouseDown = false;
    }

    private void OnCanvasMouseLeave(MouseEventArgs e)
    {
        isMouseDown = false;
    }

    private void OnCanvasWheel(WheelEventArgs e)
    {
        if (orbitController is null) return;

        var zoomMultiplier = 1.0f + (float)e.DeltaY * 0.001f;
        orbitController.ApplyZoomMultiplier(zoomMultiplier);
    }

    private async Task OnFrameAsync(float dt)
    {
        if (app is null || camera is null || orbitController is null || scene is null) return;

        orbitController.UpdateCamera(camera);
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
