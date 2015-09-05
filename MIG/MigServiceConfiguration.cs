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

