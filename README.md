# MIG libray for .Net/Mono

MIG is a .Net library providing an integrated solution for developing asynchronous real time web applications.

## How it works

We have two main concept in MIG: **Gateway** and **Interface**.

A Gateway is the medium used for receiving API commands from the client and transmitting responses and events back to it.

An Interface is where all API commands are defined and related actions take place.

So, when writing an application the developer will just focus on writing the *API* part of the application.
Everything else will be automatically handled by MIG service.

Example code:
```csharp
var migService = new MigService();

// Add Web Server gateway
var webServiceGw = migService.AddGateway("WebServiceGateway");

// Add Web Socket gateway
var webSocketGw = migService.AddGateway("WebSocketGateway");

// Define an API handler for a specific URL /api/myapp/demo/greet
migService.RegisterApi("myapp/demo/greet", (request)=>{
    // we registered an handler for
    // /api/myapp/demo/greet/<option_0>[/<option_1>/../<option_n>]
    var name = request.Command.GetOption(0);
    // raise an event in the form
    // <domain>, <source>, <description>, <property>, <value>
    migService.RaiseEvent("myapp", "demo", "Reply to greet", "Greet.User", name);
    // return the response (objects are automatically serialized to JSON)
    return new ResponseStatus(Status.Ok);
});

// OR we can define a generic API handler for any request starting with /api/myapp/....
migService.RegisterApi("myapp", (request)=>{
    // we registered an handler for
    // /api/myapp/<module_address>/<command>/<option_0>[/<option_1>/../<option_n>]

    var cmd = request.Command;
    switch(cmd.Address)
    {
    case "demo":
        // /api/myapp/demo/....
        switch (cmd.Command)
        {
        case "greet":
            // /api/myapp/demo/greet/....
            // ....
            break;
        case "ping":
            // /api/myapp/demo/ping/....
            // ...
            break;
        }
        break;
    case "configure":
        // /api/myapp/configure/....
        // ...
        break;
    }

    // return the response (objects are automatically serialized to JSON)
    return new ResponseStatus(Status.Ok);
});
```
In the above example we defined an handler for the API command */api/myapp/demo/greet*.
This command can be issued from the web browser by either using a standard HTTP call
```
$.get('http://localhost/api/myapp/demo/greet/Foo+Bar', function(res) { ... });
```
or by using the websocket client connection
```
wsclient.send('myapp/demo/greet/Foo+Bar');
```

## MIG Gateways

Each gateway can be configured by using the *SetOption* method.

### WebServiceGateway Features and Options

Features

- built-in support for SSE (Server Sent Events) stream (url **/events**)
- basic authentication
- automatic Markdown files to HTML translation
- file caching

Option List

- BaseUrl (base url for HTML files)
- HomePath (folder where to get HTML files from)
- Host (host name or IP - use * for any)
- Port (TCP port)
- Password (currently only support single user *admin*)
- EnableFileCaching (*True* or *False*)

Example
```csharp
var web = migService.AddGateway("WebServiceGateway");
// HTML files folder 
web.SetOption("HomePath", "html");
// base URL for html files
web.SetOption("BaseUrl", "/pages/"); 
// listen on any address
web.SetOption("Host", "*"); 
// TCP port
web.SetOption("Port", "8080");
// disable authentication
web.SetOption("Password", ""); 
// disable file caching
web.SetOption("EnableFileCaching", "False"); 
```

### WebSocketGateway Features and Options

The Web Socket server is based on [WebSocketSharp](https://github.com/sta/websocket-sharp).
See the project home page for informations about all supported features.

Options

- Port (TCP port to listen on)

Example
```csharp
var ws = migService.AddGateway("WebSocketGateway");
ws.SetOption("Port", "8181");
```

### MqttServiceGateway

A MIG Gateway for supporting the MQTT protocol will be available in future releases.

## Interface API Plugins

While in the earlier example we saw how to define an API command dynamically by using the **RegisterApi** method, MIG API commands can also
be extended by using **Interface Plugins**.
Interface Plugins are loadable modules that adds new API commands to MIG.

Example:
```csharp
var zwave = migService.AddInterface("HomeAutomation.ZWave", "MIG.HomeAutomation.dll");
zwave.SetOption("Port", "/dev/ttyUSB0");
```
The example above adds to MIG, API commands for controlling Z-Wave Home Automation hardware.
For a list of currently implemented interfaces see the [MIG API](http://www.homegenie.it/docs/api/mig_api_interfaces.html) documentation page.

## Suggested syntax for API commands

 The following is the suggested syntax for a MIG API command:
```
/api/<api_domain>/<module_address>/<command>[/<option_0>/.../<option_n>]
```
Where **<api_domain>** is used to address a specific API domain, **<module_address>** the target module of the API **<command>**
and **<option_0>...<option_n>** are optional parameters that the **<command>** may require. 

So in the previous example where we used the **RegisterApi** methods, we have:

**<api_domain>** ::= "myapp"

**<module_address>** ::= "demo"

**<command>** ::= "greet"

**<option_0>** ::= "Foo+Bar"


## NuGet Package

MigService  is available as a [NuGet package](https://www.nuget.org/packages/MigService).

Run `Install-Package MigService` in the [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console) or search for “MigService” in your IDE’s package management plug-in.

## License

MigService is open source software, licensed under the terms of GNU GPLV3 license. See the [LICENSE](LICENSE) file for details.
