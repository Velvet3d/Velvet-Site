using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Velvet.Core.Geometry;
using Velvet.Core.Math;
using Velvet.Core.Rendering.Cameras;
using Velvet.Core.Rendering.Controllers;
using Velvet.Core.Rendering.Lighting;
using Velvet.Core.Rendering.Materials;
using Velvet.Core.Rendering.Meshes;
using Velvet.Core.Scene;
using Velvet.Graphics.WebGL;
using BlazorApp = Velvet.Hosting.Web.BlazorVelvetHost;

namespace Velvet_Site.Pages;

public partial class Scene3 : ComponentBase, IAsyncDisposable
{
    private const float PointMarkerScale = 0.1f;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ElementReference canvasRef;

    private BlazorApp? app;
    private Scene? scene;
    private Camera? camera;
    private DirectionalLight? directional;
    private PointLight? point;
    private SpotLight? spot;
    private SceneNode? pointMarkerNode;
    private float[]? pointMarkerTransform;

    private enum LightKind
    {
        Directional,
        Point,
        Spot
    }

    private LightKind activeLight = LightKind.Point;

    private bool directionalEnabled = true;
    private float directionalIntensity = 1.15f;

    private bool pointEnabled = true;
    private float pointIntensity = 2.6f;
    private float pointPosY = 1.35f;

    private bool spotEnabled = true;
    private float spotIntensity = 8.0f;

    private const string BlazorCode = """
var app = await BlazorVelvetHost.CreateAsync(canvasRef, JS, ShaderProgram.CreateDefaultAsync);

var directional = new DirectionalLight(...);
var point = new PointLight(...);
var spot = new SpotLight(...);

app.DirectionalLight = directional;
app.PointLight = point;
app.SpotLight = spot;

await app.StartAsync(dt =>
{
    app.Render(scene);
});
""";

    private const string RazorCode = """
<canvas @ref="canvasRef" class="scene3-canvas"></canvas>

<script>
    window.addEventListener("load", () => {
        window.Velvet.start("lighting-canvas");
    });
</script>
""";

    private string ActiveLightLabel => activeLight switch
    {
        LightKind.Directional => "Directional light selected",
        LightKind.Point => "Point light selected",
        LightKind.Spot => "Spot light selected",
        _ => "Directional light selected"
    };

