/*
  This file is part of MIG (https://github.com/genielabs/mig-service-dotnet)
 
  Copyright (2012-2023) G-Labs (https://github.com/genielabs)

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
using System.Collections.Generic;
using System.Xml.Serialization;

namespace MIG.Config
{

    [Serializable()]
    public class MigServiceConfiguration
    {
        public List<Gateway> Gateways = new List<Gateway>();

        public List<Interface> Interfaces = new List<Interface>();

        public Interface GetInterface(string domain)
        {
            return this.Interfaces.Find(i => i.Domain.Equals(domain));
        }

        public Gateway GetGateway(string name)
        {
            return this.Gateways.Find(g => g.Name.Equals(name));
        }
    }

    [Serializable]
    public class Gateway
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public bool IsEnabled { get; set; }

        public List<Option> Options = new List<Option>();
    }

    [Serializable]
    public class Option
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Value { get; set; }

        public Option()
        {
        }

        public Option(string name, string value = "")
        {
            Name = name;
            Value = value;
        }
    }

    [Serializable]
    public class Interface
    {

        [XmlAttribute]
        public string Domain { get; set; }

        public string Description { get; set; }

        [XmlAttribute]
        public bool IsEnabled { get; set; }

        public List<Option> Options = new List<Option>();

        [XmlAttribute]
        public string AssemblyName { get; set; }

        // TODO: add SupportedPlatform field (Windows, Unix, All)
    }
}

