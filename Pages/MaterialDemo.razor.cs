using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Velvet.Hosting.Web;
using Velvet.Core.Assets.Gltf;
using Velvet.Core.Scene;
using Velvet.Core.Math;
using Velvet.Core.Rendering;
using Velvet.Core.Rendering.Cameras;
using Velvet.Core.Rendering.Cameras.Controllers;
using Velvet.Core.Rendering.Input;
using Velvet.Core.Rendering.Lighting;
using Velvet.Core.Rendering.Materials;
using Velvet.Core.Rendering.Meshes;
using Velvet.Graphics.WebGL;
using Velvet.Graphics.WebGL.Shaders;
using BlazorApp = Velvet.Hosting.Web.VelvetHost;
using NewMaterial = Velvet.Core.Rendering.Materials.Material;

namespace Velvet_Site.Pages;

public partial class MaterialDemo : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private ElementReference canvasRef;

    private BlazorApp? app;
    private Scene? scene;
    private Camera? camera;
    private DirectionalLight? directional;
    private PointLight? point;

    // New Material system fields
    private NewMaterial? matteMaterial;
    private NewMaterial? standardMaterial;
    private NewMaterial? brightMaterial;
    private WebGLShader? shader;
    private Dictionary<Mesh, NewMaterial> meshMaterialMap = new();

    // Debug UI properties
    private bool directionalEnabled = true;
    private float directionalIntensity = 1.2f;
    private string directionalColor = "#ffffff";

    private bool pointEnabled = true;
    private float pointIntensity = 2.5f;
    private string pointColor = "#fff2e6";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        app = await BlazorApp.CreateAsync(canvasRef, JS, ShaderProgram.CreateDefaultAsync);

        // Create shader adapter for material uniforms
        shader = new WebGLShader(app.Program);

        camera = new Camera(
            position: new Vector3(0, 2f, 7f),
            target: new Vector3(0, 0, 0),
            up: Vector3.UnitY,
            fovYRadians: 50.0f * (System.MathF.PI / 180.0f),
            aspectRatio: 16.0f / 9.0f,
            nearPlane: 0.1f,
            farPlane: 100.0f);

        directional = new DirectionalLight(
            direction: new Vector3(0.3f, -1.0f, -0.3f),
            color: HexToVector3(directionalColor),
            intensity: directionalIntensity);

        point = new PointLight(
            position: new Vector3(3f, 2.5f, 3f),
            color: HexToVector3(pointColor),
            intensity: pointIntensity,
            constant: 1.0f,
            linear: 0.14f,
            quadratic: 0.07f);

        // Load Suzanne model
        var modelPath = "models/gltf/suzanne.glb";
        var bytes = await Http.GetByteArrayAsync(modelPath);
        var loadedScene = await GltfLoader.LoadScene(bytes);

        if (loadedScene == null || loadedScene.Roots.Count == 0)
        {
            Console.WriteLine($"Failed to load model: {modelPath}");
            return;
        }

        var rootNodes = new List<SceneNode>();

        // Create three new material variations using shader-driven system
        // Material 1: Matte Red (low ambient)
        matteMaterial = new NewMaterial(shader);
        matteMaterial.Set("uBaseColor", new Vector3(1.0f, 0.42f, 0.42f));
        matteMaterial.Set("uAmbientStrength", 0.03f);

        // Material 2: Standard Cyan (balanced lighting)
        standardMaterial = new NewMaterial(shader);
        standardMaterial.Set("uBaseColor", new Vector3(0.31f, 0.80f, 0.77f));
        standardMaterial.Set("uAmbientStrength", 0.08f);

        // Material 3: Bright Yellow (high ambient)
        brightMaterial = new NewMaterial(shader);
        brightMaterial.Set("uBaseColor", new Vector3(1.0f, 0.90f, 0.43f));
        brightMaterial.Set("uAmbientStrength", 0.15f);

        // Create three instances of Suzanne with different materials
        var newMaterials = new[] { matteMaterial, standardMaterial, brightMaterial };
        var positions = new[] { -2.5f, 0f, 2.5f };
        var names = new[] { "Suzanne_Matte", "Suzanne_Standard", "Suzanne_Bright" };

        for (int i = 0; i < 3; i++)
        {
            var suzanneNode = CloneSceneNode(loadedScene.Roots[0]);
            suzanneNode = new SceneNode(
                localTransform: Matrix.Trs(
                    new Vector3(positions[i], 0, 0),
                    Quaternion.Identity,
                    new Vector3(1.0f, 1.0f, 1.0f)
                ),
                meshes: suzanneNode.Meshes,
                children: suzanneNode.Children,
                name: names[i]
            );

            // Map all meshes in this node to their material
            foreach (var mesh in GetAllMeshes(suzanneNode))
            {
                meshMaterialMap[mesh] = newMaterials[i];
            }

            rootNodes.Add(suzanneNode);
        }

        scene = new Scene(rootNodes);
        app.Add(scene);

        var bounds = scene.ComputeBounds();
        camera.Frame(bounds, frameMultiplier: 1.5f);

        app.Camera = camera;
        app.DirectionalLight = directional;
        app.PointLight = point;
        app.SetDirectionalEnabled(directionalEnabled);
        app.SetPointEnabled(pointEnabled);

        var orbitController = new OrbitController(
            target: Vector3.Zero,
            yaw: 0.3f,
            pitch: 0.15f,
            distance: 7f,
            minDistance: 3f,
            maxDistance: 20f);
        app.SetController(orbitController);

        await app.StartAsync(
            onFrame: OnFrameAsync,
            beforeDrawMesh: BeforeDrawMesh);
    }

    private SceneNode CloneSceneNode(SceneNode original)
    {
        var clonedChildren = original.Children.Select(child => CloneSceneNode(child)).ToList();
        return new SceneNode(
            localTransform: original.LocalTransform,
            meshes: original.Meshes,
            children: clonedChildren,
            name: original.Name
        );
    }

    private List<Mesh> GetAllMeshes(SceneNode node)
    {
        var meshes = new List<Mesh>(node.Meshes);
        foreach (var child in node.Children)
        {
            meshes.AddRange(GetAllMeshes(child));
        }
        return meshes;
    }

    private async Task BeforeDrawMesh(Mesh mesh)
    {
        if (shader == null) return;

        // Look up the material for this specific mesh
        if (meshMaterialMap.TryGetValue(mesh, out var material))
        {
            // Apply the material's uniforms
            material.Apply();
            
            // Flush pending uniform writes
            if (shader is WebGLShader webglShader)
            {
                await webglShader.FlushAsync();
            }
        }
    }

    private async Task OnFrameAsync(float deltaTime)
    {
        if (scene == null || app == null) return;

        // Update light properties from UI
        if (directional != null)
        {
            directional.Intensity = directionalEnabled ? directionalIntensity : 0f;
            directional.Color = HexToVector3(directionalColor);
        }

        if (point != null)
        {
            point.Intensity = pointEnabled ? pointIntensity : 0f;
            point.Color = HexToVector3(pointColor);
        }

        app.SetDirectionalEnabled(directionalEnabled);
        app.SetPointEnabled(pointEnabled);

        app.Render(scene);

        await Task.CompletedTask;
    }

    private static Vector3 HexToVector3(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return new Vector3(1, 1, 1);

        var r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        var g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        var b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;

        return new Vector3(r, g, b);
    }

    public async ValueTask DisposeAsync()
    {
        if (app != null)
        {
            await app.StopAsync();
        }
    }
}
