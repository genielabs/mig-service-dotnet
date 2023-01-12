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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MIG
{
    public class MigClientRequest
    {
        private object responseData;

        public MigContext Context  { get; }
        public MigInterfaceCommand Command { get; }

        public string RequestText;
        public byte[] RequestData;

        public object ResponseData
        {
            get { return responseData; }
            set
            {
                if (value != null)
                    Handled = true;
                responseData = value;
            }
        }

        public bool Handled = false;

        public MigClientRequest(MigContext context, MigInterfaceCommand command)
        {
            Context = context;
            Command = command;
        }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum Status
    {
        Ok,
        Error
    }

    public class ResponseText
    {
        public string ResponseValue { get; }

        public ResponseText(string response)
        {
            ResponseValue = response;
        }
    }

    public class ResponseStatus
    {
        public Status Status { get; }
        public string Message { get; }

        public ResponseStatus(Status status, string message = "")
        {
            Status = status;
            Message = message;
        }
    }

}

