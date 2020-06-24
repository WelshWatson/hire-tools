using System;
using System.Collections.Generic;
using System.Linq;
using SDK;
using SDK.Extensions;
using SDK.IO.Socket;
using SDK.IO.Logging;
using SDK.IO.Tunnel;
using SDK.Serialisation;
using Fabric.Configuration;
using Fabric.Extensions;
using Fabric.TapTun;
using PacketDotNet;
using PacketDotNet.Utils;

namespace Fabric
{
    public class Test : ITest
    {
        public Test()
        {
            Author = "";
            Copyright = "";
            Compatibility = new List<PlatformType> { PlatformType.Windows, PlatformType.Mono, PlatformType.MacOSX };
            Description = "";
            IconUrl = new Uri("https://example.org/favicon.ico");
            Id = Guid.Parse("40df0f9a-efe7-44ee-821d-4070c9500600");
            Languages = new List<string> { "en" };
            License = "";
            LicenseUrl = null;
            Owner = "";
            Priority = 4096;
            ProjectUrl = "https://example.org/";
            Tags = new List<string> { "" };
            Title = "example\\test";
            Version = Version.Parse("1.0.0");
        }

        public string Author { get; }
        public string Copyright { get; }
        public List<PlatformType> Compatibility { get; }
        public string Description { get; }
        public Uri IconUrl { get; }
        public Guid Id { get; }
        public bool IsEnabled { get; set; }
        public bool IsFaulted { get; set; }
        public List<string> Languages { get; }
        public string License { get; set; }
        public Uri LicenseUrl { get; }
        public string Owner { get; }
        public ushort Priority { get; }
        public string ProjectUrl { get; }
        public string State => $"rewrites={_intercepts} discards={_discards}";
        public List<string> Tags { get; }
        public string Title { get; }
        public Version Version { get; }

        public AuthorisePeerDelegate AuthorisePeer { get; set; }
        public DisablePeerDelegate DisablePeer { get; set; }
        public EnablePeerDelegate EnablePeer { get; set; }
        public Func<List<FabricPeer>> FabricPeers { get; set; }
        public Func<Profile> Profile { get; set; }
        public RemovePeerDelegate RemovePeer { get; set; }
        public SendBroadcastDelegate SendBroadcast { get; set; }
        public SendDataDelegate SendData { get; set; }
        public SendMessageDelegate SendMessage { get; set; }
        public WriteTapDelegate WriteTap { get; set; }

        public void OnInit(string license = "")
        {
        }

        public bool OnStart(IVirtualNetworkInterface virtualNetworkInterface)
        {
            _vni = virtualNetworkInterface;
            return OnReload();
        }
    
        public void OnStop()
        {
        }

        public bool OnReload(string license = "")
        {
            IsEnabled = Profile().TestEnabled;
            return true;
        }

        public bool OnDataFromTap(ITapData frame)
        {
            if (_vni == null) return false;

            var ethernetPacket = new EthernetPacket(new ByteArraySegment(frame.Data, 0, frame.Length));
            if (ethernetPacket.Type == EthernetType.Arp)
            {
                var arpFrame = ethernetPacket.Extract<ArpPacket>();

                if (IsEnabled)
                {
                    if (arpFrame.Operation != ArpOperation.Request) return false;
                    if (arpFrame.Operation == ArpOperation.Request)
                    {
                        var inject = false;
                        var originalSenderMac = arpFrame.SenderHardwareAddress;
                        var originalSenderAddr = arpFrame.SenderProtocolAddress;

                        if (Profile().FakeDefaultRoute == null ||
                            Profile().FakeDefaultRoute.Equals(Constants.DefaultPhysicalAddress))
                        {
                            Profile().FakeDefaultRoute = PhysicalAddressExtensions.GenerateRandomMac();
                            Profile().Save();
                        }

                        if (arpFrame.TargetProtocolAddress.Equals(Profile().VirtualNetwork.LastUsable))
                        {
                            arpFrame.SenderHardwareAddress = Profile().FakeDefaultRoute;
                            arpFrame.SenderProtocolAddress = Profile().VirtualNetwork.LastUsable;
                            inject = true;
                        }

                        var peer = FabricPeers().FirstOrDefault(n => n.VirtualAddress != null && n.VirtualAddress.Equals(arpFrame.TargetProtocolAddress));
                        if (peer != null)
                        {
                            arpFrame.SenderHardwareAddress = peer.MacAddress;
                            arpFrame.SenderProtocolAddress = peer.VirtualAddress;
                            inject = true;
                        }

                        if (inject)
                        {
                            unchecked
                            {
                                _intercepts++;
                            }

                            arpFrame.Operation = ArpOperation.Response;
                            arpFrame.TargetHardwareAddress = originalSenderMac;
                            arpFrame.TargetProtocolAddress = originalSenderAddr;
                            ethernetPacket.SourceHardwareAddress = arpFrame.SenderHardwareAddress;
                            ethernetPacket.DestinationHardwareAddress = arpFrame.TargetHardwareAddress;

                            WriteTap(ethernetPacket.Bytes, ethernetPacket.Bytes.Length);

                            return false;
                        }
                    }
                }

                var isBroadcast = ethernetPacket.DestinationHardwareAddress.ToString().Equals("FFFFFFFFFFFF");
                var isMulticast = ethernetPacket.DestinationHardwareAddress.ToString().StartsWith("01005E");

                if (isBroadcast || isMulticast)
                {
                    SendBroadcast(frame.Data, frame.Length);
                    return false;
                }

                var target = FabricPeers().FirstOrDefault(n => n.MacAddress != null && n.MacAddress.Equals(arpFrame.TargetHardwareAddress));
                if (target != null)
                {
                    SendData(target.Peer, frame.Data, frame.Length);
                }
            }

            return true;
        }

