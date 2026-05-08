using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.VirtualFileSystem;

namespace UnrealExporter;

public class PatchFileProvider : AbstractFileProvider, IVfsFileProvider
{
    private readonly FileProviderDictionary _files = new(true);

    public override IReadOnlyDictionary<string, GameFile> Files => _files;
    public override IReadOnlyDictionary<FPackageId, GameFile> FilesById => _files.byId;
    public IoGlobalData? GlobalData { get; set; }

    public IReadOnlyCollection<IAesVfsReader> UnloadedVfs => throw new NotImplementedException();

    public IReadOnlyCollection<IAesVfsReader> MountedVfs => throw new NotImplementedException();

    public IReadOnlyDictionary<FGuid, FAesKey> Keys => throw new NotImplementedException();

    public IReadOnlyCollection<FGuid> RequiredKeys => throw new NotImplementedException();

    public IAesVfsReader.CustomEncryptionDelegate? CustomEncryption { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public event EventHandler<int>? VfsRegistered;
    public event EventHandler<int>? VfsMounted;
    public event EventHandler<int>? VfsUnmounted;

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void Initialize()
    {
        throw new NotImplementedException();
    }

    public void Load(AbstractVfsFileProvider provider)
    {
        GlobalData = provider.GlobalData;
        //var vfsList = new List<IAesVfsReader>();
        var vfsList = new Dictionary<int, List<IAesVfsReader>>();
        vfsList[-1] = new List<IAesVfsReader>();
        foreach (var vfs in provider.MountedVfs)
        {
            if (vfs.Name.EndsWith("_P.pak"))
            {
                var name = vfs.Name.Substring(0, vfs.Name.Length - 6);
                var iof = name.LastIndexOf('_');
                var patchNo = int.Parse(name.Substring(iof + 1));
                if (!vfsList.ContainsKey(patchNo))
                    vfsList[patchNo] = new List<IAesVfsReader>();
                vfsList[patchNo].Add(vfs);
            }
            else
            {
                vfsList[-1].Add(vfs);
            }
        }

        var files = new Dictionary<string, GameFile>();
        int[] patchNos = vfsList.Keys.ToArray();
        Array.Sort(patchNos);
        foreach (int patchNo in patchNos)
        {
            foreach (var vfs in vfsList[patchNo])
            {
                foreach (var file in vfs.Files)
                    files[file.Key] = file.Value;
            }
        }

        _files.AddFiles(files);
    }

    public int Mount()
    {
        throw new NotImplementedException();
    }

    public Task<int> MountAsync()
    {
        throw new NotImplementedException();
    }

    public void RegisterVfs(string file)
    {
        throw new NotImplementedException();
    }

    public void RegisterVfs(FileInfo file)
    {
        throw new NotImplementedException();
    }

    public void RegisterVfs(string file, Stream[] stream, Func<string, FArchive>? openContainerStreamFunc = null)
    {
        throw new NotImplementedException();
    }

    public int SubmitKey(FGuid guid, FAesKey key)
    {
        throw new NotImplementedException();
    }

    public Task<int> SubmitKeyAsync(FGuid guid, FAesKey key)
    {
        throw new NotImplementedException();
    }

    public int SubmitKeys(IEnumerable<KeyValuePair<FGuid, FAesKey>> keys)
    {
        throw new NotImplementedException();
    }

    public Task<int> SubmitKeysAsync(IEnumerable<KeyValuePair<FGuid, FAesKey>> keys)
    {
        throw new NotImplementedException();
    }

}