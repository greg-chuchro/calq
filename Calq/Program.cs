using Ghbvft6.Calq.Tooler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;

namespace Calq {

    internal static class Extension {
        public static void WaitForSuccess(this Process process) {
            process.WaitForExit();
            if (process.ExitCode != 0) {
                throw new Exception($"exited with: {process.ExitCode}");
            }
        }
    }
    public class Program {

        public string ProjectFile { get; set; }
        private string ProjectDir { get; set; }
        private string AssemblyFile { get; set; }
        private string AssemblyDir { get; set; }
        private string CalqDir { get; set; }
        private Process ServerProcess { get; set; }
        private ServerPipeClient Server { get; set; }
        private string RootTypeFullName { get; set; }
        private string Prefix { get; set; }
        private string ClientNamespace { get; set; }
        private string CalqNamespace { get; set; }
        private string CalqClientDir { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        // TODO use final initializer https://github.com/dotnet/csharplang/discussions/3707
        public Program() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private void Init() {
            ProjectFile ??= GetProjectFile();

            Process.Start(new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = $"build --configuration Release \"{ProjectFile}\"",
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            })!.WaitForSuccess();

            ProjectDir = Path.GetDirectoryName(ProjectFile)!;
            AssemblyFile = Directory.GetFiles(
                $"{ProjectDir}/bin/Release",
                $"{Path.GetFileNameWithoutExtension(Path.GetFileName(ProjectFile))}.dll",
                SearchOption.AllDirectories
            )[0];
            AssemblyDir = Path.GetDirectoryName(AssemblyFile)!;
            CalqDir = $"{AssemblyDir}/Calq";

            ServerProcess = Process.Start(new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = AssemblyFile,
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            })!;
            Server = new(ServerProcess);

            RootTypeFullName = Server.RootType;
            Prefix = Server.Prefix;
            Server.Exit();

            ClientNamespace = $"{RootTypeFullName}Client";
            CalqNamespace = "Calq";

            CalqClientDir = $"{CalqDir}/{ClientNamespace}";

            static string GetProjectFile() {
                var projectFiles = Directory.GetFiles("./", "*.csproj", SearchOption.AllDirectories); // FIXME fsproj
                foreach (var projectFile in projectFiles) {
                    if (projectFile.EndsWith("Test")) {
                        continue;
                    }
                    var projectFileDocument = new XmlDocument();
                    projectFileDocument.LoadXml(File.ReadAllText(projectFile));
                    var calqServerReference = projectFileDocument.SelectSingleNode("//PackageReference[@Include='Ghbvft6.Calq.Server']");
                    if (calqServerReference != null) {
                        return projectFile;
                    }
                }
                throw new Exception("calq server project not found");
            }
        }

        private static Assembly LoadAssembly(string assemblyFile) {
            var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
            var assemblyPaths = new List<string>(runtimeAssemblies);

            var assemblies = Directory.GetFiles(Path.GetDirectoryName(assemblyFile)!, "*.dll", new EnumerationOptions { RecurseSubdirectories = true });
            assemblyPaths.AddRange(assemblies);

            return new MetadataLoadContext(new PathAssemblyResolver(assemblyPaths)).LoadFromAssemblyPath(assemblyFile);
        }

