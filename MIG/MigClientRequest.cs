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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MIG
{
    public class MigClientRequest
    {
        private object responseData;

        public MigContext Context  { get; }
        public MigInterfaceCommand Command { get; }

        public string RequestData;

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
            Command = command;
            Context = context;
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
            this.Status = status;
            this.Message = message;
        }
    }

}

