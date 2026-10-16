using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using System.Linq;

namespace VoxelImporter
{
    public abstract class VoxelBaseCore
    {
        public VoxelBaseCore(VoxelBase target)
        {
            voxelBase = target;
        }

        public VoxelBase voxelBase { get; protected set; }

        public virtual void Initialize()
        {
            if (voxelBase == null) return;

            if (voxelBase.dataVersion != VoxelBase.EditorDataVersion)
            {
                ReadyVoxelData();
            }
            voxelBase.EditorInitialize();
            voxelBase.EditorInitializeDone();

            if (voxelBase.voxelData == null)
                voxelBase.edit_configureMode = VoxelBase.Edit_ConfigureMode.None;

            AutoSetSelectedWireframeHidden();
        }

        #region AtlasRects
        protected class TextureBoundArea
        {
            public TextureBoundArea()
            {
                textureIndex = -1;
                min = new IntVector2(int.MaxValue, int.MaxValue);
                max = new IntVector2(int.MinValue, int.MinValue);
            }
            public void Set(IntVector2 pos)
            {
                min = IntVector2.Min(min, pos);
                max = IntVector2.Max(max, pos);
            }
            public IntVector2 Size { get { return max - min + IntVector2.one; } }
            public int textureIndex;
            public IntVector2 min;
            public IntVector2 max;
        }
        protected class AtlasRectTable
        {
            public List<TextureBoundArea> forward = new List<TextureBoundArea>();
            public List<TextureBoundArea> up = new List<TextureBoundArea>();
            public List<TextureBoundArea> right = new List<TextureBoundArea>();
            public List<TextureBoundArea> left = new List<TextureBoundArea>();
            public List<TextureBoundArea> down = new List<TextureBoundArea>();
            public List<TextureBoundArea> back = new List<TextureBoundArea>();
        }
        #endregion

        #region Chunk
        protected virtual void CreateChunkData() { }
        #endregion

