using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace Calq {
    public class ServerPipeClient {
        readonly NamedPipeClientStream pipe;
        readonly StreamReader reader;
        private readonly StreamWriter writer;

        public string RootType {
            get {
                writer.WriteLine("0/rootType");
                writer.Flush();
                return reader.ReadLine()!;
            }
        }

        public string Prefix {
            get {
                writer.WriteLine("0/prefix");
                writer.Flush();
                return reader.ReadLine()!;
            }
        }

        public void Exit() {
            writer.WriteLine("0/exit");
            writer.Flush();
            pipe.Close(); // FIXME
        }

        public ServerPipeClient(Process serverProcess) {
            var workingDirectory = serverProcess.StartInfo.WorkingDirectory != "" ? serverProcess.StartInfo.WorkingDirectory : Environment.CurrentDirectory;
            pipe = new NamedPipeClientStream($"{workingDirectory}/{serverProcess.ProcessName}-{serverProcess.Id}-calq");
            pipe.Connect();
            reader = new(pipe);
            writer = new(pipe);
        }
    }
}
