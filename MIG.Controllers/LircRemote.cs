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
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

using MIG.Config;

namespace MIG.Interfaces.Controllers
{

    // TODO: This interface driver requires installation of LIRC client library
    // TODO: Add README.md file with instructions

    [Serializable]
    public class LircRemoteData
    {
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        //[NonSerialized]
        public byte[] Configuration { get; set; }
    }

    public class LircRemote : MigInterface
    {

        #region MigInterface API commands and events

        public enum Commands
        {
            Remotes_Search,
            Remotes_Add,
            Remotes_Remove,
            Remotes_List,
            Control_IrSend
        }

        #endregion

        #region Managed to Unmanaged Interop

        [DllImport("lirc_client")]
        private extern static int lirc_init(string prog, int verbose);

        [DllImport("lirc_client")]
        private extern static int LircDeinit();

        [DllImport("lirc_client")]
        private extern static int lirc_nextcode(out string code);

        [DllImport("lirc_client")]
        private extern static int lirc_readconfig(IntPtr file, out IntPtr config, IntPtr check);

        [DllImport("lirc_client")]
        private extern static int LircFreeConfig(IntPtr config);

        [DllImport("lirc_client")]
        private extern static int lirc_code2char(IntPtr config, string code, out string str);

        #endregion

        #region Private fields

        private string programName = "homegenie";
        private bool isConnected;
        private Thread lircListener;
        private IntPtr lircConfig;
        private List<LircRemoteData> remotesData = null;
        private List<LircRemoteData> remotesConfig = null;
        private Timer rfPulseTimer;

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
            InterfaceModule module = new InterfaceModule();
            module.Domain = this.GetDomain();
            module.Address = "IR";
            module.ModuleType = ModuleTypes.Sensor;
            modules.Add(module);
            return modules;
        }

