namespace AEWithMSI.Controllers
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Mvc;

    using AEWithMSI;

    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class HomeController : Controller
    {
        private static ClientCredential clientCredential;

        public ActionResult Index()
        {
#if DEBUG
            return this.RedirectToAction("AzureBlobEncryption");
#endif

            return this.View();
        }

        public async System.Threading.Tasks.Task<ActionResult> AlwaysEncrypted()
        {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

            try
            {
                using (var db = new DataContext())
                {
                    var firstRow = db.TestTable1.First();

                    firstRow.test1 = $"existing data1, utc now: {DateTime.UtcNow.ToLongTimeString()}";
                    firstRow.test2 = $"existing data2, utc now: {DateTime.UtcNow.ToLongTimeString()}";
                    await db.SaveChangesAsync();

                    this.ViewBag.Test1Updated = $"Column1 (Plain text) updated row: {firstRow.test1}";
                    this.ViewBag.Test2Updated = $"Column2 (Encrypted) updated row: {firstRow.test2}";

                    var allRowsExceptFirst = db.TestTable1.Where(t => t.id != firstRow.id);
                    db.TestTable1.RemoveRange(allRowsExceptFirst);
                    await db.SaveChangesAsync();

                    var newRow = new TestTable1
                    {
                        test1 = $"new data1, utc now: {DateTime.UtcNow.ToLongTimeString()}",
                        test2 = $"new data2, utc now: {DateTime.UtcNow.ToLongTimeString()}"
                    };

                    db.TestTable1.Add(newRow);
                    await db.SaveChangesAsync();

                    var lastRow = db.TestTable1.First(t => t.id != firstRow.id);

                    this.ViewBag.Test1Inserted = $"Column1 (Plain text) inserted row: {lastRow.test1}";
                    this.ViewBag.Test2Inserted = $"Column2 (Encrypted) inserted row: {lastRow.test2}";
                }
            }
            catch (Exception exp)
            {
                this.ViewBag.Error = $"Something went wrong: {exp.Message}";
            }

            this.ViewBag.Principal = azureServiceTokenProvider.PrincipalUsed != null ? $"Principal Used: {azureServiceTokenProvider.PrincipalUsed}" : string.Empty;

            return this.View();
        }

        public async static Task<string> GetToken(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            var result = await authContext.AcquireTokenAsync(resource, clientCredential);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to obtain the access token");
            }

            return result.AccessToken;
        }

        public static Stream ToStream(string text)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(text);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public async System.Threading.Tasks.Task<ActionResult> AzureBlobEncryption()
        {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

            try
            {
#if DEBUG
                var clientID = "";
                var clientSecret = "";
#else
                var keyVaultClient = new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

                var clientID = keyVaultClient.GetSecretAsync(ConfigurationManager.AppSettings["ADClientID"]).ConfigureAwait(false).GetAwaiter().GetResult().Value;
                var clientSecret = keyVaultClient.GetSecretAsync(ConfigurationManager.AppSettings["ADClientSecret"]).ConfigureAwait(false).GetAwaiter().GetResult().Value;
#endif
                var account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureStorageConnectionString"]);

                clientCredential = new ClientCredential(clientID, clientSecret);
                var client = account.CreateCloudBlobClient();

                var container = client.GetContainerReference(ConfigurationManager.AppSettings["AzureStorageContainer"]);
                container.CreateIfNotExists();

                // The Resolver object is used to interact with Key Vault for Azure Storage.
                // This is where the GetToken method from above is used.
                var cloudResolver = new KeyVaultKeyResolver(GetToken);

                // Retrieve the key that you created previously.
                // The IKey that is returned here is an RsaKey.
                // Remember that we used the names contosokeyvault and testrsakey1.
                var rsa = cloudResolver.ResolveKeyAsync(ConfigurationManager.AppSettings["BlobEncryptionKey"], CancellationToken.None).GetAwaiter().GetResult();
                
                // Now you simply use the RSA key to encrypt by setting it in the BlobEncryptionPolicy.
                var policy = new BlobEncryptionPolicy(rsa, null);
                var options = new BlobRequestOptions() { EncryptionPolicy = policy };

                // Reference a block blob.
                var blob = container.GetBlockBlobReference("EncryptedTextFile.txt");
                var testData = $"some test data 123, utc now: {DateTime.UtcNow.ToLongTimeString()}";

                // Upload using the UploadFromStream method.
                using (var stream = ToStream(testData))
                {
                    blob.UploadFromStream(stream, stream.Length, null, options, null);
                }

                this.ViewBag.Test1 = $"text stored in EncryptedTextFile.txt as blob with encryption: {testData}";

                var policy2 = new BlobEncryptionPolicy(null, cloudResolver);
                var options2 = new BlobRequestOptions() { EncryptionPolicy = policy2 };

                using (var stream = new MemoryStream())
                {
                    blob.DownloadToStream(stream);
                    stream.Position = 0;

                    var reader = new StreamReader(stream);
                    var text = reader.ReadToEnd();

                    this.ViewBag.Test2 = $"text read from EncryptedTextFile.txt without decryption: {text}";
                }

                // In this case, we will not pass a key and only pass the resolver because
                // this policy will only be used for downloading / decrypting.
                using (var stream = new MemoryStream())
                {
                    blob.DownloadToStream(stream, null, options2, null);
                    stream.Position = 0;

                    var reader = new StreamReader(stream);
                    var text = reader.ReadToEnd();

                    this.ViewBag.Test3 = $"text read from EncryptedTextFile.txt with decryption: {text}";
                }
            }
            catch (Exception exp)
            {
                this.ViewBag.Error = $"Something went wrong: {exp.Message}";
            }

            this.ViewBag.Principal = azureServiceTokenProvider.PrincipalUsed != null ? $"Principal Used: {azureServiceTokenProvider.PrincipalUsed}" : string.Empty;

            return this.View();
        }

        public ActionResult About()
        {
            this.ViewBag.Message = "Your application description page.";

            return this.View();
        }

        public ActionResult Contact()
        {
            this.ViewBag.Message = "Your contact page.";

            return this.View();
        }
    }
}