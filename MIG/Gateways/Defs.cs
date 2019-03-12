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

namespace MIG.Gateways
{
  public static class Gateway
  {
    public const string WebServiceGateway = "WebServiceGateway";
    public const string WebSocketGateway = "WebSocketGateway";
  }
  
  public static class WebServiceGatewayOptions
  {
    public const string BaseUrl = "BaseUrl";
    public const string HomePath = "HomePath";
    public const string Host = "Host";
    public const string Port = "Port";
    public const string Username = "Username";
    public const string Password = "Password";
    public const string EnableFileCaching = "EnableFileCaching";
    public const string CorsAllowOrigin = "CorsAllowOrigin";
    public const string HttpCacheIgnorePrefix = "HttpCacheIgnore.";
    public const string UrlAliasPrefix = "UrlAlias.";
  }

  public static class WebSocketGatewayOptions
  {
    public const string Port = "Port";
    public const string Username = "Username";
    public const string Password = "Password";
  }

}