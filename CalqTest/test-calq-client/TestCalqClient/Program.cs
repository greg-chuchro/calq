using System;
using static System.Console;

namespace TestCalqClient {
    class Program {
        static void Main() {
            var service = TestCalqServer.HelloWorldServiceClient.Client.DefaultInstance.Service;
            service.Get(); // get everything

            var item = service.item;
            var collection = service.collection;

            WriteLine(item.field); // foo
            foreach (var element in collection) {
                WriteLine(element.field);
                // foo 1
                // foo 2
            }
            WriteLine(item.Property.NestedProperty); // foo

            item.field = "foo x"; // change locally
            WriteLine(item.field); // foo x
            item.Put(); // update server
            item.field = "foo y"; // change locally
            WriteLine(item.field); // foo y
            item.Get(); // overwrite local changes
            WriteLine(item.field); // foo x

            item.Property = new() { NestedProperty = "foo x" }; // change locally
            WriteLine(item.Property.NestedProperty); // foo x
            item.Property.Put(); // update server
            item.Property.NestedProperty = "foo y"; // change locally
            WriteLine(item.Property.NestedProperty); // foo y
            item.Property.Get(); // overwrite local changes
            WriteLine(item.Property.NestedProperty); // foo x
        }
    }
}
