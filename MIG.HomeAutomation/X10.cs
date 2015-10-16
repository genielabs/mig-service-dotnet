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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

using MIG.Interfaces.HomeAutomation.Commons;
using MIG.Config;
using XTenLib;

namespace MIG.Interfaces.HomeAutomation
{
    public class X10 : MigInterface
    {

        #region MigInterface API commands

        public enum Commands
        {
            Parameter_Status,
            Control_On,
            Control_Off,
            Control_Bright,
            Control_Dim,
            Control_Level,
            Control_Level_Adjust,
            Control_Toggle,
            Control_AllLightsOn,
            Control_AllLightsOff
        }

        #endregion

        #region Private fields

        private XTenManager x10lib;
        private Timer rfPulseTimer;
        private List<InterfaceModule> securityModules;
        private List<Option> options;

        #endregion

        #region MIG Interface members

        public event InterfaceModulesChangedEventHandler InterfaceModulesChanged;
        public event InterfacePropertyChangedEventHandler InterfacePropertyChanged;

        public bool IsEnabled { get; set; }

        public List<Option> Options
        { 
            get
            {
                return options;
            }
            set
            {
                options = value;
                if (this.GetOption("Port") != null)
                    x10lib.PortName = this.GetOption("Port").Value.Replace("|", "/");
                if (this.GetOption("HouseCodes") != null)
                    x10lib.HouseCode = this.GetOption("HouseCodes").Value;
            }
        }

        public void OnSetOption(Option option)
        {
            if (IsEnabled)
                Connect();
        }

        public List<InterfaceModule> GetModules()
        {
            List<InterfaceModule> modules = new List<InterfaceModule>();
            if (x10lib != null)
            {

                InterfaceModule module = new InterfaceModule();

                // CM-15 RF receiver
                if (this.GetOption("Port").Value.Equals("USB"))
                {
                    module.Domain = this.GetDomain();
                    module.Address = "RF";
                    module.ModuleType = ModuleTypes.Sensor;
                    modules.Add(module);
                }

                // Standard X10 modules
                foreach (var kv in x10lib.Modules)
                {

                    module = new InterfaceModule();
                    module.Domain = this.GetDomain();
                    module.Address = kv.Value.Code;
                    module.ModuleType = ModuleTypes.Switch;
                    module.Description = "X10 Module";
                    modules.Add(module);

                }

                // CM-15 RF Security modules
                modules.AddRange(securityModules);

            }
            return modules;
        }

        public bool Connect()
        {
            x10lib.PortName = this.GetOption("Port").Value.Replace("|", "/");
            x10lib.HouseCode = this.GetOption("HouseCodes").Value;
            OnInterfaceModulesChanged(this.GetDomain());
            return x10lib.Connect();
        }

        public void Disconnect()
        {
            x10lib.Disconnect();
        }

        public bool IsConnected
        {
            get { return x10lib.IsConnected; }
        }

        public bool IsDevicePresent()
        {
            //bool present = false;
            ////
            ////TODO: implement serial port scanning for CM11 as well
            //foreach (UsbRegistry usbdev in LibUsbDevice.AllDevices)
            //{
            //    //Console.WriteLine(o.Vid + " " + o.SymbolicName + " " + o.Pid + " " + o.Rev + " " + o.FullName + " " + o.Name + " ");
            //    if ((usbdev.Vid == 0x0BC7 && usbdev.Pid == 0x0001) || usbdev.FullName.ToUpper().Contains("X10"))
            //    {
            //        present = true;
            //        break;
            //    }
            //}
            //return present;
            return true;
        }

        public object InterfaceControl(MigInterfaceCommand request)
        {
            string response = "[{ ResponseValue : 'OK' }]";

            string nodeId = request.Address;
            string option = request.GetOption(0);

            Commands command;
            Enum.TryParse<Commands>(request.Command.Replace(".", "_"), out command);

            // Parse house/unit
            var houseCode = XTenLib.Utility.HouseCodeFromString(nodeId);
            var unitCode = XTenLib.Utility.UnitCodeFromString(nodeId);

