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
using System.Linq;
using System.Text;
using MIG.Config;

namespace MIG
{
    public static class GatewayExtension
    {
        public static string GetName(this MigGateway gateway)
        {
            return gateway.GetType().ToString();
        }

        public static ConfigurationOption GetOption(this MigGateway gateway, string option)
        {
            if (gateway.Options != null)
            {
                return gateway.Options.Find(o => o.Name == option);
            }
            return null;
        }

        public static void SetOption(this MigGateway gateway, string option, string value)
        {
            MigService.Log.Trace("{0}: {1}={2}", gateway.GetName(), option, value);
            var opt = gateway.GetOption(option);
            if (opt == null)
            {
                opt = new ConfigurationOption() { Name = option };
                gateway.Options.Add(opt);
            }
            opt.Value = value;
            gateway.OnSetOption(opt);
        }
    }

    public interface MigGateway
    {
        event PreProcessRequestEventHandler PreProcessRequest;
        event PostProcessRequestEventHandler PostProcessRequest;

        void OnSetOption(ConfigurationOption option);
        void OnInterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args);

        List<ConfigurationOption> Options { get; set; }

        bool Start();
        void Stop();
    }
}
