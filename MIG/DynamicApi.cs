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
using System.Linq;

namespace MIG
{
    public class DynamicApi
    {
        private Dictionary<string, Func<MigClientRequest, object>> dynamicApi = new Dictionary<string, Func<MigClientRequest, object>>();

        public DynamicApi()
        {
        }

        public Func<MigClientRequest, object> FindExact(string request)
        {
            Func<MigClientRequest, object> handler = null;
            if (dynamicApi.ContainsKey(request))
            {
                handler = dynamicApi[request];
            }
            return handler;
        }

        public Func<MigClientRequest, object> FindMatching(string request)
        {
            Func<MigClientRequest, object> handler = null;
            for (int i = 0; i < dynamicApi.Keys.Count; i++)
            {
                if (request.StartsWith(dynamicApi.Keys.ElementAt(i)))
                {
                    handler = dynamicApi[dynamicApi.Keys.ElementAt(i)];
                    break;
                }
            }
            return handler;
        }

        public void Register(string request, Func<MigClientRequest, object> handlerfn)
        {
            //TODO: should this throw an exception if already registered?
            if (dynamicApi.ContainsKey(request))
            {
                dynamicApi[request] = handlerfn;
            }
            else
            {
                dynamicApi.Add(request, handlerfn);
            }
        }

        public void UnRegister(string request)
        {
            if (dynamicApi.ContainsKey(request))
            {
                dynamicApi.Remove(request);
            }
        }

    }
}

