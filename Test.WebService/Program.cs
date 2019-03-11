﻿/*
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
using System.IO;
using System.Threading;
using System.Xml.Serialization;

using MIG;
using MIG.Config;

namespace Test.WebService
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            string webPort = "8088";
            string authUser = "admin";
            string authPass = ""; // auth is disabled with empty password

            Console.WriteLine("MigService test APP");
            Console.WriteLine("URL: http://localhost:{0}", webPort);

            var migService = new MigService();

            // Add and configure the Web gateway
            var web = migService.AddGateway("WebServiceGateway");
            web.SetOption("HomePath", "html");
            web.SetOption("BaseUrl", "/pages/");
            web.SetOption("Host", "*");
            web.SetOption("Port", webPort);
            web.SetOption("Username", authUser);
            web.SetOption("Password", authPass);
            web.SetOption("EnableFileCaching", "False");

            // Add and configure the Web Socket gateway
            var ws = migService.AddGateway("WebSocketGateway");
            ws.SetOption("Port", "8181");
            ws.SetOption("Username", authUser);
            ws.SetOption("Password", authPass);

            migService.StartService();

            migService.RegisterApi("myapp/demo", (request) =>
            {
                Console.WriteLine("Received API call from source {0}\n", request.Context.Source);
                Console.WriteLine("[Context data]\n{0}\n", MigService.JsonSerialize(request.Context.Data, true));
                Console.WriteLine("[Mig Command]\n{0}\n", MigService.JsonSerialize(request.Command, true));

                var cmd = request.Command;

                // cmd.Domain is the first element in the API URL (myapp)
                // cmd.Address is the second element in the API URL (demo)
                // cmd.Command is the third element in the API URL (greet | echo | ping)
                // cmd.GetOption(<n>) will give all the subsequent elements in the API URL (0...n)

                switch (cmd.Command)
                {
                case "greet":
                    var name = cmd.GetOption(0);
                    migService.RaiseEvent(typeof(MainClass), cmd.Domain, cmd.Address, "Reply to Greet", "Greet.User", name);
                    break;
                case "echo":
                    string fullRequestPath = cmd.OriginalRequest;
                    migService.RaiseEvent(typeof(MainClass), cmd.Domain, cmd.Address, "Reply to Echo", "Echo.Data", fullRequestPath);
                    break;
                case "ping":
                    migService.RaiseEvent(typeof(MainClass), cmd.Domain, cmd.Address, "Reply to Ping", "Ping.Reply", "PONG");
                    break;
                }

                return new ResponseStatus(Status.Ok);
            });

            while (true)
            {
                Thread.Sleep(10000);
            }

        }
    }
}