        #region CreateVoxel
        public VoxelData voxelData
        {
            get
            {
                if (voxelBase.voxelData == null)
                    ReadyVoxelData();
                return voxelBase.voxelData;
            }
        }
        public bool Create(string path, UnityEngine.Object obj)
        {
            if (!IsEnableFile(path))
            {
                return false;
            }

            var undoGroupID = Undo.GetCurrentGroup();

            voxelBase.voxelFilePath = path;
            voxelBase.voxelFileGUID = null;
            voxelBase.voxelFileObject = obj;
            voxelBase.voxelFileSubIndex = -1;

            bool result = LoadVoxelData();
            if (result)
            {
                CreateMeshBefore();

                result = CreateMesh();

                CreateMeshAfter();
            }
            else
            {
                CreateMeshThrough();
            }

            voxelBase.fileRefreshLastTimeTicks = DateTime.Now.Ticks;

            voxelBase.edit_afterRefresh = false;

            RefreshCheckerSave();

            Undo.CollapseUndoOperations(undoGroupID);

            return result;
        }
        public virtual bool ReCreate()
        {
            #region Path
            if (voxelBase.voxelFileObject != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(voxelBase.voxelFileObject);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    voxelBase.voxelFilePath = EditorCommon.GetProjectRelativePath2FullPath(assetPath);
                }
            }
            #endregion
            return Create(voxelBase.voxelFilePath, voxelBase.voxelFileObject);
        }
        public virtual void Reset(string path, UnityEngine.Object obj)
        {
            voxelBase.edit_configureMode = VoxelBase.Edit_ConfigureMode.None;
            voxelBase.edit_configureMaterialIndex = -1;
            ClearVoxelData();
            voxelBase.voxelStructure = null;
            voxelBase.disableData = null;
            voxelBase.materialData = null;
            voxelBase.materialIndexes = null;
        }
        public virtual bool IsVoxelFileExists()
        {
            var fileExists = false;
            if (!string.IsNullOrEmpty(voxelBase.voxelFilePath))
            {
                fileExists = File.Exists(voxelBase.voxelFilePath);
            }
            if (!fileExists)
            {
                if (voxelBase.voxelFileObject != null && AssetDatabase.Contains(voxelBase.voxelFileObject))
                {
                    fileExists = true;
                }
            }
            return fileExists;
        }
        public bool IsEnableFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogErrorFormat("<color=green>[Voxel Importer]</color> Voxel file not found. <color=red>{0}</color>", path);
                return false;
            }
            if (GetFileType(path) < 0)
            {
                Debug.LogErrorFormat("<color=green>[Voxel Importer]</color> It is unsupported file. <color=red>{0}</color>", path);
                return false;
            }
            return true;
        }
        public VoxelBase.FileType GetFileType(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            if (ext == ".vox")
                return VoxelBase.FileType.vox;
            else if (ext == ".qb")
                return VoxelBase.FileType.qb;
            else if (ext == ".png")
                return VoxelBase.FileType.png;
            else
                return (VoxelBase.FileType)(-1);
        }
        public virtual void ClearVoxelData()
        {
            voxelBase.voxelData = null;
            voxelBase.voxelDataCreatedVoxelFileTimeTicks = 0;
        }
        public bool ReadyVoxelData(bool forceReload = false)
        {
            if (forceReload)
                ClearVoxelData();
            if (voxelBase.voxelData == null)
            {
                return LoadVoxelData();
            }
            return true;
        }
        protected virtual bool LoadVoxelData()
        {
            bool result = false;
            #region Path
            if (voxelBase.voxelFileObject != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(voxelBase.voxelFileObject);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    voxelBase.voxelFilePath = EditorCommon.GetProjectRelativePath2FullPath(assetPath);
                }
            }
            #endregion
            if (!string.IsNullOrEmpty(voxelBase.voxelFilePath) &&
                File.Exists(voxelBase.voxelFilePath))
            {
                var ticks = File.GetLastWriteTimeUtc(voxelBase.voxelFilePath).Ticks;
                if (voxelBase.voxelData == null || voxelBase.voxelDataCreatedVoxelFileTimeTicks != ticks)  //not update
                {
                    voxelBase.voxelDataCreatedVoxelFileTimeTicks = ticks;
                    using (BinaryReader br = new BinaryReader(File.Open(voxelBase.voxelFilePath, FileMode.Open)))
                    {
                        #region ProgressBar
                        const float MaxProgressCount = 4f;
                        float ProgressCount = 0;
                        Action<string> DisplayProgressBar = (info) =>
                        {
                            if (br.BaseStream.Length > 32 * 1024)
                                EditorUtility.DisplayProgressBar("Load Voxel File...", string.Format("{0} / {1} {2}", ProgressCount, MaxProgressCount, info), (ProgressCount++ / MaxProgressCount));
                        };
                        #endregion

                        DisplayProgressBar("Load File");

                        {
                            switch (GetFileType(voxelBase.voxelFilePath))
                            {
                            case VoxelBase.FileType.vox:
                                result = LoadVoxelDataFromVOX(br);
                                break;
                            case VoxelBase.FileType.qb:
                                result = LoadVoxelDataFromQB(br);
                                break;
                            case VoxelBase.FileType.png:
                                result = LoadVoxelDataFromPNG(br);
                                break;
                            default:
                                result = false;
                                break;
                            }
                        }
                        GC.Collect();

                        if (result)
                        {
                            DisplayProgressBar("Apply Import Flags");
                            ApplyImportFlags();
                            GC.Collect();

                            DisplayProgressBar("Create Voxel Table");
                            CreateVoxelTable();
                            GC.Collect();

                            DisplayProgressBar("Create Outside Table");
                            if (voxelBase.ignoreCavity)
                                CreateOutsideTable();
                            GC.Collect();
                        }

                        EditorUtility.ClearProgressBar();
                    }
                }
                else
                {
                    result = true;
                }
                if (result)
                {
                    UpdateVisibleFlags();
                    CreateChunkData();
                }
            }
            else
            {
                voxelBase.voxelDataCreatedVoxelFileTimeTicks = 0;
            }
            if (voxelBase.voxelData != null)
            {
                if (voxelBase.voxelData.materials == null)
                {
                    voxelBase.loadFromVoxelFile = false;
                }
                if (voxelBase.voxelStructure != null)
                {
                    voxelBase.voxelStructure.Set(voxelBase.voxelData);
                }
            }
            return result;
        }

        protected abstract class VoxBase
        {
            public int nodeID;
            public Dictionary<string, string> attributes;
        }
        protected class VoxTRN : VoxBase
        {
            public int childNodeID;
            public int reservedID;
            public int layerID;

            public class Frame
            {
                public Dictionary<string, string> frameAttributes;

                public Matrix4x4 matrix
                {
                    get
                    {
                        Matrix4x4 result = Matrix4x4.identity;
                        {
                            string param;
                            if (frameAttributes.TryGetValue("_r", out param))
                            {
                                var r = Convert.ToByte(param);
                                var indexRow0 = (r & 3);
                                var indexRow1 = (r & 12) >> 2;
                                var signRow0 = (r & 16) == 0;
                                var signRow1 = (r & 32) == 0;
                                var signRow2 = (r & 64) == 0;

                                result.SetRow(0, Vector4.zero);
                                switch (indexRow0)
                                {
                                case 0: result[0, 0] = signRow0 ? 1f : -1f; break;
                                case 1: result[0, 1] = signRow0 ? 1f : -1f; break;
                                case 2: result[0, 2] = signRow0 ? 1f : -1f; break;
                                }
                                result.SetRow(1, Vector4.zero);
                                switch (indexRow1)
                                {
                                case 0: result[1, 0] = signRow1 ? 1f : -1f; break;
                                case 1: result[1, 1] = signRow1 ? 1f : -1f; break;
                                case 2: result[1, 2] = signRow1 ? 1f : -1f; break;
                                }
                                result.SetRow(2, Vector4.zero);
                                switch (indexRow0 + indexRow1)
                                {
                                case 1: result[2, 2] = signRow2 ? 1f : -1f; break;
                                case 2: result[2, 1] = signRow2 ? 1f : -1f; break;
                                case 3: result[2, 0] = signRow2 ? 1f : -1f; break;
                                }
                            }
                        }
                        {
                            string param;
                            if (frameAttributes.TryGetValue("_t", out param))
                            {
                                var offsets = param.Split(' ');
                                if (offsets.Length == 3)
                                {
                                    var x = Convert.ToInt32(offsets[0]);
                                    var y = Convert.ToInt32(offsets[1]);
                                    var z = Convert.ToInt32(offsets[2]);
                                    result.SetColumn(3, new Vector4(x, y, z, 1f));
                                }
                            }
                        }
                        return result;
                    }
                }
            }
            public Frame[] frames;

            public string name
            {
                get
                {
                    string param;
                    if (attributes.TryGetValue("_name", out param))
                        return param;
                    return null;
                }
            }
            public bool hidden
            {
                get
                {
                    string param;
                    if (attributes.TryGetValue("_hidden", out param))
                        return Convert.ToInt32(param) != 0;
                    return false;
                }
            }
            public Matrix4x4 matrix
            {
                get
                {
                    if (frames != null && frames.Length > 0)
                        return frames[0].matrix;
                    return Matrix4x4.identity;
                }
            }
        }
        protected class VoxGRP : VoxBase
        {
            public int[] childNodeID;
        }
        protected class VoxSHP : VoxBase
        {
            public class Model
            {
                public int modelID;
                public Dictionary<string, string> modelAttributes;
            }
            public Model[] models;
        }
        protected class VoxLayer
        {
            public int layerID;
            public Dictionary<string, string> attributes;

            public string name
            {
                get
                {
                    string param;
                    if (attributes.TryGetValue("_name", out param))
                        return param;
                    return null;
                }
            }
            public bool hidden
            {
                get
                {
                    string param;
                    if (attributes.TryGetValue("_hidden", out param))
                        return Convert.ToInt32(param) != 0;
                    return false;
                }
            }
        }
        protected bool LoadVoxelDataFromVOX(BinaryReader br)
        {
            Func<string, bool> SeekChunk = (name) =>
            {
                Assert.IsTrue(name.Length == 4);
                if (br.BaseStream.Length - br.BaseStream.Position < name.Length)
                    return false;
                var position = br.BaseStream.Position;
                while (br.BaseStream.Length - br.BaseStream.Position >= name.Length)
                {
                    var data = ASCIIEncoding.ASCII.GetString(br.ReadBytes(name.Length));
                    if (name == data)
                        return true;
                    var n = br.ReadInt32();
                    var m = br.ReadInt32();
                    br.BaseStream.Seek(n + m, SeekOrigin.Current);
                }
                br.BaseStream.Position = position;
                return false;
            };
            Func<string, bool> CheckChunk = (name) =>
            {
                Assert.IsTrue(name.Length == 4);
                if (br.BaseStream.Length - br.BaseStream.Position < name.Length)
                    return false;
                var position = br.BaseStream.Position;
                {
                    var data = ASCIIEncoding.ASCII.GetString(br.ReadBytes(name.Length));
                    if (name == data)
                        return true;
                }
                br.BaseStream.Position = position;
                return false;
            };
            Func<string> GetString = () =>
            {
                var size = br.ReadInt32();
                return ASCIIEncoding.ASCII.GetString(br.ReadBytes(size));
            };

            if (!SeekChunk("VOX "))
            {
                Debug.LogError("<color=green>[Voxel Importer]</color> vox file error.");
                return false;
            }
            br.BaseStream.Seek(4, SeekOrigin.Current);  //version
            #region MAIN
            if (!SeekChunk("MAIN"))
            {
                Debug.LogError("<color=green>[Voxel Importer]</color> vox chunk error.");
                return false;
            }
            br.BaseStream.Seek(8, SeekOrigin.Current);
            #endregion
            #region PACK
            int packCount = 1;
            if (SeekChunk("PACK"))
            {
                br.BaseStream.Seek(8, SeekOrigin.Current);
                packCount = br.ReadInt32();
	            if (voxelBase.voxelFileSubIndex >= packCount)
	            {
	                Debug.LogErrorFormat("<color=green>[Voxel Importer]</color> Index out of range. index {0} - size {1}", voxelBase.voxelFileSubIndex, packCount);
	                return false;
	            }
            }
            #endregion
            #region Models
            int modelNum = 0;
            var modelsPosition = new List<long>();
            {
                var savePosition = br.BaseStream.Position;
                for (modelNum = 0; ; modelNum++)
                {
                    modelsPosition.Add(br.BaseStream.Position);
                    if (!SeekChunk("SIZE"))
                        break;
                    var n = br.ReadInt32();
                    var m = br.ReadInt32();
                    br.BaseStream.Seek(n + m, SeekOrigin.Current);
                }
                br.BaseStream.Position = savePosition;
            }
            if (modelNum == 0)
            {
                Debug.LogError("<color=green>[Voxel Importer]</color> vox chunk error.");
                return false;
            }
            #endregion

            IntVector3[] allVoxelSize = new IntVector3[modelNum];
            VoxelData.Voxel[][] allVoxels = new VoxelData.Voxel[modelNum][];
            Func<int, bool> LoadModel = (mi) =>
            {
                if (allVoxels[mi] != null)
                    return true;
                var savePosition = br.BaseStream.Position;
                br.BaseStream.Position = modelsPosition[mi];
                try
                {
                    IntVector3 size = IntVector3.zero;
                    #region SIZE
                    if (!SeekChunk("SIZE"))
                        return false;
                    br.BaseStream.Seek(8, SeekOrigin.Current);
                    {
                        var x = br.ReadInt32();
                        var z = br.ReadInt32();   //swapYZ
                        var y = br.ReadInt32();
                        size = new IntVector3(x, y, z);
                    }
                    #endregion
                    #region XYZI
                    if (!SeekChunk("XYZI"))
                        return false;
                    br.BaseStream.Seek(8, SeekOrigin.Current);
                    var numVoxels = br.ReadInt32();
                    List<VoxelData.Voxel> data = new List<VoxelData.Voxel>(numVoxels);
                    for (int i = 0; i < numVoxels; i++)
                    {
                        var x = size.x - 1 - br.ReadByte();  //invert
                        var z = size.z - 1 - br.ReadByte();   //swapYZ  //invert
                        var y = br.ReadByte();
                        var index = br.ReadByte();
                        if (index > 0)
                            data.Add(new VoxelData.Voxel(x, y, z, index - 1));
                    }
                    #endregion
                    allVoxelSize[mi] = size;
                    allVoxels[mi] = data.ToArray();
                    return true;
                }
                finally
                {
                    br.BaseStream.Position = savePosition;
                }
            };
            var modelIndex = voxelBase.voxelFileSubIndex >= 0 ? voxelBase.voxelFileSubIndex : 0;
            if (!LoadModel(modelIndex))
            {
                Debug.LogError("<color=green>[Voxel Importer]</color> vox chunk error.");
                return false;
            }
            var numberFormatInfo = CultureInfo.InvariantCulture.NumberFormat;
            var voxelSize = allVoxelSize[modelIndex];
            var voxels = allVoxels[modelIndex];
            List<int> chunkVoxelList = new List<int>(voxels.Length);
            List<VoxelData.ChunkData> chunkDataList = new List<VoxelData.ChunkData>();
            #region Version 0.99 World Editor
            bool isHaveTransform = false;
            Vector3 transformOffset = Vector3.zero;
            if (!voxelBase.legacyVoxImport && SeekChunk("nTRN"))
            {
                br.BaseStream.Seek(-4, SeekOrigin.Current);
                isHaveTransform = true;
                VoxTRN rootTRN = null;
                var nodeDic = new Dictionary<int, VoxBase>();
                var layerDic = new Dictionary<int, VoxLayer>();
                #region Read
                {
                    var savePosition = br.BaseStream.Position;
                    while (SeekChunk("LAYR"))
                    {
                        br.BaseStream.Seek(8, SeekOrigin.Current);
                        var layer = new VoxLayer();
                        layer.layerID = br.ReadInt32();
                        {
                            var numAttributes = br.ReadInt32();
                            layer.attributes = new Dictionary<string, string>(numAttributes);
                            for (int i = 0; i < numAttributes; i++)
                            {
                                var key = GetString();
                                var param = GetString();
                                layer.attributes.Add(key, param);
                            }
                        }
                        br.ReadInt32(); //unknown
                        layerDic.Add(layer.layerID, layer);
                    }
                    br.BaseStream.Position = savePosition;
                }
                while (true)
                {
                    if (CheckChunk("nTRN"))
                    {
                        #region VoxTRN
                        var trn = new VoxTRN();
                        br.BaseStream.Seek(8, SeekOrigin.Current);
                        trn.nodeID = br.ReadInt32();
                        {
                            {
                                var numAttributes = br.ReadInt32();
                                trn.attributes = new Dictionary<string, string>(numAttributes);
                                for (int i = 0; i < numAttributes; i++)
                                {
                                    var key = GetString();
                                    var param = GetString();
                                    trn.attributes.Add(key, param);
                                }
                            }
                            trn.childNodeID = br.ReadInt32();
                            trn.reservedID = br.ReadInt32();
                            trn.layerID = br.ReadInt32();
                            {
                                var numFrames = br.ReadInt32();
                                trn.frames = new VoxTRN.Frame[numFrames];
                                for (int i = 0; i < numFrames; i++)
                                {
                                    trn.frames[i] = new VoxTRN.Frame();
                                    var frameValueCount = br.ReadInt32();
                                    trn.frames[i].frameAttributes = new Dictionary<string, string>(frameValueCount);
                                    for (int j = 0; j < frameValueCount; j++)
                                    {
                                        var key = GetString();
                                        var param = GetString();
                                        trn.frames[i].frameAttributes.Add(key, param);
                                    }
                                }
                            }
                        }
                        nodeDic.Add(trn.nodeID, trn);
                        if (rootTRN == null)
                            rootTRN = trn;
                        #endregion
                    }
                    else if (CheckChunk("nGRP"))
                    {
                        #region VoxGRP
                        var grp = new VoxGRP();
                        br.BaseStream.Seek(8, SeekOrigin.Current);
                        {
                            grp.nodeID = br.ReadInt32();
                            var numAttributes = br.ReadInt32();
                            grp.attributes = new Dictionary<string, string>(numAttributes);
                            for (int i = 0; i < numAttributes; i++)
                            {
                                var key = GetString();
                                var param = GetString();
                                grp.attributes.Add(key, param);
                            }
                        }
                        {
                            var numChildrenNodes = br.ReadInt32();
                            grp.childNodeID = new int[numChildrenNodes];
                            for (int j = 0; j < numChildrenNodes; j++)
                            {
                                grp.childNodeID[j] = br.ReadInt32();
                            }
                        }
                        nodeDic.Add(grp.nodeID, grp);
                        #endregion
                    }
                    else if (CheckChunk("nSHP"))
                    {
                        #region VoxSHP
                        var shp = new VoxSHP();
                        br.BaseStream.Seek(8, SeekOrigin.Current);
                        {
                            shp.nodeID = br.ReadInt32();
                            var numAttributes = br.ReadInt32();
                            shp.attributes = new Dictionary<string, string>(numAttributes);
                            for (int i = 0; i < numAttributes; i++)
                            {
                                var key = GetString();
                                var param = GetString();
                                shp.attributes.Add(key, param);
                            }
                        }
                        {
                            var numModels = br.ReadInt32();
                            shp.models = new VoxSHP.Model[numModels];
                            for (int i = 0; i < numModels; i++)
                            {
                                shp.models[i] = new VoxSHP.Model();
                                shp.models[i].modelID = br.ReadInt32();

                                var numModelAttributes = br.ReadInt32();
                                shp.models[i].modelAttributes = new Dictionary<string, string>(numModelAttributes);
                                for (int j = 0; j < numModelAttributes; j++)
                                {
                                    var key = GetString();
                                    var param = GetString();
                                    shp.models[i].modelAttributes.Add(key, param);
                                }
                            }
                        }
                        nodeDic.Add(shp.nodeID, shp);
                        #endregion
                    }
                    else
                    {
                        break;
                    }
                }
                #endregion
                #region Write
                {
                    int chunkIndex = -1;
                    var newVoxels = new List<VoxelData.Voxel>();
                    Action<VoxTRN, Matrix4x4, VoxBase> WriteNode = null;
                    WriteNode = (parentTrn, matrix, node) =>
                    {
                        if (node is VoxTRN)
                        {
                            var trn = node as VoxTRN;
                            if (trn.hidden)
                                return;
                            if (layerDic.ContainsKey(trn.layerID))
                            {
                                var layer = layerDic[trn.layerID];
                                if (layer.hidden)
                                    return;
                            }
                            if (chunkIndex >= chunkDataList.Count)
                            {
                                chunkDataList.Add(new VoxelData.ChunkData()
                                {
                                    name = string.IsNullOrEmpty(trn.name) ? chunkIndex.ToString() : trn.name,
                                    area = new VoxelData.ChunkArea()
                                    {
                                        min = new IntVector3(int.MaxValue, int.MaxValue, int.MaxValue),
                                        max = new IntVector3(int.MinValue, int.MinValue, int.MinValue),
                                    },
                                });
                            }
                            if (trn.childNodeID >= 0)
                            {
                                matrix *= trn.matrix;
                                WriteNode(trn, matrix, nodeDic[trn.childNodeID]);
                            }
                        }
                        else if (node is VoxGRP)
                        {
                            var grp = node as VoxGRP;
                            if (grp.childNodeID != null)
                            {
                                foreach (var childID in grp.childNodeID)
                                {
                                    if (childID >= 0)
                                    {
                                        WriteNode(parentTrn, matrix, nodeDic[childID]);
                                    }
                                }
                            }
                        }
                        else if (node is VoxSHP)
                        {
                            var shp = node as VoxSHP;
                            foreach (var model in shp.models)
                            {
                                if (voxelBase.voxelFileSubIndex >= 0 &&
                                    model.modelID != voxelBase.voxelFileSubIndex)
                                    continue;
                                if (!LoadModel(model.modelID))
                                    return;
                                var origSize = allVoxelSize[model.modelID];
                                origSize.y = allVoxelSize[model.modelID].z; //swapYZ
                                origSize.z = allVoxelSize[model.modelID].y;
                                var pivot = new Vector3(origSize.x / 2, origSize.y / 2, origSize.z / 2);
                                var fpivot = new Vector3(origSize.x / 2f, origSize.y / 2f, origSize.z / 2f);
                                foreach (var voxel in allVoxels[model.modelID])
                                {
                                    var tmpVoxel = voxel;
                                    {
                                        IntVector3 origPos;
                                        origPos.x = allVoxelSize[model.modelID].x - 1 - tmpVoxel.x;  //invert
                                        origPos.y = allVoxelSize[model.modelID].z - 1 - tmpVoxel.z;  //swapYZ  //invert
                                        origPos.z = tmpVoxel.y;
                                        {
                                            Vector3 pos = new Vector3(origPos.x + 0.5f, origPos.y + 0.5f, origPos.z + 0.5f);

                                            pos -= pivot;
                                            pos = matrix.MultiplyPoint(pos);
                                            pos += pivot;

                                            pos.x += fpivot.x;
                                            pos.y += fpivot.y;
                                            pos.z -= fpivot.z;

                                            origPos.x = Mathf.FloorToInt(pos.x);
                                            origPos.y = Mathf.FloorToInt(pos.y);
                                            origPos.z = Mathf.FloorToInt(pos.z);
                                        }
                                        tmpVoxel.x = (short)(allVoxelSize[model.modelID].x - 1 - origPos.x);  //invert
                                        tmpVoxel.z = (short)(allVoxelSize[model.modelID].z - 1 - origPos.y);   //swapYZ  //invert
                                        tmpVoxel.y = (short)(origPos.z);
                                    }
                                    newVoxels.Add(tmpVoxel);
                                    chunkVoxelList.Add(chunkIndex);
                                }
                            }
                        }
                        else
                        {
                            Assert.IsTrue(false);
                        }
                    };
                    {
                        if (rootTRN is VoxTRN)
                        {
                            var trn = rootTRN as VoxTRN;
                            if (trn.childNodeID >= 0 && nodeDic[trn.childNodeID] is VoxGRP)
                            {
                                var grp = nodeDic[trn.childNodeID] as VoxGRP;
                                if (grp.childNodeID != null)
                                {
                                    foreach (var childID in grp.childNodeID)
                                    {
                                        if (childID >= 0)
                                        {
                                            chunkIndex++;
                                            var beforeCount = newVoxels.Count;
                                            WriteNode(trn, trn.matrix, nodeDic[childID]);
                                            if (beforeCount == newVoxels.Count)
                                                chunkIndex--;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogError("<color=green>[Voxel Importer]</color> VoxGRP error.");
                            }
                        }
                        else
                        {
                            Debug.LogError("<color=green>[Voxel Importer]</color> VoxTRN error.");
                        }
                    }
                    {
                        voxels = newVoxels.ToArray();
                        voxelSize = IntVector3.zero;
                        if (newVoxels.Count > 0)
                        {
                            {
                                IntVector3 min = new IntVector3(int.MaxValue, int.MaxValue, int.MaxValue);
                                foreach (var voxel in voxels)
                                    min = IntVector3.Min(min, voxel.position);
                                if (min.x < 0)
                                    transformOffset.x = Math.Abs(min.x);
                                if (min.y < 0)
                                    transformOffset.y = Math.Abs(min.y);
                                if (min.z < 0)
                                    transformOffset.z = Math.Abs(min.z);
                            }
                            {
                                IntVector3 transformOffsetI = new IntVector3(Mathf.RoundToInt(transformOffset.x), Mathf.RoundToInt(transformOffset.y), Mathf.RoundToInt(transformOffset.z));
                                for (int i = 0; i < voxels.Length; i++)
                                {
                                    voxels[i].position += transformOffsetI;
                                    voxelSize = IntVector3.Max(voxelSize, voxels[i].position + IntVector3.one);
                                }
                            }
                        }
                    }
                }
                #endregion
            }
            else
            {
                chunkDataList.Add(new VoxelData.ChunkData()
                {
                    name = 0.ToString(),
                    area = new VoxelData.ChunkArea()
                    {
                        min = new IntVector3(int.MaxValue, int.MaxValue, int.MaxValue),
                        max = new IntVector3(int.MinValue, int.MinValue, int.MinValue),
                    },
                });
                for (int i = 0; i < voxels.Length; i++)
                    chunkVoxelList.Add(0);
            }
            #endregion
            #region RGBA
            Color[] palettes = null;
            if (SeekChunk("RGBA"))
            {
                #region PaletteChunk
                br.BaseStream.Seek(8, SeekOrigin.Current);
                palettes = new Color[256];
                for (int i = 0; i < 256; i++)
                {
                    var r = br.ReadByte();
                    var g = br.ReadByte();
                    var b = br.ReadByte();
                    var a = br.ReadByte();
                    a = 0xff;
                    palettes[i] = new Color32(r, g, b, a);
                }
                #endregion
            }
            else
            {
                #region default palette
                Func<UInt32, Color> C = (color) =>
                {
                    return new Color32((byte)((color & 0x000000ff) >> 0),
                                        (byte)((color & 0x0000ff00) >> 8),
                                        (byte)((color & 0x00ff0000) >> 16),
                                        (byte)((color & 0xff000000) >> 24));
                };
                Color[] MagicaVoxelDefaultPalette =
                {
                    C(0xffffffff), C(0xffccffff), C(0xff99ffff), C(0xff66ffff), C(0xff33ffff), C(0xff00ffff), C(0xffffccff), C(0xffccccff), C(0xff99ccff), C(0xff66ccff), C(0xff33ccff), C(0xff00ccff), C(0xffff99ff), C(0xffcc99ff), C(0xff9999ff),
                    C(0xff6699ff), C(0xff3399ff), C(0xff0099ff), C(0xffff66ff), C(0xffcc66ff), C(0xff9966ff), C(0xff6666ff), C(0xff3366ff), C(0xff0066ff), C(0xffff33ff), C(0xffcc33ff), C(0xff9933ff), C(0xff6633ff), C(0xff3333ff), C(0xff0033ff), C(0xffff00ff),
                    C(0xffcc00ff), C(0xff9900ff), C(0xff6600ff), C(0xff3300ff), C(0xff0000ff), C(0xffffffcc), C(0xffccffcc), C(0xff99ffcc), C(0xff66ffcc), C(0xff33ffcc), C(0xff00ffcc), C(0xffffcccc), C(0xffcccccc), C(0xff99cccc), C(0xff66cccc), C(0xff33cccc),
                    C(0xff00cccc), C(0xffff99cc), C(0xffcc99cc), C(0xff9999cc), C(0xff6699cc), C(0xff3399cc), C(0xff0099cc), C(0xffff66cc), C(0xffcc66cc), C(0xff9966cc), C(0xff6666cc), C(0xff3366cc), C(0xff0066cc), C(0xffff33cc), C(0xffcc33cc), C(0xff9933cc),
                    C(0xff6633cc), C(0xff3333cc), C(0xff0033cc), C(0xffff00cc), C(0xffcc00cc), C(0xff9900cc), C(0xff6600cc), C(0xff3300cc), C(0xff0000cc), C(0xffffff99), C(0xffccff99), C(0xff99ff99), C(0xff66ff99), C(0xff33ff99), C(0xff00ff99), C(0xffffcc99),
                    C(0xffcccc99), C(0xff99cc99), C(0xff66cc99), C(0xff33cc99), C(0xff00cc99), C(0xffff9999), C(0xffcc9999), C(0xff999999), C(0xff669999), C(0xff339999), C(0xff009999), C(0xffff6699), C(0xffcc6699), C(0xff996699), C(0xff666699), C(0xff336699),
                    C(0xff006699), C(0xffff3399), C(0xffcc3399), C(0xff993399), C(0xff663399), C(0xff333399), C(0xff003399), C(0xffff0099), C(0xffcc0099), C(0xff990099), C(0xff660099), C(0xff330099), C(0xff000099), C(0xffffff66), C(0xffccff66), C(0xff99ff66),
                    C(0xff66ff66), C(0xff33ff66), C(0xff00ff66), C(0xffffcc66), C(0xffcccc66), C(0xff99cc66), C(0xff66cc66), C(0xff33cc66), C(0xff00cc66), C(0xffff9966), C(0xffcc9966), C(0xff999966), C(0xff669966), C(0xff339966), C(0xff009966), C(0xffff6666),
                    C(0xffcc6666), C(0xff996666), C(0xff666666), C(0xff336666), C(0xff006666), C(0xffff3366), C(0xffcc3366), C(0xff993366), C(0xff663366), C(0xff333366), C(0xff003366), C(0xffff0066), C(0xffcc0066), C(0xff990066), C(0xff660066), C(0xff330066),
                    C(0xff000066), C(0xffffff33), C(0xffccff33), C(0xff99ff33), C(0xff66ff33), C(0xff33ff33), C(0xff00ff33), C(0xffffcc33), C(0xffcccc33), C(0xff99cc33), C(0xff66cc33), C(0xff33cc33), C(0xff00cc33), C(0xffff9933), C(0xffcc9933), C(0xff999933),
                    C(0xff669933), C(0xff339933), C(0xff009933), C(0xffff6633), C(0xffcc6633), C(0xff996633), C(0xff666633), C(0xff336633), C(0xff006633), C(0xffff3333), C(0xffcc3333), C(0xff993333), C(0xff663333), C(0xff333333), C(0xff003333), C(0xffff0033),
                    C(0xffcc0033), C(0xff990033), C(0xff660033), C(0xff330033), C(0xff000033), C(0xffffff00), C(0xffccff00), C(0xff99ff00), C(0xff66ff00), C(0xff33ff00), C(0xff00ff00), C(0xffffcc00), C(0xffcccc00), C(0xff99cc00), C(0xff66cc00), C(0xff33cc00),
                    C(0xff00cc00), C(0xffff9900), C(0xffcc9900), C(0xff999900), C(0xff669900), C(0xff339900), C(0xff009900), C(0xffff6600), C(0xffcc6600), C(0xff996600), C(0xff666600), C(0xff336600), C(0xff006600), C(0xffff3300), C(0xffcc3300), C(0xff993300),
                    C(0xff663300), C(0xff333300), C(0xff003300), C(0xffff0000), C(0xffcc0000), C(0xff990000), C(0xff660000), C(0xff330000), C(0xff0000ee), C(0xff0000dd), C(0xff0000bb), C(0xff0000aa), C(0xff000088), C(0xff000077), C(0xff000055), C(0xff000044),
                    C(0xff000022), C(0xff000011), C(0xff00ee00), C(0xff00dd00), C(0xff00bb00), C(0xff00aa00), C(0xff008800), C(0xff007700), C(0xff005500), C(0xff004400), C(0xff002200), C(0xff001100), C(0xffee0000), C(0xffdd0000), C(0xffbb0000), C(0xffaa0000),
                    C(0xff880000), C(0xff770000), C(0xff550000), C(0xff440000), C(0xff220000), C(0xff110000), C(0xffeeeeee), C(0xffdddddd), C(0xffbbbbbb), C(0xffaaaaaa), C(0xff888888), C(0xff777777), C(0xff555555), C(0xff444444), C(0xff222222), C(0xff111111), C(0xff000000),
                };
                palettes = new Color[256];
                for (int i = 0; i < 256; i++)
                {
                    palettes[i] = MagicaVoxelDefaultPalette[i];
                }
                #endregion
            }
            #endregion
            #region Material
            List<VoxelData.VoxMaterial> materials = null;
            if (SeekChunk("MATT"))   //0.98 Only?
            {
                #region MATT
                br.BaseStream.Seek(-4, SeekOrigin.Current);
                while (SeekChunk("MATT"))
                {
                    var n = br.ReadInt32();
                    br.BaseStream.Seek(4, SeekOrigin.Current);
                    var index = br.ReadInt32() - 1;
                    var materialType = (VoxelData.VoxMaterial.Type)br.ReadInt32();
                    var materialWeight = br.ReadSingle();
                    var propertyBits = br.ReadUInt32();
                    var valueCount = (n - 16) / 4;
                    var normalizedPropertyValues = new float[valueCount];
                    for (int i = 0; i < valueCount; i++)
                        normalizedPropertyValues[i] = br.ReadSingle();
                    if (materialType == VoxelData.VoxMaterial.Type.diffuse)
                        continue;
                    var material = new VoxelData.VoxMaterial()
                    {
                        palattes = new List<int>(),
                        materialType = materialType,
                        materialWeight = materialWeight,
                        propertyBits = propertyBits,
                        normalizedPropertyValues = normalizedPropertyValues,
                        materialParams = new Dictionary<string, float>(),
                    };
                    if (index >= 0)
                    {
                        material.palattes.Add(index);
                        if (materials == null)
                            materials = new List<VoxelData.VoxMaterial>();
                        materials.Add(material);
                    }
                }
                #endregion
            }
            else if (SeekChunk("MATL")) //0.99 or later
            {
                #region MATL
                br.BaseStream.Seek(-4, SeekOrigin.Current);
                while (SeekChunk("MATL"))
                {
                    br.BaseStream.Seek(8, SeekOrigin.Current);
                    var index = br.ReadInt32() - 1;
                    var valueCount = br.ReadInt32();
                    VoxelData.VoxMaterial.Type materialType = VoxelData.VoxMaterial.Type.diffuse;
                    float materialWeight = 0f;
                    UInt32 propertyBits = 0x000000ff;
                    var normalizedPropertyValues = new float[8];
                    var materialParams = new Dictionary<string, float>();
                    for (int i = 0; i < valueCount; i++)
                    {
                        var type = GetString();
                        var param = GetString();
                        switch (type)
                        {
                        case "_type":
                            switch (param)
                            {
                                case "_diffuse": materialType = VoxelData.VoxMaterial.Type.diffuse; break;
                                case "_metal": materialType = VoxelData.VoxMaterial.Type.metal; break;
                                case "_glass": materialType = VoxelData.VoxMaterial.Type.glass; break;
                                case "_emit": materialType = VoxelData.VoxMaterial.Type.emissive; break;
                                case "_plastic": materialType = VoxelData.VoxMaterial.Type.plastic; break;
                                case "_media": materialType = VoxelData.VoxMaterial.Type.cloud; break;
                                case "_blend": materialType = VoxelData.VoxMaterial.Type.blend; break;
                                default:
                                    Debug.LogWarningFormat("<color=green>[Voxel Importer]</color> Unknown material type => {0}", param);
                                    break;
                            }
                            break;
                        case "_weight":
                            materialWeight = Convert.ToSingle(param, numberFormatInfo);
                            break;
                        case "_plastic":
                            normalizedPropertyValues[0] = Convert.ToSingle(param, numberFormatInfo);  //| bit(0) : Plastic
                            materialParams.Add(type, normalizedPropertyValues[0]);
                            break;
                        case "_rough":
                            normalizedPropertyValues[1] = Convert.ToSingle(param, numberFormatInfo);  //| bit(1) : Roughness
                            materialParams.Add(type, normalizedPropertyValues[1]);
                            break;
                        case "_spec":
                            normalizedPropertyValues[2] = Convert.ToSingle(param, numberFormatInfo);  //| bit(2) : Specular
                            materialParams.Add(type, normalizedPropertyValues[2]);
                            break;
                        case "_ior":
                            normalizedPropertyValues[3] = Convert.ToSingle(param, numberFormatInfo);  //| bit(3) : IOR
                            materialParams.Add(type, normalizedPropertyValues[3]);
                            break;
                        case "_att":
                            normalizedPropertyValues[4] = Convert.ToSingle(param, numberFormatInfo);  //| bit(4) : Attenuation
                            materialParams.Add(type, normalizedPropertyValues[4]);
                            break;
                        case "_flux":
                            normalizedPropertyValues[5] = Convert.ToSingle(param, numberFormatInfo);  //| bit(5) : Power
                            materialParams.Add(type, normalizedPropertyValues[5]);
                            break;
                        case "_glow":
                            normalizedPropertyValues[6] = Convert.ToSingle(param, numberFormatInfo);  //| bit(6) : Glow
                            materialParams.Add(type, normalizedPropertyValues[6]);
                            break;
                        case "_unit":
                            normalizedPropertyValues[7] = Convert.ToSingle(param, numberFormatInfo);  //| bit(7) : isTotalPower (*no value)
                            materialParams.Add(type, normalizedPropertyValues[7]);
                            break;
                        case "_media_type":
                            //unknown
                            break;
                        default:
                            {
                                float single;
                                try
                                {
                                    single = Convert.ToSingle(param, numberFormatInfo);
                                }
                                catch
                                {
                                    single = 0f;
                                }
                                materialParams.Add(type, single);
                            }
                            break;
                        }
                    }
                    if (materialType == VoxelData.VoxMaterial.Type.diffuse)
                        continue;
                    if (index >= 0)
                    {
                        var material = new VoxelData.VoxMaterial()
                        {
                            palattes = new List<int>(),
                            materialType = materialType,
                            materialWeight = materialWeight,
                            propertyBits = propertyBits,
                            normalizedPropertyValues = normalizedPropertyValues,
                            materialParams = materialParams,
                        };
                        material.palattes.Add(index);
                        if (materials == null)
                            materials = new List<VoxelData.VoxMaterial>();
                        materials.Add(material);
                    }
                }
                #endregion
            }
            #endregion

            #region compress palette and material
            {
                int[] paletteCount = new int[palettes.Length];
                if (voxelBase.removeUnusedPalettes)
                {
                    for (int i = 0; i < voxels.Length; i++)
                    {
                        paletteCount[voxels[i].palette]++;
                    }
                    int[] removeCount = new int[palettes.Length];
                    for (int i = 0; i < paletteCount.Length; i++)
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (paletteCount[j] == 0)
                                removeCount[i]++;
                        }
                    }
                    for (int i = 0; i < voxels.Length; i++)
                    {
                        int palette = voxels[i].palette - removeCount[voxels[i].palette];
                        voxels[i].palette = (byte)palette;
                    }
                }
                List<Color> paletteList = new List<Color>(palettes.Length);
                List<VoxelData.VoxMaterial> materialList = null;
                Func<int, VoxelData.VoxMaterial> FindMaterials = (palette) =>
                {
                    if (materials == null)
                        return null;
                    foreach (var material in materials)
                    {
                        if (material.palattes.Contains(palette))
                            return material;
                    }
                    return null;
                };
                Func<VoxelData.VoxMaterial, int, bool> CompressMaterial = (srcMaterial, palette) =>
                {
                    if (materialList == null)
                        return false;
                    foreach (var material in materialList)
                    {
                        if (material.materialType != srcMaterial.materialType ||
                            material.materialWeight != srcMaterial.materialWeight ||
                            material.propertyBits != srcMaterial.propertyBits)
                            continue;
                        {
                            bool result = true;
                            for (int i = 0; i < material.normalizedPropertyValues.Length; i++)
                            {
                                if (material.normalizedPropertyValues[i] != srcMaterial.normalizedPropertyValues[i])
                                {
                                    result = false;
                                    break;
                                }
                            }
                            if (!result) continue;
                        }
                        material.palattes.Add(palette);
                        return true;
                    }
                    return false;
                };
                for (int i = 0; i < paletteCount.Length; i++)
                {
                    if (!voxelBase.removeUnusedPalettes ||
                        paletteCount[i] > 0)
                    {
                        var srcMaterial = FindMaterials(i);
                        if (srcMaterial != null)
                        {
                            var index = paletteList.Count;
                            if (!voxelBase.shareSameMaterial || 
                                !CompressMaterial(srcMaterial, index))
                            {
                                var material = new VoxelData.VoxMaterial()
                                {
                                    palattes = new List<int>(),
                                    materialType = srcMaterial.materialType,
                                    materialWeight = srcMaterial.materialWeight,
                                    propertyBits = srcMaterial.propertyBits,
                                    normalizedPropertyValues = srcMaterial.normalizedPropertyValues,
                                    materialParams = srcMaterial.materialParams,
                                };
                                material.palattes.Add(index);
                                if (materialList == null)
                                    materialList = new List<VoxelData.VoxMaterial>();
                                materialList.Add(material);
                            }
                        }
                        paletteList.Add(palettes[i]);
                    }
                }
                palettes = paletteList.ToArray();
                materials = materialList;
            }
            #endregion

            if (isHaveTransform)
            {
                voxelBase.localOffset = new Vector3(-transformOffset.x, -transformOffset.y, -transformOffset.z);
            }
            else
            {
                voxelBase.localOffset = new Vector3(-((float)voxelSize.x / 2), 0f, -((float)voxelSize.z / 2));
            }

            voxelBase.fileType = VoxelBase.FileType.vox;

            voxelBase.voxelData = new VoxelData();
            voxelBase.voxelData.voxels = voxels;
            voxelBase.voxelData.palettes = palettes;
            voxelBase.voxelData.voxelSize = voxelSize;
            voxelBase.voxelData.materials = materials;

            DataTable3<int> chunkIndexTable = new DataTable3<int>(voxelSize.x, voxelSize.y, voxelSize.z);
            for (int i = 0; i < voxels.Length; i++)
            {
                chunkIndexTable.Set(voxels[i].position, chunkVoxelList[i]);
                var data = chunkDataList[chunkVoxelList[i]];
                data.area.min = IntVector3.Min(data.area.min, voxels[i].position);
                data.area.max = IntVector3.Max(data.area.max, voxels[i].position);
                chunkDataList[chunkVoxelList[i]] = data;
            }
            voxelBase.voxelData.chunkIndexTable = chunkIndexTable;
            voxelBase.voxelData.chunkDataList = chunkDataList;

            if (voxelBase.loadFromVoxelFile && materials != null)
            {
                voxelBase.materialData = new List<MaterialData>();
                voxelBase.materialData.Add(new MaterialData());
                foreach (var material in materials)
                {
                    Color32 color32 = palettes[material.palattes[0]];
                    var materialData = new MaterialData()
                    {
                        name = string.Format("{0} {1} {2}", color32.r, color32.g, color32.b),
                    };
                    switch (material.materialType)
                    {
                    case VoxelData.VoxMaterial.Type.diffuse:
                        materialData.material.renderingMode = MaterialData.Material.StandardShaderRenderingMode.Opaque;
                        break;
                    case VoxelData.VoxMaterial.Type.metal:
                        {
                            materialData.material.renderingMode = MaterialData.Material.StandardShaderRenderingMode.Opaque;
                            if (material.materialParams.ContainsKey("_metal"))  //0.99.6.2
                            {
                                materialData.material.metallic = material.materialParams["_metal"];
                                float _rough;
                                material.materialParams.TryGetValue("_rough", out _rough);
                                materialData.material.smoothness = 1f - _rough;
                            }
                            else
                            {
                                materialData.material.metallic = material.materialWeight;
                                materialData.material.smoothness = 1f - material.GetNormalizedPropertyValues(1);   //bit(1) : Roughness
                            }
                        }
                        break;
                    case VoxelData.VoxMaterial.Type.glass:
                        {
                            materialData.material.renderingMode = MaterialData.Material.StandardShaderRenderingMode.Transparent;
                            if (material.materialParams.ContainsKey("_alpha"))  //0.99.6.2
                            {
                                materialData.material.alpha = 1f - material.materialParams["_alpha"];
                                float _rough;
                                material.materialParams.TryGetValue("_rough", out _rough);
                                materialData.material.smoothness = 1f - _rough;
                            }
                            else
                            {
                                materialData.material.alpha = 1f - material.materialWeight;
                                materialData.material.smoothness = 1f - material.GetNormalizedPropertyValues(1);   //bit(1) : Roughness
                            }
                            materialData.transparent = true;
                        }
                        break;
                    case VoxelData.VoxMaterial.Type.emissive:
                        {
                            materialData.material.renderingMode = MaterialData.Material.StandardShaderRenderingMode.Opaque;
                            if (material.materialParams.ContainsKey("_emit"))  //0.99.6.2
                            {
                                materialData.material.emission = Color.Lerp(Color.black, Color.white, material.materialParams["_emit"]);
                                float _flux;
                                material.materialParams.TryGetValue("_flux", out _flux);
                                materialData.material.emissionPower = Mathf.Lerp(2f, 12f, _flux / 4f);   //bit(5) : Power
                            }
                            else
                            {
                                materialData.material.emission = Color.Lerp(Color.black, Color.white, material.materialWeight);
                                materialData.material.emissionPower = Mathf.Lerp(2f, 12f, material.GetNormalizedPropertyValues(5) / 4f);   //bit(5) : Power
                            }
                        }
                        break;
                    case VoxelData.VoxMaterial.Type.plastic:
                        {
                            materialData.material.renderingMode = MaterialData.Material.StandardShaderRenderingMode.Opaque;
                            materialData.material.metallic = material.materialWeight;
                            materialData.material.smoothness = 1f - material.GetNormalizedPropertyValues(1);   //bit(1) : Roughness]
                        }
                        break;
                    case VoxelData.VoxMaterial.Type.cloud:
                        {
                            materialData.material.renderingMode = MaterialData.Material.StandardShaderRenderingMode.Transparent;
                            if (material.materialParams.ContainsKey("_alpha"))  //0.99.6.2
                            {
                                materialData.material.alpha = 1f - material.materialParams["_alpha"];
                                float _d;
                                material.materialParams.TryGetValue("_d", out _d);
                                materialData.material.alpha *= _d * 10f;
                            }                            
                            else if (material.materialParams.ContainsKey("_d"))  //0.99.7.1
                            {
                                float _d;
                                material.materialParams.TryGetValue("_d", out _d);
                                materialData.material.alpha = _d;
                            }
                            else
                            {
                                materialData.material.alpha = 1f - material.materialWeight;
                            }
                            materialData.transparent = true;
                        }
                        break;
                    case VoxelData.VoxMaterial.Type.blend:  //0.99.6.2
                        {
                            float _alpha, _metal, _rough;
                            material.materialParams.TryGetValue("_alpha", out _alpha);
                            materialData.material.renderingMode = _alpha > 0f ? MaterialData.Material.StandardShaderRenderingMode.Transparent : MaterialData.Material.StandardShaderRenderingMode.Opaque;
                            materialData.material.alpha = 1f - _alpha;
                            material.materialParams.TryGetValue("_metal", out _metal);
                            materialData.material.metallic = _metal;
                            material.materialParams.TryGetValue("_rough", out _rough);
                            materialData.material.smoothness = 1f - _rough;
                            materialData.transparent = _alpha > 0f;
                        }
                        break;
                    }
                    for (int i = 0; i < voxels.Length; i++)
                    {
                        if (material.palattes.Contains(voxels[i].palette))
                        {
                            materialData.SetMaterial(voxels[i].position);
                        }
                    }
                    voxelBase.materialData.Add(materialData);
                }
            }

            return true;
        }
        protected bool LoadVoxelDataFromQB(BinaryReader br)
        {
            br.BaseStream.Seek(4, SeekOrigin.Current);  //version
            var colorFormat = br.ReadUInt32();
            var zAxisOrientation = br.ReadUInt32();
            var compressed = br.ReadUInt32();
            br.BaseStream.Seek(4, SeekOrigin.Current);  //visibilityMaskEncoded
            var numMatrices = br.ReadUInt32();

            List<VoxelData.Voxel> voxelList = new List<VoxelData.Voxel>();
            Dictionary<Color, int> paletteList = new Dictionary<Color, int>();
            Dictionary<int, Dictionary<int, HashSet<int>>> doneTable = new Dictionary<int, Dictionary<int, HashSet<int>>>();

            int chunkIndex = -1;
            List<int> chunkVoxelList = new List<int>();
            List<VoxelData.ChunkData> chunkDataList = new List<VoxelData.ChunkData>();

            Action<int, int, int, UInt32> AddVoxel = (x, y, z, data) =>
           {
               Color color;
               if (colorFormat == 0)
               {
                   var a = (byte)((data & 0xff000000) >> 24);
                   var b = (byte)((data & 0x00ff0000) >> 16);
                   var g = (byte)((data & 0x0000ff00) >> 8);
                   var r = (byte)((data & 0x000000ff));
                   color = new Color32(r, g, b, a);
               }
               else
               {
                   var a = (byte)((data & 0xff000000) >> 24);
                   var r = (byte)((data & 0x00ff0000) >> 16);
                   var g = (byte)((data & 0x0000ff00) >> 8);
                   var b = (byte)((data & 0x000000ff));
                   color = new Color32(r, g, b, a);
               }
               if (color.a > 0f)
               {
                   if (!doneTable.ContainsKey(x) ||
                       !doneTable[x].ContainsKey(y) ||
                       !doneTable[x][y].Contains(z))
                   {
                       if (!doneTable.ContainsKey(x))
                           doneTable.Add(x, new Dictionary<int, HashSet<int>>());
                       if (!doneTable[x].ContainsKey(y))
                           doneTable[x].Add(y, new HashSet<int>());
                       doneTable[x][y].Add(z);

                       color.a = 1f;
                       int palette;
                       if (paletteList.ContainsKey(color))
                       {
                           palette = paletteList[color];
                       }
                       else
                       {
                           palette = paletteList.Count;
                           paletteList.Add(color, palette);
                       }

                       voxelList.Add(new VoxelData.Voxel(x, y, z, palette));
                       chunkVoxelList.Add(chunkIndex);
                   }
               }
           };

            for (int i = 0; i < numMatrices; i++)
            {
                var nameLength = br.ReadByte();
                var name = ASCIIEncoding.ASCII.GetString(br.ReadBytes(nameLength));
                var sizeX = br.ReadUInt32();
                var sizeY = br.ReadUInt32();
                var sizeZ = br.ReadUInt32();
                var posX = br.ReadInt32();
                var posY = br.ReadInt32();
                var posZ = br.ReadInt32();

                chunkIndex = i;
                chunkDataList.Add(new VoxelData.ChunkData()
                {
                    name = name,
                    area = new VoxelData.ChunkArea()
                    {
                        min = new IntVector3(int.MaxValue, int.MaxValue, int.MaxValue),
                        max = new IntVector3(int.MinValue, int.MinValue, int.MinValue),
                    },
                });

                if (compressed == 0)
                {
                    for (int zi = 0; zi < sizeZ; zi++)
                    {
                        for (int yi = 0; yi < sizeY; yi++)
                        {
                            for (int xi = 0; xi < sizeX; xi++)
                            {
                                var x = (posX + xi);
                                var y = (posY + yi);
                                var z = (zAxisOrientation == 0 ? -(posZ + zi + 1) : (posZ + zi));
                                AddVoxel(x, y, z, br.ReadUInt32());
                            }
                        }
                    }
                }
                else
                {
                    const UInt32 CODEFLAG = 2;
                    const UInt32 NEXTSLICEFLAG = 6;
                    int zi = 0;
                    while (zi < sizeZ)
                    {
                        zi++;
                        int index = 0;
                        while (true)
                        {
                            var data = br.ReadUInt32();
                            if (data == NEXTSLICEFLAG)
                                break;
                            else if (data == CODEFLAG)
                            {
                                var count = br.ReadUInt32();
                                data = br.ReadUInt32();

                                for (int j = 0; j < count; j++)
                                {
                                    int xi = (int)(index % sizeX) + 1;
                                    int yi = (int)(index / sizeX) + 1;
                                    index++;

                                    var x = (posX + xi) - 1;
                                    var y = (posY + yi) - 1;
                                    var z = (zAxisOrientation == 0 ? -(posZ + zi) : (posZ + zi) - 1);
                                    AddVoxel(x, y, z, data);
                                }
                            }
                            else
                            {
                                int xi = (int)(index % sizeX) + 1;
                                int yi = (int)(index / sizeX) + 1;
                                index++;

                                var x = (posX + xi) - 1;
                                var y = (posY + yi) - 1;
                                var z = (zAxisOrientation == 0 ? -(posZ + zi) : (posZ + zi) - 1);
                                AddVoxel(x, y, z, data);
                            }
                        }
                    }
                }
            }

            IntVector3 voxelSize;
            {
                IntVector3 min = new IntVector3(int.MaxValue, int.MaxValue, int.MaxValue);
                IntVector3 max = new IntVector3(int.MinValue, int.MinValue, int.MinValue);
                for (int i = 0; i < voxelList.Count; i++)
                {
                    //invert
                    {
                        var voxel = voxelList[i];
                        voxel.x = (short)(-voxel.x - 1);
                        voxel.z = (short)(-voxel.z - 1);
                        voxelList[i] = voxel;
                    }

                    min = IntVector3.Min(min, voxelList[i].position);
                    max = IntVector3.Max(max, voxelList[i].position);
                }

                voxelSize = max - min + IntVector3.one;
                for (int i = 0; i < voxelList.Count; i++)
                {
                    var v = voxelList[i];
                    v.position -= min;
                    voxelList[i] = v;
                }
                voxelBase.localOffset = new Vector3(min.x, min.y, min.z);
            }

            var voxels = voxelList.ToArray();
            var palettes = new Color[paletteList.Count];
            foreach (var pair in paletteList)
            {
                palettes[pair.Value] = pair.Key;
            }

            voxelBase.fileType = VoxelBase.FileType.qb;

            voxelBase.voxelData = new VoxelData();
            voxelBase.voxelData.voxels = voxels;
            voxelBase.voxelData.palettes = palettes;
            voxelBase.voxelData.voxelSize = voxelSize;

            DataTable3<int> chunkIndexTable = new DataTable3<int>(voxelSize.x, voxelSize.y, voxelSize.z);
            for (int i = 0; i < voxelList.Count; i++)
            {
                chunkIndexTable.Set(voxelList[i].position, chunkVoxelList[i]);
                var data = chunkDataList[chunkVoxelList[i]];
                data.area.min = IntVector3.Min(data.area.min, voxelList[i].position);
                data.area.max = IntVector3.Max(data.area.max, voxelList[i].position);
                chunkDataList[chunkVoxelList[i]] = data;
            }
            voxelBase.voxelData.chunkIndexTable = chunkIndexTable;
            voxelBase.voxelData.chunkDataList = chunkDataList;

            return true;
        }
        protected bool LoadVoxelDataFromPNG(BinaryReader br)
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
            tex.hideFlags = HideFlags.DontSave;
            if (!tex.LoadImage(br.ReadBytes((int)br.BaseStream.Length)))
                return false;

            Rect rect = new Rect(0, 0, tex.width, tex.height);
            Vector2 spritePivot = new Vector2(0.5f, 0f);
            Vector2 texScale = Vector2.one;
            #region Sprite
            if (voxelBase.voxelFileObject != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(voxelBase.voxelFileObject);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (voxelBase.voxelFileObject is Texture2D)
                    {
                        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                        if (importer != null)
                        {
                            switch (importer.spriteImportMode)
                            {
                            case SpriteImportMode.Single:
                                //spritePivot = importer.spritePivot;     //have bug
                                {
                                    var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                                    for (int i = 0; i < sprites.Length; i++)
                                    {
                                        if (sprites[i].texture == voxelBase.voxelFileObject)
                                        {
                                            spritePivot = new Vector2(sprites[i].pivot.x / tex.width, sprites[i].pivot.y / tex.height);
                                            break;
                                        }
                                    }
                                }
                                break;
                            }
                        }
                    }
                    else if (voxelBase.voxelFileObject is Sprite)
                    {
                        var sprite = voxelBase.voxelFileObject as Sprite;
                        rect = sprite.rect;
                        texScale = new Vector2(tex.width / (float)sprite.texture.width, tex.height / (float)sprite.texture.height);
                        spritePivot = new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);
                    }
                }
            }
            #endregion

            IntVector3 voxelSize = new IntVector3(Mathf.CeilToInt(rect.width), Mathf.CeilToInt(rect.height), 1);
            VoxelData.Voxel[] voxels;
            Color[] palettes;
            {
                List<VoxelData.Voxel> voxelList = new List<VoxelData.Voxel>(Mathf.CeilToInt(rect.width) * Mathf.CeilToInt(rect.height));
                Dictionary<Color, int> paletteList = new Dictionary<Color, int>();
                for (int x = 0; x < rect.width; x++)
                {
                    for (int y = 0; y < rect.height; y++)
                    {
                        var color = tex.GetPixel(Mathf.FloorToInt(texScale.x * (x + rect.xMin)),
                                                 Mathf.FloorToInt(texScale.y * (y + rect.yMin)));
                        if (color.a <= 0f) continue;
                        color.a = 1f;
                        int index;
                        if (paletteList.ContainsKey(color))
                        {
                            index = paletteList[color];
                        }
                        else
                        {
                            index = paletteList.Count;
                            paletteList.Add(color, index);
                        }
                        voxelList.Add(new VoxelData.Voxel(x, y, 0, index));
                    }
                }
                voxels = voxelList.ToArray();
                palettes = new Color[paletteList.Count];
                foreach (var pair in paletteList)
                {
                    palettes[pair.Value] = pair.Key;
                }
            }

            voxelBase.localOffset = new Vector3(-((float)voxelSize.x * spritePivot.x), -((float)voxelSize.y * spritePivot.y), -((float)voxelSize.z / 2));

            voxelBase.fileType = VoxelBase.FileType.png;

            voxelBase.voxelData = new VoxelData();
            voxelBase.voxelData.voxels = voxels;
            voxelBase.voxelData.palettes = palettes;
            voxelBase.voxelData.voxelSize = voxelSize;

            return true;
        }
        protected void ApplyImportFlags()
        {
            VoxelData.Voxel[] vs = voxelData.voxels;
            if ((voxelBase.importFlags & (VoxelBase.ImportFlag.FlipX | VoxelBase.ImportFlag.FlipY | VoxelBase.ImportFlag.FlipZ)) != 0)
            {
                vs = new VoxelData.Voxel[voxelData.voxels.Length];
                for (int i = 0; i < vs.Length; i++)
                {
                    vs[i] = voxelData.voxels[i];
                    if ((voxelBase.importFlags & VoxelBase.ImportFlag.FlipX) != 0) vs[i].x = (short)(voxelData.voxelSize.x - 1 - vs[i].x);
                    if ((voxelBase.importFlags & VoxelBase.ImportFlag.FlipY) != 0) vs[i].y = (short)(voxelData.voxelSize.y - 1 - vs[i].y);
                    if ((voxelBase.importFlags & VoxelBase.ImportFlag.FlipZ) != 0) vs[i].z = (short)(voxelData.voxelSize.z - 1 - vs[i].z);
                }
            }
            voxelData.voxels = vs;
        }
        protected virtual void CreateVoxelTable()
        {
            voxelBase.voxelData.CreateVoxelTable();
        }
        protected virtual void CreateOutsideTable()
        {
            voxelBase.voxelData.CreateOutsideTable();
        }
        protected virtual void UpdateVisibleFlags()
        {
            VoxelData.Voxel[] vs = voxelData.voxels;
            if (voxelBase.ignoreCavity)
            {
                for (int i = 0; i < vs.Length; i++)
                {
                    vs[i].visible = 0;
                    if (voxelData.VoxelTableContains(vs[i].x, vs[i].y, vs[i].z + 1) < 0 && voxelData.OutsideTableContains(vs[i].x, vs[i].y, vs[i].z + 1))
                        vs[i].visible |= VoxelBase.Face.forward;
                    if (voxelData.VoxelTableContains(vs[i].x, vs[i].y + 1, vs[i].z) < 0 && voxelData.OutsideTableContains(vs[i].x, vs[i].y + 1, vs[i].z))
                        vs[i].visible |= VoxelBase.Face.up;
                    if (voxelData.VoxelTableContains(vs[i].x + 1, vs[i].y, vs[i].z) < 0 && voxelData.OutsideTableContains(vs[i].x + 1, vs[i].y, vs[i].z))
                        vs[i].visible |= VoxelBase.Face.right;
                    if (voxelData.VoxelTableContains(vs[i].x - 1, vs[i].y, vs[i].z) < 0 && voxelData.OutsideTableContains(vs[i].x - 1, vs[i].y, vs[i].z))
                        vs[i].visible |= VoxelBase.Face.left;
                    if (voxelData.VoxelTableContains(vs[i].x, vs[i].y - 1, vs[i].z) < 0 && voxelData.OutsideTableContains(vs[i].x, vs[i].y - 1, vs[i].z))
                        vs[i].visible |= VoxelBase.Face.down;
                    if (voxelData.VoxelTableContains(vs[i].x, vs[i].y, vs[i].z - 1) < 0 && voxelData.OutsideTableContains(vs[i].x, vs[i].y, vs[i].z - 1))
                        vs[i].visible |= VoxelBase.Face.back;
                }
            }
            else
            {
                for (int i = 0; i < vs.Length; i++)
                {
                    vs[i].visible = 0;
                    if (voxelData.VoxelTableContains(vs[i].x, vs[i].y, vs[i].z + 1) < 0)
                        vs[i].visible |= VoxelBase.Face.forward;
                    if (voxelData.VoxelTableContains(vs[i].x, vs[i].y + 1, vs[i].z) < 0)
                        vs[i].visible |= VoxelBase.Face.up;
                    if (voxelData.VoxelTableContains(vs[i].x + 1, vs[i].y, vs[i].z) < 0)
                        vs[i].visible |= VoxelBase.Face.right;
                    if (voxelData.VoxelTableContains(vs[i].x - 1, vs[i].y, vs[i].z) < 0)
                        vs[i].visible |= VoxelBase.Face.left;
                    if (voxelData.VoxelTableContains(vs[i].x, vs[i].y - 1, vs[i].z) < 0)
                        vs[i].visible |= VoxelBase.Face.down;
                    if (voxelData.VoxelTableContains(vs[i].x, vs[i].y, vs[i].z - 1) < 0)
                        vs[i].visible |= VoxelBase.Face.back;
                }
            }
        }
        public virtual string GetDefaultPath()
        {
            var path = Application.dataPath;
            var prefabType = PrefabUtility.GetPrefabAssetType(voxelBase.gameObject);
            if (isPrefabEditMode)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                path = prefabStage.assetPath;
            }
            else if (prefabType == PrefabAssetType.Regular || prefabType == PrefabAssetType.Variant)
            {
                var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(voxelBase.gameObject);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    path = Path.GetDirectoryName(assetPath);
                }
            }
            return path;
        }
        public int GetVoxelFileSubCount(string path)
        {
            if (!File.Exists(path))
                return 0;

            if (GetFileType(path) == VoxelBase.FileType.vox)
            {
                using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open)))
                {
                    Func<string, bool> SeekChunk = (name) =>
                    {
                        Assert.IsTrue(name.Length == 4);
                        if (br.BaseStream.Length - br.BaseStream.Position < name.Length)
                            return false;
                        var position = br.BaseStream.Position;
                        while (br.BaseStream.Length - br.BaseStream.Position >= name.Length)
                        {
                            var data = ASCIIEncoding.ASCII.GetString(br.ReadBytes(name.Length));
                            if (name == data)
                                return true;
                            var n = br.ReadInt32();
                            var m = br.ReadInt32();
                            br.BaseStream.Seek(n + m, SeekOrigin.Current);
                        }
                        br.BaseStream.Position = position;
                        return false;
                    };
                    if (SeekChunk("VOX "))
                    {
                        br.BaseStream.Seek(4, SeekOrigin.Current);  //version
                        if (SeekChunk("MAIN"))
                            br.BaseStream.Seek(8, SeekOrigin.Current);
                        if (SeekChunk("PACK"))
                        {
                            br.BaseStream.Seek(8, SeekOrigin.Current);
                            return br.ReadInt32();
                        }
                        else
                        {
                            int modelNum = 0;
                            {
                                for (modelNum = 0; ; modelNum++)
                                {
                                    if (!SeekChunk("SIZE"))
                                        break;
                                    var n = br.ReadInt32();
                                    var m = br.ReadInt32();
                                    br.BaseStream.Seek(n + m, SeekOrigin.Current);
                                }
                            }
                            return modelNum;
                        }
                    }
                }
            }

            return 0;
        }
        #endregion

        #region CalcData
        protected VoxelBase.Face[] voxelDoneFaces;
        protected void CalcDataCreate(VoxelData.Voxel[] voxels)
        {
            #region voxelDoneFaces
            voxelDoneFaces = new VoxelBase.Face[voxelData.voxels.Length];
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    var index = voxelData.VoxelTableContains(voxels[i].position);
                    var material = voxelBase.GetMaterialIndexTable(voxels[i].position);
                    if ((voxels[i].visible & VoxelBase.Face.forward) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.forward))
                    {
                        var nearMaterial = voxelBase.GetMaterialIndexTable(new IntVector3(voxels[i].position.x, voxels[i].position.y, voxels[i].position.z + 1));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.forward;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.up) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.up))
                    {
                        var nearMaterial = voxelBase.GetMaterialIndexTable(new IntVector3(voxels[i].position.x, voxels[i].position.y + 1, voxels[i].position.z));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.up;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.right) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.right))
                    {
                        var nearMaterial = voxelBase.GetMaterialIndexTable(new IntVector3(voxels[i].position.x + 1, voxels[i].position.y, voxels[i].position.z));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.right;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.left) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.left))
                    {
                        var nearMaterial = voxelBase.GetMaterialIndexTable(new IntVector3(voxels[i].position.x - 1, voxels[i].position.y, voxels[i].position.z));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.left;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.down) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.down))
                    {
                        var nearMaterial = voxelBase.GetMaterialIndexTable(new IntVector3(voxels[i].position.x, voxels[i].position.y - 1, voxels[i].position.z));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.down;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.back) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.back))
                    {
                        var nearMaterial = voxelBase.GetMaterialIndexTable(new IntVector3(voxels[i].position.x, voxels[i].position.y, voxels[i].position.z - 1));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.back;
                        }
                    }
                }
                #region Disable
                if (voxelBase.disableData != null)
                {
                    voxelBase.disableData.AllAction((position, face) =>
                    {
                        var index = voxelData.VoxelTableContains(position);
                        if (index >= 0)
                            voxelDoneFaces[index] |= face;
                    });
                }
                #endregion
            }
            #endregion
        }
        protected void CalcDataRelease()
        {
            voxelDoneFaces = null;
        }
        protected void SetDoneFacesFlag(VoxelData.FaceArea faceArea, VoxelBase.Face flag)
        {
            for (int x = faceArea.min.x; x <= faceArea.max.x; x++)
            {
                for (int y = faceArea.min.y; y <= faceArea.max.y; y++)
                {
                    for (int z = faceArea.min.z; z <= faceArea.max.z; z++)
                    {
                        var index = voxelData.VoxelTableContains(x, y, z);
                        Assert.IsTrue(index >= 0);
                        voxelDoneFaces[index] |= flag;
                    }
                }
            }
        }
        protected int[] GetVoxelIndexTable(VoxelData.Voxel[] voxels)
        {
            var voxelIndexTable = new int[voxels.Length];
            for (int i = 0; i < voxels.Length; i++)
            {
                var index = voxelData.VoxelTableContains(voxels[i].position);
                voxelIndexTable[i] = index;
            }
            return voxelIndexTable;
        }
        #endregion

        #region CreateTexture
        protected bool CreateTexture(VoxelData.FaceAreaTable faceAreaTable, Color[] palettes, ref AtlasRectTable atlasRectTable, ref Texture2D atlasTexture, ref Rect[] atlasRects)
        {
            if (voxelBase.importMode == VoxelBase.ImportMode.LowTexture)
                return CreateTexture_LowTexture(palettes, ref atlasRectTable, ref atlasTexture, ref atlasRects);
            else if (voxelBase.importMode == VoxelBase.ImportMode.LowPoly)
                return CreateTexture_LowPoly(faceAreaTable, ref atlasRectTable, ref atlasTexture, ref atlasRects);
            else
                return false;
        }
        protected bool CreateTexture_LowTexture(Color[] palettes, ref AtlasRectTable atlasRectTable, ref Texture2D atlasTexture, ref Rect[] atlasRects)
        {
            if (voxelData == null) return false;

            var textures = new Texture2D[palettes.Length];
            for (int i = 0; i < palettes.Length; i++)
            {
                textures[i] = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                textures[i].hideFlags |= HideFlags.HideAndDontSave;
                textures[i].name = palettes[i].ToString();
                for (int x = 0; x < textures[i].width; x++)
                {
                    for (int y = 0; y < textures[i].height; y++)
                    {
                        textures[i].SetPixel(x, y, palettes[i]);
                    }
                }
                textures[i].Apply();
            }

            //Texture
            {
                Texture2D tex;
                if (atlasTexture == null || EditorCommon.IsMainAsset(atlasTexture))
                {
                    tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
                }
                else
                {
                    tex = atlasTexture;
                }
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                atlasRects = tex.PackTextures(textures, 0, 8192);
                #region Fill
                {
                    var pixels = tex.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i].a = 0xff;
                    }
                    tex.SetPixels32(pixels);
                    tex.Apply();
                }
                #endregion
                #region Mipmap
                if (voxelBase.generateMipMaps)
                {
                    var pixels = tex.GetPixels32();
                    tex.Reinitialize(tex.width, tex.height, tex.format, true);
                    tex.SetPixels32(pixels, 0);
                    tex.Apply(true);
                }
                #endregion
                #region UV
                for (int i = 0; i < atlasRects.Length; i++)
                {
                    atlasRects[i].center += atlasRects[i].size / 2;
                    atlasRects[i].size = Vector2.zero;
                }
                #endregion
                if (EditorCommon.IsMainAsset(atlasTexture))
                {
                    var path = AssetDatabase.GetAssetPath(atlasTexture);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    AssetDatabase.ImportAsset(path);
                    Texture2D.DestroyImmediate(tex);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                atlasTexture = tex;
            }
            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D.DestroyImmediate(textures[i]);
            }
            textures = null;

            return true;
        }

        private class LowPolyTextureBuffer : IDisposable
        {
            ~LowPolyTextureBuffer()
            {
                Assert.IsNull(texture);
            }
            public void Dispose()
            {
                if (texture != null)
                {
                    Texture2D.DestroyImmediate(texture);
                    texture = null;
                }
            }

            public int index;
            public Color[,] colors;
            public Texture2D texture;
        }
        protected bool CreateTexture_LowPoly(VoxelData.FaceAreaTable faceAreaTable, ref AtlasRectTable atlasRectTable, ref Texture2D atlasTexture, ref Rect[] atlasRects)
        {
            if (voxelData == null) return false;

            Assert.IsNotNull(faceAreaTable);

            var bufferList = new List<LowPolyTextureBuffer>();
            var bufferDic = new Dictionary<IntVector2, List<LowPolyTextureBuffer>>();

            Color[,] texColors;
            {
                var maxSize = faceAreaTable.GetMaxSideLength();
                texColors = new Color[maxSize, maxSize];
            }

            Func<int, int, TextureBoundArea, int> AddTexture = (texSizeX, texSizeY, bound) =>
            {
                #region shareSameFace
                if (voxelBase.shareSameFace)
                {
                    List<LowPolyTextureBuffer> bufferChunk;
                    if (bufferDic.TryGetValue(new IntVector2(texSizeX, texSizeY), out bufferChunk))
                    {
                        for (int i = 0; i < bufferChunk.Count; i++)
                        {
                            bool wrong = false;
                            for (int x = 0; x < texSizeX; x++)
                            {
                                for (int y = 0; y < texSizeY; y++)
                                {
                                    if (bufferChunk[i].colors[x, y] != texColors[x, y])
                                    {
                                        wrong = true;
                                        break;
                                    }
                                }
                                if (wrong) break;
                            }
                            if (!wrong)
                            {
                                return bufferChunk[i].index;
                            }
                        }
                    }
                }
                #endregion

                var size = bound.Size;
                Texture2D newTex = new Texture2D(size.x + 2, size.y + 2, TextureFormat.ARGB32, false);
                newTex.hideFlags |= HideFlags.HideAndDontSave;
                for (int x = 0; x < size.x; x++)
                {
                    for (int y = 0; y < size.y; y++)
                    {
                        newTex.SetPixel(1 + x, 1 + y, texColors[x, y]);
                    }
                }
                #region Bordering
                newTex.SetPixel(0, 0, texColors[0, 0]);
                newTex.SetPixel(1 + size.x, 0, texColors[size.x - 1, 0]);
                newTex.SetPixel(0, 1 + size.y, texColors[0, size.y - 1]);
                newTex.SetPixel(1 + size.x, 1 + size.y, texColors[size.x - 1, size.y - 1]);
                for (int x = 0; x < size.x; x++)
                {
                    newTex.SetPixel(1 + x, 0, texColors[x, 0]);
                    newTex.SetPixel(1 + x, 1 + size.y, texColors[x, size.y - 1]);
                }
                for (int y = 0; y < size.y; y++)
                {
                    newTex.SetPixel(0, 1 + y, texColors[0, y]);
                    newTex.SetPixel(1 + size.x, 1 + y, texColors[size.x - 1, y]);
                }
                #endregion
                newTex.Apply();

                var colors = new Color[texSizeX, texSizeY];
                for (int x = 0; x < texSizeX; x++)
                {
                    for (int y = 0; y < texSizeY; y++)
                        colors[x, y] = texColors[x, y];
                }

                var bufferIndex = bufferList.Count;
                var newBuffer = new LowPolyTextureBuffer()
                {
                    index = bufferIndex,
                    colors = colors,
                    texture = newTex,
                };
                bufferList.Add(newBuffer);

                {
                    var sizePos = new IntVector2(texSizeX, texSizeY);
                    List<LowPolyTextureBuffer> bufferChunk;
                    if (!bufferDic.TryGetValue(sizePos, out bufferChunk))
                        bufferChunk = new List<LowPolyTextureBuffer>();
                    bufferChunk.Add(newBuffer);
                    bufferDic[sizePos] = bufferChunk;
                }

                return bufferIndex;
            };

            atlasRectTable = new AtlasRectTable();
            #region forward 
            {
                atlasRectTable.forward.Capacity = faceAreaTable.forward.Count;
                for (int i = 0; i < faceAreaTable.forward.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.forward[i].min.z == faceAreaTable.forward[i].max.z);
                    int z = faceAreaTable.forward[i].min.z;
                    TextureBoundArea bound = null;
                    for (int x = faceAreaTable.forward[i].min.x; x <= faceAreaTable.forward[i].max.x; x++)
                    {
                        for (int y = faceAreaTable.forward[i].min.y; y <= faceAreaTable.forward[i].max.y; y++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.forward))
                            {
                                var index = voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                texColors[x - faceAreaTable.forward[i].min.x, y - faceAreaTable.forward[i].min.y] = voxelData.palettes[voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(x, y));
                            }
                            else
                            {
                                texColors[x - faceAreaTable.forward[i].min.x, y - faceAreaTable.forward[i].min.y] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = AddTexture(faceAreaTable.forward[i].size.x, faceAreaTable.forward[i].size.y, bound);
                    }
                    atlasRectTable.forward.Add(bound);
                }
            }
            #endregion
            #region up 
            {
                atlasRectTable.up.Capacity = faceAreaTable.up.Count;
                for (int i = 0; i < faceAreaTable.up.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.up[i].min.y == faceAreaTable.up[i].max.y);
                    int y = faceAreaTable.up[i].min.y;
                    TextureBoundArea bound = null;
                    for (int x = faceAreaTable.up[i].min.x; x <= faceAreaTable.up[i].max.x; x++)
                    {
                        for (int z = faceAreaTable.up[i].min.z; z <= faceAreaTable.up[i].max.z; z++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.up))
                            {
                                var index = voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                texColors[x - faceAreaTable.up[i].min.x, z - faceAreaTable.up[i].min.z] = voxelData.palettes[voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(x, z));
                            }
                            else
                            {
                                texColors[x - faceAreaTable.up[i].min.x, z - faceAreaTable.up[i].min.z] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = AddTexture(faceAreaTable.up[i].size.x, faceAreaTable.up[i].size.z, bound);
                    }
                    atlasRectTable.up.Add(bound);
                }
            }
            #endregion
            #region right 
            {
                atlasRectTable.right.Capacity = faceAreaTable.right.Count;
                for (int i = 0; i < faceAreaTable.right.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.right[i].min.x == faceAreaTable.right[i].max.x);
                    int x = faceAreaTable.right[i].min.x;
                    TextureBoundArea bound = null;
                    for (int y = faceAreaTable.right[i].min.y; y <= faceAreaTable.right[i].max.y; y++)
                    {
                        for (int z = faceAreaTable.right[i].min.z; z <= faceAreaTable.right[i].max.z; z++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.right))
                            {
                                var index = voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                texColors[y - faceAreaTable.right[i].min.y, z - faceAreaTable.right[i].min.z] = voxelData.palettes[voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(y, z));
                            }
                            else
                            {
                                texColors[y - faceAreaTable.right[i].min.y, z - faceAreaTable.right[i].min.z] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = AddTexture(faceAreaTable.right[i].size.y, faceAreaTable.right[i].size.z, bound);
                    }
                    atlasRectTable.right.Add(bound);
                }
            }
            #endregion
            #region left 
            {
                atlasRectTable.left.Capacity = faceAreaTable.left.Count;
                for (int i = 0; i < faceAreaTable.left.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.left[i].min.x == faceAreaTable.left[i].max.x);
                    int x = faceAreaTable.left[i].min.x;
                    TextureBoundArea bound = null;
                    for (int y = faceAreaTable.left[i].min.y; y <= faceAreaTable.left[i].max.y; y++)
                    {
                        for (int z = faceAreaTable.left[i].min.z; z <= faceAreaTable.left[i].max.z; z++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.left))
                            {
                                var index = voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                texColors[y - faceAreaTable.left[i].min.y, z - faceAreaTable.left[i].min.z] = voxelData.palettes[voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(y, z));
                            }
                            else
                            {
                                texColors[y - faceAreaTable.left[i].min.y, z - faceAreaTable.left[i].min.z] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = AddTexture(faceAreaTable.left[i].size.y, faceAreaTable.left[i].size.z, bound);
                    }
                    atlasRectTable.left.Add(bound);
                }
            }
            #endregion
            #region down 
            {
                atlasRectTable.down.Capacity = faceAreaTable.down.Count;
                for (int i = 0; i < faceAreaTable.down.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.down[i].min.y == faceAreaTable.down[i].max.y);
                    int y = faceAreaTable.down[i].min.y;
                    TextureBoundArea bound = null;
                    for (int x = faceAreaTable.down[i].min.x; x <= faceAreaTable.down[i].max.x; x++)
                    {
                        for (int z = faceAreaTable.down[i].min.z; z <= faceAreaTable.down[i].max.z; z++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.down))
                            {
                                var index = voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                texColors[x - faceAreaTable.down[i].min.x, z - faceAreaTable.down[i].min.z] = voxelData.palettes[voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(x, z));
                            }
                            else
                            {
                                texColors[x - faceAreaTable.down[i].min.x, z - faceAreaTable.down[i].min.z] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = AddTexture(faceAreaTable.down[i].size.x, faceAreaTable.down[i].size.z, bound);
                    }
                    atlasRectTable.down.Add(bound);
                }
            }
            #endregion
            #region back 
            {
                atlasRectTable.back.Capacity = faceAreaTable.back.Count;
                for (int i = 0; i < faceAreaTable.back.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.back[i].min.z == faceAreaTable.back[i].max.z);
                    int z = faceAreaTable.back[i].min.z;
                    TextureBoundArea bound = null;
                    for (int x = faceAreaTable.back[i].min.x; x <= faceAreaTable.back[i].max.x; x++)
                    {
                        for (int y = faceAreaTable.back[i].min.y; y <= faceAreaTable.back[i].max.y; y++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.back))
                            {
                                var index = voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                texColors[x - faceAreaTable.back[i].min.x, y - faceAreaTable.back[i].min.y] = voxelData.palettes[voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(x, y));
                            }
                            else
                            {
                                texColors[x - faceAreaTable.back[i].min.x, y - faceAreaTable.back[i].min.y] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = AddTexture(faceAreaTable.back[i].size.x, faceAreaTable.back[i].size.y, bound);
                    }
                    atlasRectTable.back.Add(bound);
                }
            }
            #endregion

            //Texture
            {
                var textures = bufferList
                    .Select(x => x.texture)
                    .ToArray();

                Texture2D tex;
                if (atlasTexture == null || EditorCommon.IsMainAsset(atlasTexture))
                {
                    tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
                }
                else
                {
                    tex = atlasTexture;
                }
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                atlasRects = tex.PackTextures(textures, 0, 8192);
                #region Bordering
                {
                    Color color = Color.clear;
                    int count = 0;
                    Action<int, int> AddPixel = (xx, yy) =>
                    {
                        var c = tex.GetPixel(xx, yy);
                        if (c.a > 0f)
                        {
                            color += c;
                            count++;
                        }
                    };

                    var pixels = tex.GetPixels32();
                    for (int x = 0; x < tex.width; x++)
                    {
                        for (int y = 0; y < tex.height; y++)
                        {
                            color = tex.GetPixel(x, y);
                            if (color.a <= 0f)
                            {
                                color = Color.clear;
                                count = 0;
                                if (x > 0) AddPixel(x - 1, y);
                                if (x < tex.width - 1) AddPixel(x + 1, y);
                                if (y > 0) AddPixel(x, y - 1);
                                if (y < tex.height - 1) AddPixel(x, y + 1);
                                if (count == 0)
                                {
                                    if (x > 0 && y > 0) AddPixel(x - 1, y - 1);
                                    if (x < tex.width - 1 && y > 0) AddPixel(x + 1, y - 1);
                                    if (x > 0 && y < tex.height - 1) AddPixel(x - 1, y + 1);
                                    if (x < tex.width - 1 && y < tex.height - 1) AddPixel(x + 1, y + 1);
                                }
                                if (count > 0)
                                {
                                    color = color / (float)count;
                                    color.a = 1f;
                                }
                                pixels[x + y * tex.width] = color;
                            }
                            else
                            {
                                color.a = 1f;
                                pixels[x + y * tex.width] = color;
                            }
                        }
                    }
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    for (int x = 0; x < tex.width; x++)
                    {
                        for (int y = 0; y < tex.height; y++)
                        {
                            color = pixels[x + y * tex.width];
                            if (color.a > 0f) continue;
                            int distanceMax = int.MaxValue;
                            int unionCount = 1;
                            const int BorderSize = 4;
                            for (int i = 1; i < BorderSize; i++)
                            {
                                var xx = ((x + i) % tex.width);
                                var subColor = tex.GetPixel(xx, y);
                                if (subColor.a > 0f)
                                {
                                    if (i < distanceMax)
                                    {
                                        unionCount = 1;
                                        distanceMax = i;
                                        color = subColor;
                                    }
                                    else if (i == distanceMax)
                                    {
                                        unionCount++;
                                        color += subColor;
                                    }
                                    break;
                                }
                            }
                            for (int i = 1; i < BorderSize; i++)
                            {
                                var xx = x - i;
                                if (xx < 0) xx += tex.width;
                                var subColor = tex.GetPixel(xx, y);
                                if (subColor.a > 0f)
                                {
                                    if (i < distanceMax)
                                    {
                                        unionCount = 1;
                                        distanceMax = i;
                                        color = subColor;
                                    }
                                    else if (i == distanceMax)
                                    {
                                        unionCount++;
                                        color += subColor;
                                    }
                                    break;
                                }
                            }
                            for (int i = 1; i < BorderSize; i++)
                            {
                                var yy = ((y + i) % tex.height);
                                var subColor = tex.GetPixel(x, yy);
                                if (subColor.a > 0f)
                                {
                                    if (i < distanceMax)
                                    {
                                        unionCount = 1;
                                        distanceMax = i;
                                        color = subColor;
                                    }
                                    else if (i == distanceMax)
                                    {
                                        unionCount++;
                                        color += subColor;
                                    }
                                    break;
                                }
                            }
                            for (int i = 1; i < BorderSize; i++)
                            {
                                var yy = y - i;
                                if (yy < 0) yy += tex.height;
                                var subColor = tex.GetPixel(x, yy);
                                if (subColor.a > 0f)
                                {
                                    if (i < distanceMax)
                                    {
                                        unionCount = 1;
                                        distanceMax = i;
                                        color = subColor;
                                    }
                                    else if (i == distanceMax)
                                    {
                                        unionCount++;
                                        color += subColor;
                                    }
                                    break;
                                }
                            }
                            color /= unionCount;
                            color.a = 1f;
                            pixels[x + y * tex.width] = color;
                        }
                    }
                    tex.SetPixels32(pixels);
                    tex.Apply();
                }
                #endregion
                #region Mipmap
                if (voxelBase.generateMipMaps)
                {
                    var pixels = tex.GetPixels32();
                    tex.Reinitialize(tex.width, tex.height, tex.format, true);
                    tex.SetPixels32(pixels, 0);
                    tex.Apply(true);
                }
                #endregion
                #region UV
                {
                    Vector2 uvone = new Vector2(1f / tex.width, 1f / tex.height);
                    for (int i = 0; i < atlasRects.Length; i++)
                    {
                        atlasRects[i].min += uvone;
                        atlasRects[i].max -= uvone;
                    }
                }
                #endregion
                if (EditorCommon.IsMainAsset(atlasTexture))
                {
                    var path = AssetDatabase.GetAssetPath(atlasTexture);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    AssetDatabase.ImportAsset(path);
                    MonoBehaviour.DestroyImmediate(tex);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                atlasTexture = tex;
            }
            foreach (var buffer in bufferList)
            {
                buffer.Dispose();
            }

            return true;
        }
        public void SetTextureImporterSetting(string path, Texture2D atlasTexture = null, bool assetDatabaseRefresh = true)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = voxelBase.generateMipMaps;
                importer.borderMipmap = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                if (atlasTexture != null)
                {
                    if (Math.Max(atlasTexture.width, atlasTexture.height) > importer.maxTextureSize)
                        importer.maxTextureSize = Math.Max(atlasTexture.width, atlasTexture.height);
                }
                if (assetDatabaseRefresh)
                    importer.SaveAndReimport();
            }
        }
        #endregion

        #region CreateMesh
        private float GetAmbientOcclusionPower(IntVector3 position, int dof, IntVector3 check)
        {
            var nearA = IntVector3.zero;
            var nearB = IntVector3.zero;
            switch (dof)
            {
                case 0: position.x += check.x; nearA = new IntVector3(0, check.y, 0); nearB = new IntVector3(0, 0, check.z); break;
                case 1: position.y += check.y; nearA = new IntVector3(check.x, 0, 0); nearB = new IntVector3(0, 0, check.z); break;
                case 2: position.z += check.z; nearA = new IntVector3(check.x, 0, 0); nearB = new IntVector3(0, check.y, 0); break;
            }
            var indexS = voxelData.VoxelTableContains(position);
            var indexL = voxelData.VoxelTableContains(position + nearA);
            var indexR = voxelData.VoxelTableContains(position + nearB);
            var indexC = voxelData.VoxelTableContains(position + nearA + nearB);
            var power = 0f;
            if (indexS >= 0)
            {
                if (indexL >= 0 || indexR >= 0)
                    power = 1f;
                else
                    power = 0.5f;
            }
            else
            {
                if (indexL >= 0 && indexR >= 0)
                    power = 1f;
                else if (indexL >= 0 || indexR >= 0 || indexC >= 0)
                    power = 0.5f;
            }
            return power;
        }

        protected virtual bool IsCombineVoxelFace(IntVector3 pos, IntVector3 combinePos, VoxelBase.Face face)
        {
            if (!voxelBase.combineFaces)
                return false;

            if (voxelBase.generateVertexColors)
            {
                int dof = 0;
                var check = IntVector3.zero;
                switch (face)
                {
                    case VoxelBase.Face.forward:
                        dof = 2; 
                        check = new IntVector3(0, 0, 1); 
                        break;
                    case VoxelBase.Face.up:
                        dof = 1;
                        check = new IntVector3(0, 1, 0);
                        break;
                    case VoxelBase.Face.right:
                        dof = 0;
                        check = new IntVector3(1, 0, 0);
                        break;
                    case VoxelBase.Face.left:
                        dof = 0;
                        check = new IntVector3(-1, 0, 0);
                        break;
                    case VoxelBase.Face.down:
                        dof = 1;
                        check = new IntVector3(0, -1, 0);
                        break;
                    case VoxelBase.Face.back:
                        dof = 2;
                        check = new IntVector3(0, 0, -1);
                        break;
                }
                switch (face)
                {
                    case VoxelBase.Face.right:
                    case VoxelBase.Face.left:
                        {
                            var checkYZ = check + new IntVector3(0, 1, 1);
                            var check_YZ = check + new IntVector3(0, -1, 1);
                            var checkY_Z = check + new IntVector3(0, 1, -1);
                            var check_Y_Z = check + new IntVector3(0, -1, -1);
                            if (GetAmbientOcclusionPower(pos, dof, checkYZ) != GetAmbientOcclusionPower(combinePos, dof, checkYZ) ||
                                GetAmbientOcclusionPower(pos, dof, check_YZ) != GetAmbientOcclusionPower(combinePos, dof, check_YZ) ||
                                GetAmbientOcclusionPower(pos, dof, checkY_Z) != GetAmbientOcclusionPower(combinePos, dof, checkY_Z) ||
                                GetAmbientOcclusionPower(pos, dof, check_Y_Z) != GetAmbientOcclusionPower(combinePos, dof, check_Y_Z))
                                return false;
                        }
                        break;
                    case VoxelBase.Face.up:
                    case VoxelBase.Face.down:
                        {
                            var checkXZ = check + new IntVector3(1, 0, 1);
                            var check_XZ = check + new IntVector3(-1, 0, 1);
                            var checkX_Z = check + new IntVector3(1, 0, -1);
                            var check_X_Z = check + new IntVector3(-1, 0, -1);
                            if (GetAmbientOcclusionPower(pos, dof, checkXZ) != GetAmbientOcclusionPower(combinePos, dof, checkXZ) ||
                                GetAmbientOcclusionPower(pos, dof, check_XZ) != GetAmbientOcclusionPower(combinePos, dof, check_XZ) ||
                                GetAmbientOcclusionPower(pos, dof, checkX_Z) != GetAmbientOcclusionPower(combinePos, dof, checkX_Z) ||
                                GetAmbientOcclusionPower(pos, dof, check_X_Z) != GetAmbientOcclusionPower(combinePos, dof, check_X_Z))
                                return false;
                        }
                        break;
                    case VoxelBase.Face.forward:
                    case VoxelBase.Face.back:
                        {
                            var checkXY = check + new IntVector3(1, 1, 0);
                            var check_XY = check + new IntVector3(-1, 1, 0);
                            var checkX_Y = check + new IntVector3(1, -1, 0);
                            var check_X_Y = check + new IntVector3(-1, -1, 0);
                            if (GetAmbientOcclusionPower(pos, dof, checkXY) != GetAmbientOcclusionPower(combinePos, dof, checkXY) ||
                                GetAmbientOcclusionPower(pos, dof, check_XY) != GetAmbientOcclusionPower(combinePos, dof, check_XY) ||
                                GetAmbientOcclusionPower(pos, dof, checkX_Y) != GetAmbientOcclusionPower(combinePos, dof, checkX_Y) ||
                                GetAmbientOcclusionPower(pos, dof, check_X_Y) != GetAmbientOcclusionPower(combinePos, dof, check_X_Y))
                                return false;
                        }
                        break;
                }
            }

            return true;
        }
        protected virtual bool IsHiddenVoxelFace(IntVector3 pos, VoxelBase.Face faceFlag)
        {
            return true;
        }
        protected virtual bool IsShowVoxelFace(IntVector3 pos, VoxelBase.Face faceFlag)
        {
            var index = voxelData.VoxelTableContains(pos);
            if (index < 0) return false;
            Assert.IsTrue(faceFlag == VoxelBase.Face.forward || faceFlag == VoxelBase.Face.up || faceFlag == VoxelBase.Face.right || faceFlag == VoxelBase.Face.left || faceFlag == VoxelBase.Face.down || faceFlag == VoxelBase.Face.back);
            IntVector3 combinePos = pos;
            {
                if (faceFlag == VoxelBase.Face.forward) combinePos.z++;
                if (faceFlag == VoxelBase.Face.up) combinePos.y++;
                if (faceFlag == VoxelBase.Face.right) combinePos.x++;
                if (faceFlag == VoxelBase.Face.left) combinePos.x--;
                if (faceFlag == VoxelBase.Face.down) combinePos.y--;
                if (faceFlag == VoxelBase.Face.back) combinePos.z--;
            }
            index = voxelData.VoxelTableContains(combinePos);
            if (index < 0)
                return true;
            {
                var material = voxelBase.GetMaterialIndexTable(pos);
                var combineMaterial = voxelBase.GetMaterialIndexTable(combinePos);
                if (material != combineMaterial)
                {
                    if (combineMaterial >= 0)
                    {
                        if (voxelBase.materialData[combineMaterial].transparent)
                            return true;
                    }
                }
            }
            return !IsHiddenVoxelFace(pos, faceFlag);
        }
        protected virtual void CreateMeshBefore()
        {
            PrefabAssetReImport = false;
        }
        protected virtual bool CreateMesh()
        {
            DestroyUnusedObjectInPrefabObject();

            return true;
        }
        protected virtual void CreateMeshAfter()
        {
            CheckPrefabAssetReImport();

            CalcDataRelease();

            GC.Collect();
        }
        protected void CreateMeshThrough()
        {
            PrefabAssetReImport = false;
            DestroyUnusedObjectInPrefabObject();
            CheckPrefabAssetReImport();
        }
        protected void SetMaterialData(Material material, MaterialData data)
        {
            if (material.shader == Shader.Find("Standard"))
            {
                #region Standard
                switch (data.material.renderingMode)
                {
                case MaterialData.Material.StandardShaderRenderingMode.Opaque:
#if false //Legacy
                    material.SetFloat("_Mode", 0);
                    material.renderQueue = -1;
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Glossiness", data.material.smoothness);
                    if (data.material.emission != Color.black && data.material.emissionPower != 0f)
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", data.material.emission * data.material.emissionPower);
                    }
                    else
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                        material.DisableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", Color.black);
                    }
                    material.color = Color.white;
                    //Reset
                    material.SetOverrideTag("RenderType", "");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.SetInt("_SrcBlend", 1);
                    material.SetInt("_DstBlend", 0);
                    material.SetInt("_ZWrite", 1);
#else //2021.3
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = -1;
                    material.SetOverrideTag("RenderType", "");
                    material.SetInt("_DstBlend", 0);
                    material.SetFloat("_Mode", 0);
                    material.SetInt("_SrcBlend", 1);
                    material.SetInt("_ZWrite", 1);

                    material.color = Color.white;
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Glossiness", data.material.smoothness);
                    if (data.material.emission != Color.black && data.material.emissionPower != 0f)
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", data.material.emission * data.material.emissionPower);
                    }
                    else
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                        material.DisableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", Color.black);
                    }
