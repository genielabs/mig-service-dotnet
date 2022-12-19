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

using MIG.Utility.Encryption;

namespace MIG.Gateways.Authentication
{

    public static class WebAuthenticationSchema
    {
        public const string None = "None";
        public const string Basic = "Basic";
        public const string Digest = "Digest";
        public const string Token = "Token";
    }

    public class AuthorizationToken
    {
        private readonly double expireSeconds;
        private readonly string token;
        private readonly DateTime creationDate = DateTime.UtcNow;

        public AuthorizationToken(double expireSeconds)
        {
            this.token = Guid.NewGuid().ToString();
            this.expireSeconds = expireSeconds;
        }
        public string Value => token;
        public bool IsExpired => expireSeconds != 0 && (DateTime.UtcNow - creationDate).TotalSeconds > expireSeconds;
    }

    public class Digest
    {
        public static string CreatePassword(string username, string realm, string password)
        {
            return MD5Core
                .GetHashString(username + ":" + realm + ":" + password).ToLower();
        }
    }

    public class User
    {
        public User(string name, string realm, string password)
        {
            Name = name;
            Realm = realm;
            Password = password;;
        }
        public readonly string Name;
        public readonly string Realm;
        public readonly string Password;
    }

    public class UserAuthenticationEventArgs
    {
        public UserAuthenticationEventArgs(string username)
        {
            Username = username;
        }
        /// <summary>
        /// HTTP authentication user name provided by the client.
        /// </summary>
        public string Username { get; internal set; }
    }
    public delegate User UserAuthenticationEventHandler(object sender, UserAuthenticationEventArgs e);
}
