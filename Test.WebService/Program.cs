/*
  This file is part of MIG (https://github.com/genielabs/mig-service-dotnet)
 
  Copyright (2012-2018) G-Labs (https://github.com/genielabs)

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

using System;
using System.Threading;

using MIG;
using MIG.Gateways;

namespace Test.WebService
{
    class ApiCommands
    {
        public const string Echo = "echo";
        public const string Ping = "ping";
        public const string Greet = "greet";
    }
    
    class MainClass
    {
        public static void Main(string[] args)
        {
            var Log = MigService.Log;
            string webServicePort = "8088";
            string webSocketPort = "8181";

            string authUser = "admin";
            string authPass = "test"; // auth is disabled with empty password

            Log.Info("MigService test APP");
            Log.Info("URL: http://localhost:{0}", webServicePort);

            var migService = new MigService();

            // Add and configure the WebService gateway
            var web = migService.AddGateway(Gateway.WebServiceGateway);
            web.SetOption(WebServiceGatewayOptions.HomePath, "html");
            web.SetOption(WebServiceGatewayOptions.BaseUrl, "/pages/");
            web.SetOption(WebServiceGatewayOptions.Host, "*");
            web.SetOption(WebServiceGatewayOptions.Port, webServicePort);
            //web.SetOption(WebServiceGatewayOptions.Authentication, WebAuthenticationSchema.Digest);
            //web.SetOption(WebServiceGatewayOptions.Authentication, WebAuthenticationSchema.Basic);
            //((WebServiceGateway) web).BasicAuthenticationHandler += (sender, eventArgs) =>
            //{
            //    var user = eventArgs.UserData;
            //    return (user.Name == eventArgs.Username && user.Password == eventArgs.Password)
            //};
            //web.SetOption(WebServiceGatewayOptions.Username, authUser);
            //web.SetOption(WebServiceGatewayOptions.Password, authPass);
            web.SetOption(WebServiceGatewayOptions.EnableFileCaching, "False");

            // Add and configure the WebSocket gateway
            var ws = migService.AddGateway(Gateway.WebSocketGateway);
            ws.SetOption(WebSocketGatewayOptions.Port, webSocketPort);
            //ws.SetOption(WebSocketGatewayOptions.Authentication, WebAuthenticationSchema.Digest);
            //ws.SetOption(WebSocketGatewayOptions.Authentication, WebAuthenticationSchema.Basic);
            //ws.SetOption(WebSocketGatewayOptions.Username, authUser);
            //ws.SetOption(WebSocketGatewayOptions.Password, authPass);

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
