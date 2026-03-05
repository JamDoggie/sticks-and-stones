using Godot;
using net.minecraft.src;

namespace SticksAndStones.sticks_and_stones.bridge_utils
{
    /// <summary>
    /// Converts raw vertex buffer data (in the Tessellator's 32-byte-per-vertex format) into
    /// Godot meshes managed through the RenderingServer API.
    /// 
    /// Vertex layout (32 bytes per vertex):
    ///   [0..11]  3 floats  — position  (X, Y, Z)
    ///   [12..19] 2 floats  — texture   (U, V)
    ///   [20..23] 4 bytes   — color     (R, G, B, A)
    ///   [24..27] 4 sbytes  — normal    (X, Y, Z, _)
    ///   [28..31] 2 shorts  — brightness (X, Y)
    /// </summary>
    public static class GodotMeshBuilder
    {
        private static readonly int VertexSize = Tessellator.VertexSize; // 32

        /// <summary>
        /// Builds a RenderingServer mesh from raw vertex bytes produced by the Tessellator.
        /// The returned Rid must be freed by the caller via <see cref="RenderingServer.FreeRid"/>.
        /// Returns a default (invalid) Rid when the data contains no vertices.
        /// </summary>
        public static Rid BuildMeshFromRawVertexData(ReadOnlySpan<byte> data)
        {
            int vertexCount = data.Length / VertexSize;

            if (vertexCount == 0)
                return new Rid();

            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var colors = new Color[vertexCount];
            var normals = new Vector3[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                int off = i * VertexSize;

                // Position
                float x = BitConverter.ToSingle(data.Slice(off, 4));
                float y = BitConverter.ToSingle(data.Slice(off + 4, 4));
                float z = BitConverter.ToSingle(data.Slice(off + 8, 4));
                vertices[i] = new Vector3(x, y, z);

                // Texture coordinates
                float u = BitConverter.ToSingle(data.Slice(off + 12, 4));
                float v = BitConverter.ToSingle(data.Slice(off + 16, 4));
                uvs[i] = new Vector2(u, v);

                // Color (RGBA unsigned bytes)
                byte r = data[off + 20];
                byte g = data[off + 21];
                byte b = data[off + 22];
                byte a = data[off + 23];
                colors[i] = new Color(r / 255f, g / 255f, b / 255f, a / 255f);

                // Normal (signed bytes, scaled to [-1,1])
                sbyte nx = (sbyte)data[off + 24];
                sbyte ny = (sbyte)data[off + 25];
                sbyte nz = (sbyte)data[off + 26];
                normals[i] = new Vector3(nx / 127f, ny / 127f, nz / 127f);
            }

            // Always compute face normals from geometry.
            // The Tessellator may carry stale non-zero normals from prior draw calls.
            ComputeNormals(vertices, normals);

            // Reverse triangle winding order (swap v1 ↔ v2 per triangle).
            // OpenGL uses CCW front faces; Godot (Vulkan) uses CW.
            ReverseTriangleWinding(vertices, uvs, colors, normals);

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
            arrays[(int)Mesh.ArrayType.Color] = colors;
            arrays[(int)Mesh.ArrayType.Normal] = normals;

            Rid mesh = RenderingServer.MeshCreate();
            RenderingServer.MeshAddSurfaceFromArrays(mesh, RenderingServer.PrimitiveType.Triangles, arrays);

            return mesh;
        }

        /// <summary>
        /// Replaces the surface data of an existing mesh Rid with new vertex data.
        /// This avoids freeing/recreating the mesh and instance Rids on every chunk rebuild.
        /// </summary>
        public static void UpdateMeshFromRawVertexData(Rid mesh, ReadOnlySpan<byte> data)
        {
            RenderingServer.MeshClear(mesh);

            int vertexCount = data.Length / VertexSize;
            if (vertexCount == 0)
                return;

            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var colors = new Color[vertexCount];
            var normals = new Vector3[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                int off = i * VertexSize;

                float x = BitConverter.ToSingle(data.Slice(off, 4));
                float y = BitConverter.ToSingle(data.Slice(off + 4, 4));
                float z = BitConverter.ToSingle(data.Slice(off + 8, 4));
                vertices[i] = new Vector3(x, y, z);

                float u = BitConverter.ToSingle(data.Slice(off + 12, 4));
                float v = BitConverter.ToSingle(data.Slice(off + 16, 4));
                uvs[i] = new Vector2(u, v);

                byte r = data[off + 20];
                byte g = data[off + 21];
                byte b = data[off + 22];
                byte a = data[off + 23];
                colors[i] = new Color(r / 255f, g / 255f, b / 255f, a / 255f);

                sbyte nx = (sbyte)data[off + 24];
                sbyte ny = (sbyte)data[off + 25];
                sbyte nz = (sbyte)data[off + 26];
                normals[i] = new Vector3(nx / 127f, ny / 127f, nz / 127f);
            }

            ComputeNormals(vertices, normals);
            ReverseTriangleWinding(vertices, uvs, colors, normals);

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
            arrays[(int)Mesh.ArrayType.Color] = colors;
            arrays[(int)Mesh.ArrayType.Normal] = normals;

            RenderingServer.MeshAddSurfaceFromArrays(mesh, RenderingServer.PrimitiveType.Triangles, arrays);
        }

        /// <summary>
        /// Swaps the second and third vertex of every triangle to reverse winding order.
        /// OpenGL treats CCW as front-facing; Godot (Vulkan) treats CW as front-facing.
        /// </summary>
        private static void ReverseTriangleWinding(
            Vector3[] vertices, Vector2[] uvs, Color[] colors, Vector3[] normals)
        {
            for (int t = 0; t + 2 < vertices.Length; t += 3)
            {
                (vertices[t + 1], vertices[t + 2]) = (vertices[t + 2], vertices[t + 1]);
                (uvs[t + 1], uvs[t + 2]) = (uvs[t + 2], uvs[t + 1]);
                (colors[t + 1], colors[t + 2]) = (colors[t + 2], colors[t + 1]);
                (normals[t + 1], normals[t + 2]) = (normals[t + 2], normals[t + 1]);
            }
        }

        /// <summary>
        /// Computes face normals from triangle geometry for every triangle.
        ///
        /// The Tessellator's <c>hasNormals</c> flag is not reset by <c>StartBuildingVBO</c>,
        /// so stale normals from a prior draw call (e.g. inventory item rendering) can leak
        /// into the terrain vertex buffer.  Because the stale values are non-zero,
        /// a conditional "skip if non-zero" check would leave them in place, giving every
        /// face the same incorrect normal.  Computing from geometry unconditionally avoids
        /// this and is always correct for terrain chunk data.
        /// </summary>
        private static void ComputeNormals(Vector3[] vertices, Vector3[] normals)
        {
            for (int t = 0; t + 2 < vertices.Length; t += 3)
            {
                Vector3 edge1 = vertices[t + 1] - vertices[t];
                Vector3 edge2 = vertices[t + 2] - vertices[t];
                Vector3 faceNormal = edge1.Cross(edge2).Normalized();

                if (faceNormal.LengthSquared() < 0.001f)
                    continue;

                normals[t] = faceNormal;
                normals[t + 1] = faceNormal;
                normals[t + 2] = faceNormal;
            }
        }
    }
}
