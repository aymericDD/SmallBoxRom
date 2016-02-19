using HttpMultipartParser;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace WCFServiceWebRole1
{
    // REMARQUE : vous pouvez utiliser la commande Renommer du menu Refactoriser pour changer le nom de classe "Service1" dans le code, le fichier svc et le fichier de configuration.
    // REMARQUE : pour lancer le client test WCF afin de tester ce service, sélectionnez Service1.svc ou Service1.svc.cs dans l'Explorateur de solutions et démarrez le débogage.
    public class Service1 : IService1
    {
        protected CloudBlobContainer container;

        public Service1()
        {
            //Connexion au compte et au blob

            CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            this.container = blobClient.GetContainerReference("smallblob");
        }



        public Stream downloadFile(string folder, string fileName)
        {
            CloudBlobDirectory blobFolder = this.container.GetDirectoryReference(folder);
            CloudBlockBlob blobFile = blobFolder.GetBlockBlobReference(fileName);

            Stream returnStream = new MemoryStream();

            blobFile.DownloadToStream(returnStream);

            returnStream.Seek(0, SeekOrigin.Begin);

            WebOperationContext.Current.OutgoingResponse.Headers["Content-Disposition"] = "attachment; filename=" + blobFile.Uri.Segments.Last();
         
            WebOperationContext.Current.OutgoingResponse.ContentType = blobFile.Properties.ContentType;

            return returnStream;
        }

        public List<string> listfiles(string folderName)
        {
            CloudBlobDirectory dir = (CloudBlobDirectory)container.GetDirectoryReference(folderName);

            IEnumerable<IListBlobItem> list = dir.ListBlobs();
            var foldersName = list.Where(b => b as CloudBlockBlob != null);

            List<string> stringfoldersName = foldersName.Select(f => (f as CloudBlockBlob).Name).ToList();

            return stringfoldersName;
        }

        public List<string> listfolders()
        {
            IEnumerable<IListBlobItem> blobs = container.ListBlobs();
            List<string> folders = blobs.Select(f => (f as CloudBlobDirectory).Prefix).ToList();
            return folders;
        }

        public Boolean uploadFile(string folderName, Stream data)
        {
            try
            {
                CloudBlobDirectory rootDirectory = container.GetDirectoryReference(folderName);

                var parser = new MultipartFormDataParser(data, Encoding.UTF8);

                Stream content = parser.Files.First().Data;

                string fileName = parser.Files.First().FileName;

                if (parser.Files.First().ContentType.Equals("application/x-zip-compressed"))
                {
                    string tempFolder = Path.GetTempPath();
                    string tempZipFile = tempFolder + "/" + fileName;

                    using (FileStream zipStreamFile = new FileStream(tempZipFile, FileMode.Create))
                    {
                        using (var streamReader = new MemoryStream())
                        {
                            content.CopyTo(streamReader);
                            zipStreamFile.Write(streamReader.ToArray(), 0, streamReader.ToArray().Length);
                        }
                    }

                    string tempExtractedFile = tempFolder + "/" + fileName.Replace(".zip", "");

                    ZipFile.ExtractToDirectory(tempZipFile, tempExtractedFile);

                    CloudBlobDirectory blobDirectory = rootDirectory.GetDirectoryReference(fileName.Replace(".zip", ""));

                    foreach (var file in Directory.GetFiles(tempExtractedFile))
                    {
                        blobDirectory.GetBlockBlobReference(Path.GetFileName(file)).UploadFromFile(file, FileMode.Open);
                    }

                } else
                {
                    rootDirectory.GetBlockBlobReference(folderName + "/" + fileName).UploadFromStream(content);
                }

                content.Close();
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public string zipFolder(string folderName)
        {
            throw new NotImplementedException();
        }
    }
}
