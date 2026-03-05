using BlockByBlock;
using Godot;
using net.minecraft.src;
using SticksAndStones.sticks_and_stones.bridge_utils;

namespace net.minecraft.client.world.render
{
    /// <summary>
    /// Godot RenderingServer integration for the chunk mesh arena allocator.
    /// 
    /// Mirrors the OpenGL buffer management pattern using Godot's RenderingServer API:
    ///   - Each allocation (chunk + render pass) gets its own mesh Rid and instance Rid.
    ///   - Mesh data is uploaded via <see cref="GodotMeshBuilder"/>.
    ///   - Instances are placed in the active Godot scenario so they render automatically.
    ///   - Buffer lifecycle (allocate / free / update) is kept in sync with the GL path.
    /// </summary>
    public partial class ChunkMeshAllocator
    {
        private Rid godotScenario;
        private bool godotInitialized = false;
        private Rid godotDefaultMaterial;

        // Prevent GC from collecting the material / texture resources and invalidating their Rids.
        private StandardMaterial3D? godotMaterialResource;
        private ImageTexture? godotTerrainTexture;

        private readonly Dictionary<int, GodotChunkMesh> godotMeshes = new();

        private struct GodotChunkMesh
        {
            public Rid Mesh;
            public Rid Instance;
        }

        /// <summary>
        /// Call once after the Godot scene tree is ready to enable Godot-side mesh rendering.
        /// <paramref name="scenario"/> should come from <c>GetViewport().World3D.Scenario</c>.
        /// </summary>
        public void InitGodot(Rid scenario, Rid? material = null)
        {
            godotScenario = scenario;
            godotInitialized = true;

            if (material.HasValue)
            {
                godotDefaultMaterial = material.Value;
            }
            else
            {
                CreateDefaultGodotMaterial();
            }
        }

        /// <summary>
        /// Loads <c>/terrain.png</c> through the same resource path the game uses and
        /// builds a <see cref="StandardMaterial3D"/> with the atlas as albedo texture,
        /// vertex-color modulation, nearest-neighbor filtering, and alpha-scissor
        /// transparency.
        /// </summary>
        private void CreateDefaultGodotMaterial()
        {
            godotTerrainTexture = LoadTerrainTexture();

            var mat = new StandardMaterial3D();
            mat.VertexColorUseAsAlbedo = true;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Back;
            mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps;

            if (godotTerrainTexture != null)
                mat.AlbedoTexture = godotTerrainTexture;

            mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            mat.AlphaScissorThreshold = 0.1f;

            godotMaterialResource = mat;
            godotDefaultMaterial = mat.GetRid();
        }

        /// <summary>
        /// Loads <c>/terrain.png</c> via <see cref="GameEnv.GetResourceAsStream"/> (the same
        /// path used by the OpenGL <see cref="TextureManager"/>) and returns it as a Godot
        /// <see cref="ImageTexture"/>.
        /// </summary>
        private static ImageTexture? LoadTerrainTexture()
        {
            try
            {
                using var stream = GameEnv.GetResourceAsStream("/terrain.png");
                if (stream == null)
                    return null;

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                byte[] pngBytes = ms.ToArray();

                var image = new Image();
                image.LoadPngFromBuffer(pngBytes);
                image.GenerateMipmaps();

                return ImageTexture.CreateFromImage(image);
            }
            catch (Exception e)
            {
                Godot.GD.PrintErr($"Failed to load terrain texture for Godot: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Called from <see cref="PlaceData"/> after the GL upload.  Creates (or updates) the
        /// Godot mesh and instance for this allocation.
        /// </summary>
        private void GodotPlaceData(int id, ReadOnlySpan<byte> data, WorldRenderer? renderer)
        {
            if (!godotInitialized)
                return;

            // Build or update the mesh
            if (godotMeshes.TryGetValue(id, out GodotChunkMesh existing))
            {
                GodotMeshBuilder.UpdateMeshFromRawVertexData(existing.Mesh, data);

                if (godotDefaultMaterial.IsValid)
                    RenderingServer.MeshSurfaceSetMaterial(existing.Mesh, 0, godotDefaultMaterial);

                SetGodotInstanceTransform(existing.Instance, renderer);
            }
            else
            {
                Rid mesh = GodotMeshBuilder.BuildMeshFromRawVertexData(data);

                if (!mesh.IsValid)
                    return;

                if (godotDefaultMaterial.IsValid)
                    RenderingServer.MeshSurfaceSetMaterial(mesh, 0, godotDefaultMaterial);

                Rid instance = RenderingServer.InstanceCreate();
                RenderingServer.InstanceSetBase(instance, mesh);
                RenderingServer.InstanceSetScenario(instance, godotScenario);
                RenderingServer.InstanceGeometrySetCastShadowsSetting(instance, RenderingServer.ShadowCastingSetting.On);
                SetGodotInstanceTransform(instance, renderer);

                godotMeshes[id] = new GodotChunkMesh { Mesh = mesh, Instance = instance };
            }
        }

        /// <summary>
        /// Called from <see cref="FreeData"/> after the GL cleanup.  Frees the corresponding
        /// Godot mesh and instance Rids.
        /// </summary>
        private void GodotFreeData(int id)
        {
            if (!godotInitialized)
                return;

            if (godotMeshes.TryGetValue(id, out GodotChunkMesh chunk))
            {
                RenderingServer.FreeRid(chunk.Instance);
                RenderingServer.FreeRid(chunk.Mesh);
                godotMeshes.Remove(id);
            }
        }

        /// <summary>
        /// Positions a Godot instance at the chunk's world-space origin.
        ///
        /// The Tessellator stores vertex positions relative to
        /// <c>(posXMinus, posYMinus, posZMinus)</c>, so the instance transform translates
        /// by that offset to place vertices at their correct world coordinates.
        /// </summary>
        private static void SetGodotInstanceTransform(Rid instance, WorldRenderer? renderer)
        {
            if (renderer == null)
                return;

            var origin = new Vector3(renderer.posXMinus, renderer.posYMinus, renderer.posZMinus);
            RenderingServer.InstanceSetTransform(instance, new Transform3D(Basis.Identity, origin));
        }
    }
}
