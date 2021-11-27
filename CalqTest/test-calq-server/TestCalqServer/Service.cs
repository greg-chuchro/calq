using Ghbvft6.Calq.Server;
using System.Collections.Generic;

namespace TestCalqServer {
    class Service {
        public class Resource {
            public string field = "Hello World!";
        }

        public Resource item = new();
        public List<Resource> collection = new() {
            new Resource { field = "Hello World! 1" },
            new Resource { field = "Hello World! 2" }
        };

        static void Main() {
            var service = new Service();
            var server = new CalqServer(service) {
                Prefixes = new[] { "http://localhost:8069/" }
            };
            server.Start();
        }
    }
}
