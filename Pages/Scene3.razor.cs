using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Velvet.Hosting.Web;
using Velvet.Core.Scene;
using Velvet.Core.Geometry;
using Velvet.Core.Math;
using Velvet.Core.Rendering;
using Velvet.Core.Rendering.Cameras;
using Velvet.Core.Rendering.Cameras.Controllers;
using Velvet.Core.Rendering.Input;
using Velvet.Core.Rendering.Lighting;
using Velvet.Core.Rendering.Meshes;
using Velvet.Graphics.WebGL;
using BlazorApp = Velvet.Hosting.Web.VelvetHost;

namespace Velvet_Site.Pages;

public partial class Scene3 : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ElementReference canvasRef;

    private BlazorApp? app;
    private Scene? scene;
    private Camera? camera;
    private DirectionalLight? directional;
    private PointLight? point;
    private SpotLight? spot;

    // Debug UI properties
    private bool directionalEnabled = true;
    private float directionalIntensity = 1.1f;
    private string directionalColor = "#ffffff";

    private bool pointEnabled = true;
    private float pointIntensity = 2.0f;
    private string pointColor = "#fff2e6";
    private float pointPosY = 1.5f;

    private bool spotEnabled = false;
    private float spotIntensity = 5.0f;
    private string spotColor = "#ffffff";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateDefaultAsync);

        camera = new Camera(
            position: new Vector3(0, 2f, 5f),
            target: new Vector3(0, 0, 0),
            up: Vector3.UnitY,
            fovYRadians: 60.0f * (System.MathF.PI / 180.0f),
            aspectRatio: 16.0f / 9.0f,
            nearPlane: 0.1f,
            farPlane: 100.0f);

        directional = new DirectionalLight(
            direction: new Vector3(0.4f, -1.0f, -0.25f),
            color: new Vector3(1, 1, 1),
            intensity: directionalIntensity);

        point = new PointLight(
            position: new Vector3(2f, pointPosY, 2f),
            color: HexToVector3(pointColor),
            intensity: pointIntensity,
            constant: 1.0f,
            linear: 0.14f,
            quadratic: 0.07f);

        spot = new SpotLight(
            position: new Vector3(0.0f, 3.0f, 2.0f),
            direction: new Vector3(0.0f, -1.0f, -0.5f),
            color: new Vector3(1.0f, 1.0f, 1.0f),
            intensity: 0.0f,
            cutoff: 12.0f * (System.MathF.PI / 180.0f),
            outerCutoff: 20.0f * (System.MathF.PI / 180.0f),
            constant: 1.0f,
            linear: 0.09f,
            quadratic: 0.032f);

        // Create cube node
        var cubeGeometry = new CubeGeometry();
        var cubeMesh = new Mesh(cubeGeometry);
        var cubeNode = new SceneNode(
            localTransform: Matrix.Trs(
                new Vector3(-1.5f, 0, 0),
                Quaternion.Identity,
                new Vector3(1f, 1f, 1f)),
            meshes: new List<Mesh> { cubeMesh },
            children: new List<SceneNode>(),
            name: "Cube"
        );

        // Create sphere node
        var sphereGeometry = new SphereGeometry(latitudeSegments: 24, longitudeSegments: 32, radius: 0.8f);
        var sphereMesh = new Mesh(sphereGeometry);
        var sphereNode = new SceneNode(
            localTransform: Matrix.Trs(
                new Vector3(1.5f, 0, 0),
                Quaternion.Identity,
                new Vector3(1f, 1f, 1f)),
            meshes: new List<Mesh> { sphereMesh },
            children: new List<SceneNode>(),
            name: "Sphere"
        );

        // Create the scene with both nodes as roots
        scene = new Scene(new List<SceneNode> { cubeNode, sphereNode });

        app.Add(scene);

        var bounds = scene.ComputeBounds();
        camera.Frame(bounds, frameMultiplier: 2.0f);

        app.Camera = camera;
        app.DirectionalLight = directional;
        app.PointLight = point;
        app.SpotLight = spot;
        app.SetDirectionalEnabled(directionalEnabled);
        app.SetPointEnabled(pointEnabled);

        var orbitController = new OrbitController(
            target: Vector3.Zero,
            yaw: 0f,
            pitch: 0.3f,
            distance: 5f,
            minDistance: 2f,
            maxDistance: 15f);
        app.SetController(orbitController);

        await app.StartAsync(OnFrameAsync);
    }

    private async Task OnFrameAsync(float dt)
    {
        if (app is null || scene is null) return;

        // Update lights based on debug UI
        if (directional is not null)
        {
            directional.Intensity = directionalEnabled ? directionalIntensity : 0f;
            directional.Color = HexToVector3(directionalColor);
        }

        if (point is not null)
        {
            point.Intensity = pointEnabled ? pointIntensity : 0f;
            point.Color = HexToVector3(pointColor);
            point.Position = new Vector3(2f, pointPosY, 2f);
        }

        if (spot is not null)
        {
            spot.Intensity = spotEnabled ? spotIntensity : 0f;
            spot.Color = HexToVector3(spotColor);
        }

        app.SetDirectionalEnabled(directionalEnabled);
        app.SetPointEnabled(pointEnabled);

        app.Render(scene);

        await Task.CompletedTask;
    }

    private Vector3 HexToVector3(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            var r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            var g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            var b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            return new Vector3(r, g, b);
        }
        return new Vector3(1, 1, 1);
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
        }
    }

}
