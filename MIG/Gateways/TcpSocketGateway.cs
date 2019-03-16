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
using System.Text;

using MIG.Utility;
using MIG.Config;

namespace MIG.Gateways
{
    public class TcpSocketGateway : IMigGateway
    {
        private const int bufferLength = 1024;

        private TcpServerChannel server;
        private int servicePort = 4502; // 4502 was chosen because it was accessible to Silverlight web apps

        public event PreProcessRequestEventHandler PreProcessRequest;
        public event PostProcessRequestEventHandler PostProcessRequest;

        public List<Option> Options { get; set; }

        public void OnSetOption(Option option)
        {
            if (option.Name.Equals(TcpSocketGatewayOptions.Port))
            {
                int.TryParse(option.Value, out servicePort);
            }
        }

        public void OnInterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args)
        {
            UTF8Encoding encoding = new UTF8Encoding();
            server.SendAll(encoding.GetBytes(MigService.JsonSerialize(args)));
        }

        public bool Start()
        {
            bool success = false;
            server = NetworkConnectivity.CreateTcpServerChannel("server");
            try
            {
                server.Connect(servicePort);
                server.ChannelClientConnected += server_ChannelClientConnected;
                server.ChannelClientDisconnected += server_ChannelClientDisconnected;
                server.DataReceived += server_DataReceived;
                server.ExceptionOccurred += server_ExceptionOccurred;
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
            server.ChannelClientConnected -= server_ChannelClientConnected;
            server.ChannelClientDisconnected -= server_ChannelClientDisconnected;
            server.DataReceived -= server_DataReceived;
            server.ExceptionOccurred -= server_ExceptionOccurred;
            server.Disconnect();
        }

        public void ProcessRequest(ServerDataEventArgs args)
        {
            UTF8Encoding encoding = new UTF8Encoding();
            var message = encoding.GetString(args.Data, 0, args.DataLength);
            var migContext = new MigContext(ContextSource.TcpSocketGateway, args);
            var migRequest = new MigClientRequest(migContext, new MigInterfaceCommand(message));
            OnPreProcessRequest(migRequest);
            if (!migRequest.Handled)
                OnPostProcessRequest(migRequest);
        }

        private void server_ChannelClientConnected(object sender, ServerConnectionEventArgs args)
        {
            server.Receive(bufferLength, args.ClientId);
        }

        private void server_DataReceived(object sender, ServerDataEventArgs args)
        {
            server.Receive(bufferLength, (int)args.ClientId);
            if (args.DataLength > 0)
            {
                ProcessRequest(args);
            }
        }

        private void server_ChannelClientDisconnected(object sender, ServerConnectionEventArgs args)
        {
            // TODO: should auto-reconnect?
        }

        private void server_ExceptionOccurred(object sender, System.IO.ErrorEventArgs e)
        {

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
