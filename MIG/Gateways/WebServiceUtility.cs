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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MIG.Gateways
{
    public class WebServiceUtility
    {


        public static void WriteStringToContext(System.Net.HttpListenerContext context, string returnValue)
        {
            Encoding encoding = context.Response.ContentEncoding;
            if (encoding == null)
                encoding = Encoding.GetEncoding("ISO-8859-1");
            WriteBytesToContext(context, encoding.GetBytes(returnValue));
        }

        public static void WriteBytesToContext(System.Net.HttpListenerContext context, byte[] buffer)
        {
            try
            {
                context.Response.ContentLength64 = buffer.Length;
                System.IO.Stream output = context.Response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
            catch
            {
                // TODO: add error logging 
            }
        }


        // code adapted from
        // http://stackoverflow.com/questions/8466703/httplistener-and-file-upload

        public static String GetBoundary(String contentType)
        {
            if (contentType == null)
                return "";
            return /*"--" + */ contentType.Split(';')[1].Split('=')[1];
        }

        // from:
        // http://stackoverflow.com/questions/1080442/how-to-convert-an-stream-into-a-byte-in-c
        public static byte[] ReadToEnd(System.IO.Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition; 
                }
            }
        }

        public static string SaveFile(Stream input, string outputPath)
        {
            return SaveFile(ReadToEnd(input), outputPath);
        }

        public static string SaveFile(byte[] data, string outputPath)
        {
            var parser = new MultipartParser(data);
            if (parser.Success)
            {
                var fileName = parser.Filename;
                if (!String.IsNullOrWhiteSpace(outputPath) && Directory.Exists(outputPath))
                {
                    Array.ForEach(Path.GetInvalidFileNameChars(), c => fileName = fileName.Replace(c.ToString(), String.Empty));
                    outputPath = Path.Combine(outputPath, fileName);
                }
                File.WriteAllBytes(outputPath, parser.FileContents);
            }
            return outputPath;
        }

    }

    public class MultipartParser
    {
        public MultipartParser(byte[] data)
        {   
            this.Parse(data, Encoding.UTF8);
        }

        public MultipartParser(byte[] data, Encoding encoding)
        {
            this.Parse(data, encoding);
        }

        private void Parse(byte[] data, Encoding encoding)
        {
            this.Success = false;

            // Copy to a string for header parsing
            string content = encoding.GetString(data);

            // The first line should contain the delimiter
            int delimiterEndIndex = content.IndexOf("\r\n");

            if (delimiterEndIndex > -1)
            {
                string delimiter = content.Substring(0, content.IndexOf("\r\n"));

                // Look for Content-Type
                Regex re = new Regex(@"(?<=Content\-Type:)(.*?)(?=\r\n\r\n)");
                Match contentTypeMatch = re.Match(content);

                // Look for filename
                re = new Regex(@"(?<=filename\=\"")(.*?)(?=\"")");
                Match filenameMatch = re.Match(content);

                // Did we find the required values?
                if (contentTypeMatch.Success && filenameMatch.Success)
                {
                    // Set properties
                    this.ContentType = contentTypeMatch.Value.Trim();
                    this.Filename = filenameMatch.Value.Trim();

                    // Get the start & end indexes of the file contents
                    int startIndex = contentTypeMatch.Index + contentTypeMatch.Length + "\r\n\r\n".Length;

                    byte[] delimiterBytes = encoding.GetBytes("\r\n" + delimiter);
                    int endIndex = IndexOf(data, delimiterBytes, startIndex);

                    int contentLength = endIndex - startIndex;

                    // Extract the file contents from the byte array
                    byte[] fileData = new byte[contentLength];

                    Buffer.BlockCopy(data, startIndex, fileData, 0, contentLength);

                    this.FileContents = fileData;
                    this.Success = true;
                }
            }
        }

        private int IndexOf(byte[] searchWithin, byte[] serachFor, int startIndex)
        {
            int index = 0;
            int startPos = Array.IndexOf(searchWithin, serachFor[0], startIndex);

            if (startPos != -1)
            {
                while ((startPos + index) < searchWithin.Length)
                {
                    if (searchWithin[startPos + index] == serachFor[index])
                    {
                        index++;
                        if (index == serachFor.Length)
                        {
                            return startPos;
                        }
                    }
                    else
                    {
                        startPos = Array.IndexOf<byte>(searchWithin, serachFor[0], startPos + index);
                        if (startPos == -1)
                        {
                            return -1;
                        }
                        index = 0;
                    }
                }
            }

            return -1;
        }

        public bool Success
        {
            get;
            private set;
        }

        public string ContentType
        {
            get;
            private set;
        }

        public string Filename
        {
            get;
            private set;
        }

        public byte[] FileContents
        {
            get;
            private set;
        }
    }
}
