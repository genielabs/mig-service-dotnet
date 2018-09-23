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

using System;

namespace MIG.Interfaces.HomeAutomation.Commons
{
    /// <summary>
    /// Common Home Automation events.
    /// </summary>
    public static class ModuleEvents
    {

        public static string VirtualMeter_Watts =
            "VirtualMeter.Watts";
        public static string Status_Level =
            "Status.Level";
        public static string Status_DoorLock =
            "Status.DoorLock";
        public static string Status_Battery =
            "Status.Battery";
        public static string Meter_KwHour =
            "Meter.KilowattHour";
        public static string Meter_KvaHour =
            "Meter.KilovoltAmpereHour";
        public static string Meter_Watts =
            "Meter.Watts";
        public static string Meter_Pulses =
            "Meter.Pulses";
        public static string Meter_AcVoltage =
            "Meter.AcVoltage";
        public static string Meter_AcCurrent =
            "Meter.AcCurrent";
        public static string Sensor_Power =
            "Sensor.Power";
        public static string Sensor_Generic =
            "Sensor.Generic";
        public static string Sensor_MotionDetect =
            "Sensor.MotionDetect";
        public static string Sensor_Temperature =
            "Sensor.Temperature";
        public static string Sensor_Luminance =
            "Sensor.Luminance";
        public static string Sensor_Humidity =
            "Sensor.Humidity";
        public static string Sensor_WaterFlow =
            "Sensor.WaterFlow";
        public static string Sensor_WaterPressure =
            "Sensor.WaterPressure";
        public static string Sensor_Ultraviolet =
            "Sensor.Ultraviolet";
        public static string Sensor_DoorWindow =
            "Sensor.DoorWindow";
        public static string Sensor_Key =
            "Sensor.Key";
        public static string Sensor_Alarm =
            "Sensor.Alarm";
        public static string Sensor_CarbonMonoxide =
            "Sensor.CarbonMonoxide";
        public static string Sensor_CarbonDioxide =
            "Sensor.CarbonDioxide";
        public static string Sensor_Smoke =
            "Sensor.Smoke";
        public static string Sensor_Heat =
            "Sensor.Heat";
        public static string Sensor_Flood =
            "Sensor.Flood";
        public static string Sensor_Tamper =
            "Sensor.Tamper";
        public static string Receiver_RawData = 
            "Receiver.RawData";
        public static string Receiver_Status = 
            "Receiver.Status";

    }
}

