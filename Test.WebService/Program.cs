/*
  This file is part of MIG (https://github.com/genielabs/mig-service-dotnet)

  Copyright (2012-2023) G-Labs (https://github.com/genielabs)

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
*/

using MIG;
using MIG.Gateways;
using MIG.Gateways.Authentication;

namespace Test.WebService
{
    class ApiCommands
    {
        public const string Echo = "echo";
        public const string Ping = "ping";
        public const string Greet = "greet";
        public const string Token = "token";
    }

    class MainClass
    {
        public static void Main(string[] args)
        {
            var Log = MigService.Log;
            string webServicePort = "8088";
            string webSocketPort = "8181";

            string authUser = "admin";
            string authPass = "password";
            string authRealm = "MIG Secure Zone";

            Log.Info("MigService test APP");
            Log.Info("URL: http://localhost:{0}", webServicePort);

            var migService = new MigService();

            // Add and configure the WebService gateway
            var web = (WebServiceGateway)migService.AddGateway(Gateways.WebServiceGateway);
            web.SetOption(WebServiceGatewayOptions.HomePath, "html");
            web.SetOption(WebServiceGatewayOptions.BaseUrl, "/pages/");
            // for deploying modern web app (eg. Angular 2 apps)
            web.SetOption(WebServiceGatewayOptions.UrlAliasPrefix + "1", "app/*:app/index.html");
            web.SetOption(WebServiceGatewayOptions.UrlAliasPrefix + "2", "hg/html/pages/control/widgets/homegenie/generic/images/*:assets/widgets/compat/images/*");
            web.SetOption(WebServiceGatewayOptions.Host, "*");
            web.SetOption(WebServiceGatewayOptions.Port, webServicePort);
            if (!String.IsNullOrEmpty(authUser) && !String.IsNullOrEmpty(authPass))
            {
                //web.SetOption(WebServiceGatewayOptions.Authentication, WebAuthenticationSchema.Basic);
                web.SetOption(WebServiceGatewayOptions.Authentication, WebAuthenticationSchema.Digest);
                web.SetOption(WebServiceGatewayOptions.AuthenticationRealm, authRealm);
                web.UserAuthenticationHandler += (sender, eventArgs) =>
                {
                    if (eventArgs.Username == authUser)
                    {
                        // WebServiceGateway requires password to be encrypted using the `Digest.CreatePassword(..)` method.
                        // This applies both to 'Digest' and 'Basic' authentication methods.
                        string password = Digest.CreatePassword(authUser, authRealm, authPass);
                        return new User(authUser, authRealm, password);
                    }
                    return null;
                };
            }
            web.SetOption(WebServiceGatewayOptions.EnableFileCaching, "False");

            // Add and configure the WebSocket gateway
            var ws = (WebSocketGateway)migService.AddGateway(Gateways.WebSocketGateway);
            ws.SetOption(WebSocketGatewayOptions.Port, webSocketPort);
            // WebSocketGateway access via authorization token
            ws.SetOption(WebSocketGatewayOptions.Authentication, WebAuthenticationSchema.Token);
            /*
            if (!String.IsNullOrEmpty(authUser) && !String.IsNullOrEmpty(authPass))
            {
                //ws.SetOption(WebSocketGatewayOptions.Authentication, WebAuthenticationSchema.Basic);
                ws.SetOption(WebSocketGatewayOptions.Authentication, WebAuthenticationSchema.Digest);
                ws.SetOption(WebSocketGatewayOptions.AuthenticationRealm, authRealm);
                ((WebSocketGateway) ws).UserAuthenticationHandler += (sender, eventArgs) =>
                {
                    if (eventArgs.Username == authUser)
                    {
                        return new User(authUser, authRealm, authPass);
                    }
                    return null;
                };
            }
            */

            migService.StartService();

            // API commands and events are exposed to all active gateways (WebService and WebSocket in this example)
            migService.RegisterApi("myapp/demo", (request) =>
            {
                Log.Debug("Received API call over {0}\n", request.Context.Source);
                Log.Debug("[Context data]\n{0}\n", request.Context.Data);
                Log.Debug("[Mig Command]\n{0}\n", MigService.JsonSerialize(request.Command, true));

                var cmd = request.Command;

                // cmd.Domain is the first element in the API URL (myapp)
                // cmd.Address is the second element in the API URL (demo)
                // cmd.Command is the third element in the API URL (greet | echo | ping)
                // cmd.GetOption(<n>) will give all the subsequent elements in the API URL (0...n)

                switch (cmd.Command)
                {
                case ApiCommands.Token:
                    // authorization token will expire in 5 seconds
                    var token = ws.GetAuthorizationToken(5);
                    return new ResponseText(token.Value);
                case ApiCommands.Greet:
                    var name = cmd.GetOption(0);
                    migService.RaiseEvent(
                        typeof(MainClass),
                        cmd.Domain,
                        cmd.Address,
                        "Reply to Greet",
                        "Greet.User",
                        name
                    );
                    break;
                case ApiCommands.Echo:
                    string fullRequestPath = cmd.OriginalRequest;
                    migService.RaiseEvent(
                        typeof(MainClass),
                        cmd.Domain,
                        cmd.Address,
                        "Reply to Echo",
                        "Echo.Data",
                        fullRequestPath
                    );
                    break;
                case ApiCommands.Ping:
                    migService.RaiseEvent(
                        typeof(MainClass),
                        cmd.Domain,
                        cmd.Address,
                        "Reply to Ping",
                        "Ping.Reply",
                        "PONG"
                    );
                    break;
                }

                return new ResponseStatus(Status.Ok);
            });

            while (true)
            {
                Thread.Sleep(5000);
            }
        }
    }
}
