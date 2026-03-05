using Godot;
using net.minecraft.client;

namespace SticksAndStones.sticks_and_stones
{
    [GlobalClass]
    public partial class MinecraftBridge : Node
    {
        private Minecraft? minecraft;
        private bool godotMeshInitialized = false;

        // Game nodes
        [Export]
        public MinecraftGodotCamera? MinecraftCamera { get; set; }
        [Export]
        public Node3D? ArmNode { get; set; }

        public override void _Ready()
        {
            minecraft = Minecraft._EntryPoint(this);
        }

        public override void _Process(double delta)
        {
            if (minecraft == null)
                return;

            if (!godotMeshInitialized && minecraft.renderGlobal != null)
            {
                var scenario = GetViewport().World3D.Scenario;
                minecraft.renderGlobal.MeshAllocator.InitGodot(scenario);
                godotMeshInitialized = true;
            }

            minecraft._RunLoop();
        }
    }
}
