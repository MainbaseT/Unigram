//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Profile;
using Telegram.ViewModels.Stories;
using Telegram.Views.Chats;
using Telegram.Views.Popups;
using Telegram.Views.Profile;
using Windows.Foundation.Metadata;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class ProfilePage : HostedPage, IProfileDelegate, INavigablePage
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        private readonly DispatcherTimer _dateHeaderTimer;
        private Visual _dateHeaderPanel;
        private bool _dateHeaderCollapsed = true;

        public ProfilePage()
        {
            InitializeComponent();

            _dateHeaderTimer = new DispatcherTimer();
            _dateHeaderTimer.Interval = TimeSpan.FromMilliseconds(2000);
            _dateHeaderTimer.Tick += (s, args) =>
            {
                _dateHeaderTimer.Stop();
                ShowHideDateHeader(false, true);
            };

            InitializeScrolling();

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "Shadow"))
            {
                var themeShadow = new ThemeShadow();
                ToolTip.Shadow = themeShadow;
                ToolTip.Translation += new Vector3(0, 0, 32);

                themeShadow.Receivers.Add(ScrollingHost);
            }
        }

        public override HostedPagePositionBase GetPosition()
        {
            ViewModel.Delegate = null;
            return new HostedPageListViewPosition(DataContext, ScrollingHost.VerticalOffset, string.Empty);
        }

        public override void SetPosition(HostedPagePositionBase position)
        {
            if (position is HostedPageListViewPosition listViewPosition)
            {
                DataContext = listViewPosition.DataContext;
                ViewModel.Delegate = this;

                void handler(object sender, RoutedEventArgs e)
                {
                    ScrollingHost.Loaded -= handler;
                    ScrollingHost.ChangeView(null, listViewPosition.ScrollPosition, null, true);
                }

                ScrollingHost.Loaded += handler;
            }
        }

        private void InitializeScrolling()
        {
            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);
            var visual = ElementComposition.GetElementVisual(HeaderPanel);
            var border = ElementComposition.GetElementVisual(CardBackground);
            var menu = ElementComposition.GetElementVisual(MenuRoot);
            var media = ElementComposition.GetElementVisual(MediaFrame);
            media.Clip = media.Compositor.CreateInsetClip();

            ElementCompositionPreview.SetIsTranslationEnabled(HeaderPanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(BackButton, true);

            var translation = visual.Compositor.CreateExpressionAnimation(
                "properties.ActualHeight > 16 ? scrollViewer.Translation.Y > -(properties.ActualHeight + 8) ? 0 : -scrollViewer.Translation.Y - (properties.ActualHeight + 8) : -scrollViewer.Translation.Y");
            translation.SetReferenceParameter("scrollViewer", properties);
            translation.SetReferenceParameter("properties", ProfileHeader.Properties);

            var clip = visual.Compositor.CreateExpressionAnimation(
                "-scrollViewer.Translation.Y + 4");
            clip.SetReferenceParameter("scrollViewer", properties);
            clip.SetReferenceParameter("properties", ProfileHeader.Properties);

            //var fadeIn = visual.Compositor.CreateExpressionAnimation(
            //    "properties.ActualHeight > 16 ? scrollViewer.Translation.Y > -(properties.ActualHeight - 16) ? 0 : ((-scrollViewer.Translation.Y - (properties.ActualHeight - 16)) / 16) : 1");
            //fadeIn.SetReferenceParameter("scrollViewer", properties);
            //fadeIn.SetReferenceParameter("properties", ProfileHeader.Properties);

            visual.StartAnimation("Translation.Y", translation);
            media.Clip.StartAnimation("TopInset", clip);

            //border.StartAnimation("Opacity", fadeOut);

            menu.Opacity = 0;
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            if (MediaFrame.Content is INavigablePage tabPage)
            {
                tabPage.OnBackRequested(args);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            if (ViewModel.SelectedItem is ProfileTabItem tab)
            {
                MediaFrame.Navigate(tab.Type, tab.Parameter, new SuppressNavigationTransitionInfo());
            }

            if (ViewModel.IsSavedMessages)
            {
                ShowHideSubtitle(true);
            }

            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);

            var visual4 = ElementComposition.GetElementVisual(BackButton);
            visual4.CenterPoint = new Vector3(24, 16, 0);

            if (ProfileHeader.Visibility == Visibility.Visible && !ViewModel.IsSavedMessages)
            {
                var expOut2 = "clamp(1 - ((-(scrollViewer.Translation.Y + 148) / 32) * 0.2), 0.8, 1)";
                var slideOut2 = properties.Compositor.CreateExpressionAnimation($"vector3({expOut2}, {expOut2}, 1)");
                slideOut2.SetReferenceParameter("scrollViewer", properties);

                var slideOut3 = properties.Compositor.CreateExpressionAnimation("-clamp(((-(scrollViewer.Translation.Y + 148) / 32) * 16), 0, 16)");
                slideOut3.SetReferenceParameter("scrollViewer", properties);

                visual4.StartAnimation("Scale", slideOut2);
                visual4.StartAnimation("Translation.Y", slideOut3);
            }
            else
            {
                visual4.Scale = new Vector3(0.8f);
                visual4.Properties.InsertVector3("Translation", new Vector3(0, -16, 0));
            }

            ProfileHeader.InitializeScrolling(properties);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
            ViewModel.Delegate = null;

            if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, ref _itemsSourceToken);
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, ref _selectionModeToken);
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SharedCount") && ViewModel.SelectedItem is ProfileTabItem tab)
            {
                MediaFrame.Navigate(tab.Type, null, new SuppressNavigationTransitionInfo());
            }
        }

        #region Date visibility

        private void ShowHideDateHeader(bool show, bool animate)
        {
            if (_dateHeaderCollapsed != show)
            {
                return;
            }

            _dateHeaderCollapsed = !show;
            DateHeader.Visibility = show || animate ? Visibility.Visible : Visibility.Collapsed;

            _dateHeaderPanel ??= ElementComposition.GetElementVisual(DateHeader);

            if (!animate)
            {
                _dateHeaderPanel.Opacity = show ? 1 : 0;
                return;
            }

            var batch = _dateHeaderPanel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                DateHeader.Visibility = _dateHeaderCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var opacity = _dateHeaderPanel.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            _dateHeaderPanel.StartAnimation("Opacity", opacity);

            batch.End();
        }

        #endregion

        private void UpdateBackButton()
        {
            BackButton.RequestedTheme = ScrollingHost.VerticalOffset >= ProfileHeader.OccludedHeight
                ? ElementTheme.Default
                : ProfileHeader.HeaderTheme;
        }

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            ProfileHeader.UpdateChat(chat);

            UpdateBackButton();
        }

        public void UpdateChatTitle(Chat chat)
        {
            ProfileHeader.UpdateChatTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            ProfileHeader.UpdateChatPhoto(chat);
        }

        public void UpdateChatLastMessage(Chat chat)
        {
            ProfileHeader.UpdateChatLastMessage(chat);
        }

        public void UpdateChatEmojiStatus(Chat chat)
        {
            ProfileHeader.UpdateChatEmojiStatus(chat);
        }

        public void UpdateChatAccentColors(Chat chat)
        {
            ProfileHeader.UpdateChatAccentColors(chat);

            UpdateBackButton();
        }

        public void UpdateChatGifts(Chat chat)
        {
            // TODO: this should be optimized, not the best approach at all
            var item = ViewModel?.Items.FirstOrDefault(x => x.Type == typeof(ProfileGiftsTabPage));
            if (item != null)
            {
                var container = Navigation.ContainerFromItem(item) as SelectorItem;

                var grid = container?.Content as Grid;
                if (grid == null)
                {
                    return;
                }
                else if (grid.Children.Count == 4)
                {
                    return;
                }

                for (int i = 0; i < Math.Min(3, ViewModel.GiftsTab.Items.Count); i++)
                {
                    var gift = ViewModel.GiftsTab.Items[i] as ReceivedGift;
                    var animated = new AnimatedImage
                    {
                        Source = DelayedFileSource.FromSticker(ViewModel.ClientService, gift.GetSticker()),
                        Width = 20,
                        Height = 20,
                        FrameSize = new Windows.Foundation.Size(20, 20),
                        DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                        IsViewportAware = true,
                        LoopCount = 3,
                        Margin = new Thickness(4, 0, 0, 0),
                    };

                    Grid.SetColumn(animated, grid.Children.Count);
                    grid.Children.Add(animated);
                }

                container.Content = grid;
                container.ContentTemplate = null;

            }
        }

        public void UpdateChatActiveStories(Chat chat)
        {
            ProfileHeader.UpdateChatActiveStories(chat);
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            ProfileHeader.UpdateChatNotificationSettings(chat);
        }

        public void UpdateUser(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            ProfileHeader.UpdateUserFullInfo(chat, user, fullInfo, secret, accessToken);
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            ProfileHeader.UpdateUserStatus(chat, user);
        }



        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            ProfileHeader.UpdateSecretChat(chat, secretChat);
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ProfileHeader.UpdateBasicGroupFullInfo(chat, group, fullInfo);

            if (fullInfo != null)
            {
                ViewModel.Members = new SortedObservableCollection<ChatMember>(new ChatMemberComparer(ViewModel.ClientService, true), fullInfo.Members);
            }
        }



        public void UpdateSupergroup(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ProfileHeader.UpdateSupergroupFullInfo(chat, group, fullInfo);

            if (!group.IsChannel && (ViewModel.Members == null || group.MemberCount < 200 && group.MemberCount != ViewModel.Members.Count))
            {
                ViewModel.Members = ViewModel.CreateMembers(group.Id);
            }
        }

        #endregion

        private long _itemsSourceToken;
        private long _selectionModeToken;

        private void OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, ref _itemsSourceToken);
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, ref _selectionModeToken);
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is not ProfileTabPage tabPage)
            {
                return;
            }

            if (e.Content is ProfileSavedChatsTabPage or ProfileMediaTabPage or ProfileGiftsTabPage)
            {
                Menu.Visibility = Visibility.Visible;
            }
            else
            {
                Menu.Visibility = Visibility.Collapsed;
            }

            if (e.Content is ProfileStoriesTabPage)
            {
                if (e.Parameter is ChatStoriesType type)
                {
                    tabPage.DataContext = type == ChatStoriesType.Pinned
                        ? ViewModel.PinnedStoriesTab
                        : ViewModel.ArchivedStoriesTab;
                }
                else
                {
                    tabPage.DataContext = ViewModel.PinnedStoriesTab;
                }
            }

            if (tabPage.ScrollingHost.ItemsSource != null)
            {
                LoadMore(tabPage.ScrollingHost);
            }
            else
            {
                tabPage.ScrollingHost.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, OnItemsSourceChanged, ref _itemsSourceToken);
            }

            if (e.Content is not ProfileStoriesTabPage)
            {
                tabPage.ScrollingHost.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, OnSelectionModeChanged, ref _selectionModeToken);
            }

            if (_fromItemClick)
            {
                _fromItemClick = false;
                ScrollingHost.ChangeView(null, ProfileHeader.ActualHeight - 48 + 24, null);
            }
        }

        private void OnItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private void ProfileHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBackButton();

            ViewModel.HeaderHeight = Math.Max(e.NewSize.Height, 48 + 10);
            RootGrid.HeaderHeight = Math.Max((float)e.NewSize.Height - 88, 48 + 10);
            MediaFrame.MinHeight = ScrollingHost.ActualHeight + e.NewSize.Height - 88;
        }

        private Thumb _scrollingThumb;
        private Border _scrollingPanningThumb;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            MediaFrame.MinHeight = Header.ActualHeight + e.NewSize.Height - 88;

            var scrollBar = ScrollingHost.GetLastChild<ScrollBar>(x => x.Orientation == Orientation.Vertical);
            if (scrollBar != null && _scrollingThumb == null)
            {
                var rootGrid = scrollBar.GetChild<Grid>();
                if (rootGrid != null)
                {
                    var visualStateGroups = VisualStateManager.GetVisualStateGroups(rootGrid);
                    if (visualStateGroups != null)
                    {
                        foreach (var group in visualStateGroups)
                        {
                            group.CurrentStateChanged += ScrollingIndicatorStates_CurrentStateChanged;
                        }
                    }
                }

                _scrollingThumb = scrollBar.GetChild<Thumb>(x => x.Name == "VerticalThumb");
                _scrollingPanningThumb = scrollBar.GetChild<Border>(x => x.Name == "VerticalPanningThumb");
            }

            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private bool _scrollingIndicatorVisible;
        private bool _scrollingIndicatorEnabled;

        private void ScrollingIndicatorStates_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            switch (e.NewState.Name)
            {
                case "MouseIndicator":
                    BindToolTipToThumb(_scrollingThumb);

                    _scrollingIndicatorVisible = true;
                    ToolTip.Visibility = _scrollingIndicatorEnabled
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    break;
                case "TouchIndicator":
                    BindToolTipToThumb(_scrollingPanningThumb);

                    _scrollingIndicatorVisible = true;
                    ToolTip.Visibility = _scrollingIndicatorEnabled
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    break;
                case "NoIndicator":
                    _scrollingIndicatorVisible = false;
                    ToolTip.Visibility = Visibility.Collapsed;
                    break;

                // This part is needed to fix the visual state (that is still broken all across the app)
                case "Collapsed":
                    if (e.OldState.Name == "Expanded")
                    {
                        VisualStateManager.GoToState(ScrollingHost, "NoIndicator", true);
                    }
                    break;
            }
        }

        private void BindToolTipToThumb(UIElement element)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(ToolTip, true);

            var target = ElementComposition.GetElementVisual(element);

            if (element is not Thumb)
            {
                element = VisualTreeHelper.GetParent(element) as UIElement;
            }

            var visual = ElementComposition.GetElementVisual(ToolTip);
            var thumb = ElementComposition.GetElementVisual(element);

            var animation = visual.Compositor.CreateExpressionAnimation("max(thumb.Offset.Y + (target.Size.Y - this.Target.Size.Y) / 2, 8)");
            animation.SetReferenceParameter("thumb", thumb);
            animation.SetReferenceParameter("target", target);

            visual.StartAnimation("Translation.Y", animation);
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdateBackButton();

            var diff = ScrollingHost.VerticalOffset - (ProfileHeader.ActualSize.Y - 48);
            RootGrid.HeaderHeight = diff >= 0 && diff < 24
                ? Math.Max(ProfileHeader.ActualSize.Y - 24, 48 + 10)
                : -1;

            if (ProfileHeader.Visibility == Visibility.Visible && !ViewModel.IsSavedMessages)
            {
                ProfileHeader.ViewChanged(ScrollingHost.VerticalOffset);
                ShowHideSubtitle(ScrollingHost.VerticalOffset >= ProfileHeader.ActualHeight - 48);
            }

            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);

            var index = scrollingHost.ItemsPanelRoot switch
            {
                ItemsStackPanel stackPanel => stackPanel.FirstVisibleIndex,
                ItemsWrapGrid wrapGrid => wrapGrid.FirstVisibleIndex,
                _ => -1
            };

            if (index < 0 || index >= scrollingHost.Items.Count)
            {
                return;
            }

            if (scrollingHost.ItemsSource is MediaDataSource dataSource)
            {
                var offset = diff / ScrollingHost.ScrollableHeight;
                if (offset >= 0 && dataSource.HasPositions)
                {
                    var position = dataSource.GetByOffset(offset);
                    if (position != null)
                    {
                        ToolTipContent.Text = Formatter.Date(position.Date, Strings.formatterMonthYear);

                        _scrollingIndicatorEnabled = true;
                        ToolTip.Visibility = _scrollingIndicatorVisible
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }
                }
                else
                {
                    _scrollingIndicatorEnabled = false;
                    ToolTip.Visibility = Visibility.Collapsed;
                }

                _dateHeaderTimer.Stop();
                ShowHideDateHeader(false, false);
            }
            else
            {
                _scrollingIndicatorEnabled = false;
                ToolTip.Visibility = Visibility.Collapsed;

                var container = scrollingHost.Items[index];
                if (container is MessageWithOwner message)
                {
                    DateHeaderLabel.Text = Formatter.Date(message.Date, Strings.formatterMonthYear);
                }
                else if (container is StoryViewModel story)
                {
                    DateHeaderLabel.Text = Formatter.Date(story.Date, Strings.formatterMonthYear);
                }
                else
                {
                    return;
                }

                _dateHeaderTimer.Stop();
                _dateHeaderTimer.Start();
                ShowHideDateHeader(ScrollingHost.VerticalOffset > ProfileHeader.ActualHeight, true);
            }
        }

        private bool _subtitleCollapsed = true;

        private void ShowHideSubtitle(bool show)
        {
            if (_subtitleCollapsed != show)
            {
                return;
            }

            _subtitleCollapsed = !show;

            var cardBackground = ElementComposition.GetElementVisual(CardBackground);
            var menu = ElementComposition.GetElementVisual(MenuRoot);

            var opacityOut = cardBackground.Compositor.CreateScalarKeyFrameAnimation();
            opacityOut.InsertKeyFrame(0, show ? 1 : 0);
            opacityOut.InsertKeyFrame(1, show ? 0 : 1);

            var opacityIn = cardBackground.Compositor.CreateScalarKeyFrameAnimation();
            opacityIn.InsertKeyFrame(0, show ? 0 : 1);
            opacityIn.InsertKeyFrame(1, show ? 1 : 0);

            cardBackground.StartAnimation("Opacity", opacityOut);
            menu.StartAnimation("Opacity", opacityIn);

            ScrollingHost.SetVerticalPadding(0, 0);
        }

        private bool _loadingMore;

        private async void LoadMore(ListViewBase scrollingHost)
        {
            if (_loadingMore)
            {
                return;
            }

            _loadingMore = true;

            uint loadedMore = 0;
            int lastCacheIndex = scrollingHost.ItemsPanelRoot switch
            {
                ItemsStackPanel stackPanel => stackPanel.LastCacheIndex,
                ItemsWrapGrid wrapGrid => wrapGrid.LastCacheIndex,
                _ => -1
            };

            var needsMore = lastCacheIndex == scrollingHost.Items.Count - 1;
            needsMore |= scrollingHost.ActualHeight < ScrollingHost.ActualHeight;

            if (needsMore && scrollingHost.ItemsSource is ISupportIncrementalLoading supportIncrementalLoading && supportIncrementalLoading.HasMoreItems)
            {
                var result = await supportIncrementalLoading.LoadMoreItemsAsync(50);
                loadedMore = result.Count;
            }

            _loadingMore = false;

            if (loadedMore > 0)
            {
                LoadMore(scrollingHost);
            }
        }

        private bool _fromItemClick;

        private void Navigation_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (Navigation.SelectedItem == e.ClickedItem)
            {
                _fromItemClick = false;
                ScrollingHost.ChangeView(null, ProfileHeader.ActualHeight - 48 + 24, null);
            }
            else
            {
                _fromItemClick = true;
            }
        }

        private int _prevSelectedIndex = -1;

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Navigation.SelectedItem is ProfileTabItem page && (page.Parameter != null || page.Type != MediaFrame.Content?.GetType()))
            {
                Logger.Info(page.Type);

                NavigationTransitionInfo transition = _prevSelectedIndex == -1
                    ? new SuppressNavigationTransitionInfo()
                    : new SlideNavigationTransitionInfo
                    {
                        Effect = _prevSelectedIndex < Navigation.SelectedIndex
                            ? SlideNavigationTransitionEffect.FromRight
                            : SlideNavigationTransitionEffect.FromLeft
                    };

                _prevSelectedIndex = Navigation.SelectedIndex;
                MediaFrame.Navigate(page.Type, page.Parameter, transition);
            }
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (MediaFrame.Content is ProfileSavedChatsTabPage)
            {
                flyout.CreateFlyoutItem(ViewModel.SendMessage, Strings.SavedViewAsMessages, Icons.ChatEmpty);
            }
            else if (MediaFrame.Content is ProfileMediaTabPage)
            {
                var zoomIn = new MenuFlyoutItem
                {
                    Text = Strings.MediaZoomIn,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.ZoomIn)
                };

                var zoomOut = new MenuFlyoutItem
                {
                    Text = Strings.MediaZoomOut,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.ZoomOut)
                };

                var calendar = new MenuFlyoutItem
                {
                    Text = Strings.Calendar,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.Calendar)
                };

                var photos = new MenuFlyoutItem
                {
                    Text = Strings.MediaShowPhotos,
                    Icon = ViewModel.Media.Source.Filter is SearchMessagesFilterPhoto or SearchMessagesFilterPhotoAndVideo ? MenuFlyoutHelper.CreateIcon(Icons.Checkmark) : null
                };

                var videos = new MenuFlyoutItem
                {
                    Text = Strings.MediaShowVideos,
                    Icon = ViewModel.Media.Source.Filter is SearchMessagesFilterVideo or SearchMessagesFilterPhotoAndVideo ? MenuFlyoutHelper.CreateIcon(Icons.Checkmark) : null
                };

                zoomIn.Click += MediaZoomIn_Click;
                zoomOut.Click += MediaZoomOut_Click;
                calendar.Click += MediaCalendar_Click;

                photos.Click += MediaShowPhotos_Click;
                videos.Click += MediaShowVideos_Click;

                if (SettingsService.Current.Diagnostics.SparseMessagesDebug)
                {
                    flyout.Items.Add(zoomIn);
                    flyout.Items.Add(zoomOut);
                    flyout.Items.Add(calendar);
                    flyout.CreateFlyoutSeparator();
                }

                flyout.Items.Add(photos);
                flyout.Items.Add(videos);
            }
            else if (MediaFrame.Content is ProfileGiftsTabPage)
            {
                var sort = new MenuFlyoutItem
                {
                    Text = ViewModel.GiftsTab.SortByPrice
                        ? Strings.Gift2FilterSortByValue
                        : Strings.Gift2FilterSortByDate,
                    Icon = MenuFlyoutHelper.CreateIcon(ViewModel.GiftsTab.SortByPrice ? Icons.DollarArrowUp : Icons.CalendarArrowUp)
                };

                var unlimited = new MenuFlyoutItem
                {
                    Text = Strings.Gift2FilterUnlimited,
                    Icon = ViewModel.GiftsTab.ExcludeUnlimited ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                };

                var limited = new MenuFlyoutItem
                {
                    Text = Strings.Gift2FilterLimited,
                    Icon = ViewModel.GiftsTab.ExcludeLimited ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                };

                var unique = new MenuFlyoutItem
                {
                    Text = Strings.Gift2FilterUnique,
                    Icon = ViewModel.GiftsTab.ExcludeUpgraded ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                };

                sort.Click += (s, args) => ViewModel.GiftsTab.SortByPrice = !ViewModel.GiftsTab.SortByPrice;
                unlimited.Click += (s, args) => ViewModel.GiftsTab.ExcludeUnlimited = !ViewModel.GiftsTab.ExcludeUnlimited;
                limited.Click += (s, args) => ViewModel.GiftsTab.ExcludeLimited = !ViewModel.GiftsTab.ExcludeLimited;
                unique.Click += (s, args) => ViewModel.GiftsTab.ExcludeUpgraded = !ViewModel.GiftsTab.ExcludeUpgraded;

                flyout.Items.Add(sort);
                flyout.CreateFlyoutSeparator();
                flyout.Items.Add(unlimited);
                flyout.Items.Add(limited);
                flyout.Items.Add(unique);

                if (ViewModel.ClientService.IsSavedMessages(ViewModel.Chat) || ViewModel.ClientService.TryGetSupergroup(ViewModel.Chat, out Supergroup supergroup) && supergroup.CanPostMessages())
                {
                    var displayed = new ToggleMenuFlyoutItem
                    {
                        Text = Strings.Gift2FilterDisplayed,
                        IsChecked = !ViewModel.GiftsTab.ExcludeSaved
                    };

                    var hidden = new ToggleMenuFlyoutItem
                    {
                        Text = Strings.Gift2FilterHidden,
                        IsChecked = !ViewModel.GiftsTab.ExcludeUnsaved
                    };

                    displayed.Click += (s, args) => ViewModel.GiftsTab.ExcludeSaved = !ViewModel.GiftsTab.ExcludeSaved;
                    hidden.Click += (s, args) => ViewModel.GiftsTab.ExcludeUnsaved = !ViewModel.GiftsTab.ExcludeUnsaved;

                    flyout.CreateFlyoutSeparator();
                    flyout.Items.Add(displayed);
                    flyout.Items.Add(hidden);
                }
            }

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void MediaZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (MediaFrame.Content is ProfileMediaTabPage media)
            {
                media.Zoom(-1);
            }
        }

        private void MediaZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (MediaFrame.Content is ProfileMediaTabPage media)
            {
                media.Zoom(1);
            }
        }

        private async void MediaCalendar_Click(object sender, RoutedEventArgs e)
        {
            if (MediaFrame.Content is ProfileMediaTabPage media)
            {
                var popup = new CalendarPopup(ViewModel.ClientService, ViewModel.Chat.Id, ViewModel.Topic);
                popup.MaxDate = DateTimeOffset.Now.Date;

                var confirm = await ViewModel.ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary && popup.SelectedDates.Count > 0)
                {
                    var first = popup.SelectedDates.FirstOrDefault();
                    var offset = first.Date.ToTimestamp();

                    var closest = ViewModel.MediaSource.GetByDate(offset);
                    var panel = media.ScrollingHost.ItemsPanelRoot as ItemsWrapGrid;

                    int x = closest.Position % panel.MaximumRowsOrColumns;
                    int y = closest.Position / panel.MaximumRowsOrColumns;

                    ScrollingHost.ChangeView(null, ProfileHeader.ActualHeight - 48 + 24 + (y * panel.ItemHeight), null, true);
                }
            }
        }

        private void MediaShowPhotos_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Media.Source.Filter is SearchMessagesFilterPhotoAndVideo)
            {
                ViewModel.Media.UpdateSender(new SearchMessagesFilterVideo());
            }
            else if (ViewModel.Media.Source.Filter is SearchMessagesFilterVideo)
            {
                ViewModel.Media.UpdateSender(new SearchMessagesFilterPhotoAndVideo());
            }
        }

        private void MediaShowVideos_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Media.Source.Filter is SearchMessagesFilterPhotoAndVideo)
            {
                ViewModel.Media.UpdateSender(new SearchMessagesFilterPhoto());
            }
            else if (ViewModel.Media.Source.Filter is SearchMessagesFilterPhoto)
            {
                ViewModel.Media.UpdateSender(new SearchMessagesFilterPhotoAndVideo());
            }
        }

        #region Selection

        private string ConvertSelection(int count)
        {
            return Locale.Declension(Strings.R.messages, count);
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is ListViewBase selector)
            {
                ShowHideManagePanel(selector.SelectionMode == ListViewSelectionMode.Multiple);
            }
        }

        private bool _manageCollapsed = true;

        private void ShowHideManagePanel(bool show)
        {
            if (_manageCollapsed != show)
            {
                return;
            }

            _manageCollapsed = !show;
            ManagePanel.Visibility = Visibility.Visible;

            var manage = ElementComposition.GetElementVisual(ManagePanel);
            ElementCompositionPreview.SetIsTranslationEnabled(ManagePanel, true);
            manage.Opacity = show ? 0 : 1;

            var batch = manage.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ManagePanel.Visibility = _manageCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var offset1 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 48, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3(0, 0, 0));

            var opacity1 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);

            manage.StartAnimation("Translation", offset1);
            manage.StartAnimation("Opacity", opacity1);

            batch.End();
        }

        #endregion

        private void Navigation_PrepareContainerForItem(SelectorItem sender, object args)
        {
            if (args is ProfileTabItem item && item.Type == typeof(ProfileGiftsTabPage))
            {
                var textBlock = new TextBlock
                {
                    Text = item.Text,
                    Margin = new Thickness(0, 0, 4, 0)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(1, GridUnitType.Auto);
                grid.ColumnDefinitions.Add(1, GridUnitType.Auto);
                grid.ColumnDefinitions.Add(1, GridUnitType.Auto);
                grid.ColumnDefinitions.Add(1, GridUnitType.Auto);
                grid.Children.Add(textBlock);
                sender.Content = grid;
                sender.ContentTemplate = null;
            }
        }

    public class ProfileSnapGrid : Grid, IScrollSnapPointsInfo
    {
        public IReadOnlyList<float> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment alignment)
        {
            return new float[]
            {
                HeaderHeight
            };
        }

        public float GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment alignment, out float offset)
        {
            offset = 0;
            return 0;
        }

        private float _headerHeight;
        public float HeaderHeight
        {
            get => _headerHeight;
            set
            {
                if (_headerHeight != value)
                {
                    _headerHeight = value;
                    VerticalSnapPointsChanged?.Invoke(this, null);
                }
            }
        }

        public bool AreHorizontalSnapPointsRegular => false;

        public bool AreVerticalSnapPointsRegular => false;

        public event EventHandler<object> HorizontalSnapPointsChanged;
        public event EventHandler<object> VerticalSnapPointsChanged;
    }
}
