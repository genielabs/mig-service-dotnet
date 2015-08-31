# MIG libray for .Net/Mono

MIG is a .Net library providing an integrated solution for developing networked applications and real time web applications.

## How it works

We have two main actors in MIG: **Gateway** and **Interface**.

A Gateway is the medium used for receiving API commands from the client and for transmitting responses and events back to it.

An Interface is where all API commands are defined and related actions take place.

So, when writing an application based on MIG, the developer will just focus on the *API* coding.

Once the API commands are implemented, the application is ready to communicate with its clients through the desired **Gateway** medium. This can be a raw TCP socket server *(TcpSocketGateway)*, an HTTP server *(WebServiceGateway)*, a WebSocket server *(WebSocketGateway)*, a MQTT server *(MqttServiceGateway)* or even **ALL** of them at the same time, so to have a multi-protocol support for the application.

Example code:
```csharp
var migService = new MigService();

// Add Web Server gateway
var webServiceGw = migService.AddGateway("WebServiceGateway");

// Add Web Socket gateway
var webSocketGw = migService.AddGateway("WebSocketGateway");

// Define an API handler for myapp/demo/greet command
migService.RegisterApi("myapp/demo/greet", (request)=>{
    // we registered an handler for
    // myapp/demo/greet/<option_0>[/<option_1>/../<option_n>]
    var name = request.Command.GetOption(0);
    // raise an event in the form
    // <domain>, <source>, <description>, <property>, <value>
    migService.RaiseEvent("myapp", "demo", "Reply to greet", "Greet.User", name);
    // return the response (objects are automatically serialized to JSON)
    return new ResponseStatus(Status.Ok);
});

// OR we can define a generic API handler for any request starting with myapp/....
migService.RegisterApi("myapp", (request)=>{
    // we registered an handler for
    // myapp/<module_address>/<command>/<option_0>[/<option_1>/../<option_n>]

    var cmd = request.Command;
    switch(cmd.Address)
    {
    case "demo":
        // myapp/demo/....
        switch (cmd.Command)
        {
        case "greet":
            // myapp/demo/greet/....
            // ....
            break;
        case "ping":
            // myapp/demo/ping/....
            // ...
            break;
        }
        break;
    case "configure":
        // myapp/configure/....
        // ...
        break;
    }

    // return the response (objects are automatically serialized to JSON)
    return new ResponseStatus(Status.Ok);
});
```
In the above example we defined an handler for the API command *myapp/demo/greet*.
This command can be issued from the web browser by either using a standard HTTP call
```
// HTTP service API commands have to be issued using the /api/ prefix
$.get('http://localhost/api/myapp/demo/greet/Foo+Bar', function(res) { ... });
```
or by using the websocket client connection
```
wsclient.send('myapp/demo/greet/Foo Bar');
```

## Gateways

Each gateway can be configured by using the *SetOption* method.

### WebServiceGateway

Features

- HTTP server with built-in support for SSE (Server Sent Events) stream (url **/events**)
- Basic Authentication
- Automatic Markdown files to HTML translation
- File caching

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

### WebSocketGateway

The Web Socket server is based on [WebSocketSharp](https://github.com/sta/websocket-sharp).
See the project home page for informations about all supported features.

Options

- Port (TCP port to listen on)

Example
```csharp
var ws = migService.AddGateway("WebSocketGateway");
ws.SetOption("Port", "8181");
```

### TcpSocketGateway

Options

- Port (TCP port to listen on)


### MqttServiceGateway

A MIG Gateway for supporting the MQTT protocol will be available in future releases.

## Interfaces 

While in the earlier example we saw how to define an API command dynamically by using the **RegisterApi** method, MIG API commands can also
be extended by using **Interface Plugins**.
Interface Plugins are loadable modules (dll) that adds new API commands to MIG.

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
<api_domain>/<module_address>/<command>[/<option_0>/.../<option_n>]
```
Where ```<api_domain>``` is used to address a specific API domain, ```<module_address>``` the target module of the API ```<command>```
and ```<option_0>...<option_n>``` are optional parameters that the ```<command>``` may require. 

So in the previous example where we used the **RegisterApi** methods, we have:
```
<api_domain> ::= "myapp"
<module_address> ::= "demo"
<command> ::= "greet"
<option_0> ::= "Foo+Bar"
```

## NuGet Package

MigService  is available as a [NuGet package](https://www.nuget.org/packages/MigService).

Run `Install-Package MigService` in the [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console) or search for “MigService” in your IDE’s package management plug-in.

## License

MigService is open source software, licensed under the terms of GNU GPLV3 license. See the [LICENSE](LICENSE) file for details.