        public void Generate() {
            Init();

            if (Directory.Exists(CalqDir) == false) {
                Directory.CreateDirectory(CalqDir);
            }
            foreach (var file in Directory.GetFiles(CalqDir, "*", SearchOption.AllDirectories)) {
                File.Delete(file);
            }

            Process.Start(new ProcessStartInfo {
                WorkingDirectory = CalqDir,
                FileName = "dotnet",
                Arguments = $"new classlib --name {ClientNamespace}",
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            })!.WaitForSuccess();
            var dotnetGeneratedSourceFiles = Directory.GetFiles(CalqClientDir, "*.cs");
            foreach (var file in dotnetGeneratedSourceFiles) {
                File.Delete(file);
            }

            Process.Start(new ProcessStartInfo {
                WorkingDirectory = CalqClientDir,
                FileName = "dotnet",
                Arguments = $"add package Ghbvft6.Calq.Client --version 0.2.1", // FIXME use the latest version
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            })!.WaitForSuccess();

            File.WriteAllText($"{CalqClientDir}/{ClientNamespace}.Client.cs", GenerateClientClass());

            var assembly = LoadAssembly(AssemblyFile);
            var rootType = assembly.GetType(RootTypeFullName)!;
            File.WriteAllText($"{CalqClientDir}/{CalqNamespace}.{rootType.Namespace}.{rootType.Name}.cs", GenerateCalqClass(rootType));

            // FIXME use DFS
            foreach (var field in rootType.GetFields()) {
                var type = field.FieldType;
                if (type.GetInterface("ICollection") != null) continue;
                if (type.IsPrimitive || type.FullName == "System.String") continue;
                File.WriteAllText($"{CalqClientDir}/{CalqNamespace}.{type.Namespace}.{type.Name}.cs", GenerateCalqClass(type));
            }

            Process.Start(new ProcessStartInfo {
                WorkingDirectory = CalqClientDir,
                FileName = "dotnet",
                Arguments = $"build --configuration Release",
            })!.WaitForSuccess();

            string GenerateClientClass() {
                return $@"
namespace {ClientNamespace} {{
    public class Client : Ghbvft6.Calq.Client.CalqClient {{
        private static readonly Client instance = new(""{Prefix}"");
        public static Client Instance {{ get => instance; }}
        public Calq.{RootTypeFullName} service = new(null, """");
        public Client(string url) : base(new System.Net.Http.HttpClient {{ BaseAddress = new System.Uri(url) }}) {{ }}
    }}
}}
";
            }

            string GenerateCalqClass(Type type) {

                string GetMemberTypeFullName(Type memberType) {
                    if (memberType.GetInterface("IList") != null) { // FIXME other collections // INFO IsAssignableTo() doesn't work
                        var elementType = memberType.GetGenericArguments()[0];
                        return $"Ghbvft6.Calq.Client.CalqList<Calq.{elementType.Namespace}.{elementType.Name}>";
                    } else {
                        if (memberType.IsPrimitive || memberType.FullName == "System.String") { // FIXME add Decimal etc.
                            return $"{memberType.Namespace}.{memberType.Name}";
                        } else {
                            return $"Calq.{memberType.Namespace}.{memberType.Name}";
                        }
                    }
                }

                string GetBaseTypeFullName() {
                    if (type.BaseType!.FullName == "System.Object") {
                        if (type.GetInterface("IList") != null) { // FIXME other collections // INFO IsAssignableTo() doesn't work
                            return "Ghbvft6.Calq.Client.CalqList";
                        } else {
                            return "Ghbvft6.Calq.Client.CalqObject";
                        }
                    } else {
                        return $"{type.BaseType.Namespace}.{type.BaseType.Name}";
                    }
                }

                List<string> GetFieldDefinitions() {
                    var fieldDefinitions = new List<string>();
                    foreach (var field in type.GetFields()) {
                        fieldDefinitions.Add($"public {GetMemberTypeFullName(field.FieldType)} {field.Name};"); 
                    }
                    return fieldDefinitions;
                }

                return $@"#pragma warning disable CS0649

namespace {CalqNamespace}.{type.Namespace} {{
    public class {type.Name} : {GetBaseTypeFullName()} {{
        internal {type.Name}(Ghbvft6.Calq.Client.ICalqObject parent, string name) : base(parent, name) {{ }}
        {string.Join("\n        ", GetFieldDefinitions())}
    }}
}}
";
            }
        }

        static void Main(string[] args) {
            Tool.Execute(new Program(), args);
        }
    }
}
