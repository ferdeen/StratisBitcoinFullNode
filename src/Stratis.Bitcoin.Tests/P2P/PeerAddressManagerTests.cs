using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Castle.Components.DictionaryAdapter;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.Utilities;
using Xunit;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerAddressManagerTests : TestBase
    {
        private readonly ExtendedLoggerFactory extendedLoggerFactory;

        public PeerAddressManagerTests()
        {
            this.extendedLoggerFactory = new ExtendedLoggerFactory();
            this.extendedLoggerFactory.AddConsoleWithFilters();
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerConnected()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory, new Mock<IPeerBanning>().Object);
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(endpoint, applicableDate);
            addressManager.PeerConnected(endpoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.Endpoint.Address.ToString());
            Assert.Equal(80, savedPeer.Endpoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerHandshaked()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, this.GetType().Name));

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory, new Mock<IPeerBanning>().Object);
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(endpoint, applicableDate);
            addressManager.PeerConnected(endpoint, applicableDate);
            addressManager.PeerHandshaked(endpoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.Endpoint.Address.ToString());
            Assert.Equal(80, savedPeer.Endpoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastConnectionHandshake.Value.Date);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerSeen()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, this.GetType().Name));

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory, new Mock<IPeerBanning>().Object);
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(endpoint, applicableDate);
            addressManager.PeerConnected(endpoint, applicableDate);
            addressManager.PeerHandshaked(endpoint, applicableDate);
            addressManager.PeerSeen(endpoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.Endpoint.Address.ToString());
            Assert.Equal(80, savedPeer.Endpoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastConnectionHandshake.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastSeen.Value.Date);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerAddressManager_AttemptThresholdReached_ResetAttempts()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, this.GetType().Name));
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory, new Mock<IPeerBanning>().Object);

            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var applicableDate = DateTimeProvider.Default.GetUtcNow();

            //Ensure that there was 10 failed attempts
            for (int i = 0; i < 10; i++)
            {
                addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-i));
            }

            //Ensure that the last attempt was more than 12 hours ago
            addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-13));

            //This call should now reset the counts
            var resetTimestamp = DateTimeProvider.Default.GetUtcNow();
            addressManager.PeerAttempted(endpoint, resetTimestamp);

            var savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal(1, savedPeer.ConnectionAttempts);
            Assert.Equal(resetTimestamp, savedPeer.LastAttempt);
            Assert.Null(savedPeer.LastConnectionSuccess);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Null(savedPeer.LastSeen);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerAddressManager_AttemptThresholdTimeNotReached_DoNotReset()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, this.GetType().Name));

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory, new Mock<IPeerBanning>().Object);
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var applicableDate = DateTimeProvider.Default.GetUtcNow();

            //Ensure that there was 10 failed attempts
            for (int i = 0; i < 10; i++)
            {
                addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-i));
            }

            //Capture the last attempt timestamp
            var lastAttempt = DateTimeProvider.Default.GetUtcNow();
            addressManager.PeerAttempted(endpoint, lastAttempt);

            var savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal(11, savedPeer.ConnectionAttempts);
            Assert.Equal(lastAttempt, savedPeer.LastAttempt);
            Assert.Null(savedPeer.LastConnectionSuccess);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Null(savedPeer.LastSeen);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerAddressMananger_BannedPeers_ShouldNotAppearInStore_AddingPeer()
        {
            // Arrange - Setup Peer and ban it using the PeerAddressManager
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");            
            var endpoint = new IPEndPoint(ipAddress, 80);
            PeerAddressManager addressManager = this.PeerAddressManager(ipAddress, endpoint);

            // Act
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            // Assert - Peer does not exist in the store as banned.
            PeerAddress savedPeer = addressManager.FindPeer(endpoint);
            Assert.Null(savedPeer);
        }

        [Fact]
        public void PeerAddressMananger_BannedPeers_ShouldNotAppearInStore_AddPeer()
        {
            var goodIPAddress = @"::ffff:192.170.0.1";

            // Arrange - Setup Peer and ban it using the PeerAddressManager
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var bannedEndPoint = new IPEndPoint(ipAddress, 80);
            PeerAddressManager addressManager = this.PeerAddressManager(ipAddress, bannedEndPoint);

            // Add good Peer
            ipAddress = IPAddress.Parse(goodIPAddress);
            var goodEndpoint = new IPEndPoint(ipAddress, 80);
            addressManager.AddPeer(goodEndpoint, ipAddress);

            // Act
            addressManager.SavePeers();
            addressManager.LoadPeers();

            // Assert - Peer does not exist in the store as banned.
            PeerAddress bannedPeer = addressManager.FindPeer(bannedEndPoint);
            Assert.Null(bannedPeer);

            // Assert - Good Peer does exist in the store
            PeerAddress goodPeer = addressManager.FindPeer(goodEndpoint);
            Assert.Equal(goodIPAddress, goodPeer.Endpoint.Address.ToString());
        }

        [Fact]
        public void PeerAddressMananger_BannedPeers_ShouldNotAppearInStore_AddPeers()
        {
            // Arrange - Setup Peer and ban it using the PeerAddressManager
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var bannedEndPoint = new IPEndPoint(ipAddress, 80);
            PeerAddressManager addressManager = this.PeerAddressManager(ipAddress, bannedEndPoint);

            // Add banned peer and two good peers.
            var peersToAdd = new List<IPEndPoint>()
            {
                bannedEndPoint,
                new IPEndPoint(IPAddress.Parse("::ffff:192.168.0.3"), 80),
                new IPEndPoint(IPAddress.Parse("::ffff:192.168.0.4"), 80),
            };

            addressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            // Act
            addressManager.SavePeers();
            addressManager.LoadPeers();

            // Assert - Peer does not exist in the store as banned.
            PeerAddress bannedPeer = addressManager.FindPeer(bannedEndPoint);
            Assert.Null(bannedPeer);

            // Assert - Good Peers exist in the store
            var peers = addressManager.PeerSelector.SelectPeersForGetAddrPayload(3);
            Assert.Equal(2, peers.Count());
        }

        private PeerAddressManager PeerAddressManager(IPAddress ipAddress, IPEndPoint endpoint)
        {
            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.Setup(np => np.PeerEndPoint).Returns(new IPEndPoint(ipAddress, 80));
            networkPeer.Setup(np => np.RemoteSocketAddress).Returns(ipAddress);
            networkPeer.Setup(np => np.RemoteSocketPort).Returns(80);
            networkPeer.Setup(np => np.State).Returns(NetworkPeerState.HandShaked);

            var connectionManager = new Mock<IConnectionManager>();
            var connectionManagerBehavior =
                new ConnectionManagerBehavior(false, connectionManager.Object, this.extendedLoggerFactory);
            networkPeer.Setup(p => p.Behavior<ConnectionManagerBehavior>()).Returns(connectionManagerBehavior);

            var networkPeerCollection = new NetworkPeerCollection {networkPeer.Object};
            IPeerBanning peerBanning =
                new PeerBanning(networkPeerCollection, this.extendedLoggerFactory, DateTimeProvider.Default);
            peerBanning.BanPeer(endpoint, 120, new StackTrace().GetFrame(0).GetMethod().Name);

            DataFolder peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, this.GetType().Name));
            var addressManager =
                new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, peerBanning);
            return addressManager;
        }
    }
}