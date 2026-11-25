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
    var timed=BatchNatCollector.Run(routers,"","",true,false,1,1,root,CancellationToken.None,_=>{},(_,_,_)=>throw new TaskCanceledException("Simulated timeout"));
    if(timed.Failed!=3 || timed.Skipped!=0 || timed.Statuses.Any(s=>s.Status!="FAILED: TIMEOUT")) throw new Exception("Timeout classified as cancellation");
    var retry=BatchNatCollector.Run(new[]{"192.0.2.3"},"","",true,false,1,1,root,CancellationToken.None,_=>{},(_,_,_)=>new CollectionResult(),batch);
    if(retry.Complete!=2||retry.Partial!=1||retry.OfficeCount!=2||retry.Failed!=0)throw new Exception("Retry merge mismatch");
    string input=Path.Combine(root,"earlier.csv"),later=Path.Combine(root,"later.csv");
    string head="Timestamp;Sample;Router;SourceIP;SourcePort;DestinationIP;DestinationPort;Protocol\n";
    string row="2026-01-01 12:00:00;1;192.0.2.1;192.0.2.9;50001;198.51.100.23;443;tcp\n";
    File.WriteAllText(input,head+row+row+"2026-01-01 12:00:00;1;192.0.2.1;198.51.100.23;443;192.0.2.9;50001;TCP\n"+"2026-01-01 12:00:00;1;192.0.2.1;192.0.2.9;50001;198.51.100.23;0;TCP\n");
    File.WriteAllText(later,head+row+"2026-01-02 12:00:00;1;192.0.2.2;192.0.2.9;50002;198.51.100.23;443;TCP\n"+"2026-01-02 12:00:00;1;192.0.2.2;192.0.2.9;50003;198.51.100.23;443;UDP\n");
    var analysis=AccessAnalyzer.Analyze(new[]{input,later},"198.51.100.0/24",CancellationToken.None);
    if(analysis.Endpoints.Count!=2||analysis.Sources.Count!=3||analysis.Endpoints.Single(e=>e.Protocol=="TCP").Observations!="2"||analysis.Audit[0].ReverseOrOutside!="1"||analysis.Audit[0].Invalid!="1")throw new Exception("Access cleanup mismatch");
    AccessAnalyzer.Compare(analysis,AccessAnalyzer.Analyze(new[]{input},"198.51.100.0/24",CancellationToken.None),AccessAnalyzer.Analyze(new[]{later},"198.51.100.0/24",CancellationToken.None));
    if(analysis.Changes.Count!=2||analysis.Changes.Count(e=>e.Change=="NEW OBSERVATION")!=1)throw new Exception("Comparison mismatch");
    if(!File.Exists(Engine.SaveAccessReport(Path.Combine(root,"analysis"),analysis)))throw new Exception("Access export failed");
}
finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
Console.WriteLine("PASS: legacy NAT, auth, VPN, partial reports, central flow, TOP traffic, XLSX, DEMO and multi-router office batch");
