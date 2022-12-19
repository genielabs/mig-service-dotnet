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

namespace MIG
{
    public class MigInterfaceCommand
    {
        private string[] options = new string[0];

        public string Domain { get; set; }
        public string Address { get; set; }
        public string Command { get; private set; }
        public object Data { get; internal set; }
        /// <summary>
        /// The full unparsed original request string.
        /// </summary>
        public string OriginalRequest { get; private set; }

        public MigInterfaceCommand(string request, object data)
        {
            Data = data;
            BuildRequest(request);
        }
        public MigInterfaceCommand(string request)
        {
            BuildRequest(request);
        }

        public string GetOption(int index)
        {
            var option = "";
            if (index < options.Length)
            {
                option = Uri.UnescapeDataString(options[ index ]);
            }
            return option;
        }

        public string OptionsString
        {
            get
            {
                var optiontext = "";
                for (var o = 0; o < options.Length; o++)
                {
                    optiontext += options[ o ] + "/";
                }
                return optiontext;
            }
        }

        private void BuildRequest(string request, object data = null)
        {
            OriginalRequest = request;
            try
            {
                var requests = request.Trim('/').Split(new char[] { '/' }, StringSplitOptions.None);
                // At least two elements required for a valid command
                if (requests.Length > 1)
                {
                    Domain = requests[0];
                    if (Domain == "html")
                    {
                        return;
                    }
                    else if (requests.Length > 2)
                    {
                        Address = requests[1];
                        Command = requests[2];
                        if (requests.Length > 3)
                        {
                            options = new string[requests.Length - 3];
                            Array.Copy(requests, 3, options, 0, requests.Length - 3);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MigService.Log.Error(e);
            }
        }

    }
}

