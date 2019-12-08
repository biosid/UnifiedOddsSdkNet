/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Collections.Generic;
using System.Linq;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.EntitiesImpl
{
    internal class PeriodStatistics : IPeriodStatistics
    {
        public PeriodStatistics(PeriodStatisticsDTO dto)
        {
            PeriodName = dto.PeriodName;
            TeamStatistics = dto.TeamStatisticsDTOs?.Select(s => new TeamStatistics(s));
        }

        // ReSharper disable once InconsistentNaming
        public PeriodStatistics(string periodName, IEnumerable<TeamStatisticsDTO> teamStatisticsDTOs)
        {
            PeriodName = periodName;
            TeamStatistics = teamStatisticsDTOs?.Select(s => new TeamStatistics(s));
        }

        public string PeriodName { get; }
        public IEnumerable<ITeamStatistics> TeamStatistics { get; }
    }
}