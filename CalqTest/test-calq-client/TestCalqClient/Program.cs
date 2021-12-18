using System;
using TestCalqServer;
using static System.Console;

namespace TestCalqClient {
    class Program {
        static void Main() {
            var service = TestServiceClient.DefaultInstance.Service;
            service.Get(); // get everything

            var resource = service.resource;
            var collection = service.collection;

            WriteLine(resource.field); // foo
            foreach (var item in collection) {
                WriteLine(item.field);
                // foo 1
                // foo 2
            }
            WriteLine(resource.NestedResource.Property); // foo

            resource.field = "foo x"; // change locally
            WriteLine(resource.field); // foo x
            resource.Put(); // update server
            resource.field = "foo y"; // change locally
            WriteLine(resource.field); // foo y
            resource.Get(); // overwrite local changes
            WriteLine(resource.field); // foo x

            resource.NestedResource = new() { Property = "foo x" }; // change locally
            WriteLine(resource.NestedResource.Property); // foo x
            resource.NestedResource.Put(); // update server
            resource.NestedResource.Property = "foo y"; // change locally
            WriteLine(resource.NestedResource.Property); // foo y
            resource.NestedResource.Get(); // overwrite local changes
            WriteLine(resource.NestedResource.Property); // foo x
        }
    }
}
