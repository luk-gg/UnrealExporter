using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;



using Newtonsoft.Json;
using System.Globalization;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;
using SkiaSharp;
using CUE4Parse.UE4.Localization;
using System.Collections.Concurrent;
using CUE4Parse.MappingsProvider;
using JSBeautifyLib;
using CUE4Parse.Compression;

public class UnrealExporter
{
    public static void Main(string[] args)
    {
        var cli = new CommandApp<ExporterCli>();

        cli.Configure(conf =>
        {
            conf.SetApplicationName("unrealexporter");
            conf.SetExceptionHandler((ex, resolver) =>
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                return -1;
            });
        });

        cli.Run(args);
    }
}