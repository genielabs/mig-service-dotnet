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
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace MIG.Logging
{
    public static class LogManager
    {
        private static ILoggerFactory _factory;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes the LogManager with the application's ILoggerFactory.
        /// This is the recommended approach for production applications.
        /// </summary>
        public static void Initialize(ILoggerFactory loggerFactory)
        {
            lock (_lock)
            {
                _factory = loggerFactory;
            }
        }

        /// <summary>
        /// Gets a logger for the specified category name.
        /// </summary>
        public static Logger GetLogger(string categoryName)
        {
            if (_factory == null)
            {
                lock (_lock)
                {
                    if (_factory == null)
                    {
#if NET6_0_OR_GREATER
                        _factory = LoggerFactory.Create(builder =>
                        {
                            builder
                                .AddSimpleConsole(options =>
                                {
                                    options.SingleLine = true;
                                    options.TimestampFormat = "HH:mm:ss ";
                                })
                                .SetMinimumLevel(LogLevel.Trace);
                        });
#else
                        var services = new ServiceCollection();
                        services.AddLogging(builder =>
                        {
                            builder.AddConsole(); 
                            builder.SetMinimumLevel(LogLevel.Trace);
                        });
                        var serviceProvider = services.BuildServiceProvider();
                        _factory = serviceProvider.GetService<ILoggerFactory>();
#endif
                        
                        var initLogger = _factory.CreateLogger("MIG.LogManager");
                        initLogger.LogWarning("LogManager was not initialized. A default console logger will be used. For production applications, call LogManager.Initialize(loggerFactory) at startup.");
                    }
                }
            }
            return new Logger(_factory.CreateLogger(categoryName));
        }

        /// <summary>
        /// Gets a logger using the caller's file name as the category.
        /// </summary>
        public static Logger GetCurrentClassLogger([CallerFilePath] string callerFilePath = "")
        {
            var categoryName = Path.GetFileNameWithoutExtension(callerFilePath);
            return GetLogger(categoryName);
        }
    }
}
