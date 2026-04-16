//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Popups
{
    public sealed partial class TextEditorPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly FormattedText _text;

        public TextEditorPopup(IClientService clientService, INavigationService navigationService, FormattedText text)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _text = text;

            //_variants = variants;
            //_itemsSource = new MvxObservableCollection<object>(variants.Models.Cast<object>());

            //OnTick(null, null);

            //if (gift.Gift is SentGiftUpgraded upgraded)
            //{
            //    UpgradedTitle.Text = upgraded.Gift.Title;
            //}

            //ScrollingHost.ItemsSource = _itemsSource;

            //_timer = new DispatcherTimer
            //{
            //    Interval = TimeSpan.FromSeconds(3)
            //};

            //_timer.Tick += OnTick;
            //_timer.Start();

            //UpgradedSubtitle.Text = Strings.Gift2PreviewRandomTraits;

            //TextBlockHelper.SetMarkdown(Info, Locale.Declension(Strings.R.GiftPreviewCountModels, _variants.Models.Count));

            TabStyleItems.ItemsSource = clientService.TextCompositionStyles;
            TabStyleOutput.SetText(clientService, text);

            Navigation.SelectedIndex = 1;
            Navigation.SelectionChanged += Navigation_SelectionChanged;
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabTranslate.Visibility = Navigation.SelectedIndex == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            TabStyle.Visibility = Navigation.SelectedIndex == 1
                ? Visibility.Visible
                : Visibility.Collapsed;
            TabFix.Visibility = Navigation.SelectedIndex == 2
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (Navigation.SelectedIndex == 2 && _fix == null)
            {
                InitializeFix();
            }
        }

        private async void InitializeFix()
        {
            _fix = new MessageTranslateResultPending();

            TabFixOriginal.SetText(_clientService, _text);

            var response = await _clientService.SendAsync(new FixTextWithAi(_text));
            if (response is FixedText text)
            {
                TabFixResult.Document.SetText(Windows.UI.Text.TextSetOptions.None, text.DiffText.Text);

                foreach (var diff in text.DiffText.Entities)
                {
                    var range = TabFixResult.Document.GetRange(diff.Offset, diff.Offset + diff.Length);
                    range.CharacterFormat.Underline = Windows.UI.Text.UnderlineType.Wave;
                }

                TabFixResult.IsReadOnly = true;
            }
        }

        private void TabStyleItems_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is TextCompositionStyle style)
            {
                var animated = content.Children[0] as AnimatedImage;
                var text = content.Children[1] as TextBlock;

                animated.Source = new CustomEmojiFileSource(_clientService, style.CustomEmojiId);
                text.Text = style.Title;
            }

            args.Handled = true;
        }

        private void TabStyleItems_PrepareContainerForItem(SelectorItem sender, object args)
        {
            if (sender.ContentTemplateRoot is Grid content && args is TextCompositionStyle style)
            {
                var animated = content.Children[0] as AnimatedImage;
                var text = content.Children[1] as TextBlock;

                animated.Source = new CustomEmojiFileSource(_clientService, style.CustomEmojiId);
                text.Text = style.Title;
            }
        }

        private Dictionary<string, MessageTranslateResult> _styles = new();
        private MessageTranslateResult _fix;

        private void TabStyleItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabStyleItems.SelectedItem is not TextCompositionStyle style)
            {
                return;
            }

            var addEmojis = TabStyleEmoji.IsChecked is true;
            var styleName = style.Name + "_" + addEmojis;

            if (_styles.TryGetValue(styleName, out MessageTranslateResult result))
            {
                if (result is MessageTranslateResultText text)
                {
                    TabStyleOutput.ShowHideSkeleton(false);
                    TabStyleOutput.SetText(_clientService, text.Text);
                }
            }
            else
            {
                TabStyleOutput.ShowHideSkeleton(true);
                TabStyleOutput.InvalidateArrange();

                _styles[styleName] = new MessageTranslateResultPending();
                _clientService.Send(new ComposeTextWithAi(_text, string.Empty, style.Name, addEmojis), result =>
                {
                    this.BeginOnUIThread(() =>
                    {
                        if (result is FormattedText text)
                        {
                            var styled = TextStyleRun.GetText(text);

                            _styles[styleName] = new MessageTranslateResultText(style.Name, styled);
                            TabStyleOutput.ShowHideSkeleton(false);
                            TabStyleOutput.SetText(_clientService, styled);
                        }
                        else
                        {
                            _styles[styleName] = new MessageTranslateResultError();
                        }
                    });
                });
            }
        }
    }

    public class TabStylePanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var maxWidth = 0d;
            var maxHeight = 0d;

            foreach (var child in Children)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }

            return new Size(Math.Max(availableSize.Width, maxWidth * Children.Count), maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = finalSize.Width / Children.Count;
            var x = 0d;

            foreach (var child in Children)
            {
                child.Arrange(new Rect(x, 0, width, finalSize.Height));
                x += width;
            }

            return finalSize;
        }
    }
}
