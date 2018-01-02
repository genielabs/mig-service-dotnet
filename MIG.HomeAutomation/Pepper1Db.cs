using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using System.Xml.XPath;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MIG.Interfaces.HomeAutomation
{
    public class Pepper1Db
    {
        private const string dbFilename = "p1db.xml";
        private const string additionalDbFilename = "p1db_custom.xml";
        private const string archiveFilename = "archive.zip";
        private const string tempFolder = "temp";
        private const string defaultPepper1Url = "https://genielabs.github.io/HomeGenie/_hg_files/zwave/pepper1_device_archive.zip";

        public bool DbExists
        {
            get
            {
                var dbFile = new FileInfo(GetDbFullPath(dbFilename));
                return dbFile.Exists;
            }
        }

        public bool Update(string pepper1Url = defaultPepper1Url)
        {
            ZipConstants.DefaultCodePage = System.Text.Encoding.UTF8.CodePage;

            // request archive from P1 db
            using (var client = new WebClient())
            {
                try
                {
                    MigService.Log.Debug("Downloading archive from {0}.", pepper1Url);
                    client.DownloadFile(pepper1Url, archiveFilename);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }

            // extract archive
            MigService.Log.Debug("Extracting archive from '{0}' to '{1}' folder.", archiveFilename, tempFolder);
            ExtractZipFile(archiveFilename, null, tempFolder);

            MigService.Log.Debug("Creating consolidated DB.");
            var p1db = new XDocument();
            var dbElement = new XElement("Devices");

            // for each xml file read it content and add to one file
            var files = Directory.GetFiles(tempFolder, "*.xml");
            foreach (var file in files)
            {
                try
                {
                    var fi = new FileInfo(file);
                    var xDoc = XElement.Load(fi.OpenText());
                    dbElement.Add(xDoc.RemoveAllNamespaces());
                }
                catch (Exception)
                {
                }
            }

            p1db.Add(dbElement);
            var dbFile = new FileInfo(GetDbFullPath(dbFilename));
            using (var writer = dbFile.CreateText())
            {
                p1db.Save(writer);
            }
            MigService.Log.Debug("DB saved: {0}.", dbFilename);
            return true;
        }

        /// <summary>
        /// Searches local pepper1 db for the specified device and returns an array of matched device infos in JSON.
        /// </summary>
        /// <returns>The device info.</returns>
        /// <param name="manufacturerId">Manufacturer identifier.</param>
        /// <param name="version">Version (in format appVersion.appSubVersion).</param>
        public string GetDeviceInfo(string manufacturerId, string version)
        {
            var res = GetDeviceInfoInDb(dbFilename, manufacturerId, version);
            // if no devices has been found in pepper1 db, we should try to find them in additional db
            if (res.Count == 0)
            {
                res = GetDeviceInfoInDb(additionalDbFilename, manufacturerId, version);
            }

            return JsonConvert.SerializeObject(res, Newtonsoft.Json.Formatting.Indented, new []{ new XmlNodeConverter() });
        }

        private List<XElement> GetDeviceInfoInDb(string filename, string manufacturerId, string version)
        {
            var res = new List<XElement>();
            var dbFile = new FileInfo(GetDbFullPath(filename));
            if (!dbFile.Exists)
                return res;
            XDocument db;
            using (var reader = dbFile.OpenText())
            {
                db = XDocument.Load(reader);
            }

            var mIdParts = manufacturerId.Split(new []{ ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (mIdParts.Length != 3)
                throw new ArgumentException(string.Format("Wrong manufacturerId ({0})", manufacturerId));

            var query = string.Format("deviceData/manufacturerId[@value=\"{0}\"] and deviceData/productType[@value=\"{1}\"] and deviceData/productId[@value=\"{2}\"]", mIdParts[0], mIdParts[1], mIdParts[2]);
            if (!string.IsNullOrEmpty(version))
            {
                var vParts = version.Split(new []{ '.' }, StringSplitOptions.RemoveEmptyEntries);
                query += string.Format(" and deviceData/appVersion[@value=\"{0}\"] and deviceData/appSubVersion[@value=\"{1}\"]", vParts[0], vParts[1]);
            }
            var baseQuery = string.Format("//ZWaveDevice[ {0} ]", query);
            res = db.XPathSelectElements(baseQuery).ToList();
            MigService.Log.Debug("Found {0} elements in {1} with query {2}", res.Count, filename, baseQuery);

            if (res.Count == 0)
            {
                // try to find generic device info without version information
                query = string.Format("deviceData/manufacturerId[@value=\"{0}\"] and deviceData/productType[@value=\"{1}\"] and deviceData/productId[@value=\"{2}\"]", mIdParts[0], mIdParts[1], mIdParts[2]);
                baseQuery = string.Format("//ZWaveDevice[ {0} ]", query);
                res = db.XPathSelectElements(baseQuery).ToList();
                MigService.Log.Debug("Found {0} elements in {1} with query {2}", res.Count, filename, baseQuery);
            }

            return res;
        }

        private static string GetDbFullPath(string file)
        {
            string assemblyFolder = Path.GetDirectoryName(typeof(Pepper1Db).Assembly.Location);
            string path = Path.Combine(assemblyFolder, file);
            return path;
        }

        private static void ExtractZipFile(string archiveFilenameIn, string password, string outFolder)
        {
            ZipFile zf = null;
            try
            {
                FileStream fs = File.OpenRead(archiveFilenameIn);
                zf = new ZipFile(fs);
                if (!String.IsNullOrEmpty(password))
                {
                    zf.Password = password;     // AES encrypted entries are handled automatically
                }
                foreach (ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile)
                    {
                        continue;           // Ignore directories
                    }
                    String entryFileName = zipEntry.Name;
                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    String fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
            finally
            {
                if (zf != null)
                {
                    zf.IsStreamOwner = true; // Makes close also shut the underlying stream
                    zf.Close(); // Ensure we release resources
                }
            }
        }
    }
}
