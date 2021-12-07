using System;

namespace TestCalqClient {
    class Program {
        static void Main(string[] args) {
            var client = TestCalqServer.HelloWorldServiceClient.Client.Instance;
            // get all data from server
            client.Get(client.service);

            var item = client.service.item;
            var collection = client.service.collection;

            Console.WriteLine(item.field); // foo
            foreach (var element in collection) {
                Console.WriteLine(element.field);
                // foo 1
                // foo 2
            }

            // update server
            item.field = "foo 3";
            client.Put(item);

            // change locally
            item.field = "foo local";

            // revert local changes with server's state
            client.Get(item);

            Console.WriteLine(item.field); // foo 3
            Console.WriteLine(item.Property.NestedProperty); // foo nested
        }
    }
}