        public bool OnDataFromPeer(ISocket sender, ITunnel tunnel, IPeer peer, byte[] data, int length)
        {
            if (_vni == null) return false;
            
            var ethernetPacket = new EthernetPacket(new ByteArraySegment(data, 0, length));

            switch (ethernetPacket.Type)
            {
                case EthernetType.Arp:
                    {
                        var arpFrame = ethernetPacket.Extract<ArpPacket>();

                        if (IsEnabled)
                        {
                            unchecked
                            {
                                _discards++;
                            }

                            return false;
                        }

                        if (arpFrame.Operation == ArpOperation.Request)
                        {
                            var fabricPeer = FabricPeers().FirstOrDefault(n => n.Peer.Equals(peer));
                            if (fabricPeer != null)
                            {
                                if (fabricPeer.MacAddress == null)
                                {
                                    fabricPeer.MacAddress = arpFrame.SenderHardwareAddress;
                                }
                            }
                        }

                        break;
                    }
                case EthernetType.IPv4:
                    {
                        if (IsEnabled)
                        {
                            var ipPacket = ethernetPacket.Extract<IPPacket>();

                            var fabricPeer = FabricPeers().FirstOrDefault(n => n.Peer.Equals(peer));
                            if (fabricPeer != null)
                            {
                                ethernetPacket.SourceHardwareAddress = fabricPeer.MacAddress;
                            }

                            if (ipPacket.DestinationAddress.Equals(Profile().VirtualAddress))
                            {
                                ethernetPacket.DestinationHardwareAddress = _vni.PhysicalAddress;
                            }
                        }

                        break;
                    }
            }

            return true;
        }

        public void Dispose()
        {
        }

        private IVirtualNetworkInterface _vni;

        private ulong _discards;

        private ulong _intercepts;
    }
    
    public interface ITest : IDisposable
    {
        string Author { get; }
        string Copyright { get; }
        List<PlatformType> Compatibility { get; }
        string Description { get; }
        Uri IconUrl { get; }
        Guid Id { get; }
        bool IsEnabled { get; set; }
        bool IsFaulted { get; set; }
        List<string> Languages { get; }
        string License { get; set; }
        Uri LicenseUrl { get; }
        string Owner { get; }
        ushort Priority { get; }
        string ProjectUrl { get; }
        string State { get; }
        List<string> Tags { get; }
        string Title { get; }
        Version Version { get; }

        AuthorisePeerDelegate AuthorisePeer { get; set; }
        DisablePeerDelegate DisablePeer { get; set; }
        EnablePeerDelegate EnablePeer { get; set; }
        Func<List<FabricPeer>> FabricPeers { get; set; }
        Func<Profile> Profile { get; set; }
        RemovePeerDelegate RemovePeer { get; set; }
        SendBroadcastDelegate SendBroadcast { get; set; }
        SendDataDelegate SendData { get; set; }
        SendMessageDelegate SendMessage { get; set; }
        WriteTapDelegate WriteTap { get; set; }

        void OnInit(string license = "");
        bool OnStart(IVirtualNetworkInterface virtualNetworkInterface);
        void OnStop();
        bool OnReload(string license = "");
        bool OnDataFromTap(ITapData frame);        
        bool OnDataFromPeer(ISocket sender, ITunnel tunnel, IPeer peer, byte[] data, int length);
    }

    public delegate void AuthorisePeerDelegate(string name, string description, bool isadministrativelydown = false, byte[] pinnedpublickey = null);
    public delegate void DisablePeerDelegate(string name);
    public delegate void EnablePeerDelegate(string name);
    public delegate void RemovePeerDelegate(string name);
    public delegate void SendBroadcastDelegate(byte[] data, int length);
    public delegate SocketError SendDataDelegate(IPeer peer, byte[] data, int length, int timeout = 15000);
    public delegate SocketError SendMessageDelegate(IPeer peer, uint controlCode, object message = null, int timeout = 15000);
    public delegate int WriteTapDelegate(byte[] data, int length);
}
