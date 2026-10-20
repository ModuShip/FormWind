#if NET8_0
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Paketin <c>build/FormWind.targets</c> dosyası küçük bir projeyle derlenir. Derleme birkaç saniye sürdüğünden yalnızca
/// net8.0-windows çalıştırmasında yer alır.
/// </summary>
public class PackageTargetsTests
{
    // KeePass 2.61.1 denemesinde görüldü: paket temayı yalnızca derlemeye gömüyordu; dosyayı çıktıya kopyalamak elle
    // eklenmezse uygulama çalışırken (TwTheme.LoadIfExists) temayı bulamıyor, varsayılan görünümle açılıyordu.
    [Fact]
    public void Theme_file_is_copied_to_the_output_and_publish_folders_once()
    {
        var folder = Path.Combine(Path.GetTempPath(), "formwind-targets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var targets = FindTargets();
            File.WriteAllText(Path.Combine(folder, "Demo.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>
                    <ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>
                  </PropertyGroup>
                  <ItemGroup Condition="'$(OwnCopy)' == 'true'">
                    <None Update="tw.config.json" CopyToOutputDirectory="PreserveNewest" />
                  </ItemGroup>
                  <Import Project="{targets}" />
                </Project>
                """);
            File.WriteAllText(Path.Combine(folder, "tw.config.json"), """{ "preset": "fluent" }""");
            File.WriteAllText(Path.Combine(folder, "Demo.cs"), "namespace Demo { public static class Empty { } }");

            Run(folder, "publish", "-o", "pub", "-p:OutDir=out1/");
            Assert.True(File.Exists(Path.Combine(folder, "out1", "tw.config.json")));
            Assert.True(File.Exists(Path.Combine(folder, "pub", "tw.config.json")));

            Run(folder, "build", "-p:OwnCopy=true", "-p:OutDir=out2/");                       // proje zaten kopyalıyor: tek öğe
            Assert.True(File.Exists(Path.Combine(folder, "out2", "tw.config.json")));

            Run(folder, "build", "-p:FormWindCopyTheme=false", "-p:OutDir=out3/");
            Assert.False(File.Exists(Path.Combine(folder, "out3", "tw.config.json")));
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // KeePass 2.61.1 denemesi: eski biçimli .NET Framework projesinde derleme yönlendirmeleri üretilmiyordu (açılışta
    // System.Text.Json yüklenemezdi); AutoGenerateBindingRedirects açılınca da uygulamanın kendi yapılandırma dosyası
    // (..\Ext\KeePass.exe.config) yerine yeni bir dosya üretildi ve KeePass "yapılandırma dosyası bozuk" dedi.
    [Fact]
    public void Binding_redirects_are_turned_on_and_a_separate_app_config_is_reported()
    {
        var folder = Path.Combine(Path.GetTempPath(), "formwind-targets-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(folder, "App");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(Path.Combine(folder, "Ext"));
        try
        {
            var targets = FindTargets();
            // Eski biçimli proje: yalnızca hedef dosyası; özellik proje bir değer vermezse açılır.
            var plain = Path.Combine(folder, "Plain");
            Directory.CreateDirectory(plain);
            File.WriteAllText(Path.Combine(plain, "Plain.proj"), $"""
                <Project>
                  <Import Project="{targets}" />
                  <Target Name="Show"><Message Importance="high" Text="ABR=[$(AutoGenerateBindingRedirects)]" /></Target>
                </Project>
                """);
            Assert.Contains("ABR=[true]", Run(plain, "msbuild", "Plain.proj", "-t:Show"));
            Assert.Contains("ABR=[false]", Run(plain, "msbuild", "Plain.proj", "-t:Show", "-p:AutoGenerateBindingRedirects=false"));

            File.WriteAllText(Path.Combine(folder, "Ext", "Demo.exe.config"), "<configuration />");
            File.WriteAllText(Path.Combine(project, "Demo.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>WinExe</OutputType>
                    <TargetFramework>net472</TargetFramework>
                    <ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>
                    <ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>
                  </PropertyGroup>
                  <Import Project="{targets}" />
                </Project>
                """);
            File.WriteAllText(Path.Combine(project, "Program.cs"), "static class Program { static void Main() { } }");

            var warned = Run(project, expectWarnings: true, "build", "-p:OutDir=out1/");
            Assert.Contains("FW100", warned);
            Assert.Contains(Path.Combine("Ext", "Demo.exe.config"), warned);
            Run(project, "build", "-p:OutDir=out2/", @"-p:AppConfig=..\Ext\Demo.exe.config");     // AppConfig verilince uyarı yok
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static string Run(string folder, params string[] args) => Run(folder, expectWarnings: false, args);

    private static string Run(string folder, bool expectWarnings, params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.ArgumentList.Add("-nologo");
        if (!expectWarnings) start.ArgumentList.Add("-p:TreatWarningsAsErrors=true");
        start.Environment["MSBUILDNOINPROCNODE"] = "1";
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output + error.Result);
        // MSBuild uyarı satırları ": warning XX123:" biçimindedir; İngilizce özetteki "0 Warning(s)" uyarı değildir.
        if (!expectWarnings) Assert.DoesNotContain(": warning ", output + error.Result, StringComparison.OrdinalIgnoreCase);
        return output + error.Result;
    }

    private static string FindTargets()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "FormWind", "build", "FormWind.targets");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("FormWind.targets bulunamadı.");
    }
}
#endif
