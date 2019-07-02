﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sportradar.OddsFeed.SDK.API.Internal.Replay;
using Sportradar.OddsFeed.SDK.Common.Internal.Log;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal;
using Sportradar.OddsFeed.SDK.Messages.REST;
using Sportradar.OddsFeed.SDK.Test.Shared;
using System.Net.Http;

namespace Sportradar.OddsFeed.SDK.API.Test
{
    [TestClass]
    public class ReplayManagerTest
    {
        private IDataRestful _httpDataRestful;
        private IReplayManager _replayManager;

        [TestInitialize]
        public void Init()
        {
            var config = TestConfigurationInternal.GetConfig();

            object[] args =
            {
                new HttpClient(),
                config.AccessToken,
                new Deserializer<response>(),
                5,
                15
            };

            _httpDataRestful = LogProxyFactory.Create<IDataRestful>(args);

            object[] args2 =
            {
                "https://api.betradar.com/v1/replay",
                _httpDataRestful,
                0
            };
            _replayManager = LogProxyFactory.Create<IReplayManager>(args2);
        }

        [TestMethod]
        public void AddMessageToReplayQueue()
        {
            _replayManager.AddMessagesToReplayQueue(StaticRandom.Urn(9934843));
        }
    }
}