    private float ActiveLightIntensity
    {
        get => activeLight switch
        {
            LightKind.Directional => directionalIntensity,
            LightKind.Point => pointIntensity,
            LightKind.Spot => spotIntensity,
            _ => directionalIntensity
        };
        set
        {
            switch (activeLight)
            {
                case LightKind.Directional:
                    directionalIntensity = value;
                    break;
                case LightKind.Point:
                    pointIntensity = value;
                    break;
                case LightKind.Spot:
                    spotIntensity = value;
                    break;
            }
            // No engine calls here; handled in OnFrameAsync
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateDefaultAsync);

        camera = new Camera(
            position: new Vector3(0f, 2.1f, 5.5f),
            target: new Vector3(0f, 0.15f, 0f),
            up: Vector3.UnitY,
            fovYRadians: 60.0f * (System.MathF.PI / 180.0f),
            aspectRatio: 16.0f / 9.0f,
            nearPlane: 0.1f,
            farPlane: 100.0f);

        directional = new DirectionalLight(
            direction: new Vector3(-0.45f, -1.0f, -0.25f),
            color: new Vector3(1f, 0.98f, 0.95f),
            intensity: directionalIntensity);

        // point = new PointLight(
        //     position: new Vector3(0f, pointPosY, 2f),
        //     color: new Vector3(1f, 0.95f, 0.9f),
        //     intensity: pointIntensity,
        //     constant: 1.0f,
        //     linear: 0.05f,
        //     quadratic: 0.01f);
        point = new PointLight(
position: new Vector3(0f, 1f, 1f),
color: new Vector3(1f, 0f, 0f), // RED
intensity: 50f, // VERY HIGH
constant: 1f,
linear: 0.01f,
quadratic: 0.001f);

        spot = new SpotLight(
            position: new Vector3(0f, 3f, 2.2f),
            direction: new Vector3(0f, -0.5f, -1f),
            color: new Vector3(1f, 1f, 1f),
            intensity: spotIntensity,
            cutoff: 12.0f * (System.MathF.PI / 180.0f),
            outerCutoff: 22.0f * (System.MathF.PI / 180.0f),
            constant: 1.0f,
            linear: 0.05f,
            quadratic: 0.01f);

        var cubeGeometry = new CubeGeometry();
        var cubeMesh = new Mesh(cubeGeometry);
        var cubeNode = new SceneNode(
            localTransform: Matrix.Trs(
                new Vector3(-0.95f, 0f, 0f),
                Quaternion.Identity,
                new Vector3(1f, 1f, 1f)),
            meshes: new List<Mesh> { cubeMesh },
            children: new List<SceneNode>(),
            name: "Cube");

        var sphereGeometry = new SphereGeometry(latitudeSegments: 24, longitudeSegments: 32, radius: 0.8f);
        var sphereMesh = new Mesh(sphereGeometry);
        var sphereNode = new SceneNode(
            localTransform: Matrix.Trs(
                new Vector3(0.95f, 0f, 0f),
                Quaternion.Identity,
                new Vector3(1f, 1f, 1f)),
            meshes: new List<Mesh> { sphereMesh },
            children: new List<SceneNode>(),
            name: "Sphere");

        var pointMarkerGeometry = new SphereGeometry(latitudeSegments: 16, longitudeSegments: 24, radius: 0.5f);
        var pointMarkerMesh = new Mesh(pointMarkerGeometry)
        {
            Material = new StandardMaterial(new Vector3(1f, 0.98f, 0.85f), ambientStrength: 0.35f, diffuseStrength: 1.0f, unlit: true)
        };
        pointMarkerTransform = Matrix.Trs(
            new Vector3(0f, pointPosY, 2f),
            Quaternion.Identity,
            new Vector3(PointMarkerScale, PointMarkerScale, PointMarkerScale));
        pointMarkerNode = new SceneNode(
            localTransform: pointMarkerTransform,
            meshes: new List<Mesh> { pointMarkerMesh },
            children: new List<SceneNode>(),
            name: "PointLightMarker");

        scene = new Scene(new List<SceneNode> { cubeNode, sphereNode, pointMarkerNode });

        app.Add(scene);

        var bounds = scene.ComputeBounds();
        camera.Frame(bounds, frameMultiplier: 1.7f);

        app.Camera = camera;
        app.DirectionalLight = directional;
        app.PointLight = point;
        app.SetSpotLight(spot); // Only use SetSpotLight
        app.SetDirectionalEnabled(directionalEnabled);
        app.SetPointEnabled(pointEnabled);

        var orbitController = new OrbitController(
            target: bounds.Center,
            yaw: 0f,
            pitch: 0.28f,
            distance: (bounds.Center - camera.Position).Length,
            minDistance: bounds.Radius * 0.6f,
            maxDistance: bounds.Radius * 4.5f);
        app.SetController(orbitController);

        // Initial state applied ONCE before StartAsync
        ApplyLightState();

        await app.StartAsync(OnFrameAsync);
    }

    private void SetActiveLight(LightKind kind)
    {
        activeLight = kind;
        // No engine calls here; handled in OnFrameAsync
    }

    private void ApplyLightState()
    {
        // Only used for initial state after scene setup
        if (directional is not null)
        {
            directional.Intensity = directionalEnabled ? directionalIntensity : 0f;
        }
        if (point is not null)
        {
            point.Intensity = pointEnabled ? pointIntensity : 0f;
            point.Position = new Vector3(0f, pointPosY, 2f);
        }
        if (spot is not null)
        {
            spot.Intensity = spotEnabled ? spotIntensity : 0f;
            spot.Direction = new Vector3(0f, -0.5f, -1f);
        }
    }

    private Task OnFrameAsync(float dt)
    {
        if (app is null || scene is null)
            return Task.CompletedTask;

        // --- Update light values ONLY (no rebinding) ---

        if (directional is not null)
        {
            directional.Intensity = directionalEnabled ? directionalIntensity : 0f;
        }

        if (point is not null)
        {
            point.Intensity = pointEnabled ? pointIntensity : 0f;
            point.Position = new Vector3(0f, pointPosY, 2f);
        }

        if (spot is not null)
        {
            spot.Intensity = spotEnabled ? spotIntensity : 0f;
            spot.Direction = new Vector3(0f, -0.5f, -1f);
        }

        // Enable flags (this is OK per frame)
        app.SetDirectionalEnabled(directionalEnabled);
        app.SetPointEnabled(pointEnabled);

        // Update marker
        if (pointMarkerTransform is not null && point is not null)
        {
            pointMarkerTransform[12] = point.Position.X;
            pointMarkerTransform[13] = point.Position.Y;
            pointMarkerTransform[14] = point.Position.Z;
        }

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