        /// <summary>
        /// Gets a value indicating whether the interface/controller device is connected or not.
        /// </summary>
        /// <value>
        /// <c>true</c> if it is connected; otherwise, <c>false</c>.
        /// </value>
        public bool IsConnected
        {
            get { return isConnected; }
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

        public bool Connect()
        {
            if (!isConnected)
            {
                try
                {
                    if (lirc_init(programName, 1) == -1)
                    {
                        return false;
                    }
                    if (lirc_readconfig(IntPtr.Zero, out lircConfig, IntPtr.Zero) != 0)
                    {
                        return false;
                    }
                    //
                    isConnected = true;
                    //
                    lircListener = new Thread(new ThreadStart(() =>
                    {
                        while (isConnected)
                        {
                            string code = null;
                            try
                            {
                                lirc_nextcode(out code);
                            }
                            catch
                            {
                            } // TODO: handle exception
                            //
                            if (code == null)
                            {
                                // TODO: reconnect??
                                isConnected = false;
                                break;
                            }
                            //
                            if (code != "" && InterfacePropertyChanged != null)
                            {
                                string[] codeparts = code.Split(' ');
                                try
                                {
                                    if (codeparts[ 1 ] == "00") // we signal only the first pulse
                                    {
                                        OnInterfacePropertyChanged(this.GetDomain(), "IR", "LIRC Remote", "Receiver.RawData", codeparts[ 3 ].TrimEnd(new char[] { '\n', '\r' }) + "/" + codeparts[ 2 ]);
                                        //
                                        if (rfPulseTimer == null)
                                        {
                                            rfPulseTimer = new Timer(delegate(object target)
                                            {
                                                try
                                                {
                                                    //_rfprevstringdata = "";
                                                    OnInterfacePropertyChanged(this.GetDomain(), "IR", "LIRC Remote", "Receiver.RawData", "");
                                                }
                                                catch
                                                {
                                                    // TODO: add error logging 
                                                }
                                            });
                                        }
                                        rfPulseTimer.Change(1000, Timeout.Infinite);
                                    }
                                }
                                catch
                                {
                                } // TODO: handle exception
                            }
                            Thread.Sleep(100);
                        }
                    }));
                    lircListener.Start();
                }
                catch
                {
                    return false;
                }
            }
            OnInterfaceModulesChanged(this.GetDomain());
            return true;
        }

        public void Disconnect()
        {
            if (isConnected)
            {
                try
                {
                    lircListener.Abort();
                }
                catch
                {
                }
                lircListener = null;
                //
                try
                {
                    LircFreeConfig(lircConfig);
                    LircDeinit();
                }
                catch
                {

                }
                //
                isConnected = false;
            }
        }

        public object InterfaceControl(MigInterfaceCommand request)
        {
            string response = ""; //default success value

            Commands command;
            Enum.TryParse<Commands>(request.Command.Replace(".", "_"), out command);

            switch (command)
            {
            case Commands.Remotes_Search:
                response = MigService.JsonSerialize(SearchRemotes(request.GetOption(0)));
                break;
            case Commands.Remotes_Add:
                {
                    var remote = remotesData.Find(r => r.Manufacturer.ToLower() == request.GetOption(0).ToLower() && r.Model.ToLower() == request.GetOption(1).ToLower());
                    if (remote != null && remotesConfig.Find(r => r.Model.ToLower() == remote.Model.ToLower() && r.Manufacturer.ToLower() == remote.Manufacturer.ToLower()) == null)
                    {
                        var webClient = new WebClient();
                        string config = webClient.DownloadString("http://lirc.sourceforge.net/remotes/" + remote.Manufacturer + "/" + remote.Model);
                        remote.Configuration = GetBytes(config);
                        remotesConfig.Add(remote);
                        SaveConfig();
                    }
                }
                break;
            case Commands.Remotes_Remove:
                {
                    var remote = remotesConfig.Find(r => r.Manufacturer.ToLower() == request.GetOption(0).ToLower() && r.Model.ToLower() == request.GetOption(1).ToLower());
                    if (remote != null)
                    {
                        remotesConfig.Remove(remote);
                        SaveConfig();
                    }
                }
                break;
            case Commands.Remotes_List:
                response = MigService.JsonSerialize(remotesConfig);
                break;
            case Commands.Control_IrSend:
                string commands = "";
                int c = 0;
                while (request.GetOption(c) != "")
                {
                    var options = request.GetOption(c).Split('/');
                    foreach (string o in options)
                    {
                        commands += "\"" + o + "\" ";
                    }
                    c++;
                }
                MigService.ShellCommand("irsend", "SEND_ONCE " + commands);
                break;
            }

            return response;
        }

        #endregion

        #region Lifecycle

        public LircRemote()
        {
            // lirc client lib symlink
            string assemblyFolder = MigService.GetAssemblyDirectory(this.GetType().Assembly);
            var liblirclink = Path.Combine(assemblyFolder, "liblirc_client.so");
            if (File.Exists("/usr/lib/liblirc_client.so") && !File.Exists(liblirclink))
            {
                MigService.ShellCommand("ln", " -s \"/usr/lib/liblirc_client.so\" \"" + liblirclink + "\"");
            }
            else if (File.Exists("/usr/lib/liblirc_client.so.0") && !File.Exists(liblirclink))
            {
                MigService.ShellCommand("ln", " -s \"/usr/lib/liblirc_client.so.0\" \"" + liblirclink + "\"");
            }
            // create .lircrc file
            var lircrcFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".lircrc");
            if (!File.Exists(lircrcFile))
            {
                var lircrc = "begin\n" +
                    "        prog = homegenie\n" +
                    "        button = KEY_1\n" +
                    "        repeat = 3\n" +
                    "        config = KEY_1\n" +
                    "end\n";
                try
                {
                    File.WriteAllText(lircrcFile, lircrc);
                }
                catch { }
            }
            //
            remotesConfig = new List<LircRemoteData>();
            string configfile = Path.Combine(assemblyFolder, "lircconfig.xml");
            if (File.Exists(configfile))
            {
                var serializer = new XmlSerializer(typeof(List<LircRemoteData>));
                var reader = new StreamReader(configfile);
                remotesConfig = (List<LircRemoteData>)serializer.Deserialize(reader);
                reader.Close();
            }
            //
            string remotesdb = Path.Combine(assemblyFolder, "lircremotes.xml");
            if (File.Exists(remotesdb))
            {
                var serializer = new XmlSerializer(typeof(List<LircRemoteData>));
                var reader = new StreamReader(remotesdb);
                remotesData = (List<LircRemoteData>)serializer.Deserialize(reader);
                reader.Close();
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        #endregion

        #region Public Members

        public List<LircRemoteData> SearchRemotes(string searchString)
        {
            var filtered = new List<LircRemoteData>();
            searchString = searchString.ToLower();
            foreach (var remote in remotesData)
            {
                if (remote.Manufacturer.ToLower().StartsWith(searchString) || remote.Model.ToLower().StartsWith(searchString))
                {
                    filtered.Add(remote);
                }
            }
            return filtered;
        }

        #endregion

        #region Private members

        #region Utility methods

        private void SaveConfig()
        {
            string fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lircconfig.xml");
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            var settings = new System.Xml.XmlWriterSettings();
            settings.Indent = true;
            var serializer = new System.Xml.Serialization.XmlSerializer(remotesConfig.GetType());
            var writer = System.Xml.XmlWriter.Create(fileName, settings);
            serializer.Serialize(writer, remotesConfig);
            writer.Close();
            //
            try
            {
                string lircConfiguration = "";
                foreach (var remote in remotesConfig)
                {
                    lircConfiguration += GetString(remote.Configuration) + "\n";
                }
                File.WriteAllText("/etc/lirc/lircd.conf", lircConfiguration);
                MigService.ShellCommand("/etc/init.d/lirc", " force-reload");
            }
            catch
            {
            }
        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
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

