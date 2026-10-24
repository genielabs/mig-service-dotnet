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
using System.IO;
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
        public MultipartParser(byte[] data) { Parse(data, Encoding.UTF8); }
        public MultipartParser(byte[] data, Encoding encoding) { Parse(data, encoding); }
        private void Parse(byte[] data, Encoding encoding)
        {
            Success = false;
            ContentType = null;
            Filename = null;
            FileContents = null;
            if (data == null || encoding == null) return;

            byte[] lineEnd = { 13, 10 };
            byte[] headerEnd = { 13, 10, 13, 10 };
            int delimiterEnd = IndexOf(data, lineEnd, 0);
            if (delimiterEnd < 3 || data[0] != 45 || data[1] != 45) return;

            byte[] boundary = new byte[delimiterEnd + 2];
            boundary[0] = 13;
            boundary[1] = 10;
            Buffer.BlockCopy(data, 0, boundary, 2, delimiterEnd);
            int partStart = delimiterEnd + 2;
            var options = RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

            while (partStart < data.Length)
            {
                int headersEnd = IndexOf(data, headerEnd, partStart);
                if (headersEnd < 0) return;
                int end = IndexOfBoundary(data, boundary, partStart);
                if (end < 0 || headersEnd + headerEnd.Length > end) return;

                string headers = encoding.GetString(data, partStart, headersEnd - partStart);
                Match disposition = Regex.Match(headers,
                    @"^Content-Disposition:[ \t]*([^\r\n]*)", options);
                Match contentType = Regex.Match(headers,
                    @"^Content-Type:[ \t]*([^\r\n]*)", options);
                Match filename = Regex.Match(disposition.Value,
                    @"(?:^|;)[ \t]*filename[ \t]*=[ \t]*""([^""]*)""",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                if (disposition.Success && contentType.Success && filename.Success)
                {
                    int start = headersEnd + headerEnd.Length;
                    byte[] fileData = new byte[end - start];
                    Buffer.BlockCopy(data, start, fileData, 0, fileData.Length);
                    ContentType = contentType.Groups[1].Value.Trim();
                    Filename = filename.Groups[1].Value.Trim();
                    FileContents = fileData;
                    Success = true;
                    return;
                }

                int suffix = end + boundary.Length;
                if (data[suffix] == 45 && data[suffix + 1] == 45) return;
                partStart = suffix + 2;
            }
        }
        private int IndexOfBoundary(byte[] data, byte[] boundary, int start)
        {
            int index = IndexOf(data, boundary, start);
            while (index >= 0)
            {
                int suffix = index + boundary.Length;
                if (suffix + 1 < data.Length &&
                    ((data[suffix] == 13 && data[suffix + 1] == 10) ||
                     (data[suffix] == 45 && data[suffix + 1] == 45)))
                    return index;
                index = IndexOf(data, boundary, index + 1);
            }
            return -1;
        }
        private int IndexOf(byte[] data, byte[] value, int start)
        {
            if (data == null || value == null || value.Length == 0 || start < 0) return -1;
            for (int i = start; i <= data.Length - value.Length; i++)
            {
                int j = 0;
                while (j < value.Length && data[i + j] == value[j]) j++;
                if (j == value.Length) return i;
            }
            return -1;
        }
        public bool Success { get; private set; }
        public string ContentType { get; private set; }
        public string Filename { get; private set; }
        public byte[] FileContents { get; private set; }
    }
}
