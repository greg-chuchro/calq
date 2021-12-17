# calq
Calq (/kælk/) is an open-source, cross-platform framework that automates the development of RESTful and RPC API.
Calq makes it easier to create highly secure web apps, online games, and all other kinds of cloud-based multi-tier applications.

## Get Started
```
dotnet tool install --global Ghbvft6.Calq
````
Both the [client](https://github.com/greg-chuchro/calq-client) and the [server](https://github.com/greg-chuchro/calq-server) can be simply added to any project.
```
dotnet add package Ghbvft6.Calq.Server
```
```
dotnet add package Ghbvft6.Calq.Client
```

### How it works?
To generate a client DLL from a server project run the following in its parent directory.
```
calq Generate
```
This will generate a similar message to:
```
Microsoft (R) Build Engine version 16.8.0+126527ff1 for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  All projects are up-to-date for restore.
  TestCalqServer.HelloWorldServiceClient -> /home/ghbvft6/repos/greg-chuchro/calq/CalqTest/bin/Release/net5.0/test-calq-server/TestCalqServer/bin/Release/net5.0/Calq/TestCalqServer.HelloWorldServiceClient/bin/Release/net5.0/TestCalqServer.HelloWorldServiceClient.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:00.60
```
The generated DLL will contain a ready to use client class and all necessary classes redefined under `Calq` namespace.
### Example
[CalqTest/test-calq-server/TestCalqServer/HelloWorldService.cs](CalqTest/test-calq-server/TestCalqServer/HelloWorldService.cs)
```csharp
public class NestedResource {
    public string NestedProperty { get; set; }
    public NestedResource() {
        NestedProperty = "foo";
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
        new() { field = "foo 1" },
        new() { field = "foo 2" }
    };

    static void Main() {
        var service = new HelloWorldService();
        var server = new CalqServer(service) {
            Prefixes = new[] { "http://localhost:8078/" }
        };
        server.Start();
    }
}
```
[CalqTest/test-calq-client/TestCalqClient/Program.cs](CalqTest/test-calq-client/TestCalqClient/Program.cs)
```csharp
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
```