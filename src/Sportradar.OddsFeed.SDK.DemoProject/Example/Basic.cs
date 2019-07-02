/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System;
using System.Diagnostics.Contracts;
using Common.Logging;
using Sportradar.OddsFeed.SDK.API;
using Sportradar.OddsFeed.SDK.API.EventArguments;
using Sportradar.OddsFeed.SDK.Entities;
using Sportradar.OddsFeed.SDK.Entities.REST;

namespace Sportradar.OddsFeed.SDK.DemoProject.Example
{
    /// <summary>
    /// Basic Example with single session, generic dispatcher, basic output
    /// </summary>
    public class Basic
    {
        private readonly ILog _log;

        public Basic(ILog log)
        {
            _log = log;
        }

        public void Run(MessageInterest messageInterest)
        {
            Console.WriteLine("Running the OddsFeed SDK Basic example");

            Console.WriteLine("Retrieving configuration from application configuration file");
            var configuration = Feed.GetConfigurationBuilder().SetAccessTokenFromConfigFile().SelectIntegration()
                .LoadFromConfigFile().Build();
            //you can also create the IOddsFeedConfiguration instance by providing required values
            //var configuration = Feed.CreateConfiguration("myAccessToken", new[] {"en"});

            Console.WriteLine("Creating Feed instance");
            var oddsFeed = new Feed(configuration);

            Console.WriteLine("Creating IOddsFeedSession");
            var session = oddsFeed.CreateBuilder()
                .SetMessageInterest(messageInterest)
                .Build();

            Console.WriteLine("Attaching to feed events");
            AttachToFeedEvents(oddsFeed);
            AttachToSessionEvents(session);

            Console.WriteLine("Opening the feed instance");

            oddsFeed.Open();
            Console.WriteLine("Example successfully started. Hit <enter> to quit");
            Console.WriteLine(string.Empty);
            Console.ReadLine();

            Console.WriteLine("Closing / disposing the feed");
            oddsFeed.Close();

            DetachFromFeedEvents(oddsFeed);
            DetachFromSessionEvents(session);

            Console.WriteLine("Stopped");
        }

        /// <summary>
        /// Attaches to events raised by <see cref="IOddsFeed"/>
        /// </summary>
        /// <param name="oddsFeed">A <see cref="IOddsFeed"/> instance </param>
        private void AttachToFeedEvents(IOddsFeed oddsFeed)
        {
            Contract.Requires(oddsFeed != null);

            Console.WriteLine("Attaching to feed events");
            oddsFeed.ProducerUp += OnProducerUp;
            oddsFeed.ProducerDown += OnProducerDown;
            oddsFeed.Disconnected += OnDisconnected;
            oddsFeed.Closed += OnClosed;
        }

        /// <summary>
        /// Detaches from events defined by <see cref="IOddsFeed"/>
        /// </summary>
        /// <param name="oddsFeed">A <see cref="IOddsFeed"/> instance</param>
        private void DetachFromFeedEvents(IOddsFeed oddsFeed)
        {
            Contract.Requires(oddsFeed != null);

            Console.WriteLine("Detaching from feed events");
            oddsFeed.ProducerUp -= OnProducerUp;
            oddsFeed.ProducerDown -= OnProducerDown;
            oddsFeed.Disconnected -= OnDisconnected;
            oddsFeed.Closed -= OnClosed;
        }

        /// <summary>
        /// Attaches to events raised by <see cref="IOddsFeed"/>
        /// </summary>
        /// <param name="session">A <see cref="IOddsFeedSession"/> instance </param>
        private void AttachToSessionEvents(IOddsFeedSession session)
        {
            Contract.Requires(session != null);

            Console.WriteLine("Attaching to session events");
            session.OnUnparsableMessageReceived += SessionOnUnparsableMessageReceived;
            session.OnBetCancel += SessionOnBetCancel;
            session.OnBetSettlement += SessionOnBetSettlement;
            session.OnBetStop += SessionOnBetStop;
            session.OnFixtureChange += SessionOnFixtureChange;
            session.OnOddsChange += SessionOnOddsChange;
            session.OnRollbackBetCancel += SessionOnRollbackBetCancel;
            session.OnRollbackBetSettlement += SessionOnRollbackBetSettlement;
        }

