﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using Sportradar.OddsFeed.SDK.Messages;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.EntitiesImpl
{
    /// <summary>
    ///     Represents a goal scorer in a sport event
    /// </summary>
    /// <seealso cref="IPlayer" />
    internal class GoalScorer : BaseEntity, IGoalScorer
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IGoalScorer" /> class
        /// </summary>
        /// <param name="id">The <see cref="URN" /> uniquely identifying the current <see cref="IPlayer" /> instance</param>
        /// <param name="names">A <see cref="IDictionary{TKey,TValue}" /> containing player names in different languages</param>
        public GoalScorer(URN id, IDictionary<CultureInfo, string> names)
            : base(id, names as IReadOnlyDictionary<CultureInfo, string>)
        {
            Contract.Requires(id != null);
        }
    }
}