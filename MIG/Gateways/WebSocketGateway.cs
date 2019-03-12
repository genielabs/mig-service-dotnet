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

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: https://github.com/genielabs/mig-service-dotnet
 */

using System;
using System.Collections.Generic;

using MIG.Config;

using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace MIG.Gateways
{

    public class MigWsServer : WebSocketBehavior
    {
        private WebSocketGateway gateway;

        public MigWsServer(WebSocketGateway gw) : base()
        {
            gateway = gw;
        }

        protected override void OnMessage(MessageEventArgs args)
        {
            gateway.ProcessRequest(args);
        }
    }

    public class WebSocketGateway : IMigGateway
    {
        public event PreProcessRequestEventHandler PreProcessRequest;
        public event PostProcessRequestEventHandler PostProcessRequest;

        private WebSocketServer wsocketServer;
        private int servicePort = 88;
        private string serviceUsername = "admin";
        private string servicePassword = "";

        public WebSocketGateway()
        {
        }

        public List<Option> Options { get; set; }

        public void OnSetOption(Option option)
        {
            switch (option.Name)
            {
                case WebSocketGatewayOptions.Port:
                    int.TryParse(option.Value, out servicePort);
                    break;
                case WebSocketGatewayOptions.Username:
                    serviceUsername = option.Value;
                    break;
                case WebSocketGatewayOptions.Password:
                    servicePassword = option.Value;
                    break;
            }
        }

        public void OnInterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args)
        {
            if (wsocketServer.IsListening)
            {
                wsocketServer.WebSocketServices.Broadcast(MigService.JsonSerialize(args.EventData));
            }
        }

        public bool Start()
        {
            bool success = false;
            try
            {
                Stop();
                wsocketServer = new WebSocketServer(servicePort);
                wsocketServer.AddWebSocketService<MigWsServer>("/events", () => new MigWsServer(this) {
                    // To ignore the extensions requested from a client.
                    IgnoreExtensions = true
                });
                if (!servicePassword.IsNullOrEmpty())
                {
                    wsocketServer.AuthenticationSchemes = AuthenticationSchemes.Basic;
                    wsocketServer.Realm = "WebSocket Auth";
                    wsocketServer.UserCredentialsFinder = id => id.Name == serviceUsername
                        ? new NetworkCredential (serviceUsername, servicePassword)
                        : null;
                    // TODO: also implement Digest auth
                }
                wsocketServer.Start();
                success = true;
            }
            catch (Exception e)
            {
                MigService.Log.Error(e);
            }
            return success;
        }

        public void Stop()
        {
            if (wsocketServer != null)
            {
                wsocketServer.Stop();
                wsocketServer = null;
            }
        }

        public void ProcessRequest(MessageEventArgs args)
        {
            var migContext = new MigContext(ContextSource.WebSocketGateway, args);
            var migRequest = new MigClientRequest(migContext, new MigInterfaceCommand(args.Data));
            OnPreProcessRequest(migRequest);
            if (!migRequest.Handled)
                OnPostProcessRequest(migRequest);
        }

        protected virtual void OnPreProcessRequest(MigClientRequest request)
        {
            if (request != null && PreProcessRequest != null)
                PreProcessRequest(this, new ProcessRequestEventArgs(request));
        }

        protected virtual void OnPostProcessRequest(MigClientRequest request)
        {
            if (request != null && PostProcessRequest != null)
                PostProcessRequest(this, new ProcessRequestEventArgs(request));
        }
    }
}

