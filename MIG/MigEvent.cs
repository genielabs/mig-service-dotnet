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

namespace MIG
{

    [Serializable()]
    public class MigEvent
    {
        public DateTime Timestamp { get; set; }

        public double UnixTimestamp
        {
            get
            {
                var uts = (Timestamp - new DateTime(1970, 1, 1, 0, 0, 0));
                return uts.TotalMilliseconds;
            }
        }

        public string Domain { get; }
        public string Source { get; }
        public string Description { get; }
        public string Property { get; }
        public object Value { get; set; }

        public MigEvent(string domain, string source, string description, string propertyPath, object propertyValue)
        {
            Timestamp = DateTime.UtcNow;
            Domain = domain;
            Source = source;
            Description = description;
            Property = propertyPath;
            Value = propertyValue;
        }

        public override string ToString()
        {
            //string date = this.Timestamp.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz");
            //string logentrytxt = date + "\t" + this.Domain + "\t" + this.Source + "\t" + (this.Description == "" ? "-" : this.Description) + "\t" + this.Property + "\t" + this.Value;
            string logentrytxt = this.Domain + "\t" + this.Source + "\t" + (this.Description == "" ? "-" : this.Description) + "\t" + this.Property + "\t" + this.Value;
            return logentrytxt;
        }
    }

}

