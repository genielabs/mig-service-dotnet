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
using System.Text;
using System.Threading;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using System.Net;

using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;

using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

using ZWaveLib;
using ZWaveLib.CommandClasses;

using MIG.Interfaces.HomeAutomation.Commons;
using MIG.Config;
using System.Xml.XPath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MIG.Interfaces.HomeAutomation
{

    public class ZWave : MigInterface
    {

        #region MigInterface API commands and events

        public enum Commands
        {
            NotSet,

            Controller_Discovery,
            Controller_NodeAdd,
            Controller_NodeRemove,
            Controller_SoftReset,
            Controller_HardReset,
            Controller_HealNetwork,
            Controller_NodeNeighborUpdate,
            Controller_NodeRoutingInfo,

            Basic_Get,
            Basic_Set,

            SwitchBinary_Get,
            SwitchBinary_Set,

            SwitchMultilevel_Get,
            SwitchMultilevel_Set,

            MultiInstance_Get,
            MultiInstance_Set,
            MultiInstance_GetCount,

            Battery_Get,

            Association_Get,
            Association_Set,
            Association_Remove,

            ManufacturerSpecific_Get,
            NodeInfo_Get,

            Config_ParameterGet,
            Config_ParameterSet,

            WakeUp_Get,
            WakeUp_Set,
            WakeUp_SendToSleep,
            WakeUp_GetAlwaysAwake,
            WakeUp_SetAlwaysAwake,

            SensorBinary_Get,
            SensorMultiLevel_Get,

            Meter_Get,
            Meter_SupportedGet,
            Meter_Reset,

            Control_On,
            Control_Off,
            Control_Level,
            Control_Toggle,

            Thermostat_ModeGet,
            Thermostat_ModeSet,
            Thermostat_SetPointGet,
            Thermostat_SetPointSet,
            Thermostat_FanModeGet,
            Thermostat_FanModeSet,
            Thermostat_FanStateGet,
            Thermostat_OperatingStateGet,

            UserCode_Set,

            Version_Report,
            Version_Get,
            Version_GetAll,

            DoorLock_Set,
            DoorLock_Get,

            Db_Update,
            Db_GetDevice
        }

        // Z-Wave specific events
        const string EventPath_Basic
           = "ZWaveNode.Basic";
        const string EventPath_SwitchBinary
            = "ZWaveNode.SwitchBinary";
        const string EventPath_SwitchMultilevel
            = "ZWaveNode.SwitchMultilevel";
        const string EventPath_WakeUpInterval
            = "ZWaveNode.WakeUpInterval";
        const string EventPath_Battery
            = "ZWaveNode.Battery";
        const string EventPath_MultiInstance
            = "ZWaveNode.MultiInstance";
        const string EventPath_Associations
            = "ZWaveNode.Associations";
        const string EventPath_ConfigVariables
            = "ZWaveNode.Variables";
        const string EventPath_NodeInfo
            = "ZWaveNode.NodeInfo";
        const string EventPath_RoutingInfo
            = "ZWaveNode.RoutingInfo";
        const string EventPath_ManufacturerSpecific
            = "ZWaveNode.ManufacturerSpecific";
        const string EventPath_VersionReport
            = "ZWaveNode.VersionReport";

        #endregion

        #region Private fields

        private ZWaveController controller;

        private byte lastRemovedNode = 0;
        private byte lastAddedNode = 0;

        #endregion

        #region MIG Interface members

        public event InterfaceModulesChangedEventHandler InterfaceModulesChanged;
        public event InterfacePropertyChangedEventHandler InterfacePropertyChanged;

        public bool IsEnabled { get; set; }

        public List<Option> Options { get; set; }

        public void OnSetOption(Option option)
        {
            if (IsEnabled)
                Connect();
        }

        public List<InterfaceModule> GetModules()
        {
            List<InterfaceModule> modules = new List<InterfaceModule>();
            if (controller != null)
            {
                for (int d = 0; d < controller.Nodes.Count; d++)
                {
                    var node = controller.Nodes[d];
                    // add new module
                    InterfaceModule module = new InterfaceModule();
                    module.Domain = this.GetDomain();
                    module.Address = node.Id.ToString();
                    //module.Description = "ZWave Node";
                    module.ModuleType = ModuleTypes.Generic;
                    if (node.ProtocolInfo.GenericType != (byte)GenericType.None)
                    {
                        switch (node.ProtocolInfo.GenericType)
                        {
                        case (byte)GenericType.StaticController:
                            module.Description = "Static Controller";
                            module.ModuleType = ModuleTypes.Generic;
                            break;

                        case (byte)GenericType.SwitchBinary:
                            module.Description = "Binary Switch";
                            module.ModuleType = ModuleTypes.Switch;
                            break;

                        case (byte)GenericType.SwitchMultilevel:
                            module.Description = "Multilevel Switch";
                            module.ModuleType = ModuleTypes.Dimmer;
                            break;

                        case (byte)GenericType.Thermostat:
                            module.Description = "Thermostat";
                            module.ModuleType = ModuleTypes.Thermostat;
                            break;
                            
                        case (byte)GenericType.SensorAlarm:
                            module.Description = "Alarm Sensor";
                            module.ModuleType = ModuleTypes.Sensor;
                            break;

                        case (byte)GenericType.SensorBinary:
                            module.Description = "Binary Sensor";
                            module.ModuleType = ModuleTypes.Sensor;
                            break;

                        case (byte)GenericType.SensorMultilevel:
                            module.Description = "Multilevel Sensor";
                            module.ModuleType = ModuleTypes.Sensor;
                            break;

                        case (byte)GenericType.Meter:
                            module.Description = "ZWave Meter";
                            module.ModuleType = ModuleTypes.Sensor;
                            break;

                        case (byte)GenericType.EntryControl:
                            module.Description = "ZWave Door Lock";
                            module.ModuleType = ModuleTypes.DoorLock;
                            break;

                        }
                    }
                    modules.Add(module);
                }
            }
            return modules;
        }

        public bool IsConnected
        {
            get
            {
                return (controller.Status == ControllerStatus.Ready);
            }
        }

        public object InterfaceControl(MigInterfaceCommand request)
        {
            ResponseText returnValue = new ResponseText("OK");
            bool raiseEvent = false;
            string eventParameter = ModuleEvents.Status_Level;
            string eventValue = "";

            string nodeId = request.Address;
            Commands command;
            Enum.TryParse<Commands>(request.Command.Replace(".", "_"), out command);
            ZWaveNode node = null;

            byte nodeNumber = 0;
            if (byte.TryParse(nodeId, out nodeNumber))
            {
                if (nodeNumber > 0)
                    node = controller.GetNode(nodeNumber);
                switch (command)
                {

                case Commands.Controller_Discovery:
                    controller.Discovery();
                    break;

                case Commands.Controller_SoftReset:
                    controller.SoftReset();
                    break;

                case Commands.Controller_HardReset:
                    controller.HardReset();
                    controller.Discovery();
                    break;

                case Commands.Controller_HealNetwork:
                    controller.HealNetwork();
                    break;

                case Commands.Controller_NodeNeighborUpdate:
                    controller.RequestNeighborsUpdateOptions(nodeNumber);
                    controller.RequestNeighborsUpdate(nodeNumber);
                    controller.GetNeighborsRoutingInfo(nodeNumber);
                    returnValue = GetResponseValue(nodeNumber, EventPath_RoutingInfo);
                    break;

                case Commands.Controller_NodeRoutingInfo:
                    controller.GetNeighborsRoutingInfo(nodeNumber);
                    returnValue = GetResponseValue(nodeNumber, EventPath_RoutingInfo);
                    break;

                case Commands.Controller_NodeAdd:
                    lastAddedNode = 0;
                    controller.BeginNodeAdd();
                    for (int i = 0; i < 20; i++)
                    {
                        if (lastAddedNode > 0)
                        {
                            break;
                        }
                        Thread.Sleep(500);
                    }
                    controller.StopNodeAdd();
                    returnValue = new ResponseText(lastAddedNode.ToString());
                    break;

                case Commands.Controller_NodeRemove:
                    lastRemovedNode = 0;
                    controller.BeginNodeRemove();
                    for (int i = 0; i < 20; i++)
                    {
                        if (lastRemovedNode > 0)
                        {
                            break;
                        }
                        Thread.Sleep(500);
                    }
                    controller.StopNodeRemove();
                    returnValue = new ResponseText(lastRemovedNode.ToString());
                    break;

                case Commands.Basic_Set:
                    {
                        raiseEvent = true;
                        var level = int.Parse(request.GetOption(0));
                        eventValue = level.ToString(CultureInfo.InvariantCulture);
                        Basic.Set(node, (byte)level);
                    }
                    break;

                case Commands.Basic_Get:
                    Basic.Get(node);
                    returnValue = GetResponseValue(nodeNumber, EventPath_Basic);
                    break;

                case Commands.SwitchBinary_Set:
                    {
                        raiseEvent = true;
                        var level = int.Parse(request.GetOption(0));
                        eventValue = level.ToString(CultureInfo.InvariantCulture);
                        SwitchBinary.Set(node, (byte)level);
                    }
                    break;

                case Commands.SwitchBinary_Get:
                    SwitchBinary.Get(node);
                    returnValue = GetResponseValue(nodeNumber, EventPath_SwitchBinary);
                    break;

                case Commands.SwitchMultilevel_Set:
                    {
                        raiseEvent = true;
                        var level = int.Parse(request.GetOption(0));
                        eventValue = level.ToString(CultureInfo.InvariantCulture);
                        SwitchMultilevel.Set(node, (byte)level);
                    }
                    break;

                case Commands.SwitchMultilevel_Get:
                    SwitchMultilevel.Get(node);
                    returnValue = GetResponseValue(nodeNumber, EventPath_SwitchMultilevel);
                    break;

                case Commands.MultiInstance_GetCount:
                    {
                        string commandType = request.GetOption(0).Replace(".", "");
                        switch (commandType)
                        {
                        case "SwitchBinary":
                            MultiInstance.GetCount(node, (byte)ZWaveLib.CommandClass.SwitchBinary);
                            break;
                        case "SwitchMultiLevel":
                            MultiInstance.GetCount(node, (byte)ZWaveLib.CommandClass.SwitchMultilevel);
                            break;
                        case "SensorBinary":
                            MultiInstance.GetCount(node, (byte)ZWaveLib.CommandClass.SensorBinary);
                            break;
                        case "SensorMultiLevel":
                            MultiInstance.GetCount(node, (byte)ZWaveLib.CommandClass.SensorMultilevel);
                            break;
                        }
                        returnValue = GetResponseValue(nodeNumber, EventPath_MultiInstance + "." + commandType + ".Count");
                    }
                    break;

                case Commands.MultiInstance_Get:
                    {
                        byte instance = (byte)int.Parse(request.GetOption(1));
                        string commandType = request.GetOption(0).Replace(".", "");
                        switch (commandType)
                        {
                        case "SwitchBinary":
                            MultiInstance.SwitchBinaryGet(node, instance);
                            break;
                        case "SwitchMultiLevel":
                            MultiInstance.SwitchMultiLevelGet(node, instance);
                            break;
                        case "SensorBinary":
                            MultiInstance.SensorBinaryGet(node, instance);
                            break;
                        case "SensorMultiLevel":
                            MultiInstance.SensorMultiLevelGet(node, instance);
                            break;
                        }
                        returnValue = GetResponseValue(nodeNumber, EventPath_MultiInstance + "." + commandType + "." + instance);
                    }
                    break;

                case Commands.MultiInstance_Set:
                    {
                        byte instance = (byte)int.Parse(request.GetOption(1));
                        int value = int.Parse(request.GetOption(2));
                        //
                        //raisepropchanged = true;
                        //parampath += "." + instance; // Status.Level.<instance>
                        //
                        switch (request.GetOption(0))
                        {
                        case "Switch.Binary":
                            MultiInstance.SwitchBinarySet(node, instance, value);
                        //raiseparam = (double.Parse(request.GetOption(2)) / 255).ToString();
                            break;
                        case "Switch.MultiLevel":
                            MultiInstance.SwitchMultiLevelSet(node, instance, value);
                        //raiseparam = (double.Parse(request.GetOption(2)) / 100).ToString(); // TODO: should it be 99 ?
                            break;
                        }
                    }
                    break;

                case Commands.SensorBinary_Get:
                    SensorBinary.Get(node);
                    break;

                case Commands.SensorMultiLevel_Get:
                    SensorMultilevel.Get(node);
                    break;

                case Commands.Meter_Get:
                // see ZWaveLib Sensor.cs for EnergyMeterScale options
                    int scaleType = 0;
                    int.TryParse(request.GetOption(0), out scaleType);
                    Meter.Get(node, (byte)(scaleType << 0x03));
                    break;

                case Commands.Meter_SupportedGet:
                    Meter.GetSupported(node);
                    break;

                case Commands.Meter_Reset:
                    Meter.Reset(node);
                    break;

                case Commands.NodeInfo_Get:
                    controller.GetNodeInformationFrame(nodeNumber);
                    returnValue = GetResponseValue(nodeNumber, EventPath_NodeInfo);
                    break;

                case Commands.Version_Report:
                    ZWaveLib.CommandClasses.Version.Report(node);
                    returnValue = GetResponseValue(nodeNumber, EventPath_VersionReport);
                    break;

                case Commands.Battery_Get:
                    Battery.Get(node);
                    returnValue = GetResponseValue(nodeNumber, EventPath_Battery);
                    break;

                case Commands.Association_Set:
                    Association.Set(node, (byte)int.Parse(request.GetOption(0)), (byte)int.Parse(request.GetOption(1)));
                    break;

                case Commands.Association_Get:
                    byte group = (byte)int.Parse(request.GetOption(0));
                    Association.Get(node, group);
                    returnValue = GetResponseValue(nodeNumber, EventPath_Associations + "." + group);
                    break;

                case Commands.Association_Remove:
                    Association.Remove(node, (byte)int.Parse(request.GetOption(0)), (byte)int.Parse(request.GetOption(1)));
                    break;

                case Commands.ManufacturerSpecific_Get:
                    ManufacturerSpecific.Get(node);
                    returnValue = GetResponseValue(nodeNumber, EventPath_ManufacturerSpecific);
                    break;

                case Commands.Config_ParameterSet:
                    Configuration.Set(node, (byte)int.Parse(request.GetOption(0)), int.Parse(request.GetOption(1)));
                    break;

                case Commands.Config_ParameterGet:
                    byte position = (byte)int.Parse(request.GetOption(0));
                    Configuration.Get(node, position);
                    returnValue = GetResponseValue(nodeNumber, EventPath_ConfigVariables + "." + position);
                    break;

                case Commands.WakeUp_Get:
                    WakeUp.Get(node);
                    returnValue = GetResponseValue(nodeNumber, EventPath_WakeUpInterval);
                    break;

                case Commands.WakeUp_Set:
                    WakeUp.Set(node, uint.Parse(request.GetOption(0)));
                    break;

                case Commands.WakeUp_SendToSleep:
                    WakeUp.SendToSleep(node);
                    break;

                case Commands.WakeUp_GetAlwaysAwake:
                    returnValue = new ResponseText(WakeUp.GetAlwaysAwake(node) ? "1" : "0");
                    break;

                case Commands.WakeUp_SetAlwaysAwake:
                    WakeUp.SetAlwaysAwake(node, uint.Parse(request.GetOption(0)) == 1 ? true : false);
                    break;

                case Commands.Version_Get:
                    returnValue = new ResponseText("ERROR");
                    CommandClass cclass;
                    Enum.TryParse<CommandClass>(request.GetOption(0), out cclass);
                    if (cclass != CommandClass.NotSet)
                    {
                        var nodeCclass = node.GetCommandClass(cclass);
                        if (nodeCclass != null && nodeCclass.Version != 0)
                        {
                            returnValue = new ResponseText(nodeCclass.Version.ToString());
                        }
                        else
                        {
                            ZWaveLib.CommandClasses.Version.Get(node, cclass); 
                            returnValue = GetResponseValue(nodeNumber, "ZWaveNode.Version." + cclass);
                        }
                    }
                    break;

                case Commands.Version_GetAll:
                    controller.GetNodeCcsVersion(node);
                    break;

                case Commands.Control_On:
                    raiseEvent = true;
                    double lastLevel = GetNormalizedValue((double)GetNodeLastLevel(node));
                    eventValue = lastLevel > 0 ? lastLevel.ToString(CultureInfo.InvariantCulture) : "1";
                    if (node.SupportCommandClass(CommandClass.SwitchMultilevel))
                        SwitchMultilevel.Set(node, 0xFF);
                    else if (node.SupportCommandClass(CommandClass.SwitchBinary))
                        SwitchBinary.Set(node, 0xFF);
                    else
                        Basic.Set(node, 0xFF);
                    SetNodeLevel(node, 0xFF);
                    break;

                case Commands.Control_Off:
                    raiseEvent = true;
                    eventValue = "0";
                    if (node.SupportCommandClass(CommandClass.SwitchMultilevel))
                        SwitchMultilevel.Set(node, 0x00);
                    else if (node.SupportCommandClass(CommandClass.SwitchBinary))
                        SwitchBinary.Set(node, 0x00);
                    else
                        Basic.Set(node, 0x00);
                    SetNodeLevel(node, 0x00);
                    break;

                case Commands.Control_Level:
                    {
                        raiseEvent = true;
                        var level = int.Parse(request.GetOption(0));
                        eventValue = Math.Round(level / 100D, 2).ToString(CultureInfo.InvariantCulture);
                        // the max value should be obtained from node parameters specifications,
                        // here we assume that the commonly used interval is [0-99] for most multilevel switches
                        if (level >= 100)
                            level = 99;
                        if (node.SupportCommandClass(CommandClass.SwitchMultilevel))
                            SwitchMultilevel.Set(node, (byte)level);
                        else
                            Basic.Set(node, (byte)level);
                        SetNodeLevel(node, (byte)level);
                    }
                    break;

                case Commands.Control_Toggle:
                    raiseEvent = true;
                    if (GetNodeLevel(node) == 0)
                    {
                        double lastOnLevel = GetNormalizedValue((double)GetNodeLastLevel(node));
                        eventValue = lastOnLevel > 0 ? lastOnLevel.ToString(CultureInfo.InvariantCulture) : "1";
                        if (node.SupportCommandClass(CommandClass.SwitchMultilevel))
                            SwitchMultilevel.Set(node, 0xFF);
                        else if (node.SupportCommandClass(CommandClass.SwitchBinary))
                            SwitchBinary.Set(node, 0xFF);
                        else
                            Basic.Set(node, 0xFF);
                        SetNodeLevel(node, 0xFF);
                    }
                    else
                    {
                        eventValue = "0";
                        if (node.SupportCommandClass(CommandClass.SwitchMultilevel))
                            SwitchMultilevel.Set(node, 0x00);
                        else if (node.SupportCommandClass(CommandClass.SwitchBinary))
                            SwitchBinary.Set(node, 0x00);
                        else
                            Basic.Set(node, 0x00);
                        SetNodeLevel(node, 0x00);
                    }
                    break;

                case Commands.Thermostat_ModeGet:
                    ThermostatMode.Get(node);
                    break;

                case Commands.Thermostat_ModeSet:
                    {
                        ThermostatMode.Value mode = (ThermostatMode.Value)Enum.Parse(typeof(ThermostatMode.Value), request.GetOption(0));
                        //
                        raiseEvent = true;
                        eventParameter = "Thermostat.Mode";
                        eventValue = request.GetOption(0);
                        //
                        ThermostatMode.Set(node, mode);
                    }
                    break;

                case Commands.Thermostat_SetPointGet:
                    {
                        ThermostatSetPoint.Value mode = (ThermostatSetPoint.Value)Enum.Parse(typeof(ThermostatSetPoint.Value), request.GetOption(0));
                        ThermostatSetPoint.Get(node, mode);
                    }
                    break;

                case Commands.Thermostat_SetPointSet:
                    {
                        ThermostatSetPoint.Value mode = (ThermostatSetPoint.Value)Enum.Parse(typeof(ThermostatSetPoint.Value), request.GetOption(0));
                        double temperature = double.Parse(request.GetOption(1).Replace(',', '.'), CultureInfo.InvariantCulture);
                        //
                        raiseEvent = true;
                        eventParameter = "Thermostat.SetPoint." + request.GetOption(0);
                        eventValue = temperature.ToString(CultureInfo.InvariantCulture);
                        //
                        ThermostatSetPoint.Set(node, mode, temperature);
                    }
                    break;

                case Commands.Thermostat_FanModeGet:
                    ThermostatFanMode.Get(node);
                    break;

                case Commands.Thermostat_FanModeSet:
                    {
                        ThermostatFanMode.Value mode = (ThermostatFanMode.Value)Enum.Parse(typeof(ThermostatFanMode.Value), request.GetOption(0));
                        //
                        raiseEvent = true;
                        eventParameter = "Thermostat.FanMode";
                        eventValue = request.GetOption(0);
                        //
                        ThermostatFanMode.Set(node, mode);
                    }
                    break;

                case Commands.Thermostat_FanStateGet:
                    ThermostatFanState.Get(node);
                    break;

                case Commands.Thermostat_OperatingStateGet:
                    ThermostatOperatingState.GetOperatingState(node);
                    break;

                case Commands.UserCode_Set:
                    byte userId = byte.Parse(request.GetOption(0));
                    byte userIdStatus = byte.Parse(request.GetOption(1));
                    byte[] tagCode = ZWaveLib.Utility.HexStringToByteArray(request.GetOption(2));
                    UserCode.Set(node, new ZWaveLib.Values.UserCodeValue(userId, userIdStatus, tagCode));
                    break;

                case Commands.DoorLock_Get:
                    DoorLock.Get(node);
                    returnValue = GetResponseValue(nodeNumber, ModuleEvents.Status_DoorLock);
                    break;

                case Commands.DoorLock_Set:
                    {
                        DoorLock.Value mode = (DoorLock.Value)Enum.Parse(typeof(DoorLock.Value), request.GetOption(0));
                        DoorLock.Set(node, mode);
                    }
                    break;

                case Commands.Db_Update:
                    {
                        var p1db = new Pepper1Db();
                        p1db.Update(request.GetOption(0));
                        break;
                    }
                case Commands.Db_GetDevice:
                    {
                        var p1db = new Pepper1Db();
                        returnValue = new ResponseText(p1db.GetDeviceInfo(request.GetOption(0), request.GetOption(1)));
                        break;
                    }
                }
            }

            if (raiseEvent)
            {
                //ZWaveNode node = _controller.GetDevice ((byte)int.Parse (nodeid));
                OnInterfacePropertyChanged(this.GetDomain(), nodeId, "ZWave Node", eventParameter, eventValue);
            }
            //
            return returnValue;
        }

        public bool Connect()
        {
            int commandDelay = 100;
            if (this.GetOption("Delay") != null)
                int.TryParse(this.GetOption("Delay").Value, out commandDelay);
            controller.CommandDelay = commandDelay;
            controller.PortName = this.GetOption("Port").Value;
            controller.Connect();
            return true;
        }

        public void Disconnect()
        {
            controller.Disconnect();
        }

        public bool IsDevicePresent()
        {
            /*
            List<USBDeviceInfo> devices = new List<USBDeviceInfo>();

            ManagementObjectCollection collection;
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
            collection = searcher.Get();      

            foreach (var device in collection)
            {
            devices.Add(new USBDeviceInfo(
                (string)device.GetPropertyValue("DeviceID"),
                (string)device.GetPropertyValue("PNPDeviceID"),
                (string)device.GetPropertyValue("Description")
            ));
            }

            collection.Dispose();
            */
            //bool present = false;
            ////
            //Console.WriteLine(LibUsbDevice.LegacyLibUsbDeviceList.Count);
            //foreach (UsbRegistry usbdev in LibUsbDevice.AllDevices)
            //{
            //    if ((usbdev.Vid == 0x10C4 && usbdev.Pid == 0xEA60) || usbdev.FullName.ToUpper().Contains("CP2102"))
            //    {
            //        present = true;
            //        break;
            //    }
            //}
            //return present;
            return true;
        }

        #endregion

        #region Public members

        public ZWave()
        {
            controller = new ZWaveController();
            controller.ControllerStatusChanged += Controller_ControllerStatusChanged;
            controller.DiscoveryProgress += Controller_DiscoveryProgress;
            controller.HealProgress += Controller_HealProgress;
            controller.NodeOperationProgress += Controller_NodeOperationProgress;
            controller.NodeUpdated += Controller_NodeUpdated;
            Initialize();
        }


        #endregion

        #region Private members

        /*
        class USBDeviceInfo
        {
            public USBDeviceInfo(string deviceID, string pnpDeviceID, string description)
            {
                this.DeviceID = deviceID;
                this.PnpDeviceID = pnpDeviceID;
                this.Description = description;
            }
            public string DeviceID { get; private set; }
            public string PnpDeviceID { get; private set; }
            public string Description { get; private set; }
        }
        */

        private void Initialize()
        {
            // Upon start we should check existence of pepper1 database and create it if needed.
            var p1Db = new Pepper1Db();
            if (!p1Db.DbExists)
            {
                ThreadPool.QueueUserWorkItem((o) => p1Db.Update());
            }
        }

        private void Controller_ControllerStatusChanged(object sender, ControllerStatusEventArgs args)
        {
            var controller = (sender as ZWaveController);
            switch (args.Status)
            {
            case ControllerStatus.Connected:
                // Initialize the controller and get the node list
                controller.Initialize();
                break;
            case ControllerStatus.Disconnected:
                break;
            case ControllerStatus.Initializing:
                break;
            case ControllerStatus.Ready:
                // Query all nodes (Basic Classes, Node Information Frame, Manufacturer Specific[, Command Class version])
                // Enabled by default
                if (this.GetOption("StartupDiscovery") == null || this.GetOption("StartupDiscovery").Value != "0")
                    controller.Discovery();
                break;
            case ControllerStatus.Error:
                controller.Connect();
                break;
            }
        }

        private void Controller_NodeOperationProgress(object sender, NodeOperationProgressEventArgs args)
        {
            // this will fire on a node operation such as Add, Remove, Updating Routing, etc..
            switch (args.Status)
            {
            case NodeQueryStatus.NodeAdded:
                lastAddedNode = args.NodeId;
                OnInterfacePropertyChanged(this.GetDomain(), "1", "Z-Wave Controller", "Controller.Status", "Added node " + args.NodeId);
                OnInterfaceModulesChanged(this.GetDomain());
                break;
            case NodeQueryStatus.NodeUpdated:
                OnInterfacePropertyChanged(this.GetDomain(), "1", "Z-Wave Controller", "Controller.Status", "Updated node " + args.NodeId);
                //OnInterfaceModulesChanged(this.Domain);
                break;
            case NodeQueryStatus.NodeRemoved:
                lastRemovedNode = args.NodeId;
                OnInterfacePropertyChanged(this.GetDomain(), "1", "Z-Wave Controller", "Controller.Status", "Removed node " + args.NodeId);
                OnInterfaceModulesChanged(this.GetDomain());
                break;
            case NodeQueryStatus.Timeout:
                OnInterfacePropertyChanged(this.GetDomain(), "1", "Z-Wave Controller", "Controller.Status", "Node " + args.NodeId + " response timeout!");
                break;
            case NodeQueryStatus.Error:
                OnInterfacePropertyChanged(this.GetDomain(), args.NodeId.ToString(), "Z-Wave Node", "Status.Error", "Response timeout!");
                break;
            default:
                OnInterfacePropertyChanged(this.GetDomain(), "1", "Z-Wave Controller", "Controller.Status", String.Format("Node {0} Status {1}", args.NodeId, args.Status.ToString()));
                break;
            }
        }

        private void Controller_DiscoveryProgress(object sender, DiscoveryProgressEventArgs args)
        {
            //var controller = (sender as ZWaveController);
            switch (args.Status)
            {
            case DiscoveryStatus.DiscoveryStart:
                OnInterfacePropertyChanged(this.GetDomain(), "1", "Z-Wave Controller", "Controller.Status", "Discovery Started");
                break;
            case DiscoveryStatus.DiscoveryEnd:
                OnInterfacePropertyChanged(this.GetDomain(), "1", "Z-Wave Controller", "Controller.Status", "Discovery Complete");
                OnInterfaceModulesChanged(this.GetDomain());
                break;
            }
        }

        private void Controller_HealProgress(object sender, HealProgressEventArgs args)
        {
            switch (args.Status)
            {
            case HealStatus.HealStart:
                OnInterfacePropertyChanged(this.GetDomain(), "1", "Z-Wave Controller", "Controller.Status", "Network Heal Started");
                break;
            case HealStatus.HealEnd:
                OnInterfacePropertyChanged(this.GetDomain(), "1", "Z-Wave Controller", "Controller.Status", "Network Heal Complete");
                break;
            }
        }

        private void Controller_NodeUpdated(object sender, NodeUpdatedEventArgs args)
        {
            var eventData = args.Event;
            while (eventData != null)
            {
                string eventPath = "UnknwonParameter";
                object eventValue = eventData.Value;
                switch (eventData.Parameter)
                {
                case EventParameter.MeterKwHour:
                    eventPath = GetIndexedParameterPath(ModuleEvents.Meter_KwHour, eventData.Instance);
                    break;
                case EventParameter.MeterKvaHour:
                    eventPath = GetIndexedParameterPath(ModuleEvents.Meter_KvaHour, eventData.Instance);
                    break;
                case EventParameter.MeterWatt:
                    eventPath = GetIndexedParameterPath(ModuleEvents.Meter_Watts, eventData.Instance);
                    break;
                case EventParameter.MeterPulses:
                    eventPath = GetIndexedParameterPath(ModuleEvents.Meter_Pulses, eventData.Instance);
                    break;
                case EventParameter.MeterAcVolt:
                    eventPath = GetIndexedParameterPath(ModuleEvents.Meter_AcVoltage, eventData.Instance);
                    break;
                case EventParameter.MeterAcCurrent:
                    eventPath = GetIndexedParameterPath(ModuleEvents.Meter_AcCurrent, eventData.Instance);
                    break;
                case EventParameter.MeterPower:
                    eventPath = GetIndexedParameterPath(ModuleEvents.Sensor_Power, eventData.Instance);
                    break;
                case EventParameter.Battery:
                    OnInterfacePropertyChanged(this.GetDomain(), eventData.Node.Id.ToString(), "ZWave Node", EventPath_Battery, eventValue);
                    eventPath = ModuleEvents.Status_Battery;
                    break;
                case EventParameter.NodeInfo:
                    eventPath = EventPath_NodeInfo;
                    break;
                case EventParameter.RoutingInfo:
                    eventPath = EventPath_RoutingInfo;
                    break;
                case EventParameter.SensorGeneric:
                    eventPath = ModuleEvents.Sensor_Generic;
                    break;
                case EventParameter.SensorTemperature:
                    eventPath = ModuleEvents.Sensor_Temperature;
                    break;
                case EventParameter.SensorHumidity:
                    eventPath = ModuleEvents.Sensor_Humidity;
                    break;
                case EventParameter.SensorLuminance:
                    eventPath = ModuleEvents.Sensor_Luminance;
                    break;
                case EventParameter.SensorMotion:
                    eventPath = ModuleEvents.Sensor_MotionDetect;
                    break;
                case EventParameter.AlarmGeneric:
                    eventPath = ModuleEvents.Sensor_Alarm;
                    // Translate generic alarm into specific Door Lock event values if node is an entry control type device
                    //at this level the sender is the controller so get the node from eventData
                    if (eventData.Node.ProtocolInfo.GenericType == (byte)GenericType.EntryControl)
                    {
                        eventPath = ModuleEvents.Status_DoorLock;
                        //! do not convert to string since Alarms accept ONLY numbers a string would be outputed as NaN
                        //! for now let it as is.
                        //eventValue = ((DoorLock.Alarm)(byte)value).ToString();
                    }
                    break;
                case EventParameter.AlarmDoorWindow:
                    eventPath = ModuleEvents.Sensor_DoorWindow;
                    break;
                case EventParameter.AlarmTampered:
                    eventPath = ModuleEvents.Sensor_Tamper;
                    break;
                case EventParameter.AlarmSmoke:
                    eventPath = ModuleEvents.Sensor_Smoke;
                    break;
                case EventParameter.AlarmCarbonMonoxide:
                    eventPath = ModuleEvents.Sensor_CarbonMonoxide;
                    break;
                case EventParameter.AlarmCarbonDioxide:
                    eventPath = ModuleEvents.Sensor_CarbonDioxide;
                    break;
                case EventParameter.AlarmHeat:
                    eventPath = ModuleEvents.Sensor_Heat;
                    break;
                case EventParameter.AlarmFlood:
                    eventPath = ModuleEvents.Sensor_Flood;
                    break;
                case EventParameter.DoorLockStatus:
                    eventPath = ModuleEvents.Status_DoorLock;
                    eventValue = ((DoorLock.Value)(byte)eventValue).ToString();
                    break;
                case EventParameter.ManufacturerSpecific:
                    ManufacturerSpecificInfo mf = (ManufacturerSpecificInfo)eventValue;
                    eventPath = EventPath_ManufacturerSpecific;
                    eventValue = mf.ManufacturerId + ":" + mf.TypeId + ":" + mf.ProductId;
                    break;
                case EventParameter.Configuration:
                    eventPath = EventPath_ConfigVariables + "." + eventData.Instance;
                    break;
                case EventParameter.Association:
                    var associationResponse = (Association.AssociationResponse)eventValue;
                    OnInterfacePropertyChanged(this.GetDomain(), eventData.Node.Id.ToString(), "ZWave Node", EventPath_Associations + ".Max", associationResponse.Max);
                    OnInterfacePropertyChanged(this.GetDomain(), eventData.Node.Id.ToString(), "ZWave Node", EventPath_Associations + ".Count", associationResponse.Count);
                    eventPath = EventPath_Associations + "." + associationResponse.GroupId; // TODO: implement generic group/node association instead of fixed one
                    eventValue = associationResponse.NodeList;
                    break;
                case EventParameter.MultiinstanceSwitchBinaryCount:
                    eventPath = EventPath_MultiInstance + ".SwitchBinary.Count";
                    break;
                case EventParameter.MultiinstanceSwitchMultilevelCount:
                    eventPath = EventPath_MultiInstance + ".SwitchMultiLevel.Count";
                    break;
                case EventParameter.MultiinstanceSensorBinaryCount:
                    eventPath = EventPath_MultiInstance + ".SensorBinary.Count";
                    break;
                case EventParameter.MultiinstanceSensorMultilevelCount:
                    eventPath = EventPath_MultiInstance + ".SensorMultiLevel.Count";
                    break;
                case EventParameter.MultiinstanceSwitchBinary:
                    eventPath = EventPath_MultiInstance + ".SwitchBinary." + eventData.Instance;
                    break;
                case EventParameter.MultiinstanceSwitchMultilevel:
                    eventPath = EventPath_MultiInstance + ".SwitchMultiLevel." + eventData.Instance;
                    break;
                case EventParameter.MultiinstanceSensorBinary:
                    eventPath = EventPath_MultiInstance + ".SensorBinary." + eventData.Instance;
                    break;
                case EventParameter.MultiinstanceSensorMultilevel:
                    eventPath = EventPath_MultiInstance + ".SensorMultiLevel." + eventData.Instance;
                    break;
                case EventParameter.WakeUpInterval:
                    eventPath = EventPath_WakeUpInterval;
                    break;
                case EventParameter.WakeUpSleepingStatus:
                    eventPath = "ZWaveNode.WakeUpSleepingStatus";
                    break;
                case EventParameter.WakeUpNotify:
                    eventPath = "ZWaveNode.WakeUpNotify";
                    break;
                case EventParameter.Basic:
                    eventPath = EventPath_Basic;
                    {
                        double normalizedLevel = GetNormalizedValue((double)eventValue);
                        OnInterfacePropertyChanged(this.GetDomain(), eventData.Node.Id.ToString(), "ZWave Node", ModuleEvents.Status_Level + (eventData.Instance == 0 ? "" : "." + eventData.Instance), normalizedLevel.ToString(CultureInfo.InvariantCulture));
                    }
                    SetNodeLevel(eventData.Node, Convert.ToByte((double)eventValue));
                    break;
                case EventParameter.SwitchBinary:
                    eventPath = EventPath_SwitchBinary;
                    {
                        double normalizedLevel = GetNormalizedValue((double)eventValue);
                        OnInterfacePropertyChanged(this.GetDomain(), eventData.Node.Id.ToString(), "ZWave Node", ModuleEvents.Status_Level + (eventData.Instance == 0 ? "" : "." + eventData.Instance), normalizedLevel.ToString(CultureInfo.InvariantCulture));
                    }
                    SetNodeLevel(eventData.Node, Convert.ToByte((double)eventValue));
                    break;
                case EventParameter.SwitchMultilevel:
                    eventPath = EventPath_SwitchMultilevel;
                    {
                        double normalizedLevel = GetNormalizedValue((double)eventValue);
                        OnInterfacePropertyChanged(this.GetDomain(), eventData.Node.Id.ToString(), "ZWave Node", ModuleEvents.Status_Level + (eventData.Instance == 0 ? "" : "." + eventData.Instance), normalizedLevel.ToString(CultureInfo.InvariantCulture));
                    }
                    SetNodeLevel(eventData.Node, Convert.ToByte((double)eventValue));
                    break;
                case EventParameter.ThermostatMode:
                    eventPath = "Thermostat.Mode";
                    eventValue = ((ThermostatMode.Value)eventValue).ToString();
                    break;
                case EventParameter.ThermostatOperatingState:
                    eventPath = "Thermostat.OperatingState";
                    eventValue = ((ThermostatOperatingState.Value)eventValue).ToString();
                    break;
                case EventParameter.ThermostatFanMode:
                    eventPath = "Thermostat.FanMode";
                    eventValue = ((ThermostatFanMode.Value)eventValue).ToString();
                    break;
                case EventParameter.ThermostatFanState:
                    eventPath = "Thermostat.FanState";
                    eventValue = ((ThermostatFanState.Value)eventValue).ToString();
                    break;
                case EventParameter.ThermostatHeating:
                    eventPath = "Thermostat.Heating";
                    break;
                case EventParameter.ThermostatSetBack:
                    eventPath = "Thermostat.SetBack";
                    break;
                case EventParameter.ThermostatSetPoint:
                    // value stores a dynamic object with Type and Value fields: value = { Type = ..., Value = ... }
                    eventPath = "Thermostat.SetPoint." + ((ThermostatSetPoint.Value)((dynamic)eventValue).Type).ToString();
                    eventValue = ((dynamic)eventValue).Value;
                    break;
                case EventParameter.UserCode:
                    eventPath = "EntryControl.UserCode";
                    eventValue = ((ZWaveLib.Values.UserCodeValue)eventValue).TagCodeToHexString();
                    break;
                case EventParameter.SecurityNodeInformationFrame:
                    eventPath = "ZWaveNode.SecuredNodeInfo";
                    break;
                case EventParameter.VersionCommandClass:
                    if (eventValue is NodeVersion)
                    {
                        eventPath = "ZWaveNode.VersionReport";
                        eventValue = (eventValue as NodeVersion).ToString();
                    }
                    else
                    {
                        eventPath = "ZWaveNode.Version." + (eventValue as ZWaveLib.Values.VersionValue).CmdClass;
                        eventValue = (eventValue as ZWaveLib.Values.VersionValue).Version;
                    }
                    break;
                default:
                    MigService.Log.Warn("Unhandled event from node {0} (Event={1}, Id={2}, Value={3})", eventData.Node.Id, eventData.Parameter, eventData.Instance, eventValue);
                    break;
                }

                OnInterfacePropertyChanged(this.GetDomain(), eventData.Node.Id.ToString(), "ZWave Node", eventPath, eventValue);

                eventData = eventData.NestedEvent;
            }
        }

        private ResponseText GetResponseValue(byte nodeNumber, string eventPath)
        {
            ResponseText returnValue = new ResponseText("ERR_TIMEOUT");
            InterfacePropertyChangedEventHandler eventHandler = new InterfacePropertyChangedEventHandler((sender, property) =>
            {
                if (property.EventData.Source == nodeNumber.ToString() && property.EventData.Property == eventPath)
                {
                    returnValue = new ResponseText(property.EventData.Value.ToString());
                }
            });
            InterfacePropertyChanged += eventHandler;
            Thread t = new Thread(() =>
            {
                int timeout = 0;
                int delay = 100;
                while (returnValue.ResponseValue == "ERR_TIMEOUT" && timeout < ZWaveMessage.SendMessageTimeoutMs / delay)
                {
                    Thread.Sleep(delay);
                    timeout++;
                }
            });
            t.Start();
            t.Join(ZWaveMessage.SendMessageTimeoutMs);
            InterfacePropertyChanged -= eventHandler;
            return returnValue;
        }

        private string GetIndexedParameterPath(string basePath, int parameterId)
        {
            if (parameterId > 0)
            {
                basePath += "." + parameterId;
            }
            return basePath;
        }

        private void SetNodeLevel(ZWaveNode node, int level)
        {
            node.UpdateData("Level", level);
            if (level > 0)
                node.UpdateData("LastLevel", level);
        }

        private int GetNodeLevel(ZWaveNode node)
        {
            return (int)node.GetData("Level", 0).Value;
        }

        private int GetNodeLastLevel(ZWaveNode node)
        {
            return (int)node.GetData("LastLevel", 0).Value;
        }

        private double GetNormalizedValue(double val)
        {
            double normalizedval = (Math.Round(val / 100D, 2));
            if (normalizedval >= 0.99)
                normalizedval = 1.0;
            return normalizedval;
        }

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
                new Thread(() =>
                {
                    InterfacePropertyChanged(this, args);
                }).Start();
            }
        }

        #endregion

    }


    public class Pepper1Db
    {
        private const string dbFilename = "p1db.xml";
        private const string additionalDbFilename = "additionalZwaveDevices.xml";
        private const string archiveFilename = "archive.zip";
        private const string tempFolder = "temp";
        private const string defaultPepper1Url = "http://pepper1.net/zwavedb/device/export/device_archive.zip";

        public bool DbExists
        {
            get
            {
                var dbFile = new FileInfo(dbFilename);
                return dbFile.Exists;
            }
        }

        public bool Update(string pepper1Url = defaultPepper1Url)
        {
            ZipConstants.DefaultCodePage = System.Text.Encoding.UTF8.CodePage;

            // request archive from P1 db
            using (var client = new WebClient ()) 
            {
                try 
                {
                    MigService.Log.Debug("Downloading archive from {0}.", pepper1Url);
                    client.DownloadFile (pepper1Url, archiveFilename);
                } 
                catch (Exception ex) 
                {
                    Console.WriteLine (ex.Message);
                    return false;
                }
            }

            // extract archive
            MigService.Log.Debug ("Extracting archive from '{0}' to '{1}' folder.", archiveFilename, tempFolder);
            ExtractZipFile(archiveFilename, null, tempFolder);

            MigService.Log.Debug ("Creating consolidated DB.");
            var p1db = new XDocument();
            var dbElement = new XElement ("Devices");

            // for each xml file read it content and add to one file
            var files = Directory.GetFiles(tempFolder, "*.xml");
            foreach (var file in files)
            {
                try
                {
                    var fi = new FileInfo (file);
                    var xDoc = XElement.Load (fi.OpenText ());
                    dbElement.Add (xDoc.RemoveAllNamespaces());
                }
                catch (Exception)
                {
                }
            }

            // Also we need to include some predefined entries from db, distributed with HG
            // for devices that couldn't be found in pepper1db.
            // TODO

            p1db.Add (dbElement);
            var dbFile = new FileInfo (dbFilename);
            using (var writer = dbFile.CreateText())
            {
                p1db.Save (writer);
            }
            MigService.Log.Debug ("DB saved: {0}.", dbFilename);
            return true;
        }

        /// <summary>
        /// Searches local pepper1 db for the specified device and returns an array of matched device infos in JSON.
        /// </summary>
        /// <returns>The device info.</returns>
        /// <param name="manufacturerId">Manufacturer identifier.</param>
        /// <param name="version">Version (in format appVersion.appSubVersion).</param>
        public string GetDeviceInfo(string manufacturerId, string version)
        {
            var res = GetDeviceInfoInDb(dbFilename, manufacturerId, version);
            // if no devices has been found in pepper1 db, we should try to find them in additional db
            if (res.Count == 0)
            {
                res = GetDeviceInfoInDb(additionalDbFilename, manufacturerId, version);
            }

            return JsonConvert.SerializeObject(res, Newtonsoft.Json.Formatting.Indented, new []{new XmlNodeConverter()});
        }

        private List<XElement> GetDeviceInfoInDb(string filename, string manufacturerId, string version)
        {
            var dbFile = new FileInfo (filename);
            XDocument db;
            using (var reader = dbFile.OpenText ())
            {
                db = XDocument.Load(reader);
            }

            var mIdParts = manufacturerId.Split(new []{':'}, StringSplitOptions.RemoveEmptyEntries);
            if(mIdParts.Length != 3)
                throw new ArgumentException(string.Format("Wrong manufacturerId ({0})", manufacturerId));

            var query = string.Format("deviceData/manufacturerId[@value=\"{0}\"] and deviceData/productType[@value=\"{1}\"] and deviceData/productId[@value=\"{2}\"]", mIdParts[0], mIdParts[1], mIdParts[2]);
            if (!string.IsNullOrEmpty(version))
            {
                var vParts = version.Split(new []{'.'}, StringSplitOptions.RemoveEmptyEntries);
                query += string.Format(" and deviceData/appVersion[@value=\"{0}\"] and deviceData/appSubVersion[@value=\"{1}\"]", vParts[0], vParts[1]);
            }
            var baseQuery = string.Format("//ZWaveDevice[ {0} ]", query);
            var res = db.XPathSelectElements (baseQuery).ToList();
            MigService.Log.Debug("Found {0} elements in {1} with query {2}", res.Count, filename, baseQuery);

            if (res.Count == 0)
            {
                // try to find generic device info without version information
                query = string.Format("deviceData/manufacturerId[@value=\"{0}\"] and deviceData/productType[@value=\"{1}\"] and deviceData/productId[@value=\"{2}\"]", mIdParts[0], mIdParts[1], mIdParts[2]);
                baseQuery = string.Format("//ZWaveDevice[ {0} ]", query);
                res = db.XPathSelectElements (baseQuery).ToList();
                MigService.Log.Debug("Found {0} elements in {1} with query {2}", res.Count, filename, baseQuery);
            }

            return res;
        }

        private static void ExtractZipFile(string archiveFilenameIn, string password, string outFolder)
        {
            ZipFile zf = null;
            try {
                FileStream fs = File.OpenRead(archiveFilenameIn);
                zf = new ZipFile(fs);
                if (!String.IsNullOrEmpty(password)) {
                    zf.Password = password;     // AES encrypted entries are handled automatically
                }
                foreach (ZipEntry zipEntry in zf) {
                    if (!zipEntry.IsFile) {
                        continue;           // Ignore directories
                    }
                    String entryFileName = zipEntry.Name;
                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    String fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath)) {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            } finally {
                if (zf != null) {
                    zf.IsStreamOwner = true; // Makes close also shut the underlying stream
                    zf.Close(); // Ensure we release resources
                }
            }
        }
    }

    public static class Extensions
    {
        public static XElement RemoveAllNamespaces(this XElement xmlDocument)
        {
            XElement xElement = new XElement(xmlDocument.Name.LocalName);
            foreach (XAttribute attribute in xmlDocument.Attributes().Where(x => !x.IsNamespaceDeclaration))
                xElement.Add(attribute);

            if (!xmlDocument.HasElements)
            {                
                xElement.Value = xmlDocument.Value;                
                return xElement;
            }

            xElement.Add(xmlDocument.Elements().Select(el => RemoveAllNamespaces(el)));
            return xElement;
        }
    }
}
