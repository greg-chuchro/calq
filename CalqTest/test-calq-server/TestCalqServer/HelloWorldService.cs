using Ghbvft6.Calq.Server;
using System.Collections.Generic;

namespace TestCalqServer {
    public class NestedResource {
        public string NestedProperty { get; set; }
        public NestedResource() {
            NestedProperty = "foo nested";
        }
    }
    public class Resource {
        private NestedResource privateField = new();
        public string field = "foo";
        public NestedResource Property { get => privateField; set => privateField = value; }
    }
    class HelloWorldService {
        public Resource item = new();
        public List<Resource> collection = new() {
            new Resource { field = "foo 1" },
            new Resource { field = "foo 2" }
        };

        static void Main() {
            var service = new HelloWorldService();
            var server = new CalqServer(service) {
                Prefixes = new[] { "http://localhost:8078/" }
            };
            server.Start();
        }
    }
}
