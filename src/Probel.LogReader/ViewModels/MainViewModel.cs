﻿using Caliburn.Micro;
using Probel.LogReader.Core.Configuration;
using Probel.LogReader.Core.Constants;
using Probel.LogReader.Core.Filters;
using Probel.LogReader.Core.Helpers;
using Probel.LogReader.Core.Plugins;
using Probel.LogReader.Helpers;
using Probel.LogReader.Models;
using Probel.LogReader.Ui;
using Probel.LogReader.ViewModels.Packs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Probel.LogReader.ViewModels
{
    public class MainViewModel : Conductor<IScreen>, IHandle<UiEvent>
    {
        #region Fields

        private readonly IConfigurationManager _configurationManager;
        private readonly IFilterTranslator _filterTranslator;
        private readonly ILogger _log;
        private readonly ManageFilterViewModel _manageFilterViewModel;
        private readonly ManageRepositoryViewModel _manageRepositoryViewModel;
        private readonly IPluginInfoManager _pluginInfoManager;
        private readonly IPluginManager _pluginManager;
        private readonly IUserInteraction _userInteraction;
        private readonly DaysViewModel _vmDaysViewModel;
        private readonly LogsViewModel _vmLogsViewModel;
        private bool _isFilterVisible = false;
        private ObservableCollection<MenuItemModel> _menuFile;
        private ObservableCollection<MenuItemModel> _menuFilter;

        #endregion Fields

        #region Constructors

        public MainViewModel(IConfigurationManager cfg
            , IPluginInfoManager pluginInfoManager
            , IPluginManager pluginManager
            , IFilterTranslator filterTranslator
            , MainViewModelPack views
            , IEventAggregator eventAggregator
            , IUserInteraction userInteraction
            , ILogger log)
        {
            eventAggregator.Subscribe(this);

            _log = log;
            _configurationManager = cfg;
            _userInteraction = userInteraction;
            _pluginInfoManager = pluginInfoManager;
            _pluginManager = pluginManager;
            _filterTranslator = filterTranslator;
            _vmDaysViewModel = views.DaysViewModel;
            _vmLogsViewModel = views.LogsViewModel;
            _manageRepositoryViewModel = views.ManageRepositoryViewModel;
            _manageFilterViewModel = views.ManageFilterViewModel;
        }

        #endregion Constructors

        #region Properties

        public bool IsFilterVisible
        {
            get => _isFilterVisible;
            set => Set(ref _isFilterVisible, value, nameof(IsFilterVisible));
        }

        public ObservableCollection<MenuItemModel> MenuFilter
        {
            get => _menuFilter;
            set => Set(ref _menuFilter, value, nameof(MenuFilter));
        }

        public ObservableCollection<MenuItemModel> MenuRepository
        {
            get => _menuFile;
            set => Set(ref _menuFile, value, nameof(MenuRepository));
        }

        #endregion Properties

        #region Methods

        private void LoadDays(IPlugin plugin)
        {
            var waiter = _userInteraction.NotifyWait();

            var t1 = Task.Run(() =>
            {
                var r = plugin.GetDays();

                _vmDaysViewModel.Days = new ObservableCollection<DateTime>(r);
                _vmDaysViewModel.Plugin = plugin;

                _vmLogsViewModel.ClearCache();
            });
            t1.OnErrorHandleWith(r => _log.Error(r.Exception));

            var token = new CancellationToken();
            var sched = TaskScheduler.FromCurrentSynchronizationContext();

            var t2 = t1.ContinueWith(r =>
            {
                ActivateItem(_vmDaysViewModel);
                waiter.Dispose();
            }, token, TaskContinuationOptions.OnlyOnRanToCompletion, sched);
            t2.OnErrorHandleWith(r => _log.Error(r.Exception), token, sched);
        }

        private void LoadFilter(IFilterComposite filterComposite)
        {
            _vmLogsViewModel.ResetCache();
            var logs = filterComposite.Filter(_vmLogsViewModel.Logs);
            _vmLogsViewModel.Logs = new ObservableCollection<LogRow>(logs);
        }

        private IEnumerable<MenuItemModel> LoadMenuFilter(AppSettings app, IFilterManager fManager)
        {
            var menus = new List<MenuItemModel>();
            var aps = new AppSettingsDecorator(app);
            var filters = aps.GetFilters(OrderBy.Asc);
            foreach (var filter in filters)
            {
                menus.Add(new MenuItemModel
                {
                    Name = filter.Name ?? _filterTranslator.Translate(filter),
                    MenuCommand = new RelayCommand(() => LoadFilter(fManager.Build(filter.Id))),
                });
            }
            return menus;
        }

        private IEnumerable<MenuItemModel> LoadMenuRepository(AppSettings app)
        {
            var pil = _pluginInfoManager.GetPluginsInfo();
            var repositories = (from r in app.Repositories
                                where pil.Where(e => e.Id == r.PluginId).Count() > 0
                                select r).OrderBy(e => e.Name);

            var menus = new List<MenuItemModel>();
            foreach (var repo in repositories)
            {
                menus.Add(new MenuItemModel
                {
                    Name = repo.Name,
                    MenuCommand = new RelayCommand(() => LoadDays(_pluginManager.Build(repo)))
                });
            }
            return menus;
        }

        public void Handle(UiEvent message)
        {
            if (message.Event == UiEvents.RefreshMenus)
            {
                LoadMenus();
            }
            else if (message.Event == UiEvents.FilterVisibility && message.Context is bool isVisible)
            {
                IsFilterVisible = isVisible;
            }
        }

        public void LoadLogs(IPlugin plugin, DateTime day)
        {
            using (_userInteraction.NotifyWait())
            {
                var token = new CancellationToken();
                var scheduler = TaskScheduler.Current;

                var t1 = Task.Run(() =>
                {
                    var cfg = _configurationManager.Get();
                    var logs = plugin.GetLogs(day);

                    _vmLogsViewModel.IsLoggerVisible = cfg.Ui.ShowLogger;
                    _vmLogsViewModel.IsThreadIdVisible = cfg.Ui.ShowThreadId;
                    _vmLogsViewModel.Logs = new ObservableCollection<LogRow>(logs);
                    _vmLogsViewModel.RepositoryName = plugin.RepositoryName;

                    _vmLogsViewModel.GoBack = () => LoadDays(plugin);
                    //_vmLogsViewModel.RefreshData = () => LoadLogsAsync(plugin, day);
                    _vmLogsViewModel.Listener = plugin;

                    _vmLogsViewModel.IsFile = plugin.TryGetFile(out var path);
                    _vmLogsViewModel.CanListen = plugin.CanListen;
                    _vmLogsViewModel.FilePath = path;

                    _vmLogsViewModel.Cache(logs);
                });
                t1.OnErrorHandleWith(r => _log.Error(r.Exception));

                var t2 = t1.ContinueWith(r => ActivateItem(_vmLogsViewModel), token, TaskContinuationOptions.OnlyOnRanToCompletion, scheduler);
                t2.OnErrorHandleWith(r => _log.Error(r.Exception), token, scheduler);
            }
        }

        public void LoadMenus()
        {
            try
            {
                var t1 = Task.Run(() =>
                {
                    var app = _configurationManager.Get();
                    var fmanager = _configurationManager.BuildFilterManager();

                    var menuRepository = LoadMenuRepository(app);
                    var menuFilter = LoadMenuFilter(app, fmanager);

                    MenuRepository = new ObservableCollection<MenuItemModel>(menuRepository);
                    MenuFilter = new ObservableCollection<MenuItemModel>(menuFilter);
                });
                t1.OnErrorHandleWith(r => _log.Error(r.Exception));
            }
            catch (Exception ex) { throw ex; }
        }

        public void ManageFilters()
        {
            _manageFilterViewModel.Load();
            ActivateItem(_manageFilterViewModel);
        }

        public void ManageRepositories()
        {
            _manageRepositoryViewModel.Load();
            ActivateItem(_manageRepositoryViewModel);
        }

        #endregion Methods
    }
}