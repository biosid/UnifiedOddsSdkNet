﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Sportradar.OddsFeed.SDK.Common;
using Sportradar.OddsFeed.SDK.Common.Exceptions;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.InternalEntities;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.MarketNames;
using Sportradar.OddsFeed.SDK.Messages;

namespace Sportradar.OddsFeed.SDK.API.Internal
{
    /// <summary>
    /// The run-time implementation of the <see cref="IMarketDescriptionManager"/> interface
    /// </summary>
    internal class MarketDescriptionManager : IMarketDescriptionManager
    {
        //private readonly ILog _clientLog = SdkLoggerFactory.GetLoggerForClientInteraction(typeof(BookingManager));
        private readonly ILog _executionLog = SdkLoggerFactory.GetLoggerForExecution(typeof(MarketDescriptionManager));

        private readonly IOddsFeedConfigurationInternal _config;
        private readonly IMarketCacheProvider _marketCacheProvider;
        private readonly InvariantMarketDescriptionCache _invariantMarketDescriptionCache;
        private readonly ExceptionHandlingStrategy _exceptionHandlingStrategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarketDescriptionManager"/> class
        /// </summary>
        /// <param name="config">The <see cref="IOddsFeedConfigurationInternal"/> representing feed configuration</param>
        /// <param name="marketCacheProvider">A <see cref="IMarketCacheProvider"/> used to get market descriptions</param>
        /// <param name="invariantMarketDescriptionCache">A <see cref="IMarketDescriptionCache"/> used to get invariant market descriptions</param>
        public MarketDescriptionManager(IOddsFeedConfigurationInternal config, IMarketCacheProvider marketCacheProvider, IMarketDescriptionCache invariantMarketDescriptionCache)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (marketCacheProvider == null)
                throw new ArgumentNullException(nameof(marketCacheProvider));
            if (invariantMarketDescriptionCache == null)
                throw new ArgumentNullException(nameof(invariantMarketDescriptionCache));

            _config = config;
            _marketCacheProvider = marketCacheProvider;
            _invariantMarketDescriptionCache = invariantMarketDescriptionCache as InvariantMarketDescriptionCache;
            _exceptionHandlingStrategy = config.ExceptionHandlingStrategy;

            if (_invariantMarketDescriptionCache == null)
                throw new ArgumentException(nameof(invariantMarketDescriptionCache));
        }

        /// <summary>
        /// Asynchronously gets a <see cref="IEnumerable{IMarketDescription}"/> of all available static market descriptions
        /// </summary>
        /// <returns>A <see cref="IEnumerable{IMarketDescription}"/> of available static market descriptions</returns>
        public async Task<IEnumerable<IMarketDescription>> GetMarketDescriptionsAsync(CultureInfo culture = null)
        {
            try
            {
                if (culture == null)
                    culture = _config.DefaultLocale;
                return await _invariantMarketDescriptionCache.GetAllInvariantMarketDescriptionsAsync(new[] {culture}).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (_exceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                {
                    throw new ObjectNotFoundException($"Market descriptions({culture}) could not be provided", ex);
                }

                _executionLog.Warn($"Market descriptions with the {culture} locale could not be provided", ex);
                return null;
            }
        }

        /// <summary>
        /// Asynchronously gets a <see cref="IEnumerable{IMarketMappingData}"/> of available mappings for the provided marketId/producer combination
        /// </summary>
        /// <param name="marketId">The id of the market for which you need the mapping</param>
        /// <param name="producer">The <see cref="IProducer"/> for which you need the mapping</param>
        /// <returns>A <see cref="IEnumerable{IMarketMappingData}"/> of available mappings for the provided marketId/producer combination</returns>
        public async Task<IEnumerable<IMarketMappingData>> GetMarketMappingAsync(int marketId, IProducer producer)
        {
            return await GetMarketMappingAsync(marketId, null, producer).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously gets a <see cref="IEnumerable{IMarketMappingData}"/> of available mappings for the provided marketId/producer combination
        /// </summary>
        /// <param name="marketId">The id of the market for which you need the mapping</param>
        /// <param name="specifiers">The associated market specifiers</param>
        /// <param name="producer">The <see cref="IProducer"/> for which you need the mapping</param>
        /// <returns>A <see cref="IEnumerable{IMarketMappingData}"/> of available mappings for the provided marketId/producer combination</returns>
        public async Task<IEnumerable<IMarketMappingData>> GetMarketMappingAsync(int marketId, IReadOnlyDictionary<string, string> specifiers, IProducer producer)
        {
            IMarketDescription marketDescriptor;
            try
            {
                marketDescriptor = await _marketCacheProvider.GetMarketDescriptionAsync(marketId, specifiers, new[] { _config.DefaultLocale }, false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var specifiersMessage = specifiers != null ? $", specifiers: {string.Join("; ", specifiers)}" : "";
                if (_exceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                {
                    throw new ObjectNotFoundException($"Market mappings for {marketId} could not be provided{specifiersMessage}", ex);
                }

                _executionLog.Warn($"Market mappings for the marketId: {marketId} could not be provided{specifiersMessage}", ex);
                return null;
            }

            return marketDescriptor.Mappings?.Where(m => m.ProducerIds.Contains(producer.Id)).ToList() ?? Enumerable.Empty<IMarketMappingData>();
        }
    }
}
