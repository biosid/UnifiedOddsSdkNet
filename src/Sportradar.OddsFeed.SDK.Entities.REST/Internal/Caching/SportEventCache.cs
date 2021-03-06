/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Common.Logging;
using Metrics;
using Sportradar.OddsFeed.SDK.Common;
using Sportradar.OddsFeed.SDK.Common.Exceptions;
using Sportradar.OddsFeed.SDK.Common.Internal;
using Sportradar.OddsFeed.SDK.Common.Internal.Metrics;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.Events;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO.Lottery;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.Enums;
using Sportradar.OddsFeed.SDK.Messages;

// ReSharper disable InconsistentlySynchronizedField

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching
{
    /// <summary>
    ///     A implementation of the interface <see cref="ISportEventCache" />
    /// </summary>
    /// <seealso cref="ISportEventCache" />
    internal class SportEventCache : SdkCache, ISportEventCache, IHealthStatusProvider, IDisposable
    {
        /// <summary>
        ///     A <see cref="ILog" /> instance used for logging
        /// </summary>
        private static readonly ILog CacheLog = SdkLoggerFactory.GetLoggerForCache(typeof(SportEventCache));

        /// <summary>
        ///     A <see cref="object" /> to ensure thread safety when adding items to cache
        /// </summary>
        private readonly object _addLock = new object();

        /// <summary>
        ///     The list of all supported <see cref="CultureInfo" />
        /// </summary>
        private readonly IEnumerable<CultureInfo> _cultures;

        /// <summary>
        ///     The <see cref="IDataRouterManager" /> used to obtain data via REST request
        /// </summary>
        private readonly IDataRouterManager _dataRouterManager;

        /// <summary>
        ///     The list of dates already automatically loaded by the timer
        /// </summary>
        private readonly List<DateTime> _fetchedDates;

        /// <summary>
        ///     >A instance of <see cref="ISportEventCacheItemFactory" /> used to create new <see cref="SportEventCI" />
        /// </summary>
        private readonly ISportEventCacheItemFactory _sportEventCacheItemFactory;

        /// <summary>
        ///     A <see cref="ITimer" /> instance used to trigger periodic cache refresh-es
        /// </summary>
        private readonly ITimer _timer;

        /// <summary>
        ///     A <see cref="MemoryCache" /> which will be used to cache the data
        /// </summary>
        internal readonly MemoryCache Cache;

        /// <summary>
        ///     The special tournaments, which are not listed on All tournaments list, but are introduced by events on feed
        ///     messages
        /// </summary>
        internal readonly ConcurrentBag<URN> SpecialTournaments;

        /// <summary>
        ///     Value specifying whether the current instance is disposed
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SportEventCache" /> class
        /// </summary>
        /// <param name="cache">The in-memory cache of sport events</param>
        /// <param name="dataRouterManager">The <see cref="IDataRouterManager" /> used to obtain data</param>
        /// <param name="sportEventCacheItemFactory">
        ///     A instance of <see cref="ISportEventCacheItemFactory" /> used to create new
        ///     <see cref="SportEventCI" />
        /// </param>
        /// <param name="timer">The timer</param>
        /// <param name="cultures">A list of all supported languages</param>
        /// <param name="cacheManager">A <see cref="ICacheManager" /> used to interact among caches</param>
        public SportEventCache(MemoryCache cache,
            IDataRouterManager dataRouterManager,
            ISportEventCacheItemFactory sportEventCacheItemFactory,
            ITimer timer,
            IEnumerable<CultureInfo> cultures,
            ICacheManager cacheManager)
            : base(cacheManager)
        {
            Contract.Requires(cache != null);
            Contract.Requires(dataRouterManager != null);
            Contract.Requires(sportEventCacheItemFactory != null);
            Contract.Requires(timer != null);
            Contract.Requires(cultures.Any());

            Cache = cache;
            _dataRouterManager = dataRouterManager;
            _sportEventCacheItemFactory = sportEventCacheItemFactory;
            _cultures = cultures as IReadOnlyList<CultureInfo>;

            _fetchedDates = new List<DateTime>();

            _isDisposed = false;

            SpecialTournaments = new ConcurrentBag<URN>();

            _timer = timer;
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Registers the health check which will be periodically triggered
        /// </summary>
        public void RegisterHealthCheck()
        {
            HealthChecks.RegisterHealthCheck("SportEventCache", StartHealthCheck);
        }

        /// <summary>
        ///     Starts the health check and returns <see cref="HealthCheckResult" />
        /// </summary>
        public HealthCheckResult StartHealthCheck()
        {
            var keys = Cache.Select(w => w.Key).ToList();
            var details =
                $" [Match: {keys.Count(c => c.Contains("match"))}, Stage: {keys.Count(c => c.Contains("race") || c.Contains("stage"))}, Season: {keys.Count(c => c.Contains("season"))}, Tournament: {keys.Count(c => c.Contains("tournament"))}, Draw: {keys.Count(c => c.Contains("draw"))}, Lottery: {keys.Count(c => c.Contains("lottery"))}]";
            //var otherKeys = Cache.Where(w => w.Key.Contains("tournament")).Select(s=>s.Key);
            //CacheLog.Debug($"Tournament Ids: {string.Join(",", otherKeys)}");
            return Cache.Any()
                ? HealthCheckResult.Healthy($"Cache has {Cache.Count()} items{details}.")
                : HealthCheckResult.Unhealthy("Cache is empty.");
        }

        /// <summary>
        ///     Gets a <see cref="SportEventCI" /> instance representing cached sport event data
        /// </summary>
        /// <param name="id">A <see cref="URN" /> specifying the id of the sport event which cached representation to return</param>
        /// <returns>a <see cref="SportEventCI" /> instance representing cached sport event data</returns>
        public SportEventCI GetEventCacheItem(URN id)
        {
            Metric.Context("CACHE").Meter("SportEventCache->GetEventCacheItem", Unit.Calls);

            lock (_addLock)
            {
                try
                {
                    var item = (SportEventCI) Cache.Get(id.ToString());
                    if (item != null) return item;

                    item = _sportEventCacheItemFactory.Build(id);

                    AddNewCacheItem(item);

                    // if there are events for non-standard tournaments (tournaments not on All tournaments for all sports)
                    if (item is TournamentInfoCI && !SpecialTournaments.Contains(item.Id))
                        SpecialTournaments.Add(item.Id);

                    return item;
                }
                catch (Exception ex)
                {
                    ExecutionLog.Error($"Error getting cache item for id={id}", ex);
                }
            }

            return null;
        }

        /// <summary>
        ///     Asynchronous gets a <see cref="IEnumerable{URN}" /> containing id's of sport events, which belong to the specified
        ///     tournament
        /// </summary>
        /// <param name="tournamentId">A <see cref="URN" /> representing the tournament identifier</param>
        /// <returns>A <see cref="Task{T}" /> representing an asynchronous operation</returns>
        public async Task<IEnumerable<Tuple<URN, URN>>> GetEventIdsAsync(URN tournamentId)
        {
            Metric.Context("CACHE").Meter("SportEventCache->GetEventIdsAsync by tournamentId", Unit.Calls);

            Contract.Assume(_cultures.Any());

            var ci = _cultures.FirstOrDefault();
            Contract.Assume(ci != null);

            var schedule = await _dataRouterManager.GetSportEventsForTournamentAsync(tournamentId, ci, null)
                .ConfigureAwait(false);

            return schedule;
        }

        /// <summary>
        ///     Asynchronous gets a <see cref="IEnumerable{URN}" /> containing id's of sport events, which are scheduled for
        ///     specified date
        /// </summary>
        /// <param name="date">The date for which to retrieve the schedule, or a null reference to get currently live events</param>
        /// <returns>A <see cref="Task{T}" /> representing an asynchronous operation</returns>
        public async Task<IEnumerable<Tuple<URN, URN>>> GetEventIdsAsync(DateTime? date)
        {
            Metric.Context("CACHE").Meter("SportEventCache->GetEventIdsAsync by date", Unit.Calls);

            Contract.Assume(_cultures.Any());

            var culture = _cultures.FirstOrDefault();
            Contract.Assume(culture != null);

            var schedule = date == null
                ? await _dataRouterManager.GetLiveSportEventsAsync(culture).ConfigureAwait(false)
                : await _dataRouterManager.GetSportEventsForDateAsync(date.Value, culture).ConfigureAwait(false);

            return schedule;
        }

        /// <summary>
        ///     Adds fixture timestamp to cache so that the next fixture calls for the event goes through non-cached fixture
        ///     provider
        /// </summary>
        /// <param name="id">A <see cref="URN" /> representing the event</param>
        public void AddFixtureTimestamp(URN id)
        {
            var cache = _sportEventCacheItemFactory.GetFixtureTimestampCache();
            cache.Set(id.ToString(), id, DateTimeOffset.Now.AddMinutes(2));
        }

        /// <summary>
        ///     Set the list of <see cref="DtoType" /> in the this cache
        /// </summary>
        public override void SetDtoTypes()
        {
            RegisteredDtoTypes = new List<DtoType>
            {
                DtoType.Fixture,
                DtoType.MatchSummary,
                DtoType.MatchTimeline,
                DtoType.RaceSummary,
                DtoType.Sport,
                DtoType.SportList,
                DtoType.SportEventSummary,
                DtoType.SportEventSummaryList,
                DtoType.Tournament,
                DtoType.TournamentInfo,
                DtoType.TournamentSeasons,
                DtoType.Lottery,
                DtoType.LotteryDraw,
                DtoType.LotteryList,
                DtoType.BookingStatus
            };
        }

        /// <summary>
        ///     Purges item from cache
        /// </summary>
        /// <param name="id">A <see cref="URN" /> representing the id of the sport event which to be deleted</param>
        /// <param name="cacheItemType">The cache item type to be deleted</param>
        public override void CacheDeleteItem(URN id, CacheItemType cacheItemType)
        {
            lock (_addLock)
            {
                if (cacheItemType == CacheItemType.All
                    || cacheItemType == CacheItemType.SportEvent
                    || cacheItemType == CacheItemType.Tournament)
                    Cache.Remove(id.ToString());
            }
        }

        /// <summary>
        ///     Does item exists in the cache
        /// </summary>
        /// <param name="id">A <see cref="URN" /> representing the id of the item to be checked</param>
        /// <param name="cacheItemType">A cache item type</param>
        /// <returns><c>true</c> if exists, <c>false</c> otherwise</returns>
        public override bool CacheHasItem(URN id, CacheItemType cacheItemType)
        {
            lock (_addLock)
            {
                if (cacheItemType == CacheItemType.All
                    || cacheItemType == CacheItemType.SportEvent
                    || cacheItemType == CacheItemType.Tournament)
                    return Cache.Contains(id.ToString());
                return false;
            }
        }

        /// <summary>
        ///     Defines object invariants as required by code contracts
        /// </summary>
        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(Cache != null);
            Contract.Invariant(_dataRouterManager != null);
            Contract.Invariant(_sportEventCacheItemFactory != null);
            Contract.Invariant(SpecialTournaments != null);
        }

        /// <summary>
        ///     Invoked when the internally used timer elapses
        /// </summary>
        /// <param name="sender">A <see cref="object" /> representation of the <see cref="ITimer" /> raising the event</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data</param>
        private async void OnTimerElapsed(object sender, EventArgs e)
        {
            //check what needs to be fetched, then go fetched by culture, (not by date)
            var datesToFetch = new List<DateTime>();

            lock (_addLock)
            {
                var date = DateTime.Now;
                for (var i = 0; i < 3; i++)
                {
                    if (_fetchedDates.Any(d => (date - d).TotalDays < 1)) continue;

                    datesToFetch.Add(date);
                    _fetchedDates.Add(date);
                    date = date.AddDays(1);
                }
            }

            if (!datesToFetch.Any())
                return;

            var culturesToFetch = _cultures.ToDictionary(ci => ci, ci => datesToFetch);

            foreach (var key in culturesToFetch)
                try
                {
                    var tasks = key.Value.Select(d => GetScheduleAsync(d, key.Key)).ToList();
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (ObjectDisposedException ex)
                {
                    ExecutionLog.Warn(
                        $"Periodic events schedule retrieval failed because the instance {ex.ObjectName} is being disposed.");
                }
                catch (TaskCanceledException)
                {
                    ExecutionLog.Warn(
                        "Periodic events schedule retrieval failed because the instance is being disposed.");
                }
                catch (FeedSdkException ex)
                {
                    ExecutionLog.Warn(
                        $"An exception occurred while attempting to retrieve schedule. Exception was: {ex}");
                }
                catch (AggregateException ex)
                {
                    var baseException = ex.GetBaseException();
                    if (baseException.GetType() == typeof(ObjectDisposedException))
                        ExecutionLog.Warn(
                            $"Error happened during fetching schedule, because the instance {((ObjectDisposedException) baseException).ObjectName} is being disposed.");
                }
                catch (Exception ex)
                {
                    ExecutionLog.Warn($"An exception occurred while attempting to retrieve schedule. Exception: {ex}");
                }
        }

        /// <summary>
        ///     Asynchronously gets a sport event schedule specified by the <code>id</code> in the language specified by
        ///     <code>culture</code>
        /// </summary>
        /// <param name="date">The value specifying which schedule to get</param>
        /// <param name="culture">A <see cref="CultureInfo" /> specifying the language of the retrieved schedule</param>
        /// <returns>A <see cref="Task" /> representing the retrieval operation</returns>
        private async Task GetScheduleAsync(DateTime date, CultureInfo culture)
        {
            Contract.Requires(date != null && date > DateTime.MinValue);

            Metric.Context("CACHE").Meter("SportEventCache->GetScheduleAsync", Unit.Calls);

            var fetchedItem = await _dataRouterManager.GetSportEventsForDateAsync(date, culture).ConfigureAwait(false);

            CacheLog.Info(
                $"{fetchedItem.Count()} sport events retrieved for {date.ToShortDateString()} and locale '{culture.TwoLetterISOLanguageName}'.");
        }

        /// <summary>
        ///     Asynchronous gets a <see cref="IEnumerable{URN}" /> containing id's of sport events, which belong to the specified
        ///     tournament
        /// </summary>
        /// <param name="tournamentId">A <see cref="URN" /> representing the tournament identifier</param>
        /// <param name="cultures">A list of <see cref="CultureInfo" /> used to fetch schedules</param>
        /// <returns>A <see cref="Task{T}" /> representing an asynchronous operation</returns>
        public async Task GetEventIdsAsync(URN tournamentId, IEnumerable<CultureInfo> cultures)
        {
            Metric.Context("CACHE").Meter("SportEventCache->GetEventIdsAsync by tournamentId", Unit.Calls);

            var wantedCultures = cultures?.ToList() ?? _cultures.ToList();

            var tasks = wantedCultures.Select(s =>
                _dataRouterManager.GetSportEventsForTournamentAsync(tournamentId, s, null));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private void CacheItemRemovedCallback(CacheEntryRemovedArguments arguments)
        {
            if (arguments?.CacheItem == null) return;

            if (arguments.RemovedReason != CacheEntryRemovedReason.CacheSpecificEviction)
                CacheLog.Debug(
                    $"SportEventCI {arguments.CacheItem.Key} removed from cache. Reason={arguments.RemovedReason}.");

            Metric.Context("CACHE").Meter("SportEventCache->CacheItemRemovedCallback", Unit.Calls)
                .Mark(arguments.RemovedReason.ToString());
        }

        /// <summary>
        ///     Adds the item to the cache
        /// </summary>
        /// <param name="id">The identifier of the item</param>
        /// <param name="item">The item to be add</param>
        /// <param name="culture">The culture of the data-transfer-object</param>
        /// <param name="dtoType">Type of the cache item</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns><c>true</c> if is added/updated, <c>false</c> otherwise</returns>
        /// <exception cref="NotImplementedException"></exception>
        protected override bool CacheAddDtoItem(URN id, object item, CultureInfo culture, DtoType dtoType,
            ISportEventCI requester)
        {
            if (_isDisposed) return false;

            var saved = false;
            switch (dtoType)
            {
                case DtoType.Category:
                    break;
                case DtoType.Competitor:
                    break;
                case DtoType.CompetitorProfile:
                    break;
                case DtoType.SimpleTeamProfile:
                    break;
                case DtoType.Fixture:
                    var fixture = item as FixtureDTO;
                    if (fixture != null)
                    {
                        AddSportEvent(id, fixture, culture, requester, dtoType);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(FixtureDTO), item.GetType());
                    }

                    break;
                case DtoType.MarketDescription:
                    break;
                case DtoType.MatchSummary:
                    var match = item as MatchDTO;
                    if (match != null)
                    {
                        AddSportEvent(id, match, culture, requester, dtoType);
                        if (match.Tournament != null)
                        {
                            var ti = new TournamentInfoDTO(match.Tournament);
                            AddSportEvent(ti.Id, ti, culture, requester, dtoType);
                        }

                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(MatchDTO), item.GetType());
                    }

                    break;
                case DtoType.MatchTimeline:
                    var timeline = item as MatchTimelineDTO;
                    if (timeline != null)
                    {
                        AddMatchTimeLine(timeline, culture, requester, dtoType);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(MatchTimelineDTO), item.GetType());
                    }

                    break;
                case DtoType.PlayerProfile:
                    break;
                case DtoType.RaceSummary:
                    var stage = item as StageDTO;
                    if (stage != null)
                    {
                        AddSportEvent(id, stage, culture, requester, dtoType);
                        if (stage.Tournament != null)
                        {
                            var ti = new TournamentInfoDTO(stage.Tournament);
                            AddSportEvent(ti.Id, ti, culture, requester, dtoType);
                        }

                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(StageDTO), item.GetType());
                    }

                    break;
                case DtoType.Sport:
                    var sport = item as SportDTO;
                    if (sport != null)
                    {
                        SaveTournamentDataFromSport(sport, culture);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(SportDTO), item.GetType());
                    }

                    break;
                case DtoType.SportList:
                    var sportEntityList = item as EntityList<SportDTO>;
                    if (sportEntityList != null)
                    {
                        foreach (var sportDTO in sportEntityList.Items) SaveTournamentDataFromSport(sportDTO, culture);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(EntityList<SportDTO>), item.GetType());
                    }

                    break;
                case DtoType.SportEventStatus:
                    break;
                case DtoType.SportEventSummary:
                    var tourInfo = item as TournamentInfoDTO;
                    if (tourInfo != null)
                    {
                        SaveTournamentDataToSportEventCache(tourInfo, tourInfo.CurrentSeason?.Id, culture);
                        if (tourInfo.Season != null)
                            SaveTournamentDataToSportEventCache(tourInfo, tourInfo.Season?.Id, culture);
                        break;
                    }

                    var summary = item as SportEventSummaryDTO;
                    if (summary != null)
                    {
                        AddSportEvent(id, summary, culture, requester, dtoType);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(SportEventSummaryDTO), item.GetType());
                    }

                    break;
                case DtoType.SportEventSummaryList:
                    var summaryList = item as EntityList<SportEventSummaryDTO>;
                    if (summaryList != null)
                    {
                        foreach (var s in summaryList.Items)
                        {
                            var tourInfosDTO = s as TournamentInfoDTO;
                            if (tourInfosDTO != null)
                            {
                                SaveTournamentDataToSportEventCache(tourInfosDTO, tourInfosDTO.CurrentSeason?.Id,
                                    culture);
                                if (tourInfosDTO.Season != null)
                                    SaveTournamentDataToSportEventCache(tourInfosDTO, tourInfosDTO.Season?.Id, culture);
                                continue;
                            }

                            AddSportEvent(s.Id, s, culture, requester, dtoType);
                        }

                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(EntityList<SportEventSummaryDTO>), item.GetType());
                    }

                    break;
                case DtoType.Tournament:
                    var t = item as TournamentDTO;
                    if (t != null)
                    {
                        var ti = new TournamentInfoDTO(t);
                        AddSportEvent(id, ti, culture, requester, dtoType);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(TournamentDTO), item.GetType());
                    }

                    break;
                case DtoType.TournamentInfo:
                    var tour = item as TournamentInfoDTO;
                    if (tour != null)
                    {
                        AddSportEvent(id, tour, culture, requester, dtoType);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(TournamentInfoDTO), item.GetType());
                    }

                    break;
                case DtoType.TournamentSeasons:
                    var tourSeasons = item as TournamentSeasonsDTO;
                    if (tourSeasons?.Tournament != null)
                    {
                        AddSportEvent(id, tourSeasons.Tournament, culture, requester, dtoType);
                        var cacheItem = (TournamentInfoCI) _sportEventCacheItemFactory.Get(Cache.Get(id.ToString()));
                        cacheItem.Merge(tourSeasons, culture, true);

                        if (tourSeasons.Seasons != null && tourSeasons.Seasons.Any())
                            foreach (var season in tourSeasons.Seasons)
                                AddSportEvent(season.Id, new TournamentInfoDTO(season), culture, null, dtoType);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(TournamentInfoDTO), item.GetType());
                    }

                    break;
                case DtoType.MarketDescriptionList:
                    break;
                case DtoType.VariantDescription:
                    break;
                case DtoType.VariantDescriptionList:
                    break;
                case DtoType.Lottery:
                    var lottery = item as LotteryDTO;
                    if (lottery != null)
                    {
                        AddSportEvent(id, lottery, culture, requester, dtoType);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(TournamentInfoDTO), item.GetType());
                    }

                    break;
                case DtoType.LotteryDraw:
                    var draw = item as DrawDTO;
                    if (draw != null)
                    {
                        AddSportEvent(id, draw, culture, requester, dtoType);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(TournamentInfoDTO), item.GetType());
                    }

                    break;
                case DtoType.LotteryList:
                    var lotteryList = item as EntityList<LotteryDTO>;
                    if (lotteryList != null && lotteryList.Items.Any())
                    {
                        foreach (var l in lotteryList.Items) AddSportEvent(l.Id, l, culture, requester, dtoType);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(TournamentInfoDTO), item.GetType());
                    }

                    break;
                case DtoType.BookingStatus:
                    if (Cache.Contains(id.ToString()))
                    {
                        var e = Cache.Get(id.ToString());
                        var comp = e as CompetitionCI;
                        comp?.Book();
                    }

                    break;
                case DtoType.SportCategories:
                    break;
                default:
                    ExecutionLog.Warn($"Trying to add unchecked dto type:{dtoType} for id: {id}.");
                    break;
            }

            return saved;
        }

        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        ///     <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
        ///     unmanaged resources
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing) _timer.Stop();
            _isDisposed = true;
        }

        private void SaveTournamentDataFromSport(SportDTO sportDTO, CultureInfo culture)
        {
            if (sportDTO.Categories != null)
                foreach (var categoryData in sportDTO.Categories)
                    if (categoryData.Tournaments != null)
                        foreach (var tournamentData in categoryData.Tournaments)
                            SaveTournamentDataToSportEventCache(tournamentData, tournamentData.CurrentSeason?.Id,
                                culture);
        }

        private void SaveTournamentDataToSportEventCache(TournamentDTO tournamentDto, URN secondId, CultureInfo culture)
        {
            CacheAddDtoItem(tournamentDto.Id, tournamentDto, culture, DtoType.Tournament, null);

            if (secondId != null && !Equals(tournamentDto.Id, secondId))
            {
                var tourInfoDto = new TournamentInfoDTO(tournamentDto);
                var newTournamentDto = new TournamentInfoDTO(tourInfoDto, tourInfoDto.Season != null,
                    tourInfoDto.CurrentSeason != null);
                CacheAddDtoItem(secondId, newTournamentDto, culture, DtoType.TournamentInfo, null);
            }
        }

        private void SaveTournamentDataToSportEventCache(TournamentInfoDTO tournamentDto, URN secondId,
            CultureInfo culture)
        {
            CacheAddDtoItem(tournamentDto.Id, tournamentDto, culture, DtoType.TournamentInfo, null);

            if (secondId != null && !Equals(tournamentDto.Id, secondId))
            {
                var newTournamentDto = new TournamentInfoDTO(tournamentDto, tournamentDto.Season != null,
                    tournamentDto.CurrentSeason != null);
                CacheAddDtoItem(secondId, newTournamentDto, culture, DtoType.TournamentInfo, null);
            }
        }

        private void AddSportEvent(URN id, SportEventSummaryDTO item, CultureInfo culture, ISportEventCI requester,
            DtoType dtoType)
        {
            TournamentInfoDTO tournamentInfoDTO = null;
            lock (_addLock)
            {
                try
                {
                    var cacheItem = _sportEventCacheItemFactory.Get(Cache.Get(id.ToString()));

                    if (requester != null && !Equals(requester, cacheItem) && id.Equals(requester.Id))
                        try
                        {
                            var requesterMerged = false;
                            var fixture = item as FixtureDTO;
                            if (fixture != null)
                            {
                                if (requester.Id.TypeGroup == ResourceTypeGroup.MATCH)
                                    ((MatchCI) requester).MergeFixture(fixture, culture, true);
                                else if (requester.Id.TypeGroup == ResourceTypeGroup.STAGE)
                                    ((StageCI) requester).MergeFixture(fixture, culture, true);
                                else
                                    ((TournamentInfoCI) requester).MergeFixture(fixture, culture, true);
                                requesterMerged = true;
                            }

                            if (!requesterMerged)
                            {
                                var match = item as MatchDTO;
                                if (match != null)
                                {
                                    ((MatchCI) requester).Merge(match, culture, true);
                                    requesterMerged = true;
                                }
                            }

                            if (!requesterMerged)
                            {
                                var stage = item as StageDTO;
                                if (stage != null)
                                {
                                    ((StageCI) requester).Merge(stage, culture, true);
                                    requesterMerged = true;
                                }
                            }

                            if (!requesterMerged)
                            {
                                var tour = item as TournamentInfoDTO;
                                if (tour != null)
                                {
                                    var stageCI = requester as StageCI;
                                    if (stageCI != null)
                                    {
                                        stageCI.Merge(tour, culture, true);
                                        requesterMerged = true;
                                    }
                                    else
                                    {
                                        var tourCI = requester as TournamentInfoCI;
                                        if (tourCI != null)
                                        {
                                            tourCI.Merge(tour, culture, true);
                                            requesterMerged = true;
                                        }
                                    }
                                }
                            }

                            if (!requesterMerged)
                            {
                                var draw = item as DrawDTO;
                                if (draw != null)
                                {
                                    ((DrawCI) requester).Merge(draw, culture, true);
                                    requesterMerged = true;
                                }
                            }

                            if (!requesterMerged)
                            {
                                var lottery = item as LotteryDTO;
                                if (lottery != null)
                                {
                                    ((LotteryCI) requester).Merge(lottery, culture, true);
                                    requesterMerged = true;
                                }
                            }

                            if (!requesterMerged) requester.Merge(item, culture, true);
                        }
                        catch (Exception)
                        {
                            ExecutionLog.Debug(
                                $"Merging failed for {id} and item type: {item.GetType().Name} and dto type: {dtoType} for requester: {requester.Id}.");
                        }

                    if (cacheItem != null)
                    {
                        //ExecutionLog.Debug($"Saving OLD data for {id} and item type: {item.GetType().Name} and dto type: {dtoType}.");
                        var merged = false;
                        //var cacheItem = _sportEventCacheItemFactory.Get(Cache.Get(id.ToString()));
                        var fixture = item as FixtureDTO;
                        if (fixture != null)
                        {
                            if (cacheItem.Id.TypeGroup == ResourceTypeGroup.MATCH)
                                ((MatchCI) cacheItem).MergeFixture(fixture, culture, true);
                            else if (cacheItem.Id.TypeGroup == ResourceTypeGroup.STAGE)
                                ((StageCI) cacheItem).MergeFixture(fixture, culture, true);
                            else
                                ((TournamentInfoCI) cacheItem).MergeFixture(fixture, culture, true);
                            if (fixture.Tournament != null)
                                tournamentInfoDTO = new TournamentInfoDTO(fixture.Tournament);
                            merged = true;
                        }

                        if (!merged)
                        {
                            var stage = item as StageDTO;
                            if (stage != null)
                            {
                                ((StageCI) cacheItem).Merge(stage, culture, true);
                                merged = true;
                                if (stage.Tournament != null)
                                    tournamentInfoDTO = new TournamentInfoDTO(stage.Tournament);
                            }
                        }

                        if (!merged)
                        {
                            var tour = item as TournamentInfoDTO;
                            if (tour != null)
                            {
                                var stageCI = cacheItem as StageCI;
                                if (stageCI != null)
                                {
                                    stageCI.Merge(tour, culture, true);
                                    merged = true;
                                }
                                else
                                {
                                    var tourCI = cacheItem as TournamentInfoCI;
                                    if (tourCI != null)
                                    {
                                        tourCI.Merge(tour, culture, true);
                                        merged = true;
                                    }
                                }
                            }
                        }

                        if (!merged)
                        {
                            var match = item as MatchDTO;
                            if (match != null)
                            {
                                ((MatchCI) cacheItem).Merge(match, culture, true);
                                merged = true;
                                if (match.Tournament != null)
                                    tournamentInfoDTO = new TournamentInfoDTO(match.Tournament);
                            }
                        }

                        if (!merged)
                        {
                            var draw = item as DrawDTO;
                            if (draw != null)
                            {
                                ((DrawCI) cacheItem).Merge(draw, culture, true);
                                merged = true;
                            }
                        }

                        if (!merged)
                        {
                            var lottery = item as LotteryDTO;
                            if (lottery != null)
                            {
                                ((LotteryCI) cacheItem).Merge(lottery, culture, true);
                                merged = true;
                            }
                        }

                        if (!merged) cacheItem.Merge(item, culture, true);
                    }
                    else
                    {
                        //ExecutionLog.Debug($"Saving NEW data for {id} and item type: {item.GetType().Name} and dto type: {dtoType}.");
                        var ci = _sportEventCacheItemFactory.Build(item, culture);
                        if (dtoType == DtoType.SportEventSummary || dtoType == DtoType.LotteryDraw ||
                            dtoType == DtoType.MatchSummary)
                            ci.LoadedSummaries.Add(culture);
                        else if (dtoType == DtoType.Fixture) ci.LoadedFixtures.Add(culture);
                        AddNewCacheItem(ci);
                        if (!ci.Id.Equals(id))
                        {
                            var tInfo = item as TournamentInfoDTO;
                            if (tInfo != null)
                            {
                                var newTournamentDto = new TournamentInfoDTO(tInfo, tInfo.Season != null,
                                    tInfo.CurrentSeason != null);
                                var ci2 = _sportEventCacheItemFactory.Build(newTournamentDto, culture);
                                AddNewCacheItem(ci2);
                            }
                            else
                            {
                                var ci2 = _sportEventCacheItemFactory.Build(item, culture);
                                ci2.Id = id;
                                AddNewCacheItem(ci2);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExecutionLog.Error(
                        $"Error adding sport event for id={id}, dto type={item?.GetType().Name} and lang={culture.TwoLetterISOLanguageName}.",
                        ex);
                }
            }

            // if there are events for non-standard tournaments (tournaments not on All tournaments for all sports)
            // TODO: should we save/merge all, or just adding non-existing???
            if (tournamentInfoDTO != null && (SpecialTournaments.Contains(tournamentInfoDTO.Id) ||
                                              !Cache.Contains(tournamentInfoDTO.Id.ToString())))
            {
                if (SpecialTournaments.Contains(tournamentInfoDTO.Id))
                {
                    //ExecutionLog.Debug($"Updating tournament id={tournamentInfoDTO.Id}, introduced by event id={id} and lang=[{culture.TwoLetterISOLanguageName}].");
                }
                else
                {
                    //ExecutionLog.Debug($"Saving tournament id={tournamentInfoDTO.Id}, introduced by event id={id} and lang=[{culture.TwoLetterISOLanguageName}].");
                    SpecialTournaments.Add(tournamentInfoDTO.Id);
                }

                AddSportEvent(tournamentInfoDTO.Id, tournamentInfoDTO, culture, null, dtoType);
            }
        }

        private void AddMatchTimeLine(MatchTimelineDTO item, CultureInfo culture, ISportEventCI requester,
            DtoType dtoType)
        {
            AddSportEvent(item.SportEvent.Id, item.SportEvent, culture, requester, dtoType);

            lock (_addLock)
            {
                try
                {
                    UpdateMatchWithTimeline(item, culture);
                }
                catch (Exception ex)
                {
                    ExecutionLog.Error(
                        $"Error adding timeline for id={item.SportEvent.Id}, dto type={item.GetType().Name} and lang={culture.TwoLetterISOLanguageName}.",
                        ex);
                }
            }
        }

        private void UpdateMatchWithTimeline(MatchTimelineDTO item, CultureInfo culture)
        {
            if (item?.BasicEvents == null) return;

            var cacheItem = _sportEventCacheItemFactory.Get(Cache.Get(item.SportEvent.Id.ToString()));

            var matchCI = cacheItem as MatchCI;
            matchCI?.MergeTimeline(item, culture, true);
        }

        private void AddNewCacheItem(SportEventCI item)
        {
            Cache.Add(item.Id.ToString(), item, new CacheItemPolicy {RemovedCallback = CacheItemRemovedCallback});
        }
    }
}