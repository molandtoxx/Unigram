﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core;
using Unigram.Services;
using Windows.UI.Popups;
using Template10.Utils;
using Unigram.Core.Common;

namespace Unigram.ViewModels
{
    public class DialogStickersViewModel : UnigramViewModelBase
    {
        public readonly IStickersService _stickersService;

        private TLMessagesStickerSet _frequentlyUsed;

        private bool _recentGifs;
        private bool _recentStickers;
        private bool _featured;
        private bool _stickers;

        public DialogStickersViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService)
            : base(protoService, cacheService, aggregator)
        {
            _stickersService = stickersService;
            _stickersService.RecentsDidLoaded += OnRecentsDidLoaded;
            _stickersService.StickersDidLoaded += OnStickersDidLoaded;
            _stickersService.FeaturedStickersDidLoaded += OnFeaturedStickersDidLoaded;

            _frequentlyUsed = new TLMessagesStickerSet
            {
                Set = new TLStickerSet
                {
                    Title = "Frequently used",
                    ShortName = "tg/recentlyUsed"
                }
            };

            SavedGifs = new ObservableCollection<TLDocument>();
            FeaturedStickers = new ObservableCollection<TLStickerSetMultiCovered>();
            SavedStickers = new ObservableCollectionEx<TLMessagesStickerSet>();

            SyncStickers();
            SyncGifs();
        }

        public IStickersService StickersService
        {
            get
            {
                return _stickersService;
            }
        }

        private void OnRecentsDidLoaded(object sender, RecentsDidLoadedEventArgs e)
        {
            if (e.IsGifs)
            {
                ProcessRecentGifs();
            }
            else if (e.Type == Services.StickersService.TYPE_IMAGE)
            {
                ProcessRecentStickers();
            }
        }

        private void OnStickersDidLoaded(object sender, StickersDidLoadedEventArgs e)
        {
            Debug.WriteLine("StickersDidLoaded");

            if (e.Type == Services.StickersService.TYPE_IMAGE)
            {
                ProcessStickers();
            }
        }

        private void OnFeaturedStickersDidLoaded(object sender, FeaturedStickersDidLoadedEventArgs e)
        {
            ProcessFeaturedStickers();
        }

        private void ProcessRecentGifs()
        {
            var recent = _stickersService.GetRecentGifs();
            Execute.BeginOnUIThread(() =>
            {
                SavedGifs.AddRange(recent, true);
            });
        }

        private void ProcessRecentStickers()
        {
            var recent = _stickersService.GetRecentStickers(Services.StickersService.TYPE_IMAGE);
            Execute.BeginOnUIThread(() =>
            {
                _frequentlyUsed.Documents = new TLVector<TLDocumentBase>(recent);

                if (SavedStickers.Count > 0 && SavedStickers[0].Set.ShortName.Equals("tg/recentlyUsed"))
                {
                    SavedStickers.RemoveAt(0);
                }

                if (_frequentlyUsed.Documents.Count > 0)
                {
                    SavedStickers.Insert(0, _frequentlyUsed);
                }
            });
        }

        private void ProcessStickers()
        {
            _stickers = true;
            var stickers = _stickersService.GetStickerSets(Services.StickersService.TYPE_IMAGE);
            Execute.BeginOnUIThread(() =>
            {
                SavedStickers.AddRange(stickers, true);

                if (_frequentlyUsed.Documents.Count > 0)
                {
                    SavedStickers.Insert(0, _frequentlyUsed);
                }
            });
        }

        private void ProcessFeaturedStickers()
        {
            _featured = true;
            var stickers = _stickersService.GetFeaturedStickerSets();
            Execute.BeginOnUIThread(() =>
            {
                FeaturedStickers.Clear();

                foreach (var set in stickers)
                {
                    FeaturedStickers.Add(new TLStickerSetMultiCovered
                    {
                        Set = set.Set,
                        Covers = new TLVector<TLDocumentBase>(set.Documents.Take(Math.Min(set.Documents.Count, 5)))
                    });
                }
            });
        }

        public int SavedGifsHash { get; private set; }
        public ObservableCollection<TLDocument> SavedGifs { get; private set; }

        public ObservableCollection<TLStickerSetMultiCovered> FeaturedStickers { get; private set; }

        public ObservableCollectionEx<TLMessagesStickerSet> SavedStickers { get; private set; }

