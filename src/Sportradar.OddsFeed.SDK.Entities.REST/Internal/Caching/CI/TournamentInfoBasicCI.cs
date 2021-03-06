﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Diagnostics.Contracts;
using System.Globalization;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO;
using Sportradar.OddsFeed.SDK.Messages;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.CI
{
    /// <summary>
    ///     Class TournamentInfoBasicCI
    /// </summary>
    /// <seealso cref="CacheItem" />
    public class TournamentInfoBasicCI : CacheItem
    {
        private readonly IDataRouterManager _dataRouterManager;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TournamentInfoBasicCI" /> class
        /// </summary>
        /// <param name="dto">The dto</param>
        /// <param name="culture">The culture</param>
        /// <param name="dataRouterManager">The <see cref="IDataRouterManager" /> used to fetch missing data</param>
        public TournamentInfoBasicCI(TournamentInfoDTO dto, CultureInfo culture, IDataRouterManager dataRouterManager)
            : base(dto.Id, dto.Name, culture)
        {
            Contract.Requires(dto != null);

            _dataRouterManager = dataRouterManager;
            if (dto.Category != null) Category = dto.Category.Id;
            if (dto.CurrentSeason != null)
                CurrentSeason = new CurrentSeasonInfoCI(dto.CurrentSeason, culture, _dataRouterManager);
        }

        /// <summary>
        ///     Gets the category
        /// </summary>
        /// <value>The category</value>
        public URN Category { get; }

        /// <summary>
        ///     Gets the current season
        /// </summary>
        /// <value>The current season</value>
        public CurrentSeasonInfoCI CurrentSeason { get; private set; }

        /// <summary>
        ///     Merges the specified dto
        /// </summary>
        /// <param name="dto">The dto</param>
        /// <param name="culture">The culture</param>
        public void Merge(TournamentInfoDTO dto, CultureInfo culture)
        {
            base.Merge(new CacheItem(dto.Id, dto.Name, culture), culture);

            if (dto.Category != null)
                if (!Category.Equals(dto.Category.Id))
                {
                    // WRONG
                }

            if (dto.CurrentSeason != null)
            {
                if (CurrentSeason == null)
                    CurrentSeason = new CurrentSeasonInfoCI(dto.CurrentSeason, culture, _dataRouterManager);
                else
                    CurrentSeason.Merge(dto.CurrentSeason, culture);
            }
        }
    }
}