#endif
                    break;
                case MaterialData.Material.StandardShaderRenderingMode.Transparent:
#if false //Legacy
                    material.SetFloat("_Mode", 3);
                    material.renderQueue = 3000;
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Glossiness", data.material.smoothness);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.SetInt("_SrcBlend", 1);
                    material.SetInt("_DstBlend", 10);
                    material.SetInt("_ZWrite", 0);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                    material.color = new Color(1, 1, 1, data.material.alpha);
                    break;
#else //2021.3
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = 3000;
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetInt("_DstBlend", 10);
                    material.SetFloat("_Mode", 3);
                    material.SetInt("_SrcBlend", 1);
                    material.SetInt("_ZWrite", 0);
                        
                    material.color = new Color(1, 1, 1, data.material.alpha);
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Glossiness", data.material.smoothness);
                    if (data.material.emission != Color.black && data.material.emissionPower != 0f)
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", data.material.emission * data.material.emissionPower);
                    }
                    else
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                        material.DisableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", Color.black);
                    }
                    break;
#endif
                default:
                    Debug.LogError("<color=green>[Voxel Importer]</color> Not support mode.");
                    break;
                }
                #endregion
            }
            else if (EditorCommon.IsUniversalRenderPipeline())
            {
                #region URP
                switch (data.material.renderingMode)
                {
                case MaterialData.Material.StandardShaderRenderingMode.Opaque:
#if false //Legacy
                    material.renderQueue = -1;
                    material.SetFloat("_Metallic", data.material.metallic);
                    if (material.HasProperty("_Glossiness"))
                        material.SetFloat("_Glossiness", data.material.smoothness);
                    if (material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness", data.material.smoothness);
                    if (data.material.emission != Color.black && data.material.emissionPower != 0f)
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", data.material.emission * data.material.emissionPower);
                    }
                    else
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                        material.DisableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", Color.black);
                    }
                    material.color = Color.white;
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", Color.white);
                    //Reset
                    material.SetOverrideTag("RenderType", "");
                    material.SetShaderPassEnabled("SHADOWCASTER", true);
                    material.SetInt("_SrcBlend", 1);
                    material.SetInt("_DstBlend", 0);
                    material.SetInt("_Surface", 0);
                    material.SetInt("_ZWrite", 1);
#else //2021.3
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = -1;
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.SetShaderPassEnabled("DepthOnly", false);
                    material.SetShaderPassEnabled("SHADOWCASTER", false);
                    material.SetInt("_DstBlend", 0);
                    if (material.HasProperty("_DstBlendAlpha"))
                        material.SetInt("_DstBlendAlpha", 0);
                    material.SetInt("_SrcBlend", 1);
                    if (material.HasProperty("_SrcBlendAlpha"))
                        material.SetInt("_SrcBlendAlpha", 1);
                    material.SetInt("_Surface", 0);
                    material.SetInt("_ZWrite", 1);
                        
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Glossiness", data.material.smoothness);
                    material.SetFloat("_Smoothness", data.material.smoothness);
                    if (data.material.emission != Color.black && data.material.emissionPower != 0f)
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", data.material.emission * data.material.emissionPower);
                    }
                    else
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                        material.DisableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", Color.black);
                    }
                    material.color = Color.white;
                    material.SetColor("_BaseColor", Color.white);
