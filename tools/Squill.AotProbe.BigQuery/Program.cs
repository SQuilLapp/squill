using Google.Cloud.BigQuery.V2;

// Compile/publish-only probe: no Google Cloud project available; the client is built with an anonymous endpoint and fails on the call.
try
{
    var client = new BigQueryClientBuilder { ProjectId = "none", BaseUri = "http://127.0.0.1:1/", Credential = null }.Build();
    var q = client.ExecuteQuery("SELECT 1", null);
    Console.WriteLine(q.TotalRows);
}
catch (Exception e) { Console.WriteLine(e.GetType().FullName + ": " + e.Message); }
Console.WriteLine("done");
