using Ghbvft6.Calq.Tooler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;

namespace Ghbvft6.Calq {

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
                Arguments = $"add package Ghbvft6.Calq.Client --version 0.3.0", // FIXME use the latest version
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            })!.WaitForSuccess();

            File.WriteAllText($"{CalqClientDir}/{ClientNamespace}.Client.cs", GenerateClientClass());
            File.WriteAllText($"{CalqClientDir}/{ClientNamespace}.CalqObject.cs", GenerateCalqObjectClass());
            File.WriteAllText($"{CalqClientDir}/{ClientNamespace}.CalqList.cs", GenerateCalqListClass());
            File.WriteAllText($"{CalqClientDir}/{ClientNamespace}.CalqObjectList.cs", GenerateCalqObjectListClass());

            var assembly = LoadAssembly(AssemblyFile);
            var rootType = assembly.GetType(RootTypeFullName)!;

            var discoveredTypes = new HashSet<Type>();
            var typeStack = new Stack<Type>();
            discoveredTypes.Add(rootType);
            typeStack.Push(rootType);

            while (typeStack.Count > 0) {
                var type = typeStack.Pop();
                File.WriteAllText($"{CalqClientDir}/{CalqNamespace}.{type.Namespace}.{type.Name}.cs", GenerateCalqClass(type));
                foreach (var member in type.GetFields()) {
                    var memberType = member.FieldType;
                    if (memberType.GetInterface("ICollection") != null) continue;
                    if (memberType.IsPrimitive || memberType.FullName == "System.String") continue; // FIXME other "primitive" types
                    if (discoveredTypes.Contains(memberType) == false) {
                        discoveredTypes.Add(memberType);
                        typeStack.Push(memberType);
                    }
                }
                foreach (var member in type.GetProperties()) {
                    var memberType = member.PropertyType;
                    if (memberType.GetInterface("ICollection") != null) continue;
                    if (memberType.IsPrimitive || memberType.FullName == "System.String") continue; // FIXME other "primitive" types
                    if (discoveredTypes.Contains(memberType) == false) {
                        discoveredTypes.Add(memberType);
                        typeStack.Push(memberType);
                    }
                }
            }

            Process.Start(new ProcessStartInfo {
                WorkingDirectory = CalqClientDir,
                FileName = "dotnet",
                Arguments = $"build --configuration Release",
            })!.WaitForSuccess();


            // TODO move to a separate class
            // TODO disposable
            string GenerateClientClass() {
                return $@"#nullable enable
using System.Threading;

namespace {ClientNamespace} {{
    public class Client : Ghbvft6.Calq.Client.CalqClient {{
        private static Client? defaultInstance;
        private static ThreadLocal<Client?> threadLocal;

        public static Client? DefaultInstance {{ get => defaultInstance; protected set => defaultInstance = value; }}
        public static Client? ThreadLocalInstance {{ get => threadLocal.Value; protected set => threadLocal.Value = value; }}

        static Client() {{
            defaultInstance = new(""{Prefix}"");
            threadLocal = new(() => defaultInstance);
        }}

        private readonly Calq.{RootTypeFullName} service;
        public Calq.{RootTypeFullName} Service {{ get => service; }}

        public Client() : base(new System.Net.Http.HttpClient {{ BaseAddress = new System.Uri(""{Prefix}"") }}) {{
            service = new();
        }}
        public Client(string url) : base(new System.Net.Http.HttpClient {{ BaseAddress = new System.Uri(url) }}) {{
            service = new();
        }}
    }}
}}
";
            }

            string GenerateCalqObjectClass() {
                return $@"#nullable enable
using Ghbvft6.Calq.Client;

namespace {ClientNamespace} {{
    public class CalqObject : ICalqObject {{
        private ICalqObject? Parent {{ get; set; }}
        private string? Name {{ get; set; }}

        ICalqObject? ICalqObject.Parent => Parent;
        string? ICalqObject.Name => Name;

        public CalqObject() {{ }}

        internal void Attach(ICalqObject parent, string name) {{
            Parent = parent;
            Name = name;
        }}

        public void Get() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Get(this);
        }}

        public void Post() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Post(this);
        }}

        public void Put() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Put(this);
        }}

        public void Delete() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Delete(this);
        }}

        public void Patch() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Patch(this);
        }}
    }}
}}
";
            }

            string GenerateCalqListClass() {
                return $@"#nullable enable
using Ghbvft6.Calq.Client;
using System.Collections.Generic;

namespace {ClientNamespace} {{

    public class CalqList<T> : List<T>, ICalqObject {{
        private ICalqObject? Parent {{ get; set; }}
        private string? Name {{ get; set; }}

        ICalqObject? ICalqObject.Parent => Parent;
        string? ICalqObject.Name => Name;

        public CalqList() {{ }}

        internal void Attach(ICalqObject parent, string name) {{
            Parent = parent;
            Name = name;
        }}

        public void Get() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Get(this);
        }}

        public void Post() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Post(this);
        }}

        public void Put() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Put(this);
        }}

        public void Delete() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Delete(this);
        }}

        public void Patch() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Patch(this);
        }}
    }}
}}
";
            }

            string GenerateCalqObjectListClass() {
                return $@"#nullable enable
#pragma warning disable CS8601
#pragma warning disable CS8604

using Ghbvft6.Calq.Client;
using System.Collections;
using System.Collections.Generic;

namespace {ClientNamespace} {{

    public class CalqObjectList<T> : List<T>, ICalqObject, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>, ICollection, IList where T : CalqObject {{
        private ICalqObject? Parent {{ get; set; }}
        private string? Name {{ get; set; }}

        ICalqObject? ICalqObject.Parent => Parent;
        string? ICalqObject.Name => Name;

        public CalqObjectList() {{ }}

        internal void Attach(ICalqObject parent, string name) {{
            Parent = parent;
            Name = name;
        }}

        public void Get() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Get(this);
        }}

        public void Post() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Post(this);
        }}

        public void Put() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Put(this);
        }}

        public void Delete() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Delete(this);
        }}

        public void Patch() {{
            {ClientNamespace}.Client.ThreadLocalInstance!.Patch(this);
        }}

        public int Add(object? value) {{
            ((T?)value)?.Attach(this, this.Count.ToString());
            Add((T?)value);
            return this.Count;
        }}

        public void Insert(int index, object? value) {{
            ((T?)value)?.Attach(this, this.Count.ToString());
            base.Insert(index, (T?)value);
        }}

        new public void Add(T item) {{
            item?.Attach(this, this.Count.ToString());
            base.Add(item);
        }}

        new public void Insert(int index, T item) {{
            item?.Attach(this, this.Count.ToString());
            base.Insert(index, item);
        }}

        new public T this[int i] {{
            get => base[i];
            set {{
                value?.Attach(this, i.ToString());
                base[i] = value;
            }}
        }}
        new public void AddRange(IEnumerable<T> collection) {{
            var count = this.Count;
            foreach (var item in collection) {{
                item?.Attach(this, count.ToString());
                ++count;
            }}
            base.AddRange(collection);
        }}
    }}
}}
";
            }

            string GenerateCalqClass(Type type) {

                string GetMemberTypeFullName(Type memberType) {
                    if (memberType.GetInterface("IList") != null) { // FIXME other collections // IsAssignableTo() doesn't work
                        var itemType = memberType.GetGenericArguments()[0];
                        if (itemType.IsPrimitive || itemType.FullName == "System.String") { // FIXME add Decimal etc.
                            return $"global::{ClientNamespace}.CalqList<Calq.{itemType.Namespace}.{itemType.Name}>";
                        } else {
                            return $"global::{ClientNamespace}.CalqObjectList<Calq.{itemType.Namespace}.{itemType.Name}>";
                        }
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
                        if (type.GetInterface("IList") != null) { // FIXME other collections // IsAssignableTo() doesn't work
                            return $"global::{ClientNamespace}.CalqList";
                        } else {
                            return $"global::{ClientNamespace}.CalqObject";
                        }
                    } else {
                        return $"{type.BaseType.Namespace}.{type.BaseType.Name}";
                    }
                }

                List<string> GetPrivateFieldDefinitions() {
                    var memberDefinitions = new List<string>();
                    foreach (var member in type.GetProperties()) {
                        var memberType = member.PropertyType;
                        if (memberType.IsPrimitive || memberType.FullName == "System.String") { // FIXME add Decimal etc.
                        } else {
                            memberDefinitions.Add($"private {GetMemberTypeFullName(memberType)}? _{member.Name};");
                        }
                    }
                    foreach (var member in type.GetFields()) {
                        var memberType = member.FieldType;
                        if (memberType.IsPrimitive || memberType.FullName == "System.String") { // FIXME add Decimal etc.
                        } else {
                            memberDefinitions.Add($"private {GetMemberTypeFullName(memberType)}? _{member.Name};");
                        }
                    }
                    return memberDefinitions;
                }

                List<string> GetFieldDefinitions() {
                    var memberDefinitions = new List<string>();
                    foreach (var member in type.GetFields()) {
                        var memberType = member.FieldType;
                        if (memberType.IsPrimitive || memberType.FullName == "System.String") { // FIXME add Decimal etc.
                            memberDefinitions.Add($"public {GetMemberTypeFullName(memberType)} {member.Name};");
                        } else {
                            memberDefinitions.Add($"public {GetMemberTypeFullName(memberType)}? {member.Name} {{ get => _{member.Name}; set {{ value?.Attach(this, nameof({member.Name})); _{member.Name} = value; }} }}");
                        }
                    }
                    return memberDefinitions;
                }

                List<string> GetPropertyDefinitions() {
                    var memberDefinitions = new List<string>();
                    foreach (var member in type.GetProperties()) {
                        var memberType = member.PropertyType;
                        if (memberType.IsPrimitive || memberType.FullName == "System.String") { // FIXME add Decimal etc.
                            memberDefinitions.Add($"public {GetMemberTypeFullName(memberType)} {member.Name};");
                        } else {
                            memberDefinitions.Add($"public {GetMemberTypeFullName(memberType)}? {member.Name} {{ get => _{member.Name}; set {{ value?.Attach(this, nameof({member.Name})); _{member.Name} = value; }} }}");
                        }
                    }
                    return memberDefinitions;
                }

                return $@"#pragma warning disable CS0649
#pragma warning disable CS8618
#nullable enable

namespace {CalqNamespace}.{type.Namespace} {{
    public class {type.Name} : {GetBaseTypeFullName()} {{
        public {type.Name}() {{ }}
        {string.Join("\n        ", GetPrivateFieldDefinitions())}
        {string.Join("\n        ", GetFieldDefinitions())}
        {string.Join("\n        ", GetPropertyDefinitions())}
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
