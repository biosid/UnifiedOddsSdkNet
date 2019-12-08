/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Diagnostics.Contracts;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.MarketNames
{
    internal class MarketSpecifierCacheItem
    {
        internal MarketSpecifierCacheItem(SpecifierDTO dto)
        {
            Contract.Requires(dto != null);

            Type = dto.Type;
            Name = dto.Name;
        }

        internal string Name { get; }

        internal string Type { get; }
    }

    internal class MarketAttributeCacheItem
    {
        internal MarketAttributeCacheItem(MarketAttributeDTO dto)
        {
            Contract.Requires(dto != null);

            Name = dto.Name;
            Description = dto.Description;
        }

        internal string Name { get; }

        internal string Description { get; }
    }
}