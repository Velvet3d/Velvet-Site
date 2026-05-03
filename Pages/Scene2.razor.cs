using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Velvet.Hosting.Web;
using Velvet.Core.Animation;
using Velvet.Core.Assets.Gltf;
using Velvet.Core.Math;
using Velvet.Core.Rendering.Cameras;
using Velvet.Core.Rendering.Controllers;
using Velvet.Core.Rendering.Lighting;
using Velvet.Graphics.WebGL;

using BlazorApp = Velvet.Hosting.Web.BlazorVelvetHost;
using EngineScene = Velvet.Core.Scene.Scene;

namespace Velvet_Site.Pages;

public partial class Scene2 : ComponentBase, IAsyncDisposable
{
    // ==============================
    // Injected services
    // ==============================
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    // ==============================
    // Canvas
    // ==============================
    private ElementReference canvasRef;

    // ==============================
    // Engine state
    // ==============================
    private BlazorApp? app;
    private EngineScene? scene;
    private Camera? camera;
    private DirectionalLight? directional;
    private PointLight? point;
    private SpotLight? spot;
    private Animator? animator;
    private List<AnimationClip>? animationClips;
    private string? activeAnimationClipName;
    private bool isAnimationDropdownOpen;

    // ==============================
    // Code snippets (curated, not full dump)
    // ==============================
    private const string BlazorCode = """
// Blazor Example
@page "/scene2"

<canvas @ref="canvasRef"></canvas>

var app = await BlazorVelvetHost.CreateAsync(
    canvasRef,
    JS,
    ShaderProgram.CreateSkinnedAsync);

// Load model
var (scene, animations) =
    await GltfLoader.LoadFromUrl(Http, "models/Fox.glb");

var animator = new Animator(scene);

await app.StartAsync(dt =>
{
    animator.Update(dt);
    app.Render(scene);
});
""";

    private const string RazorCode = """
// Razor (SSR) Example
<canvas id="fox-canvas"></canvas>

<script>
    window.addEventListener("load", () => {
        window.Velvet.start("fox-canvas");
    });
</script>
""";


    // ==============================
    // Lifecycle
    // ==============================
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateSkinnedAsync);

        // Camera
        camera = new Camera(
            position: new Vector3(0, 20f, 2.6f),
            target: new Vector3(0, 0, 0),
            up: Vector3.UnitY,
            fovYRadians: 60f * (MathF.PI / 180f),
            aspectRatio: 16f / 9f,
            nearPlane: 0.1f,
            farPlane: 100f);

        // Lights
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

        spot = new SpotLight(
            position: new Vector3(0.0f, 2.2f, 2.2f),
            direction: new Vector3(0.0f, -1.0f, -1.0f),
            color: new Vector3(1, 1, 1),
            intensity: 0.0f,
            cutoff: 12f * (MathF.PI / 180f),
            outerCutoff: 20f * (MathF.PI / 180f),
            constant: 1.0f,
            linear: 0.09f,
            quadratic: 0.032f);

        var loadResult = await GltfLoader.LoadFromUrl(Http, "models/Fox.glb");

        scene = loadResult.Scene;
        animationClips = loadResult.Animations;

        animator = new Animator(scene);

        var initialClip = animationClips.FirstOrDefault(clip => clip.Name.Contains("Walk", StringComparison.OrdinalIgnoreCase))
            ?? animationClips.FirstOrDefault();

        if (initialClip is not null)
        {
            SetActiveClip(initialClip);
        }

        app.Add(scene);

        // Skybox
        await app.SetCubemapSkybox(
            "skybox/px.png",
            "skybox/nx.png",
            "skybox/py.png",
            "skybox/ny.png",
            "skybox/pz.png",
            "skybox/nz.png");

        // Frame camera
        var bounds = scene.ComputeBounds();
        camera.Frame(bounds, 1.3f);

        // Assign
        app.Camera = camera;
        app.DirectionalLight = directional;
        app.PointLight = point;
        app.SpotLight = spot;

        app.SetDirectionalEnabled(true);
        app.SetPointEnabled(true);

        // Orbit controller
        var orbitController = new OrbitController(
            target: bounds.Center,
            yaw: 0f,
            pitch: 0.3f,
            distance: (bounds.Center - camera.Position).Length,
            minDistance: bounds.Radius * 0.5f,
            maxDistance: bounds.Radius * 10f);

        app.SetController(orbitController);

        await app.StartAsync(OnFrameAsync);
        await InvokeAsync(StateHasChanged);
    }

    private bool HasAnimations => animationClips is { Count: > 0 };

    private string ActiveAnimationLabel => animationClips?.FirstOrDefault(clip => IsActiveClip(clip))?.Name ?? "Animations";

    private bool IsActiveClip(AnimationClip clip)
        => string.Equals(activeAnimationClipName, clip.Name, StringComparison.Ordinal);

    private void ToggleAnimationDropdown()
    {
        if (!HasAnimations)
            return;

        isAnimationDropdownOpen = !isAnimationDropdownOpen;
    }

    private void SetActiveClip(AnimationClip clip)
    {
        if (animator is null)
            return;

        if (string.Equals(activeAnimationClipName, clip.Name, StringComparison.Ordinal))
            return;

        if (!string.IsNullOrWhiteSpace(activeAnimationClipName))
        {
            animator.StopClip(activeAnimationClipName);
        }

        animator.PlayClip(clip);
        activeAnimationClipName = clip.Name;
    }

    private Task SelectAnimationClipAsync(AnimationClip clip)
    {
        SetActiveClip(clip);
        isAnimationDropdownOpen = false;
        return Task.CompletedTask;
    }

    private Task OnFrameAsync(float dt)
    {
        if (app is null || scene is null || animator is null)
            return Task.CompletedTask;

        animator.Update(dt);
        app.Render(scene);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
        }
    }
}