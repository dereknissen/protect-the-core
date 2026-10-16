using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR

namespace VoxelImporter
{
    public class VoxelData
    {
        [System.Diagnostics.DebuggerDisplay("Position({x}, {y}, {z}), Palette({palette})")]
        public struct Voxel
        {
            public Voxel(int x, int y, int z, int palette = 0, VoxelBase.Face visible = VoxelBase.Face.forward | VoxelBase.Face.up | VoxelBase.Face.right | VoxelBase.Face.left | VoxelBase.Face.down | VoxelBase.Face.back)
            {
                this.x = (short)x;
                this.y = (short)y;
                this.z = (short)z;
                this.palette = (byte)palette;
                this.visible = visible;
            }

            public IntVector3 position { get { return new IntVector3(x, y, z); } set { x = (short)value.x; y = (short)value.y; z = (short)value.z; } }

            public short x;
            public short y;
            public short z;
            public byte palette;
            public VoxelBase.Face visible;
        }

        #region FaceArea
        public struct FaceArea
        {
            public IntVector3 min;
            public IntVector3 max;
            public int palette;
            public int material;

            public IntVector3 size { get { return max - min + IntVector3.one; } }
            public Vector3 minf { get { return new Vector3(min.x, min.y, min.z); } }
            public Vector3 maxf { get { return new Vector3(max.x, max.y, max.z); } }

            public IntVector3 Get(VoxelBase.VoxelVertexIndex index)
            {
                switch (index)
                {
                case VoxelBase.VoxelVertexIndex.XYZ: return new IntVector3(max.x, max.y, max.z);
                case VoxelBase.VoxelVertexIndex._XYZ: return new IntVector3(min.x, max.y, max.z);
                case VoxelBase.VoxelVertexIndex.X_YZ: return new IntVector3(max.x, min.y, max.z);
                case VoxelBase.VoxelVertexIndex.XY_Z: return new IntVector3(max.x, max.y, min.z);
                case VoxelBase.VoxelVertexIndex._X_YZ: return new IntVector3(min.x, min.y, max.z);
                case VoxelBase.VoxelVertexIndex._XY_Z: return new IntVector3(min.x, max.y, min.z);
                case VoxelBase.VoxelVertexIndex.X_Y_Z: return new IntVector3(max.x, min.y, min.z);
                case VoxelBase.VoxelVertexIndex._X_Y_Z: return new IntVector3(min.x, min.y, min.z);
                default: Assert.IsFalse(false); return IntVector3.zero;
                }
            }
        }
        public class FaceAreaTable
        {
            public List<FaceArea> forward = new List<FaceArea>();
            public List<FaceArea> up = new List<FaceArea>();
            public List<FaceArea> right = new List<FaceArea>();
            public List<FaceArea> left = new List<FaceArea>();
            public List<FaceArea> down = new List<FaceArea>();
            public List<FaceArea> back = new List<FaceArea>();

            public void Merge(FaceAreaTable src)
            {
                forward.AddRange(src.forward);
                up.AddRange(src.up);
                right.AddRange(src.right);
                left.AddRange(src.left);
                down.AddRange(src.down);
                back.AddRange(src.back);
            }

