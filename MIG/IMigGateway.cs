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

using System.Collections.Generic;
using MIG.Config;

namespace MIG
{
    public static class GatewayExtension
    {
        public static string GetName(this IMigGateway gateway)
        {
            return gateway.GetType().Name;
        }

        public static Option GetOption(this IMigGateway gateway, string option)
        {
            if (gateway.Options != null)
            {
                return gateway.Options.Find(o => o.Name == option);
            }
            return null;
        }

        public static void SetOption(this IMigGateway gateway, string option, string value)
        {
            MigService.Log.Debug("{0}: {1}={2}", gateway.GetName(), option, value);
            var opt = gateway.GetOption(option);
            if (opt == null)
            {
                opt = new Option(option);
                gateway.Options.Add(opt);
            }
            opt.Value = value;
            gateway.OnSetOption(opt);
        }
    }

    public interface IMigGateway
    {
        event PreProcessRequestEventHandler PreProcessRequest;
        event PostProcessRequestEventHandler PostProcessRequest;

        void OnSetOption(Option option);
        void OnInterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args);

        List<Option> Options { get; set; }

        bool Start();
        void Stop();
    }
}