#endif  
                    break;
                case MaterialData.Material.StandardShaderRenderingMode.Transparent:
#if false //Legacy
                    material.renderQueue = 3000;
                    material.SetFloat("_Metallic", data.material.metallic);
                    if (material.HasProperty("_Glossiness"))
                        material.SetFloat("_Glossiness", data.material.smoothness);
                    if (material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness", data.material.smoothness);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetShaderPassEnabled("SHADOWCASTER", false);
                    material.SetInt("_SrcBlend", 5);
                    material.SetInt("_DstBlend", 10);
                    material.SetInt("_Surface", 1);
                    material.SetInt("_ZWrite", 0);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                    material.color = new Color(1, 1, 1, data.material.alpha);
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", new Color(1, 1, 1, data.material.alpha));
#else //2021.3
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = 3000;
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetShaderPassEnabled("DepthOnly", false);
                    material.SetShaderPassEnabled("SHADOWCASTER", false);
                    material.SetInt("_DstBlend", 10);
                    if (material.HasProperty("_DstBlendAlpha"))
                        material.SetInt("_DstBlendAlpha", 10);
                    material.SetInt("_SrcBlend", 5);
                    if (material.HasProperty("_SrcBlendAlpha"))
                        material.SetInt("_SrcBlendAlpha", 5);
                    material.SetInt("_Surface", 1);
                    material.SetInt("_ZWrite", 0);
                        
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Glossiness", data.material.smoothness);
                    material.SetFloat("_Smoothness", data.material.smoothness);
                    if (data.material.emission != Color.black && data.material.emissionPower != 0f)
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", data.material.emission * data.material.emissionPower);
                    }
                    else
                    {
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                        material.DisableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", Color.black);
                    }
                    material.color = new Color(1, 1, 1, data.material.alpha);
                    material.SetColor("_BaseColor", new Color(1, 1, 1, data.material.alpha));
