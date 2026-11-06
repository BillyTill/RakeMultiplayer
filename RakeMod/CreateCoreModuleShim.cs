using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

class Program {
    static void Main(string[] args) {
        string unityEnginePath = @"D:\Rake Multiplayer\Rake\Rake_Data\Managed\UnityEngine.dll";
        string outputDir = @"D:\Rake Multiplayer\RakeMod\CoreModuleShim";
        string outputPath = Path.Combine(outputDir, "UnityEngine.CoreModule.dll");
        
        Directory.CreateDirectory(outputDir);
        
        var assembly = Assembly.LoadFrom(unityEnginePath);
        var types = assembly.GetTypes();
        
        var source = "using System.Reflection;\nusing System.Runtime.CompilerServices;\n\n" +
            "[assembly: AssemblyTitle(\"UnityEngine.CoreModule\")]\n" +
            "[assembly: AssemblyDescription(\"Type forwarder for Unity 5.x compatibility\")]\n" +
            "[assembly: AssemblyVersion(\"1.0.0.0\")]\n\n";
        
        int count = 0;
        foreach (var type in types) {
            // Only forward public, non-generic types from UnityEngine namespace
            if (type.IsPublic && !type.ContainsGenericParameters && 
                !type.FullName.Contains("<>") && !type.FullName.Contains("`") &&
                type.Namespace != null && type.Namespace.StartsWith("UnityEngine")) {
                source += "[assembly: TypeForwardedTo(typeof(" + type.FullName + "))]\n";
                count++;
            }
        }
        
        Console.WriteLine("Found " + count + " public non-generic UnityEngine types to forward");
        
        string sourcePath = Path.Combine(outputDir, "CoreModuleShim.cs");
        File.WriteAllText(sourcePath, source);
        
        Console.WriteLine("Source written, compiling with Roslyn...");
        
        var csc = @"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe";
        
        string refs = "/r:\"" + unityEnginePath + "\" /r:\"D:\\Rake Multiplayer\\Rake\\Rake_Data\\Managed\\UnityEngine.UI.dll\"";
        string cmd = "/target:library /out:\"" + outputPath + "\" " + refs + " \"" + sourcePath + "\"";
        
        var psi = new System.Diagnostics.ProcessStartInfo(csc, cmd) {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        var proc = System.Diagnostics.Process.Start(psi);
        proc.WaitForExit();
        
        string output = proc.StandardOutput.ReadToEnd();
        string error = proc.StandardError.ReadToEnd();
        
        Console.WriteLine(output);
        if (!string.IsNullOrEmpty(error)) Console.Error.WriteLine(error);
        
        if (File.Exists(outputPath)) {
            File.Copy(outputPath, @"D:\Rake Multiplayer\RakeMod\Plugins\UnityEngine.CoreModule.dll", true);
            Console.WriteLine("Shim created successfully!");
        } else {
            Console.Error.WriteLine("Failed to compile shim");
        }
    }
}