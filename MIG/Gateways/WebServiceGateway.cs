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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Web;

using Ude;
using CommonMark;

using MIG.Config;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace MIG.Gateways
{
    class WebFile
    {
        public DateTime Timestamp = DateTime.Now;
        public string FilePath = "";
        public string Content = "";
        public Encoding Encoding;
        public bool IsCached;
    }

    class SseEvent
    {
        // Wrapper class for MigEvent
        // used to provide a better resolution Timestamp
        // and avoid events skipping
        public MigEvent Event;
        public DateTime Timestamp;

        public SseEvent(MigEvent evt)
        {
            Event = evt;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class HttpListenerCallbackState
    {
        private readonly HttpListener listener;
        private readonly AutoResetEvent listenForNextRequest;

        public HttpListenerCallbackState(HttpListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");
            this.listener = listener;
            listenForNextRequest = new AutoResetEvent(false);
        }

        public HttpListener Listener { get { return listener; } }

        public AutoResetEvent ListenForNextRequest { get { return listenForNextRequest; } }
    }

    public class WebServiceGateway : IMigGateway, IDisposable
    {
        public event PreProcessRequestEventHandler PreProcessRequest;
        public event PostProcessRequestEventHandler PostProcessRequest;
        //private event InterfacePropertyChangedEventHandler PropertyChanged;

        private ManualResetEvent stopEvent = new ManualResetEvent(false);

        // Web Service configuration fields
        private string baseUrl = "/";
        private string homePath = "html";
        private string serviceHost = "*";
        private string servicePort = "8080";
        private string serviceUsername = "admin";
        private string servicePassword = "";
        private string corsAllowOrigin = "*";
        private bool enableFileCache;
        private AuthenticationSchemes authenticationSchema = AuthenticationSchemes.Digest;

        private Encoding defaultWebFileEncoding = Encoding.GetEncoding("UTF-8");

        private List<WebFile> filesCache = new List<WebFile>();
        private List<string> httpCacheIgnoreList = new List<string>();
        private List<string> urlAliases = new List<string>();

        private const int sseEventBufferSize = 100;
        private List<SseEvent> sseEventBuffer;
        private object sseEventToken = new object();

        public WebServiceGateway()
        {
            sseEventBuffer = new List<SseEvent>();
        }

        public List<Option> Options { get; set; }

        public void OnSetOption(Option option)
        {
            switch (option.Name)
            {
            case WebServiceGatewayOptions.BaseUrl:
                baseUrl = option.Value.Trim('/') + "/";
                break;
            case WebServiceGatewayOptions.HomePath:
                homePath = option.Value;
                break;
            case WebServiceGatewayOptions.Host:
                serviceHost = option.Value;
                break;
            case WebServiceGatewayOptions.Port:
                servicePort = option.Value;
                break;
            case WebServiceGatewayOptions.Username:
                serviceUsername = option.Value;
                break;
            case WebServiceGatewayOptions.Password:
                servicePassword = option.Value;
                break;
            case WebServiceGatewayOptions.EnableFileCaching:
                ClearWebCache();
                bool.TryParse(option.Value, out enableFileCache);
                break;
            case WebServiceGatewayOptions.CorsAllowOrigin:
            case "corsAllowOrigin":
                corsAllowOrigin = option.Value;
                break;
            default:
                if (option.Name.StartsWith(WebServiceGatewayOptions.HttpCacheIgnorePrefix))
                    HttpCacheIgnoreAdd(option.Value);
                else if (option.Name.StartsWith(WebServiceGatewayOptions.UrlAliasPrefix))
                    UrlAliasAdd(option.Value);
                break;
            }
        }

        public void OnInterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args)
        {
            if (sseEventBuffer.Count > sseEventBufferSize)
            {
                sseEventBuffer.RemoveRange(0, sseEventBuffer.Count - sseEventBufferSize);
            }
            sseEventBuffer.Add(new SseEvent(args.EventData));
            // dirty work around for signaling new event and 
            // avoiding locks on long socket timetout
            lock (sseEventToken)
                Monitor.PulseAll(sseEventToken);
        }

        public bool Start()
        {
            MigService.Log.Debug("Starting Gateway {0}", this.GetName());
            bool success = false;
            string[] bindingPrefixes = new string[1] {
                $@"http://{serviceHost}:{servicePort}/"
            };
            try
            {
                StartListener(bindingPrefixes);
                success = true;
            }
            catch (Exception e)
            {
                MigService.Log.Error(e);
            }
            return success;
        }

        public void Stop()
        {
            StopListener();
        }

        public void Dispose()
        {
            Stop();
        }

        private void Worker(object state)
        {
            HttpListenerRequest request = null;
            HttpListenerResponse response = null;
            try
            {
                var context = state as HttpListenerContext;
                //
                request = context.Request;
                response = context.Response;
                //
                if (request.UserLanguages != null && request.UserLanguages.Length > 0)
                {
                    try
                    {
                        CultureInfo culture = CultureInfo.CreateSpecificCulture(request.UserLanguages[0].ToLowerInvariant().Trim());
                        Thread.CurrentThread.CurrentCulture = culture;
                        Thread.CurrentThread.CurrentUICulture = culture;
                    }
                    catch
                    {
                    }
                }
                //
                // TODO: deprecate this - implement true SSL support
                if (request.IsSecureConnection)
                {
                    var clientCertificate = request.GetClientCertificate();
                    var chain = new X509Chain
                    {
                        ChainPolicy = {RevocationMode = X509RevocationMode.NoCheck}
                    };
                    chain.Build(clientCertificate);
                    if (chain.ChainStatus.Length != 0)
                    {
                        // Invalid certificate
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        response.OutputStream.Close();
                        return;
                    }
                }
                //
                response.Headers.Set(HttpResponseHeader.Server, "MIG WebService Gateway");
                response.KeepAlive = false;
                //
                bool requestHasAuthorizationHeader = request.Headers["Authorization"] != null;
                string remoteAddress = request.RemoteEndPoint.Address.ToString();
                string logExtras = "";
                //
                if (String.IsNullOrEmpty(servicePassword) || requestHasAuthorizationHeader) //request.IsAuthenticated)
                {
                    bool verified = false;
                    //
                    if (!String.IsNullOrEmpty(corsAllowOrigin))
                    {
                        if (requestHasAuthorizationHeader)
                        {
                            response.Headers.Set("Access-Control-Allow-Origin", (corsAllowOrigin != "*" || request.UrlReferrer == null) ? corsAllowOrigin : request.UrlReferrer.Scheme + "://" + request.UrlReferrer.Host + ":" + request.UrlReferrer.Port);
                            response.Headers.Set("Access-Control-Allow-Credentials", "true");
                        }
                        else
                        {
                            response.Headers.Set("Access-Control-Allow-Origin", corsAllowOrigin);
                        }
                    }
                    //
                    //
                    //NOTE: context.User.Identity and request.IsAuthenticated
                    //aren't working under MONO with this code =/
                    //so we proceed by manually parsing Authorization header
                    //
                    //HttpListenerBasicIdentity identity = null;
                    //
                    if (requestHasAuthorizationHeader)
                    {
                        //identity = (HttpListenerBasicIdentity)context.User.Identity;
                        // authuser = identity.Name;
                        // authpass = identity.Password;
                        string[] authorizationParts = request.Headers["Authorization"].Split(' ');
                        string authorizationSchema = authorizationParts[0];
                        if (authorizationSchema == AuthenticationSchemes.Basic.ToString())
                        {
                            byte[] encodedDataAsBytes = Convert.FromBase64String(authorizationParts[1]);
                            string authorizationToken = Encoding.UTF8.GetString(encodedDataAsBytes);
                            var authUser = authorizationToken.Split(':')[0];
                            var authPass = authorizationToken.Split(':')[1];
                            if (authUser == serviceUsername && authPass == servicePassword)
                            {
                                verified = true;
                            }
                        }
                        else if (authorizationSchema == AuthenticationSchemes.Digest.ToString())
                        {
                            // TODO: implment Digest auth
                            //verified = true;
                            throw new NotImplementedException("Digest");
                        }
                        else
                        {
                            // TODO: unsupported authorization schema
                        }
                    }
                    // if no password is set authentication is disabled
                    if (!verified && String.IsNullOrEmpty(servicePassword))
                    {
                        verified = true;
                    }
                    if (verified)
                    {
                        string url = request.RawUrl.TrimStart('/').TrimStart('\\').TrimStart('.');
                        if (url.IndexOf("?") > 0)
                            url = url.Substring(0, url.IndexOf("?"));
                        // Check if this url is an alias
                        url = UrlAliasCheck(url.TrimEnd('/'));
                        //
                        // url aliasing check
                        if (url == "" || url.TrimEnd('/') == baseUrl.TrimEnd('/'))
                        {
                            // default home redirect
                            response.Redirect("/" + baseUrl.TrimEnd('/') + "/index.html"); 
                            //TODO: find a solution for HG homepage redirect ---> ?" + new TimeSpan(DateTime.UtcNow.Ticks).TotalMilliseconds + "#page_control");
                            response.Close();
                        }
                        else
                        {
                            var connectionWatch = Stopwatch.StartNew();
                            MigService.Log.Info(new MigEvent(this.GetName(), remoteAddress, "HTTP", request.HttpMethod,
                                $"{response.StatusCode} {request.RawUrl} [OPEN]"));
                            // this url is reserved for Server Sent Event stream
                            if (url.TrimEnd('/').Equals("events"))
                            {
                                HandleEventsRoute(request, response, context, remoteAddress);
                            }
                            else
                            {
                                try
                                {
                                    MigClientRequest migRequest = null;
                                    if (url.StartsWith("api/"))
                                    {
                                        string message = url.Substring(url.IndexOf('/', 1) + 1);
                                        var migContext = new MigContext(ContextSource.WebServiceGateway, context);
                                        migRequest = new MigClientRequest(migContext, new MigInterfaceCommand(message));
                                        // Disable HTTP caching
                                        response.Headers.Set(HttpResponseHeader.CacheControl, "no-cache, no-store, must-revalidate");
                                        response.Headers.Set(HttpResponseHeader.Pragma, "no-cache");
                                        response.Headers.Set(HttpResponseHeader.Expires, "0");
                                        // Store POST data (if any) in the migRequest.RequestData field
                                        migRequest.RequestData = WebServiceUtility.ReadToEnd(request.InputStream);
                                        migRequest.RequestText = request.ContentEncoding.GetString(migRequest.RequestData);
                                    }

                                    OnPreProcessRequest(migRequest);

                                    bool requestHandled = migRequest != null && migRequest.Handled;
                                    if (requestHandled)
                                    {
                                        SendResponseObject(context, migRequest.ResponseData);
                                    }
                                    else if (url.StartsWith(baseUrl) || baseUrl.Equals("/"))
                                    {
                                        // If request begins <base_url>, process as standard Web request
                                        string requestedFile = GetWebFilePath(url);
                                        if (!File.Exists(requestedFile))
                                        {
                                            response.StatusCode = (int)HttpStatusCode.NotFound;
                                            WebServiceUtility.WriteStringToContext(context, "<h1>404 - Not Found</h1>");
                                        }
                                        else
                                        {
                                            bool isText = false;
                                            if (url.ToLower().EndsWith(".js")) // || requestedurl.EndsWith(".json"))
                                            {
                                                response.ContentType = "text/javascript";
                                                isText = true;
                                            }
                                            else if (url.ToLower().EndsWith(".css"))
                                            {
                                                response.ContentType = "text/css";
                                                isText = true;
                                            }
                                            else if (url.ToLower().EndsWith(".zip"))
                                            {
                                                response.ContentType = "application/zip";
                                            }
                                            else if (url.ToLower().EndsWith(".png"))
                                            {
                                                response.ContentType = "image/png";
                                            }
                                            else if (url.ToLower().EndsWith(".jpg"))
                                            {
                                                response.ContentType = "image/jpeg";
                                            }
                                            else if (url.ToLower().EndsWith(".gif"))
                                            {
                                                response.ContentType = "image/gif";
                                            }
                                            else if (url.ToLower().EndsWith(".svg"))
                                            {
                                                response.ContentType = "image/svg+xml";
                                            }
                                            else if (url.ToLower().EndsWith(".mp3"))
                                            {
                                                response.ContentType = "audio/mp3";
                                            }
                                            else if (url.ToLower().EndsWith(".wav"))
                                            {
                                                response.ContentType = "audio/x-wav";
                                            }
                                            else if (url.ToLower().EndsWith(".appcache"))
                                            {
                                                response.ContentType = "text/cache-manifest";
                                            }
                                            else if (url.ToLower().EndsWith(".otf") || url.ToLower().EndsWith(".ttf") || url.ToLower().EndsWith(".woff") || url.ToLower().EndsWith(".woff2"))
                                            {
                                                response.ContentType = "application/octet-stream";
                                            }
                                            else if (url.ToLower().EndsWith(".xml"))
                                            {
                                                response.ContentType = "text/xml";
                                                isText = true;
                                            }
                                            else
                                            {
                                                response.ContentType = "text/html";
                                                isText = true;
                                            }

                                            var file = new FileInfo(requestedFile);
                                            response.ContentLength64 = file.Length;

                                            bool modified = true;
                                            if (request.Headers.AllKeys.Contains("If-Modified-Since"))
                                            {
                                                var modifiedSince = DateTime.MinValue;
                                                DateTime.TryParse(request.Headers["If-Modified-Since"], out modifiedSince);
                                                if (file.LastWriteTime.ToUniversalTime().Equals(modifiedSince))
                                                    modified = false;
                                            }
                                            bool disableCacheControl = HttpCacheIgnoreCheck(url);
                                            if (!modified && !disableCacheControl)
                                            {
                                                // TODO: !IMPORTANT! exclude from caching files that contains SSI tags!
                                                response.StatusCode = (int)HttpStatusCode.NotModified;
                                                //!!DISABLED!! - The following line was preventing browser to load file from cache
                                                //response.Headers.Set(HttpResponseHeader.Date, file.LastWriteTimeUtc.ToString().Replace(",", "."));
                                            }
                                            else
                                            {
                                                response.Headers.Set(HttpResponseHeader.LastModified, file.LastWriteTimeUtc.ToString().Replace(",", "."));
                                                if (disableCacheControl)
                                                {
                                                    response.Headers.Set(HttpResponseHeader.CacheControl, "no-cache, no-store, must-revalidate");
                                                    response.Headers.Set(HttpResponseHeader.Pragma, "no-cache");
                                                    response.Headers.Set(HttpResponseHeader.Expires, "0");
                                                }
                                                else
                                                {
                                                    response.Headers.Set(HttpResponseHeader.CacheControl, "max-age=86400");
                                                }

                                                // PRE PROCESS text output
                                                if (isText)
                                                {
                                                    try
                                                    {
                                                        WebFile webFile = GetWebFile(requestedFile);
                                                        response.ContentEncoding = webFile.Encoding;
                                                        response.ContentType += "; charset=" + webFile.Encoding.BodyName;
                                                        // We don't need to parse the content again if it's coming from the cache
                                                        if (!webFile.IsCached)
                                                        {
                                                            string body = webFile.Content;
                                                            if (requestedFile.EndsWith(".md"))
                                                            {
                                                                // Built-in Markdown files support
                                                                body = CommonMarkConverter.Convert(body);
                                                                // TODO: add a way to include HTML header and footer template to be appended to the
                                                                // TODO: translated markdown text
                                                            }
                                                            else
                                                            {
                                                                // HTML file
                                                                // replace prepocessor directives with values
                                                                bool tagFound;
                                                                do
                                                                {
                                                                    tagFound = false;
                                                                    int ts = body.IndexOf("{include ");
                                                                    if (ts >= 0)
                                                                    {
                                                                        int te = body.IndexOf("}", ts);
                                                                        if (te > ts)
                                                                        {
                                                                            string rs = body.Substring(ts + (te - ts) + 1);
                                                                            string cs = body.Substring(ts, te - ts + 1);
                                                                            string ls = body.Substring(0, ts);
                                                                            //
                                                                            try
                                                                            {
                                                                                if (cs.StartsWith("{include "))
                                                                                {
                                                                                    string fileName = cs.Substring(9).TrimEnd('}').Trim();
                                                                                    fileName = GetWebFilePath(fileName);
                                                                                    //
                                                                                    Encoding fileEncoding = DetectWebFileEncoding(fileName);
                                                                                    if (fileEncoding == null)
                                                                                        fileEncoding = defaultWebFileEncoding;
                                                                                    var incFile = File.ReadAllText(fileName, fileEncoding) + rs;
                                                                                    body = ls + incFile;
                                                                                }
                                                                            }
                                                                            catch
                                                                            {
                                                                                body = ls + "<h5 style=\"color:red\">Error processing '" + cs.Replace("{", "[").Replace("}", "]") + "'</h5>" + rs;
                                                                            }
                                                                            tagFound = true;
                                                                        }
                                                                    }
                                                                } while (tagFound); // continue if a pre processor tag was found
                                                                // {hostos}
                                                                body = body.Replace("{hostos}", Environment.OSVersion.Platform.ToString());
                                                                // {filebase}
                                                                body = body.Replace("{filebase}", Path.GetFileNameWithoutExtension(requestedFile));
                                                            }
                                                            // update the cache content with parsing results
                                                            webFile.Content = body;
                                                        }
                                                        // Store the cache item if the file cache is enabled
                                                        if (enableFileCache)
                                                        {
                                                            UpdateWebFileCache(requestedFile, webFile.Content, response.ContentEncoding);
                                                        }
                                                        //
                                                        WebServiceUtility.WriteStringToContext(context, webFile.Content);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        // TODO: report internal mig interface  error
                                                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                                        WebServiceUtility.WriteStringToContext(context, ex.Message + "\n" + ex.StackTrace);
                                                        MigService.Log.Error(ex);
                                                    }
                                                }
                                                else
                                                {
                                                    WebServiceUtility.WriteBytesToContext(context, File.ReadAllBytes(requestedFile));
                                                }
                                            }
                                        }
                                        requestHandled = true;
                                    }

                                    OnPostProcessRequest(migRequest);

                                    if (!requestHandled && migRequest != null && migRequest.Handled)
                                    {
                                        SendResponseObject(context, migRequest.ResponseData);
                                    }
                                    else if (!requestHandled)
                                    {
                                        response.StatusCode = (int)HttpStatusCode.NotFound;
                                        WebServiceUtility.WriteStringToContext(context, "<h1>404 - Not Found</h1>");
                                    }

                                }
                                catch (Exception eh)
                                {
                                    // TODO: add error logging 
                                    Console.Error.WriteLine(eh);
                                }
                            }
                            connectionWatch.Stop();
                            logExtras = " [CLOSED AFTER " + Math.Round(connectionWatch.Elapsed.TotalSeconds, 3) + " seconds]";
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        if (authenticationSchema == AuthenticationSchemes.Digest)
                        {
                            //WWW-Authenticate →Digest realm=”SecureZone”, nonce=”636261917149325544–5cd5f84dd2b03c350ce40be42484968168ac7c6c”, algorithm=MD5, qop=”auth”
                            response.AddHeader("WWW-Authenticate", "Digest realm=\"secure@realm.com\", qop=\"auth,auth-int\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"");
                        }
                        else
                        {
                            // this will only work in Linux (mono)
                            //response.Headers.Set(HttpResponseHeader.WwwAuthenticate, "Basic");
                            // this works both on Linux and Windows
                            response.AddHeader("WWW-Authenticate", "Basic realm=\"User Visible Realm\"");
                        }
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    if (authenticationSchema == AuthenticationSchemes.Digest)
                    {
                        response.AddHeader("WWW-Authenticate", "Digest realm=\"secure@realm.com\", qop=\"auth,auth-int\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"");
                        //response.AddHeader("WWW-Authenticate", "Digest realm=\"Secure Zone\", nonce=\"636261917149325544–5cd5f84dd2b03c350ce40be42484968168ac7c6c\", algorithm=MD5, qop=\"auth\"");
                    }
                    else
                    {
                        // this will only work in Linux (mono)
                        //response.Headers.Set(HttpResponseHeader.WwwAuthenticate, "Basic");
                        // this works both on Linux and Windows
                        response.AddHeader("WWW-Authenticate", "Basic realm=\"User Visible Realm\"");
                    }
                }
                MigService.Log.Info(new MigEvent(this.GetName(), remoteAddress, "HTTP", request.HttpMethod,
                    $"{response.StatusCode} {request.RawUrl}{logExtras}"));
            }
            catch (Exception ex)
            {
                MigService.Log.Error(ex);
            }
            finally
            {
                //
                // CleanUp/Dispose allocated resources
                //
                try { request.InputStream.Close(); } catch {
                    // TODO: add logging
                }
                try { response.OutputStream.Close(); } catch {
                    // TODO: add logging
                }
                try { response.Close(); } catch {
                    // TODO: add logging
                }
                try { response.Abort(); } catch {
                    // TODO: add logging
                }
            }
        }

        private void HandleEventsRoute(HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context,
            string remoteAddress)
        {
            // Server sent events
            // NOTE: no PreProcess or PostProcess events are fired in this case
            //response.KeepAlive = true;
            response.ContentEncoding = Encoding.UTF8;
            response.ContentType = "text/event-stream";
            response.Headers.Set(HttpResponseHeader.CacheControl, "no-cache, no-store, must-revalidate");
            response.Headers.Set(HttpResponseHeader.Pragma, "no-cache");

            // 2K padding for IE
            var padding = ":" + new String(' ', 2048) + "\n";
            byte[] paddingData = Encoding.UTF8.GetBytes(padding);
            response.OutputStream.Write(paddingData, 0, paddingData.Length);
            byte[] retryData = Encoding.UTF8.GetBytes("retry: 1000\n");
            response.OutputStream.Write(retryData, 0, retryData.Length);

            DateTime lastTimeStamp = DateTime.UtcNow;
            var lastId = context.Request.Headers.Get("Last-Event-ID");
            if (lastId == null || lastId == "")
            {
                var queryValues = HttpUtility.ParseQueryString(context.Request.Url.Query);
                lastId = queryValues.Get("lastEventId");

            }

            if (lastId != null && lastId != "")
            {
                double unixTimestamp = 0;
                double.TryParse(lastId, NumberStyles.Float | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                    out unixTimestamp);
                if (unixTimestamp != 0)
                {
                    lastTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                    lastTimeStamp.AddSeconds(Math.Round(unixTimestamp / 1000d));
                }
            }

            bool connected = true;
            var timeoutWatch = Stopwatch.StartNew();
            while (connected)
            {
                // dirty work around for signaling new event and 
                // avoiding locks on long socket timetout
                lock (sseEventToken)
                    Monitor.Wait(sseEventToken, 1000);
                // safely dequeue events
                List<SseEvent> bufferedData;
                do
                {
                    bufferedData = sseEventBuffer.FindAll(le => le != null && le.Timestamp.Ticks > lastTimeStamp.Ticks);
                    if (bufferedData.Count > 0)
                    {
                        foreach (SseEvent entry in bufferedData)
                        {
                            // send events
                            try
                            {
                                // The following throws an error on some mono-arm (Input string was not in the correct format)
                                // entry.Event.UnixTimestamp.ToString("R", CultureInfo.InvariantCulture)
                                byte[] data = Encoding.UTF8.GetBytes("id: " + entry.Event.UnixTimestamp.ToString().Replace(",", ".") +
                                                                     "\ndata: " + MigService.JsonSerialize(entry.Event) + "\n\n");
                                response.OutputStream.Write(data, 0, data.Length);
                                //response.OutputStream.Flush();
                                lastTimeStamp = entry.Timestamp;
                            }
                            catch (Exception e)
                            {
                                MigService.Log.Info(new MigEvent(this.GetName(), remoteAddress, "HTTP", request.HttpMethod,
                                    $"{response.StatusCode} {request.RawUrl} [ERROR: {e.Message}]"));
                                connected = false;
                                break;
                            }
                        }

                        Thread.Sleep(100);
                    }

                    // there might be new data after sending
                } while (connected && bufferedData.Count > 0);

                // check if the remote end point is still alive every 15 seconds or so
                if (timeoutWatch.Elapsed.TotalSeconds > 15)
                {
                    connected = connected && IsRemoteEndPointConnected(request.RemoteEndPoint);
                    timeoutWatch.Stop();
                    timeoutWatch = Stopwatch.StartNew();
                }
            }
        }

        #region Client Connection management

        private void SendResponseObject(HttpListenerContext context, object responseObject)
        {
            if (responseObject != null && responseObject.GetType().Equals(typeof(byte[])) == false)
            {
                string responseText = "";
                if (responseObject.GetType() == typeof(String))
                {
                    responseText = responseObject.ToString();
                }
                else
                {
                    responseText = MigService.JsonSerialize(responseObject);
                }
                // simple automatic json response type detection
                if (responseText.StartsWith("[") && responseText.EndsWith("]") || responseText.StartsWith("{") && responseText.EndsWith("}"))
                {
                    // send as JSON
                    context.Response.ContentType = "application/json";
                    context.Response.ContentEncoding = defaultWebFileEncoding;
                }
                WebServiceUtility.WriteStringToContext(context, responseText);
            }
            else
            {
                // Send as binary data
                WebServiceUtility.WriteBytesToContext(context, (byte[])responseObject);
            }
        }

        private void StartListener(IEnumerable<string> prefixes)
        {
            stopEvent.Reset();
            HttpListener listener = new HttpListener();
            if (servicePassword != "")
            {
                listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
            }
            foreach (string s in prefixes)
            {
                listener.Prefixes.Add(s);
            }
            listener.Start();
            HttpListenerCallbackState state = new HttpListenerCallbackState(listener);
            ThreadPool.QueueUserWorkItem(Listen, state);
        }

        private void StopListener()
        {
            stopEvent.Set();
        }

        private void Listen(object state)
        {
            HttpListenerCallbackState callbackState = (HttpListenerCallbackState)state;
            while (callbackState.Listener.IsListening)
            {
                callbackState.Listener.BeginGetContext(new AsyncCallback(ListenerCallback), callbackState);
                int n = WaitHandle.WaitAny(new WaitHandle[] { callbackState.ListenForNextRequest, stopEvent });
                if (n == 1)
                {
                    // stopEvent was signaled 
                    try
                    {
                        callbackState.Listener.Stop();
                    }
                    catch (Exception e)
                    {
                        MigService.Log.Error(e);
                    }
                    break;
                }
            }
        }

        private void ListenerCallback(IAsyncResult result)
        {
            HttpListenerCallbackState callbackState = (HttpListenerCallbackState)result.AsyncState;
            HttpListenerContext context = null;
            callbackState.ListenForNextRequest.Set();
            try
            {
                context = callbackState.Listener.EndGetContext(result);
            }
            catch (Exception ex)
            {
                MigService.Log.Error(ex);
            }
            //finally
            //{
            //    callbackState.ListenForNextRequest.Set();
            //}
            if (context == null)
                return;
            Worker(context);
        }

        private bool IsRemoteEndPointConnected(IPEndPoint ep)
        {
            bool isConnected = false;
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections(); 
            foreach (TcpConnectionInformation c in connections)
            {
                if (c.RemoteEndPoint.ToString() == ep.ToString())
                {
                    isConnected = !(c.State == TcpState.CloseWait);
                    break;
                }
            }
            return isConnected;
        }

        #endregion

        #region URL Aliases

        private void UrlAliasAdd(string alias)
        {
            if (!urlAliases.Contains(alias))
                urlAliases.Add(alias);
        }

        private string UrlAliasCheck(string url)
        {
            var alias = urlAliases.Find(a => a.StartsWith(url + ":"));
            if (alias != null)
                return alias.Substring(alias.IndexOf(":") + 1);
            else
                return url;
        }

        #endregion

        #region HTTP Caching Ignore List

        private void HttpCacheIgnoreAdd(string regExpression)
        {
            if (!httpCacheIgnoreList.Contains(regExpression))
                httpCacheIgnoreList.Add(regExpression);
        }

        private bool HttpCacheIgnoreCheck(string url)
        {
            bool ignore = false;
            for(int i = 0; i < httpCacheIgnoreList.Count; i++)
            {
                var expr = new Regex(httpCacheIgnoreList[i]);
                if (expr.IsMatch(url))
                {
                    ignore = true;
                    break;
                }
            }
            return ignore;
        }

        #endregion

        #region HTTP Files Management and Caching

        private WebFile GetWebFile(string file)
        {
            WebFile fileItem = new WebFile(), cachedItem = null;
            try
            {
                cachedItem = filesCache.Find(wfc => wfc.FilePath == file);
            }
            catch (Exception ex)
            {
                //TODO: sometimes the Find method fires an "object reference not set" error (who knows why???)
                MigService.Log.Error(ex);
                // clear possibly corrupted cache items
                ClearWebCache();
            }
            //
            if (cachedItem != null && (DateTime.Now - cachedItem.Timestamp).TotalSeconds < 600)
            {
                fileItem = cachedItem;
            }
            else
            {
                Encoding fileEncoding = DetectWebFileEncoding(file);  //TextFileEncodingDetector.DetectTextFileEncoding(file);
                if (fileEncoding == null)
                    fileEncoding = defaultWebFileEncoding;
                fileItem.Content = File.ReadAllText(file, fileEncoding);  //Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                fileItem.Encoding = fileEncoding;
                if (cachedItem != null)
                {
                    filesCache.Remove(cachedItem);
                }
            }
            return fileItem;
        }

        public void ClearWebCache()
        {
            filesCache.Clear();
        }

        private Encoding DetectWebFileEncoding(string filename)
        {
            Encoding enc = defaultWebFileEncoding;
            using (FileStream fs = File.OpenRead(filename))
            {
                ICharsetDetector cdet = new CharsetDetector();
                cdet.Feed(fs);
                cdet.DataEnd();
                if (cdet.Charset != null)
                {
                    //Console.WriteLine("Charset: {0}, confidence: {1}",
                    //     cdet.Charset, cdet.Confidence);
                    enc = Encoding.GetEncoding(cdet.Charset);
                }
                else
                {
                    //Console.WriteLine("Detection failed.");
                }
            }
            return enc;
        }

        private void UpdateWebFileCache(string file, string content, Encoding encoding)
        {
            var cachedItem = filesCache.Find(wfc => wfc.FilePath == file);
            if (cachedItem == null)
            {
                cachedItem = new WebFile();
                filesCache.Add(cachedItem);
            }
            cachedItem.FilePath = file;
            cachedItem.Content = content;
            cachedItem.Encoding = encoding;
            cachedItem.IsCached = true;
        }

        private string GetWebFilePath(string file)
        {
            string path = homePath;
            file = file.Replace(baseUrl, "");
            //
            var os = Environment.OSVersion;
            var platformId = os.Platform;
            //
            switch (platformId)
            {
            case PlatformID.Win32NT:
            case PlatformID.Win32S:
            case PlatformID.Win32Windows:
            case PlatformID.WinCE:
                path = Path.Combine(path, file.Replace("/", "\\").TrimStart('\\'));
                break;
            case PlatformID.Unix:
            case PlatformID.MacOSX:
            default:
                path = Path.Combine(path, file.TrimStart('/'));
                break;
            }
            return path;
        }

        #endregion

        #region Events

        protected virtual void OnPreProcessRequest(MigClientRequest request)
        {
            if (request != null && PreProcessRequest != null)
                PreProcessRequest(this, new ProcessRequestEventArgs(request));
        }

        protected virtual void OnPostProcessRequest(MigClientRequest request)
        {
            if (request != null && PostProcessRequest != null)
                PostProcessRequest(this, new ProcessRequestEventArgs(request));
        }

        #endregion

    }

}
