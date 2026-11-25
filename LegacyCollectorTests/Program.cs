using KeeneticObserver;
Engine.SetTargetSubnet(Engine.DemoSubnet);
Engine.CheckSelfTestFixtures();
Engine.CheckAuthSelfTestFixtures();
Engine.CheckPartialSelfTestFixtures();
Engine.CheckRemoteVpnSelfTestFixtures();
Engine.CheckOfficeEvidenceFixtures();
CentralFlowImporter.CheckSelfTestFixtures();
TrafficMonitor.CheckSelfTestFixtures();
TrafficAnalyticsExport.CheckSelfTestFixtures();
var result = Engine.Run(true,"","","",false,2,1,CancellationToken.None,_=>{});
if(result.All.Count!=10 || result.Target.Count!=4 || result.Routes.Count!=2 || !File.Exists(result.Excel)) throw new Exception("DEMO mismatch");
Engine.SetTargetSubnet("198.51.100.0/24");
if (!Engine.TargetIP("198.51.100.23") || Engine.TargetIP("203.0.113.23")) throw new Exception("Dynamic target failed");
var routers=BatchNatCollector.ParseAddresses("192.0.2.1\n192.0.2.2;192.0.2.1:443,192.0.2.3",true,false);
if(routers.Length!=3) throw new Exception("Batch address dedup failed");
string root=Path.Combine(Path.GetTempPath(),"ObserverBatchTest-"+Guid.NewGuid().ToString("N"));
try
{
    var batch=BatchNatCollector.Run(routers,"","",true,false,1,1,root,CancellationToken.None,_=>{},(addr,ct,log)=>{
        if(addr=="192.0.2.3") throw new IOException("Simulated failure");
        var r=new CollectionResult { Partial=addr=="192.0.2.2" };
        r.All.Add(new Entry { SourceIP="192.0.2.9", DestinationIP="198.51.100.23",Repeat="YES" });
        r.All.Add(new Entry { SourceIP="198.51.100.23",DestinationIP="192.0.2.9",TranslatedDestination="198.51.100.23" });
        return r;
    });
    if(batch.OfficeCount!=2 || batch.Complete!=1 || batch.Partial!=1 || batch.Failed!=1 || !File.Exists(batch.Excel)) throw new Exception("Batch coverage mismatch");
    var csv=File.ReadAllText(Path.Combine(batch.Folder,"07_Office_To_Connections.csv"));
    if(!csv.Contains("\"192.0.2.1\"") || !csv.Contains("\"192.0.2.2\"") || csv.Contains("\"192.0.2.3\"")) throw new Exception("Batch router identity mismatch");
    var statuses=File.ReadAllText(Path.Combine(batch.Folder,"00_Router_Status.csv"));
    if(!statuses.Contains("FAILED") || !statuses.Contains("PARTIAL")) throw new Exception("Batch status mismatch");
    using var cancel=new CancellationTokenSource();cancel.Cancel();
    var cancelled=BatchNatCollector.Run(routers,"","",true,false,1,1,root,cancel.Token,_=>{},(_,_,_)=>throw new Exception("Cancelled batch must not connect"));
    if(cancelled.Skipped!=3 || cancelled.OfficeCount!=0) throw new Exception("Batch cancellation mismatch");
}
finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
Console.WriteLine("PASS: legacy NAT, auth, VPN, partial reports, central flow, TOP traffic, XLSX, DEMO and multi-router office batch");
