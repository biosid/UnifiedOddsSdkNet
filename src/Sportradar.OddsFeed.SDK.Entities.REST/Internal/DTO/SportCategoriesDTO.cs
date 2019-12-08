﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Sportradar.OddsFeed.SDK.Messages.REST;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO
{
    /// <summary>
    ///     A data-transfer-object for sport categories
    /// </summary>
    public class SportCategoriesDTO
    {
        internal SportCategoriesDTO(sportCategoriesEndpoint categoriesEndpoint)
        {
            Contract.Requires(categoriesEndpoint != null);
            Contract.Requires(categoriesEndpoint.sport != null);

            Sport = new SportEntityDTO(categoriesEndpoint.sport.id, categoriesEndpoint.sport.name);
            Categories = categoriesEndpoint.categories
                ?.Select(c => new CategoryDTO(c.id, c.name, c.country_code, new List<tournamentExtended>())).ToList();
        }

        /// <summary>
        ///     Gets the <see cref="SportEntityDTO" /> specifying the the parent sport
        /// </summary>
        public SportEntityDTO Sport { get; }

        /// <summary>
        ///     Gets the <see cref="CategoryDTO" /> specifying associated categories
        /// </summary>
        public IEnumerable<CategoryDTO> Categories { get; }
    }
}