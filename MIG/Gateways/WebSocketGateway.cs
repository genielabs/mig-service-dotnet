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

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: https://github.com/genielabs/mig-service-dotnet
 */

using System;
using System.Collections.Generic;
using MessagePack;
using MIG.Config;
using MIG.Gateways.Authentication;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace MIG.Gateways
{

    public class MigWsServer : WebSocketBehavior
    {
        private WebSocketGateway gateway;

        public MigWsServer()
        {

        }
        public void SetWebSocketGateway(WebSocketGateway gw)
        {
            gateway = gw;
        }

        protected override void OnOpen()
        {
            base.OnOpen();
            var token = Context.QueryString.Get("at");
            if (!gateway.IsAuthorized(token))
            {
                Context.WebSocket.Close(CloseStatusCode.ProtocolError, "Invalid token.");
            }
        }

        protected override void OnMessage(MessageEventArgs args)
        {
            base.OnMessage(args);
            gateway.ProcessRequest(args, Context);
        }
    }

    public class WebSocketGateway : IMigGateway
    {
        public event PreProcessRequestEventHandler PreProcessRequest;
        public event PostProcessRequestEventHandler PostProcessRequest;
        public event UserAuthenticationEventHandler UserAuthenticationHandler;

        private WebSocketServer webSocketServer;
        private List<AuthorizationToken> authorizationTokens = new List<AuthorizationToken>();

        private int servicePort = 8181;
        private string authenticationSchema = WebAuthenticationSchema.None;
        private string authenticationRealm = "MIG Secure Zone";
        private bool ignoreExtensions;
        private bool messagePack;

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
                case WebSocketGatewayOptions.Authentication:
                    if (option.Value == WebAuthenticationSchema.None || option.Value == WebAuthenticationSchema.Token || option.Value == WebAuthenticationSchema.Basic || option.Value == WebAuthenticationSchema.Digest)
                    {
                        authenticationSchema = option.Value;
                    }
                    break;
                case WebSocketGatewayOptions.AuthenticationRealm:
                    authenticationRealm = option.Value;
                    break;
                case WebSocketGatewayOptions.IgnoreExtensions:
                    bool.TryParse(option.Value, out ignoreExtensions);
                    break;
                case WebSocketGatewayOptions.MessagePack:
                    bool.TryParse(option.Value, out messagePack);
                    break;
            }
        }

        public void OnInterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args)
        {
            SendMessage(args.EventData);
        }

        public bool Start()
        {
            bool success = false;
            try
            {
                Stop();
                webSocketServer = new WebSocketServer(servicePort);
                webSocketServer.AddWebSocketService<MigWsServer>("/events", (server) => {
                    // To ignore the extensions requested from a client.
                    server.SetWebSocketGateway(this);
                    server.IgnoreExtensions = ignoreExtensions;
                });
                if (authenticationSchema != WebAuthenticationSchema.None && authenticationSchema != WebAuthenticationSchema.Token)
                {
                    webSocketServer.AuthenticationSchemes = (authenticationSchema == WebAuthenticationSchema.Basic) ? AuthenticationSchemes.Basic : AuthenticationSchemes.Digest;
                    webSocketServer.Realm = authenticationRealm;
                    webSocketServer.UserCredentialsFinder = (id) =>
                    {
                        var user = OnUserAuthentication(new UserAuthenticationEventArgs(id.Name));
                        if (user != null)
                            return new NetworkCredential(user.Name, user.Password, user.Realm);
                        return null;
                    };
                }
                webSocketServer.Start();
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
            if (webSocketServer != null)
            {
                webSocketServer.Stop();
                webSocketServer = null;
            }
        }

        public void ProcessRequest(MessageEventArgs message, WebSocketContext context)
        {
            // check if Data is a JSON string
            bool isJsonPayload = false;
            string requestId = "";
            string messageData = message.Data;
            if (messagePack)
            {
                var request = MigService.Unpack<WsRequest>(message.RawData);
                requestId = request.id;
                messageData = request.data;
                isJsonPayload = true;
            }
            else
            {
                try
                {
                    dynamic jsonPayload = JsonConvert.DeserializeObject(messageData);
                    if (jsonPayload != null && jsonPayload.id != null)
                    {
                        requestId = jsonPayload.id;
                        messageData = jsonPayload.data;
                        isJsonPayload = true;
                    }
                }
                catch (Exception e)
                {
                    // Not valid JSON, process as text message
                }
            }
            var migContext = new MigContext(ContextSource.WebSocketGateway, message);
            var migRequest = new MigClientRequest(migContext, new MigInterfaceCommand(messageData, message));
            OnPreProcessRequest(migRequest);
            if (!migRequest.Handled)
                OnPostProcessRequest(migRequest);
            if (isJsonPayload && !String.IsNullOrEmpty(requestId))
            {
                // send reply to this message using the requestId
                var responseEvent = new MigEvent("#", requestId, "", "Response.Data", migRequest.ResponseData);
                if (messagePack)
                {
                    if (responseEvent.Value is string == false)
                    {
                        responseEvent.Value = MigService.JsonSerialize(responseEvent.Value);
                    }
                    context.WebSocket.Send(MigService.Pack(responseEvent));
                }
                else
                {
                    context.WebSocket.Send(MigService.JsonSerialize(responseEvent));
                }
            }
        }

        public bool IsAuthorized(string token)
        {
            if (authenticationSchema != WebAuthenticationSchema.Token)
                return true;
            var t = authorizationTokens.Find(at => at.Value == token);
            if (t != null) authorizationTokens.Remove(t);
            return t != null && !t.IsExpired;
        }

        public AuthorizationToken GetAuthorizationToken(double expireSeconds)
        {
            var token = new AuthorizationToken(expireSeconds);
            try
            {
                authorizationTokens.RemoveAll(t => t.IsExpired);
            }
            catch (Exception e)
            { 
                // ignored
                Console.Error.WriteLine(e);
            }
            authorizationTokens.Add(token);
            return token;
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
        protected virtual User OnUserAuthentication(UserAuthenticationEventArgs args)
        {
            if (UserAuthenticationHandler != null)
            {
                return UserAuthenticationHandler(this, args);
            }
            return null;
        }

        private void SendMessage(MigEvent message)
        {
            WebSocketServiceHost host = null;
            if (webSocketServer != null && webSocketServer.IsListening)
            {
                webSocketServer.WebSocketServices.TryGetServiceHost("/events", out host);
            }
            if (host == null) return;
            try
            {
                if (messagePack)
                {
                    host.Sessions.BroadcastAsync(MigService.Pack(message), () => { });
                }
                else
                {
                    host.Sessions.BroadcastAsync(MigService.JsonSerialize(message), () => { });
                }
            }
            catch (Exception e)
            {
                MigService.Log.Error(e);
            }
        }
    }
    
    [Serializable, MessagePackObject]
    public class WsRequest
    {
        [Key(0)]
        public string id { get; set; }
        [Key(1)]
        public string data { get; set; }
        public WsRequest()
        {
        }
    }
}
