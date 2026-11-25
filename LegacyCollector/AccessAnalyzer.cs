using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace KeeneticObserver;

internal sealed class AccessEndpoint
{
    public string IP="", Port="", Protocol="", Observations="", Sources="", Routers="", FirstSeen="", LastSeen="";
    public string TcpStatus="", CheckedAt="", CheckDetail="", ServiceHint="", Review="";
    public string Key => IP+":"+Port+"/"+Protocol;
}
internal sealed class AccessSource
{
    public string Router="", SourceIP="", SourceName="", SourceMAC="", DestinationIP="", DestinationPort="", Protocol="", Observations="", FirstSeen="", LastSeen="";
    public string Key => string.Join("|",Router,SourceIP,DestinationIP,DestinationPort,Protocol);
}
internal sealed class AccessAudit
{
    public string File="", Result="", Rows="", Accepted="", Duplicate="", ReverseOrOutside="", Invalid="", Unsupported="";
}
internal sealed class AccessDelta
{
    public string IP="", Port="", Protocol="", Earlier="", Later="", Change="";
}
internal sealed class AccessSourceDelta
{
    public string Router="", SourceIP="", DestinationIP="", DestinationPort="", Protocol="", Earlier="", Later="", Change="";
}
internal sealed class AccessAnalysis
{
    public string Target="";
    public List<AccessEndpoint> Endpoints=new();
    public List<AccessSource> Sources=new();
    public List<AccessAudit> Audit=new();
    public List<AccessDelta> Changes=new();
    public List<AccessSourceDelta> SourceChanges=new();
}
internal static class AccessAnalyzer
{
    internal static List<Dictionary<string,string>> ReadCsv(string path)
    {
        if(new FileInfo(path).Length>100_000_000) throw new InvalidDataException("CSV exceeds 100 MB.");
        using var stream=new StreamReader(path,Encoding.UTF8,true);
        string head=stream.ReadLine()??"";
        if(head.Length==0) throw new InvalidDataException("Empty CSV.");
        using var parser=new TextFieldParser(path,Encoding.UTF8,true) { HasFieldsEnclosedInQuotes=true, TrimWhiteSpace=false };
        parser.SetDelimiters(head.Count(c=>c==';')>head.Count(c=>c==',')?";":",");
        var headers=parser.ReadFields()??Array.Empty<string>();
        headers=headers.Select(h=>h.Trim().TrimStart('\uFEFF')).ToArray();
        if(headers.Length==0 || headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=headers.Length)
            throw new InvalidDataException("Invalid or duplicate CSV headers.");
        var rows=new List<Dictionary<string,string>>();
        while(!parser.EndOfData)
        {
            var values=parser.ReadFields()??Array.Empty<string>();
            if(values.Length!=headers.Length) throw new InvalidDataException("CSV column count mismatch.");
            var row=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<headers.Length;i++) row.Add(headers[i],values[i]);
            rows.Add(row);
        }
        // Validate headers even for an empty data file.
        if(rows.Count==0) rows.Add(headers.ToDictionary(h=>h,_=>"",StringComparer.OrdinalIgnoreCase));
        return rows;
    }
    private static string V(Dictionary<string,string> row,string field)=>row.TryGetValue(field,out var v)?v.Trim():"";
    private static bool IPv4(string s,out IPAddress ip)=>IPAddress.TryParse(s,out ip!) && ip.AddressFamily==AddressFamily.InterNetwork && ip.ToString()==s;
    private static byte[] Network(string target)
    {
        var p=target.Trim().Split('/');
        if(p.Length!=2 || p[1]!="24" || !IPv4(p[0],out var ip) || ip.GetAddressBytes()[3]!=0) throw new ArgumentException("Enter a canonical IPv4 /24.");
        return ip.GetAddressBytes();
    }
    private static bool InNetwork(string s,byte[] network)=>IPv4(s,out var ip)&&ip.GetAddressBytes().Take(3).SequenceEqual(network.Take(3));
    private static string Hint(string proto,int port)=> (proto,port) switch
    {
        ("UDP",53) or ("TCP",53)=>"DNS (possible)",
        ("TCP",80) or ("TCP",443) or ("TCP",8080)=>"Web (possible)",
        ("TCP",445)=>"SMB (possible)",
        ("TCP",3389) or ("UDP",3389)=>"RDP (possible)",
        ("TCP",5985)=>"WinRM (possible)",
        ("UDP",3478)=>"STUN/TURN (possible)",
        ("UDP",514) or ("UDP",5514)=>"Syslog (possible)",
        _=>"UNKNOWN"
    };
    private static string Earliest(IEnumerable<string> times)=>times.Where(t=>t.Length>0).OrderBy(t=>t,StringComparer.Ordinal).FirstOrDefault()??"";
    private static string Latest(IEnumerable<string> times)=>times.Where(t=>t.Length>0).OrderByDescending(t=>t,StringComparer.Ordinal).FirstOrDefault()??"";
    private static string Count(int n)=>n.ToString(CultureInfo.InvariantCulture);
    internal static AccessAnalysis Analyze(IEnumerable<string> files,string target,CancellationToken token)
    {
        var network=Network(target);
        var result=new AccessAnalysis { Target=target.Trim() };
        var hashes=new HashSet<string>(StringComparer.Ordinal);
        var identities=new HashSet<string>(StringComparer.Ordinal);
        var accepted=new List<Dictionary<string,string>>();
        foreach(var path in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            var audit=new AccessAudit{File=path};result.Audit.Add(audit);
            int total=0,good=0,dup=0,outside=0,invalid=0,unsupported=0;
            try
            {
                if(new FileInfo(path).Length>100_000_000) throw new InvalidDataException("CSV exceeds 100 MB.");
                using(var input=File.OpenRead(path))
                {
                    var hash=Convert.ToHexString(SHA256.HashData(input));
                    if(!hashes.Add(hash)){audit.Result="DUPLICATE FILE";continue;}
                }
                var rows=ReadCsv(path);
                string[] required={"Timestamp","Sample","Router","SourceIP","DestinationIP","DestinationPort","Protocol"};
                if(required.Any(c=>!rows[0].ContainsKey(c))) throw new InvalidDataException("Required NAT columns missing.");
                foreach(var row in rows)
                {
                    token.ThrowIfCancellationRequested();
                    if(row.Values.All(string.IsNullOrWhiteSpace))continue;
                    total++;
                    string source=V(row,"SourceIP"),dest=V(row,"DestinationIP"),proto=V(row,"Protocol").ToUpperInvariant();
                    if(!IPv4(source,out _)||!IPv4(dest,out _)||V(row,"Router").Length==0||V(row,"Timestamp").Length==0){invalid++;continue;}
                    // Original endpoints only: reject reverse-only, office-internal, translated-only and outside-subnet rows.
                    if(!InNetwork(dest,network)||InNetwork(source,network)){outside++;continue;}
                    if(proto!="TCP"&&proto!="UDP"){unsupported++;continue;}
                    if(!int.TryParse(V(row,"DestinationPort"),NumberStyles.None,CultureInfo.InvariantCulture,out int port)||port<1||port>65535){invalid++;continue;}
                    row["DestinationPort"]=Count(port);row["Protocol"]=proto;
                    // Reimports of the same observation (including overlapping cumulative retry exports) are counted once.
                    string identity=System.Text.Json.JsonSerializer.Serialize(new[]{"Timestamp","Sample","Router","SourceIP","SourcePort","DestinationIP","DestinationPort","Protocol","TranslatedSource","TranslatedDestination","TranslatedSourcePort","TranslatedDestinationPort","EvidenceSource"}.Select(k=>V(row,k)));
                    if(!identities.Add(identity)){dup++;continue;}
                    accepted.Add(row);good++;
                }
                audit.Result="IMPORTED";
            }
            catch(OperationCanceledException){throw;}
            catch(Exception ex){audit.Result="ERROR: "+ex.GetType().Name+" "+ex.Message;}
            finally{audit.Rows=Count(total);audit.Accepted=Count(good);audit.Duplicate=Count(dup);audit.ReverseOrOutside=Count(outside);audit.Invalid=Count(invalid);audit.Unsupported=Count(unsupported);}
        }
        foreach(var g in accepted.GroupBy(r=>(IP:V(r,"DestinationIP"),Port:V(r,"DestinationPort"),Protocol:V(r,"Protocol"))))
        {
            var hint=Hint(g.Key.Protocol,int.Parse(g.Key.Port,CultureInfo.InvariantCulture));
            result.Endpoints.Add(new AccessEndpoint{IP=g.Key.IP,Port=g.Key.Port,Protocol=g.Key.Protocol,Observations=Count(g.Count()),
                Sources=Count(g.Select(r=>(V(r,"Router"),V(r,"SourceIP"))).Distinct().Count()),Routers=Count(g.Select(r=>V(r,"Router")).Distinct().Count()),
                FirstSeen=Earliest(g.Select(r=>V(r,"Timestamp"))),LastSeen=Latest(g.Select(r=>V(r,"Timestamp"))),
                TcpStatus=g.Key.Protocol=="TCP"?"NOT CHECKED":"UDP OBSERVED",ServiceHint=hint,
                Review=hint=="UNKNOWN"?"REVIEW UNKNOWN SERVICE":"VERIFY SERVICE OWNER"});
        }
        foreach(var g in accepted.GroupBy(r=>(Router:V(r,"Router"),Source:V(r,"SourceIP"),IP:V(r,"DestinationIP"),Port:V(r,"DestinationPort"),Protocol:V(r,"Protocol"))))
            result.Sources.Add(new AccessSource{Router=g.Key.Router,SourceIP=g.Key.Source,SourceName=string.Join(" | ",g.Select(r=>V(r,"SourceName")).Where(v=>v.Length>0).Distinct()),SourceMAC=string.Join(" | ",g.Select(r=>V(r,"SourceMAC")).Where(v=>v.Length>0).Distinct()),DestinationIP=g.Key.IP,DestinationPort=g.Key.Port,Protocol=g.Key.Protocol,Observations=Count(g.Count()),FirstSeen=Earliest(g.Select(r=>V(r,"Timestamp"))),LastSeen=Latest(g.Select(r=>V(r,"Timestamp")))});
        result.Endpoints=result.Endpoints.OrderBy(r=>IPNumber(r.IP)).ThenBy(r=>int.Parse(r.Port)).ThenBy(r=>r.Protocol).ToList();
        result.Sources=result.Sources.OrderBy(r=>r.Router).ThenBy(r=>IPNumber(r.SourceIP)).ThenBy(r=>IPNumber(r.DestinationIP)).ThenBy(r=>int.Parse(r.DestinationPort)).ThenBy(r=>r.Protocol).ToList();
        return result;
    }
    private static uint IPNumber(string ip)=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(IPAddress.Parse(ip).GetAddressBytes());
    private static bool Private(IPAddress ip)=>RemoteVpn.IsPrivateTarget(new UriBuilder("http",ip.ToString()).Uri);
    internal static async Task CheckTcp(AccessAnalysis analysis,CancellationToken token,Action<string> progress)
    {
        var network=Network(analysis.Target);
        var tcp=analysis.Endpoints.Where(e=>e.Protocol=="TCP").ToArray();
        if(tcp.Any(e=>!InNetwork(e.IP,network)||!IPv4(e.IP,out var ip)||!Private(ip))) throw new InvalidOperationException("TCP validation is limited to observed private IPv4 destinations.");
        await Parallel.ForEachAsync(tcp,new ParallelOptions{MaxDegreeOfParallelism=6,CancellationToken=token},async (e,ct)=>{
            using var limit=CancellationTokenSource.CreateLinkedTokenSource(ct);limit.CancelAfter(TimeSpan.FromSeconds(4));
            using var socket=new TcpClient(AddressFamily.InterNetwork);
            string status,detail="";
            try{await socket.ConnectAsync(IPAddress.Parse(e.IP),int.Parse(e.Port,CultureInfo.InvariantCulture),limit.Token);status="TCP OPEN";}
            catch(OperationCanceledException) when(!ct.IsCancellationRequested){status="TCP TIMEOUT";}
            catch(SocketException ex){status=ex.SocketErrorCode==SocketError.ConnectionRefused?"TCP REFUSED":ex.SocketErrorCode==SocketError.TimedOut?"TCP TIMEOUT":"TCP ERROR";detail=ex.SocketErrorCode.ToString();}
            e.TcpStatus=status;e.CheckDetail=detail;e.CheckedAt=DateTimeOffset.Now.ToString("O",CultureInfo.InvariantCulture);
            progress(e.Key+" | "+status);
        });
    }
    internal static void Compare(AccessAnalysis combined,AccessAnalysis earlier,AccessAnalysis later)
    {
        if(combined.Target!=earlier.Target||combined.Target!=later.Target)throw new ArgumentException("Targets must match.");
        var a=earlier.Endpoints.ToDictionary(r=>r.Key);var b=later.Endpoints.ToDictionary(r=>r.Key);
        combined.Changes=a.Keys.Union(b.Keys).OrderBy(k=>k,StringComparer.Ordinal).Select(k=>{
            a.TryGetValue(k,out var old);b.TryGetValue(k,out var now);var e=now??old!;
            return new AccessDelta{IP=e.IP,Port=e.Port,Protocol=e.Protocol,Earlier=old?.Observations??"0",Later=now?.Observations??"0",Change=old==null?"NEW OBSERVATION":now==null?"NOT OBSERVED LATER":"OBSERVED BOTH"};
        }).ToList();
        var sa=earlier.Sources.ToDictionary(r=>r.Key);var sb=later.Sources.ToDictionary(r=>r.Key);
        combined.SourceChanges=sa.Keys.Union(sb.Keys).OrderBy(k=>k,StringComparer.Ordinal).Select(k=>{
            sa.TryGetValue(k,out var old);sb.TryGetValue(k,out var now);var e=now??old!;
            return new AccessSourceDelta{Router=e.Router,SourceIP=e.SourceIP,DestinationIP=e.DestinationIP,DestinationPort=e.DestinationPort,Protocol=e.Protocol,Earlier=old?.Observations??"0",Later=now?.Observations??"0",Change=old==null?"NEW OBSERVATION":now==null?"NOT OBSERVED LATER":"OBSERVED BOTH"};
        }).ToList();
    }
}
