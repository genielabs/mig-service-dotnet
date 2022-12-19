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

