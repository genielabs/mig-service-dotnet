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
    public enum ContextSource
    {
        TcpSocketGateway,
        WebServiceGateway,
        WebSocketGateway,
        MqttServiceGateWay
    }

    public class MigContext
    {
        public ContextSource Source { get; }
        public object Data { get; }

        public MigContext(ContextSource source, object data)
        {
            Source = source;
            Data = data;
        }
    }
}

