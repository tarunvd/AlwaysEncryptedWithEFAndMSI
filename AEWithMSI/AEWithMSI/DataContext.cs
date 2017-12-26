namespace AEWithMSI
{
    using System.Data.Entity;

    using AEWithMSI;

    public partial class DataContext : DbContext
    {
        public DataContext()
            : base("DataContext")
        {
        }

        public virtual DbSet<TestTable1> TestTable1 { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
        }

        //private static string EnsureAlwaysEncryptedConfiguration(string nameOrConnectionString)
        //{
        //    var connectionString = nameOrConnectionString;
        //    var providerName = string.Empty;

        //    if (!TreatAsConnectionString(nameOrConnectionString))
        //    {
        //        var connectionStringObject = System.Configuration.ConfigurationManager.ConnectionStrings[nameOrConnectionString];

        //        if (connectionStringObject != null)
        //        {
        //            connectionString = connectionStringObject.ConnectionString;
        //            providerName = connectionStringObject.ProviderName;
        //        }
        //    }
            
        //    var connStringBuilder = new SqlConnectionStringBuilder(connectionString)
        //                                {
        //                                    ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
        //                                };

        //    var efBuilder = new EntityConnectionStringBuilder
        //    {
        //        Metadata = "",
        //        Provider = providerName,
        //        ProviderConnectionString = connStringBuilder.ConnectionString
        //    };

        //    return efBuilder.ConnectionString;
        //}

        //// Determining if string is a connection string similar to System.Data.Entity.Internal.DbHelpers.TreatAsConnectionString()
        //private static bool TreatAsConnectionString(string nameOrConnectionString)
        //{
        //    return nameOrConnectionString.IndexOf('=') >= 0;
        //}
    }
}
