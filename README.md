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

While in the earlier examples we used the **RegisterApi** method to dynamically add new API commands, these can also be added by using **Interface** plugins.
Interface plugins are library modules (dll) that can be dinamically loaded into MIG. See [MIG.HomeAutomation](MIG.HomeAutomation) project source code for an example about how to create a MIG Interface plugin.

Code for loading an Interface plugin:
```csharp
// Load the "HomeAutomation.ZWave" Interface from the "MIG.HomeAutomation.dll" library file
var zwave = migService.AddInterface("HomeAutomation.ZWave", "MIG.HomeAutomation.dll");
// configure the serial port
zwave.SetOption("Port", "/dev/ttyUSB0");
```
The example above is used to load the *HomeAutomation.ZWave* Interface plugin into MIG. This will add a set of new API commands for controlling Z-Wave Home Automation hardware.
For a list of currently available Interfaces and API, see the [MIG API](http://www.homegenie.it/docs/api/mig_api_zwave.html) documentation page.

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

MigService v1.0.0-beta *(pre-release)* is available as a [NuGet package](https://www.nuget.org/packages/MigService).

Run `Install-Package MigService` in the [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console) or search for “MIG Service” in your IDE’s package management plug-in.

## Related Projects

- https://github.com/genielabs/intel-upnp-dlna
- https://github.com/genielabs/mig-controllers-lircremote
- https://github.com/genielabs/mig-homeauto-insteon
- https://github.com/genielabs/mig-homeauto-w800rf32
- https://github.com/genielabs/mig-media-v4lcamera
- https://github.com/genielabs/mig-protocols-mqttbroker
- https://github.com/genielabs/mig-sbc-weecoboard

## License

MigService is open source software, licensed under the terms of GNU GPLV3 license. See the [LICENSE](LICENSE) file for details.