#endif
                    break;
                default:
                    Debug.LogError("<color=green>[Voxel Importer]</color> Not support type.");
                    break;
                }
                #endregion
            }
            else if (EditorCommon.IsHighDefinitionRenderPipeline())
            {
                #region HDRP
                switch (data.material.renderingMode)
                {
                case MaterialData.Material.StandardShaderRenderingMode.Opaque:
#if false //Legacy
                    material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                    material.renderQueue = 2000;
                    material.SetShaderPassEnabled("DistortionVectors", false);
                    material.SetShaderPassEnabled("TransparentDepthPrepass", false);
                    material.SetShaderPassEnabled("TransparentDepthPostpass", false);
                    material.SetShaderPassEnabled("TransparentBackface", false);
                    material.SetShaderPassEnabled("MOTIONVECTORS", false);
                    material.SetInt("_DistortionBlurDstBlend", 1);
                    material.SetInt("_DistortionBlurSrcBlend", 1);
                    material.SetInt("_DistortionDstBlend", 1);
                    material.SetInt("_DistortionSrcBlend", 1);
                    material.SetInt("_ZTestDepthEqualForOpaque", 3);
                    material.SetInt("_ZTestModeDistortion", 4);
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Smoothness", data.material.smoothness);
                    if (data.material.emission != Color.black && data.material.emissionPower != 0f)
                    {
                        if (material.HasProperty("_UseEmissiveIntensity")) //Unity2019.1
                        {
                            material.SetInt("_UseEmissiveIntensity", 1);
                            material.SetFloat("_EmissiveIntensity", data.material.emissionPower);
                            material.SetColor("_EmissiveColor", data.material.emission * data.material.emissionPower);
                            material.SetColor("_EmissiveColorLDR", data.material.emission);
                            material.SetFloat("_EmissiveExposureWeight", 0f);
                        }
                        else if (material.HasProperty("_EmissiveIntensity")) //Unity2018.1
                        {
                            material.SetFloat("_EmissiveIntensity", data.material.emissionPower);
                            material.SetColor("_EmissiveColor", data.material.emission);
                        }
                        else
                        {
                            material.SetColor("_EmissiveColor", data.material.emission * data.material.emissionPower);
                        }
                        material.SetInt("_AlbedoAffectEmissive", 1);
                    }
                    else
                    {
                        material.SetColor("_EmissiveColor", Color.black);
                        material.SetInt("_AlbedoAffectEmissive", 0);
                    }
                    material.color = Color.white;
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", Color.white);
                    //Reset
                    material.SetOverrideTag("RenderType", "");
                    material.DisableKeyword("_BLENDMODE_ALPHA");
                    material.DisableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
                    material.DisableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetInt("_DstBlend", 0);
                    material.SetInt("_SurfaceType", 0);
                    material.SetInt("_ZWrite", 1);
#else //2021.3
                    material.DisableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = 2225;
                    material.SetOverrideTag("RenderType", "");
                    material.SetFloat("_AlphaDstBlend", 0);
                    material.SetFloat("_DstBlend", 0);
                    if (material.HasProperty("_DstBlend2"))
                        material.SetFloat("_DstBlend2", 0);
                    material.SetInt("_StencilRefDepth", 8);
                    material.SetInt("_StencilRefGBuffer", 10);
                    material.SetInt("_StencilRefMV", 40);
                    material.SetFloat("_SurfaceType", 0);
                    material.SetInt("_ZTestDepthEqualForOpaque", 3);
                    material.SetFloat("_ZWrite", 1);
                        
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Smoothness", data.material.smoothness);
                    if (data.material.emission != Color.black && data.material.emissionPower != 0f)
                    {
                        material.SetInt("_AlbedoAffectEmissive", 1);
                        material.SetFloat("_EmissiveExposureWeight", 0f);
                        material.SetFloat("_EmissiveIntensity", data.material.emissionPower);
                        material.SetInt("_UseEmissiveIntensity", 1);
                        material.SetColor("_EmissiveColor", data.material.emission * data.material.emissionPower);
                        material.SetColor("_EmissiveColorLDR", data.material.emission);
                    }
                    else
                    {
                        material.SetInt("_UseEmissiveIntensity", 0);
                    }
                    material.color = Color.white;
                    material.SetColor("_BaseColor", Color.white);
#endif
                    break;
                case MaterialData.Material.StandardShaderRenderingMode.Transparent:
#if false //Legacy
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.EnableKeyword("_BLENDMODE_ALPHA");
                    material.EnableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
                    material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                    material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = 3000;
                    material.SetShaderPassEnabled("DistortionVectors", false);
                    material.SetShaderPassEnabled("TransparentDepthPrepass", false);
                    material.SetShaderPassEnabled("TransparentDepthPostpass", false);
                    material.SetShaderPassEnabled("TransparentBackface", false);
                    material.SetShaderPassEnabled("MOTIONVECTORS", false);
                    material.SetInt("_DistortionBlurDstBlend", 1);
                    material.SetInt("_DistortionBlurSrcBlend", 1);
                    material.SetInt("_DistortionDstBlend", 1);
                    material.SetInt("_DistortionSrcBlend", 1);
                    material.SetInt("_DstBlend", 10);
                    material.SetInt("_SurfaceType", 1);
                    material.SetInt("_ZTestDepthEqualForOpaque", 4);
                    material.SetInt("_ZTestModeDistortion", 4);
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Smoothness", data.material.smoothness);
                    material.SetInt("_ZWrite", 0);
                    material.SetColor("_EmissiveColor", Color.black);
                    material.SetInt("_AlbedoAffectEmissive", 0);
                    material.color = new Color(1, 1, 1, data.material.alpha);
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", new Color(1, 1, 1, data.material.alpha));
#else //2021.3
                    material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = 3000;
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetFloat("_AlphaDstBlend", 10);
                    material.SetFloat("_DstBlend", 10);
                    if (material.HasProperty("_DstBlend2"))
                        material.SetFloat("_DstBlend2", 10);
                    material.SetInt("_StencilRefDepth", 0);
                    material.SetInt("_StencilRefGBuffer", 2);
                    material.SetInt("_StencilRefMV", 32);
                    material.SetFloat("_SurfaceType", 1);
                    material.SetInt("_ZTestDepthEqualForOpaque", 4);
                    material.SetFloat("_ZWrite", 0);
                        
                    material.SetFloat("_Metallic", data.material.metallic);
                    material.SetFloat("_Smoothness", data.material.smoothness);
                    if (data.material.emission != Color.black && data.material.emissionPower != 0f)
                    {
                        material.SetInt("_AlbedoAffectEmissive", 1);
                        material.SetFloat("_EmissiveExposureWeight", 0f);
                        material.SetFloat("_EmissiveIntensity", data.material.emissionPower);
                        material.SetInt("_UseEmissiveIntensity", 1);
                        material.SetColor("_EmissiveColor", data.material.emission * data.material.emissionPower);
                        material.SetColor("_EmissiveColorLDR", data.material.emission);
                    }
                    else
                    {
                        material.SetInt("_UseEmissiveIntensity", 0);
                    }
                    material.color = new Color(1, 1, 1, data.material.alpha);
                    material.SetColor("_BaseColor", new Color(1, 1, 1, data.material.alpha));
#endif
                    break;
                default:
                    Debug.LogError("<color=green>[Voxel Importer]</color> Not support mode.");
                    break;
                }
                #endregion
            }
        }
        protected Mesh CreateMeshOnly(Mesh result, VoxelData.FaceAreaTable faceAreaTable, Texture2D atlasTexture, Rect[] atlasRects, AtlasRectTable atlasRectTable, Vector3 extraOffset, out List<int> materialIndexes)
        {
            if (voxelBase.importMode == VoxelBase.ImportMode.LowTexture)
                return CreateMeshOnly_LowTexture(result, faceAreaTable, atlasTexture, atlasRects, atlasRectTable, extraOffset, out materialIndexes);
            else if (voxelBase.importMode == VoxelBase.ImportMode.LowPoly)
                return CreateMeshOnly_LowPoly(result, faceAreaTable, atlasTexture, atlasRects, atlasRectTable, extraOffset, out materialIndexes);
            else
            {
                materialIndexes = new List<int>();
                return null;
            }
        }
        protected Mesh CreateMeshOnly_LowTexture(Mesh result, VoxelData.FaceAreaTable faceAreaTable, Texture2D atlasTexture, Rect[] atlasRects, AtlasRectTable atlasRectTable, Vector3 extraOffset, out List<int> materialIndexes)
        {
            Assert.IsNotNull(faceAreaTable);

            materialIndexes = new List<int>();

            if (result == null)
            {
                result = new Mesh();
            }
            else
            {
                result.ClearBlendShapes();
                result.Clear(false);
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<BoneWeight> boneWeights = isHaveBoneWeight ? new List<BoneWeight>() : null;
            List<int>[] triangles = new List<int>[voxelBase.materialData.Count];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new List<int>();
            }
            EditMeshReady();

            #region Create
            {
                var offsetPosition = voxelBase.localOffset + voxelBase.importOffset + extraOffset;
                Vector3 voffset3 = voxelBase.importScale * voxelBase.meshFaceVertexOffset;
                Action<VoxelData.FaceArea, VoxelBase.VoxelVertexIndex, int> AddStructureData = (fa, vi, index) =>
                {
                    voxelBase.structureData.voxels[voxelData.VoxelTableContains(fa.Get(vi))].indices.Add(new StructureData.Index(index, vi));
                };
                Action<VoxelData.FaceArea, VoxelBase.VoxelVertexIndex, int, IntVector3> AddColorData = (fa, vi, dof, check) =>
                {
                    var pos = voxelData.voxels[voxelData.VoxelTableContains(fa.Get(vi))].position;
                    var power = GetAmbientOcclusionPower(pos, dof, check);
                    colors.Add(Color.Lerp(voxelBase.generateVertexColorMax, voxelBase.generateVertexColorMin, power));
                };
                #region forward
                {
                    for (int i = 0; i < faceAreaTable.forward.Count; i++)
                    {
                        var faceArea = faceAreaTable.forward[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(-voffset3.x, sizeY + voffset3.y, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, -voffset3.y, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, -voffset3.y, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, sizeY + voffset3.y, voxelBase.importScale.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.forward);
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, 2, new IntVector3(-1, 1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, 2, new IntVector3(-1, -1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, 2, new IntVector3(1, -1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, 2, new IntVector3(1, 1, 1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))].position, VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))].position, VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))].position, VoxelBase.VoxelVertexIndex.X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))].position, VoxelBase.VoxelVertexIndex.XYZ));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region up
                {
                    for (int i = 0; i < faceAreaTable.up.Count; i++)
                    {
                        var faceArea = faceAreaTable.up[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(-voffset3.x, voxelBase.importScale.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, voxelBase.importScale.y, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, voxelBase.importScale.y, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, voxelBase.importScale.y, -voffset3.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.up);
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, 1, new IntVector3(-1, 1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, 1, new IntVector3(-1, 1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, 1, new IntVector3(1, 1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, 1, new IntVector3(1, 1, -1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))].position, VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))].position, VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))].position, VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))].position, VoxelBase.VoxelVertexIndex.XY_Z));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region right
                {
                    for (int i = 0; i < faceAreaTable.right.Count; i++)
                    {
                        var faceArea = faceAreaTable.right[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(voxelBase.importScale.x, -voffset3.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY + voffset3.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY + voffset3.y, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, -voffset3.y, sizeZ + voffset3.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.right);
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, 0, new IntVector3(1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, 0, new IntVector3(1, 1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, 0, new IntVector3(1, 1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, 0, new IntVector3(1, -1, 1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))].position, VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))].position, VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))].position, VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))].position, VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region left
                {
                    for (int i = 0; i < faceAreaTable.left.Count; i++)
                    {
                        var faceArea = faceAreaTable.left[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, -voffset3.y, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(0, -voffset3.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(0, sizeY + voffset3.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(0, sizeY + voffset3.y, sizeZ + voffset3.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.left);
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, 0, new IntVector3(-1, -1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, 0, new IntVector3(-1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, 0, new IntVector3(-1, 1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, 0, new IntVector3(-1, 1, 1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))].position, VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))].position, VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))].position, VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))].position, VoxelBase.VoxelVertexIndex._XYZ));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region down
                {
                    for (int i = 0; i < faceAreaTable.down.Count; i++)
                    {
                        var faceArea = faceAreaTable.down[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(sizeX + voffset3.x, 0, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, 0, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, 0, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, 0, sizeZ + voffset3.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.down);
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, 1, new IntVector3(1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, 1, new IntVector3(-1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, 1, new IntVector3(-1, -1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, 1, new IntVector3(1, -1, 1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))].position, VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))].position, VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))].position, VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))].position, VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region back
                {
                    for (int i = 0; i < faceAreaTable.back.Count; i++)
                    {
                        var faceArea = faceAreaTable.back[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(-voffset3.x, -voffset3.y, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, -voffset3.y, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, sizeY + voffset3.y, 0) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, sizeY + voffset3.y, 0) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.back);
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, 2, new IntVector3(-1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, 2, new IntVector3(1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, 2, new IntVector3(1, 1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, 2, new IntVector3(-1, 1, -1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))].position, VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))].position, VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))].position, VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))].position, VoxelBase.VoxelVertexIndex._XY_Z));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, vOffset + 3);
                        }
                    }
                }
                #endregion
            }
            #endregion

            EditMeshInvoke(new VoxelBase.OnBeforeCreateMeshData()
            {
                vertices = vertices,
                uv = uv,
                normals = normals,
                colors = colors,
                boneWeights = boneWeights,
                triangles = triangles,
            });

            #region Mesh
            {
                result.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                result.vertices = vertices.ToArray();
                result.uv = uv.ToArray();
                result.normals = normals.ToArray();
                if (isHaveBoneWeight)
                {
                    result.boneWeights = boneWeights.ToArray();
                    result.bindposes = GetBindposes();
                }
                {
                    int materialCount = 0;
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        if (triangles[i].Count > 0)
                            materialCount++;
                    }
                    result.subMeshCount = materialCount;
                    int submesh = 0;
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        if (triangles[i].Count > 0)
                        {
                            materialIndexes.Add(i);
                            result.SetTriangles(triangles[i].ToArray(), submesh++);
                        }
                    }
                }
                if (voxelBase.generateVertexColors)
                {
                    result.colors = colors.ToArray();
                }
                result.RecalculateBounds();
            }
            #endregion

            return result;
        }
        protected Mesh CreateMeshOnly_LowPoly(Mesh result, VoxelData.FaceAreaTable faceAreaTable, Texture2D atlasTexture, Rect[] atlasRects, AtlasRectTable atlasRectTable, Vector3 extraOffset, out List<int> materialIndexes)
        {
            Assert.IsNotNull(faceAreaTable);

            materialIndexes = new List<int>();

            if (result == null)
            {
                result = new Mesh();
            }
            else
            {
                result.ClearBlendShapes();
                result.Clear(false);
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<BoneWeight> boneWeights = isHaveBoneWeight ? new List<BoneWeight>() : null;
            List<int>[] triangles = new List<int>[voxelBase.materialData.Count];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new List<int>();
            }
            EditMeshReady();

            #region Create
            {
                var offsetPosition = voxelBase.localOffset + voxelBase.importOffset + extraOffset;
                Vector2 uvone = new Vector2(1f / atlasTexture.width, 1f / atlasTexture.height);
                Vector2 uvoffset = new Vector2(voxelBase.meshFaceVertexOffset / atlasTexture.width, voxelBase.meshFaceVertexOffset / atlasTexture.height);
                Vector3 voffset3 = voxelBase.importScale * voxelBase.meshFaceVertexOffset;
                Action<VoxelData.FaceArea, VoxelBase.VoxelVertexIndex, int> AddStructureData = (fa, vi, index) =>
                {
                    voxelBase.structureData.voxels[voxelData.VoxelTableContains(fa.Get(vi))].indices.Add(new StructureData.Index(index, vi));
                };
                Action<VoxelData.FaceArea, VoxelBase.VoxelVertexIndex, int, IntVector3> AddColorData = (fa, vi, dof, check) =>
                {
                    var pos = voxelData.voxels[voxelData.VoxelTableContains(fa.Get(vi))].position;
                    var power = GetAmbientOcclusionPower(pos, dof, check);
                    colors.Add(Color.Lerp(voxelBase.generateVertexColorMax, voxelBase.generateVertexColorMin, power));
                };
                #region forward
                {
                    for (int i = 0; i < faceAreaTable.forward.Count; i++)
                    {
                        var faceArea = faceAreaTable.forward[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(-voffset3.x, sizeY + voffset3.y, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, -voffset3.y, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, -voffset3.y, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, sizeY + voffset3.y, voxelBase.importScale.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.forward);
                        }
                        if (atlasRects != null)
                        {
                            var bound = atlasRectTable.forward[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.y) * uvone.y + uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.y - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.y - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.y) * uvone.y + uvoffset.y));
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, 2, new IntVector3(-1, 1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, 2, new IntVector3(-1, -1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, 2, new IntVector3(1, -1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, 2, new IntVector3(1, 1, 1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))].position, VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))].position, VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))].position, VoxelBase.VoxelVertexIndex.X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))].position, VoxelBase.VoxelVertexIndex.XYZ));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region up
                {
                    for (int i = 0; i < faceAreaTable.up.Count; i++)
                    {
                        var faceArea = faceAreaTable.up[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(-voffset3.x, voxelBase.importScale.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, voxelBase.importScale.y, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, voxelBase.importScale.y, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, voxelBase.importScale.y, -voffset3.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.up);
                        }
                        if (atlasRects != null)
                        {
                            var bound = atlasRectTable.up[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y + uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y + uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y - uvoffset.y));
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, 1, new IntVector3(-1, 1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, 1, new IntVector3(-1, 1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, 1, new IntVector3(1, 1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, 1, new IntVector3(1, 1, -1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))].position, VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))].position, VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))].position, VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))].position, VoxelBase.VoxelVertexIndex.XY_Z));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region right
                {
                    for (int i = 0; i < faceAreaTable.right.Count; i++)
                    {
                        var faceArea = faceAreaTable.right[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(voxelBase.importScale.x, -voffset3.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY + voffset3.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY + voffset3.y, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, -voffset3.y, sizeZ + voffset3.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.right);
                        }
                        if (atlasRects != null)
                        {
                            var bound = atlasRectTable.right[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.y - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.y) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.y) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y + uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.y - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y + uvoffset.y));
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, 0, new IntVector3(1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, 0, new IntVector3(1, 1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, 0, new IntVector3(1, 1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, 0, new IntVector3(1, -1, 1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))].position, VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))].position, VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))].position, VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))].position, VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XYZ, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region left
                {
                    for (int i = 0; i < faceAreaTable.left.Count; i++)
                    {
                        var faceArea = faceAreaTable.left[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, -voffset3.y, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(0, -voffset3.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(0, sizeY + voffset3.y, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(0, sizeY + voffset3.y, sizeZ + voffset3.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.left);
                        }
                        if (atlasRects != null)
                        {
                            var bound = atlasRectTable.left[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.y - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y + uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.y - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.y) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y - +uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.y) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y + uvoffset.y));
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, 0, new IntVector3(-1, -1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, 0, new IntVector3(-1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, 0, new IntVector3(-1, 1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, 0, new IntVector3(-1, 1, 1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))].position, VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))].position, VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))].position, VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))].position, VoxelBase.VoxelVertexIndex._XYZ));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XYZ, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region down
                {
                    for (int i = 0; i < faceAreaTable.down.Count; i++)
                    {
                        var faceArea = faceAreaTable.down[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(sizeX + voffset3.x, 0, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, 0, -voffset3.z) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, 0, sizeZ + voffset3.z) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, 0, sizeZ + voffset3.z) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.down);
                        }
                        if (atlasRects != null)
                        {
                            var bound = atlasRectTable.down[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y + uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y + uvoffset.y));
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, 1, new IntVector3(1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, 1, new IntVector3(-1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, 1, new IntVector3(-1, -1, 1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, 1, new IntVector3(1, -1, 1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))].position, VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))].position, VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))].position, VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))].position, VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_YZ, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_YZ, vOffset + 3);
                        }
                    }
                }
                #endregion
                #region back
                {
                    for (int i = 0; i < faceAreaTable.back.Count; i++)
                    {
                        var faceArea = faceAreaTable.back[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(-voffset3.x, -voffset3.y, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, -voffset3.y, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX + voffset3.x, sizeY + voffset3.y, 0) + pOffset);
                        vertices.Add(new Vector3(-voffset3.x, sizeY + voffset3.y, 0) + pOffset);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.back);
                        }
                        if (atlasRects != null)
                        {
                            var bound = atlasRectTable.back[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.y - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.y - bound.min.y) * uvone.y - uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x + uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.y) * uvone.y + uvoffset.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x - uvoffset.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.y) * uvone.y + uvoffset.y));
                        }
                        bool swapSquareSplits = false;
                        if (voxelBase.generateVertexColors)
                        {
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, 2, new IntVector3(-1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, 2, new IntVector3(1, -1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, 2, new IntVector3(1, 1, -1));
                            AddColorData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, 2, new IntVector3(-1, 1, -1));
                            if (faceArea.size == IntVector3.one)
                            {
                                if (colors[colors.Count - 3] == colors[colors.Count - 1] &&
                                   colors[colors.Count - 4] != colors[colors.Count - 2])
                                    swapSquareSplits = true;
                            }
                        }
                        if (!swapSquareSplits)
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        else
                        {
                            triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 3);
                            triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))].position, VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))].position, VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))].position, VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))].position, VoxelBase.VoxelVertexIndex._XY_Z));
                        }
                        if (voxelBase.structureData != null)
                        {
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._X_Y_Z, vOffset + 0);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.X_Y_Z, vOffset + 1);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex.XY_Z, vOffset + 2);
                            AddStructureData(faceArea, VoxelBase.VoxelVertexIndex._XY_Z, vOffset + 3);
                        }
                    }
                }
                #endregion
            }
            #endregion

            EditMeshInvoke(new VoxelBase.OnBeforeCreateMeshData()
            {
                vertices = vertices,
                uv = uv,
                normals = normals,
                colors = colors,
                boneWeights = boneWeights,
                triangles = triangles,
            });

            #region Mesh
            {
                result.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                result.vertices = vertices.ToArray();
                result.uv = uv.ToArray();
                result.normals = normals.ToArray();
                if (isHaveBoneWeight)
                {
                    result.boneWeights = boneWeights.ToArray();
                    result.bindposes = GetBindposes();
                }
                {
                    int materialCount = 0;
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        if (triangles[i].Count > 0)
                            materialCount++;
                    }
                    result.subMeshCount = materialCount;
                    int submesh = 0;
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        if (triangles[i].Count > 0)
                        {
                            materialIndexes.Add(i);
                            result.SetTriangles(triangles[i].ToArray(), submesh++);
                        }
                    }
                }
                if (voxelBase.generateVertexColors)
                {
                    result.colors = colors.ToArray();
                }
                result.RecalculateBounds();
            }
            #endregion

            return result;
        }
        public virtual void SetRendererCompornent() { }
        #region Edit
        public struct Edit_VerticesInfo
        {
            public IntVector3 position;
            public VoxelBase.VoxelVertexIndex vertexIndex;
        }
        public abstract Mesh[] Edit_CreateMesh(List<VoxelData.Voxel> voxels, List<Edit_VerticesInfo> dstList = null, bool combine = true);
        public Mesh Edit_CreateMeshOnly(List<VoxelData.Voxel> voxels, Rect[] atlasRects, List<Edit_VerticesInfo> dstList = null, bool combine = true)
        {
            var tmpFaceAreaTable = Edit_CreateMeshOnly_FaceArea(voxels, combine);

            var result = Edit_CreateMeshOnly_Mesh(tmpFaceAreaTable, atlasRects, dstList);

            return result;
        }
        public VoxelData.FaceAreaTable Edit_CreateMeshOnly_FaceArea(List<VoxelData.Voxel> voxels, bool combine = true)
        {
            #region VoxelTable
            DataTable3<int> tmpVoxelTable;
            {
                tmpVoxelTable = new DataTable3<int>(voxelData.voxelSize.x, voxelData.voxelSize.y, voxelData.voxelSize.z);
                for (int i = 0; i < voxels.Count; i++)
                {
                    tmpVoxelTable.Set(voxels[i].position, i);
                }
            }
            Func<int, int, int, int> TmpVoxelTableContains = (x, y, z) =>
            {
                if (!tmpVoxelTable.Contains(x, y, z))
                    return -1;
                else
                    return tmpVoxelTable.Get(x, y, z);
            };
            #endregion

            #region FaceArea
            VoxelData.FaceAreaTable tmpFaceAreaTable;
            {
                VoxelBase.Face[] voxelDoneFaces = new VoxelBase.Face[voxels.Count];
                {
                    int index;
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        voxelDoneFaces[i] = ~voxels[i].visible;
                        index = TmpVoxelTableContains(voxels[i].x, voxels[i].y, voxels[i].z + 1);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.forward;
                        index = TmpVoxelTableContains(voxels[i].x, voxels[i].y + 1, voxels[i].z);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.up;
                        index = TmpVoxelTableContains(voxels[i].x + 1, voxels[i].y, voxels[i].z);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.right;
                        index = TmpVoxelTableContains(voxels[i].x - 1, voxels[i].y, voxels[i].z);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.left;
                        index = TmpVoxelTableContains(voxels[i].x, voxels[i].y - 1, voxels[i].z);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.down;
                        index = TmpVoxelTableContains(voxels[i].x, voxels[i].y, voxels[i].z - 1);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.back;
                    }
                }
                Action<VoxelData.FaceArea, VoxelBase.Face> SetDoneFacesFlag = (faceArea, flag) =>
                {
                    for (int x = faceArea.min.x; x <= faceArea.max.x; x++)
                    {
                        for (int y = faceArea.min.y; y <= faceArea.max.y; y++)
                        {
                            for (int z = faceArea.min.z; z <= faceArea.max.z; z++)
                            {
                                var index = TmpVoxelTableContains(x, y, z);
                                Assert.IsTrue(index >= 0);
                                voxelDoneFaces[index] |= flag;
                            }
                        }
                    }
                };

                Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceX = (baseIndex, flag) =>
                {
                    var palette = voxels[baseIndex].palette;
                    var x = voxels[baseIndex].x;
                    var y = voxels[baseIndex].y;
                    var z = voxels[baseIndex].z;
                    var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = voxels[baseIndex].palette };
                    if (combine)
                    {
                        //back
                        for (int i = z - 1; ; i--)
                        {
                            var index = TmpVoxelTableContains(x, y, i);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.min.z = i;
                        }
                        //forward
                        for (int i = z + 1; ; i++)
                        {
                            var index = TmpVoxelTableContains(x, y, i);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.max.z = i;
                        }
                        //down
                        for (int i = y - 1; ; i--)
                        {
                            bool r = true;
                            for (int j = area.min.z; j <= area.max.z; j++)
                            {
                                var index = TmpVoxelTableContains(x, i, j);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.min.y = i;
                        }
                        //up
                        for (int i = y + 1; ; i++)
                        {
                            bool r = true;
                            for (int j = area.min.z; j <= area.max.z; j++)
                            {
                                var index = TmpVoxelTableContains(x, i, j);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.max.y = i;
                        }
                    }
                    return area;
                };
                Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceY = (baseIndex, flag) =>
                {
                    var palette = voxels[baseIndex].palette;
                    var x = voxels[baseIndex].x;
                    var y = voxels[baseIndex].y;
                    var z = voxels[baseIndex].z;
                    var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = voxels[baseIndex].palette };
                    if (combine)
                    {
                        //back
                        for (int i = z - 1; ; i--)
                        {
                            var index = TmpVoxelTableContains(x, y, i);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.min.z = i;
                        }
                        //forward
                        for (int i = z + 1; ; i++)
                        {
                            var index = TmpVoxelTableContains(x, y, i);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.max.z = i;
                        }
                        //left
                        for (int i = x - 1; ; i--)
                        {
                            bool r = true;
                            for (int j = area.min.z; j <= area.max.z; j++)
                            {
                                var index = TmpVoxelTableContains(i, y, j);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.min.x = i;
                        }
                        //right
                        for (int i = x + 1; ; i++)
                        {
                            bool r = true;
                            for (int j = area.min.z; j <= area.max.z; j++)
                            {
                                var index = TmpVoxelTableContains(i, y, j);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.max.x = i;
                        }
                    }
                    return area;
                };
                Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceZ = (baseIndex, flag) =>
                {
                    var palette = voxels[baseIndex].palette;
                    var x = voxels[baseIndex].x;
                    var y = voxels[baseIndex].y;
                    var z = voxels[baseIndex].z;
                    var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = voxels[baseIndex].palette };
                    if (combine)
                    {
                        //up
                        for (int i = y - 1; ; i--)
                        {
                            var index = TmpVoxelTableContains(x, i, z);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.min.y = i;
                        }
                        //down
                        for (int i = y + 1; ; i++)
                        {
                            var index = TmpVoxelTableContains(x, i, z);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.max.y = i;
                        }
                        //left
                        for (int i = x - 1; ; i--)
                        {
                            bool r = true;
                            for (int j = area.min.y; j <= area.max.y; j++)
                            {
                                var index = TmpVoxelTableContains(i, j, z);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.min.x = i;
                        }
                        //right
                        for (int i = x + 1; ; i++)
                        {
                            bool r = true;
                            for (int j = area.min.y; j <= area.max.y; j++)
                            {
                                var index = TmpVoxelTableContains(i, j, z);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.max.x = i;
                        }
                    }
                    return area;
                };

                tmpFaceAreaTable = new VoxelData.FaceAreaTable();

                #region forward
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.forward) != 0) continue;
                        var faceArea = CalcFaceZ(i, VoxelBase.Face.forward);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.forward);
                        tmpFaceAreaTable.forward.Add(faceArea);
                    }
                }
                #endregion
                #region up
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.up) != 0) continue;
                        var faceArea = CalcFaceY(i, VoxelBase.Face.up);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.up);
                        tmpFaceAreaTable.up.Add(faceArea);
                    }
                }
                #endregion
                #region right
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.right) != 0) continue;
                        var faceArea = CalcFaceX(i, VoxelBase.Face.right);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.right);
                        tmpFaceAreaTable.right.Add(faceArea);
                    }
                }
                #endregion
                #region left
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.left) != 0) continue;
                        var faceArea = CalcFaceX(i, VoxelBase.Face.left);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.left);
                        tmpFaceAreaTable.left.Add(faceArea);
                    }
                }
                #endregion
                #region down
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.down) != 0) continue;
                        var faceArea = CalcFaceY(i, VoxelBase.Face.down);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.down);
                        tmpFaceAreaTable.down.Add(faceArea);
                    }
                }
                #endregion
                #region back
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.back) != 0) continue;
                        var faceArea = CalcFaceZ(i, VoxelBase.Face.back);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.back);
                        tmpFaceAreaTable.back.Add(faceArea);
                    }
                }
                #endregion
            }
            #endregion

            return tmpFaceAreaTable;
        }
        public Mesh Edit_CreateMeshOnly_Mesh(VoxelData.FaceAreaTable tmpFaceAreaTable, Rect[] atlasRects, List<Edit_VerticesInfo> dstList = null)
        {
            #region CreateMesh
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<BoneWeight> boneWeights = isHaveBoneWeight ? new List<BoneWeight>() : null;
            List<int> triangles = new List<int>();

            #region Create
            {
                var offsetPosition = voxelBase.localOffset + voxelBase.importOffset;
                #region forward
                {
                    for (int i = 0; i < tmpFaceAreaTable.forward.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.forward[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, sizeY, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(0, 0, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX, sizeY, voxelBase.importScale.z) + pOffset);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 1); triangles.Add(vOffset + 2);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 2); triangles.Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.forward);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))].position, VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))].position, VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))].position, VoxelBase.VoxelVertexIndex.X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))].position, VoxelBase.VoxelVertexIndex.XYZ));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ), vertexIndex = VoxelBase.VoxelVertexIndex._XYZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex._X_YZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex.X_YZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ), vertexIndex = VoxelBase.VoxelVertexIndex.XYZ });
                        }
                    }
                }
                #endregion
                #region up
                {
                    for (int i = 0; i < tmpFaceAreaTable.up.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.up[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, voxelBase.importScale.y, 0) + pOffset);
                        vertices.Add(new Vector3(0, voxelBase.importScale.y, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, voxelBase.importScale.y, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, voxelBase.importScale.y, 0) + pOffset);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 1); triangles.Add(vOffset + 2);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 2); triangles.Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.up);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))].position, VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))].position, VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))].position, VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))].position, VoxelBase.VoxelVertexIndex.XY_Z));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex._XY_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ), vertexIndex = VoxelBase.VoxelVertexIndex._XYZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ), vertexIndex = VoxelBase.VoxelVertexIndex.XYZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex.XY_Z });
                        }
                    }
                }
                #endregion
                #region right
                {
                    for (int i = 0; i < tmpFaceAreaTable.right.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.right[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(voxelBase.importScale.x, 0, 0) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY, sizeZ) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, 0, sizeZ) + pOffset);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 1); triangles.Add(vOffset + 2);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 2); triangles.Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.right);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))].position, VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))].position, VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))].position, VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))].position, VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex.X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex.XY_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ), vertexIndex = VoxelBase.VoxelVertexIndex.XYZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex.X_YZ });
                        }
                    }
                }
                #endregion
                #region left
                {
                    for (int i = 0; i < tmpFaceAreaTable.left.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.left[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, 0, sizeZ) + pOffset);
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, sizeZ) + pOffset);
                        triangles.Add(vOffset + 2); triangles.Add(vOffset + 1); triangles.Add(vOffset + 0);
                        triangles.Add(vOffset + 3); triangles.Add(vOffset + 2); triangles.Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.left);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))].position, VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))].position, VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))].position, VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))].position, VoxelBase.VoxelVertexIndex._XYZ));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex._X_YZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex._X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex._XY_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ), vertexIndex = VoxelBase.VoxelVertexIndex._XYZ });
                        }
                    }
                }
                #endregion
                #region down
                {
                    for (int i = 0; i < tmpFaceAreaTable.down.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.down[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(sizeX, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, 0, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, sizeZ) + pOffset);
                        triangles.Add(vOffset + 2); triangles.Add(vOffset + 1); triangles.Add(vOffset + 0);
                        triangles.Add(vOffset + 3); triangles.Add(vOffset + 2); triangles.Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.down);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))].position, VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))].position, VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))].position, VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))].position, VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex.X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex._X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex._X_YZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex.X_YZ });
                        }
                    }
                }
                #endregion
                #region back
                {
                    for (int i = 0; i < tmpFaceAreaTable.back.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.back[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, 0) + pOffset);
                        triangles.Add(vOffset + 2); triangles.Add(vOffset + 1); triangles.Add(vOffset + 0);
                        triangles.Add(vOffset + 3); triangles.Add(vOffset + 2); triangles.Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            if (atlasRects != null)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.back);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))].position, VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))].position, VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))].position, VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(voxelData.voxels[voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))].position, VoxelBase.VoxelVertexIndex._XY_Z));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex._X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex.X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex.XY_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex._XY_Z });
                        }
                    }
                }
                #endregion
            }
            #endregion

            #region Mesh
            var result = new Mesh();
            {
                result.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                result.vertices = vertices.ToArray();
                result.uv = uv.ToArray();
                result.normals = normals.ToArray();
                if (isHaveBoneWeight)
                {
                    result.boneWeights = boneWeights.ToArray();
                    result.bindposes = GetBindposes();
                }
                result.triangles = triangles.ToArray();
                result.RecalculateBounds();
            }
            #endregion
            #endregion

            result.hideFlags = HideFlags.DontSave;

            return result;
        }
        #endregion
        #region EditMesh
        protected void EditMeshReady()
        {
            if (voxelBase.IsNeedStructureData())
                voxelBase.structureData = new StructureData(voxelBase.voxelData);
            else
                voxelBase.structureData = null;
        }
        protected void EditMeshInvoke(VoxelBase.OnBeforeCreateMeshData data)
        {
            if (voxelBase.onBeforeCreateMesh != null)
                voxelBase.onBeforeCreateMesh.Invoke(data);
            voxelBase.structureData = null;
        }
        #endregion
        #endregion

        #region CreateFaceArea
        protected VoxelData.FaceAreaTable CreateFaceArea(VoxelData.Voxel[] voxels)
        {
            VoxelData.FaceAreaTable result;
            if (voxelBase.importMode == VoxelBase.ImportMode.LowTexture)
                result = CreateFaceArea_LowTexture(voxels);
            else if (voxelBase.importMode == VoxelBase.ImportMode.LowPoly)
                result = CreateFaceData_LowPoly(voxels);
            else
            {
                Assert.IsFalse(false);
                result = new VoxelData.FaceAreaTable();
            }

            return result;
        }
        protected VoxelData.FaceAreaTable CreateFaceArea_LowTexture(VoxelData.Voxel[] voxels)
        {
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceX = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = voxelBase.GetMaterialIndexTable(x, y, z);
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //back
                for (int i = z - 1; ; i--)
                {
                    var index = voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(x, y, i) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.min.z = i;
                }
                //forward
                for (int i = z + 1; ; i++)
                {
                    var index = voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(x, y, i) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.max.z = i;
                }
                //down
                for (int i = y - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelData.VoxelTableContains(x, i, j);
                        if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(x, i, j) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.y = i;
                }
                //up
                for (int i = y + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelData.VoxelTableContains(x, i, j);
                        if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(x, i, j) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.y = i;
                }
                return area;
            };
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceY = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = voxelBase.GetMaterialIndexTable(x, y, z);
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //back
                for (int i = z - 1; ; i--)
                {
                    var index = voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(x, y, i) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.min.z = i;
                }
                //forward
                for (int i = z + 1; ; i++)
                {
                    var index = voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(x, y, i) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.max.z = i;
                }
                //left
                for (int i = x - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelData.VoxelTableContains(i, y, j);
                        if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(i, y, j) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.x = i;
                }
                //right
                for (int i = x + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelData.VoxelTableContains(i, y, j);
                        if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(i, y, j) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.x = i;
                }
                return area;
            };
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceZ = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = voxelBase.GetMaterialIndexTable(x, y, z);
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //up
                for (int i = y - 1; ; i--)
                {
                    var index = voxelData.VoxelTableContains(x, i, z);
                    if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(x, i, z) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.min.y = i;
                }
                //down
                for (int i = y + 1; ; i++)
                {
                    var index = voxelData.VoxelTableContains(x, i, z);
                    if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(x, i, z) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.max.y = i;
                }
                //left
                for (int i = x - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.y; j <= area.max.y; j++)
                    {
                        var index = voxelData.VoxelTableContains(i, j, z);
                        if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(i, j, z) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.x = i;
                }
                //right
                for (int i = x + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.y; j <= area.max.y; j++)
                    {
                        var index = voxelData.VoxelTableContains(i, j, z);
                        if (index < 0 || voxelData.voxels[index].palette != palette || voxelBase.GetMaterialIndexTable(i, j, z) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.x = i;
                }
                return area;
            };

            var result = new VoxelData.FaceAreaTable();
            var voxelIndexTable = GetVoxelIndexTable(voxels);

            #region forward
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.forward) != 0) continue;
                    var faceArea = CalcFaceZ(i, VoxelBase.Face.forward);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.forward);
                    result.forward.Add(faceArea);
                }
            }
            #endregion
            #region up
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.up) != 0) continue;
                    var faceArea = CalcFaceY(i, VoxelBase.Face.up);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.up);
                    result.up.Add(faceArea);
                }
            }
            #endregion
            #region right
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.right) != 0) continue;
                    var faceArea = CalcFaceX(i, VoxelBase.Face.right);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.right);
                    result.right.Add(faceArea);
                }
            }
            #endregion
            #region left
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.left) != 0) continue;
                    var faceArea = CalcFaceX(i, VoxelBase.Face.left);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.left);
                    result.left.Add(faceArea);
                }
            }
            #endregion
            #region down
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.down) != 0) continue;
                    var faceArea = CalcFaceY(i, VoxelBase.Face.down);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.down);
                    result.down.Add(faceArea);
                }
            }
            #endregion
            #region back
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.back) != 0) continue;
                    var faceArea = CalcFaceZ(i, VoxelBase.Face.back);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.back);
                    result.back.Add(faceArea);
                }
            }
            #endregion

            return result;
        }
        protected VoxelData.FaceAreaTable CreateFaceData_LowPoly(VoxelData.Voxel[] voxels)
        {
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceX = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = voxelBase.GetMaterialIndexTable(x, y, z);
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };

                //back
                for (int i = z - 1; ; i--)
                {
                    var index = voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelBase.GetMaterialIndexTable(x, y, i) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.min.z = i;
                }
                //forward
                for (int i = z + 1; ; i++)
                {
                    var index = voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelBase.GetMaterialIndexTable(x, y, i) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.max.z = i;
                }
                //down
                for (int i = y - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelData.VoxelTableContains(x, i, j);
                        if (index < 0 || voxelBase.GetMaterialIndexTable(x, i, j) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.y = i;
                }
                //up
                for (int i = y + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelData.VoxelTableContains(x, i, j);
                        if (index < 0 || voxelBase.GetMaterialIndexTable(x, i, j) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.y = i;
                }
                return area;
            };
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceY = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = voxelBase.GetMaterialIndexTable(x, y, z);
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //back
                for (int i = z - 1; ; i--)
                {
                    var index = voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelBase.GetMaterialIndexTable(x, y, i) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.min.z = i;
                }
                //forward
                for (int i = z + 1; ; i++)
                {
                    var index = voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelBase.GetMaterialIndexTable(x, y, i) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.max.z = i;
                }
                //left
                for (int i = x - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelData.VoxelTableContains(i, y, j);
                        if (index < 0 || voxelBase.GetMaterialIndexTable(i, y, j) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.x = i;
                }
                //right
                for (int i = x + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelData.VoxelTableContains(i, y, j);
                        if (index < 0 || voxelBase.GetMaterialIndexTable(i, y, j) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.x = i;
                }
                return area;
            };
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceZ = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = voxelBase.GetMaterialIndexTable(x, y, z);
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //up
                for (int i = y - 1; ; i--)
                {
                    var index = voxelData.VoxelTableContains(x, i, z);
                    if (index < 0 || voxelBase.GetMaterialIndexTable(x, i, z) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.min.y = i;
                }
                //down
                for (int i = y + 1; ; i++)
                {
                    var index = voxelData.VoxelTableContains(x, i, z);
                    if (index < 0 || voxelBase.GetMaterialIndexTable(x, i, z) != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) break;
                    area.max.y = i;
                }
                //left
                for (int i = x - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.y; j <= area.max.y; j++)
                    {
                        var index = voxelData.VoxelTableContains(i, j, z);
                        if (index < 0 || voxelBase.GetMaterialIndexTable(i, j, z) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.x = i;
                }
                //right
                for (int i = x + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.y; j <= area.max.y; j++)
                    {
                        var index = voxelData.VoxelTableContains(i, j, z);
                        if (index < 0 || voxelBase.GetMaterialIndexTable(i, j, z) != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.x = i;
                }
                return area;
            };

            var result = new VoxelData.FaceAreaTable();
            var voxelIndexTable = GetVoxelIndexTable(voxels);

            #region forward
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.forward) != 0) continue;
                    var faceArea = CalcFaceZ(i, VoxelBase.Face.forward);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.forward);
                    result.forward.Add(faceArea);
                }
            }
            #endregion
            #region up
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.up) != 0) continue;
                    var faceArea = CalcFaceY(i, VoxelBase.Face.up);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.up);
                    result.up.Add(faceArea);
                }
            }
            #endregion
            #region right
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.right) != 0) continue;
                    var faceArea = CalcFaceX(i, VoxelBase.Face.right);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.right);
                    result.right.Add(faceArea);
                }
            }
            #endregion
            #region left
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.left) != 0) continue;
                    var faceArea = CalcFaceX(i, VoxelBase.Face.left);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.left);
                    result.left.Add(faceArea);
                }
            }
            #endregion
            #region down
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.down) != 0) continue;
                    var faceArea = CalcFaceY(i, VoxelBase.Face.down);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.down);
                    result.down.Add(faceArea);
                }
            }
            #endregion
            #region back
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.back) != 0) continue;
                    var faceArea = CalcFaceZ(i, VoxelBase.Face.back);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.back);
                    result.back.Add(faceArea);
                }
            }
            #endregion

            return result;
        }
        #endregion

        #region BoneWeight
        public virtual bool isHaveBoneWeight { get { return false; } }
        public virtual Matrix4x4[] GetBindposes() { return null; }
        public virtual BoneWeight GetBoneWeight(IntVector3 pos, VoxelBase.VoxelVertexIndex index) { return new BoneWeight(); }
        #endregion

        #region Voxel
        public Vector3 GetVoxelRatePosition(IntVector3 pos, Vector3 rate)
        {
            Vector3 posV3 = new Vector3(pos.x, pos.y, pos.z);
            return Vector3.Scale(voxelBase.localOffset + voxelBase.importOffset + rate + posV3, voxelBase.importScale);
        }
        public Vector3 GetVoxelCenterPosition(IntVector3 pos)
        {
            return GetVoxelRatePosition(pos, new Vector3(0.5f, 0.5f, 0.5f));
        }
        public VoxelBase.VoxelVertices GetVoxelVertices(IntVector3 pos)
        {
            var min = GetVoxelRatePosition(pos, Vector3.zero);
            var max = GetVoxelRatePosition(pos, Vector3.one);
            VoxelBase.VoxelVertices vertices = new VoxelBase.VoxelVertices();
            vertices.vertexXYZ = new Vector3(max.x, max.y, max.z); //XYZ,
            vertices.vertex_XYZ = new Vector3(min.x, max.y, max.z); //_XYZ,
            vertices.vertexX_YZ = new Vector3(max.x, min.y, max.z); //X_YZ,
            vertices.vertexXY_Z = new Vector3(max.x, max.y, min.z); //XY_Z,
            vertices.vertex_X_YZ = new Vector3(min.x, min.y, max.z); //_X_YZ,
            vertices.vertex_XY_Z = new Vector3(min.x, max.y, min.z); //_XY_Z,
            vertices.vertexX_Y_Z = new Vector3(max.x, min.y, min.z); //X_Y_Z,
            vertices.vertex_X_Y_Z = new Vector3(min.x, min.y, min.z); //_X_Y_Z,
            return vertices;
        }
        public Bounds GetVoxelBounds(IntVector3 pos)
        {
            Bounds bounds = new Bounds();
            bounds.SetMinMax(GetVoxelRatePosition(pos, Vector3.zero), GetVoxelRatePosition(pos, Vector3.one));
            return bounds;
        }
        public BoundingSphere GetVoxelBoundingSphere(IntVector3 pos)
        {
            var min = GetVoxelRatePosition(pos, Vector3.zero);
            var max = GetVoxelRatePosition(pos, Vector3.one);
            return new BoundingSphere(Vector3.Lerp(min, max, 0.5f), (max - min).magnitude * 0.5f);
        }
        public Vector3 GetVoxelPosition(Vector3 localPosition)
        {
            return new Vector3(localPosition.x / voxelBase.importScale.x, localPosition.y / voxelBase.importScale.y, localPosition.z / voxelBase.importScale.z) - (voxelBase.localOffset + voxelBase.importOffset);
        }
        public bool IsVoxelVisible(IntVector3 pos)
        {
            var index = voxelData.VoxelTableContains(pos);
            if (index < 0) return false;

            if (voxelData.voxels[index].visible != 0) return true;

            if (IsShowVoxelFace(pos, VoxelBase.Face.forward)) return true;
            if (IsShowVoxelFace(pos, VoxelBase.Face.up)) return true;
            if (IsShowVoxelFace(pos, VoxelBase.Face.right)) return true;
            if (IsShowVoxelFace(pos, VoxelBase.Face.left)) return true;
            if (IsShowVoxelFace(pos, VoxelBase.Face.down)) return true;
            if (IsShowVoxelFace(pos, VoxelBase.Face.back)) return true;

            return false;
        }
        public Vector3 GetVoxelsCenter()
        {
            Vector3 center = Vector3.zero;

            Dictionary<int, List<Vector3>> centers = new Dictionary<int, List<Vector3>>();
            for (int i = 0; i < voxelData.voxels.Length; i++)
            {
                var pos = GetVoxelCenterPosition(voxelData.voxels[i].position);
                if (!centers.ContainsKey(voxelData.voxels[i].y))
                    centers.Add(voxelData.voxels[i].y, new List<Vector3>());
                centers[voxelData.voxels[i].y].Add(pos);
            }

            Dictionary<int, Vector3> centerPositions = new Dictionary<int, Vector3>();
            foreach (var pair in centers)
            {
                Vector3 value = Vector3.zero;
                for (int i = 0; i < pair.Value.Count; i++)
                    value += pair.Value[i];
                value /= (float)pair.Value.Count;
                centerPositions.Add(pair.Key, value);
            }

            foreach (var pair in centerPositions)
            {
                center += pair.Value;
            }
            center /= (float)centerPositions.Count;
            center.x = Mathf.Round(center.x * 2f) / 2f;
            center.y = Mathf.Round(center.y * 2f) / 2f;
            center.z = Mathf.Round(center.z * 2f) / 2f;

            return center;
        }
        #endregion

        #region Animation
        public void SwapAnimationObjectReference(UnityEngine.Object oldObj, UnityEngine.Object newObj)
        {
            var animator = voxelBase.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                Undo.RecordObjects(animator.runtimeAnimatorController.animationClips, "Swap Animation Object Reference");
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    if (bindings.IsReadOnly) continue;
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        var curves = AnimationUtility.GetObjectReferenceCurve(clip, bindings[i]);
                        bool changed = false;
                        for (int j = 0; j < curves.Length; j++)
                        {
                            if (curves[j].value == oldObj)
                            {
                                curves[j].value = newObj;
                                changed = true;
                            }
                        }
                        if (changed)
                        {
                            AnimationUtility.SetObjectReferenceCurve(clip, bindings[i], curves);
                        }
                    }
                }
            }
        }
        #endregion

        #region Edit
        public void AutoSetSelectedWireframeHidden()
        {
            SetSelectedWireframeHidden(voxelBase.edit_configureMode != VoxelBase.Edit_ConfigureMode.None);
        }
        public virtual void SetSelectedWireframeHidden(bool hidden)
        {
            if (voxelBase != null)
            {
                var renderer = voxelBase.GetComponent<Renderer>();
                if (renderer != null)
                {
                    EditorUtility.SetSelectedRenderState(renderer, hidden ? EditorSelectedRenderState.Hidden : EditorSelectedRenderState.Wireframe | EditorSelectedRenderState.Highlight);
                }
            }
        }
        #endregion

        #region Asset
        public bool PrefabAssetReImport { get; set; }
        public bool isPrefabEditMode { get { return PrefabStageUtility.GetCurrentPrefabStage() != null && PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot != null; } }
        public bool isPrefabEditable { get { return EditorCommon.IsComponentEditable(voxelBase); } }
        public void AddObjectToPrefabAsset(UnityEngine.Object obj)
        {
            if (AssetDatabase.Contains(obj)) return;

            UnityEngine.Object prefab = null;
            {
                if (isPrefabEditMode)
                {
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    var assetPath = prefabStage.assetPath;
                    prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                }
                else
                {
                    prefab = PrefabUtility.GetCorrespondingObjectFromSource(voxelBase.gameObject);
                }
                if (prefab == null)
                    return;
            }
            if (prefab != null)
            {
                Undo.RegisterCreatedObjectUndo(obj, "Create Object");
                AssetDatabase.AddObjectToAsset(obj, prefab);
                PrefabAssetReImport = true;
            }
        }
        public void DestroyUnusedObjectInPrefabObject()
        {
            UnityEngine.Object prefab = null;
            GameObject root = null;
            if (isPrefabEditMode)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                var assetPath = prefabStage.assetPath;

                prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                root = prefabStage.prefabContentsRoot;
            }
            else
            {
                prefab = PrefabUtility.GetCorrespondingObjectFromSource(voxelBase.gameObject);
                root = PrefabUtility.GetNearestPrefabInstanceRoot(voxelBase.gameObject);
            }
            if (prefab == null)
                return;
            var path = AssetDatabase.GetAssetPath(prefab);
            var objects = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            if (ArrayUtility.Contains(objects, null))
            {
                AssetDatabase.ImportAsset(path);
                objects = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            }
            VoxelBase[] childrenVoxelBases;
            VoxelBaseExplosion[] childrenVoxelBaseExplosions;
            {
                childrenVoxelBases = root.GetComponentsInChildren<VoxelBase>(true);
                childrenVoxelBaseExplosions = root.GetComponentsInChildren<VoxelBaseExplosion>(true);
            }
            foreach (var obj in objects)
            {
                if (obj == null) continue;
                if (obj is GameObject) continue;
                if (childrenVoxelBases != null)
                {
                    bool isUse = false;
                    foreach (var vb in childrenVoxelBases)
                    {
                        if (vb.IsUseAssetObject(obj))
                        {
                            isUse = true;
                            break;
                        }
                    }
                    if (isUse) continue;
                }
                #region Extra
                if (childrenVoxelBaseExplosions != null)
                {
                    bool isUse = false;
                    foreach (var vb in childrenVoxelBaseExplosions)
                    {
                        if (vb.IsUseAssetObject(obj))
                        {
                            isUse = true;
                            break;
                        }
                    }
                    if (isUse) continue;
                }
                #endregion
                AssetDatabase.RemoveObjectFromAsset(obj);
                PrefabAssetReImport = true;
            }
        }
        public void CheckPrefabAssetReImport()
        {
            if (PrefabAssetReImport)
            {
                PrefabAssetReImport = false;
            }
        }
        public abstract void ResetAllAssets();
        #region StaticForceReCreate
        public static void StaticForceReCreate(VoxelBase voxelBase)
        {
            VoxelBaseCore voxelCore = null;
            if (voxelBase is VoxelObject)
            {
                voxelCore = new VoxelObjectCore(voxelBase);
            }
            else if (voxelBase is VoxelChunksObject)
            {
                voxelCore = new VoxelChunksObjectCore(voxelBase);
            }
            else if (voxelBase is VoxelFrameAnimationObject)
            {
                voxelCore = new VoxelFrameAnimationObjectCore(voxelBase);
            }
            else if (voxelBase is VoxelSkinnedAnimationObject)
            {
                voxelCore = new VoxelSkinnedAnimationObjectCore(voxelBase);
            }
            else
            {
                Assert.IsTrue(false);
            }
            if (voxelCore != null)
            {
                voxelCore.ResetAllAssets();
                voxelCore.ReCreate();
            }
        }
        #endregion
        #endregion

        #region Export
        protected class SaveTransform
        {
            public SaveTransform(Transform t)
            {
                Save(t);
            }
            public void Save(Transform t)
            {
                localPosition = t.localPosition;
                localRotation = t.localRotation;
                localScale = t.localScale;
            }
            public void Load(Transform t)
            {
                t.localPosition = localPosition;
                t.localRotation = localRotation;
                t.localScale = localScale;
            }

            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }
        public bool ExportDaeFile(string path, bool assetDatabaseRefresh = true)
        {
            SaveTransform saveRootTransform = new SaveTransform(voxelBase.transform);
            {
                voxelBase.transform.localPosition = Vector3.zero;
                voxelBase.transform.localRotation = Quaternion.identity;
                voxelBase.transform.localScale = Vector3.one;
            }
            bool result = false;
            try
            {
                List<Transform> transforms = new List<Transform>();
                ExportDaeFile_AddTransform(transforms);

                DaeExporter exporter = new DaeExporter();
                exporter.settings_AssetDatabaseRefresh = assetDatabaseRefresh;
                result = exporter.Export(path, transforms);
                if (result)
                {
                    foreach (var p in exporter.exportedFiles)
                    {
                        if (p.IndexOf(Application.dataPath) < 0) continue;
                        var pTmp = FileUtil.GetProjectRelativePath(p);
                        SetTextureImporterSetting(pTmp, null, assetDatabaseRefresh);
                    }
                    if (assetDatabaseRefresh)
                        AssetDatabase.Refresh();
                }
            }
            finally
            {
                saveRootTransform.Load(voxelBase.transform);
            }
            return result;
        }
        protected virtual void ExportDaeFile_AddTransform(List<Transform> transforms)
        {
            transforms.Add(voxelBase.gameObject.transform);
        }
        #endregion

        #region Undo
        protected virtual void RefreshCheckerCreate() { voxelBase.refreshChecker = new VoxelBase.RefreshChecker(voxelBase); }
        public void RefreshCheckerClear() { voxelBase.refreshChecker = null; }
        public void RefreshCheckerSave() { if (voxelBase.refreshChecker == null) { RefreshCheckerCreate(); } voxelBase.refreshChecker.Save(); }
        public bool RefreshCheckerCheck() { return voxelBase.refreshChecker != null ? voxelBase.refreshChecker.Check() : false; }
        #endregion
    }
}
