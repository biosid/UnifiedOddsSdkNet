/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System;
using System.Diagnostics.Contracts;
using Sportradar.OddsFeed.SDK.Messages.REST;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO
{
    /// <summary>
    ///     A data-transfer-object representation for tv channel
    /// </summary>
    internal class TvChannelDTO
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TvChannelDTO" /> class
        /// </summary>
        /// <param name="tvChannel">The <see cref="tvChannel" /> used for creating instance</param>
        internal TvChannelDTO(tvChannel tvChannel)
        {
            Contract.Requires(tvChannel != null);
            Contract.Requires(!string.IsNullOrEmpty(tvChannel.name));

            Name = tvChannel.name;
            StartTime = tvChannel.start_timeSpecified
                ? (DateTime?) tvChannel.start_time
                : null;
            StreamUrl = tvChannel.stream_url;
        }

        internal string Name { get; }

        internal DateTime? StartTime { get; }

        internal string StreamUrl { get; }
    }
}