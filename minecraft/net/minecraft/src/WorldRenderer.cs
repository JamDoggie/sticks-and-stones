using BlockByBlock.helpers;
using BlockByBlock.net.minecraft.client.entity.render;
using BlockByBlock.net.minecraft.render;
using net.minecraft.client;
using net.minecraft.client.entity;
using net.minecraft.client.entity.render;
using net.minecraft.client.world.render;
using OpenTK.Graphics.OpenGL;
using System.Collections;
using System.Collections.Generic;

namespace net.minecraft.src
{

    public class WorldRenderer
    {
        public World worldObj;
        private static Tessellator tessellator = Tessellator.instance;
        public static int chunksUpdated = 0;
        public int posX { get; set; }
        public int posY { get; set; }
        public int posZ { get; set; }
        public int posXMinus { get; set; }
        public int posYMinus { get; set; }
        public int posZMinus { get; set; }
        public int posXClip { get; set; }
        public int posYClip { get; set; }
        public int posZClip { get; set; }

        public int X2 = 0;
        public int Y2 = 0;
        public int Z2 = 0;
        public double X1 = 0;
        public double Y1 = 0;
        public double Z1 = 0;

        public bool isInFrustum { get; set; } = false;
        public bool[] skipRenderPass = new bool[2];
        public int posXPlus;
        public int posYPlus;
        public int posZPlus;
        public bool needsUpdate;
        public AxisAlignedBB rendererBoundingBox;
        public int chunkIndex;
        public bool isVisible = true;
        public bool isWaitingOnOcclusionQuery;
        public int glOcclusionQuery { get; set; }
        public bool isChunkLit;
        private bool isInitialized = false;
        public IList tileEntityRenderers = new ArrayList();
        private IList tileEntities;
        private int bytesDrawn;
        private ChunkMeshAllocator meshAllocator;
        
        private VertexBuffer? AABBVBO = null;
        
        public int?[] meshDataAllocations = { null, null };

        public WorldRenderer(World world1, IList list2, int x, int y, int z, ChunkMeshAllocator meshAllocator)
        {
            worldObj = world1;
            tileEntities = list2;
            posX = -999;
            setPosition(x, y, z);
            needsUpdate = false;
            this.meshAllocator = meshAllocator;
        }

        ~WorldRenderer()
        {
            for (int i = 0; i < meshDataAllocations.Length; i++)
            {
                if (meshDataAllocations[i] != null)
                {
                    meshAllocator.FreeData(meshDataAllocations[i].Value);
                }
            }
        }

        public virtual void setPosition(int x, int y, int z)
        {
            if (x != posX || y != posY || z != posZ)
            {
                setDontDraw();
                posX = x;
                posY = y;
                posZ = z;
                posXPlus = x + 8;
                posYPlus = y + 8;
                posZPlus = z + 8;
                posXClip = x & 1023;
                posYClip = y;
                posZClip = z & 1023;
                posXMinus = x - posXClip;
                posYMinus = y - posYClip;
                posZMinus = z - posZClip;
                float f4 = 6.0F;
                rendererBoundingBox = AxisAlignedBB.getBoundingBox((double)((float)x - f4), (double)((float)y - f4), (double)((float)z - f4), (double)((float)(x + 16) + f4), (double)((float)(y + 16) + f4), (double)((float)(z + 16) + f4));

                AABBVBO = Renderer.BuildAABBVBO(AxisAlignedBB.getBoundingBoxFromPool((double)((float)posXClip - f4), (double)((float)posYClip - f4), (double)((float)posZClip - f4), (double)((float)(posXClip + 16) + f4), (double)((float)(posYClip + 16) + f4), (double)((float)(posZClip + 16) + f4)));

                markDirty();
            }
        }

        public virtual void SetRenderPos(double x, double y, double z, int x2, int y2, int z2)
        {
            X1 = x;
            Y1 = y;
            Z1 = z;
            X2 = x2;
            Y2 = y2;
            Z2 = z2;
        }

        internal void setupGLTranslation()
        {
            //stack.Translate((float)this.posXClip, (float)this.posYClip, (float)this.posZClip);
            tessellator.setTranslation(posXClip, posYClip, posZClip);
        }

