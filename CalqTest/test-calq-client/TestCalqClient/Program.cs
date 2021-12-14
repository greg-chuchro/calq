using System;

namespace TestCalqClient {
    class Program {
        static void Main(string[] args) {
            var client = TestCalqServer.HelloWorldServiceClient.Client.Instance;
            client.Get(client.service); // get everything

            var item = client.service.item;
            var collection = client.service.collection;

            Console.WriteLine(item.field); // foo
            foreach (var element in collection) {
                Console.WriteLine(element.field);
                // foo 1
                // foo 2
            }
            
            item.field = "foo 3"; // change locally
            client.Put(item); // update server
            item.field = "foo local"; // change locally
            Console.WriteLine(item.field); // foo local
            client.Get(item); // overwrite local changes
            Console.WriteLine(item.field); // foo 3

            Console.WriteLine(item.Property.NestedProperty); // foo nested
            item.Property = new() { NestedProperty = "foo changed" };
            Console.WriteLine(item.Property.NestedProperty); // foo changed
            client.Put(item.Property); // update server
            client.Get(item.Property); // confirm that the value has changed on the server
            Console.WriteLine(item.Property.NestedProperty); // foo changed
        }
    }
}
