using CUE4Parse.Compression;

// public class InitService
// {
//     public static async ValueTask InitOodle()
//     {
//         var oodlePath = Path.Combine(".", OodleHelper.OODLE_DLL_NAME);
//         if (File.Exists(OodleHelper.OODLE_DLL_NAME))
//         {
//             File.Move(OodleHelper.OODLE_DLL_NAME, oodlePath, true);
//         }
//         else if (!File.Exists(oodlePath))
//         {
//             await OodleHelper.DownloadOodleDllAsync(oodlePath);
//         }

//         OodleHelper.Initialize(oodlePath);
//     }

//     public static async ValueTask InitZlib()
//     {
//         var zlibPath = Path.Combine(".", ZlibHelper.DLL_NAME);
//         if (!File.Exists(zlibPath))
//         {
//             await ZlibHelper.DownloadDllAsync(zlibPath);
//         }

//         ZlibHelper.Initialize(zlibPath);
//     }
// }