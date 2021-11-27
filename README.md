# calq
calq (/kælk/) is an open-source, cross-platform framework that automates the development of RESTful and RPC API.
calq makes it easier to create highly secure web apps, online games, and all other kinds of cloud-based multi-tier applications.

## Get Started
```
dotnet tool install --global Ghbvft6.Calq
````
Both the [client](https://github.com/greg-chuchro/calq-client) and the [server](https://github.com/greg-chuchro/calq-server) can be simply added to any project:
```
dotnet add package Ghbvft6.Calq.Server
```
```
dotnet add package Ghbvft6.Calq.Client
```

### How it works?
To generate a client DLL from a server project run the following in its parent directory:
```
calq Generate
```
This will generate a similar message to:
```
Microsoft (R) Build Engine version 16.8.0+126527ff1 for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  All projects are up-to-date for restore.
  TestCalqServer.ServiceClient -> /home/username/repos/greg-chuchro/calq/CalqTest/test-calq-server/TestCalqServer/bin/Release/net5.0/Calq/TestCalqServer.ServiceClient/bin/Release/net5.0/TestCalqServer.ServiceClient.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:00.60
```
The generated DLL will contain a ready to use client class and all necessary classes redefined under `Calq` namespace.
### Example
[CalqTest/test-calq-server/TestCalqServer/Service.cs](CalqTest/test-calq-server/TestCalqServer/Service.cs)
```csharp
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
```
[CalqTest/test-calq-client/TestCalqClient/Program.cs](CalqTest/test-calq-client/TestCalqClient/Program.cs)
```csharp
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
```