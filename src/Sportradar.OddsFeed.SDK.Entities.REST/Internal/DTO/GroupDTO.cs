﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using Sportradar.OddsFeed.SDK.Messages.REST;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO
{
    /// <summary>
    ///     A data-transfer-object representing a group
    /// </summary>
    public class GroupDTO
    {
        internal GroupDTO(tournamentGroup group)
        {
            Contract.Requires(group != null);

            Name = group.name ?? string.Empty;
            Competitors = group.competitor == null
                ? null
                : new ReadOnlyCollection<CompetitorDTO>(group.competitor.Select(c => new CompetitorDTO(c)).ToList());
        }

        internal string Name { get; }

        internal IEnumerable<CompetitorDTO> Competitors { get; }
    }
}