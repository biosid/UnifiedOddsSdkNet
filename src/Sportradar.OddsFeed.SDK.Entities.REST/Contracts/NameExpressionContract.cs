﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Diagnostics.Contracts;
using System.Globalization;
using System.Threading.Tasks;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.MarketNames;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Contracts
{
    [ContractClassFor(typeof(INameExpression))]
    internal abstract class NameExpressionContract : INameExpression
    {
        public Task<string> BuildNameAsync(CultureInfo culture)
        {
            Contract.Ensures(Contract.Result<Task<string>>() != null);
            return Contract.Result<Task<string>>();
        }
    }
}