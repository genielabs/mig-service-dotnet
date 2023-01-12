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
    public static class Extensions
    {
        public static string GetDomain(this MigInterface iface)
        {
            string domain = iface.GetType().Namespace.ToString();
            domain = domain.Substring(domain.LastIndexOf(".") + 1) + "." + iface.GetType().Name.ToString();
            return domain;
        }

        public static Option GetOption(this MigInterface iface, string option)
        {
            if (iface.Options != null)
            {
                return iface.Options.Find(o => o.Name == option);
            }
            return null;
        }

        public static void SetOption(this MigInterface iface, string option, string value)
        {
            MigService.Log.Trace("{0}: {1}={2}", iface.GetDomain(), option, value);
            var opt = iface.GetOption(option);
            if (opt == null)
            {
                opt = new Option(option);
                iface.Options.Add(opt);
            }
            opt.Value = value;
            iface.OnSetOption(opt);
        }
    }

    public interface MigInterface
    {
        bool IsEnabled { get; set; }

        List<InterfaceModule> GetModules();

        /// <summary>
        /// sets the interface options.
        /// </summary>
        /// <param name="options">Options.</param>
        List<Option> Options { get; set; }

        void OnSetOption(Option option);

        /// <summary>
        /// all input data coming from connected device
        /// is routed via InterfacePropertyChangedAction event
        /// </summary>
        event InterfacePropertyChangedEventHandler InterfacePropertyChanged;
        event InterfaceModulesChangedEventHandler InterfaceModulesChanged;

        /// <summary>
        /// entry point for sending commands (control/configuration)
        /// to the connected device. 
        /// </summary>
        /// <param name="command">MIG interface command</param>
        /// <returns></returns>
        object InterfaceControl(MigInterfaceCommand command);

        /// <summary>
        /// this value can be actively polled to detect
        /// current interface connection state
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// connect to the device interface / perform all setup
        /// </summary>
        /// <returns>a boolean indicating if the connection was succesful</returns>
        bool Connect();

        /// <summary>
        /// disconnect the device interface / perform everything needed for shutdown/cleanup
        /// </summary>
        void Disconnect();

        /// <summary>
        /// this return true if the device has been found in the system (probing)
        /// </summary>
        /// <returns></returns>
        bool IsDevicePresent();

    }

    public class InterfaceModule
    {
        public string Domain { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
        public dynamic CustomData { get; set; }
    }
}

