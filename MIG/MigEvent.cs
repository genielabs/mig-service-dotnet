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
using MessagePack;

namespace MIG
{

    [Serializable, MessagePackObject]
    public class MigEvent
    {
        [Key(0)]
        public DateTime Timestamp { get; set; }

        [Key(1)]
        public double UnixTimestamp
        {
            get
            {
                var uts = (Timestamp - new DateTime(1970, 1, 1, 0, 0, 0));
                return uts.TotalMilliseconds;
            }
        }

        [Key(2)]
        public string Domain { get; init; }
        [Key(3)]
        public string Source { get; init; }
        [Key(4)]
        public string Description { get; init; }
        [Key(5)]
        public string Property { get; init; }
        [Key(6)]
        public object Value { get; set; }

        public MigEvent()
        {
        }
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
            string logEntryTxt = Domain + "\t" + Source + "\t" + (Description == "" ? "-" : Description) + "\t" + Property + "\t" + Value;
            return logEntryTxt;
        }
    }

}
