using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Velvet.Blazor;
using Velvet.Core.Animation;
using Velvet.Core.Assets.Gltf;
using Velvet.Core.Math;
using Velvet.Core.Rendering;
using Velvet.Core.Rendering.Input;
using Velvet.Core.Rendering.Lighting;
using Velvet.WebGL;
using BlazorApp = Velvet.Blazor.VelvetApp;
using EngineScene = Velvet.Core.Engine.Scene;

namespace Velvet_Site.Pages;

public partial class Scene2 : ComponentBase, IAsyncDisposable
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
    private Animator? animator;
    private List<AnimationClip>? animationClips;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateSkinnedAsync);

        camera = new Camera(
            position: new Vector3(0, 20f, 2.6f),
            target: new Vector3(0, 0, 0),
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

        var bytes = await Http.GetByteArrayAsync("models/Fox.glb");
        var loadResult = await GltfLoader.LoadSceneWithAnimations(bytes, "models");
        scene = loadResult.Scene;
        animationClips = loadResult.Animations;

        animator = new Animator(scene);
        if (animationClips.Count > 0)
        {
            animator.PlayClip(animationClips[1]);
        }

        app.Add(scene);

        // Add cubemap skybox
        await app.SetCubemapSkybox(
            "skybox/px.png",
            "skybox/nx.png",
            "skybox/py.png",
            "skybox/ny.png",
            "skybox/pz.png",
            "skybox/nz.png");

        var bounds = scene.ComputeBounds();
        camera.Frame(bounds, frameMultiplier: 1.3f);

        app.Camera = camera;
        app.DirectionalLight = directional;
        app.PointLight = point;
        app.SpotLight = spot;
        app.SetDirectionalEnabled(true);
        app.SetPointEnabled(true);

        var orbitController = new OrbitController(
            target: bounds.Center,
            yaw: 0f,
            pitch: 0.3f,
            distance: (bounds.Center - camera.Position).Length,
            minDistance: bounds.Radius * 0.5f,
            maxDistance: bounds.Radius * 10f);
        app.SetController(orbitController);

        await app.StartAsync(OnFrameAsync);
    }

    private async Task OnFrameAsync(float dt)
    {
        if (app is null) return;

        if (scene is not null && animator is not null)
        {
            animator.Update(dt);
            app.Render(scene);
        }

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
