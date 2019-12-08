﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Diagnostics.Contracts;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.EntitiesImpl
{
    /// <summary>
    ///     An <see cref="INamedValue" /> implementation
    /// </summary>
    public class NamedValue : INamedValue
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NamedValue" /> class.
        /// </summary>
        /// <param name="id">The value associated with the current instance.</param>
        public NamedValue(int id)
        {
            Id = id;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NamedValue" /> class.
        /// </summary>
        /// <param name="id">The value associated with the current instance.</param>
        /// <param name="description">The description associated with the current instance.</param>
        public NamedValue(int id, string description)
        {
            Contract.Requires(!string.IsNullOrEmpty(description));

            Id = id;
            Description = description;
        }

        /// <summary>
        ///     Gets the value associated with the current instance
        /// </summary>
        public int Id { get; }

        /// <summary>
        ///     Gets the description associated with the current instance
        /// </summary>
        public string Description { get; protected set; }
    }
}