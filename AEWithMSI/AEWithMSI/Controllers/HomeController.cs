namespace AEWithMSI.Controllers
{
    using System;
    using System.Linq;
    using System.Web.Mvc;

    using AEWithMSI;

    using Microsoft.Azure.Services.AppAuthentication;

    public class HomeController : Controller
    {
        public async System.Threading.Tasks.Task<ActionResult> Index()
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