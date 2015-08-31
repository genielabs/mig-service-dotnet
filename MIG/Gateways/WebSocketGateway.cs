/*
    This file is part of MIG Project source code.

    MIG is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MIG is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MIG.  If not, see <http://www.gnu.org/licenses/>.  
*/

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: https://github.com/genielabs/mig-service-dotnet
 */

using System;
using System.Collections.Generic;

using MIG.Config;

using WebSocketSharp;
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

    public class WebSocketGateway : MigGateway
    {
        public event PreProcessRequestEventHandler PreProcessRequest;
        public event PostProcessRequestEventHandler PostProcessRequest;

        private WebSocketServer wsocketServer;
        private int servicePort = 8181;

        public WebSocketGateway()
        {
        }

        public List<ConfigurationOption> Options { get; set; }

        public void OnSetOption(ConfigurationOption option)
        {
            if (option.Name.Equals("Port"))
            {
                int.TryParse(option.Value, out servicePort);
            }
        }

        public void OnInterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args)
        {
            wsocketServer.WebSocketServices.Broadcast(MigService.JsonSerialize(args.EventData));
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

