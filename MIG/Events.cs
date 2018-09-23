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