            public void ReplacePalette(int[] paletteTable)
            {
                for (int i = 0; i < forward.Count; i++)
                {
                    var faceArea = forward[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    forward[i] = faceArea;
                }
                for (int i = 0; i < up.Count; i++)
                {
                    var faceArea = up[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    up[i] = faceArea;
                }
                for (int i = 0; i < right.Count; i++)
                {
                    var faceArea = right[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    right[i] = faceArea;
                }
                for (int i = 0; i < left.Count; i++)
                {
                    var faceArea = left[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    left[i] = faceArea;
                }
                for (int i = 0; i < down.Count; i++)
                {
                    var faceArea = down[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    down[i] = faceArea;
                }
                for (int i = 0; i < back.Count; i++)
                {
                    var faceArea = back[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    back[i] = faceArea;
                }
            }

            public int GetMaxSideLength()
            {
                int max = 0;
                for (int i = 0; i < forward.Count; i++)
                {
                    max = Math.Max(max, forward[i].size.x);
                    max = Math.Max(max, forward[i].size.y);
                    max = Math.Max(max, forward[i].size.z);
                }
                for (int i = 0; i < up.Count; i++)
                {
                    max = Math.Max(max, up[i].size.x);
                    max = Math.Max(max, up[i].size.y);
                    max = Math.Max(max, up[i].size.z);
                }
                for (int i = 0; i < right.Count; i++)
                {
                    max = Math.Max(max, right[i].size.x);
                    max = Math.Max(max, right[i].size.y);
                    max = Math.Max(max, right[i].size.z);
                }
                for (int i = 0; i < left.Count; i++)
                {
                    max = Math.Max(max, left[i].size.x);
                    max = Math.Max(max, left[i].size.y);
                    max = Math.Max(max, left[i].size.z);
                }
                for (int i = 0; i < down.Count; i++)
                {
                    max = Math.Max(max, down[i].size.x);
                    max = Math.Max(max, down[i].size.y);
                    max = Math.Max(max, down[i].size.z);
                }
                for (int i = 0; i < back.Count; i++)
                {
                    max = Math.Max(max, back[i].size.x);
                    max = Math.Max(max, back[i].size.y);
                    max = Math.Max(max, back[i].size.z);
                }
                return max;
            }
        }
        #endregion

        #region VoxelTable
        private DataTable3<int> voxelTable;
        public List<ShortVector3> vertexList;
        private FlagTable3 outsideTable;

        public void CreateVoxelTable()
        {
            #region voxelTable
            {
                voxelTable = new DataTable3<int>(voxelSize.x, voxelSize.y, voxelSize.z);
                if (voxels != null)
                {
                    for (int i = 0; i < voxels.Length; i++)
                    {
                        voxelTable.Set(voxels[i].position, i);
                    }
                }
            }
            #endregion

            #region vertexList 
            {
                vertexList = new List<ShortVector3>(2 * voxels.Length);
                FlagTable3 doneTable = new FlagTable3(voxelSize.x + 1, voxelSize.y + 1, voxelSize.z + 1);
                void AddPoint(ShortVector3 pos)
                {
                   if (pos.x < 0 || pos.y < 0 || pos.z < 0) return;
                   if (!doneTable.Get(pos.x, pos.y, pos.z))
                   {
                       doneTable.Set(pos.x, pos.y, pos.z, true);
                       vertexList.Add(pos);
                   }
                }
                if (voxels != null)
                {
                    for (int i = 0; i < voxels.Length; i++)
                    {
                        AddPoint(new ShortVector3(voxels[i].x, voxels[i].y, voxels[i].z));
                        AddPoint(new ShortVector3((short)(voxels[i].x + 1), voxels[i].y, voxels[i].z));
                        AddPoint(new ShortVector3(voxels[i].x, (short)(voxels[i].y + 1), voxels[i].z));
                        AddPoint(new ShortVector3(voxels[i].x, voxels[i].y, (short)(voxels[i].z + 1)));
                        AddPoint(new ShortVector3((short)(voxels[i].x + 1), (short)(voxels[i].y + 1), voxels[i].z));
                        AddPoint(new ShortVector3((short)(voxels[i].x + 1), voxels[i].y, (short)(voxels[i].z + 1)));
                        AddPoint(new ShortVector3(voxels[i].x, (short)(voxels[i].y + 1), (short)(voxels[i].z + 1)));
                        AddPoint(new ShortVector3((short)(voxels[i].x + 1), (short)(voxels[i].y + 1), (short)(voxels[i].z + 1)));
                    }
                }
            }
            #endregion

            updateVoxelTableLastTimeTicks = DateTime.Now.Ticks;
        }
        public int VoxelTableContains(IntVector3 pos)
        {
            if (!voxelTable.Contains(pos))
                return -1;
            else
                return voxelTable.Get(pos);
        }
        public int VoxelTableContains(int x, int y, int z)
        {
            if (!voxelTable.Contains(x, y, z))
                return -1;
            else
                return voxelTable.Get(x, y, z);
        }

        public void CreateOutsideTable()
        {
            outsideTable = new FlagTable3(voxelSize.x, voxelSize.y, voxelSize.z);
            {
                var doneTable = new FlagTable3(voxelSize.x, voxelSize.y, voxelSize.z);

                var findList = new List<ShortVector3>();
                void AddFindList(int x, int y, int z)
                {
                    if (x < 0 || x >= voxelSize.x || y < 0 || y >= voxelSize.y || z < 0 || z >= voxelSize.z) return;
                    if (doneTable.Get(x, y, z)) return;
                    doneTable.Set(x, y, z, true);
                    if (VoxelTableContains(x, y, z) >= 0) return;
                    if (outsideTable.Get(x, y, z)) return;
                    findList.Add(new ShortVector3((short)x, (short)y, (short)z));
                    outsideTable.Set(x, y, z, true);
                }

                for (int x = 0; x < voxelSize.x; x++)
                {
                    for (int y = 0; y < voxelSize.y; y++)
                    {
                        AddFindList(x, y, 0);
                        AddFindList(x, y, voxelSize.z - 1);
                    }
                    for (int z = 0; z < voxelSize.z; z++)
                    {
                        AddFindList(x, 0, z);
                        AddFindList(x, voxelSize.y - 1, z);
                    }
                }
                for (int z = 0; z < voxelSize.z; z++)
                {
                    for (int y = 0; y < voxelSize.y; y++)
                    {
                        AddFindList(0, y, z);
                        AddFindList(voxelSize.x - 1, y, z);
                    }
                }

                var findListSub = new List<ShortVector3>(findList.Count);
                do
                {
                    {
                        var tmp = findList;
                        findList = findListSub;
                        findListSub = tmp;
                    }
                    findList.Clear();
                    for (int i = 0; i < findListSub.Count; i++)
                    {
                        var pos = findListSub[i];
                        AddFindList(pos.x + 1, pos.y, pos.z);
                        AddFindList(pos.x - 1, pos.y, pos.z);
                        AddFindList(pos.x, pos.y + 1, pos.z);
                        AddFindList(pos.x, pos.y - 1, pos.z);
                        AddFindList(pos.x, pos.y, pos.z + 1);
                        AddFindList(pos.x, pos.y, pos.z - 1);
                    }
                    findListSub.Clear();

                } while (findList.Count > 0);
            }
        }
        public bool OutsideTableContains(IntVector3 pos)
        {
            if (outsideTable == null)
                return true;
            if (pos.x < 0 || pos.x >= voxelSize.x ||
                pos.y < 0 || pos.y >= voxelSize.y ||
                pos.z < 0 || pos.z >= voxelSize.z)
                return true;
            else
                return outsideTable.Get(pos);
        }
        public bool OutsideTableContains(int x, int y, int z)
        {
            if (outsideTable == null)
                return true;
            if (x < 0 || x >= voxelSize.x ||
                y < 0 || y >= voxelSize.y ||
                z < 0 || z >= voxelSize.z)
                return true;
            else
                return outsideTable.Get(x, y, z);
        }
        #endregion

        #region Chunk
        public DataTable3<IntVector3> chunkTable;

        public struct ChunkArea
        {
            public IntVector3 min;
            public IntVector3 max;

            public Vector3 minf { get { return new Vector3(min.x, min.y, min.z); } }
            public Vector3 maxf { get { return new Vector3(max.x, max.y, max.z); } }

            public Vector3 GetPivot(Vector3 t)
            {
                return new Vector3( Mathf.LerpUnclamped(minf.x, maxf.x + 1f, t.x),
                                    Mathf.LerpUnclamped(minf.y, maxf.y + 1f, t.y),
                                    Mathf.LerpUnclamped(minf.z, maxf.z + 1f, t.z));
            }
        }
        [System.Diagnostics.DebuggerDisplay("Name({name})")]
        public struct ChunkData
        {
            public string name;
            public ChunkArea area;
        }

        public DataTable3<int> chunkIndexTable;
        public List<ChunkData> chunkDataList;
        #endregion

        #region Material
        public class VoxMaterial
        {
            public List<int> palattes;

            public enum Type
            {
                diffuse,
                metal,
                glass,
                emissive,
                plastic,
                cloud,
                blend,
            }
            public Type materialType;
            public float materialWeight;
            //old
            public uint propertyBits;
            public float[] normalizedPropertyValues;
            //new
            public Dictionary<string, float> materialParams;

            public float GetNormalizedPropertyValues(int index)
            {
                int count = 0;
                for (int i = 0; i < index; i++)
                {
                    if((propertyBits & (1<<i)) != 0)
                        count++;
                }
                if ((propertyBits & (1 << index)) != 0 && normalizedPropertyValues != null && count < normalizedPropertyValues.Length)
                    return normalizedPropertyValues[count];
                else
                    return 0f;
            }
        }
        #endregion

        public Voxel[] voxels;
        public Color[] palettes;
        public IntVector3 voxelSize;

        public List<VoxMaterial> materials;

        public long updateVoxelTableLastTimeTicks;
    }
}

#endif
