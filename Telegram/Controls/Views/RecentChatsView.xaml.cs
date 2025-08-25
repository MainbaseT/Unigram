//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Views
{
    public sealed partial class RecentChatsView : UserControl
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly Popup _popup;
        private readonly bool _fromStart;

        public RecentChatsView(IClientService clientService, INavigationService navigationService, Popup popup, bool fromStart)
        {
            _clientService = clientService;
            _navigationService = navigationService;
            _fromStart = fromStart;

            _popup = popup;
            _popup.XamlRoot.Content.PreviewKeyDown += OnKeyDown;
            _popup.XamlRoot.Content.PreviewKeyUp += OnKeyUp;
            _popup.Closed += OnClosed;

            InitializeComponent();
            InitializeChats(clientService);

            Loaded += OnLoaded;

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "Shadow"))
            {
                var themeShadow = new ThemeShadow();
                RootGrid.Shadow = themeShadow;
                RootGrid.Translation += new Vector3(0, 0, 32);

                themeShadow.Receivers.Add(ShadowReceiver);
            }
        }

        private void OnClosed(object sender, object e)
        {
            XamlRoot.Content.PreviewKeyDown -= OnKeyDown;
            XamlRoot.Content.PreviewKeyUp -= OnKeyUp;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Focus(FocusState.Pointer);

            if (ScrollingHost.ItemsSource != null)
            {
                ScrollingHost.SelectedIndex = _fromStart ? Math.Min(1, ScrollingHost.Items.Count - 1) : ScrollingHost.Items.Count - 1;
            }
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab)
            {
                var modifiers = WindowContext.KeyModifiers();
                if (modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift))
                {
                    if (ScrollingHost.SelectedIndex > 0)
                    {
                        ScrollingHost.SelectedIndex--;
                    }
                    else
                    {
                        ScrollingHost.SelectedIndex = ScrollingHost.Items.Count - 1;
                    }

                    e.Handled = true;
                }
                else if (modifiers == VirtualKeyModifiers.Control)
                {
                    if (ScrollingHost.SelectedIndex < ScrollingHost.Items.Count - 1)
                    {
                        ScrollingHost.SelectedIndex++;
                    }
                    else
                    {
                        ScrollingHost.SelectedIndex = 0;
                    }

                    e.Handled = true;
                }
            }
        }

        private void OnKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control)
            {
                _popup.IsOpen = false;

                if (ScrollingHost.SelectedItem is Chat chat)
                {
                    _clientService.Send(new AddRecentlyFoundChat(chat.Id));
                    _navigationService.NavigateToChat(chat, force: false, clearBackStack: true);
                }
            }
        }

        private async void InitializeChats(IClientService clientService)
        {
            var response = await clientService.SendAsync(new SearchRecentlyFoundChats(string.Empty, 50));
            if (response is Td.Api.Chats chats)
            {
                var items = new List<Chat>(chats.ChatIds.Count);
                var current = _navigationService.GetChatFromBackStack(true);

                if (clientService.TryGetChat(current.ChatId, out Chat currentChat))
                {
                    items.Add(currentChat);
                }

                foreach (var chat in clientService.GetChats(chats.ChatIds))
                {
                    if (chat.Id != current.ChatId)
                    {
                        items.Add(chat);
                    }
                }

                ScrollingHost.ItemsSource = items;

                if (IsLoaded)
                {
                    ScrollingHost.SelectedIndex = _fromStart ? 0 : items.Count - 1;
                }
            }
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem
                {
                    ContentTemplate = sender.ItemTemplate,
                    UseSystemFocusVisuals = false,
                    Margin = new Thickness(2)
                };

                args.ItemContainer.GotFocus += OnGotFocus;
            }

            args.IsContainerPrepared = true;
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            ScrollingHost.SelectedItem = ScrollingHost.ItemFromContainer(sender as DependencyObject);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is Chat chat)
            {
                var photo = content.Children[0] as ProfilePicture;
                var textBlock = content.Children[1] as TextBlock;

                photo.SetChat(_clientService, chat, 56);
                textBlock.Text = chat.Title;
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Chat chat)
            {
                _popup.IsOpen = false;

                _clientService.Send(new AddRecentlyFoundChat(chat.Id));
                _navigationService.NavigateToChat(chat, force: false, clearBackStack: true);
            }
        }
    }
}
