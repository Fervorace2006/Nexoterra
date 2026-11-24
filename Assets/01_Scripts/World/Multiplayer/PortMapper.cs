using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nexoterra.World
{
 // Minimal UPnP-IGD client: asks the home router to forward a UDP port to this PC and
 // reports the router's public IP, so a "direct IP" host can be reached from any network.
 // Talks only to the local router (SSDP multicast + SOAP on the LAN). Fails gracefully.
 public static class PortMapper
 {
  static string controlUrl,serviceType;
  static readonly HttpClient http=new HttpClient{Timeout=TimeSpan.FromSeconds(4)};

  public static string LocalIPv4()
  {
   try{using var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);s.Connect("10.255.255.255",1);return ((IPEndPoint)s.LocalEndPoint).Address.ToString();}
   catch{return Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(a=>a.AddressFamily==AddressFamily.InterNetwork)?.ToString()??"127.0.0.1";}
  }

  // Returns (forwarded, publicIp, message).
  public static async Task<(bool ok,string publicIp,string error)> MapUdp(int port)
  {
   try
   {
    if(!await Discover())return(false,null,"El router no respondió a UPnP.");
    string local=LocalIPv4();
    await Soap("AddPortMapping",
     "<NewRemoteHost></NewRemoteHost><NewExternalPort>"+port+"</NewExternalPort><NewProtocol>UDP</NewProtocol>"+
     "<NewInternalPort>"+port+"</NewInternalPort><NewInternalClient>"+local+"</NewInternalClient><NewEnabled>1</NewEnabled>"+
     "<NewPortMappingDescription>Nexoterra</NewPortMappingDescription><NewLeaseDuration>0</NewLeaseDuration>");
    var reply=await Soap("GetExternalIPAddress","");
    var ip=Regex.Match(reply,"<NewExternalIPAddress>([^<]*)</NewExternalIPAddress>").Groups[1].Value;
    return(true,string.IsNullOrEmpty(ip)?null:ip,null);
   }
   catch(Exception e){return(false,null,e.Message);}
  }

  public static async Task Unmap(int port)
  {
   try{if(controlUrl!=null)await Soap("DeletePortMapping","<NewRemoteHost></NewRemoteHost><NewExternalPort>"+port+"</NewExternalPort><NewProtocol>UDP</NewProtocol>");}catch{}
  }

  static async Task<bool> Discover()
  {
   if(controlUrl!=null)return true;
   using var udp=new UdpClient(AddressFamily.InterNetwork);
   var target=new IPEndPoint(IPAddress.Parse("239.255.255.250"),1900);
   foreach(var st in new[]{"urn:schemas-upnp-org:device:InternetGatewayDevice:1","urn:schemas-upnp-org:service:WANIPConnection:1","upnp:rootdevice"})
   {
    var msg=Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: "+st+"\r\n\r\n");
    await udp.SendAsync(msg,msg.Length,target);
   }
   var deadline=DateTime.UtcNow.AddSeconds(3);
   while(DateTime.UtcNow<deadline)
   {
    var receive=udp.ReceiveAsync();
    if(await Task.WhenAny(receive,Task.Delay(TimeSpan.FromSeconds(Math.Max(.1,(deadline-DateTime.UtcNow).TotalSeconds))))!=receive)break;
    var text=Encoding.ASCII.GetString(receive.Result.Buffer);
    var location=Regex.Match(text,@"(?im)^location:\s*(\S+)").Groups[1].Value;
    if(string.IsNullOrEmpty(location))continue;
    try
    {
     var xml=await http.GetStringAsync(location);
     foreach(var type in new[]{"urn:schemas-upnp-org:service:WANIPConnection:2","urn:schemas-upnp-org:service:WANIPConnection:1","urn:schemas-upnp-org:service:WANPPPConnection:1"})
     {
      var block=Regex.Match(xml,"<service>(?:(?!</service>).)*?"+Regex.Escape(type)+"(?:(?!</service>).)*?</service>",RegexOptions.Singleline).Value;
      var path=Regex.Match(block,"<controlURL>([^<]*)</controlURL>").Groups[1].Value;
      if(string.IsNullOrEmpty(path))continue;
      controlUrl=new Uri(new Uri(location),path).ToString();serviceType=type;return true;
     }
    }
    catch{}
   }
   return false;
  }

  static async Task<string> Soap(string action,string args)
  {
   string body="<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>"+
    "<u:"+action+" xmlns:u=\""+serviceType+"\">"+args+"</u:"+action+"></s:Body></s:Envelope>";
   var request=new HttpRequestMessage(HttpMethod.Post,controlUrl){Content=new StringContent(body,Encoding.UTF8,"text/xml")};
   request.Headers.Add("SOAPAction","\""+serviceType+"#"+action+"\"");
   var response=await http.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
   if(!response.IsSuccessStatusCode)throw new Exception("El router rechazó "+action+" ("+(int)response.StatusCode+")");
   return text;
  }
 }
}
