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

namespace MIG.Gateways
{
  /// <summary>
  /// Types of available MIG Gateways
  /// </summary>
  public static class Gateways
  {
    public const string WebServiceGateway = "WebServiceGateway";
    public const string WebSocketGateway = "WebSocketGateway";
    public const string TcpSocketGateway = "TcpSocketGateway";
    public const string MqttServiceGateway = "MqttServiceGateway";
  }
  
  public static class WebServiceGatewayOptions
  {
    public const string BaseUrl = "BaseUrl";
    public const string HomePath = "HomePath";
    public const string Host = "Host";
    public const string Port = "Port";
    public const string Authentication = "Authentication";
    public const string AuthenticationRealm = "AuthenticationRealm";
    public const string EnableFileCaching = "EnableFileCaching";
    public const string CorsAllowOrigin = "CorsAllowOrigin";
    public const string HttpCacheIgnorePrefix = "HttpCacheIgnore.";
    public const string UrlAliasPrefix = "UrlAlias.";
  }

  public static class WebSocketGatewayOptions
  {
    public const string Port = "Port";
    public const string Authentication = "Authentication";
    public const string AuthenticationRealm = "AuthenticationRealm";
    public const string IgnoreExtensions = "IgnoreExtensions";
    public const string MessagePack = "MessagePack";
  }

  public static class TcpSocketGatewayOptions
  {
    public const string Port = "Port";
  }

}
