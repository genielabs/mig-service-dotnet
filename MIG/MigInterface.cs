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
        // TODO: move this specific field to MIG.HomeAutomation
        public ModuleTypes ModuleType { get; set; }
        public string Description { get; set; }
        public dynamic CustomData { get; set; }
    }

    // TODO: move this specific enum to MIG.HomeAutomation
    public enum ModuleTypes
    {
        Generic = -1,
        Program,
        Switch,
        Light,
        Dimmer,
        Sensor,
        Temperature,
        Siren,
        Fan,
        Thermostat,
        Shutter,
        DoorWindow,
        DoorLock,
        MediaTransmitter,
        MediaReceiver
        //siren, alarm, motion sensor, door sensor, thermal sensor, etc.
    }
}

