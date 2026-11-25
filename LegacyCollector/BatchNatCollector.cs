using System.Globalization;

namespace KeeneticObserver;

internal sealed class BatchRouterStatus
{
    public string Router="", Status="", OfficeRows="", AllRows="", ReportFolder="";
}
internal sealed record BatchNatResult(string Folder,string Excel,int OfficeCount,int Complete,int Partial,int Failed,int Skipped,List<Entry> Rows,BatchRouterStatus[] Statuses,string Target);

internal static class BatchNatCollector
{
    internal static string[] Split(string input) => (input ?? "").Split(new[]{' ','\r','\n','\t',',',';'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
    internal static string[] ParseAddresses(string input,bool https,bool vpn)
    {
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addresses=new List<string>();
        foreach(string value in Split(input))
        {
            var uri=Engine.ParseRouterUri(value,https);
            if(vpn) RemoteVpn.RequirePrivateTarget(uri);
            string normalized=uri.IsDefaultPort ? uri.Host : uri.Host+":"+uri.Port;
            if(seen.Add(normalized)) addresses.Add(normalized);
        }
        if(addresses.Count==0 || addresses.Count>100) throw new ArgumentException("Enter 1–100 distinct router addresses.");
        return addresses.ToArray();
    }

    // The target subnet is set once by the modal collector before workers start.
    // Engine's target is read-only during this operation; each run has its own HTTP session and report folder.
    internal static BatchNatResult Run(string[] routers,string user,string secret,bool https,bool vpn,int samples,int interval,
        string root,CancellationToken token,Action<string> log,
        Func<string,CancellationToken,Action<string>,CollectionResult>? collect=null,BatchNatResult? prior=null)
    {
        if(prior!=null && prior.Target!=Engine.TargetSubnet)throw new ArgumentException("Retry target differs from previous batch.");
        collect ??= (addr,ct,report)=>Engine.Run(false,addr,user,secret,https,samples,interval,ct,report,vpn);
        var statuses=new BatchRouterStatus[routers.Length];
        var rows=new List<Entry>?[routers.Length];
        Parallel.For(0,routers.Length,new ParallelOptions { MaxDegreeOfParallelism=3 },i=>
        {
            string addr=routers[i];
            var status=new BatchRouterStatus { Router=addr, Status="NOT_STARTED", OfficeRows="0", AllRows="0" };
            statuses[i]=status;
            if(token.IsCancellationRequested) return;
            try
            {
                log("["+addr+"] START");
                var result=collect(addr,token,s=>log("["+addr+"] "+s));
                // Never include reverse or translated-only matches in the office-destination report.
                var target=result.All.Where(e=>Engine.TargetIP(e.DestinationIP)).ToList();
                foreach(var e in target) { e.Router=addr; e.TargetMatch="YES"; }
                rows[i]=target;
                status.Status=result.Partial ? "PARTIAL" : "COMPLETE";
                status.OfficeRows=target.Count.ToString(CultureInfo.InvariantCulture);
                status.AllRows=result.All.Count.ToString(CultureInfo.InvariantCulture);
                status.ReportFolder=result.Folder;
                log("["+addr+"] "+status.Status+" | OFFICE "+status.OfficeRows);
            }
            catch(Exception ex) { string kind=Engine.Classify(ex,token);status.Status=kind=="STOPPED"?"CANCELLED":"FAILED: "+kind; log("["+addr+"] "+status.Status); }
        });
        var merged=rows.Where(x=>x!=null).SelectMany(x=>x!).ToList();
        if(prior!=null)
        {
            // Keep earlier observations including partial results; update status only for attempted routers.
            merged=prior.Rows.Concat(merged).ToList();
            var updated=prior.Statuses.ToDictionary(s=>s.Router,StringComparer.OrdinalIgnoreCase);
            foreach(var s in statuses)
            {
                if(s.Status=="NOT_STARTED"&&updated.ContainsKey(s.Router))continue;
                if(updated.TryGetValue(s.Router,out var old))
                {
                    s.OfficeRows=merged.Count(e=>e.Router==s.Router).ToString(CultureInfo.InvariantCulture);
                    s.AllRows=(int.Parse(old.AllRows,CultureInfo.InvariantCulture)+int.Parse(s.AllRows,CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
                    if(string.IsNullOrEmpty(s.ReportFolder))s.ReportFolder=old.ReportFolder;
                }
                updated[s.Router]=s;
            }
            statuses=updated.Values.ToArray();
        }
        // Preserve the original per-router repeat indicators; never combine identical local IPs across sites.
        string folder=Path.Combine(root,"Batch_NAT_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        string excel=Engine.SaveBatchOfficeReport(folder,merged,statuses);
        return new BatchNatResult(folder,excel,merged.Count,statuses.Count(s=>s.Status=="COMPLETE"),statuses.Count(s=>s.Status=="PARTIAL"),statuses.Count(s=>s.Status.StartsWith("FAILED")),statuses.Count(s=>s.Status is "NOT_STARTED" or "CANCELLED"),merged,statuses,Engine.TargetSubnet);
    }
}