        public virtual void updateRenderer()
        {
            Profiler.startSection("updateRenderer");
            if (needsUpdate)
            {
                needsUpdate = false;
                int startX = posX;
                int startY = posY;
                int startZ = posZ;
                int endX = posX + 16;
                int endY = posY + 16;
                int endZ = posZ + 16;

                for (int i = 0; i < 2; ++i)
                {
                    skipRenderPass[i] = true;
                }

                Chunk.isLit = false;
                HashSet<object> hashSet21 = new HashSet<object>();
                hashSet21.AddAll(tileEntityRenderers);
                tileEntityRenderers.Clear();
                sbyte offset = 1;
                ChunkCache chunkCache = new(worldObj, startX - offset, startY - offset, startZ - offset, endX + offset, endY + offset, endZ + offset);
                if (!chunkCache.getChunksEmpty_IDK())
                {
                    ++chunksUpdated;
                    RenderBlocks blockRenderer = new(chunkCache);

                    for (int currentPass = 0; currentPass < 2; currentPass++)
                    {
                        bool z12 = false;
                        bool rendererContainsBlocks = false;
                        bool blockFound = false;

                        for (int y = startY; y < endY; ++y)
                        {
                            for (int z = startZ; z < endZ; ++z)
                            {
                                for (int x = startX; x < endX; ++x)
                                {
                                    int blockId = chunkCache.getBlockId(x, y, z);
                                    if (blockId > 0)
                                    {
                                        if (!blockFound)
                                        {
                                            blockFound = true;

                                            tessellator.StartBuildingVBO(7);

                                            Minecraft.renderPipeline.ModelMatrix.PushMatrix();
                                            
                                            setupGLTranslation();
                                            
                                            tessellator.addTranslation((double)(-posX), (double)(-posY), (double)(-posZ));
                                        }

                                        if (currentPass == 0 && Block.blocksList[blockId].hasTileEntity())
                                        {
                                            TileEntity tileEntity = chunkCache.getBlockTileEntity(x, y, z);
                                            if (TileEntityRenderer.instance.hasSpecialRenderer(tileEntity))
                                            {
                                                tileEntityRenderers.Add(tileEntity);
                                            }
                                        }

                                        Block block = Block.blocksList[blockId];
                                        int renderPass = block.RenderBlockPass;
                                        if (renderPass != currentPass)
                                        {
                                            z12 = true;
                                        }
                                        else if (renderPass == currentPass)
                                        {
                                            rendererContainsBlocks |= blockRenderer.renderBlockByRenderType(block, x, y, z);
                                        }
                                    }
                                }
                            }
                        }

                        if (blockFound)
                        {
                            Minecraft.renderPipeline.ModelMatrix.PopMatrix();

                            byte[] meshData = tessellator.BuildCurrentVertexBuffer();
                            Profiler.startSection("allocatemeshdata");

                            if (meshDataAllocations[currentPass] != null)
                                meshAllocator.FreeData(meshDataAllocations[currentPass]!.Value);

                            if (meshData.Length > 0)
                            {
                                meshDataAllocations[currentPass] = meshAllocator.AllocateData(meshData, this, currentPass);
                            }
                            
                            Profiler.endSection();
                            
                            tessellator.setTranslation(0.0D, 0.0D, 0.0D);
                        }
                        else
                        {
                            rendererContainsBlocks = false;
                        }

                        if (rendererContainsBlocks)
                        {
                            skipRenderPass[currentPass] = false;
                        }

                        if (!z12)
                        {
                            break;
                        }
                    }
                }

                HashSet<object> hashSet22 = new HashSet<object>();
                hashSet22.AddAll(tileEntityRenderers);
                hashSet22.RemoveAll(hashSet21);
                tileEntities.AddRange(hashSet22);
                hashSet21.RemoveAll(tileEntityRenderers);
                tileEntities.RemoveAll(hashSet21);
                isChunkLit = Chunk.isLit;
                isInitialized = true;
            }
            Profiler.endSection();
        }

        public virtual float distanceToEntitySquared(Entity entity1)
        {
            float f2 = (float)(entity1.posX - posXPlus);
            float f3 = (float)(entity1.posY - posYPlus);
            float f4 = (float)(entity1.posZ - posZPlus);
            return f2 * f2 + f3 * f3 + f4 * f4;
        }

        public virtual void setDontDraw()
        {
            for (int i = 0; i < 2; ++i)
            {
                skipRenderPass[i] = true;
                if (meshDataAllocations[i] != null)
                {
                    meshAllocator.FreeData(meshDataAllocations[i]!.Value);
                    meshDataAllocations[i] = null;
                }
            }

            isInFrustum = false;
            isInitialized = false;
        }

        public virtual void stopRendering()
        {
            setDontDraw();
            worldObj = null;
        }

        public virtual void updateInFrustum(ICamera iCamera1)
        {
            this.isInFrustum = iCamera1.isBoundingBoxInFrustum(this.rendererBoundingBox);
        }

        public virtual void TessellateOcclusionQueryAABB()
        {
            if (AABBVBO != null)
                Tessellator.instance.TessellateOcclusionQuery(AABBVBO.Value.GLHandle);
        }

        public virtual bool skipAllRenderPasses()
        {
            return !this.isInitialized ? false : this.skipRenderPass[0] && this.skipRenderPass[1];
        }

        public virtual void markDirty()
        {
            needsUpdate = true;
        }
    }

}