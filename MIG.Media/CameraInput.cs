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

using System.Runtime.InteropServices;
using MIG.Config;

namespace MIG.Interfaces.Media
{

    // TODO: Add source code and binaries for V4L camera lib wrapper
    // TODO: Add README.md file with instructions

    public class CameraInput : MigInterface
    {

        #region MigInterface API commands

        public enum Commands
        {
            Camera_GetPicture,
            Camera_GetLuminance,
            Camera_SetDevice
        }

        #endregion

        #region Support Strctures and Classes

        /// <summary>
        /// Picture buffer.
        /// </summary>
        public struct PictureBuffer
        {
            public int Size;
            public IntPtr Data;
        }

        /// <summary>
        /// Camera capture v4L interop.
        /// </summary>
        public class CameraCaptureV4LInterop
        {
            #region Managed to Unmanaged Interop

            [DllImport("CameraCaptureV4L.so", EntryPoint = "TakePicture")]
            public static extern PictureBuffer TakePicture(string device, uint width, uint height, uint jpegQuantity);
            [DllImport("CameraCaptureV4L.so", EntryPoint = "GetFrame")]
            public static extern PictureBuffer GetFrame(IntPtr source);
            [DllImport("CameraCaptureV4L.so", EntryPoint = "OpenCameraStream")]
            public static extern IntPtr OpenCameraStream(string device, uint width, uint height, uint fps);
            [DllImport("CameraCaptureV4L.so", EntryPoint = "CloseCameraStream")]
            public static extern void CloseCameraStream(IntPtr source);

            #endregion
        }

        /// <summary>
        /// Camera configuration.
        /// </summary>
        public class CameraConfiguration
        {
            public string Device = "/dev/video0";
            public uint Width = 320;
            public uint Height = 240;
            public uint Fps = 2;
        }

        #endregion

        #region Private fields

        private IntPtr cameraSource = IntPtr.Zero;
        private CameraConfiguration configuration = new CameraConfiguration();
        private object readPictureLock = new object();

        #endregion

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
            List<InterfaceModule> modules = new List<InterfaceModule>();

            InterfaceModule module = new InterfaceModule();
            module.Domain = this.GetDomain();
            module.Address = "AV0";
            module.Description = "Video 4 Linux Video Input";
            module.ModuleType = MIG.ModuleTypes.Sensor;
            modules.Add(module);

            return modules;
        }

        /// <summary>
        /// Connect to the automation interface/controller device.
        /// </summary>
        public bool Connect()
        {
            if (cameraSource != IntPtr.Zero)
            {
                Disconnect();
            }
            if (this.GetOption("Configuration") != null && !string.IsNullOrEmpty(this.GetOption("Configuration").Value))
            {
                var config = this.GetOption("Configuration").Value.Split(',');
                SetConfiguration(config[0], uint.Parse(config[1]), uint.Parse(config[2]), uint.Parse(config[3]));
            }
            cameraSource = CameraCaptureV4LInterop.OpenCameraStream(configuration.Device, configuration.Width, configuration.Height, configuration.Fps);
            OnInterfaceModulesChanged(this.GetDomain());
            // TODO: Possibly move this event out of here... it's a HomeGenie specific event
            OnInterfacePropertyChanged(this.GetDomain(), "AV0", "Camera Input", "Widget.DisplayModule", "homegenie/generic/camerainput");
            return (cameraSource != IntPtr.Zero);
        }
        /// <summary>
        /// Disconnect the automation interface/controller device.
        /// </summary>
        public void Disconnect()
        {
            if (cameraSource != IntPtr.Zero)
            {
                CameraCaptureV4LInterop.CloseCameraStream(cameraSource);
                cameraSource = IntPtr.Zero;
            }
        }
        /// <summary>
        /// Gets a value indicating whether the interface/controller device is connected or not.
        /// </summary>
        /// <value>
        /// <c>true</c> if it is connected; otherwise, <c>false</c>.
        /// </value>
        public bool IsConnected
        {
            get { return (cameraSource != IntPtr.Zero); }
        }
        /// <summary>
        /// Returns true if the device has been found in the system
        /// </summary>
        /// <returns></returns>
        public bool IsDevicePresent()
        {
            // eg. check against libusb for device presence by vendorId and productId
            return true;
        }

        public object InterfaceControl(MigInterfaceCommand request)
        {
            string response = ""; //default success value

            Commands command;
            Enum.TryParse<Commands>(request.Command.Replace(".", "_"), out command);

            switch (command)
            {
            case Commands.Camera_GetPicture:
                // get picture from camera <nodeid>
                // TODO: there is actually only single camera support 
                if (cameraSource != IntPtr.Zero)
                {
                    lock (readPictureLock)
                    {
                        var pictureBuffer = CameraCaptureV4LInterop.GetFrame(cameraSource);
                        var data = new byte[pictureBuffer.Size];
                        Marshal.Copy(pictureBuffer.Data, data, 0, pictureBuffer.Size);
                        return data;
                    }
                }
                break;
            case Commands.Camera_GetLuminance:
                // TODO: ....
                break;
            case Commands.Camera_SetDevice:
                this.GetOption("Configuration").Value = request.GetOption(0) + "," + request.GetOption(1) + "," + request.GetOption(2) + "," + request.GetOption(3);
                Connect();
                break;
            }

            return response;
        }

        #endregion

        #region public members

        public CameraConfiguration GetConfiguration()
        {
            return configuration;
        }

        public void SetConfiguration(string device, uint width, uint height, uint fps)
        {
            configuration.Device = device;
            configuration.Width = width;
            configuration.Height = height;
            configuration.Fps = fps;
        }

        #endregion

        #region Private members

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

    }
}
