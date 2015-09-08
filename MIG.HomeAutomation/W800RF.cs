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
using System.Threading;

using W800Rf32Lib;
using System.Collections.Generic;
using MIG.Interfaces.HomeAutomation.Commons;
using MIG.Config;

namespace MIG.Interfaces.HomeAutomation
{
    public class W800RF : MigInterface
    {

        // TODO: Add bool option "Disable Virtual Modules"
        // TODO: Add bool option "Discard unrecognized RF messages"

        #region Private fields

        private const string X10_DOMAIN = "HomeAutomation.X10";
        private RfReceiver w800Rf32;
        private Timer rfPulseTimer;
        private List<InterfaceModule> modules;

        #endregion

        #region Lifecycle

        public W800RF()
        {
            w800Rf32 = new RfReceiver();
            w800Rf32.RfCommandReceived += W800Rf32_RfCommandReceived;
            w800Rf32.RfDataReceived += W800Rf32_RfDataReceived;
            w800Rf32.RfSecurityReceived += W800Rf32_RfSecurityReceived;
            modules = new List<InterfaceModule>();
            // Add RF receiver module
            InterfaceModule module = new InterfaceModule();
            module.Domain = this.GetDomain();
            module.Address = "RF";
            module.ModuleType = ModuleTypes.Sensor;
            modules.Add(module);
        }

        #endregion

        #region MIG Interface members

        public event InterfaceModulesChangedEventHandler InterfaceModulesChanged;
        public event InterfacePropertyChangedEventHandler InterfacePropertyChanged;

        public bool IsEnabled { get; set; }

        public List<Option> Options { get; set; }

        public void OnSetOption(Option option)
        {
            // TODO: check if this is working
            if (IsEnabled)
                Connect();
        }

        public List<InterfaceModule> GetModules()
        {
            return modules;
        }

        public bool Connect()
        {
            w800Rf32.PortName = this.GetOption("Port").Value;
            OnInterfaceModulesChanged(this.GetDomain());
            return w800Rf32.Connect();
        }

        public void Disconnect()
        {
            w800Rf32.Disconnect();
        }

        public bool IsConnected
        {
            get { return w800Rf32.IsConnected; }
        }

        public bool IsDevicePresent()
        {
            bool present = true;
            return present;
        }

        public object InterfaceControl(MigInterfaceCommand request)
        {
            return "";
        }

        #endregion

        #region Private members

        #region W800RF32Lib Events

