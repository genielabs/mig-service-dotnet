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
using MIG.Config;

namespace MIG
{

    public class OptionChangedEventArgs
    {
        public readonly Option Option;
        public OptionChangedEventArgs(Option option)
        {
            Option = option;
        }
    }

    public class ProcessRequestEventArgs
    {
        public readonly MigClientRequest Request;
        public ProcessRequestEventArgs(MigClientRequest request)
        {
            Request = request;
        }
    }


    public class InterfaceModulesChangedEventArgs
    {
        public readonly string Domain;
        public InterfaceModulesChangedEventArgs(string domain)
        {
            Domain = domain;
        }
    }

    public class InterfacePropertyChangedEventArgs
    {
        public readonly MigEvent EventData;

        public InterfacePropertyChangedEventArgs(MigEvent eventData)
        {
            EventData = eventData;
        }

        public InterfacePropertyChangedEventArgs(string domain, string source, string description, string propertyPath, object propertyValue)
        {
            EventData = new MigEvent(domain, source, description, propertyPath, propertyValue);
        }
    }

}

