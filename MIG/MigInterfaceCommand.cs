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

namespace MIG
{
    public class MigInterfaceCommand
    {
        private string[] options = new string[0];

        public string Domain { get; }
        public string Address { get; }
        public string Command { get; }
        /// <summary>
        /// The full unparsed original request string.
        /// </summary>
        public string OriginalRequest { get; }

        public MigInterfaceCommand(string request)
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

    }
}

