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

using MIG.Config;
using MIG.Gateways;
using MIG.Utility;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using NLog;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace MIG
{
    
    #region Event delegates

    /// <summary>
    /// Pre process request event handler.
    /// </summary>
    public delegate void PreProcessRequestEventHandler(object sender, ProcessRequestEventArgs args);
    /// <summary>
    /// Post process request event handler.
    /// </summary>
    public delegate void PostProcessRequestEventHandler(object sender, ProcessRequestEventArgs args);
    /// <summary>
    /// Interface property changed event handler.
    /// </summary>
    public delegate void InterfacePropertyChangedEventHandler(object sender, InterfacePropertyChangedEventArgs args);
    /// <summary>
    /// Interface modules changed event handler.
    /// </summary>
    public delegate void InterfaceModulesChangedEventHandler(object sender, InterfaceModulesChangedEventArgs args);
    /// <summary>
    /// Option changed event handler.
    /// </summary>
    public delegate void OptionChangedEventHandler(object sender, OptionChangedEventArgs args);

    #endregion

    public class MigService
    {

        #region Private fields

        private MigServiceConfiguration configuration;
        private DynamicApi dynamicApi;

        #endregion

        #region Public events and fields

        public static Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Occurs on gateway request pre process.
        /// </summary>
        public event PreProcessRequestEventHandler GatewayRequestPreProcess;
        /// <summary>
        /// Occurs on gateway request post process.
        /// </summary>
        public event PostProcessRequestEventHandler GatewayRequestPostProcess;
        /// <summary>
        /// Occurs when interface property changed.
        /// </summary>
        public event InterfacePropertyChangedEventHandler InterfacePropertyChanged;
        /// <summary>
        /// Occurs when interface modules changed.
        /// </summary>
        public event InterfaceModulesChangedEventHandler InterfaceModulesChanged;

        //TODO: add more events....
        //public event -- ServiceStarted;
        //public event -- ServiceStopped;

        // TODO: use List instead of Dictionary...
        public readonly List<MigGateway> Gateways;
        public readonly List<MigInterface> Interfaces;

        #endregion

        #region Lifecycle

        public MigService()
        {
            Interfaces = new List<MigInterface>();
            Gateways = new List<MigGateway>();
            configuration = new MigServiceConfiguration();
            dynamicApi = new DynamicApi();
        }

        /// <summary>
        /// Starts the service.
        /// </summary>
        /// <returns><c>true</c>, if service was started, <c>false</c> otherwise.</returns>
        public bool StartService()
        {
            bool success = true;
            try
            {
                // Start MIG Gateways
                foreach (var gw in Gateways)
                {
                    if (!gw.Start())
                    {
                        Log.Warn("Error starting Gateway {0}", gw.GetName());
                        success = false;
                    }
                }
                // Initialize MIG Interfaces
                foreach (Interface iface in configuration.Interfaces)
                {
                    if (iface.IsEnabled)
                    {
                        EnableInterface(iface.Domain);
                    }
                    else
                    {
                        DisableInterface(iface.Domain);
                    }
                }
            }
            catch (Exception e)
            {
                MigService.Log.Error(e);
                success = false;
            }
            return success;
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public void StopService()
        {
            Log.Debug("Stopping MigService");
            foreach (var migInterface in Interfaces)
            {
                Log.Debug("Disabling Interface {0}", migInterface.GetDomain());
                DisableInterface(migInterface.GetDomain());
            }
            foreach (var gw in Gateways)
            {
                Log.Debug("Stopping Gateway {0}", gw.GetName());
                gw.Stop();
            }
            Log.Debug("Stopped MigService");
        }

        #endregion

        #region Public members

        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        /// <value>The configuration.</value>
        public MigServiceConfiguration Configuration
        {
            get { return configuration; }
            set
            {
                // TODO: SHOULD DISPOSE PREVIOUS CONFIG AND ALLOCATED OBJECTS!!
                configuration = value;
                // Create MIG Gateways
                for (int g = 0; g < configuration.Gateways.Count; g++)
                {
                    var gwConfig = configuration.Gateways[g];
                    var gateway = AddGateway(gwConfig.Name);
//                    if (gateway != null && gwConfig.IsEnabled)
//                        gateway.Start();
                }
                // Create MIG interfaces
                for (int i = 0; i < configuration.Interfaces.Count; i++)
                {
                    var ifConfig = configuration.Interfaces[i];
                    var migInterface = AddInterface(ifConfig.Domain, ifConfig.AssemblyName);
//                    if (migInterface != null && ifConfig.IsEnabled)
//                        migInterface.Connect();
                }
            }
        }

        /// <summary>
        /// Gets the gateway.
        /// </summary>
        /// <returns>The gateway.</returns>
        /// <param name="className">Class name.</param>
        public MigGateway GetGateway(string className)
        {
            return Gateways.Find(gw => gw.GetName().Equals(className));
        }

        /// <summary>
        /// Adds the gateway.
        /// </summary>
        /// <returns>The gateway.</returns>
        /// <param name="className">Class name.</param>
        /// <param name="assemblyName">Assembly name.</param>
        public MigGateway AddGateway(string className, string assemblyName = "")
        {
            MigGateway migGateway = GetGateway(className);
            if (migGateway == null)
            {
                try
                {
                    var type = TypeLookup("MIG.Gateways." + className, assemblyName);
                    migGateway = (MigGateway)Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    MigService.Log.Error(e);
                }
                if (migGateway != null)
                {
                    Log.Debug("Adding Gateway {0}", migGateway.GetName());
                    Gateways.Add(migGateway);
                    migGateway.PreProcessRequest += Gateway_PreProcessRequest;
                    migGateway.PostProcessRequest += Gateway_PostProcessRequest;
                }
            }
            // Try loading gateway settings from MIG configuration
            var config = configuration.GetGateway(migGateway.GetName());
            if (config == null)
            {
                config = new Gateway();
                config.Name = migGateway.GetName();
                if (config.Options == null)
                    config.Options = new List<Option>();
                configuration.Gateways.Add(config);
            }
            if (migGateway != null)
            {
                Log.Debug("Setting Gateway options");
                migGateway.Options = config.Options;
                foreach (var opt in configuration.GetGateway(migGateway.GetName()).Options)
                {
                    migGateway.SetOption(opt.Name, opt.Value);
                }
            }
            return migGateway;
        }

        /// <summary>
        /// Gets the interface.
        /// </summary>
        /// <returns>The interface.</returns>
        /// <param name="domain">Domain.</param>
        public MigInterface GetInterface(string domain)
        {
            return Interfaces.Find(iface => iface.GetDomain().Equals(domain));
        }

        /// <summary>
        /// Adds the interface.
        /// </summary>
        /// <returns>The interface.</returns>
        /// <param name="domain">Domain.</param>
        /// <param name="assemblyName">Assembly name.</param>
        public MigInterface AddInterface(string domain, string assemblyName = "")
        {
            MigInterface migInterface = GetInterface(domain);
            if (migInterface == null)
            {
                try
                {
                    var type = TypeLookup("MIG.Interfaces." + domain, assemblyName);
                    migInterface = (MigInterface)Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    MigService.Log.Error(e);
                }
                if (migInterface != null)
                {
                    Log.Debug("Adding Interface {0}", migInterface.GetDomain());
                    Interfaces.Add(migInterface);
                    migInterface.InterfaceModulesChanged += MigService_InterfaceModulesChanged;
                    migInterface.InterfacePropertyChanged += MigService_InterfacePropertyChanged;
                }
            }
            // Try loading interface settings from MIG configuration
            var config = configuration.GetInterface(domain);
            if (config == null)
            {
                config = new Interface();
                config.Domain = domain;
                if (config.Options == null)
                    config.Options = new List<Option>();
                configuration.Interfaces.Add(config);
            }
            if (migInterface != null)
            {
                Log.Debug("Setting Interface options");
                migInterface.Options = config.Options;
                foreach (var opt in config.Options)
                {
                    migInterface.SetOption(opt.Name, opt.Value);
                }
            }
            return migInterface;
        }

        /// <summary>
        /// Removes the interface.
        /// </summary>
        /// <param name="domain">Domain.</param>
        public void RemoveInterface(string domain)
        {
            var migInterface = DisableInterface(domain);
            if (migInterface != null)
            {
                Log.Debug("Removing Interface {0}", domain);
                migInterface.InterfaceModulesChanged -= MigService_InterfaceModulesChanged;
                migInterface.InterfacePropertyChanged -= MigService_InterfacePropertyChanged;
                Interfaces.Remove(migInterface);
                configuration.GetInterface(domain).IsEnabled = false;
            }
            else
            {
                Log.Debug("Interface not found {0}", domain);
            }
        }

        /// <summary>
        /// Enables the interface.
        /// </summary>
        /// <returns>The interface.</returns>
        /// <param name="domain">Domain.</param>
        public MigInterface EnableInterface(string domain)
        {
            MigInterface migInterface = GetInterface(domain);
            if (migInterface != null)
            {
                Log.Debug("Enabling Interface {0}", domain);
                configuration.GetInterface(domain).IsEnabled = true;
                migInterface.IsEnabled = true;
                migInterface.Connect();
            }
            else
            {
                Log.Debug("Interface not found {0}", domain);
            }
            return migInterface;
        }

        /// <summary>
        /// Disables the interface.
        /// </summary>
        /// <returns>The interface.</returns>
        /// <param name="domain">Domain.</param>
        public MigInterface DisableInterface(string domain)
        {
            MigInterface migInterface = GetInterface(domain);
            if (migInterface != null)
            {
                Log.Debug("Disabling Interface {0}", domain);
                configuration.GetInterface(domain).IsEnabled = false;
                migInterface.IsEnabled = false;
                migInterface.Disconnect();
            }
            else
            {
                Log.Debug("Interface not found {0}", domain);
            }
            return migInterface;
        }

        /// <summary>
        /// Gets the event.
        /// </summary>
        /// <returns>The event.</returns>
        /// <param name="domain">Domain.</param>
        /// <param name="source">Source.</param>
        /// <param name="description">Description.</param>
        /// <param name="propertyPath">Property path.</param>
        /// <param name="propertyValue">Property value.</param>
        public MigEvent GetEvent(string domain, string source, string description, string propertyPath, object propertyValue)
        {
            return new MigEvent(domain, source, description, propertyPath, propertyValue);
        }

        /// <summary>
        /// Raises the event.
        /// </summary>
        /// <param name="domain">Domain.</param>
        /// <param name="source">Source.</param>
        /// <param name="description">Description.</param>
        /// <param name="propertyPath">Property path.</param>
        /// <param name="propertyValue">Property value.</param>
        public void RaiseEvent(string domain, string source, string description, string propertyPath, object propertyValue)
        {
            RaiseEvent(GetEvent(domain, source, description, propertyPath, propertyValue));
        }

        /// <summary>
        /// Raises the event.
        /// </summary>
        /// <param name="evt">Evt.</param>
        public void RaiseEvent(MigEvent evt)
        {
            OnInterfacePropertyChanged(new InterfacePropertyChangedEventArgs(evt));
        }

        /// <summary>
        /// Registers the API.
        /// </summary>
        /// <param name="basePath">Base path.</param>
        /// <param name="callback">Callback.</param>
        public void RegisterApi(string basePath, Func<MigClientRequest, object> callback)
        {
            Log.Debug("Registering Dynamic API {0}", basePath);
            dynamicApi.Register(basePath, callback);
        }

        #region public Static Utility methods

        public static string JsonSerialize(object data, bool indent = false)
        {
            return Utility.Serialization.JsonSerialize(data, indent);
        }

        public static void ShellCommand(string command, string args)
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo(command, args);
            processInfo.RedirectStandardOutput = false;
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            var process = new System.Diagnostics.Process();
            process.StartInfo = processInfo;
            process.Start();
        }

        public static Type TypeLookup(string typeName, string assemblyName)
        {
            var type = Type.GetType(typeName + (String.IsNullOrWhiteSpace(assemblyName) ? "" : ", " + Path.Combine("lib", "mig", assemblyName)));
            if (type == null)
                type = Type.GetType(typeName + (String.IsNullOrWhiteSpace(assemblyName) ? "" : ", " + Path.Combine("lib", assemblyName)));
            if (type == null)
                type = Type.GetType(typeName + (String.IsNullOrWhiteSpace(assemblyName) ? "" : ", " + assemblyName));
            return type;
        }

        public static string GetAssemblyDirectory(Assembly assembly)
        {
            string codeBase = assembly.CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        #endregion

        #endregion

        #region Private members

        #region Utility methods

        private object TryDynamicApi(MigClientRequest request)
        {
            object response = null;
            var command = request.Command;
            var apiCommand = command.Domain + "/" + command.Address + "/" + command.Command;
            // Standard MIG API commands should respect the the form
            // <domain>/<address>/<command>[/<option_1>/../<option_n>] 
            var handler = dynamicApi.FindExact(apiCommand);
            if (handler == null)
            {
                // We didn't find a dynamic API matching standard formatted MIG API command, so we try
                // to just find any dynamic API that starts with the requested url
                handler = dynamicApi.FindMatching(command.OriginalRequest.Trim('/'));
            }
            if (handler != null)
            {
                // We found an handler for this API call, so invoke it
                response = handler(request);
            }
            // TODO: the following is old code used for HomeGenie, move to HG
            /*
            if (handler != null)
            {
                // explicit command API handlers registered in the form <domain>/<address>/<command>
                // receives only the remaining part of the request after the <command>
                var args = command.OriginalRequest.Substring(registeredApi.Length).Trim('/');
                response = handler(args);
            }
            else
            {
                handler = dynamicApi.FindMatching(command.OriginalRequest.Trim('/'));
                if (handler != null)
                {
                    // other command API handlers
                    // receives the full request string
                    response = handler(command.OriginalRequest.Trim('/'));
                }
            }
            */
            return response;
        }

        #endregion

        #region MigInterface events

        private void MigService_InterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args)
        {
            // event propagation
            // TODO: should preserve the original "sender" ?
            OnInterfacePropertyChanged(args);
        }

        private void MigService_InterfaceModulesChanged(object sender, InterfaceModulesChangedEventArgs args)
        {
            // event propagation
            // TODO: should preserve the original "sender" ?
            OnInterfaceModulesChanged(args);
        }

        #endregion

        #region MigGateway events

        private void Gateway_PreProcessRequest(object sender, ProcessRequestEventArgs args)
        {
            var request = args.Request;
            // Route event
            OnPreProcessRequest(request);

            if (request.Handled)
                return;

            var command = request.Command;
            if (command.Domain == "MIGService.Interfaces")
            {
                // This is a MIGService namespace Web API
                switch (command.Command)
                {
                case "IsEnabled.Set":
                    if (command.GetOption(0) == "1")
                    {
                        if (EnableInterface(command.Address) != null)
                            request.ResponseData = new ResponseStatus(Status.Ok, String.Format("Interface {0} enabled", command.Address));
                        else
                            request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface {0} not found", command.Address));
                    }
                    else
                    {
                        if (DisableInterface(command.Address) != null)
                            request.ResponseData = new ResponseStatus(Status.Ok, String.Format("Interface {0} disabled", command.Address));
                        else
                            request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface {0} not found", command.Address));
                    }
                    OnInterfacePropertyChanged(new InterfacePropertyChangedEventArgs("MIGService.Interfaces", command.Address, "MIG Interface", "Status.IsEnabled", command.GetOption(0)));
                    break;
                case "IsEnabled.Get":
                    request.ResponseData = new ResponseText(configuration.GetInterface(command.Address).IsEnabled ? "1" : "0");
                    break;
                case "Options.Set":
                    {
                        var iface = GetInterface(command.Address);
                        if (iface != null)
                        {
                            iface.SetOption(command.GetOption(0), command.GetOption(1));
                            request.ResponseData = new ResponseStatus(Status.Ok, String.Format("{0} option '{1}' set to '{2}'", command.Address, command.GetOption(0), command.GetOption(1)));
                        }
                        else
                        {
                            request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface {0} not found", command.Address));
                        }
                    }
                    OnInterfacePropertyChanged(new InterfacePropertyChangedEventArgs("MIGService.Interfaces", command.Address, "MIG Interface", "Options." + command.GetOption(0), command.GetOption(1)));
                    break;
                case "Options.Get":
                    {
                        var iface = GetInterface(command.Address);
                        if (iface != null)
                        {
                            string optionValue = iface.GetOption(command.GetOption(0)).Value;
                            request.ResponseData = new ResponseText(optionValue);
                        }
                        else
                        {
                            request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface {0} not found", command.Address));
                        }
                    }
                    break;
                default:
                    break;
                }
            }
            else
            {
                // Try processing as MigInterface Api or Web Service Dynamic Api
                var iface = (from miginterface in Interfaces
                    let ns = miginterface.GetType().Namespace
                    let domain = ns.Substring(ns.LastIndexOf(".") + 1) + "." + miginterface.GetType().Name
                    where (command.Domain != null && command.Domain.StartsWith(domain))
                    select miginterface).FirstOrDefault();
                if (iface != null) // && iface.IsEnabled)
                {
                    //if (iface.IsConnected)
                    //{
                        try
                        {
                            request.ResponseData = iface.InterfaceControl(command);
                        }
                        catch (Exception ex)
                        {
                            request.ResponseData = new ResponseStatus(Status.Error, MigService.JsonSerialize(ex));
                        }
                    //}
                    //else
                    //{
                    //    request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface '{0}' not connected", iface.GetDomain()));
                    //}
                }
                // Try processing as Dynamic API
                if ((request.ResponseData == null || request.ResponseData.Equals(String.Empty)))
                {
                    request.ResponseData = TryDynamicApi(request);
                }

            }

        }

        private void Gateway_PostProcessRequest(object sender, ProcessRequestEventArgs args)
        {
            var request = args.Request;
            // Route event
            OnPostProcessRequest(request);
        }

        #endregion

        #region MigService Events

        protected virtual void OnPreProcessRequest(MigClientRequest request)
        {
            if (GatewayRequestPreProcess != null)
                GatewayRequestPreProcess(this, new ProcessRequestEventArgs(request));
        }

        protected virtual void OnPostProcessRequest(MigClientRequest request)
        {
            if (GatewayRequestPostProcess != null)
                GatewayRequestPostProcess(this, new ProcessRequestEventArgs(request));
        }

        protected virtual void OnInterfaceModulesChanged(InterfaceModulesChangedEventArgs args)
        {
            Log.Info(args.Domain);
            if (InterfaceModulesChanged != null)
            {
                InterfaceModulesChanged(this, args);
            }
        }

        protected virtual void OnInterfacePropertyChanged(InterfacePropertyChangedEventArgs args)
        {
            Log.Info(args.EventData);
            if (InterfacePropertyChanged != null)
            {
                InterfacePropertyChanged(this, args);
            }
            // Route event to MIG.Gateways as well
            foreach (var gateway in Gateways)
                gateway.OnInterfacePropertyChanged(this, args);
        }

        #endregion

        #endregion

    }

}