﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.CI
{
    /// <summary>
    ///     Provides information about venue of a sport event
    /// </summary>
    public class VenueCI : SportEntityCI
    {
        /// <summary>
        ///     A <see cref="IDictionary{TKey,TValue}" /> containing city of the venue in different languages
        /// </summary>
        private readonly IDictionary<CultureInfo, string> _cityNames;

        /// <summary>
        ///     A <see cref="IDictionary{CultureInfo, String}" /> containing country of the venue in different languages
        /// </summary>
        private readonly IDictionary<CultureInfo, string> _countryNames;

        /// <summary>
        ///     A <see cref="IDictionary{CultureInfo, String}" /> containing venue name in different languages
        /// </summary>
        private readonly IDictionary<CultureInfo, string> _names;

        /// <summary>
        ///     Initializes a new instance of the <see cref="VenueCI" /> class
        /// </summary>
        /// <param name="venue">A <see cref="VenueDTO" /> containing information about a venue</param>
        /// <param name="culture">A <see cref="CultureInfo" /> specifying the language of the <code>dto</code></param>
        internal VenueCI(VenueDTO venue, CultureInfo culture)
            : base(venue)
        {
            Contract.Requires(venue != null);
            Contract.Requires(culture != null);

            _names = new Dictionary<CultureInfo, string>();
            _countryNames = new Dictionary<CultureInfo, string>();
            _cityNames = new Dictionary<CultureInfo, string>();
            Merge(venue, culture);
        }

        /// <summary>
        ///     Gets the capacity of the venue associated with current <see cref="VenueCI" /> instance, or a null
        ///     reference if the capacity is not specified
        /// </summary>
        /// <value>The capacity.</value>
        public int? Capacity { get; private set; }

        /// <summary>
        ///     Gets a map coordinates specifying the exact location of the venue represented by current <see cref="VenueCI" />
        ///     instance
        /// </summary>
        public string Coordinates { get; private set; }

        /// <summary>
        ///     Gets a country code of the venue represented by current <see cref="VenueCI" /> instance
        /// </summary>
        public string CountryCode { get; private set; }

        /// <summary>
        ///     Merges information from the provided <see cref="VenueDTO" /> into the current instance
        /// </summary>
        /// <param name="venue">A <see cref="VenueDTO" /> containing information about a venue</param>
        /// <param name="culture">A <see cref="CultureInfo" /> specifying the language of the input <see cref="VenueDTO" /></param>
        internal void Merge(VenueDTO venue, CultureInfo culture)
        {
            Contract.Requires(venue != null);
            Contract.Requires(culture != null);

            Capacity = venue.Capacity;
            Coordinates = venue.Coordinates;
            _names[culture] = venue.Name;
            _countryNames[culture] = venue.Country;
            _cityNames[culture] = venue.City;
            CountryCode = venue.CountryCode;
        }

        /// <summary>
        ///     Gets the name of the venue in the specified language
        /// </summary>
        /// <param name="culture">A <see cref="CultureInfo" /> specifying the language of the returned name</param>
        /// <returns>The name of the venue in the specified language if it exists. Null otherwise.</returns>
        public string GetName(CultureInfo culture)
        {
            Contract.Requires(culture != null);

            return _names.ContainsKey(culture)
                ? _names[culture]
                : null;
        }


        /// <summary>
        ///     Gets the city name of the venue in the specified language
        /// </summary>
        /// <param name="culture">A <see cref="CultureInfo" /> specifying the language of the returned city name</param>
        /// <returns>The city name of the venue in the specified language if it exists. Null otherwise.</returns>
        public string GetCity(CultureInfo culture)
        {
            Contract.Requires(culture != null);

            return _cityNames.ContainsKey(culture)
                ? _cityNames[culture]
                : null;
        }

        /// <summary>
        ///     Gets the country name of the venue in the specified language
        /// </summary>
        /// <param name="culture">A <see cref="CultureInfo" /> specifying the language of the returned country name</param>
        /// <returns>The country name of the venue in the specified language if it exists. Null otherwise.</returns>
        public string GetCountry(CultureInfo culture)
        {
            Contract.Requires(culture != null);

            return _countryNames.ContainsKey(culture)
                ? _countryNames[culture]
                : null;
        }

        /// <summary>
        ///     Determines whether the current instance has translations for the specified languages
        /// </summary>
        /// <param name="cultures">A <see cref="IEnumerable{CultureInfo}" /> specifying the required languages</param>
        /// <returns>True if the current instance contains data in the required locals. Otherwise false.</returns>
        public bool HasTranslationsFor(IEnumerable<CultureInfo> cultures)
        {
            return cultures.All(c => _names.ContainsKey(c));
        }
    }
}