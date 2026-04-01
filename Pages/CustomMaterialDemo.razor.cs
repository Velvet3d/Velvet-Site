using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Velvet.Blazor;
using Velvet.Core.Assets.Gltf;
using Velvet.Core.Engine;
using Velvet.Core.Math;
using Velvet.Core.Rendering;
using Velvet.Core.Rendering.Materials;
using Velvet.Core.Rendering.Shaders;
using Velvet.WebGL;
using System.Collections.Generic;
using BlazorApp = Velvet.Blazor.VelvetApp;

namespace Velvet_Site.Pages;

public partial class CustomMaterialDemo : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private ElementReference canvasRef;

    private BlazorApp? app;
    private Velvet.Core.Rendering.Materials.Material? customMaterial;
    
    private Scene? scene;
    private Camera? camera;
    private OrbitController? orbitController;

    private bool isMouseDown;
    private int lastMouseX;
    private int lastMouseY;
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

        // Setup canvas resolution
        var rect = await JS.InvokeAsync<CanvasRect>("CanvasHelpers.getCanvasRect", canvasRef);
        var dpr = await JS.InvokeAsync<double>("CanvasHelpers.getDevicePixelRatio");
        var canvasWidth = (int)(rect.Width * dpr);
        var canvasHeight = (int)(rect.Height * dpr);
        await JS.InvokeVoidAsync("CanvasHelpers.setCanvasResolution", canvasRef, canvasWidth, canvasHeight);

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateDefaultAsync);
        var shaderAdapter = new ShaderProgramAdapter(app.Program);
        customMaterial = new Velvet.Core.Rendering.Materials.Material(shaderAdapter);
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
        orbitController = new OrbitController(
            target: Vector3.Zero,
            yaw: 0.3f,
            pitch: 0.15f,
            distance: 5f,
            minDistance: 2f,
            maxDistance: 15f);

        elapsedTime = 0;
        
        Console.WriteLine("[CustomMaterialDemo] Material API demo running on render path.");

        await app.StartAsync(
        onFrame: async (deltaTime) =>
        {
            if (camera == null || orbitController == null || scene == null || customMaterial == null) return;

            elapsedTime += deltaTime * timeScale;
            orbitController.UpdateCamera(camera);
            customMaterial.Set("uBaseColor", new Vector3(colorR, colorG, colorB));
            customMaterial.Set("uAmbientStrength", 0.15f + (metallic * 0.55f));
            app.Render(scene);
            await Task.CompletedTask;
        },
        beforeDrawMesh: mesh =>
        {
            customMaterial.Apply();
            return shaderAdapter.FlushAsync();
        });
    }

    private void OnCanvasMouseDown(MouseEventArgs e)
    {
        isMouseDown = true;
        lastMouseX = (int)e.ClientX;
        lastMouseY = (int)e.ClientY;
    }

    private void OnCanvasMouseMove(MouseEventArgs e)
    {
        if (!isMouseDown || orbitController == null) return;

        int dx = (int)e.ClientX - lastMouseX;
        int dy = (int)e.ClientY - lastMouseY;
        lastMouseX = (int)e.ClientX;
        lastMouseY = (int)e.ClientY;

        var yawDelta = -dx * 0.005f;
        var pitchDelta = dy * 0.005f;

        orbitController.ApplyYaw(yawDelta);
        orbitController.ApplyPitch(pitchDelta);
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
        if (orbitController == null) return;

        var zoomMultiplier = 1.0f + (float)e.DeltaY * 0.001f;
        orbitController.ApplyZoomMultiplier(zoomMultiplier);
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
        }
    }

    private sealed class ShaderProgramAdapter : IShader
    {
        private readonly ShaderProgram _program;
        private readonly List<Task> _pendingUniformWrites = new(capacity: 8);

        public ShaderProgramAdapter(ShaderProgram program)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
        }

        public void Use()
        {
            // ShaderProgram uniforms implicitly target this program.
        }

        public void SetFloat(string name, float value)
        {
            _pendingUniformWrites.Add(_program.SetUniform1fAsync(name, value));
        }

        public void SetVector3(string name, Vector3 value)
        {
            _pendingUniformWrites.Add(_program.SetUniform3fAsync(name, value.X, value.Y, value.Z));
        }

        public void SetMatrix4(string name, Matrix4 value)
        {
            _pendingUniformWrites.Add(_program.SetUniformMatrix4fvAsync(name, value.Data));
        }

        public async Task FlushAsync()
        {
            if (_pendingUniformWrites.Count == 0)
            {
                return;
            }

            var writes = _pendingUniformWrites.ToArray();
            _pendingUniformWrites.Clear();
            await Task.WhenAll(writes).ConfigureAwait(false);
        }
    }
}
