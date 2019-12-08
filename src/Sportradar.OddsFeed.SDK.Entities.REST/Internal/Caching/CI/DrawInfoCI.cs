﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Diagnostics.Contracts;
using Sportradar.OddsFeed.SDK.Entities.REST.Enums;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO.Lottery;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.CI
{
    /// <summary>
    ///     Defines a cache item object for lottery draw info
    /// </summary>
    internal class DrawInfoCI
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="DrawInfoCI" /> class
        /// </summary>
        /// <param name="dto">A <see cref="DrawInfoDTO" /> instance containing information about the draw info</param>
        public DrawInfoCI(DrawInfoDTO dto)
        {
            Contract.Requires(dto != null);

            DrawType = dto.DrawType;
            TimeType = dto.TimeType;
            GameType = dto.GameType;
        }

        /// <summary>
        ///     Gets the type of the draw
        /// </summary>
        /// <value>The type of the draw</value>
        public DrawType DrawType { get; }

        /// <summary>
        ///     Gets the type of the time
        /// </summary>
        /// <value>The type of the time</value>
        public TimeType TimeType { get; }

        /// <summary>
        ///     Gets the type of the game
        /// </summary>
        /// <value>The type of the game</value>
        public string GameType { get; }
    }
}