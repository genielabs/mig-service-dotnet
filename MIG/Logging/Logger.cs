/*
  This file is part of MIG (https://github.com/genielabs/mig-service-dotnet)

  Copyright (2012-2025) G-Labs (https://github.com/genielabs)

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

using Microsoft.Extensions.Logging;
using System;

namespace MIG.Logging
{
    /// <summary>
    /// A wrapper class that mimics the NLog Logger API but uses a Microsoft.Extensions.Logging.ILogger internally.
    /// This provides backward compatibility for existing code.
    /// </summary>
    public class Logger
    {
        private readonly ILogger _melLogger;

        internal Logger(ILogger melLogger)
        {
            _melLogger = melLogger ?? throw new ArgumentNullException(nameof(melLogger));
        }

        public void Debug(string message, params object[] args)
        {
            _melLogger.LogDebug(message, args);
        }

        public void Info(string message, params object[] args)
        {
            _melLogger.LogInformation(message, args);
        }

        public void Warn(string message, params object[] args)
        {
            _melLogger.LogWarning(message, args);
        }

        public void Error(string message, params object[] args)
        {
            _melLogger.LogError(message, args);
        }

        public void Error(Exception exception, string message = null, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
            {
                message = exception.Message;
            }
            _melLogger.LogError(exception, message, args);
        }

        public void Error(Exception exception)
        {
            _melLogger.LogError(exception, exception.Message);
        }

        public void Trace(string message, params object[] args)
        {
            _melLogger.LogTrace(message, args);
        }
        
        
        public void Debug(object value)
        {
            _melLogger.LogDebug("{ObjectValue}", value);
        }

        public void Info(object value)
        {
            _melLogger.LogInformation("{ObjectValue}", value);
        }

        public void Warn(object value)
        {
            _melLogger.LogWarning("{ObjectValue}", value);
        }

        public void Error(object value)
        {
            if (value is Exception ex)
            {
                Error(ex);
            }
            else
            {
                _melLogger.LogError("{ObjectValue}", value);
            }
        }
        
        public void Trace(object value)
        {
            _melLogger.LogTrace("{ObjectValue}", value);
        }
    }
}