            switch (command)
            {
            case Commands.Parameter_Status:
                x10lib.StatusRequest(houseCode, unitCode);
                break;
            case Commands.Control_On:
                x10lib.UnitOn(houseCode, unitCode);
                break;
            case Commands.Control_Off:
                x10lib.UnitOff(houseCode, unitCode);
                break;
            case Commands.Control_Bright:
                x10lib.Bright(houseCode, unitCode, int.Parse(option));
                break;
            case Commands.Control_Dim:
                x10lib.Dim(houseCode, unitCode, int.Parse(option));
                break;
            case Commands.Control_Level_Adjust:
                int adjvalue = int.Parse(option);
                //x10lib.Modules[nodeId].Level = ((double)adjvalue/100D);
                OnInterfacePropertyChanged(this.GetDomain(), nodeId, "X10 Module", ModuleEvents.Status_Level, x10lib.Modules[nodeId].Level);
                throw(new NotImplementedException("X10 CONTROL_LEVEL_ADJUST Not Implemented"));
                break;
            case Commands.Control_Level:
                int dimvalue = int.Parse(option) - (int)(x10lib.Modules[nodeId].Level * 100.0);
                if (dimvalue > 0)
                {
                    x10lib.Bright(houseCode, unitCode, dimvalue);
                }
                else if (dimvalue < 0)
                {
                    x10lib.Dim(houseCode, unitCode, -dimvalue);
                }
                break;
            case Commands.Control_Toggle:
                string huc = XTenLib.Utility.HouseUnitCodeFromEnum(houseCode, unitCode);
                if (x10lib.Modules[huc].Level == 0)
                {
                    x10lib.UnitOn(houseCode, unitCode);
                }
                else
                {
                    x10lib.UnitOff(houseCode, unitCode);
                }
                break;
            case Commands.Control_AllLightsOn:
                x10lib.AllLightsOn(houseCode);
                break;
            case Commands.Control_AllLightsOff:
                x10lib.AllUnitsOff(houseCode);
                break;
            }

            return response;
        }

        #endregion

        #region Lifecycle

        public X10()
        {
            string assemblyFolder = MigService.GetAssemblyDirectory(this.GetType().Assembly);
            var libusblink = Path.Combine(assemblyFolder, "libusb-1.0.so");
            // RaspBerry Pi armel dependency check and needed symlink
            if ((File.Exists("/lib/arm-linux-gnueabi/libusb-1.0.so.0.1.0") || File.Exists("/lib/arm-linux-gnueabihf/libusb-1.0.so.0.1.0")) && !File.Exists(libusblink))
            {
                MigService.ShellCommand("ln", " -s \"/lib/arm-linux-gnueabi/libusb-1.0.so.0.1.0\" \"" + libusblink + "\"");
            }
            // Debian/Ubuntu 64bit dependency and needed symlink check
            if (File.Exists("/lib/x86_64-linux-gnu/libusb-1.0.so.0") && !File.Exists(libusblink))
            {
                MigService.ShellCommand("ln", " -s \"/lib/x86_64-linux-gnu/libusb-1.0.so.0\" \"" + libusblink + "\"");
            }

            x10lib = new XTenManager();
            x10lib.ModuleChanged += X10lib_ModuleChanged;
            x10lib.RfDataReceived += X10lib_RfDataReceived;
            x10lib.RfSecurityReceived += X10lib_RfSecurityReceived;
            securityModules = new List<InterfaceModule>();
        }

        #endregion

        #region Private fields

        #region XTenLib events

        private void X10lib_RfSecurityReceived(object sender, RfSecurityReceivedEventArgs args)
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
            var module = securityModules.Find(m => m.Address == address);
            if (module == null)
            {
                module = new InterfaceModule();
                module.Domain = this.GetDomain();
                module.Address = address;
                module.Description = "X10 Security";
                module.ModuleType = moduleType;
                module.CustomData = 0.0D;
                securityModules.Add(module);
                OnInterfacePropertyChanged(this.GetDomain(), "RF", "X10 RF Receiver", ModuleEvents.Receiver_Status, "Added security module " + address);
                OnInterfaceModulesChanged(this.GetDomain());
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

        private void X10lib_RfDataReceived(object sender, RfDataReceivedEventArgs args)
        {
            var code = BitConverter.ToString(args.Data).Replace("-", " ");
            OnInterfacePropertyChanged(this.GetDomain(), "RF", "X10 RF Receiver", ModuleEvents.Receiver_RawData, code);
            if (rfPulseTimer == null)
            {
                rfPulseTimer = new Timer(delegate(object target)
                {
                    OnInterfacePropertyChanged(this.GetDomain(), "RF", "X10 RF Receiver", ModuleEvents.Receiver_RawData, "");
                });
            }
            rfPulseTimer.Change(1000, Timeout.Infinite);
        }

        private void X10lib_ModuleChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Level")
                OnInterfacePropertyChanged(this.GetDomain(), (sender as X10Module).Code, (sender as X10Module).Description, ModuleEvents.Status_Level, (sender as X10Module).Level.ToString());
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