        private void W800Rf32_RfSecurityReceived(object sender, RfSecurityReceivedEventArgs args)
        {
            string address = "S-" + args.Address.ToString("X6");
            var moduleType = ModuleTypes.Sensor;
            if (args.Event.ToString().StartsWith("DoorSensor1_"))
            {
                address += "01";
                moduleType = ModuleTypes.DoorWindow;
            }
            else if (args.Event.ToString().StartsWith("DoorSensor2_"))
            {
                address += "02";
                moduleType = ModuleTypes.DoorWindow;
            }
            else if (args.Event.ToString().StartsWith("Motion_"))
            {
                moduleType = ModuleTypes.Sensor;
            }
            else if (args.Event.ToString().StartsWith("Remote_"))
            {
                address = "S-REMOTE";
                moduleType = ModuleTypes.Sensor;
            }
            var module = modules.Find(m => m.Address == address);
            if (module == null)
            {
                module = new InterfaceModule();
                module.Domain = X10_DOMAIN;
                module.Address = address;
                module.Description = "W800RF32 security module";
                module.ModuleType = moduleType;
                module.CustomData = 0.0D;
                modules.Add(module);
                OnInterfacePropertyChanged(this.GetDomain(), "1", "W800RF32 Receiver", ModuleEvents.Receiver_Status, "Added security module " + address);
                OnInterfaceModulesChanged(X10_DOMAIN);
            }
            switch (args.Event)
            {
            case X10RfSecurityEvent.DoorSensor1_Alert:
            case X10RfSecurityEvent.DoorSensor2_Alert:
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Status_Level, 1);
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Sensor_Tamper, 0);
                break;
            case X10RfSecurityEvent.DoorSensor1_Alert_Tarmper:
            case X10RfSecurityEvent.DoorSensor2_Alert_Tamper:
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Status_Level, 1);
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Sensor_Tamper, 1);
                break;
            case X10RfSecurityEvent.DoorSensor1_Normal:
            case X10RfSecurityEvent.DoorSensor2_Normal:
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Status_Level, 0);
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Sensor_Tamper, 0);
                break;
            case X10RfSecurityEvent.DoorSensor1_Normal_Tamper:
            case X10RfSecurityEvent.DoorSensor2_Normal_Tamper:
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Status_Level, 0);
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Sensor_Tamper, 1);
                break;
            case X10RfSecurityEvent.DoorSensor1_BatteryLow:
            case X10RfSecurityEvent.DoorSensor2_BatteryLow:
            case X10RfSecurityEvent.Motion_BatteryLow:
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Status_Battery, 10);
                break;
            case X10RfSecurityEvent.DoorSensor1_BatteryOk:
            case X10RfSecurityEvent.DoorSensor2_BatteryOk:
            case X10RfSecurityEvent.Motion_BatteryOk:
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Status_Battery, 100);
                break;
            case X10RfSecurityEvent.Motion_Alert:
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Status_Level, 1);
                break;
            case X10RfSecurityEvent.Motion_Normal:
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Sensor", ModuleEvents.Status_Level, 0);
                break;
            case X10RfSecurityEvent.Remote_Arm:
            case X10RfSecurityEvent.Remote_Disarm:
            case X10RfSecurityEvent.Remote_Panic:
            case X10RfSecurityEvent.Remote_Panic_15:
            case X10RfSecurityEvent.Remote_LightOn:
            case X10RfSecurityEvent.Remote_LightOff:
                var evt = args.Event.ToString();
                evt = evt.Substring(evt.IndexOf('_') + 1);
                OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Security Remote", ModuleEvents.Sensor_Key, evt);
                break;
            }
        }

        private void W800Rf32_RfCommandReceived(object sender, RfCommandReceivedEventArgs args)
        {
            string address = args.HouseCode.ToString() + args.UnitCode.ToString().Split('_')[1];
            if (args.UnitCode == X10UnitCode.Unit_NotSet)
                return;
            var module = modules.Find(m => m.Address == address);
            if (module == null)
            {
                module = new InterfaceModule();
                module.Domain = X10_DOMAIN;
                module.Address = address;
                module.Description = "W800RF32 module";
                module.ModuleType = ModuleTypes.Switch;
                module.CustomData = 0.0D;
                modules.Add(module);
                OnInterfacePropertyChanged(this.GetDomain(), "1", "W800RF32 Receiver", ModuleEvents.Receiver_Status, "Added module " + address);
                OnInterfaceModulesChanged(X10_DOMAIN);
            }
            switch (args.Command)
            {
            case X10RfFunction.On:
                module.CustomData = 1.0D;
                break;
            case X10RfFunction.Off:
                module.CustomData = 0.0D;
                break;
            case X10RfFunction.Bright:
                double lbri = module.CustomData;
                lbri += 0.1;
                if (lbri > 1)
                    lbri = 1;
                module.CustomData = lbri;
                break;
            case X10RfFunction.Dim:
                double ldim = module.CustomData;
                ldim -= 0.1;
                if (ldim < 0)
                    ldim = 0;
                module.CustomData = ldim;
                break;
            case X10RfFunction.AllLightsOn:
                break;
            case X10RfFunction.AllLightsOff:
                break;
            }
            OnInterfacePropertyChanged(module.Domain, module.Address, "X10 Module", ModuleEvents.Status_Level, module.CustomData);
        }

        private void W800Rf32_RfDataReceived(object sender, RfDataReceivedEventArgs args)
        {
            var code = BitConverter.ToString(args.Data).Replace("-", " ");
            OnInterfacePropertyChanged(this.GetDomain(), "RF", "W800RF32 RF Receiver", ModuleEvents.Receiver_RawData, code);
            if (rfPulseTimer == null)
            {
                rfPulseTimer = new Timer(delegate(object target)
                {
                    OnInterfacePropertyChanged(this.GetDomain(), "RF", "W800RF32 RF Receiver", ModuleEvents.Receiver_RawData, "");
                });
            }
            rfPulseTimer.Change(1000, Timeout.Infinite);
        }

        #endregion

        #region Events

        protected virtual void OnInterfaceModulesChanged(string domain)
        {
            if (InterfaceModulesChanged != null)
            {
                var args = new InterfaceModulesChangedEventArgs(domain);
                InterfaceModulesChanged(this, args);
            }
        }

        protected virtual void OnInterfacePropertyChanged(string domain, string source, string description, string propertyPath, object propertyValue)
        {
            if (InterfacePropertyChanged != null)
            {
                var args = new InterfacePropertyChangedEventArgs(domain, source, description, propertyPath, propertyValue);
                InterfacePropertyChanged(this, args);
            }
        }

        #endregion

        #endregion

    }
}