        /// <summary>
        /// Detaches from events defined by <see cref="IOddsFeed"/>
        /// </summary>
        /// <param name="session">A <see cref="IOddsFeedSession"/> instance</param>
        private void DetachFromSessionEvents(IOddsFeedSession session)
        {
            Contract.Requires(session != null);

            Console.WriteLine("Detaching from session events");
            session.OnUnparsableMessageReceived -= SessionOnUnparsableMessageReceived;
            session.OnBetCancel -= SessionOnBetCancel;
            session.OnBetSettlement -= SessionOnBetSettlement;
            session.OnBetStop -= SessionOnBetStop;
            session.OnFixtureChange -= SessionOnFixtureChange;
            session.OnOddsChange -= SessionOnOddsChange;
            session.OnRollbackBetCancel -= SessionOnRollbackBetCancel;
            session.OnRollbackBetSettlement -= SessionOnRollbackBetSettlement;
        }

        private void SessionOnRollbackBetSettlement(object sender, RollbackBetSettlementEventArgs<ISportEvent> rollbackBetSettlementEventArgs)
        {
            var baseEntity = rollbackBetSettlementEventArgs.GetBetSettlementRollback();
            WriteSportEntity(baseEntity.GetType().Name, baseEntity.Event);
        }

        private void SessionOnRollbackBetCancel(object sender, RollbackBetCancelEventArgs<ISportEvent> rollbackBetCancelEventArgs)
        {
            var baseEntity = rollbackBetCancelEventArgs.GetBetCancelRollback();
            WriteSportEntity(baseEntity.GetType().Name, baseEntity.Event);
        }

        private void SessionOnOddsChange(object sender, OddsChangeEventArgs<ISportEvent> oddsChangeEventArgs)
        {
            var baseEntity = oddsChangeEventArgs.GetOddsChange();
            WriteSportEntity(baseEntity.GetType().Name, baseEntity.Event);
        }

        private void SessionOnFixtureChange(object sender, FixtureChangeEventArgs<ISportEvent> fixtureChangeEventArgs)
        {
            var baseEntity = fixtureChangeEventArgs.GetFixtureChange();
            WriteSportEntity(baseEntity.GetType().Name, baseEntity.Event);
        }

        private void SessionOnBetStop(object sender, BetStopEventArgs<ISportEvent> betStopEventArgs)
        {
            var baseEntity = betStopEventArgs.GetBetStop();
            WriteSportEntity(baseEntity.GetType().Name, baseEntity.Event);
        }

        private void SessionOnBetSettlement(object sender, BetSettlementEventArgs<ISportEvent> betSettlementEventArgs)
        {
            var baseEntity = betSettlementEventArgs.GetBetSettlement();
            WriteSportEntity(baseEntity.GetType().Name, baseEntity.Event);
        }

        private void SessionOnBetCancel(object sender, BetCancelEventArgs<ISportEvent> betCancelEventArgs)
        {
            var baseEntity = betCancelEventArgs.GetBetCancel();
            WriteSportEntity(baseEntity.GetType().Name, baseEntity.Event);
        }

        private void SessionOnUnparsableMessageReceived(object sender, UnparsableMessageEventArgs unparsableMessageEventArgs)
        {
            Console.WriteLine($"{unparsableMessageEventArgs.MessageType.GetType()} message came for event {unparsableMessageEventArgs.EventId}.");
        }

        /// <summary>
        /// Invoked when the connection to the feed is lost
        /// </summary>
        /// <param name="sender">The instance raising the event</param>
        /// <param name="e">The event arguments</param>
        private void OnDisconnected(object sender, EventArgs e)
        {
            _log.Warn("Connection to the feed lost");
        }

        /// <summary>
        /// Invoked when the the feed is closed
        /// </summary>
        /// <param name="sender">The instance raising the event</param>
        /// <param name="e">The event arguments</param>
        private void OnClosed(object sender, FeedCloseEventArgs e)
        {
            _log.Warn($"The feed is closed with the reason: {e.GetReason()}");
        }

        /// <summary>
        /// Invoked when a product associated with the feed goes down
        /// </summary>
        /// <param name="sender">The instance raising the event</param>
        /// <param name="e">The event arguments</param>
        private void OnProducerDown(object sender, ProducerStatusChangeEventArgs e)
        {
            _log.Warn($"Producer {e.GetProducerStatusChange().Producer} is down");
        }

        /// <summary>
        /// Invoked when a product associated with the feed goes up
        /// </summary>
        /// <param name="sender">The instance raising the event</param>
        /// <param name="e">The event arguments</param>
        private void OnProducerUp(object sender, ProducerStatusChangeEventArgs e)
        {
            Console.WriteLine($"Producer {e.GetProducerStatusChange().Producer} is up");
        }

        private void WriteSportEntity(string msgType, ISportEvent message)
        {
           Console.WriteLine($"{msgType.Replace("`1", string.Empty)} message for eventId {message.Id}");
        }
    }
}
