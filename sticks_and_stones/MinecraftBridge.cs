using Godot;
using net.minecraft.client;

namespace SticksAndStones.sticks_and_stones
{
    [GlobalClass]
    public partial class MinecraftBridge : Node
    {
        private Minecraft? minecraft;

        // Game nodes
        public MinecraftGodotCamera? MinecraftCamera { get; set; }

        public override void _Ready()
        {
            minecraft = Minecraft._EntryPoint(this);
        }

        public override void _Process(double delta)
        {
            if (minecraft == null)
                return;

            minecraft._RunLoop();
        }
    }
}
