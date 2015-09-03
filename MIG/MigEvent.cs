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

    [Serializable()]
    public class MigEvent
    {
        public DateTime Timestamp = DateTime.UtcNow;

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

