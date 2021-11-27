using System;

namespace TestCalqClient {
    class Program {
        static void Main(string[] args) {
            var client = TestCalqServer.ServiceClient.Client.Instance;
            // get all data from server
            client.Get(client.service);

            var item = client.service.item;
            var collection = client.service.collection;

            Console.WriteLine(item.field); // Hello World!
            foreach (var element in collection) {
                Console.WriteLine(element.field);
                // Hello World! 1
                // Hello World! 2
            }

            // update server
            item.field = "Hello World! 3";
            client.Put(item);

            // change locally
            item.field = "Hello World! LOCAL";

            // revert local changes with server's state
            client.Get(item);

            Console.WriteLine(item.field); // Hello World! 3
        }
    }
}
