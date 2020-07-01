[![Build status](https://ci.appveyor.com/api/projects/status/6genehqw9wtuuxkl?svg=true)](https://ci.appveyor.com/project/genemars/mig-service-dotnet)
[![NuGet](https://img.shields.io/nuget/v/MIG.svg)](https://www.nuget.org/packages/MIG/)
![License](https://img.shields.io/github/license/genielabs/mig-service-dotnet.svg)

# MIG libray for .Net/Mono

MIG is a .Net library providing an integrated solution for developing networked applications
and real time web applications.

## How it works

We have two main actors in MIG: **Gateway** and **Interface**.

A *Gateway* is the medium used for receiving API commands from the client and for transmitting
responses and events back to it.

An *Interface* is where all API commands are defined and related actions take place.

So, when writing an application based on MIG, the developer will just focus on the *API* coding.

Once the API commands are implemented, the application is ready to communicate with its clients
through the desired **Gateway** medium. This can be a raw TCP socket server *(TcpSocketGateway)*,
an HTTP server *(WebServiceGateway)*, a WebSocket server *(WebSocketGateway)*, a MQTT server
*(MqttServiceGateway)* or even **ALL** of them at the same time, so to have a multi-protocol
support for the application.

Example code:
```csharp
var migService = new MigService();

// Add Web Server gateway
var webServiceGw = migService.AddGateway(Gateways.WebServiceGateway);

// Add Web Socket gateway
var webSocketGw = migService.AddGateway(Gateways.WebSocketGateway);

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
- Digest/Basic Authentication
- Automatic Markdown files to HTML translation
- File caching

Option List

- BaseUrl (base url for HTML files)
- HomePath (folder where to get HTML files from)
- Host (host name or IP - use `*` for any)
- Port (TCP port, default: *`80`*)
- Authentication (`None`, `Digest` or `Basic`, default: `None`)
- AuthenticationRealm (The authentication realm, default: `MIG Secure Zone`)
- EnableFileCaching (*`True`* or *`False`*, default: *`False`*)
- corsAllowOrigin (default: *`*`*)

Example
```csharp
var web = migService.AddGateway(Gateways.WebServiceGateway);
// HTML files folder 
web.SetOption(WebServiceGatewayOptions.HomePath, "html");
// base URL for html files
web.SetOption(WebServiceGatewayOptions.BaseUrl, "/pages/"); 
// listen on any address
web.SetOption(WebServiceGatewayOptions.Host, "*"); 
// TCP port
web.SetOption(WebServiceGatewayOptions.Port, "8080");
// enable authentication
web.SetOption(WebServiceGatewayOptions.Authentication, "Digest"); 
// disable file caching
web.SetOption(WebServiceGatewayOptions.EnableFileCaching, "False"); 
// disable CORS
web.SetOption(WebServiceGatewayOptions.CorsAllowOrigin, ""); 
// add a url alias (`path/to/myfile.jpg` will serve `real/path/file.jpg`)
web.SetOption(WebServiceGatewayOptions.UrlAliasPrefix, "path/to/myfile.jpg:real/path/file.jpg");
// add web-app folder (used for deploying SPA and PWA such as Angular2 apps)
// all requested files non found in `app/` will be redirected to `app/index.html`
web.SetOption(WebServiceGatewayOptions.UrlAliasPrefix, "app/*:app/index.html");
```

#### User authentication with `Digest` or `Basic` methods

```
string authReam = "My App Realm";
web.SetOption(WebServiceGatewayOptions.Authentication, WebAuthenticationSchema.Digest);
web.SetOption(WebServiceGatewayOptions.AuthenticationRealm, authRealm);
web.UserAuthenticationHandler += (sender, eventArgs) =>
{
    // lookup 'eventArgs.Username' in your database
    var user = GetUser(eventArgs.Username);
    if (user != null)
    {
        // WebServiceGateway requires password to be encrypted using the `Digest.CreatePassword(..)` method.
        // This applies both to 'Digest' and 'Basic' authentication methods.
        // So if 'user.Password' is stored in clear (not recommended) it must be encrypted
        string encryptedPassword = Digest.CreatePassword(user.Username, authRealm, user.Password);
        // if 'user.Password' is already encrypted the previous line can be removed
        return new User(user.Username, authRealm, encryptedPassword);
    }
    return null;
};
```

### WebSocketGateway

Options

- Port (TCP port to listen on)
- Authentication (`None`, `Token`, `Digest` or `Basic`, default: `None`)
- AuthenticationRealm (The authentication realm, default: `MIG Secure Zone`)

Example
```csharp
var ws = migService.AddGateway(Gateways.WebSocketGateway);
ws.SetOption(WebSocketGatewayOptions.Port, "8181");
```

#### Authentication using Token

When `Authentication` option is set to `Token`, the websocket service will require the client
to connect by providing an authentication token in the url query-string as the `at` parameter.

The authorization token is obtained by calling the following method

```
double expireSeconds = 5; // 5 seconds validity
var token = ws.GetAuthorizationToken(expireSeconds);
// ...
```

When the client is a browser, then `GetAuthorizationToken` can be exposed implementing a custom API method as shown in
the included example application (Test.WebService).

The following snippet from the example application shows how to request and use the token in JavaScript:

```js
var webSocket;
fetch('/api/myapp/demo/token').then(function(response) {
    return response.json();
}).then(function(data) {
    var token = data.ResponseValue;
    webSocket = new WebSocket("ws://localhost:8181/events?at="+token);
    // ...
}).catch(function() {
    // ...
});
```

#### User authentication with `Digest` or `Basic` methods

This is similar as seen for `WebServiceGateway` the only difference is that the password is not encrypted.


### TcpSocketGateway

Options

- Port (TCP port to listen on)


### MqttServiceGateway

A MIG Gateway for supporting the MQTT protocol will be integrated in future releases.
In the meantime a simple MQTT broker implementation is available as a *MIG Interface*
from [this repository](https://github.com/genielabs/mig-protocols-mqttbroker).

## Interfaces 

While in the earlier examples we used the **RegisterApi** method to dynamically add new
API commands, these can also be added by using **Interface** plugins.
Interface plugins are library modules (dll) that can be dinamically loaded into MIG.
See [MIG.HomeAutomation](https://github.com/genielabs/mig-homeauto)
project source code for an example about how to create a MIG Interface plugin.

Code for loading an Interface plugin:
```csharp
// Load the "HomeAutomation.ZWave" Interface from the "MIG.HomeAutomation.dll" library file
var zwave = migService.AddInterface("HomeAutomation.ZWave", "MIG.HomeAutomation.dll");
// configure the serial port
zwave.SetOption("Port", "/dev/ttyUSB0");
```
The example above is used to load the *HomeAutomation.ZWave* Interface plugin into MIG.
This will add a set of new API commands for controlling Z-Wave Home Automation hardware.
For a list of currently available Interfaces and API, see the
[MIG API](https://genielabs.github.io/HomeGenie/api/mig/mig_api_zwave.html) documentation page.

## Suggested syntax for API commands

 The following is the suggested syntax for a MIG API command:
```
<api_domain>/<module_address>/<command>[/<option_0>/.../<option_n>]
```
Where ```<api_domain>``` is used to address a specific API domain, ```<module_address>```
the target module of the API ```<command>```
and ```<option_0>...<option_n>``` are optional parameters that the ```<command>``` may require. 

So in the previous example where we used the **RegisterApi** methods, we have:
```
<api_domain> ::= "myapp"
<module_address> ::= "demo"
<command> ::= "greet"
<option_0> ::= "Foo+Bar"
```

## NuGet Package

MIG is available as a [NuGet package](https://www.nuget.org/packages/MIG).

Run `Install-Package MIG` in the [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console) or search for “MIG” in your IDE’s package management console.

## Related Projects

- https://github.com/genielabs/HomeGenie (smart home server based on MIG)
- https://github.com/genielabs/mig-homeauto (x10/Z-Wave)
- https://github.com/genielabs/mig-homeauto-insteon
- https://github.com/genielabs/mig-protocols (UPnP/DLNA)
- https://github.com/genielabs/mig-protocols-mqttbroker
- https://github.com/genemars/mig-controllers-lircremote
- https://github.com/genemars/mig-homeauto-w800rf32
- https://github.com/genemars/mig-media-v4lcamera
- https://github.com/genemars/mig-sbc-weecoboard

## License

MIG is open source software, licensed under the terms of Apache license 2.0. See the [LICENSE](LICENSE) file for details.
