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
// MIG Insteon Interface handler it's
// based on SoapBox.FluentDwelling Insteon library.
// Documentation: http://soapboxautomation.com/support-2/fluentdwelling-support/

using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;

using SoapBox.FluentDwelling;
using SoapBox.FluentDwelling.Devices;

using MIG.Config;
using MIG.Interfaces.HomeAutomation.Commons;

namespace MIG.Interfaces.HomeAutomation
{
    public class Insteon: MigInterface
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
            Control_Toggle,
            Control_AllLightsOn,
            Control_AllLightsOff
        }

        #endregion

        #region Private fields

        private Plm insteonPlm;
        private Thread readerTask;

        #endregion

        public Insteon()
        {
        }

        #region MIG Interface members

        public event InterfaceModulesChangedEventHandler InterfaceModulesChanged;
        public event InterfacePropertyChangedEventHandler InterfacePropertyChanged;

        public bool IsEnabled { get; set; }

        public List<ConfigurationOption> Options { get; set; }

        public void OnSetOption(ConfigurationOption option)
        {
            // TODO: check if this is working
            if (IsEnabled)
                Connect();
        }

        public List<InterfaceModule> GetModules()
        {
            // TODO: make 'modules' data persistent in order to store status for various X10 operations (eg. like Control.Level)
            List<InterfaceModule> modules = new List<InterfaceModule>();
            if (insteonPlm != null)
            {
                //
                // X10 modules
                //
                var x10HouseCodes = this.GetOption("HouseCodes");
                if (x10HouseCodes != null && !String.IsNullOrEmpty(x10HouseCodes.Value))
                {
                    string[] hc = x10HouseCodes.Value.Split(',');
                    for (int i = 0; i < hc.Length; i++)
                    {
                        for (int x = 1; x <= 16; x++)
                        {
                            modules.Add(new InterfaceModule() {
                                Domain = this.GetDomain(),
                                Address = (hc[i] + x.ToString()),
                                ModuleType = ModuleTypes.Generic,
                                Description = "X10 Module"
                            });
                        }
                    }
                }
                //
                // Insteon devices discovery
                //
                var database = insteonPlm.GetAllLinkDatabase();
                foreach (var record in database.Records)
                {
                    // Connect to each device to figure out what it is
                    DeviceBase device;
                    if (insteonPlm.Network.TryConnectToDevice(record.DeviceId, out device))
                    {
                        // It responded. Get identification info
                        string address = device.DeviceId.ToString();
                        string category = device.DeviceCategoryCode.ToString();
                        string subcategory = device.DeviceSubcategoryCode.ToString();

                        ModuleTypes type = ModuleTypes.Generic;
                        switch (device.GetType().Name)
                        {
                        case "LightingControl":
                            type = ModuleTypes.Light;
                            break;
                        case "DimmableLightingControl":
                            type = ModuleTypes.Dimmer;
                            break;
                        case "SwitchedLightingControl":
                            type = ModuleTypes.Light;
                            break;
                        case "SensorsActuators":
                            type = ModuleTypes.Switch;
                            break;
                        case "WindowCoveringControl":
                            type = ModuleTypes.DoorWindow;
                            break;
                        case "PoolAndSpaControl":
                            type = ModuleTypes.Thermostat;
                            break;
                        case "IrrigationControl":
                            type = ModuleTypes.Switch;
                            break;
                        }

                        modules.Add(new InterfaceModule() {
                            Domain = this.GetDomain(),
                            Address = address,
                            ModuleType = type,
                            CustomData = category + "/" + subcategory
                        });
                    }
                    else
                    {
                        // couldn't connect - device removed?
                    }
                }
            }
            return modules;
        }

        public bool Connect()
        {
            Disconnect();
            insteonPlm = new Plm(this.GetOption("Port").Value);
            insteonPlm.OnError += insteonPlm_HandleOnError;
            /* 

            //TODO: implement incoming events handling as well:

            insteonPlm.Network.StandardMessageReceived
                += new StandardMessageReceivedHandler((s, e) =>
            {
                Console.WriteLine("Message received: " + e.Description
                    + ", from " + e.PeerId.ToString());
            });

            insteonPlm.Network.X10.CommandReceived
            += new X10CommandReceivedHandler((s, e) =>
            {
                Console.WriteLine("X10 Command Received: House Code " + e.HouseCode
                    + ", Command: " + e.Command.ToString());
            });

            // TODO: also see:

            insteonPlm.SetButton.PressedAndHeld
            insteonPlm.SetButton.ReleasedAfterHolding
            insteonPlm.SetButton.UserReset (SET Button Held During Power-up)
            insteonPlm.Network.SendStandardCommandToAddress ...

            */
            if (insteonPlm.Error)
            {
                Disconnect();
                return false;
            }
            //
            readerTask = new Thread(() =>
            {
                while (insteonPlm != null)
                {
                    insteonPlm.Receive();
                    System.Threading.Thread.Sleep(100); // wait 100 ms
                }
            });
            readerTask.Start();
            //
            OnInterfaceModulesChanged(this.GetDomain());
            return true;
        }

        public void Disconnect()
        {
            if (insteonPlm != null)
            {
                insteonPlm.OnError -= insteonPlm_HandleOnError;
                try
                {
                    insteonPlm.Dispose();
                }
                catch
                {
                }
                insteonPlm = null;
            }
            if (readerTask != null)
            {
                readerTask.Join(5000);
                try
                {
                    readerTask.Abort();
                }
                catch
                {
                }
                readerTask = null;
            }
        }

        public bool IsConnected
        {
            get { return (insteonPlm != null && !insteonPlm.Error); }
        }

        public bool IsDevicePresent()
        {
            bool present = true;
            return present;
        }

        public object InterfaceControl(MigInterfaceCommand request)
        {
            bool raisePropertyChanged = false;
            string parameterPath = ModuleEvents.Status_Level;
            string raiseParameter = "";
            //
            string nodeId = request.Address;

            Commands command;
            Enum.TryParse<Commands>(request.Command.Replace(".", "_"), out command);
            string option = request.GetOption(0);

            bool isDottedHexId = (nodeId.IndexOf(".") > 0);

            if (isDottedHexId)
            {
                // Standard Insteon device

                DeviceBase device = null;
                insteonPlm.Network.TryConnectToDevice(nodeId, out device);

                //TODO: handle types IrrigationControl and PoolAndSpaControl

                switch (command)
                {
                case Commands.Parameter_Status:
                    break;
                case Commands.Control_On:
                    raisePropertyChanged = true;
                    raiseParameter = "1";
                    //
                    if (device != null)
                        switch (device.GetType().Name)
                    {
                    case "LightingControl":
                        (device as LightingControl).TurnOn();
                        break;
                    case "DimmableLightingControl":
                        (device as DimmableLightingControl).TurnOn();
                        break;
                    case "SwitchedLightingControl":
                        (device as SwitchedLightingControl).TurnOn();
                        break;
                    case "SensorsActuators":
                        (device as SensorsActuators).TurnOnOutput(byte.Parse(option));
                        break;
                    case "WindowCoveringControl":
                        (device as WindowCoveringControl).Open();
                        break;
                    case "PoolAndSpaControl":
                        break;
                    case "IrrigationControl":
                        (device as IrrigationControl).TurnOnSprinklerValve(byte.Parse(option));
                        break;
                    }
                    break;
                case Commands.Control_Off:
                    raisePropertyChanged = true;
                    raiseParameter = "0";
                    //
                    if (device != null)
                        switch (device.GetType().Name)
                    {
                    case "LightingControl":
                        (device as LightingControl).TurnOff();
                        break;
                    case "DimmableLightingControl":
                        (device as DimmableLightingControl).TurnOff();
                        break;
                    case "SwitchedLightingControl":
                        (device as SwitchedLightingControl).TurnOff();
                        break;
                    case "SensorsActuators":
                        (device as SensorsActuators).TurnOffOutput(byte.Parse(option));
                        break;
                    case "WindowCoveringControl":
                        (device as WindowCoveringControl).Close();
                        break;
                    case "PoolAndSpaControl":
                        break;
                    case "IrrigationControl":
                        (device as IrrigationControl).TurnOffSprinklerValve(byte.Parse(option));
                        break;
                    }
                    break;
                case Commands.Control_Bright:
                    // TODO: raise parameter change event
                    if (device != null && device is DimmableLightingControl)
                    {
                        (device as DimmableLightingControl).BrightenOneStep();
                    }
                    break;
                case Commands.Control_Dim:
                    // TODO: raise parameter change event
                    if (device != null && device is DimmableLightingControl)
                    {
                        (device as DimmableLightingControl).DimOneStep();
                    }
                    break;
                case Commands.Control_Level:
                    double adjustedLevel = (double.Parse(option) / 100D);
                    raisePropertyChanged = true;
                    raiseParameter = adjustedLevel.ToString(CultureInfo.InvariantCulture);
                    //
                    byte level = (byte)((double.Parse(option) / 100D) * 255);
                    if (device != null)
                        switch (device.GetType().Name)
                    {
                    case "DimmableLightingControl":
                        (device as DimmableLightingControl).TurnOn(level);
                        break;
                    case "WindowCoveringControl":
                        (device as WindowCoveringControl).MoveToPosition(level);
                        break;
                    }
                    break;
                case Commands.Control_Toggle:
                    break;
                case Commands.Control_AllLightsOn:
                    break;
                case Commands.Control_AllLightsOff:
                    break;
                }
            }
            else
            {
                // It is not a dotted hex addres, so fallback to X10 device control
                var x10plm = insteonPlm.Network.X10;

                // Parse house/unit
                string houseCode = nodeId.Substring(0, 1);
                byte unitCode = 0x00;
                if (nodeId.Length > 1) unitCode = byte.Parse(nodeId.Substring(1));


                switch (command)
                {
                case Commands.Parameter_Status:
                    x10plm
                        .House(houseCode.ToString())
                        .Unit(unitCode)
                        .Command(X10Command.StatusRequest);
                    break;
                case Commands.Control_On:
                    raisePropertyChanged = true;
                    raiseParameter = "1";
                    //
                    x10plm
                        .House(houseCode)
                        .Unit(unitCode)
                        .Command(X10Command.On);
                    break;
                case Commands.Control_Off:
                    raisePropertyChanged = true;
                    raiseParameter = "0";
                    //
                    x10plm
                        .House(houseCode)
                        .Unit(unitCode)
                        .Command(X10Command.Off);
                    break;
                case Commands.Control_Bright:
                    // TODO: raise parameter change event
                    //int amount = int.Parse(option);
                    // TODO: how to specify bright amount parameter???
                    x10plm
                        .House(houseCode)
                        .Unit(unitCode)
                        .Command(X10Command.Bright);
                    break;
                case Commands.Control_Dim:
                    // TODO: raise parameter change event
                    //int amount = int.Parse(option);
                    // TODO: how to specify dim amount parameter???
                    x10plm
                        .House(houseCode)
                        .Unit(unitCode)
                        .Command(X10Command.Dim);
                    break;
                case Commands.Control_Level:
                    double adjustedLevel = (double.Parse(option) / 100D);
                    raisePropertyChanged = true;
                    raiseParameter = adjustedLevel.ToString(CultureInfo.InvariantCulture);
                    //
                    /*int dimvalue = int.Parse(option) - (int)(x10lib.ModulesStatus[ nodeId ].Level * 100.0);
                    if (dimvalue > 0)
                    {
                        x10lib.Bright(houseCode, unitCode, dimvalue);
                    }
                    else if (dimvalue < 0)
                    {
                        x10lib.Dim(houseCode, unitCode, -dimvalue);
                    }*/
                    break;
                case Commands.Control_Toggle:
                    /*
                    string huc = XTenLib.Utility.HouseUnitCodeFromEnum(houseCode, unitCode);
                    if (x10lib.ModulesStatus[ huc ].Level == 0)
                    {
                        x10lib.LightOn(houseCode, unitCode);
                    }
                    else
                    {
                        x10lib.LightOff(houseCode, unitCode);
                    }
                    */
                    break;
                case Commands.Control_AllLightsOn:
                    // TODO: ...
                    x10plm
                        .House(houseCode)
                        .Command(X10Command.AllLightsOn);
                    break;
                case Commands.Control_AllLightsOff:
                    // TODO: ...
                    x10plm
                        .House(houseCode)
                        .Command(X10Command.AllLightsOff);
                    break;
                }
            }

            if (raisePropertyChanged)
            {
                OnInterfacePropertyChanged(this.GetDomain(), nodeId, "Insteon Device", parameterPath, raiseParameter);
            }
            //
            return "";
        }

        #endregion

        #region Private members

        #region Insteon Interface events

        private void insteonPlm_HandleOnError(object sender, EventArgs e)
        {
            MigService.Log.Error(insteonPlm.Exception);
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
    
