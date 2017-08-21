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

