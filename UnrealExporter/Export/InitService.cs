using CUE4Parse.Compression;

public class InitService
{
    public static async Task InitOodle()
    {
        await OodleHelper.InitializeAsync();
    }

    // public static async ValueTask InitZlib()
    // {
    //     var zlibPath = Path.Combine(".", ZlibHelper.DLL_NAME);
    //     if (!File.Exists(zlibPath))
    //     {
    //         await ZlibHelper.DownloadDllAsync(zlibPath);
    //     }

    //     ZlibHelper.Initialize(zlibPath);
    // }
}