using System;
using Xunit;
using Ghbvft6.Calq;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using System.IO.Pipes;

namespace Ghbvft6.CalqTest {

    internal static class Extension {
        public static void WaitForSuccess(this Process process) {
            process.WaitForExit();
            if (process.ExitCode != 0) {
                throw new Exception($"exited with: {process.ExitCode}");
            }
        }
    }

    public class UnitTest1 {

        private static string ProjectFile { get; set; }
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


        private string ClientProjectFile { get; set; }
        private string ClientExecutableFile { get; set; }
        private string TestClientProjectFile { get; set; }
        private string TestClientProjectDir { get; set; }
        private string TestClientExecutableFile { get; set; }


        private void Init() {
            ProjectFile = GetProjectFile();


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


            ClientProjectFile = Directory.GetFiles(CalqClientDir, "*.csproj", SearchOption.AllDirectories)[0]; // TODO or by dir name instead of csproj name?
            ClientExecutableFile = Directory.GetFiles(
                $"{CalqClientDir}/bin/Release",
                $"{Path.GetFileNameWithoutExtension(Path.GetFileName(ClientProjectFile))}.dll",
                SearchOption.AllDirectories
            )[0];
            TestClientProjectFile = GetTestClientCsproj();
            TestClientProjectDir = Path.GetDirectoryName(TestClientProjectFile)!;

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
            static string GetTestClientCsproj() {
                var projectFiles = Directory.GetFiles("./", "*.csproj", SearchOption.AllDirectories); // FIXME fsproj
                foreach (var projectFile in projectFiles) {
                    if (projectFile.EndsWith("Test")) {
                        continue;
                    }
                    var doc = new XmlDocument();
                    doc.LoadXml(File.ReadAllText(projectFile));
                    var calqServerReference = doc.SelectSingleNode("//PackageReference[@Include='Ghbvft6.Calq.Client']");
                    if (calqServerReference != null) {
                        return projectFile;
                    }
                }
                throw new Exception("calq client project not found");
            }
        }

        // FIXME run dotnet "TestClientProjectFile" test --configuration Release instead
        [Fact]
        public void Test1() {
            new Program().Generate();
            Init();


            File.Copy(ClientExecutableFile, $"{Path.GetDirectoryName(TestClientProjectFile)}/lib/{Path.GetFileName(ClientExecutableFile)}", true);

            Process.Start(new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = $"build --configuration Release \"{TestClientProjectFile}\"",
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            })!.WaitForSuccess();

            TestClientExecutableFile = Directory.GetFiles(
                $"{TestClientProjectDir}/bin/Release",
                $"{Path.GetFileNameWithoutExtension(Path.GetFileName(TestClientProjectFile))}.dll",
                SearchOption.AllDirectories
            )[0];

            ServerProcess = Process.Start(new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = AssemblyFile,
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            })!;

            var clientProcess = Process.Start(new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = TestClientExecutableFile,
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            });

            var output = clientProcess.StandardOutput.ReadToEnd(); ;

            Server = new(ServerProcess);
            Server.Exit();

            Assert.Equal($"foo{Environment.NewLine}foo 1{Environment.NewLine}foo 2{Environment.NewLine}foo local{Environment.NewLine}foo 3{Environment.NewLine}foo nested{Environment.NewLine}foo changed{Environment.NewLine}foo changed{Environment.NewLine}", output);
        }
    }
}
