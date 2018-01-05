namespace AEWithMSI
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data.SqlClient;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Mvc;
    using System.Web.Optimization;
    using System.Web.Routing;

    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.SqlServer.Management.AlwaysEncrypted.AzureKeyVaultProvider;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class MvcApplication : System.Web.HttpApplication
    {
        private static ClientCredential clientCredential;

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

        private static void InitializeAzureKeyVaultProvider()
        {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

            var keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

            var clientID = keyVaultClient.GetSecretAsync(System.Configuration.ConfigurationManager.AppSettings["ADClientID"]).ConfigureAwait(false).GetAwaiter().GetResult();
            var clientSecret = keyVaultClient.GetSecretAsync(System.Configuration.ConfigurationManager.AppSettings["ADClientSecret"]).ConfigureAwait(false).GetAwaiter().GetResult();

            clientCredential = new ClientCredential(clientID.Value, clientSecret.Value);

            SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = 
                new SqlColumnEncryptionAzureKeyVaultProvider(GetToken);
            
            Dictionary<string, SqlColumnEncryptionKeyStoreProvider> providers =
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();

            providers.Add(SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider);

            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(providers);
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

#if !DEBUG
            InitializeAzureKeyVaultProvider();

            try
            {
                using (var db = new DataContext())
                {
                    db.Database.Initialize(true);
                }
            }
            catch (Exception e)
            {
                throw;
            }
#endif
        }
    }
}
