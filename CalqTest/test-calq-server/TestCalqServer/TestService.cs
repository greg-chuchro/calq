using Ghbvft6.Calq.Server;
using System.Collections.Generic;

namespace TestCalqServer {
    public class NestedResource {
        public string Property { get; set; }
        public NestedResource() {
            Property = "foo";
        }
    }
    public class Resource {
        private NestedResource nestedResource = new();
        public string field = "foo";
        public NestedResource NestedResource { get => nestedResource; set => nestedResource = value; }
    }
    class TestService {
        public Resource resource = new();
        public List<Resource> collection = new() {
            new() { field = "foo 1" },
            new() { field = "foo 2" }
        };

        static void Main() {
            var service = new TestService();
            var server = new CalqServer(service) {
                Prefixes = new[] { "http://localhost:8078/" }
            };
            server.Start();
        }
    }
}