        public void SyncStickers()
        {
            Execute.BeginOnThreadPool(() =>
            {
                _stickersService.LoadRecents(Services.StickersService.TYPE_IMAGE, false, true);
                var stickers = _stickersService.CheckStickers(Services.StickersService.TYPE_IMAGE);
                var featured = _stickersService.CheckFeaturedStickers();

                ProcessRecentStickers();
                if (stickers && !_stickers) ProcessStickers();
                if (featured && !_featured) ProcessFeaturedStickers();

                #region Old
                //var watch = Stopwatch.StartNew();

                //var response = await ProtoService.GetAllStickersAsync(0);
                //if (response.IsSucceeded)
                //{
                //    var old = DatabaseContext.Current.SelectStickerSets();

                //    var allStickers = response.Result as TLMessagesAllStickers;
                //    if (allStickers != null)
                //    {
                //        var needData = new Dictionary<long, TLStickerSet>();
                //        var ready = new Dictionary<long, TLStickerSet>();
                //        var removed = new List<TLStickerSet>();

                //        foreach (var set in allStickers.Sets)
                //        {
                //            var cached = old.FirstOrDefault(x => x.Id == set.Id);
                //            if (cached != null)
                //            {
                //                if (cached.Hash == set.Hash)
                //                {
                //                    ready[set.Id] = cached;
                //                }
                //                else
                //                {
                //                    needData[set.Id] = set;
                //                }
                //            }
                //            else
                //            {
                //                needData[set.Id] = set;
                //            }
                //        }

                //        foreach (var set in old)
                //        {
                //            if (needData.ContainsKey(set.Id) || ready.ContainsKey(set.Id)) { }
                //            else
                //            {
                //                removed.Add(set);
                //            }
                //        }

                //        if (removed.Count > 0)
                //        {
                //            DatabaseContext.Current.RemoveStickerSets(removed);
                //        }

                //        if (needData.Count > 0)
                //        {
                //            var results = new List<TLMessagesStickerSet>();
                //            var resultsSyncRoot = new object();
                //            ProtoService.GetStickerSetsAsync(new TLMessagesAllStickers { Sets = new TLVector<TLStickerSet>(needData.Values) },
                //                result =>
                //                {
                //                    Debugger.Break();
                //                    //DatabaseContext.Current.InsertStickerSets(needData.Values);
                //                },
                //                stickerSetResult =>
                //                {
                //                    var messagesStickerSet = stickerSetResult as TLMessagesStickerSet;
                //                    if (messagesStickerSet != null)
                //                    {
                //                        bool processStickerSets;
                //                        lock (resultsSyncRoot)
                //                        {
                //                            results.Add(messagesStickerSet);
                //                            processStickerSets = results.Count == needData.Values.Count;
                //                        }

                //                        if (processStickerSets)
                //                        {
                //                            DatabaseContext.Current.InsertStickerSets(results);
                //                            DatabaseContext.Current.UpdateStickerSetsOrder(allStickers.Sets);

                //                            //foreach (var item in ready)
                //                            //{
                //                            //    var items = DatabaseContext.Current.SelectDocuments("Stickers", item.Key);
                //                            //}

                //                            watch.Stop();
                //                            Execute.BeginOnUIThread(async () =>
                //                            {
                //                                await new MessageDialog(watch.Elapsed.ToString()).ShowQueuedAsync();
                //                            });
                //                        }
                //                    }
                //                },
                //                failure =>
                //                {
                //                    Debugger.Break();
                //                });
                //        }
                //        else
                //        {
                //            DatabaseContext.Current.UpdateStickerSetsOrder(allStickers.Sets);
                //        }
                //    }
                //}
                #endregion
            });
        }

        public void SyncGifs()
        {
            Execute.BeginOnThreadPool(() =>
            {
                _stickersService.LoadRecents(Services.StickersService.TYPE_IMAGE, true, true);

                ProcessRecentGifs();

                #region Old
                //var gifs = await _gifsService.GetSavedGifs();
                //if (gifs.Key != SavedGifsHash || SavedGifs.Count == 0)
                //{
                //    Execute.BeginOnUIThread(() =>
                //    {
                //        SavedGifsHash = gifs.Key;
                //        //SavedGifs.Clear();
                //        //SavedGifs.AddRange(gifs);

                //        if (SavedGifs.Count > 0)
                //        {
                //            for (int i = 0; i < gifs.Count; i++)
                //            {
                //                var user = gifs[i];
                //                var index = -1;

                //                for (int j = 0; j < SavedGifs.Count; j++)
                //                {
                //                    if (SavedGifs[j].Id == user.Id)
                //                    {
                //                        index = j;
                //                        break;
                //                    }
                //                }

                //                if (index > -1 && index != i)
                //                {
                //                    SavedGifs.RemoveAt(index);
                //                    SavedGifs.Insert(Math.Min(i, SavedGifs.Count), user);
                //                }
                //                else if (index == -1)
                //                {
                //                    SavedGifs.Insert(Math.Min(i, SavedGifs.Count), user);
                //                }
                //            }

                //            for (int i = 0; i < SavedGifs.Count; i++)
                //            {
                //                var user = SavedGifs[i];
                //                var index = -1;

                //                for (int j = 0; j < gifs.Count; j++)
                //                {
                //                    if (gifs[j].Id == user.Id)
                //                    {
                //                        index = j;
                //                        break;
                //                    }
                //                }

                //                if (index == -1)
                //                {
                //                    SavedGifs.Remove(user);
                //                    i--;
                //                }
                //            }
                //        }
                //        else
                //        {
                //            SavedGifs.Clear();
                //            SavedGifs.AddRange(gifs);
                //        }

                //        //var old = SavedGifs.ToArray();
                //        //if (old.Length > 0)
                //        //{
                //        //    var order = new Dictionary<int, int>();
                //        //    for (int i = 0; i < old.Length; i++)
                //        //    {
                //        //        order[i] = -1;

                //        //        for (int j = 0; j < gifs.Count; j++)
                //        //        {
                //        //            if (old[i].Id == gifs[j].Id)
                //        //            {
                //        //                order[i] = j;
                //        //                break;
                //        //            }
                //        //        }
                //        //    }

                //        //    //for (int j = 0; j < order.First().Value; j++)
                //        //    //{
                //        //    //    if (order.ContainsKey(j) == false)
                //        //    //    {
                //        //    //        order[j] = j;
                //        //    //    }
                //        //    //}

                //        //    foreach (var item in order)
                //        //    {
                //        //        if (item.Key != item.Value)
                //        //        {
                //        //            SavedGifs.RemoveAt(item.Key);
                //        //            SavedGifs.Insert(item.Value, gifs[item.Value]);
                //        //        }
                //        //    }

                //        //    //Debugger.Break();
                //        //}
                //        //else
                //        //{
                //        //    SavedGifs.Clear();
                //        //    SavedGifs.AddRange(gifs);
                //        //}
                //    });
                //}
                #endregion
            });
        }
    }
}
