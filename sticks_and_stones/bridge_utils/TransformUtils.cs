using BlockByBlock.net.minecraft.render;
using Godot;

namespace SticksAndStones.sticks_and_stones.bridge_utils
{
    public static class TransformUtils
    {
        public static Transform3D GodotTransform(this OpenTK.Mathematics.Matrix4 matrix)
        {
            // OpenTK uses row-vector convention (v' = v * M), Godot uses column-vector
            // convention (v' = M * v). To convert, the 3x3 part must be transposed:
            // OpenTK rows become Godot columns. The Basis constructor takes column vectors,
            // so we pass each OpenTK row as a Godot column.
            var basis = new Basis(
                new Vector3(matrix.M11, matrix.M12, matrix.M13),
                new Vector3(matrix.M21, matrix.M22, matrix.M23),
                new Vector3(matrix.M31, matrix.M32, matrix.M33));

            return new Transform3D(basis, new Vector3(matrix.M41, matrix.M42, matrix.M43));
        }

        public static Transform3D GodotTransform(this MatrixStack stack)
        {
            return stack.GetMatrix().GodotTransform();
        }

        /// <summary>
        /// Translates in local space (post-multiply), matching OpenGL glTranslate / MatrixStack.Translate semantics.
        /// </summary>
        public static Transform3D LocalTranslated(this Transform3D t, Vector3 offset)
        {
            return t * new Transform3D(Basis.Identity, offset);
        }

        /// <summary>
        /// Rotates in local space (post-multiply), matching OpenGL glRotate / MatrixStack.Rotate semantics.
        /// Angle is in radians.
        /// </summary>
        public static Transform3D LocalRotated(this Transform3D t, Vector3 axis, float angleRadians)
        {
            return t * new Transform3D(new Basis(axis, angleRadians), Vector3.Zero);
        }

        /// <summary>
        /// Scales in local space (post-multiply), matching OpenGL glScale / MatrixStack.Scale semantics.
        /// Only the basis is scaled; the origin is unchanged.
        /// </summary>
        public static Transform3D LocalScaled(this Transform3D t, Vector3 scale)
        {
            return new Transform3D(t.Basis.Scaled(scale), t.Origin);
        }
    }
}
